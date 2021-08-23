
/// srbioctl.c
/// Handles control requests sent through IOCTL_SCSI_MINIPORT. These control
/// requests are used for example to add or remove virtual disks and similar
/// tasks.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#include "phdskmnt.h"

/**************************************************************************************************/
/*                                                                                                */
/**************************************************************************************************/
VOID
ScsiIoControl(
__in pHW_HBA_EXT            pHBAExt,    // Adapter device-object extension from port driver.
__in PSCSI_REQUEST_BLOCK    pSrb,
__in pResultType            pResult,
__inout __deref PKIRQL      LowestAssumedIrql
)
{
    PSRB_IO_CONTROL  srb_io_control = (PSRB_IO_CONTROL)pSrb->DataBuffer;

    *pResult = ResultDone;

    if (pSrb->DataTransferLength < sizeof(SRB_IO_CONTROL)
        || ((srb_io_control->HeaderLength != sizeof(SRB_IO_CONTROL)) ||
        (srb_io_control->HeaderLength + srb_io_control->Length > pSrb->DataTransferLength)))
    {
        KdPrint2(("PhDskMnt::ScsiIoControl: Malformed MiniportIOCtl detected.\n",
            sizeof(srb_io_control->Signature),
            srb_io_control->Signature));

        ScsiSetError(pSrb, SRB_STATUS_INVALID_REQUEST);
        goto Done;
    }

    if (memcmp(srb_io_control->Signature, IMSCSI_FUNCTION_SIGNATURE,
        sizeof(IMSCSI_FUNCTION_SIGNATURE) - 1))
    {
        KdPrint2(("PhDskMnt::ScsiIoControl: MiniportIOCtl sig '%.*s' not supported\n",
            sizeof(srb_io_control->Signature),
            srb_io_control->Signature));

        ScsiSetError(pSrb, SRB_STATUS_INVALID_REQUEST);
        goto Done;
    }

    KdPrint2(("PhDskMnt::ScsiIoControl: Miniport IOCtl ControlCode = %#x\n",
        srb_io_control->ControlCode));

    switch (srb_io_control->ControlCode)
    {
    case SMP_IMSCSI_CHECK:
    {
        KdPrint2(("PhDskMnt::ScsiIoControl: Request to complete SRBs.\n"));
        srb_io_control->ReturnCode = STATUS_SUCCESS;
        ScsiSetSuccess(pSrb, 0);

        break;
    }

    case SMP_IMSCSI_CREATE_DEVICE:
    {
        PSRB_IMSCSI_CREATE_DATA srb_buffer = (PSRB_IMSCSI_CREATE_DATA)pSrb->DataBuffer;

        if ((srb_buffer->SrbIoControl.HeaderLength + srb_buffer->SrbIoControl.Length <
            FIELD_OFFSET(SRB_IMSCSI_CREATE_DATA, Fields.FileName))
            || (srb_buffer->Fields.FileNameLength +
            (ULONG)FIELD_OFFSET(SRB_IMSCSI_CREATE_DATA, Fields.FileName) >
            pSrb->DataTransferLength))
        {
            KdPrint(("PhDskMnt::ScsiIoControl: Bad SMP_IMSCSI_CREATE_DEVICE request.\n"));

            ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
            goto Done;
        }

        ImScsiCreateDevice(pHBAExt, pSrb, pResult, LowestAssumedIrql);

        break;
    }

    case SMP_IMSCSI_REMOVE_DEVICE:
    {
        PSRB_IMSCSI_REMOVE_DEVICE srb_buffer = (PSRB_IMSCSI_REMOVE_DEVICE)pSrb->DataBuffer;

        KdPrint2(("PhDskMnt::ScsiIoControl: Request to remove device.\n"));

        if (!SRB_IO_CONTROL_SIZE_OK(srb_buffer))
        {
            KdPrint(("PhDskMnt::ScsiIoControl: Bad SMP_IMSCSI_REMOVE_DEVICE request.\n"));

            ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
            goto Done;
        }

        srb_io_control->ReturnCode = ImScsiRemoveDevice(pHBAExt, &srb_buffer->DeviceNumber, LowestAssumedIrql);

        ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

        break;
    }

    case SMP_IMSCSI_QUERY_VERSION:
    {
        PSRB_IMSCSI_QUERY_VERSION srb_buffer = (PSRB_IMSCSI_QUERY_VERSION)pSrb->DataBuffer;

        KdPrint2(("PhDskMnt::ScsiIoControl: Request for driver version.\n"));

        if (!SRB_IO_CONTROL_SIZE_OK(srb_buffer))
        {
            srb_io_control->ReturnCode = IMSCSI_DRIVER_VERSION;
            srb_io_control->Length = 0;

            ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

            break;
        }

        srb_io_control->ReturnCode = IMSCSI_DRIVER_VERSION;
        srb_io_control->Length = sizeof(*srb_buffer) - sizeof(srb_buffer->SrbIoControl);
        srb_buffer->SubVersion = PHDSKMNT_VERSION_ULONG;

        ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

        break;
    }

    case SMP_IMSCSI_QUERY_DEVICE:
    {
        PSRB_IMSCSI_CREATE_DATA srb_buffer = (PSRB_IMSCSI_CREATE_DATA)pSrb->DataBuffer;

        KdPrint2(("PhDskMnt::ScsiIoControl: Request SMP_IMSCSI_QUERY_DEVICE.\n"));

        if (!SRB_IO_CONTROL_SIZE_OK(srb_buffer))
        {
            KdPrint(("PhDskMnt::ScsiIoControl: Bad SMP_IMSCSI_QUERY_DEVICE request.\n"));

            pSrb->DataTransferLength = 0;
            ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
            goto Done;
        }

        srb_io_control->ReturnCode = ImScsiQueryDevice(pHBAExt, srb_buffer, &pSrb->DataTransferLength, LowestAssumedIrql);

        ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

        break;
    }

    case SMP_IMSCSI_QUERY_ADAPTER:
    {
        PSRB_IMSCSI_QUERY_ADAPTER srb_buffer = (PSRB_IMSCSI_QUERY_ADAPTER)pSrb->DataBuffer;

        KdPrint2(("PhDskMnt::ScsiIoControl: Request SMP_IMSCSI_QUERY_ADAPTER.\n"));

        if (!SRB_IO_CONTROL_SIZE_OK(srb_buffer))
        {
            KdPrint(("PhDskMnt::ScsiIoControl: Bad SMP_IMSCSI_QUERY_ADAPTER request.\n"));

            ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
            goto Done;
        }

        srb_io_control->ReturnCode = ImScsiQueryAdapter(pHBAExt, srb_buffer, pSrb->DataTransferLength, LowestAssumedIrql);

        ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

        break;
    }

    case SMP_IMSCSI_SET_DEVICE_FLAGS:
    {
        PSRB_IMSCSI_SET_DEVICE_FLAGS srb_buffer = (PSRB_IMSCSI_SET_DEVICE_FLAGS)pSrb->DataBuffer;

        KdPrint2(("PhDskMnt::ScsiIoControl: Request SMP_IMSCSI_SET_DEVICE_FLAGS.\n"));

        if (!SRB_IO_CONTROL_SIZE_OK(srb_buffer))
        {
            KdPrint(("PhDskMnt::ScsiIoControl: Bad SMP_IMSCSI_SET_DEVICE_FLAGS request.\n"));

            ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
            goto Done;
        }

        srb_io_control->ReturnCode = ImScsiSetFlagsDevice(pHBAExt, srb_buffer, LowestAssumedIrql);

        ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

        break;
    }

    case SMP_IMSCSI_EXTEND_DEVICE:
    {
        PSRB_IMSCSI_EXTEND_DEVICE srb_buffer = (PSRB_IMSCSI_EXTEND_DEVICE)pSrb->DataBuffer;

        KdPrint2(("PhDskMnt::ScsiIoControl: Request SMP_IMSCSI_EXTEND_DEVICE.\n"));

        if (!SRB_IO_CONTROL_SIZE_OK(srb_buffer))
        {
            KdPrint(("PhDskMnt::ScsiIoControl: Bad SMP_IMSCSI_EXTEND_DEVICE request.\n"));

            ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
            goto Done;
        }

        ImScsiExtendDevice(pHBAExt, pSrb, pResult, LowestAssumedIrql, srb_buffer);

        break;
    }

    default:

        DbgPrint("PhDskMnt::ScsiExecute: Unknown IOControl code=0x%X\n", srb_io_control->ControlCode);

        ScsiSetError(pSrb, SRB_STATUS_INVALID_REQUEST);
        break;

    } // end switch

Done:
    KdPrint2(("PhDskMnt::ScsiIoControl: End: *Result=%i\n", (INT)*pResult));

    return;
}

