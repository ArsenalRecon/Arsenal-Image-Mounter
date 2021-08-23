
/// workerthread.c
/// Worker thread that runs at PASSIVE_LEVEL. Work from miniport callback
/// routines are queued here for completion at an IRQL where waiting and
/// communicating is possible.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

//#define _MP_H_skip_includes

#include "phdskmnt.h"

#include "legacycompat.h"

/**************************************************************************************************/
/*                                                                                                */
/* Globals, forward definitions, etc.                                                             */
/*                                                                                                */
/**************************************************************************************************/

/**************************************************************************************************/
/*                                                                                                */
/* This is the worker thread routine, which always runs in System process.                        */
/*                                                                                                */
/**************************************************************************************************/
VOID
ImScsiWorkerThread(__in PVOID Context)
{
    pHW_LU_EXTENSION            pLUExt = (pHW_LU_EXTENSION)Context;
    pMP_WorkRtnParms            pWkRtnParms = NULL;
    PLIST_ENTRY                 request_list = NULL;
    PKSPIN_LOCK                 request_list_lock = NULL;
    PKEVENT                     wait_objects[2] = { NULL };

    KeSetPriorityThread(KeGetCurrentThread(), LOW_REALTIME_PRIORITY);

    if (pLUExt != NULL)
    {
        KdPrint(("PhDskMnt::ImScsiWorkerThread: Device worker thread start. pLUExt = 0x%p\n",
            pLUExt));

        request_list = &pLUExt->RequestList;
        request_list_lock = &pLUExt->RequestListLock;
        wait_objects[0] = &pLUExt->RequestEvent;

        // If this is a VM backed disk that should be pre-loaded with an image file
        // we have to load the contents of that file now before entering the service
        // loop.
        if (pLUExt->VMDisk && (pLUExt->ImageFile != NULL))
            if (!ImScsiFillMemoryDisk(pLUExt))
                KeSetEvent(&pLUExt->StopThread, (KPRIORITY)0, FALSE);
    }
    else
    {
        KdPrint(("PhDskMnt::ImScsiWorkerThread: Global worker thread start. pLUExt=%p\n",
            pLUExt));

        request_list = &pMPDrvInfoGlobal->RequestList;
        request_list_lock = &pMPDrvInfoGlobal->RequestListLock;
        wait_objects[0] = &pMPDrvInfoGlobal->RequestEvent;
    }
    wait_objects[1] = &pMPDrvInfoGlobal->StopWorker;

    for (;;)
    {
        PLIST_ENTRY                 request;
        KLOCK_QUEUE_HANDLE          lock_handle;
        KIRQL                       lowest_assumed_irql = PASSIVE_LEVEL;

#ifdef USE_SCSIPORT

        NTSTATUS                    status = STATUS_SUCCESS;
        PIRP                        irp = NULL;

        ImScsiGetControllerObject();

        if (pMPDrvInfoGlobal->ControllerObject != NULL)
        {
            KdPrint2(("PhDskMnt::ImScsiWorkerThread: Pre-building IRP for next SMB_IMSCSI_CHECK.\n"));

            irp = ImScsiBuildCompletionIrp();
        }
#endif

        for (;;)
        {
            ImScsiAcquireLock(request_list_lock, &lock_handle, lowest_assumed_irql);

            request = RemoveHeadList(request_list);

            ImScsiReleaseLock(&lock_handle, &lowest_assumed_irql);

            if (request != request_list)
            {
                break;
            }

            if (KeReadStateEvent(&pMPDrvInfoGlobal->StopWorker) ||
                ((pLUExt != NULL) && (KeReadStateEvent(&pLUExt->StopThread))))
            {
                KdPrint(("PhDskMnt::ImScsiWorkerThread shutting down.\n"));

                if (pLUExt != NULL)
                {
                    ImScsiCleanupLU(pLUExt, &lowest_assumed_irql);
                }

#ifdef USE_SCSIPORT
                // One last SMB_IMSCSI_CHECK call to flush response queue and free allocated IRP
                if (irp != NULL)
                {
                    IoCallDriver(pMPDrvInfoGlobal->ControllerObject, irp);
                }
#endif

                PsTerminateSystemThread(STATUS_SUCCESS);
                return;
            }

            KdPrint2(("PhDskMnt::ImScsiWorkerThread idle, waiting for request.\n"));

            KeWaitForMultipleObjects(2, (PVOID*)wait_objects, WaitAny, Executive, KernelMode, FALSE, NULL, NULL);
        }

        pWkRtnParms = CONTAINING_RECORD(request, MP_WorkRtnParms, RequestListEntry);

        KdPrint2(("PhDskMnt::ImScsiWorkerThread got request. pWkRtnParms = 0x%p\n",
            pWkRtnParms));

        // Request to wait for LU worker thread to terminate
        if (pWkRtnParms->pSrb == NULL)
        {
            KdPrint(("PhDskMnt::ImScsiWorkerThread: Request to wait for LU worker thread to exit. pLUExt=%p\n",
                pWkRtnParms->pLUExt));

            if (pWkRtnParms->pLUExt->WorkerThread != NULL)
            {
                KeWaitForSingleObject(
                    pWkRtnParms->pLUExt->WorkerThread,
                    Executive,
                    KernelMode,
                    FALSE,
                    NULL);

                ObDereferenceObject(pWkRtnParms->pLUExt->WorkerThread);
                pWkRtnParms->pLUExt->WorkerThread = NULL;

                KdPrint(("PhDskMnt::ImScsiWorkerThread: Worker thread exit. Ready to free LUExt.\n"));
            }
            else
            {
                KdPrint(("PhDskMnt::ImScsiWorkerThread: Worker not started. Ready to free LUExt.\n"));
            }
            
            ExFreePoolWithTag(pWkRtnParms->pLUExt, MP_TAG_GENERAL);

            ExFreePoolWithTag(pWkRtnParms, MP_TAG_GENERAL);

            continue;
        }

        ImScsiDispatchWork(pWkRtnParms);

        if (pWkRtnParms->pReqThread != NULL)
        {
            ObDereferenceObject(pWkRtnParms->pReqThread);
        }

        if (pWkRtnParms->CallerWaitEvent != NULL)
        {
            KeSetEvent(pWkRtnParms->CallerWaitEvent, (KPRIORITY)0, FALSE);
        }

#ifdef USE_SCSIPORT

        KdPrint2(("PhDskMnt::ImScsiWorkerThread: Calling SMB_IMSCSI_CHECK for work: 0x%p.\n", pWkRtnParms));

        status = ImScsiCallForCompletion(irp, pWkRtnParms, &lowest_assumed_irql);

        if (!NT_SUCCESS(status))
            DbgPrint("PhDskMnt::ImScsiWorkerThread: IoCallDriver failed: 0x%X for work 0x%p\n", status, pWkRtnParms);
        else
            KdPrint2(("PhDskMnt::ImScsiWorkerThread: Finished SMB_IMSCSI_CHECK for work: 0x%p.\n", pWkRtnParms));

#endif

#ifdef USE_STORPORT

        KdPrint2(("PhDskMnt::ImScsiWorkerThread: Sending 'RequestComplete' to StorPort for work: 0x%p.\n", pWkRtnParms));

        StorPortNotification(RequestComplete, pWkRtnParms->pHBAExt, pWkRtnParms->pSrb);

        ExFreePoolWithTag(pWkRtnParms, MP_TAG_GENERAL);

        KdPrint2(("PhDskMnt::ImScsiWorkerThread: Finished work: 0x%p.\n", pWkRtnParms));

#endif

    }
}

