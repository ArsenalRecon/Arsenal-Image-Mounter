#include "aimwrfltr.h"

//#define QUEUE_ALL_MODIFIED_READS

NTSTATUS
AIMWrFltrRead(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
{
    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    if (device_extension->ShutdownThread)
    {
        return AIMWrFltrHandleRemovedDevice(Irp);
    }

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

    KIRQL current_irql = PASSIVE_LEVEL;

    KLOCK_QUEUE_HANDLE lock_handle;

    // For testing purposes, builds a driver that always queues read requests so that they appear
    // in worker thread queue in correct order vs queued write requests. Useful to troubleshoot
    // flaws in the read cache logic.

    if (QueueWithoutCache)
    {
        PCACHED_IRP cached_irp = CACHED_IRP::CreateEnqueuedIrp(Irp);

        if (cached_irp == NULL)
        {
            KdBreakPoint();

            Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_INSUFFICIENT_RESOURCES;
        }

        InterlockedIncrement64(
            &device_extension->Statistics.DeferredReadRequests);

        InterlockedExchangeAdd64(&device_extension->Statistics.DeferredReadBytes,
            io_stack->Parameters.Write.Length);

        //
        // Acquire the remove lock so that device will not be removed while
        // processing this irp.
        //
        status = IoAcquireRemoveLock(&device_extension->RemoveLock, cached_irp);

        if (!NT_SUCCESS(status))
        {
            DbgPrint(
                "AIMWrFltrRead: Remove lock failed read type Irp: 0x%X\n",
                status);

            KdBreakPoint();

            delete cached_irp;

            Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_INSUFFICIENT_RESOURCES;
        }

        IoMarkIrpPending(Irp);

        AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
            current_irql);

        InsertTailList(&device_extension->ListHead,
            &cached_irp->ListEntry);

        AIMWrFltrReleaseLock(&lock_handle, &current_irql);

        KeSetEvent(&device_extension->ListEvent, 0, FALSE);

        return STATUS_PENDING;
    }

    PUCHAR system_buffer = NULL;

    if (device_extension->DeviceObject->Flags & DO_BUFFERED_IO)
    {
        system_buffer = (PUCHAR)Irp->AssociatedIrp.SystemBuffer;
    }
    else
    {
        system_buffer = (PUCHAR)MmGetSystemAddressForMdlSafe(Irp->MdlAddress, NormalPagePriority);
    }

    if (system_buffer == NULL)
    {
        Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        KdBreakPoint();

        return STATUS_INSUFFICIENT_RESOURCES;
    }

    RTL_BITMAP bitmap;

    WNonPagedPoolMem<ULONG> bitmap_buffer((io_stack->Parameters.Read.Length >> 9) + sizeof(ULONG) - 1);

    if (!bitmap_buffer)
    {
        Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        KdBreakPoint();

        return STATUS_INSUFFICIENT_RESOURCES;
    }

    bitmap_buffer.Clear();

    RtlInitializeBitMap(&bitmap, bitmap_buffer, io_stack->Parameters.Read.Length >> 9);

    ULONG bytes_from_cache = 0;

    ULONG items_in_queue = 0;

    // First check whether we have queued write or trim requests that
    // match region covered by this read request

    AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
        current_irql);

    for (PLIST_ENTRY entry = device_extension->ListHead.Flink;
        entry != &device_extension->ListHead;
        entry = entry->Flink)
    {
        items_in_queue++;

        PCACHED_IRP cached_irp = CONTAINING_RECORD(entry, CACHED_IRP, ListEntry);

        PIO_STACK_LOCATION item = &cached_irp->IoStack;

        if (item->MajorFunction == IRP_MJ_WRITE && cached_irp->Irp == NULL &&
            (item->Parameters.Write.ByteOffset.QuadPart < (io_stack->Parameters.Read.ByteOffset.QuadPart + io_stack->Parameters.Read.Length)) &&
            ((item->Parameters.Write.ByteOffset.QuadPart + item->Parameters.Write.Length) > io_stack->Parameters.Read.ByteOffset.QuadPart))
        {
            LONGLONG start_pos = max(item->Parameters.Write.ByteOffset.QuadPart,
                io_stack->Parameters.Read.ByteOffset.QuadPart);

            LONGLONG end_pos = min(item->Parameters.Write.ByteOffset.QuadPart + item->Parameters.Write.Length,
                io_stack->Parameters.Read.ByteOffset.QuadPart + io_stack->Parameters.Read.Length);

            RtlCopyMemory(system_buffer + start_pos - io_stack->Parameters.Read.ByteOffset.QuadPart,
                cached_irp->Buffer + start_pos - item->Parameters.Write.ByteOffset.QuadPart,
                (SIZE_T)(end_pos - start_pos));

            RtlSetBits(&bitmap,
                (ULONG)((start_pos - io_stack->Parameters.Read.ByteOffset.QuadPart) >> 9),
                (ULONG)((end_pos - start_pos) >> 9));

            bytes_from_cache += (ULONG)(end_pos - start_pos);

            ++device_extension->Statistics.ReadRequestsFromCache;
            device_extension->Statistics.ReadBytesFromCache +=
                end_pos - start_pos;
        }
        else if (item->MajorFunction == IRP_MJ_DEVICE_CONTROL &&
            item->Parameters.DeviceIoControl.IoControlCode == IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES &&
            ((PDEVICE_MANAGE_DATA_SET_ATTRIBUTES)cached_irp->Buffer)->Action == DeviceDsmAction_Trim)
        {
            PDEVICE_MANAGE_DATA_SET_ATTRIBUTES attrs = (PDEVICE_MANAGE_DATA_SET_ATTRIBUTES)cached_irp->Buffer;

            ULONG items = attrs->DataSetRangesLength / sizeof(DEVICE_DATA_SET_RANGE);

            PDEVICE_DATA_SET_RANGE range = (PDEVICE_DATA_SET_RANGE)((PUCHAR)attrs + attrs->DataSetRangesOffset);

            for (ULONG i = 0; i < items; i++)
            {
                if (range[i].StartingOffset < (io_stack->Parameters.Read.ByteOffset.QuadPart + io_stack->Parameters.Read.Length) &&
                    ((range[i].StartingOffset + (LONGLONG)range[i].LengthInBytes) > io_stack->Parameters.Read.ByteOffset.QuadPart))
                {
                    LONGLONG start_pos = max(range[i].StartingOffset,
                        io_stack->Parameters.Read.ByteOffset.QuadPart);

                    LONGLONG end_pos = min(range[i].StartingOffset + (LONGLONG)range[i].LengthInBytes,
                        io_stack->Parameters.Read.ByteOffset.QuadPart + io_stack->Parameters.Read.Length);

                    RtlZeroMemory(system_buffer + start_pos - io_stack->Parameters.Read.ByteOffset.QuadPart,
                        (SIZE_T)(end_pos - start_pos));

                    RtlSetBits(&bitmap,
                        (ULONG)((start_pos - io_stack->Parameters.Read.ByteOffset.QuadPart) >> 9),
                        (ULONG)((end_pos - start_pos) >> 9));

                    bytes_from_cache += (ULONG)(end_pos - start_pos);

                    ++device_extension->Statistics.ReadRequestsFromCache;
                    device_extension->Statistics.ReadBytesFromCache +=
                        end_pos - start_pos;
                }
            }
        }
    }

    AIMWrFltrReleaseLock(&lock_handle, &current_irql);

    if (bytes_from_cache == 0)
    {
        bool any_block_modified = false;

        if ((device_extension->AllocationTable != NULL) &&
            (device_extension->DiffDeviceObject != NULL))
        {
            LONG first = (LONG)DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Read.ByteOffset.QuadPart);
            LONG last = (LONG)DIFF_GET_BLOCK_NUMBER(io_stack->Parameters.Read.ByteOffset.QuadPart +
                io_stack->Parameters.Read.Length - 1);

            for (LONG i = first; i <= last; i++)
            {
                if (device_extension->AllocationTable[i] != DIFF_BLOCK_UNALLOCATED)
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
    }
    else if (bytes_from_cache >= io_stack->Parameters.Read.Length &&
        RtlAreBitsSet(&bitmap, 0, io_stack->Parameters.Read.Length >> 9))
    {
        // If entire buffer filled by write queue
#if 1
        KdPrint(("AIMWrFltrRead: Read %u bytes at 0x%I64X completed from write queue. Items in queue: %u\n",
            io_stack->Parameters.Read.Length, io_stack->Parameters.Read.ByteOffset.QuadPart,
            items_in_queue));
#endif

        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = io_stack->Parameters.Read.Length;
        IoCompleteRequest(Irp, IO_DISK_INCREMENT);

        return STATUS_SUCCESS;
    }
    else
    {
        KdPrint(("AIMWrFltrRead: Read %u bytes of %u requested at 0x%I64X from write queue. Items in queue: %u\n",
            bytes_from_cache, io_stack->Parameters.Read.Length, io_stack->Parameters.Read.ByteOffset.QuadPart,
            items_in_queue));
    }

#ifdef QUEUE_ALL_MODIFIED_READS

    PCACHED_IRP cached_irp = CACHED_IRP::CreateEnqueuedIrp(Irp);

    if (cached_irp == NULL)
    {
        KdBreakPoint();

        Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    InterlockedIncrement64(
        &device_extension->Statistics.DeferredReadRequests);

    InterlockedExchangeAdd64(&device_extension->Statistics.DeferredReadBytes,
        io_stack->Parameters.Write.Length);

    //
    // Acquire the remove lock so that device will not be removed while
    // processing this irp.
    //
    status = IoAcquireRemoveLock(&device_extension->RemoveLock, cached_irp);

    if (!NT_SUCCESS(status))
    {
        DbgPrint(
            "AIMWrFltrRead: Remove lock failed read type Irp: 0x%X\n",
            status);

        KdBreakPoint();

        delete cached_irp;

        Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    IoMarkIrpPending(Irp);

    AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
        current_irql);

    InsertTailList(&device_extension->ListHead,
        &cached_irp->ListEntry);

    AIMWrFltrReleaseLock(&lock_handle, &current_irql);

    KeSetEvent(&device_extension->ListEvent, 0, FALSE);

    return STATUS_PENDING;

#else

    ULONG clear_index;
    auto clear_bits = RtlFindFirstRunClear(&bitmap, &clear_index);

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

    ULONG bytes_from_orig = 0;
    ULONG bytes_from_diff = 0;
    ULONG splits = 0;

    while (clear_bits > 0)
    {
        ULONG original_irp_offset = clear_index << 9;

        LARGE_INTEGER lower_offset;
        lower_offset.QuadPart = io_stack->Parameters.Read.ByteOffset.QuadPart + original_irp_offset;

        ULONG lower_length = clear_bits << 9;

        ULONG length_done = 0;

        ULONG first = (ULONG)DIFF_GET_BLOCK_NUMBER(lower_offset.QuadPart);
        ULONG last = (ULONG)DIFF_GET_BLOCK_NUMBER(lower_offset.QuadPart +
            lower_length - 1);

        for (
            ULONG i = first;
            (i <= last) && (length_done < lower_length);
            i++)
        {
            LONGLONG abs_offset_this_iter = lower_offset.QuadPart + length_done;
            ULONG page_offset_this_iter = DIFF_GET_BLOCK_OFFSET(abs_offset_this_iter);
            ULONG bytes_this_iter = lower_length - length_done;
            ULONG orig_irp_offset_this_iter = original_irp_offset + length_done;
            LARGE_INTEGER offset_this_iter = { 0 };
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

                bytes_from_orig += bytes_this_iter;

                lower_device = device_extension->TargetDeviceObject;
                offset_this_iter.QuadPart = abs_offset_this_iter;
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

                bytes_from_diff += bytes_this_iter;

                lower_device = device_extension->DiffDeviceObject;
                lower_file = device_extension->DiffFileObject;

                offset_this_iter.QuadPart =
                    ((LONGLONG)block_base << DIFF_BLOCK_BITS) +
                    page_offset_this_iter;
            }

            __analysis_assume(lower_device != NULL);

            PIRP lower_irp = scatter->BuildIrp(
                IRP_MJ_READ,
                lower_device,
                lower_file,
                orig_irp_offset_this_iter,
                bytes_this_iter,
                &offset_this_iter);

            if (lower_irp == NULL)
            {
                break;
            }

            ASSERT(IoGetNextIrpStackLocation(lower_irp)->FileObject == lower_file);

            ASSERT((lower_device == device_extension->TargetDeviceObject && lower_file == NULL) ||
                (lower_file == device_extension->DiffFileObject &&
                    lower_device == device_extension->DiffDeviceObject &&
                    lower_device == IoGetRelatedDeviceObject(lower_file)));

            // Defer all reads from actual files to worker thread, just to keep safe
            if (lower_file != NULL)
            {
                if (current_irql > PASSIVE_LEVEL)
                {
                    KdPrint(("AIMWrFltrRead: Read from diff at IRQL=%i. Deferring to worker thread.\n", current_irql));
                }

                PCACHED_IRP cached_irp = CACHED_IRP::CreateEnqueuedForwardIrp(lower_device, lower_irp);

                if (cached_irp == NULL)
                {
                    KdBreakPoint();

                    AIMWrFltrFreeIrpWithMdls(lower_irp);
                    break;
                }

                InterlockedIncrement64(
                    &device_extension->Statistics.DeferredReadRequests);

                InterlockedExchangeAdd64(&device_extension->Statistics.DeferredReadBytes,
                    io_stack->Parameters.Write.Length);

                //
                // Acquire the remove lock so that device will not be removed while
                // processing this irp.
                //
                status = IoAcquireRemoveLock(&device_extension->RemoveLock, cached_irp);

                if (!NT_SUCCESS(status))
                {
                    DbgPrint(
                        "AIMWrFltrWrite: Remove lock failed read type Irp: 0x%X\n",
                        status);

                    KdBreakPoint();

                    AIMWrFltrFreeIrpWithMdls(lower_irp);
                    break;
                }

                AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
                    current_irql);

                InsertTailList(&device_extension->ListHead,
                    &cached_irp->ListEntry);

                AIMWrFltrReleaseLock(&lock_handle, &current_irql);

                KeSetEvent(&device_extension->ListEvent, 0, FALSE);
            }
            else
            {
                IoCallDriver(lower_device, lower_irp);
            }

            length_done += bytes_this_iter;
        }

        clear_bits = RtlFindNextForwardRunClear(&bitmap, clear_index + clear_bits, &clear_index);

    }

    if (splits > 0)
    {
        InterlockedExchangeAdd64(&device_extension->Statistics.SplitReads, splits);
    }

    //KdPrint(("AIMWrFltrRead: Read request %u bytes at 0x%I64X, %u bytes from cache, %u bytes from original and %u bytes from diff\n",
    //    io_stack->Parameters.Read.Length, io_stack->Parameters.Read.ByteOffset.QuadPart,
    //    bytes_from_cache, bytes_from_orig, bytes_from_diff));

    // Decrement reference counter and complete if all partials are finished
    scatter->Complete();

    return STATUS_PENDING;

#endif

}				// end AIMWrFltrReadWrite()

NTSTATUS
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
            return STATUS_INSUFFICIENT_RESOURCES;
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

        if (((ULONGLONG)page_offset_this_iter + bytes_this_iter) > DIFF_BLOCK_SIZE)
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

                return STATUS_INSUFFICIENT_RESOURCES;
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

                return io_status.Status;
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
                Irp->Tail.Overlay.Thread,
                &io_status);

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

                return io_status.Status;
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

    return Irp->IoStatus.Status;
}