VOID
ImScsiCreateDevice(
__in pHW_HBA_EXT            pHBAExt,
__in PSCSI_REQUEST_BLOCK    pSrb,
__inout __deref pResultType pResult,
__inout __deref PKIRQL      LowestAssumedIrql
)
{
    pHW_LU_EXTENSION        pLUExt = NULL;
    PSRB_IMSCSI_CREATE_DATA new_device = (PSRB_IMSCSI_CREATE_DATA)pSrb->DataBuffer;
    pMP_WorkRtnParms        pWkRtnParms;

    // If auto-selecting device number
    if (new_device->Fields.DeviceNumber.LongNumber == IMSCSI_AUTO_DEVICE_NUMBER)
    {
        KdPrint(("PhDskMnt::ImScsiCreateDevice: Auto-select device number.\n"));

        for (new_device->Fields.DeviceNumber.PathId = 0;
            new_device->Fields.DeviceNumber.PathId < pMPDrvInfoGlobal->MPRegInfo.NumberOfBuses;
            new_device->Fields.DeviceNumber.PathId++)
        {
            for (new_device->Fields.DeviceNumber.Lun = 0;
                new_device->Fields.DeviceNumber.Lun < MAX_LUNS;
                new_device->Fields.DeviceNumber.Lun++)
            {
                for (new_device->Fields.DeviceNumber.TargetId = 0;
                    new_device->Fields.DeviceNumber.TargetId < MAX_TARGETS;
                    new_device->Fields.DeviceNumber.TargetId++)
                {
#ifdef USE_SCSIPORT
                    // With SCSIPORT, reserve device 0:0:0 as control device
                    if (new_device->Fields.DeviceNumber.LongNumber == 0)
                        continue;
#endif

                    ScsiGetLUExtension(
                        pHBAExt,
                        &pLUExt,
                        new_device->Fields.DeviceNumber.PathId,
                        new_device->Fields.DeviceNumber.TargetId,
                        new_device->Fields.DeviceNumber.Lun,
                        LowestAssumedIrql
                        );

                    if (pLUExt == NULL)
                        break;
                }

                if (pLUExt == NULL)
                    break;
            }

            if (pLUExt == NULL)
                break;
        }

        if (pLUExt != NULL)
        {
            KdPrint(("PhDskMnt::ImScsiCreateDevice: No free device number found.\n"));
            new_device->SrbIoControl.ReturnCode = (ULONG)STATUS_NO_MORE_ENTRIES;
            ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
            return;
        }

        KdPrint(("PhDskMnt::ImScsiCreateDevice: PathId=%i, TargetId=%i, Lun=%i.\n",
            (int)new_device->Fields.DeviceNumber.PathId,
            (int)new_device->Fields.DeviceNumber.TargetId,
            (int)new_device->Fields.DeviceNumber.Lun));
    }
    else
    {
        KdPrint(("PhDskMnt::ImScsiCreateDevice: PathId=%i, TargetId=%i, Lun=%i.\n",
            (int)new_device->Fields.DeviceNumber.PathId,
            (int)new_device->Fields.DeviceNumber.TargetId,
            (int)new_device->Fields.DeviceNumber.Lun));

#ifdef USE_SCSIPORT
        if (new_device->Fields.DeviceNumber.LongNumber == 0)
        {
            DbgPrint("PhDskMnt::ImScsiCreateDevice: Device number 0:0:0 is reserved.\n");
            new_device->SrbIoControl.ReturnCode = (ULONG)STATUS_OBJECT_NAME_COLLISION;
            ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
            return;
        }
#endif

        ScsiGetLUExtension(
            pHBAExt,
            &pLUExt,
            new_device->Fields.DeviceNumber.PathId,
            new_device->Fields.DeviceNumber.TargetId,
            new_device->Fields.DeviceNumber.Lun,
            LowestAssumedIrql
            );

        if (pLUExt != NULL)
        {
            KdPrint(("PhDskMnt::ImScsiCreateDevice: Device already exists.\n"));
            new_device->SrbIoControl.ReturnCode = (ULONG)STATUS_OBJECT_NAME_COLLISION;
            ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
            return;
        }
    }

    pWkRtnParms = ImScsiCreateWorkItem(pHBAExt, NULL, pSrb);

    if (pWkRtnParms == NULL)
    {
        DbgPrint("PhDskMnt::ImScsiCreateDevice Failed to allocate work parm structure\n");

        new_device->SrbIoControl.ReturnCode = (ULONG)STATUS_INSUFFICIENT_RESOURCES;
        ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
        return;
    }

    pWkRtnParms->pReqThread = PsGetCurrentThread();

    ObReferenceObject(pWkRtnParms->pReqThread);

    // Queue work item, which will run in the System process.

    new_device->SrbIoControl.ReturnCode = (ULONG)STATUS_PENDING;

    ImScsiScheduleWorkItem(pWkRtnParms, LowestAssumedIrql);

    *pResult = ResultQueued;                          // Indicate queuing.

    StoragePortNotification(BusChangeDetected, pHBAExt, new_device->Fields.DeviceNumber.PathId);

    KdPrint(("PhDskMnt::ImScsiCreateDevice: End: *Result=%i\n", *pResult));

    return;
}