VOID
ImScsiDispatchReadWrite(
    __in pHW_HBA_EXT pHBAExt,
    __in pHW_LU_EXTENSION pLUExt,
    __in PSCSI_REQUEST_BLOCK pSrb)
{
    PCDB pCdb = (PCDB)pSrb->Cdb;
    PVOID sysaddress;
    PVOID buffer;
    LARGE_INTEGER startingSector;
    LARGE_INTEGER startingOffset;
    KLOCK_QUEUE_HANDLE LockHandle;
    KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

    if (((pSrb->Cdb[0] == SCSIOP_WRITE) || (pSrb->Cdb[0] == SCSIOP_WRITE16)) &&
        (((pLUExt->RegistrationKey == 0) && (pLUExt->ReservationKey != 0)) ||
        (pLUExt->RegistrationKey != 0) && (pLUExt->ReservationKey == 0)))
    {
        KdPrint(("PhDskMnt::ImScsiDispatchWork Write operation on unreserved LUN.\n"));

        pSrb->SrbStatus = SRB_STATUS_ERROR;
        pSrb->ScsiStatus = SCSISTAT_RESERVATION_CONFLICT;

        return;
    }

    if ((pCdb->AsByte[0] == SCSIOP_READ16) ||
        (pCdb->AsByte[0] == SCSIOP_WRITE16))
    {
        REVERSE_BYTES_QUAD(&startingSector, pCdb->CDB16.LogicalBlock);
    }
    else
    {
        startingSector.QuadPart = 0;
        REVERSE_BYTES(&startingSector, &pCdb->CDB10.LogicalBlockByte0);
    }

    startingOffset.QuadPart = startingSector.QuadPart << pLUExt->BlockPower;

    KdPrint2(("PhDskMnt::ImScsiDispatchWork starting sector: 0x%I64X\n", startingSector));

    ULONG s_status = StoragePortGetSystemAddress(pHBAExt, pSrb, &sysaddress);

    if ((s_status != STORAGE_STATUS_SUCCESS) || (sysaddress == NULL))
    {
        DbgPrint("PhDskMnt::ImScsiDispatchWork: StorPortGetSystemAddress failed: status=0x%X address=0x%p translated=0x%p\n",
            s_status,
            pSrb->DataBuffer,
            sysaddress);

        pSrb->SrbStatus = SRB_STATUS_ERROR;
        pSrb->ScsiStatus = SCSISTAT_GOOD;

        return;
    }

    buffer = ExAllocatePoolWithTag(NonPagedPool, pSrb->DataTransferLength, MP_TAG_GENERAL);

    if (buffer == NULL)
    {
        DbgPrint("PhDskMnt::ImScsiDispatchWork: Memory allocation failed.\n");

        pSrb->SrbStatus = SRB_STATUS_ERROR;
        pSrb->ScsiStatus = SCSISTAT_GOOD;

        return;
    }

    NTSTATUS status = STATUS_NOT_IMPLEMENTED;

    /// For write operations, prepare temporary buffer
    if ((pSrb->Cdb[0] == SCSIOP_WRITE) || (pSrb->Cdb[0] == SCSIOP_WRITE16))
    {
        RtlMoveMemory(buffer, sysaddress, pSrb->DataTransferLength);
    }

    if ((pSrb->Cdb[0] == SCSIOP_READ) || (pSrb->Cdb[0] == SCSIOP_READ16))
    {
        status = ImScsiReadDevice(pLUExt, buffer, &startingOffset, &pSrb->DataTransferLength);
    }
    else if ((pSrb->Cdb[0] == SCSIOP_WRITE) || (pSrb->Cdb[0] == SCSIOP_WRITE16))
    {
        status = ImScsiWriteDevice(pLUExt, buffer, &startingOffset, &pSrb->DataTransferLength);
    }

    if (!NT_SUCCESS(status))
    {
        ExFreePoolWithTag(buffer, MP_TAG_GENERAL);

        DbgPrint("PhDskMnt::ImScsiDispatchWork: I/O error status=0x%X\n", status);
        switch (status)
        {
        case STATUS_INVALID_BUFFER_SIZE:
        {
            DbgPrint("PhDskMnt::ImScsiDispatchWork: STATUS_INVALID_BUFFER_SIZE from image I/O. Reporting SCSI_SENSE_ILLEGAL_REQUEST/SCSI_ADSENSE_INVALID_CDB/0x00.\n");
            
            ScsiSetCheckCondition(
                pSrb,
                SRB_STATUS_ERROR,
                SCSI_SENSE_ILLEGAL_REQUEST,
                SCSI_ADSENSE_INVALID_CDB,
                0);

            return;
        }

        case STATUS_DEVICE_BUSY:
        {
            DbgPrint("PhDskMnt::ImScsiDispatchWork: STATUS_DEVICE_BUSY from image I/O. Reporting SRB_STATUS_BUSY/SCSI_SENSE_NOT_READY/SCSI_ADSENSE_LUN_NOT_READY/SCSI_SENSEQ_BECOMING_READY.\n");
            
            ScsiSetCheckCondition(
                pSrb,
                SRB_STATUS_BUSY,
                SCSI_SENSE_NOT_READY,
                SCSI_ADSENSE_LUN_NOT_READY,
                SCSI_SENSEQ_BECOMING_READY
            );
            
            return;
        }

        case STATUS_CONNECTION_RESET:
        case STATUS_DEVICE_REMOVED:
        case STATUS_DEVICE_DOES_NOT_EXIST:
        case STATUS_PIPE_BROKEN:
        case STATUS_PIPE_DISCONNECTED:
        case STATUS_PORT_DISCONNECTED:
        case STATUS_REMOTE_DISCONNECT:
        {
            DbgPrint("PhDskMnt::ImScsiDispatchWork: Underlying image disconnected. Reporting SRB_STATUS_ERROR/SCSI_SENSE_NOT_READY/SCSI_ADSENSE_LUN_NOT_READY/SCSI_SENSEQ_NOT_REACHABLE.\n");
            
            ImScsiRemoveDevice(pHBAExt, &pLUExt->DeviceNumber, &lowest_assumed_irql);
            
            ScsiSetCheckCondition(
                pSrb,
                SRB_STATUS_BUSY,
                SCSI_SENSE_NOT_READY,
                SCSI_ADSENSE_LUN_COMMUNICATION,
                SCSI_SENSEQ_NOT_REACHABLE
            );

            return;
        }

        default:
        {
            ScsiSetError(pSrb, SRB_STATUS_PARITY_ERROR);
            return;
        }
        }

        return;
    }

    /// If "fake random disk signature" option is used.
    /// Compatibility fix for mounting Windows Backup vhd files in read-only etc.
    if ((pLUExt->FakeDiskSignature != 0) &&
        ((pSrb->Cdb[0] == SCSIOP_READ) ||
        (pSrb->Cdb[0] == SCSIOP_READ16)) &&
            (startingSector.QuadPart == 0) &&
        (pSrb->DataTransferLength >= 512))
    {
        PUCHAR mbr = (PUCHAR)buffer;

        if ((*(PUSHORT)(mbr + 0x01FE) == 0xAA55) &&
            (*(mbr + 0x01C2) != 0xEE) &&
            ((*(mbr + 0x01BE) & 0x7F) == 0x00) &&
            ((*(mbr + 0x01CE) & 0x7F) == 0x00) &&
            ((*(mbr + 0x01DE) & 0x7F) == 0x00) &&
            ((*(mbr + 0x01EE) & 0x7F) == 0x00))
        {
            DbgPrint("PhDskMnt::ImScsiDispatchWork: Faking disk signature as %#X.\n", pLUExt->FakeDiskSignature);

            *(PULONG)(mbr + 0x01B8) = pLUExt->FakeDiskSignature;
        }
        else
        {
            DbgPrint("PhDskMnt::ImScsiDispatchWork: Present MBR data not compatible with fake disk signature option.\n");

            pLUExt->FakeDiskSignature = 0;
        }
    }
    /// Allow the fake disk signature to be overwritten.
    else if ((pLUExt->FakeDiskSignature != 0) &&
        ((pSrb->Cdb[0] == SCSIOP_WRITE) ||
        (pSrb->Cdb[0] == SCSIOP_WRITE16)) &&
            (startingSector.QuadPart == 0) &&
        (pSrb->DataTransferLength >= 512))
    {
        pLUExt->FakeDiskSignature = 0;
    }

    /// For write operations, temporary buffer holds read data.
    /// Copy that to system buffer.
    if ((pSrb->Cdb[0] == SCSIOP_READ) || (pSrb->Cdb[0] == SCSIOP_READ16))
    {
        RtlMoveMemory(sysaddress, buffer, pSrb->DataTransferLength);
    }

    if (pLUExt->SharedImage)
    {
        ExFreePoolWithTag(buffer, MP_TAG_GENERAL);
    }
    else
    {
        ImScsiAcquireLock(&pLUExt->LastIoLock, &LockHandle, lowest_assumed_irql);

        if (pLUExt->LastIoBuffer != NULL)
            ExFreePoolWithTag(pLUExt->LastIoBuffer, MP_TAG_GENERAL);

        pLUExt->LastIoStartSector = startingSector.QuadPart;
        pLUExt->LastIoLength = pSrb->DataTransferLength;
        pLUExt->LastIoBuffer = buffer;

        ImScsiReleaseLock(&LockHandle, &lowest_assumed_irql);
    }

    ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
}

