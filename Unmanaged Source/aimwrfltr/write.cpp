#include "aimwrfltr.h"

#define AIMWRFLTR_DEFER_ALL_WRITES

NTSTATUS
AIMWrFltrWrite(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
{
    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    NTSTATUS status;

    if (!device_extension->Statistics.IsProtected)
    {
        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }

    if (!device_extension->Statistics.Initialized)
    {
        status = AIMWrFltrInitializeDiffDevice(device_extension);

        if (!NT_SUCCESS(status))
        {
            status = STATUS_MEDIA_WRITE_PROTECTED;

            Irp->IoStatus.Information = 0;
            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }
    }

    InterlockedIncrement64(&device_extension->Statistics.WriteRequests);

    if (io_stack->Parameters.Write.Length == 0)
    {
        // Turn a zero-byte write request into a read request to take
        // advantage of just bound checks etc by target device driver

        IoGetNextIrpStackLocation(Irp)->MajorFunction = IRP_MJ_READ;

        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }

    InterlockedExchangeAdd64(&device_extension->Statistics.WrittenBytes,
        io_stack->Parameters.Write.Length);

    LONGLONG highest_byte =
        io_stack->Parameters.Write.ByteOffset.QuadPart +
        io_stack->Parameters.Write.Length;

    if ((io_stack->Parameters.Write.ByteOffset.QuadPart >=
        device_extension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart) ||
        (highest_byte <= 0) ||
        (highest_byte > device_extension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart))
    {
        Irp->IoStatus.Status = STATUS_END_OF_MEDIA;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return STATUS_END_OF_MEDIA;
    }

    if (io_stack->Parameters.Write.Length >
        device_extension->Statistics.LargestWriteSize)
    {
        device_extension->Statistics.LargestWriteSize =
            io_stack->Parameters.Write.Length;

        KdPrint(("AIMWrFltrWrite: Largest write size is now %u KB\n",
            device_extension->Statistics.LargestWriteSize >> 10));
    }

    bool defer_to_worker_thread = false;

    KIRQL current_irql = KeGetCurrentIrql();

    LONG first = (LONG)
        DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Write.ByteOffset.QuadPart);
    LONG last = (LONG)
        DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Write.ByteOffset.QuadPart +
            io_stack->Parameters.Write.Length - 1);

#ifdef AIMWRFLTR_DEFER_ALL_WRITES

    defer_to_worker_thread = true;

#else

    if (current_irql >= DISPATCH_LEVEL)
    {
        defer_to_worker_thread = true;
    }

    for (LONG i = first; i <= last && !defer_to_worker_thread; i++)
    {
        if (device_extension->AllocationTable[i] == DIFF_BLOCK_UNALLOCATED)
        {
            defer_to_worker_thread = true;
        }
    }