NTSTATUS
ImScsiQueryDevice(
__in pHW_HBA_EXT               pHBAExt,
__in PSRB_IMSCSI_CREATE_DATA   create_data,
__in PULONG                    Length,
__inout __deref PKIRQL              LowestAssumedIrql
)
{
    pHW_LU_EXTENSION        device_extension = NULL;
    UCHAR                   srb_status;

    KdPrint(("PhDskMnt::ImScsiQueryDevice: Device %i:%i:%i.\n",
        (int)create_data->Fields.DeviceNumber.PathId,
        (int)create_data->Fields.DeviceNumber.TargetId,
        (int)create_data->Fields.DeviceNumber.Lun));

    srb_status = ScsiGetLUExtension(
        pHBAExt,
        &device_extension,
        create_data->Fields.DeviceNumber.PathId,
        create_data->Fields.DeviceNumber.TargetId,
        create_data->Fields.DeviceNumber.Lun,
        LowestAssumedIrql
        );

    if (srb_status != SRB_STATUS_SUCCESS)
    {
        KdPrint(("PhDskMnt::ImScsiQueryDevice: Device not found.\n"));
        *Length = sizeof(SRB_IO_CONTROL);
        return STATUS_OBJECT_NAME_NOT_FOUND;
    }

    if (*Length <
        sizeof(SRB_IMSCSI_CREATE_DATA) +
        device_extension->ObjectName.Length +
        device_extension->WriteOverlayFileName.Length +
        sizeof(*create_data->Fields.FileName))
    {
        KdPrint(("PhDskMnt::ImScsiQueryDevice: Buffer too small. Got %u, need %u.\n",
            *Length,
            (ULONG)(sizeof(SRB_IMSCSI_CREATE_DATA) +
                device_extension->ObjectName.Length +
                device_extension->WriteOverlayFileName.Length +
                sizeof(*create_data->Fields.FileName))));

        *Length = sizeof(SRB_IO_CONTROL);
        return STATUS_BUFFER_TOO_SMALL;
    }

    create_data->Fields.DeviceNumber = device_extension->DeviceNumber;
    create_data->Fields.DiskSize = device_extension->DiskSize;
    create_data->Fields.BytesPerSector = 1UL << device_extension->BlockPower;

    create_data->Fields.Flags = 0;
    if (device_extension->ReadOnly)
        create_data->Fields.Flags |= IMSCSI_OPTION_RO;

    if (device_extension->RemovableMedia)
        create_data->Fields.Flags |= IMSCSI_OPTION_REMOVABLE;

    if (device_extension->DeviceType == READ_ONLY_DIRECT_ACCESS_DEVICE)
        create_data->Fields.Flags |= IMSCSI_DEVICE_TYPE_CD | IMSCSI_OPTION_RO;
    else
        create_data->Fields.Flags |= IMSCSI_DEVICE_TYPE_HD;

    if (device_extension->VMDisk)
        create_data->Fields.Flags |= IMSCSI_TYPE_VM;
    else if (device_extension->UseProxy)
        create_data->Fields.Flags |= IMSCSI_TYPE_PROXY;
    else
        create_data->Fields.Flags |= IMSCSI_TYPE_FILE;

    if (device_extension->AWEAllocDisk)
        create_data->Fields.Flags |= IMSCSI_FILE_TYPE_AWEALLOC;
    else if (device_extension->FileObject != NULL)
        create_data->Fields.Flags |= IMSCSI_FILE_TYPE_PARALLEL_IO;

    if (device_extension->Modified)
        create_data->Fields.Flags |= IMSCSI_IMAGE_MODIFIED;

    if (device_extension->Modified)
        create_data->Fields.Flags |= IMSCSI_IMAGE_MODIFIED;

    if (device_extension->SupportsZero)
        create_data->Fields.Flags |= IMSCSI_OPTION_SPARSE_FILE;

    if (device_extension->SharedImage)
        create_data->Fields.Flags |= IMSCSI_OPTION_SHARED_IMAGE;

    if (device_extension->FakeDiskSignature != 0)
        create_data->Fields.Flags |= IMSCSI_FAKE_DISK_SIG;

    if (device_extension->WriteOverlay != NULL)
        create_data->Fields.Flags |= IMSCSI_OPTION_WRITE_OVERLAY;

    create_data->Fields.ImageOffset = device_extension->ImageOffset;

    create_data->Fields.FileNameLength = device_extension->ObjectName.Length;

    if (device_extension->ObjectName.Length > 0)
        RtlCopyMemory(create_data->Fields.FileName,
        device_extension->ObjectName.Buffer,
        device_extension->ObjectName.Length);

    create_data->Fields.WriteOverlayFileNameLength =
        device_extension->WriteOverlayFileName.Length;

    if (device_extension->WriteOverlayFileName.Length > 0)
        RtlCopyMemory(((PUCHAR)create_data->Fields.FileName) +
            create_data->Fields.FileNameLength,
            device_extension->WriteOverlayFileName.Buffer,
            device_extension->WriteOverlayFileName.Length);

    /// Copy handle to write overlay if enough size for that
    if (device_extension->WriteOverlay != NULL &&
        *Length >= FIELD_OFFSET(SRB_IMSCSI_CREATE_DATA, Fields.FileName) +
        create_data->Fields.FileNameLength +
        create_data->Fields.WriteOverlayFileNameLength +
        sizeof(HANDLE))
    {
        PHANDLE handle = (PHANDLE)(((PUCHAR)create_data->Fields.FileName) +
            create_data->Fields.FileNameLength +
            create_data->Fields.WriteOverlayFileNameLength);

        *handle = device_extension->WriteOverlay;

        *Length = FIELD_OFFSET(SRB_IMSCSI_CREATE_DATA, Fields.FileName) +
            create_data->Fields.FileNameLength +
            create_data->Fields.WriteOverlayFileNameLength +
            sizeof(HANDLE);
    }
    else
    {
        *Length = FIELD_OFFSET(SRB_IMSCSI_CREATE_DATA, Fields.FileName) +
            create_data->Fields.FileNameLength +
            create_data->Fields.WriteOverlayFileNameLength;
    }

    KdPrint(("PhDskMnt::ImScsiQueryDevice: End.\n"));
    return STATUS_SUCCESS;
}