VOID
ImScsiDispatchWork(
__in pMP_WorkRtnParms        pWkRtnParms
)
{
    pHW_HBA_EXT               pHBAExt = pWkRtnParms->pHBAExt;
    pHW_LU_EXTENSION          pLUExt = pWkRtnParms->pLUExt;
    PSCSI_REQUEST_BLOCK       pSrb = pWkRtnParms->pSrb;
    PETHREAD                  pReqThread = pWkRtnParms->pReqThread;

    KdPrint2(("PhDskMnt::ImScsiDispatchWork Action: 0x%X, pSrb: 0x%p, pSrb->DataBuffer: 0x%p pSrb->DataTransferLength: 0x%X\n",
        (int)pSrb->Cdb[0], pSrb, pSrb->DataBuffer, pSrb->DataTransferLength));

    switch (pSrb->Function)
    {
    case SRB_FUNCTION_IO_CONTROL:
    {
        PSRB_IO_CONTROL srb_io_control = (PSRB_IO_CONTROL)pSrb->DataBuffer;

        switch (srb_io_control->ControlCode)
        {
        case SMP_IMSCSI_CREATE_DEVICE:
        {                       // Create new?
            KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

            KdPrint(("PhDskMnt::ImScsiDispatchWork: Request SMP_IMSCSI_CREATE_DEVICE.\n"));

            ImScsiCreateLU(pHBAExt, pSrb, pReqThread, &lowest_assumed_irql);
        }
        break;

        case SMP_IMSCSI_EXTEND_DEVICE:
        {
            PSRB_IMSCSI_EXTEND_DEVICE srb_buffer = (PSRB_IMSCSI_EXTEND_DEVICE)pSrb->DataBuffer;

            KdPrint(("PhDskMnt::ImScsiDispatchWork: Request SMP_IMSCSI_EXTEND_DEVICE.\n"));

            srb_io_control->ReturnCode = ImScsiExtendLU(pHBAExt, pLUExt, srb_buffer);

            ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
        }
        break;

        default:
            break;
        }
    }
    break;

    case SRB_FUNCTION_EXECUTE_SCSI:
        switch (pSrb->Cdb[0])
        {
        case SCSIOP_READ:
        case SCSIOP_WRITE:
        case SCSIOP_READ16:
        case SCSIOP_WRITE16:
            // Read/write?
            ImScsiDispatchReadWrite(pHBAExt, pLUExt, pSrb);
            break;

        case SCSIOP_UNMAP:
            // UNMAP/TRIM
            ImScsiDispatchUnmapDevice(pHBAExt, pLUExt, pSrb);
            break;

        default:
        {
            DbgPrint("PhDskMnt::ImScsiDispatchWork unknown function: 0x%X\n", (int)pSrb->Cdb[0]);

            ScsiSetError(pSrb, SRB_STATUS_INTERNAL_ERROR);
        }
        }

    default:
        break;
    }

    KdPrint2(("PhDskMnt::ImScsiDispatchWork: End pSrb: 0x%p.\n", pSrb));

}                                                     // End ImScsiDispatchWork().