#endif

    if (defer_to_worker_thread)
    {
        InterlockedIncrement64(
            &device_extension->Statistics.DeferredWriteRequests);

        InterlockedExchangeAdd64(&device_extension->Statistics.DeferredWrittenBytes,
            io_stack->Parameters.Write.Length);

        //
        // Acquire the remove lock so that device will not be removed while
        // processing this irp.
        //
        status = IoAcquireRemoveLock(&device_extension->RemoveLock, Irp);

        if (!NT_SUCCESS(status))
        {
            DbgPrint(
                "AIMWrFltrWrite: Remove lock failed write type Irp: 0x%X\n",
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

    InterlockedIncrement64(&device_extension->Statistics.DirectWriteRequests);

    InterlockedExchangeAdd64(&device_extension->Statistics.DirectWrittenBytes,
        io_stack->Parameters.Write.Length);

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
        (i <= last) && (length_done < io_stack->Parameters.Write.Length);
        i++)
    {
        LONGLONG abs_offset_this_iter =
            io_stack->Parameters.Write.ByteOffset.QuadPart + length_done;
        ULONG page_offset_this_iter =
            DIFF_GET_BLOCK_OFFSET(abs_offset_this_iter);
        ULONG bytes_this_iter =
            io_stack->Parameters.Write.Length - length_done;
        ULONG block_size = DIFF_BLOCK_SIZE;
        LONG block_base = device_extension->AllocationTable[i];

        while ((page_offset_this_iter + bytes_this_iter) > block_size)
        {
            // Contiguous? Then merge with next iteration
            if (device_extension->AllocationTable[i + 1] ==
                device_extension->AllocationTable[i] + 1)
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

        LARGE_INTEGER lower_offset = { 0 };

        lower_offset.QuadPart = ((LONGLONG)block_base << DIFF_BLOCK_BITS) +
            page_offset_this_iter;

        PIRP lower_irp = scatter->BuildIrp(
            IRP_MJ_WRITE,
            device_extension->DiffDeviceObject,
            device_extension->DiffFileObject,
            length_done,
            bytes_this_iter,
            &lower_offset);

        if (lower_irp == NULL)
        {
            break;
        }

        ASSERT(IoGetNextIrpStackLocation(lower_irp)->FileObject ==
            device_extension->DiffFileObject);

        ASSERT(IoGetRelatedDeviceObject(IoGetNextIrpStackLocation(lower_irp)->FileObject) ==
            device_extension->DiffDeviceObject);

        IoCallDriver(device_extension->DiffDeviceObject, lower_irp);

        length_done += bytes_this_iter;
    }

    if (splits > 0)
    {
        InterlockedExchangeAdd64(&device_extension->Statistics.SplitWrites, splits);
    }

    // Decrement reference counter and complete if all partials are finished
    scatter->Complete();

    return STATUS_PENDING;

}				// end AIMWrFltrReadWrite()

VOID
AIMWrFltrDeferredWrite(
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
        DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Write.ByteOffset.QuadPart);
    LONG last = (LONG)
        DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Write.ByteOffset.QuadPart +
        io_stack->Parameters.Write.Length - 1);

    LONG splits = last - first;
    if (splits > 0)
    {
        InterlockedExchangeAdd64(&DeviceExtension->Statistics.SplitWrites, splits);
    }

    IO_STATUS_BLOCK io_status;
    ULONG length_done = 0;

    for (
        LONG i = first;
        (i <= last) && (length_done < io_stack->Parameters.Write.Length);
        i++)
    {
        LONGLONG abs_offset_this_iter =
            io_stack->Parameters.Write.ByteOffset.QuadPart +
            length_done;
        ULONG page_offset_this_iter =
            DIFF_GET_BLOCK_OFFSET(abs_offset_this_iter);
        ULONG bytes_this_iter = io_stack->Parameters.Write.Length -
            length_done;

        if ((page_offset_this_iter + bytes_this_iter) > DIFF_BLOCK_SIZE)
        {
            bytes_this_iter = DIFF_BLOCK_SIZE - page_offset_this_iter;
        }

        RtlCopyMemory(BlockBuffer + page_offset_this_iter,
            buffer + length_done, bytes_this_iter);

        length_done += bytes_this_iter;

        NTSTATUS status;
        LONG block_address = DeviceExtension->AllocationTable[i];
        if (block_address == DIFF_BLOCK_UNALLOCATED)
        {
            block_address = ++DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.LastAllocatedBlock;

            // If not writing a complete block, we need to fill up by reading
            // some data from target volume
            if (bytes_this_iter < DIFF_BLOCK_SIZE)
            {
                // Need to fill up beginning of block?
                if (page_offset_this_iter > 0)
                {
                    LARGE_INTEGER offset;
                    offset.QuadPart =
                        DIFF_GET_BLOCK_BASE_FROM_ABS_OFFSET(abs_offset_this_iter);

                    PIRP target_irp = IoBuildSynchronousFsdRequest(
                        IRP_MJ_READ,
                        DeviceExtension->TargetDeviceObject,
                        BlockBuffer,
                        page_offset_this_iter,
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

                    ++DeviceExtension->Statistics.FillReads;
                    DeviceExtension->Statistics.FillReadBytes +=
                        page_offset_this_iter;

                    if (status == STATUS_PENDING)
                    {
                        KeWaitForSingleObject(&event, Executive, KernelMode,
                            FALSE, NULL);
                    }

                    if (!NT_SUCCESS(io_status.Status))
                    {
                        KdBreakPoint();

                        Irp->IoStatus.Status = io_status.Status;
                        return;
                    }

                    bytes_this_iter += page_offset_this_iter;
                    page_offset_this_iter = 0;
                }

                // Need to fill up end of block?
                if (bytes_this_iter < DIFF_BLOCK_SIZE)
                {
                    LARGE_INTEGER offset;
                    offset.QuadPart =
                        DIFF_GET_BLOCK_BASE_FROM_ABS_OFFSET(abs_offset_this_iter) +
                        bytes_this_iter;

                    PIRP target_irp = IoBuildSynchronousFsdRequest(
                        IRP_MJ_READ,
                        DeviceExtension->TargetDeviceObject,
                        BlockBuffer + bytes_this_iter,
                        DIFF_BLOCK_SIZE - bytes_this_iter,
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

                    if (!NT_SUCCESS(io_status.Status))
                    {
                        KdPrint(("AIMWrFltrDeferredWrite: Fill read from original device failed: 0x%X\n",
                            io_status.Status));

                        Irp->IoStatus.Status = io_status.Status;
                        return;
                    }

                    if (io_status.Information != DIFF_BLOCK_SIZE - bytes_this_iter)
                    {
                        KdPrint(("AIMWrFltrDeferredWrite: Fill read request 0x%IX bytes, got 0x%IX.\n",
                            DIFF_BLOCK_SIZE - bytes_this_iter, io_status.Information));
                    }

                    ++DeviceExtension->Statistics.FillReads;
                    DeviceExtension->Statistics.FillReadBytes +=
                        DIFF_BLOCK_SIZE - bytes_this_iter;

                    bytes_this_iter = DIFF_BLOCK_SIZE;
                }
            }
        }

        LARGE_INTEGER lower_offset = { 0 };

        lower_offset.QuadPart = ((LONGLONG)block_address <<
            DIFF_BLOCK_BITS) + page_offset_this_iter;

        status = AIMWrFltrSynchronousReadWrite(
            DeviceExtension->DiffDeviceObject,
            DeviceExtension->DiffFileObject,
            IRP_MJ_WRITE,
            BlockBuffer + page_offset_this_iter,
            bytes_this_iter,
            &lower_offset,
            &io_status);

#pragma warning(suppress: 6102)
        if (NT_SUCCESS(status) &&
            io_status.Information != bytes_this_iter)
        {
            KdPrint(("AIMWrFltrDeferredWrite: Write request 0x%X bytes, done 0x%IX.\n",
                bytes_this_iter, io_status.Information));

            KdBreakPoint();
        }

        if (!NT_SUCCESS(status))
        {
            KdPrint(("AIMWrFltrDeferredWrite: Write to diff device failed: 0x%X\n",
                status));

            KdBreakPoint();

            Irp->IoStatus.Status = status;
            return;
        }

        if (DeviceExtension->AllocationTable[i] != block_address)
        {
            DeviceExtension->AllocationTable[i] = block_address;
        }
    }

    if (NT_SUCCESS(Irp->IoStatus.Status))
    {
        Irp->IoStatus.Information = io_stack->Parameters.Write.Length;
    }
}

VOID
AIMWrFltrDeferredFlushBuffers(
    PDEVICE_EXTENSION DeviceExtension,
    PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    NTSTATUS status = AIMWrFltrSynchronousReadWrite(
        DeviceExtension->DiffDeviceObject,
        DeviceExtension->DiffFileObject,
        io_stack->MajorFunction,
        NULL,
        0,
        NULL,
        &Irp->IoStatus);

    Irp->IoStatus.Status = status;
}

#ifdef FSCTL_FILE_LEVEL_TRIM

VOID
AIMWrFltrDeferredManageDataSetAttributes(
PDEVICE_EXTENSION DeviceExtension,
PIRP Irp,
PUCHAR BlockBuffer)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    KEVENT event;
    KeInitializeEvent(&event, SynchronizationEvent, FALSE);

    PDEVICE_MANAGE_DATA_SET_ATTRIBUTES attrs =
        (PDEVICE_MANAGE_DATA_SET_ATTRIBUTES)
        Irp->AssociatedIrp.SystemBuffer;

    if ((attrs->Action != DeviceDsmAction_Trim) ||
        (io_stack->Parameters.DeviceIoControl.InputBufferLength <
        (attrs->DataSetRangesOffset +
        attrs->DataSetRangesLength)))
    {
        Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;
        return;
    }

    int items = attrs->DataSetRangesLength /
        sizeof(DEVICE_DATA_SET_RANGE);

    PDEVICE_DATA_SET_RANGE range = (PDEVICE_DATA_SET_RANGE)
        ((PUCHAR)attrs + attrs->DataSetRangesOffset);
    
    int allocated = 0;

    static bool break_flag = false;
    if (break_flag)
    {
        KdBreakPoint();
    }

    ULONG splits = 0;

    for (int i = 0; i < items; i++)
    {
        KdPrint((
            "AIMWrFltrDeferredManageDataSetAttributes: Trim request 0x%I64X bytes at 0x%I64X\n",
            range[i].LengthInBytes, range[i].StartingOffset));

        if (range[i].LengthInBytes == 0)
            continue;

        LONGLONG highest_byte =
            range[i].StartingOffset + (LONGLONG)range[i].LengthInBytes;

        if ((range[i].StartingOffset >=
            DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart) ||
            (highest_byte <= 0) ||
            (highest_byte > DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart))
        {
            Irp->IoStatus.Status = STATUS_END_OF_MEDIA;

            return;
        }

        LONG first = (LONG)DIFF_GET_BLOCK_NUMBER(range[i].StartingOffset);
        LONG last = (LONG)DIFF_GET_BLOCK_NUMBER(range[i].StartingOffset +
            range[i].LengthInBytes - 1);

        ULONGLONG length_done = 0;

        for (
            LONG b = first;
            (b <= last) && (length_done < range[i].LengthInBytes);
            b++)
        {
            LONGLONG abs_offset_this_iter =
                range[i].StartingOffset + length_done;
            ULONG page_offset_this_iter =
                DIFF_GET_BLOCK_OFFSET(abs_offset_this_iter);
            ULONGLONG bytes_this_iter =
                range[i].LengthInBytes - length_done;
            ULONGLONG block_size = DIFF_BLOCK_SIZE;

            if (DeviceExtension->AllocationTable[b] == DIFF_BLOCK_UNALLOCATED)
            {
                if ((page_offset_this_iter + bytes_this_iter) > block_size)
                {
                    bytes_this_iter = block_size - page_offset_this_iter;
                }

                length_done += bytes_this_iter;

                InterlockedExchangeAdd64(&DeviceExtension->Statistics.TrimBytesIgnored,
                    bytes_this_iter);

                continue;
            }

            while ((page_offset_this_iter + bytes_this_iter) > block_size)
            {
                // Contigous? Then merge with next iteration
                if (DeviceExtension->AllocationTable[b + 1] ==
                    (DeviceExtension->AllocationTable[b] + 1))
                {
                    block_size += DIFF_BLOCK_SIZE;
                    ++b;
                }
                else
                {
                    bytes_this_iter = block_size - page_offset_this_iter;
                    ++splits;
                }
            }

            length_done += bytes_this_iter;

            InterlockedExchangeAdd64(&DeviceExtension->Statistics.TrimBytesForwarded,
                bytes_this_iter);

            ++allocated;
        }
    }

    if (splits > 0)
    {
        InterlockedExchangeAdd64(&DeviceExtension->Statistics.SplitTrims, splits);
    }

    if (allocated <= 0)
    {
        KdPrint((
            "AIMWrFltrControl: None of trim blocks are yet allocated.\n"));

        return;
    }

    SIZE_T lower_mdsa_size = FIELD_OFFSET(FILE_LEVEL_TRIM, Ranges) + (allocated *
        sizeof(FILE_LEVEL_TRIM_RANGE));

    if (lower_mdsa_size > DIFF_BLOCK_SIZE)
    {
        Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;
        return;
    }

    PFILE_LEVEL_TRIM lower_mdsa = (PFILE_LEVEL_TRIM)BlockBuffer;

    RtlZeroMemory(lower_mdsa, lower_mdsa_size);

    lower_mdsa->NumRanges = allocated;

    PFILE_LEVEL_TRIM_RANGE lower_range = lower_mdsa->Ranges;

    for (int i = 0; i < items; i++)
    {
        if (range[i].LengthInBytes == 0)
            continue;

        LONG first = (LONG)DIFF_GET_BLOCK_NUMBER(range[i].StartingOffset);
        LONG last = (LONG)DIFF_GET_BLOCK_NUMBER(range[i].StartingOffset +
            range[i].LengthInBytes - 1);

        ULONGLONG length_done = 0;

        for (
            LONG b = first;
            (b <= last) && (length_done < range[i].LengthInBytes);
            b++)
        {
            LONGLONG abs_offset_this_iter =
                range[i].StartingOffset + length_done;
            ULONG page_offset_this_iter =
                DIFF_GET_BLOCK_OFFSET(abs_offset_this_iter);
            ULONGLONG bytes_this_iter =
                range[i].LengthInBytes - length_done;
            ULONGLONG block_size = DIFF_BLOCK_SIZE;
            LONG block_base = DeviceExtension->AllocationTable[b];

            if (block_base == DIFF_BLOCK_UNALLOCATED)
            {
                if ((page_offset_this_iter + bytes_this_iter) > block_size)
                {
                    bytes_this_iter = block_size - page_offset_this_iter;
                }

                length_done += bytes_this_iter;

                continue;
            }

            while ((page_offset_this_iter + bytes_this_iter) > block_size)
            {
                // Contigous? Then merge with next iteration
                if (DeviceExtension->AllocationTable[b + 1] ==
                    (DeviceExtension->AllocationTable[b] + 1))
                {
                    block_size += DIFF_BLOCK_SIZE;
                    ++b;
                }
                else
                {
                    bytes_this_iter = block_size - page_offset_this_iter;
                }
            }

            lower_range->Offset =
                ((LONGLONG)block_base << DIFF_BLOCK_BITS) +
                page_offset_this_iter;

            lower_range->Length = bytes_this_iter;

            //KdPrint((
            //    "AIMWrFltrControl: Trimming diff 0x%I64X bytes at physical=0x%I64X virtual=0x%I64X\n",
            //    lower_range->LengthInBytes, lower_range->StartingOffset,
            //    range[i].StartingOffset + length_done));

            length_done += bytes_this_iter;
            ++lower_range;
        }
    }

    IO_STATUS_BLOCK io_status;

    PIRP lower_irp = IoBuildDeviceIoControlRequest(
        FSCTL_FILE_LEVEL_TRIM,
        DeviceExtension->DiffDeviceObject,
        lower_mdsa,
        (ULONG)lower_mdsa_size,
        NULL,
        0,
        FALSE,
        &event,
        &io_status);

    if (lower_irp == NULL)
    {
        Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
        return;
    }

    PIO_STACK_LOCATION lower_io_stack = IoGetNextIrpStackLocation(lower_irp);
    lower_io_stack->MajorFunction = IRP_MJ_FILE_SYSTEM_CONTROL;
    lower_io_stack->FileObject = DeviceExtension->DiffFileObject;

    NTSTATUS status = IoCallDriver(DeviceExtension->DiffDeviceObject, lower_irp);

    if (status == STATUS_PENDING)
        KeWaitForSingleObject(&event, Executive, KernelMode, FALSE, NULL);

    Irp->IoStatus.Status = io_status.Status;
}

#endif