NTSTATUS
ImScsiQueryAdapter(
__in            pHW_HBA_EXT                 pHBAExt,
__inout __deref PSRB_IMSCSI_QUERY_ADAPTER   data,
__in            ULONG                       max_length,
__inout __deref PKIRQL                      LowestAssumedIrql
)
{
    KLOCK_QUEUE_HANDLE    LockHandle;
    ULONG                 count;
    PLIST_ENTRY           list_ptr;

    KdPrint(("PhDskMnt::ImScsiQueryAdapter:  pHBAExt = 0x%p\n", pHBAExt));

    ImScsiAcquireLock(                   // Serialize the linked list of LUN extensions.              
        &pHBAExt->LUListLock, &LockHandle, *LowestAssumedIrql);

    for (count = 0, list_ptr = pHBAExt->LUList.Flink;
        list_ptr != &pHBAExt->LUList;
        count++, list_ptr = list_ptr->Flink
        )
    {
        pHW_LU_EXTENSION object;
        object = CONTAINING_RECORD(list_ptr, HW_LU_EXTENSION, List);

        if (max_length >= FIELD_OFFSET(SRB_IMSCSI_QUERY_ADAPTER, DeviceList[count]) + sizeof(data->DeviceList[count]))
            data->DeviceList[count] = object->DeviceNumber;
    }

    ImScsiReleaseLock(&LockHandle, LowestAssumedIrql);

    if (max_length >= FIELD_OFFSET(SRB_IMSCSI_QUERY_ADAPTER, NumberOfDevices) + sizeof(data->NumberOfDevices))
        data->NumberOfDevices = count;

    return STATUS_SUCCESS;
}