VOID
ImScsiDispatchUnmapDevice(
    __in pHW_HBA_EXT pHBAExt,
    __in pHW_LU_EXTENSION pLUExt,
    __in PSCSI_REQUEST_BLOCK pSrb)
{
    PUNMAP_LIST_HEADER list = (PUNMAP_LIST_HEADER)pSrb->DataBuffer;
    USHORT descrlength = RtlUshortByteSwap(*(PUSHORT)list->BlockDescrDataLength);

    UNREFERENCED_PARAMETER(pHBAExt);
    UNREFERENCED_PARAMETER(pLUExt);

    if ((ULONG)descrlength + FIELD_OFFSET(UNMAP_LIST_HEADER, Descriptors) >
        pSrb->DataTransferLength)
    {
        KdBreakPoint();
        ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
        return;
    }

    USHORT items = descrlength / sizeof(*list->Descriptors);

    NTSTATUS status = STATUS_SUCCESS;

    IO_STATUS_BLOCK io_status;

    if (pLUExt->UseProxy)
    {
        WPoolMem<DEVICE_DATA_SET_RANGE, PagedPool> range(sizeof(DEVICE_DATA_SET_RANGE) * items);

        if (!range)
        {
            ScsiSetError(pSrb, SRB_STATUS_ERROR);
            return;
        }

        for (USHORT i = 0; i < items; i++)
        {
            LONGLONG startingSector = RtlUlonglongByteSwap(*(PULONGLONG)list->Descriptors[i].StartingLba);
            ULONG numBlocks = RtlUlongByteSwap(*(PULONG)list->Descriptors[i].LbaCount);

            range[i].StartingOffset = (startingSector << pLUExt->BlockPower) + pLUExt->ImageOffset.QuadPart;
            range[i].LengthInBytes = (ULONGLONG)numBlocks << pLUExt->BlockPower;

            if (pLUExt->FakeDiskSignature != 0 &&
                range[i].StartingOffset == 0 && range[i].LengthInBytes >= 512)
            {
                pLUExt->FakeDiskSignature = 0;
            }

            KdPrint(("PhDskMnt::ImScsiDispatchUnmapDevice: Offset: %I64i, bytes: %I64u\n",
                range[i].StartingOffset, range[i].LengthInBytes));
        }

        status = ImScsiUnmapOrZeroProxy(
            &pLUExt->Proxy,
            IMDPROXY_REQ_UNMAP,
            &io_status,
            &pLUExt->StopThread,
            items,
            range);
    }
    else if (pLUExt->ImageFile != NULL)
    {
        FILE_ZERO_DATA_INFORMATION zerodata;

#if _NT_TARGET_VERSION >= 0x602
        ULONG fltrim_size = FIELD_OFFSET(FILE_LEVEL_TRIM, Ranges) +
            (items * sizeof(FILE_LEVEL_TRIM_RANGE));

        WPoolMem<FILE_LEVEL_TRIM, PagedPool> fltrim;

        if (!pLUExt->NoFileLevelTrim)
        {
            fltrim.Alloc(fltrim_size);

            if (!fltrim)
            {
                ScsiSetError(pSrb, SRB_STATUS_ERROR);
                return;
            }

            fltrim->NumRanges = items;
        }
#endif

        for (int i = 0; i < items; i++)
        {
            LONGLONG startingSector = RtlUlonglongByteSwap(*(PULONGLONG)list->Descriptors[i].StartingLba);
            ULONG numBlocks = RtlUlongByteSwap(*(PULONG)list->Descriptors[i].LbaCount);

            zerodata.FileOffset.QuadPart = (startingSector << pLUExt->BlockPower) + pLUExt->ImageOffset.QuadPart;
            zerodata.BeyondFinalZero.QuadPart = ((LONGLONG)numBlocks << pLUExt->BlockPower) +
                zerodata.FileOffset.QuadPart;

            if (pLUExt->FakeDiskSignature != 0 &&
                zerodata.FileOffset.QuadPart == 0 && zerodata.BeyondFinalZero.QuadPart >= 512)
            {
                pLUExt->FakeDiskSignature = 0;
            }

            KdPrint(("PhDskMnt::ImScsiDispatchUnmap: Zero data request from 0x%I64X to 0x%I64X\n",
                zerodata.FileOffset.QuadPart, zerodata.BeyondFinalZero.QuadPart));

#if _NT_TARGET_VERSION >= 0x602
            if (!pLUExt->NoFileLevelTrim)
            {
                fltrim->Ranges[i].Offset = zerodata.FileOffset.QuadPart;
                fltrim->Ranges[i].Length = (LONGLONG)numBlocks << pLUExt->BlockPower;

                KdPrint(("PhDskMnt::ImScsiDispatchUnmap: File level trim request 0x%I64X bytes at 0x%I64X\n",
                    fltrim->Ranges[i].Length, fltrim->Ranges[i].Offset));
            }
#endif

            status = ZwFsControlFile(
                pLUExt->ImageFile,
                NULL,
                NULL,
                NULL,
                &io_status,
                FSCTL_SET_ZERO_DATA,
                &zerodata,
                sizeof(zerodata),
                NULL,
                0);

            KdPrint(("PhDskMnt::ImScsiDispatchUnmap: FSCTL_SET_ZERO_DATA result: 0x%#X\n", status));

            if (!NT_SUCCESS(status))
            {
                goto done;
            }
        }

#if _NT_TARGET_VERSION >= 0x602
        if (!pLUExt->NoFileLevelTrim)
        {
            status = ZwFsControlFile(
                pLUExt->ImageFile,
                NULL,
                NULL,
                NULL,
                &io_status,
                FSCTL_FILE_LEVEL_TRIM,
                fltrim,
                fltrim_size,
                NULL,
                0);

            KdPrint(("PhDskMnt::ImScsiDispatchUnmap: FSCTL_FILE_LEVEL_TRIM result: %#x\n", status));

            if (!NT_SUCCESS(status))
            {
                pLUExt->NoFileLevelTrim = TRUE;
            }
        }
#endif
    }
    else
    {
        status = STATUS_NOT_SUPPORTED;
    }

done:

    KdPrint(("PhDskMnt::ImScsiDispatchUnmap: Result: %#x\n", status));

    ScsiSetSuccess(pSrb, 0);
}

