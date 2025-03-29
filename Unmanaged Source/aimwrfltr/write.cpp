
/// write.c
/// AIM Write Filter - Write routines.
/// 
/// Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#include "aimwrfltr.h"

NTSTATUS
AIMWrFltrWrite(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
{
    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    if (device_extension->ShutdownThread)
    {
        return AIMWrFltrHandleRemovedDevice(Irp);
    }

    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    NTSTATUS status;

    if (!device_extension->Statistics.IsProtected)
    {
        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }

    Irp->IoStatus.Information = 0;

    if (!device_extension->Statistics.Initialized)
    {
        status = AIMWrFltrInitializeDiffDevice(device_extension);

        if (!NT_SUCCESS(status))
        {
            status = STATUS_MEDIA_WRITE_PROTECTED;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }
    }

    if (device_extension->Statistics.DelayWriteFailed)
    {
        status = STATUS_DEVICE_REMOVED;

        Irp->IoStatus.Status = status;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return status;
    }

    InterlockedIncrement64(&device_extension->Statistics.WriteRequests);

    if (io_stack->Parameters.Write.Length == 0)
    {
        // Turn a zero-byte write request into a read request to take
        // advantage of just bound checks etc by target device driver

        io_stack->MajorFunction = IRP_MJ_READ;

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

        if (device_extension->Statistics.LargestWriteSize >= 4096)
        {
            KdPrint(("AIMWrFltrWrite: Largest write size is now %u KB\n",
                device_extension->Statistics.LargestWriteSize >> 10));
        }
        else
        {
            KdPrint(("AIMWrFltrWrite: Largest write size is now %u bytes\n",
                device_extension->Statistics.LargestWriteSize));
        }
    }

    KIRQL current_irql = PASSIVE_LEVEL;
    KLOCK_QUEUE_HANDLE lock_handle;

    // Detect possible risk of stack overflow. Defer to worker thread if we are
    // called in the completion routine for the same IRP. Also, queue original
    // request directly if queue is becoming too deep.
    if (device_extension->CompletingIrp == Irp ||
        (MaxQueueDepth != 0 &&
            AIMWrFltrIsQueueDeeperThan(device_extension, MaxQueueDepth, TRUE, &current_irql) >= MaxQueueDepth) ||
        (HighCommitCondition != NULL &&
            KeReadStateEvent(HighCommitCondition)))
    {
        PCACHED_IRP cached_irp = CACHED_IRP::CreateEnqueuedIrp(Irp);

        if (cached_irp == NULL)
        {
#if DBG
            if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                DbgBreakPoint();
#endif

            Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_INSUFFICIENT_RESOURCES;
        }

        InterlockedIncrement64(
            &device_extension->Statistics.DeferredWriteRequests);

        InterlockedExchangeAdd64(&device_extension->Statistics.DeferredWrittenBytes,
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

#if DBG
            if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                DbgBreakPoint();
#endif

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
    else
    {
        PCACHED_IRP cached_irp = CACHED_IRP::CreateFromWriteIrp(device_extension->DeviceObject, Irp);

        if (cached_irp == NULL)
        {
#if DBG
            if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                DbgBreakPoint();
#endif

            Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_INSUFFICIENT_RESOURCES;
        }

        InterlockedIncrement64(
            &device_extension->Statistics.DeferredWriteRequests);

        InterlockedExchangeAdd64(&device_extension->Statistics.DeferredWrittenBytes,
            io_stack->Parameters.Write.Length);

        //
        // Acquire the remove lock so that device will not be removed while
        // processing this irp.
        //
        status = IoAcquireRemoveLock(&device_extension->RemoveLock, cached_irp);

        if (!NT_SUCCESS(status))
        {
            DbgPrint(
                "AIMWrFltrWrite: Remove lock failed write type Irp: 0x%X\n",
                status);

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);

#if DBG
            if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                DbgBreakPoint();
#endif

            return status;
        }

        AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
            current_irql);

        InsertTailList(&device_extension->ListHead,
            &cached_irp->ListEntry);

        AIMWrFltrReleaseLock(&lock_handle, &current_irql);

        KeSetEvent(&device_extension->ListEvent, 0, FALSE);

        Irp->IoStatus.Information = io_stack->Parameters.Write.Length;
        Irp->IoStatus.Status = STATUS_SUCCESS;
        device_extension->CompletingIrp = Irp;
        IoCompleteRequest(Irp, IO_DISK_INCREMENT);
        if (device_extension->CompletingIrp == Irp)
        {
            device_extension->CompletingIrp = NULL;
        }

        return STATUS_SUCCESS;
    }
}

NTSTATUS
AIMWrFltrDeferredWrite(
PDEVICE_EXTENSION DeviceExtension,
PCACHED_IRP Irp,
PUCHAR BlockBuffer)
{
    PUCHAR buffer;

    if (Irp->Irp != NULL)
    {
        if (DeviceExtension->DeviceObject->Flags & DO_BUFFERED_IO)
        {
            buffer = (PUCHAR)Irp->Irp->AssociatedIrp.SystemBuffer;
        }
        else
        {
            buffer = (PUCHAR)MmGetSystemAddressForMdlSafe(
                Irp->Irp->MdlAddress, NormalPagePriority);
        }
    }
    else
    {
        buffer = Irp->Buffer;
    }

    if (buffer == NULL)
    {
#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        return STATUS_INSUFFICIENT_RESOURCES;
    }

    PIO_STACK_LOCATION io_stack = &Irp->IoStack;

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

        NTSTATUS status;

        LONG block_address = DeviceExtension->AllocationTable[i];

        if ((page_offset_this_iter + bytes_this_iter) > (ULONG)DIFF_BLOCK_SIZE)
        {
            bytes_this_iter = DIFF_BLOCK_SIZE - page_offset_this_iter;
        }

        // If requested I/O position or length are not aligned to sector
        // size of diff device, we need to fill parts of buffer before and
        // after new data with old data from existing diff block
        ULONG sector_mask = (ULONG)(DeviceExtension->DiffDeviceObject->SectorSize - 1);

        if (block_address != DIFF_BLOCK_UNALLOCATED &&
            DeviceExtension->DiffDeviceObject->SectorSize != 0 &&
            ((page_offset_this_iter & block_address) != 0 ||
                ((bytes_this_iter & block_address) != 0)))
        {
            KdPrint(("AIMWrFltrDeferredWrite: Requested writing 0x%X bytes at 0x%I64X requires alignment.\n",
                bytes_this_iter, ((LONGLONG)block_address <<
                    DIFF_BLOCK_BITS) + page_offset_this_iter));

            ULONG new_page_offset_this_iter = page_offset_this_iter & ~sector_mask;
            ULONG new_bytes_this_iter = ((page_offset_this_iter - new_page_offset_this_iter) +
                bytes_this_iter + sector_mask) & ~sector_mask;

            LARGE_INTEGER offset = { 0 };

            offset.QuadPart = ((LONGLONG)block_address <<
                DIFF_BLOCK_BITS) + new_page_offset_this_iter;

            KdPrint(("AIMWrFltrDeferredWrite: Alignment read 0x%X bytes from 0x%I64X.\n",
                new_bytes_this_iter, offset));

            status = AIMWrFltrSynchronousReadWrite(
                DeviceExtension->DiffDeviceObject,
                DeviceExtension->DiffFileObject,
                IRP_MJ_READ,
                BlockBuffer + new_page_offset_this_iter,
                new_bytes_this_iter,
                &offset,
                NULL,
                &io_status);

            if (NT_SUCCESS(status) &&
                io_status.Information != new_bytes_this_iter)
            {
                DbgPrint("AIMWrFltrDeferredWrite: Alignment block read request 0x%X bytes, done 0x%IX.\n",
                    new_bytes_this_iter, io_status.Information);

#if DBG
                if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                    DbgBreakPoint();
#endif

                status = STATUS_DISK_CORRUPT_ERROR;
            }

            if (!NT_SUCCESS(status))
            {
                DbgPrint("AIMWrFltrDeferredWrite: IRQL=%i Alignmenet block read 0x%X bytes at 0x%I64X from diff device failed: 0x%X\n",
                    (int)KeGetCurrentIrql(), new_bytes_this_iter, offset.QuadPart, status);

#if DBG
                if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                    DbgBreakPoint();
#endif

                return status;
            }

            RtlCopyMemory(BlockBuffer + page_offset_this_iter,
                buffer + length_done, bytes_this_iter);

            length_done += bytes_this_iter;

            page_offset_this_iter = new_page_offset_this_iter;
            bytes_this_iter = new_bytes_this_iter;
        }
        else
        {
            RtlCopyMemory(BlockBuffer + page_offset_this_iter,
                buffer + length_done, bytes_this_iter);

            length_done += bytes_this_iter;
        }

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
                    LARGE_INTEGER offset = { 0 };
                    offset.QuadPart =
                        DIFF_GET_BLOCK_BASE_FROM_ABS_OFFSET(abs_offset_this_iter);

                    status = AIMWrFltrSynchronousReadWrite(
                        DeviceExtension->TargetDeviceObject,
                        NULL,
                        IRP_MJ_READ,
                        BlockBuffer,
                        page_offset_this_iter,
                        &offset,
                        NULL,
                        &io_status);

                    if (!NT_SUCCESS(status))
                    {
                        DbgPrint("AIMWrFltrDeferredWrite: Fill read from original device failed: 0x%X\n",
                            status);

                        //KdBreakPoint();

                        return status;
                    }

                    if (io_status.Information != page_offset_this_iter)
                    {
                        DbgPrint("AIMWrFltrDeferredWrite: Fill read request 0x%X bytes, got 0x%IX.\n",
                            page_offset_this_iter, io_status.Information);
                    }

                    ++DeviceExtension->Statistics.FillReads;
                    DeviceExtension->Statistics.FillReadBytes +=
                        page_offset_this_iter;

                    bytes_this_iter += page_offset_this_iter;
                    page_offset_this_iter = 0;
                }

                // Need to fill up end of block?
                if (bytes_this_iter < DIFF_BLOCK_SIZE)
                {
                    LARGE_INTEGER offset = { 0 };
                    offset.QuadPart =
                        DIFF_GET_BLOCK_BASE_FROM_ABS_OFFSET(abs_offset_this_iter) +
                        bytes_this_iter;

                    // If at end of media, we cannot read up to a full block. Instead,
                    // read as much as possible and then pad the rest with zeroes.
                    ULONG fill_length = DIFF_BLOCK_SIZE - bytes_this_iter;

                    if (offset.QuadPart + fill_length >
                        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart)
                    {
                        fill_length = (ULONG)(DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart -
                            offset.QuadPart);
                    }

                    status = AIMWrFltrSynchronousReadWrite(
                        DeviceExtension->TargetDeviceObject,
                        NULL,
                        IRP_MJ_READ,
                        BlockBuffer + bytes_this_iter,
                        fill_length,
                        &offset,
                        NULL,
                        &io_status);

                    if (!NT_SUCCESS(status))
                    {
                        DbgPrint("AIMWrFltrDeferredWrite: Fill read from original device failed: 0x%X\n",
                            status);

                        //KdBreakPoint();

                        return status;
                    }

                    if (io_status.Information != fill_length)
                    {
                        DbgPrint("AIMWrFltrDeferredWrite: Fill read request 0x%X bytes, got 0x%IX.\n",
                            (ULONG)(DIFF_BLOCK_SIZE - bytes_this_iter), io_status.Information);
                    }

                    ULONG pad_length = (ULONG)(DIFF_BLOCK_SIZE - bytes_this_iter - io_status.Information);

                    if (pad_length > 0)
                    {
                        DbgPrint("AIMWrFltrDeferredWrite: Padding 0x%X bytes at end block.\n",
                            pad_length);

                        RtlZeroMemory(BlockBuffer + bytes_this_iter + io_status.Information, pad_length);
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
            NULL,
            &io_status);

#pragma warning(suppress: 6102)
        if (NT_SUCCESS(status) &&
            io_status.Information != bytes_this_iter)
        {
            DbgPrint("AIMWrFltrDeferredWrite: Write request 0x%X bytes, done 0x%IX.\n",
                bytes_this_iter, io_status.Information);

#if DBG
            if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                DbgBreakPoint();
#endif

            status = STATUS_DISK_CORRUPT_ERROR;
        }

        if (!NT_SUCCESS(status))
        {
            DbgPrint("AIMWrFltrDeferredWrite: IRQL=%i Write 0x%X bytes at 0x%I64X to diff device failed: 0x%X\n",
                (int)KeGetCurrentIrql(), bytes_this_iter, lower_offset.QuadPart, status);

#if DBG
            if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                DbgBreakPoint();
#endif

            return status;
        }

        if (DeviceExtension->AllocationTable[i] != block_address)
        {
            DeviceExtension->AllocationTable[i] = block_address;
        }
    }

    if (Irp->Irp != NULL)
    {
        Irp->Irp->IoStatus.Information = Irp->IoStack.Parameters.Write.Length;
    }

    return STATUS_SUCCESS;
}