NTSTATUS
ImScsiSetFlagsDevice(
__in            pHW_HBA_EXT                     pHBAExt,
__inout __deref PSRB_IMSCSI_SET_DEVICE_FLAGS    device_flags,
__inout __deref PKIRQL                          LowestAssumedIrql
)
{
    NTSTATUS ntstatus = STATUS_SUCCESS;
    UCHAR status;
    pHW_LU_EXTENSION device_extension;

    status = ScsiGetLUExtension(
        pHBAExt,
        &device_extension,
        device_flags->DeviceNumber.PathId,
        device_flags->DeviceNumber.TargetId,
        device_flags->DeviceNumber.Lun,
        LowestAssumedIrql
        );

    if ((status != SRB_STATUS_SUCCESS) || (device_extension == NULL))
        return STATUS_OBJECT_NAME_NOT_FOUND;

    // It is not possible to make a file- or proxy virtual disk
    // writable on the fly. (A physical image file or the proxy
    // comm channel might not be opened for writing.)
    if (IMSCSI_READONLY(device_flags->FlagsToChange) &&
        (device_extension->DeviceType != READ_ONLY_DIRECT_ACCESS_DEVICE) &&
        device_extension->VMDisk)
    {
        device_extension->ReadOnly = FALSE;

        device_flags->FlagsToChange &= ~IMSCSI_OPTION_RO;
    }

    if (IMSCSI_REMOVABLE(device_flags->FlagsToChange) &&
        (device_extension->DeviceType != READ_ONLY_DIRECT_ACCESS_DEVICE))
    {
        if (IMSCSI_REMOVABLE(device_flags->FlagValues))
            device_extension->RemovableMedia = TRUE;
        else
            device_extension->RemovableMedia = FALSE;

        device_flags->FlagsToChange &= ~IMSCSI_OPTION_REMOVABLE;
    }

    if (device_flags->FlagsToChange & IMSCSI_IMAGE_MODIFIED)
    {
        if (device_flags->FlagValues & IMSCSI_IMAGE_MODIFIED)
            device_extension->Modified = TRUE;
        else
            device_extension->Modified = FALSE;

        device_flags->FlagsToChange &= ~IMSCSI_IMAGE_MODIFIED;
    }

    if (KeGetCurrentIrql() == PASSIVE_LEVEL)
    {
        if (IMSCSI_SPARSE_FILE(device_flags->FlagsToChange) &&
            IMSCSI_SPARSE_FILE(device_flags->FlagValues) &&
            (!device_extension->UseProxy) &&
            (!device_extension->VMDisk))
        {
            IO_STATUS_BLOCK io_status;
            ntstatus = ZwFsControlFile(
                device_extension->ImageFile,
                NULL,
                NULL,
                NULL,
                &io_status,
                FSCTL_SET_SPARSE,
                NULL,
                0,
                NULL,
                0
                );

            if (NT_SUCCESS(ntstatus))
                device_flags->FlagsToChange &= ~IMSCSI_OPTION_SPARSE_FILE;
        }
    }

    if (device_flags->FlagsToChange == 0)
    {
        ntstatus = STATUS_SUCCESS;
        StoragePortNotification(BusChangeDetected, pHBAExt, device_flags->DeviceNumber.PathId);
    }
    else if (NT_SUCCESS(ntstatus))
        ntstatus = STATUS_INVALID_DEVICE_REQUEST;

    return ntstatus;
}