VOID
ImScsiCreateLU(
    __in pHW_HBA_EXT             pHBAExt,
    __in PSCSI_REQUEST_BLOCK     pSrb,
    __in PETHREAD                pReqThread,
    __inout __deref PKIRQL    LowestAssumedIrql
)
{
    PSRB_IMSCSI_CREATE_DATA new_device = (PSRB_IMSCSI_CREATE_DATA)pSrb->DataBuffer;
    PLIST_ENTRY             list_ptr;
    pHW_LU_EXTENSION        pLUExt = NULL;
    NTSTATUS                ntstatus;

    KLOCK_QUEUE_HANDLE      LockHandle;

    KdPrint(("PhDskMnt::ImScsiCreateLU: Initializing new device: %d:%d:%d\n",
        new_device->Fields.DeviceNumber.PathId,
        new_device->Fields.DeviceNumber.TargetId,
        new_device->Fields.DeviceNumber.Lun));

    ImScsiAcquireLock(                   // Serialize the linked list of LUN extensions.              
        &pHBAExt->LUListLock, &LockHandle, *LowestAssumedIrql);

    for (list_ptr = pHBAExt->LUList.Flink;
        list_ptr != &pHBAExt->LUList;
        list_ptr = list_ptr->Flink
        )
    {
        pHW_LU_EXTENSION object;
        object = CONTAINING_RECORD(list_ptr, HW_LU_EXTENSION, List);

        if (object->DeviceNumber.LongNumber ==
            new_device->Fields.DeviceNumber.LongNumber)
        {
            pLUExt = object;
            break;
        }
    }

    if (pLUExt != NULL)
    {
        ImScsiReleaseLock(&LockHandle, LowestAssumedIrql);

        ntstatus = STATUS_OBJECT_NAME_COLLISION;

        goto Done;
    }

    pLUExt = (pHW_LU_EXTENSION)ExAllocatePoolWithTag(NonPagedPool, sizeof(HW_LU_EXTENSION), MP_TAG_GENERAL);

    if (pLUExt == NULL)
    {
        ImScsiReleaseLock(&LockHandle, LowestAssumedIrql);

        ntstatus = STATUS_INSUFFICIENT_RESOURCES;

        goto Done;
    }

    RtlZeroMemory(pLUExt, sizeof(HW_LU_EXTENSION));

    pLUExt->DeviceNumber = new_device->Fields.DeviceNumber;

    KeInitializeEvent(&pLUExt->StopThread, NotificationEvent, FALSE);

    InsertHeadList(&pHBAExt->LUList, &pLUExt->List);

    ImScsiReleaseLock(&LockHandle, LowestAssumedIrql);

    pLUExt->pHBAExt = pHBAExt;

    ntstatus = ImScsiInitializeLU(pLUExt, new_device, pReqThread);
    if (!NT_SUCCESS(ntstatus))
    {
        ImScsiCleanupLU(pLUExt, LowestAssumedIrql);
        goto Done;
    }

Done:
    new_device->SrbIoControl.ReturnCode = ntstatus;

    ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

    return;
}

