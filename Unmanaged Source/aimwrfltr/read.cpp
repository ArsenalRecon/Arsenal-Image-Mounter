#include "aimwrfltr.h"

NTSTATUS
AIMWrFltrRead(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
{
    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    if ((!device_extension->Statistics.IsProtected) ||
        (!device_extension->Statistics.Initialized &&
        !NT_SUCCESS(AIMWrFltrInitializeDiffDevice(device_extension))))
    {
        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }

    Irp->IoStatus.Information = 0;

    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    NTSTATUS status;

    InterlockedIncrement64(&device_extension->Statistics.ReadRequests);

    InterlockedExchangeAdd64(&device_extension->Statistics.ReadBytes,
        io_stack->Parameters.Read.Length);

    if (device_extension->DiffFileObject == NULL)
    {
        KeSetEvent(&device_extension->ListEvent, 0, FALSE);
    }

    if (io_stack->Parameters.Read.Length == 0)
    {
        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }

    if (io_stack->Parameters.Read.Length >
        device_extension->Statistics.LargestReadSize)
    {
        device_extension->Statistics.LargestReadSize =
            io_stack->Parameters.Read.Length;

        KdPrint(("AIMWrFltr: Largest read size is now %u KB\n",
            device_extension->Statistics.LargestReadSize >> 10));
    }

    LONGLONG highest_byte =
        io_stack->Parameters.Read.ByteOffset.QuadPart +
        io_stack->Parameters.Read.Length;

    if ((io_stack->Parameters.Read.ByteOffset.QuadPart >=
        device_extension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart) ||
        (highest_byte <= 0) ||
        (highest_byte > device_extension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart))
    {
        Irp->IoStatus.Status = STATUS_END_OF_MEDIA;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return STATUS_END_OF_MEDIA;
    }

    bool any_block_modified = false;
    LONG first = (LONG)
        DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Read.ByteOffset.QuadPart);
    LONG last = (LONG)
        DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Read.ByteOffset.QuadPart +
            io_stack->Parameters.Read.Length - 1);

    if ((device_extension->AllocationTable != NULL) &&
        (device_extension->DiffDeviceObject != NULL))
    {
        for (LONG i = first; i <= last; i++)
        {
            if (device_extension->AllocationTable[i] !=
                DIFF_BLOCK_UNALLOCATED)
            {
                any_block_modified = true;
                break;
            }
        }
    }

    if (!any_block_modified)
    {
        InterlockedIncrement64(
            &device_extension->Statistics.ReadRequestsReroutedToOriginal);

        InterlockedExchangeAdd64(
            &device_extension->Statistics.ReadBytesReroutedToOriginal,
            io_stack->Parameters.Read.Length);

        IoSkipCurrentIrpStackLocation(Irp);
        return IoCallDriver(device_extension->TargetDeviceObject, Irp);
    }

    bool defer_to_worker_thread = false;

    KIRQL current_irql = KeGetCurrentIrql();

    if (current_irql >= DISPATCH_LEVEL)
    {
        defer_to_worker_thread = true;
    }

    if (defer_to_worker_thread)
    {
        InterlockedIncrement64(
            &device_extension->Statistics.DeferredReadRequests);

        InterlockedExchangeAdd64(&device_extension->Statistics.DeferredReadBytes,
            io_stack->Parameters.Read.Length);

        //
        // Acquire the remove lock so that device will not be removed while
        // processing this irp.
        //
        status = IoAcquireRemoveLock(&device_extension->RemoveLock, Irp);

        if (!NT_SUCCESS(status))
        {
            DbgPrint(
                "AIMWrFltrRead: Remove lock failed read type Irp: 0x%X\n",
                status);

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);

            KdBreakPoint();

            return status;
        }

        IoMarkIrpPending(Irp);

        KLOCK_QUEUE_HANDLE lock_handle;

        AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
            current_irql);

        InsertTailList(&device_extension->ListHead,
            &Irp->Tail.Overlay.ListEntry);

        AIMWrFltrReleaseLock(&lock_handle, &current_irql);

        KeSetEvent(&device_extension->ListEvent, 0, FALSE);

        return STATUS_PENDING;
    }

    PSCATTERED_IRP scatter;

    status = SCATTERED_IRP::Create(
        &scatter,
        DeviceObject,
        Irp,
        &device_extension->RemoveLock,
        (DeviceObject->Flags & DO_BUFFERED_IO) ?
        (PUCHAR)Irp->AssociatedIrp.SystemBuffer :
        NULL);

    if (!NT_SUCCESS(status))
    {
        Irp->IoStatus.Status = status;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        KdBreakPoint();

        return status;
    }

    ULONG length_done = 0;
    ULONG splits = 0;

    for (
        LONG i = first;
        (i <= last) && (length_done < io_stack->Parameters.Read.Length);
        i++)
    {
        LONGLONG abs_offset_this_iter =
            io_stack->Parameters.Read.ByteOffset.QuadPart + length_done;
        ULONG page_offset_this_iter =
            DIFF_GET_BLOCK_OFFSET(abs_offset_this_iter);
        ULONG bytes_this_iter =
            io_stack->Parameters.Read.Length - length_done;
        LARGE_INTEGER lower_offset = { 0 };
        PDEVICE_OBJECT lower_device = NULL;
        PFILE_OBJECT lower_file = NULL;

        if (device_extension->AllocationTable[i] == DIFF_BLOCK_UNALLOCATED)
        {
            ULONG block_size = DIFF_BLOCK_SIZE;

            // Contiguous? Then merge with next iteration
            while ((page_offset_this_iter + bytes_this_iter) > block_size)
            {
                if (device_extension->AllocationTable[i + 1] ==
                    DIFF_BLOCK_UNALLOCATED)
                {
                    block_size += DIFF_BLOCK_SIZE;
                    ++i;
                }
                else
                {
                    bytes_this_iter = block_size - page_offset_this_iter;
                    ++splits;
                }
            }

            InterlockedExchangeAdd64(
                &device_extension->Statistics.ReadBytesFromOriginal,
                bytes_this_iter);

            lower_device = device_extension->TargetDeviceObject;
            lower_offset.QuadPart = abs_offset_this_iter;
        }
        else
        {
            ULONG block_size = DIFF_BLOCK_SIZE;
            LONG block_base = device_extension->AllocationTable[i];

            // Contiguous? Then merge with next iteration
            while ((page_offset_this_iter + bytes_this_iter) > block_size)
            {
                if (device_extension->AllocationTable[i + 1] ==
                    device_extension->AllocationTable[i] + 1)
                {
                    block_size += DIFF_BLOCK_SIZE;
                    ++i;
                }
                else
                {
                    bytes_this_iter = block_size - page_offset_this_iter;
                }
            }

            InterlockedExchangeAdd64(&device_extension->Statistics.ReadBytesFromDiff,
                bytes_this_iter);

            lower_device = device_extension->DiffDeviceObject;
            lower_file = device_extension->DiffFileObject;

            lower_offset.QuadPart =
                ((LONGLONG)block_base << DIFF_BLOCK_BITS) +
                page_offset_this_iter;
        }

        __analysis_assume(lower_device != NULL);

        PIRP lower_irp = scatter->BuildIrp(
            IRP_MJ_READ,
            lower_device,
            lower_file,
            length_done,
            bytes_this_iter,
            &lower_offset);

        if (lower_irp == NULL)
        {
            break;
        }

        ASSERT(IoGetNextIrpStackLocation(lower_irp)->FileObject == lower_file);

        ASSERT((lower_device == device_extension->TargetDeviceObject &&
            lower_file == NULL) ||
            (lower_file == device_extension->DiffFileObject &&
                lower_device == device_extension->DiffDeviceObject &&
                lower_device == IoGetRelatedDeviceObject(lower_file)));

        IoCallDriver(lower_device, lower_irp);

        length_done += bytes_this_iter;
    }

    if (splits > 0)
    {
        InterlockedExchangeAdd64(&device_extension->Statistics.SplitReads, splits);
    }

    // Decrement reference counter and complete if all partials are finished
    scatter->Complete();

    return STATUS_PENDING;
}				// end AIMWrFltrReadWrite()