NTSTATUS
AIMWrFltrDeferredFlushBuffers(
    PDEVICE_EXTENSION DeviceExtension,
    PCACHED_IRP Irp)
{
    IO_STATUS_BLOCK io_status;

    NTSTATUS status = AIMWrFltrSynchronousReadWrite(
        DeviceExtension->DiffDeviceObject,
        DeviceExtension->DiffFileObject,
        Irp->IoStack.MajorFunction,
        NULL,
        0,
        NULL,
        Irp->Irp != NULL ? Irp->Irp->Tail.Overlay.Thread : NULL,
        &io_status);

    KdPrint(("AIMWrFltrDeferredFlushBuffers: Flush buffers complete.\n"));

    return status;
}

NTSTATUS
AIMWrFltrFlushBuffers(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
{
    //KdPrint(("AIMWrFltrFlushBuffers: DeviceObject %p Irp %p\n",
    //    DeviceObject, Irp));

    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    if (device_extension->ShutdownThread)
    {
        return AIMWrFltrHandleRemovedDevice(Irp);
    }

    if (!device_extension->Statistics.IsProtected ||
        !device_extension->Statistics.Initialized)
    {
        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }

    Irp->IoStatus.Information = 0;
    
    KLOCK_QUEUE_HANDLE lock_handle;
    KIRQL current_irql = PASSIVE_LEVEL;

    if (device_extension->Statistics.IgnoreFlushBuffers)
    {

#if DBG

        ULONG items_in_queue = AIMWrFltrIsQueueDeeperThan(device_extension, MAXULONG, TRUE, &current_irql);

        DbgPrint(
            "AIMWrFltrFlushBuffers: Ignoring flush request, %u items in queue.\n",
            items_in_queue);

#endif

        Irp->IoStatus.Status = STATUS_SUCCESS;
        IoCompleteRequest(Irp, IO_DISK_INCREMENT);
        return STATUS_SUCCESS;
    }

    PCACHED_IRP cached_irp = CACHED_IRP::CreateEnqueuedIrp(Irp);

    if (cached_irp == NULL)
    {
#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    InterlockedIncrement64(
        &device_extension->Statistics.DeferredWriteRequests);

    //
    // Acquire the remove lock so that device will not be removed while
    // processing this irp.
    //
    NTSTATUS status = IoAcquireRemoveLock(&device_extension->RemoveLock, cached_irp);

    if (!NT_SUCCESS(status))
    {
        DbgPrint(
            "AIMWrFltrFlushBuffers: Remove lock failed flush type Irp: 0x%X\n",
            status);

#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        delete cached_irp;

        Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
        current_irql);

    if (IsListEmpty(&device_extension->ListHead))
    {
        KdPrint(("AIMWrFltrFlushBuffers: Completing flush request with empty queue.\n"));

        AIMWrFltrReleaseLock(&lock_handle, &current_irql);

        Irp->IoStatus.Status = STATUS_SUCCESS;
        IoCompleteRequest(Irp, IO_DISK_INCREMENT);

        IoReleaseRemoveLock(&device_extension->RemoveLock, cached_irp);

        delete cached_irp;

        return STATUS_SUCCESS;
    }

#if DBG

    ULONG items_in_queue = AIMWrFltrIsQueueDeeperThan(device_extension, MAXULONG, FALSE, NULL);

    DbgPrint(
        "AIMWrFltrFlushBuffers: Queuing flush request, %u items in queue.\n",
        items_in_queue);

#endif

    IoMarkIrpPending(Irp);

    InsertTailList(&device_extension->ListHead,
        &cached_irp->ListEntry);

    AIMWrFltrReleaseLock(&lock_handle, &current_irql);

    KeSetEvent(&device_extension->ListEvent, 0, FALSE);

    return STATUS_PENDING;
}

#ifdef FSCTL_FILE_LEVEL_TRIM

NTSTATUS
AIMWrFltrDeferredManageDataSetAttributes(
PDEVICE_EXTENSION DeviceExtension,
PCACHED_IRP Irp,
PUCHAR BlockBuffer)
{
    PIO_STACK_LOCATION io_stack = &Irp->IoStack;

    KEVENT event;
    KeInitializeEvent(&event, SynchronizationEvent, FALSE);

    PDEVICE_MANAGE_DATA_SET_ATTRIBUTES attrs =
        (PDEVICE_MANAGE_DATA_SET_ATTRIBUTES)
        Irp->Buffer;

    if ((attrs->Action != DeviceDsmAction_Trim) ||
        (io_stack->Parameters.DeviceIoControl.InputBufferLength <
        (attrs->DataSetRangesOffset +
        attrs->DataSetRangesLength)))
    {
        return STATUS_INVALID_PARAMETER;
    }

    int items = attrs->DataSetRangesLength /
        sizeof(DEVICE_DATA_SET_RANGE);

    PDEVICE_DATA_SET_RANGE range = (PDEVICE_DATA_SET_RANGE)
        ((PUCHAR)attrs + attrs->DataSetRangesOffset);
    
    int allocated = 0;

#if DBG
    static bool break_flag = false;
    if (break_flag)
    {
        DbgBreakPoint();
    }
#endif

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
#if DBG
            if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                DbgBreakPoint();
#endif

            return STATUS_END_OF_MEDIA;
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

        return STATUS_SUCCESS;
    }

    SIZE_T lower_mdsa_size = FIELD_OFFSET(FILE_LEVEL_TRIM, Ranges) + (allocated *
        sizeof(FILE_LEVEL_TRIM_RANGE));

    if (lower_mdsa_size > DIFF_BLOCK_SIZE)
    {
#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        return STATUS_INVALID_PARAMETER;
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
#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        return STATUS_INSUFFICIENT_RESOURCES;
    }

    PIO_STACK_LOCATION lower_io_stack = IoGetNextIrpStackLocation(lower_irp);
    lower_io_stack->MajorFunction = IRP_MJ_FILE_SYSTEM_CONTROL;
    lower_io_stack->FileObject = DeviceExtension->DiffFileObject;

    NTSTATUS status = IoCallDriver(DeviceExtension->DiffDeviceObject, lower_irp);

    if (status == STATUS_PENDING)
    {
        KeWaitForSingleObject(&event, Executive, KernelMode, FALSE, NULL);

        status = io_status.Status;
    }

    return status;
}

#endif