NTSTATUS
ImScsiExtendLU(
    pHW_HBA_EXT pHBAExt,
    pHW_LU_EXTENSION device_extension,
    PSRB_IMSCSI_EXTEND_DEVICE extend_device_data)
{
    NTSTATUS status;
    FILE_END_OF_FILE_INFORMATION new_size;
    FILE_STANDARD_INFORMATION file_standard_information;

    UNREFERENCED_PARAMETER(pHBAExt);

    new_size.EndOfFile.QuadPart =
        device_extension->DiskSize.QuadPart +
        extend_device_data->ExtendSize.QuadPart;

    KdPrint(("ImScsi: New size of device %i:%i:%i will be %I64i bytes.\n",
        (int)extend_device_data->DeviceNumber.PathId,
        (int)extend_device_data->DeviceNumber.TargetId,
        (int)extend_device_data->DeviceNumber.Lun,
        new_size.EndOfFile.QuadPart));

    if (new_size.EndOfFile.QuadPart <= 0)
    {
        status = STATUS_END_OF_MEDIA;
        goto done;
    }

    if (device_extension->VMDisk)
    {
        PVOID new_image_buffer = NULL;
        SIZE_T free_size = 0;
#ifdef _WIN64
        ULONG_PTR old_size =
            device_extension->DiskSize.QuadPart;
        SIZE_T max_size = new_size.EndOfFile.QuadPart;
#else
        ULONG_PTR old_size =
            device_extension->DiskSize.LowPart;
        SIZE_T max_size = new_size.EndOfFile.LowPart;

        // A vm type disk cannot be extended to a larger size than
        // 2 GB.
        if (new_size.EndOfFile.QuadPart & 0xFFFFFFFF80000000)
        {
            status = STATUS_INVALID_DEVICE_REQUEST;
            goto done;
        }
#endif // _WIN64

        KdPrint(("ImScsi: Allocating %I64u bytes.\n",
            (ULONGLONG)max_size));

        status = ZwAllocateVirtualMemory(NtCurrentProcess(),
            &new_image_buffer,
            0,
            &max_size,
            MEM_COMMIT,
            PAGE_READWRITE);

        if (!NT_SUCCESS(status))
        {
            status = STATUS_NO_MEMORY;
            goto done;
        }

        RtlCopyMemory(new_image_buffer,
            device_extension->ImageBuffer,
            min(old_size, max_size));

        ZwFreeVirtualMemory(NtCurrentProcess(),
            (PVOID*)&device_extension->ImageBuffer,
            &free_size,
            MEM_RELEASE);

        device_extension->ImageBuffer = (PUCHAR)new_image_buffer;
        device_extension->DiskSize = new_size.EndOfFile;

        status = STATUS_SUCCESS;
        goto done;
    }

    // For proxy-type disks the new size is just accepted and
    // that's it.
    if (device_extension->UseProxy)
    {
        device_extension->DiskSize =
            new_size.EndOfFile;

        status = STATUS_SUCCESS;
        goto done;
    }

    // Image file backed disks left to do.

    // For disks with offset, refuse to extend size. Otherwise we
    // could break compatibility with the header data we have
    // skipped and we don't know about.
    if (device_extension->ImageOffset.QuadPart != 0)
    {
        status = STATUS_INVALID_DEVICE_REQUEST;
        goto done;
    }

    IO_STATUS_BLOCK io_status;

    status =
        ZwQueryInformationFile(device_extension->ImageFile,
            &io_status,
            &file_standard_information,
            sizeof file_standard_information,
            FileStandardInformation);

    if (!NT_SUCCESS(status))
    {
        goto done;
    }

    KdPrint(("ImScsi: Current image size is %I64u bytes.\n",
        file_standard_information.EndOfFile.QuadPart));

    if (file_standard_information.EndOfFile.QuadPart >=
        new_size.EndOfFile.QuadPart)
    {
        device_extension->DiskSize = new_size.EndOfFile;

        status = STATUS_SUCCESS;
        goto done;
    }

    // For other, fixed file-backed disks we need to adjust the
    // physical file size.

    KdPrint(("ImScsi: Setting new image size to %I64u bytes.\n",
        new_size.EndOfFile.QuadPart));

    status = ZwSetInformationFile(device_extension->ImageFile,
        &io_status,
        &new_size,
        sizeof new_size,
        FileEndOfFileInformation);

    if (NT_SUCCESS(status))
    {
        device_extension->DiskSize = new_size.EndOfFile;
        goto done;
    }

done:
    KdPrint(("ImScsi: SMP_IMSCSI_EXTEND_DEVICE result: %#x\n", status));

    return status;
}