VOID
ImScsiExtendDevice(
    __in pHW_HBA_EXT            pHBAExt,
    __in PSCSI_REQUEST_BLOCK    pSrb,
    __inout __deref pResultType pResult,
    __inout __deref PKIRQL      LowestAssumedIrql,
    __inout __deref PSRB_IMSCSI_EXTEND_DEVICE       extend_device_data
    )
{
    UCHAR scsi_status;
    pHW_LU_EXTENSION device_extension;

    KdPrint(("ImScsi: Request to grow device %i:%i:%i by %I64i bytes.\n",
        (int)extend_device_data->DeviceNumber.PathId,
        (int)extend_device_data->DeviceNumber.TargetId,
        (int)extend_device_data->DeviceNumber.Lun,
        extend_device_data->ExtendSize.QuadPart));

    scsi_status = ScsiGetLUExtension(
        pHBAExt,
        &device_extension,
        extend_device_data->DeviceNumber.PathId,
        extend_device_data->DeviceNumber.TargetId,
        extend_device_data->DeviceNumber.Lun,
        LowestAssumedIrql
        );

    if ((scsi_status != SRB_STATUS_SUCCESS) || (device_extension == NULL))
    {
        extend_device_data->SrbIoControl.ReturnCode = (ULONG)STATUS_OBJECT_NAME_NOT_FOUND;
        ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
        return;
    }

    if (device_extension->ReadOnly)
    {
        extend_device_data->SrbIoControl.ReturnCode = (ULONG)STATUS_MEDIA_WRITE_PROTECTED;
        ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
        return;
    }

    pMP_WorkRtnParms pWkRtnParms = ImScsiCreateWorkItem(pHBAExt, device_extension, pSrb);

    if (pWkRtnParms == NULL)
    {
        DbgPrint("ImScsi::ImScsiExtendDevice Failed to allocate work parm structure\n");

        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_NO_SENSE, 0);
        return;
    }

    KEVENT wait_event;
    BOOLEAN wait_result = KeGetCurrentIrql() < DISPATCH_LEVEL;

    if (wait_result)
    {
        KeInitializeEvent(&wait_event, EVENT_TYPE::NotificationEvent, FALSE);
        pWkRtnParms->CallerWaitEvent = &wait_event;
    }

    // Queue work item, which will run in the System process.

    ImScsiScheduleWorkItem(pWkRtnParms, LowestAssumedIrql);

    *pResult = ResultQueued;                          // Indicate queuing.

    if (wait_result)
    {
        KeWaitForSingleObject(&wait_event, KWAIT_REASON::Executive, MODE::KernelMode, FALSE, NULL);
    }

    StoragePortNotification(BusChangeDetected, pHBAExt, extend_device_data->DeviceNumber.PathId);

    return;
}

