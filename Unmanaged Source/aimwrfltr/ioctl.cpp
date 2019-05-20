#include "aimwrfltr.h"

NTSTATUS
AIMWrFltrDeviceControl(PDEVICE_OBJECT DeviceObject, PIRP Irp)
{
    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

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

        if (!device_extension->Statistics.IsProtected)
        {
            KdPrint((
                "AIMWrFltrControl: Passing through IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES action [0x%X]\n",
                attrs->Action));

            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }

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

            //
            // Acquire the remove lock so that device will not be removed
            // while processing this irp.
            //
            status = IoAcquireRemoveLock(&device_extension->RemoveLock, Irp);

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

            IoMarkIrpPending(Irp);

            ExInterlockedInsertTailList(&device_extension->ListHead,
                &Irp->Tail.Overlay.ListEntry,
                &device_extension->ListLock);

            KeSetEvent(&device_extension->ListEvent, 0, FALSE);

            return STATUS_PENDING;
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
    {
        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
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
            KdPrint(("AIMWrFltr:DeviceControl: Destructive direct IOCTL %#x access to disk with protected partition.\n",
                io_stack->Parameters.DeviceIoControl.IoControlCode));

            status = STATUS_INVALID_DEVICE_REQUEST;

            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        if (IOCTL_STORAGE_QUERY_PROPERTY ==
            io_stack->Parameters.DeviceIoControl.IoControlCode)
        {
            PSTORAGE_PROPERTY_QUERY query = (PSTORAGE_PROPERTY_QUERY)
                Irp->AssociatedIrp.SystemBuffer;

            KdPrint(("AIMWrFltr:DeviceControl: IOCTL_STORAGE_QUERY_PROPERTY QueryType=%i, PropertyId=%i.\n",
                query->QueryType, query->PropertyId));

            if (query->PropertyId > StorageDeviceWriteAggregationProperty)
            {
                status = STATUS_INVALID_DEVICE_REQUEST;

                Irp->IoStatus.Status = status;
                IoCompleteRequest(Irp, IO_NO_INCREMENT);
                return status;
            }

            return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
        }

        KdPrint(("AIMWrFltr:DeviceControl: Forwarding down read-only IOCTL %#x.\n",
            io_stack->Parameters.DeviceIoControl.IoControlCode));

        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }
    break;
    }
}				// end AIMWrFltrDeviceControl()


