#include "aimwrfltr.h"

#include <scsi.h>

NTSTATUS
AIMWrFltrDeviceControl(PDEVICE_OBJECT DeviceObject, PIRP Irp)
{
    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    if (device_extension->ShutdownThread)
    {
        return AIMWrFltrHandleRemovedDevice(Irp);
    }

    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    if (device_extension->Statistics.IsProtected &&
        !device_extension->Statistics.Initialized)
    {
        (void)AIMWrFltrInitializeDiffDevice(device_extension);
    }

    NTSTATUS status;

    Irp->IoStatus.Information = 0;

    switch (io_stack->Parameters.DeviceIoControl.IoControlCode)
    {
    case IOCTL_AIMWRFLTR_GET_DEVICE_DATA:
    {
        if (io_stack->Parameters.DeviceIoControl.OutputBufferLength <
            sizeof(AIMWRFLTR_DEVICE_STATISTICS))
        {
            status = STATUS_BUFFER_TOO_SMALL;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        RtlCopyMemory(Irp->AssociatedIrp.SystemBuffer,
            &device_extension->Statistics,
            sizeof(AIMWRFLTR_DEVICE_STATISTICS));

        status = STATUS_SUCCESS;

        Irp->IoStatus.Status = status;
        Irp->IoStatus.Information = sizeof(AIMWRFLTR_DEVICE_STATISTICS);
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return status;
    }

    case IOCTL_AIMWRFLTR_READ_PRIVATE_DATA:
    {
        if (!device_extension->Statistics.IsProtected)
        {
            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }

        if (!device_extension->Statistics.Initialized)
        {
            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if ((io_stack->Parameters.DeviceIoControl.InputBufferLength <
            sizeof(LONGLONG)) ||
            (io_stack->Parameters.DeviceIoControl.OutputBufferLength == 0))
        {
            status = STATUS_INVALID_PARAMETER;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if (device_extension->DiffFileObject == NULL)
        {
            status = STATUS_DEVICE_NOT_CONNECTED;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if ((device_extension->Statistics.DiffDeviceVbr.Fields.Head.
            OffsetToPrivateData == 0) || (device_extension->Statistics.
                DiffDeviceVbr.Fields.Head.SizeOfPrivateData == 0))
        {
            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        PLARGE_INTEGER offset = (PLARGE_INTEGER)Irp->AssociatedIrp.SystemBuffer;

        if ((offset->QuadPart < 0) || (offset->QuadPart >=
            (device_extension->Statistics.DiffDeviceVbr.Fields.Head.
                SizeOfPrivateData << SECTOR_BITS)))
        {
            status = STATUS_END_OF_FILE;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        LARGE_INTEGER lower_offset;
        lower_offset.QuadPart = (device_extension->Statistics.DiffDeviceVbr.
            Fields.Head.OffsetToPrivateData << SECTOR_BITS) +
            offset->QuadPart;

        ULONG lower_length = (ULONG)min(
            io_stack->Parameters.DeviceIoControl.OutputBufferLength,
            (device_extension->Statistics.DiffDeviceVbr.Fields.Head.
                SizeOfPrivateData << SECTOR_BITS) - offset->QuadPart);

        PSCATTERED_IRP scatter;
        status = SCATTERED_IRP::Create(
            &scatter,
            DeviceObject,
            Irp,
            &device_extension->RemoveLock,
            NULL);

        if (!NT_SUCCESS(status))
        {
            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        PIRP lower_irp = scatter->BuildIrp(
            IRP_MJ_READ,
            device_extension->DiffDeviceObject,
            device_extension->DiffFileObject,
            0,
            lower_length,
            &lower_offset);

        if (lower_irp != NULL)
        {
            IoCallDriver(device_extension->DiffDeviceObject, lower_irp);
        }

        scatter->Complete();

        return STATUS_PENDING;
    }

    case IOCTL_AIMWRFLTR_WRITE_PRIVATE_DATA:
    {
        if (!device_extension->Statistics.IsProtected)
        {
            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }

        if (!device_extension->Statistics.Initialized)
        {
            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if ((io_stack->Parameters.DeviceIoControl.InputBufferLength <
            sizeof(LONGLONG)) ||
            (io_stack->Parameters.DeviceIoControl.OutputBufferLength == 0))
        {
            status = STATUS_INVALID_PARAMETER;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if (device_extension->DiffFileObject == NULL)
        {
            status = STATUS_DEVICE_NOT_CONNECTED;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if ((device_extension->Statistics.DiffDeviceVbr.Fields.Head.
            OffsetToPrivateData == 0) || (device_extension->Statistics.
                DiffDeviceVbr.Fields.Head.SizeOfPrivateData == 0))
        {
            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        PLARGE_INTEGER offset = (PLARGE_INTEGER)Irp->AssociatedIrp.SystemBuffer;

        if ((offset->QuadPart < 0) || (offset->QuadPart >=
            (device_extension->Statistics.DiffDeviceVbr.Fields.Head.
                SizeOfPrivateData << SECTOR_BITS)))
        {
            status = STATUS_END_OF_FILE;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        LARGE_INTEGER lower_offset;
        lower_offset.QuadPart = (device_extension->Statistics.DiffDeviceVbr.
            Fields.Head.OffsetToPrivateData << SECTOR_BITS) +
            offset->QuadPart;

        ULONG lower_length = (ULONG)min(
            io_stack->Parameters.DeviceIoControl.OutputBufferLength,
            (device_extension->Statistics.DiffDeviceVbr.Fields.Head.
                SizeOfPrivateData << SECTOR_BITS) - offset->QuadPart);

        PSCATTERED_IRP scatter;
        status = SCATTERED_IRP::Create(
            &scatter,
            DeviceObject,
            Irp,
            &device_extension->RemoveLock,
            NULL);

        if (!NT_SUCCESS(status))
        {
            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        PIRP lower_irp = scatter->BuildIrp(
            IRP_MJ_WRITE,
            device_extension->DiffDeviceObject,
            device_extension->DiffFileObject,
            0,
            lower_length,
            &lower_offset);

        if (lower_irp != NULL)
        {
            IoCallDriver(device_extension->DiffDeviceObject, lower_irp);
        }
        
        scatter->Complete();

        return STATUS_PENDING;
    }

    case IOCTL_AIMWRFLTR_READ_LOG_DATA:
    {
        if (!device_extension->Statistics.IsProtected)
        {
            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }

        if (!device_extension->Statistics.Initialized)
        {
            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if ((io_stack->Parameters.DeviceIoControl.InputBufferLength <
            sizeof(LONGLONG)) ||
            (io_stack->Parameters.DeviceIoControl.OutputBufferLength == 0))
        {
            status = STATUS_INVALID_PARAMETER;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if ((device_extension->Statistics.DiffDeviceVbr.Fields.Head.
            OffsetToLogData == 0) || (device_extension->Statistics.
                DiffDeviceVbr.Fields.Head.SizeOfLogData == 0))
        {
            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        PLARGE_INTEGER offset = (PLARGE_INTEGER)Irp->AssociatedIrp.SystemBuffer;

        if ((offset->QuadPart < 0) || (offset->QuadPart >=
            (device_extension->Statistics.DiffDeviceVbr.Fields.Head.
                SizeOfLogData << SECTOR_BITS)))
        {
            status = STATUS_END_OF_FILE;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        LARGE_INTEGER lower_offset;
        lower_offset.QuadPart = (device_extension->Statistics.DiffDeviceVbr.
            Fields.Head.OffsetToLogData << SECTOR_BITS) +
            offset->QuadPart;

        ULONG lower_length = (ULONG)min(
            io_stack->Parameters.DeviceIoControl.OutputBufferLength,
            (device_extension->Statistics.DiffDeviceVbr.Fields.Head.
                SizeOfLogData << SECTOR_BITS) - offset->QuadPart);

        PSCATTERED_IRP scatter;
        status = SCATTERED_IRP::Create(
            &scatter,
            DeviceObject,
            Irp,
            &device_extension->RemoveLock,
            NULL);

        if (!NT_SUCCESS(status))
        {
            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        PIRP lower_irp = scatter->BuildIrp(
            IRP_MJ_READ,
            device_extension->DiffDeviceObject,
            device_extension->DiffFileObject,
            0,
            lower_length,
            &lower_offset);

        if (lower_irp != NULL)
        {
            IoCallDriver(device_extension->DiffDeviceObject, lower_irp);
        }

        scatter->Complete();

        return STATUS_PENDING;
    }

    case IOCTL_AIMWRFLTR_WRITE_LOG_DATA:
    {
        if (!device_extension->Statistics.IsProtected)
        {
            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }

        if (!device_extension->Statistics.Initialized)
        {
            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if ((io_stack->Parameters.DeviceIoControl.InputBufferLength <
            sizeof(LONGLONG)) ||
            (io_stack->Parameters.DeviceIoControl.OutputBufferLength == 0))
        {
            status = STATUS_INVALID_PARAMETER;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if ((device_extension->Statistics.DiffDeviceVbr.Fields.Head.
            OffsetToLogData == 0) || (device_extension->Statistics.
                DiffDeviceVbr.Fields.Head.SizeOfLogData == 0))
        {
            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        PLARGE_INTEGER offset = (PLARGE_INTEGER)Irp->AssociatedIrp.SystemBuffer;

        if ((offset->QuadPart < 0) || (offset->QuadPart >=
            (device_extension->Statistics.DiffDeviceVbr.Fields.Head.
                SizeOfLogData << SECTOR_BITS)))
        {
            status = STATUS_END_OF_FILE;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        LARGE_INTEGER lower_offset;
        lower_offset.QuadPart = (device_extension->Statistics.DiffDeviceVbr.
            Fields.Head.OffsetToLogData << SECTOR_BITS) +
            offset->QuadPart;

        ULONG lower_length = (ULONG)min(
            io_stack->Parameters.DeviceIoControl.OutputBufferLength,
            (device_extension->Statistics.DiffDeviceVbr.Fields.Head.
                SizeOfLogData << SECTOR_BITS) - offset->QuadPart);

        PSCATTERED_IRP scatter;
        status = SCATTERED_IRP::Create(
            &scatter,
            DeviceObject,
            Irp,
            &device_extension->RemoveLock,
            NULL);

        if (!NT_SUCCESS(status))
        {
            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        PIRP lower_irp = scatter->BuildIrp(
            IRP_MJ_WRITE,
            device_extension->DiffDeviceObject,
            device_extension->DiffFileObject,
            0,
            lower_length,
            &lower_offset);

        if (lower_irp != NULL)
        {
            IoCallDriver(device_extension->DiffDeviceObject, lower_irp);
        }

        scatter->Complete();

        return STATUS_PENDING;
    }

    case IOCTL_AIMWRFLTR_DELETE_ON_CLOSE:
    {
        FILE_DISPOSITION_INFORMATION file_dispose = { TRUE };

        status = ZwSetInformationFile(device_extension->DiffDeviceHandle,
            &Irp->IoStatus, &file_dispose, sizeof(file_dispose),
            FileDispositionInformation);

        if (!NT_SUCCESS(status))
        {
            DbgPrint("AIMWrFltr:DeviceControl: IOCTL_AIMWRFLTR_DELETE_ON_CLOSE: Error setting disposition flag for diff device: 0x%X\n",
                status);

            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        device_extension->Statistics.IgnoreFlushBuffers = TRUE;

        KdPrint(("AIMWrFltr:DeviceControl: IOCTL_AIMWRFLTR_DELETE_ON_CLOSE: Disposition flag set for diff device.\n"));

        status = STATUS_SUCCESS;

        Irp->IoStatus.Status = status;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return status;
    }

    case IOCTL_DISK_COPY_DATA:
    {
        if (device_extension->Statistics.IsProtected)
        {
            KdPrint(("AIMWrFltr:DeviceControl: IOCTL_DISK_COPY_DATA blocked for protected devices.\n"));

            status = STATUS_INVALID_DEVICE_REQUEST;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }

#ifdef FSCTL_FILE_LEVEL_TRIM
    case IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES:
    {
        if (!device_extension->Statistics.IsProtected)
        {
            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }

        if (!device_extension->Statistics.Initialized)
        {
            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if (io_stack->Parameters.DeviceIoControl.InputBufferLength <
            sizeof(DEVICE_MANAGE_DATA_SET_ATTRIBUTES))
        {
            status = STATUS_INVALID_PARAMETER;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        PDEVICE_MANAGE_DATA_SET_ATTRIBUTES attrs =
            (PDEVICE_MANAGE_DATA_SET_ATTRIBUTES)
            Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.DeviceIoControl.InputBufferLength <
            (attrs->DataSetRangesOffset +
                attrs->DataSetRangesLength))
        {
            status = STATUS_INVALID_PARAMETER;
            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        ULONG items = attrs->DataSetRangesLength /
            sizeof(DEVICE_DATA_SET_RANGE);

        PDEVICE_DATA_SET_RANGE range = (PDEVICE_DATA_SET_RANGE)
            ((PUCHAR)attrs + attrs->DataSetRangesOffset);

        if (attrs->Action == DeviceDsmAction_Trim)
        {
            InterlockedIncrement64(
                &device_extension->Statistics.TrimRequests);

            bool allocated = false;

            for (ULONG i = 0; (!allocated) && (i < items); i++)
            {
                if (range[i].LengthInBytes == 0)
                    continue;

                LONG first = (LONG)
                    DIFF_GET_BLOCK_NUMBER(range[i].StartingOffset);
                LONG last = (LONG)
                    DIFF_GET_BLOCK_NUMBER(range[i].StartingOffset +
                        range[i].LengthInBytes - 1);

                ULONGLONG length_done = 0;

                for (
                    LONG b = first;
                    (!allocated) && (b <= last);
                    b++)
                {
                    LONGLONG abs_offset_this_iter =
                        range[i].StartingOffset + length_done;
                    ULONG page_offset_this_iter =
                        DIFF_GET_BLOCK_OFFSET(abs_offset_this_iter);
                    ULONGLONG bytes_this_iter =
                        range[i].LengthInBytes - length_done;

                    if ((page_offset_this_iter + bytes_this_iter) >
                        DIFF_BLOCK_SIZE)
                    {
                        bytes_this_iter = DIFF_BLOCK_SIZE -
                            page_offset_this_iter;
                    }

                    length_done += bytes_this_iter;

                    if (device_extension->AllocationTable[b] !=
                        DIFF_BLOCK_UNALLOCATED)
                    {
                        allocated = true;
                    }
                }

                if (!allocated)
                {
                    //KdPrint((
                    //    "AIMWrFltrControl: Trim request 0x%I64X bytes at 0x%I64X (not yet allocated)\n",
                    //    range[i].LengthInBytes, range[i].StartingOffset));
                }
            }

            if (!allocated)
            {
                //KdPrint((
                //    "AIMWrFltrControl: None of trim blocks are yet allocated.\n"));

                status = STATUS_SUCCESS;

                Irp->IoStatus.Status = status;
                IoCompleteRequest(Irp, IO_NO_INCREMENT);

                return status;
            }

            PCACHED_IRP cached_irp = CACHED_IRP::CreateFromWriteIrp(DeviceObject, Irp);

            if (cached_irp == NULL)
            {
                status = STATUS_INSUFFICIENT_RESOURCES;

                Irp->IoStatus.Status = status;
                IoCompleteRequest(Irp, IO_NO_INCREMENT);

                return status;
            }

            //
            // Acquire the remove lock so that device will not be removed
            // while processing this irp.
            //
            status = IoAcquireRemoveLock(&device_extension->RemoveLock, cached_irp);

            if (!NT_SUCCESS(status))
            {
                DbgPrint(
                    "AIMWrFltrControl: Remove lock failed for trim: 0x%X\n",
                    status);

                Irp->IoStatus.Status = status;
                IoCompleteRequest(Irp, IO_NO_INCREMENT);

                KdBreakPoint();

                return status;
            }

            Irp->IoStatus.Status = STATUS_SUCCESS;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);

            KLOCK_QUEUE_HANDLE lock_handle;

            KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

            AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
                lowest_assumed_irql);

            InsertTailList(&device_extension->ListHead,
                &cached_irp->ListEntry);

            AIMWrFltrReleaseLock(&lock_handle, &lowest_assumed_irql);

            KeSetEvent(&device_extension->ListEvent, 0, FALSE);

            return STATUS_SUCCESS;
        }

        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }
#endif

    case IOCTL_DISK_IS_WRITABLE:
    {
        if (device_extension->Statistics.Initialized)
        {
            status = STATUS_SUCCESS;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_DISK_INCREMENT);

            return status;
        }
        else if (device_extension->Statistics.IsProtected)
        {
            KdPrint(("AIMWrFltr:DeviceControl: IOCTL_DISK_IS_WRITABLE for protected but not yet initialized device\n"));

            status = STATUS_DEVICE_NOT_READY;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);

            return status;
        }
        else
        {
            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }
    }

    case IOCTL_SCSI_MINIPORT:
    case IOCTL_ATA_MINIPORT:
#ifdef IOCTL_DISK_VOLUMES_ARE_READY
    case IOCTL_DISK_VOLUMES_ARE_READY:
#endif
    {
        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }

    case IOCTL_SCSI_PASS_THROUGH_DIRECT:
    {
        if (device_extension->Statistics.IsProtected)
        {
            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }

        if (io_stack->Parameters.DeviceIoControl.InputBufferLength >= sizeof(SCSI_PASS_THROUGH_DIRECT))
        {
            PSCSI_PASS_THROUGH_DIRECT pSrb = (PSCSI_PASS_THROUGH_DIRECT)Irp->AssociatedIrp.SystemBuffer;
            PCDB pCdb = (PCDB)pSrb->Cdb;

            switch (pCdb->CDB6GENERIC.OperationCode)
            {
            case SCSIOP_INQUIRY:
            case SCSIOP_TEST_UNIT_READY:
            case SCSIOP_READ_CAPACITY:
            case SCSIOP_READ_CAPACITY16:
                return AIMWrFltrSendToNextDriver(DeviceObject, Irp);

#ifdef SCSIOP_UNMAP
            case SCSIOP_UNMAP:
                KdPrint(("AIMWrFltr:DeviceControl: Ignoring SCSIOP_UNMAP\n"));

                break;
#endif

            default:
                KdPrint(("AIMWrFltr:DeviceControl: Attempt IOCTL_SCSI_PASS_THROUGH_DIRECT. Operation %#.2x\n",
                    (int)pCdb->CDB6GENERIC.OperationCode));
            }

#if DBG
            static bool break_here = true;

            if (break_here && !KD_DEBUGGER_NOT_PRESENT)
            {
                KdBreakPoint();
            }
#endif
        }
        else
        {
            KdPrint(("AIMWrFltr:DeviceControl: Bad formatted IOCTL_SCSI_PASS_THROUGH_DIRECT sent to protected disk.\n"));
        }

        status = STATUS_INVALID_DEVICE_REQUEST;

        Irp->IoStatus.Status = status;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return status;
    }

    default:
    {
        if (device_extension->Statistics.IsProtected &&
            (ACCESS_FROM_CTL_CODE(io_stack->Parameters.DeviceIoControl.IoControlCode) &
                FILE_WRITE_ACCESS) != 0 &&
            (DEVICE_TYPE_FROM_CTL_CODE(
                io_stack->Parameters.DeviceIoControl.IoControlCode) ==
                IOCTL_SCSI_BASE || DEVICE_TYPE_FROM_CTL_CODE(
                    io_stack->Parameters.DeviceIoControl.IoControlCode) ==
                IOCTL_DISK_BASE || DEVICE_TYPE_FROM_CTL_CODE(
                    io_stack->Parameters.DeviceIoControl.IoControlCode) ==
                IOCTL_STORAGE_BASE))
        {
            KdPrint(("AIMWrFltr:DeviceControl: Destructive direct IOCTL %#x CTL_CODE(%s, %s, %s, %s) access to disk with protected disk\n",
                io_stack->Parameters.DeviceIoControl.IoControlCode,
                AIMWrFltrGetIoctlDeviceTypeName(io_stack->Parameters.DeviceIoControl.IoControlCode),
                AIMWrFltrGetIoctlFunctionName(io_stack->Parameters.DeviceIoControl.IoControlCode),
                AIMWrFltrGetIoctlMethodName(io_stack->Parameters.DeviceIoControl.IoControlCode),
                AIMWrFltrGetIoctlAccessName(io_stack->Parameters.DeviceIoControl.IoControlCode)));

            status = STATUS_INVALID_DEVICE_REQUEST;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if (device_extension->Statistics.FakeNonRemovable &&
            io_stack->Parameters.DeviceIoControl.IoControlCode == IOCTL_STORAGE_QUERY_PROPERTY &&
            io_stack->Parameters.DeviceIoControl.InputBufferLength >= sizeof(STORAGE_PROPERTY_QUERY))
        {
            PSTORAGE_PROPERTY_QUERY query = (PSTORAGE_PROPERTY_QUERY)
                Irp->AssociatedIrp.SystemBuffer;

            KdPrint(("AIMWrFltr:DeviceControl: IOCTL_STORAGE_QUERY_PROPERTY QueryType=%i, PropertyId=%i.\n",
                query->QueryType, query->PropertyId));

            if (query->PropertyId == StorageDeviceProperty)
            {
                KdPrint(("AIMWrFltr:DeviceControl: StorageDeviceProperty, requesting physical properties.\n"));
                
                status = AIMWrFltrForwardIrpSynchronous(DeviceObject, Irp);

                if (!NT_SUCCESS(status))
                {
                    KdPrint(("AIMWrFltr:DeviceControl: StorageDeviceProperty, physical properties request failed: 0x%X.\n", status));

                    IoCompleteRequest(Irp, IO_NO_INCREMENT);
                    return status;
                }

                if (Irp->IoStatus.Information > FIELD_OFFSET(STORAGE_DEVICE_DESCRIPTOR, RemovableMedia))
                {
                    PSTORAGE_DEVICE_DESCRIPTOR descriptor = (PSTORAGE_DEVICE_DESCRIPTOR)Irp->AssociatedIrp.SystemBuffer;

                    if (descriptor->RemovableMedia)
                    {
                        KdPrint(("AIMWrFltr:DeviceControl: StorageDeviceProperty, reporting non-removable.\n"));
                        descriptor->RemovableMedia = FALSE;
                    }
                }

                IoCompleteRequest(Irp, IO_NO_INCREMENT);
                return status;
            }
            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }

        KdPrint(("AIMWrFltr:DeviceControl: Forwarding down read-only IOCTL %#x CTL_CODE(%s, %s, %s, %s).\n",
            io_stack->Parameters.DeviceIoControl.IoControlCode,
            AIMWrFltrGetIoctlDeviceTypeName(io_stack->Parameters.DeviceIoControl.IoControlCode),
            AIMWrFltrGetIoctlFunctionName(io_stack->Parameters.DeviceIoControl.IoControlCode),
            AIMWrFltrGetIoctlMethodName(io_stack->Parameters.DeviceIoControl.IoControlCode),
            AIMWrFltrGetIoctlAccessName(io_stack->Parameters.DeviceIoControl.IoControlCode)));

        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }
    break;
    }
}				// end AIMWrFltrDeviceControl()