NTSTATUS
ImScsiRemoveDevice(
__in            pHW_HBA_EXT     pHBAExt,
__in            PDEVICE_NUMBER  DeviceNumber,
__inout __deref PKIRQL          LowestAssumedIrql
)
{
    PLIST_ENTRY             list_ptr;
    NTSTATUS                status;
    ULONG                   count = 0;
    KLOCK_QUEUE_HANDLE      LockHandle;
    UCHAR                   pathId = DeviceNumber->PathId;

    KdPrint(("PhDskMnt::ImScsiRemoveDevice: PathId=%i, TargetId=%i, Lun=%i.\n",
        (int)DeviceNumber->PathId, (int)DeviceNumber->TargetId, (int)DeviceNumber->Lun));

    ImScsiAcquireLock(                   // Serialize the linked list of LUN extensions.              
        &pHBAExt->LUListLock, &LockHandle, *LowestAssumedIrql);

    for (list_ptr = pHBAExt->LUList.Flink;
        list_ptr != &pHBAExt->LUList;
        list_ptr = list_ptr->Flink
        )
    {
        pHW_LU_EXTENSION object;
        object = CONTAINING_RECORD(list_ptr, HW_LU_EXTENSION, List);

        if ((DeviceNumber->LongNumber ==
            IMSCSI_ALL_DEVICES) ||
            (object->DeviceNumber.LongNumber ==
            DeviceNumber->LongNumber))
        {
            count++;
            KeSetEvent(&object->StopThread, (KPRIORITY)0, FALSE);
            KeSetEvent(&object->RequestEvent, (KPRIORITY)0, FALSE);
        }
    }

    ImScsiReleaseLock(&LockHandle, LowestAssumedIrql);

    if (count == 0)
    {
        KdPrint(("PhDskMnt::ImScsiRemoveDevice: Non-existing device.\n"));
        status = STATUS_OBJECT_NAME_NOT_FOUND;
        goto Done;
    }

    KdPrint(("PhDskMnt::ImScsiRemoveDevice: Found %i device(s).\n", count));

    status = STATUS_SUCCESS;

    if (pathId == 0xFF)
        pathId = 0x00;

    StoragePortNotification(BusChangeDetected, pHBAExt, pathId);

Done:
    KdPrint2(("PhDskMnt::ImScsiRemoveDevice: End: status=0x%X, *Result=%i\n", status));

    return status;
}