VOID
AIMWrFltrDeferredRead(
    PDEVICE_EXTENSION DeviceExtension,
    PIRP Irp,
    PUCHAR BlockBuffer)
{
    KEVENT event;
    KeInitializeEvent(&event, SynchronizationEvent, FALSE);

    PUCHAR buffer = NULL;
    if (DeviceExtension->DeviceObject->Flags & DO_BUFFERED_IO)
    {
        buffer = (PUCHAR)Irp->AssociatedIrp.SystemBuffer;
    }
    else
    {
        buffer = (PUCHAR)MmGetSystemAddressForMdlSafe(
            Irp->MdlAddress, NormalPagePriority);

        if (buffer == NULL)
        {
            Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
            return;
        }
    }

    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    LONG first = (LONG)
        DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Read.ByteOffset.QuadPart);
    LONG last = (LONG)
        DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Read.ByteOffset.QuadPart +
            io_stack->Parameters.Read.Length - 1);

    LONG splits = last - first;
    if (splits > 0)
    {
        InterlockedExchangeAdd64(&DeviceExtension->Statistics.SplitReads, splits);
    }

    IO_STATUS_BLOCK io_status;
    ULONG length_done = 0;

    for (
        LONG i = first;
        (i <= last) && (length_done < io_stack->Parameters.Read.Length);
        i++)
    {
        LONGLONG abs_offset_this_iter =
            io_stack->Parameters.Read.ByteOffset.QuadPart +
            length_done;
        ULONG page_offset_this_iter =
            DIFF_GET_BLOCK_OFFSET(abs_offset_this_iter);
        ULONG bytes_this_iter = io_stack->Parameters.Read.Length -
            length_done;

        if ((page_offset_this_iter + bytes_this_iter) > DIFF_BLOCK_SIZE)
        {
            bytes_this_iter = DIFF_BLOCK_SIZE - page_offset_this_iter;
        }

        NTSTATUS status;
        LONG block_address = DeviceExtension->AllocationTable[i];
        if (block_address == DIFF_BLOCK_UNALLOCATED)
        {
            LARGE_INTEGER offset;
            offset.QuadPart =
                DIFF_GET_BLOCK_BASE_FROM_ABS_OFFSET(abs_offset_this_iter) +
                page_offset_this_iter;

            PIRP target_irp = IoBuildSynchronousFsdRequest(
                IRP_MJ_READ,
                DeviceExtension->TargetDeviceObject,
                BlockBuffer + page_offset_this_iter,
                bytes_this_iter,
                &offset,
                &event,
                &io_status);

            if (target_irp == NULL)
            {
                KdBreakPoint();

                Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
                return;
            }

            KeClearEvent(&event);

            status = IoCallDriver(
                DeviceExtension->TargetDeviceObject,
                target_irp);

            if (status == STATUS_PENDING)
            {
                KeWaitForSingleObject(&event, Executive, KernelMode,
                    FALSE, NULL);
            }

            if (NT_SUCCESS(status) &&
                io_status.Information != bytes_this_iter)
            {
                KdPrint(("AIMWrFltrDeferredRead: Read request 0x%X bytes, done 0x%IX.\n",
                    bytes_this_iter, io_status.Information));

                KdBreakPoint();
            }

            if (!NT_SUCCESS(status))
            {
                KdPrint(("AIMWrFltrDeferredRead: Read from target device failed: 0x%X\n",
                    status));

                KdBreakPoint();

                Irp->IoStatus.Status = io_status.Status;
                return;
            }
        }
        else
        {
            LARGE_INTEGER lower_offset = { 0 };

            lower_offset.QuadPart = ((LONGLONG)block_address <<
                DIFF_BLOCK_BITS) + page_offset_this_iter;

            status = AIMWrFltrSynchronousReadWrite(
                DeviceExtension->DiffDeviceObject,
                DeviceExtension->DiffFileObject,
                IRP_MJ_READ,
                BlockBuffer + page_offset_this_iter,
                bytes_this_iter,
                &lower_offset,
                &io_status);

#pragma warning(suppress: 6102)
            if (NT_SUCCESS(status) &&
                io_status.Information != bytes_this_iter)
            {
                KdPrint(("AIMWrFltrDeferredRead: Read request 0x%X bytes, done 0x%IX.\n",
                    bytes_this_iter, io_status.Information));

                KdBreakPoint();
            }

            if (!NT_SUCCESS(status))
            {
                KdPrint(("AIMWrFltrDeferredRead: Read from diff device failed: 0x%X\n",
                    status));

                KdBreakPoint();

                Irp->IoStatus.Status = status;
                return;
            }
        }

        RtlCopyMemory(buffer + length_done,
            BlockBuffer + page_offset_this_iter, bytes_this_iter);

        length_done += bytes_this_iter;
    }

    if (NT_SUCCESS(Irp->IoStatus.Status))
    {
        Irp->IoStatus.Information = io_stack->Parameters.Read.Length;
    }
}


