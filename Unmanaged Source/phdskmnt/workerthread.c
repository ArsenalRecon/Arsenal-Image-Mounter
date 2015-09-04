
/// workerthread.c
/// Worker thread that runs at PASSIVE_LEVEL. Work from miniport callback
/// routines are queued here for completion at an IRQL where waiting and
/// communicating is possible.
/// 
/// Copyright (c) 2012-2015, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

//#define _MP_H_skip_includes

#include "phdskmnt.h"

#define SECTOR_SIZE_CD_ROM     4096
#define SECTOR_SIZE_HDD        512

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
        KdPrint(("PhDskMnt::ImScsiWorkerThread: Global worker thread start.\n",
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
                    KIRQL lowest_assumed_irql = PASSIVE_LEVEL;
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
ImScsiDispatchWork(
__in pMP_WorkRtnParms        pWkRtnParms
)
{
    pHW_HBA_EXT               pHBAExt = pWkRtnParms->pHBAExt;
    pHW_LU_EXTENSION          pLUExt = pWkRtnParms->pLUExt;
    PSCSI_REQUEST_BLOCK       pSrb = pWkRtnParms->pSrb;
    PETHREAD                  pReqThread = pWkRtnParms->pReqThread;
    PCDB                      pCdb = (PCDB)pSrb->Cdb;

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

            KdPrint(("PhDskMnt::ImScsiDispatchWork: Request to create new device.\n"));

            ImScsiCreateLU(pHBAExt, pSrb, pReqThread, &lowest_assumed_irql);

            ObDereferenceObject(pReqThread);
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
        {
            // Read/write?
            PVOID sysaddress;
            PVOID buffer;
            ULONG status;
            LARGE_INTEGER startingSector;
            LARGE_INTEGER startingOffset;
            KLOCK_QUEUE_HANDLE LockHandle;
            KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

            if ((pCdb->AsByte[0] == SCSIOP_READ16) |
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

            status = StoragePortGetSystemAddress(pHBAExt, pSrb, &sysaddress);

            if ((status != STORAGE_STATUS_SUCCESS) | (sysaddress == NULL))
            {
                DbgPrint("PhDskMnt::ImScsiDispatchWork: StorPortGetSystemAddress failed: status=0x%X address=0x%p translated=0x%p\n",
                    status,
                    pSrb->DataBuffer,
                    sysaddress);

                pSrb->SrbStatus = SRB_STATUS_ERROR;
                pSrb->ScsiStatus = SCSISTAT_GOOD;

                break;
            }

            buffer = ExAllocatePoolWithTag(NonPagedPool, pSrb->DataTransferLength, MP_TAG_GENERAL);

            if (buffer == NULL)
            {
                DbgPrint("PhDskMnt::ImScsiDispatchWork: Memory allocation failed.\n");

                pSrb->SrbStatus = SRB_STATUS_ERROR;
                pSrb->ScsiStatus = SCSISTAT_GOOD;

                break;
            }
            else
            {
                NTSTATUS status = STATUS_NOT_IMPLEMENTED;

                /// For write operations, prepare temporary buffer
                if ((pSrb->Cdb[0] == SCSIOP_WRITE) | (pSrb->Cdb[0] == SCSIOP_WRITE16))
                {
                    RtlMoveMemory(buffer, sysaddress, pSrb->DataTransferLength);
                }

                if ((pSrb->Cdb[0] == SCSIOP_READ) | (pSrb->Cdb[0] == SCSIOP_READ16))
                {
                    status = ImScsiReadDevice(pLUExt, buffer, &startingOffset, &pSrb->DataTransferLength);
                }
                else if ((pSrb->Cdb[0] == SCSIOP_WRITE) | (pSrb->Cdb[0] == SCSIOP_WRITE16))
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
                        break;
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
                        break;
                    }
                    default:
                    {
                        ScsiSetError(pSrb, SRB_STATUS_PARITY_ERROR);
                        break;
                    }
                    }

                    break;
                }

                /// Fake random disk signature in case mounted read-only, 0xAA55 at end of mbr and 0x00000000 in disk id field.
                /// Compatibility fix for mounting Windows Backup vhd files in read-only.
                if ((pLUExt->FakeDiskSignature != 0) &&
                    ((pSrb->Cdb[0] == SCSIOP_READ) |
                    (pSrb->Cdb[0] == SCSIOP_READ16)) &&
                    (startingSector.QuadPart == 0) &&
                    (pSrb->DataTransferLength >= 512) &&
                    (pLUExt->ReadOnly))
                {
                    PUCHAR mbr = (PUCHAR)buffer;

                    if ((*(PUSHORT)(mbr + 0x01FE) == 0xAA55) &
                        (*(PUSHORT)(mbr + 0x01BC) == 0x0000) &
                        ((*(mbr + 0x01BE) & 0x7F) == 0x00) &
                        ((*(mbr + 0x01CE) & 0x7F) == 0x00) &
                        ((*(mbr + 0x01DE) & 0x7F) == 0x00) &
                        ((*(mbr + 0x01EE) & 0x7F) == 0x00) &
                        ((*(PULONG)(mbr + 0x01B8) == 0x00000000UL)))
                    {
                        DbgPrint("PhDskMnt::ImScsiDispatchWork: Faking disk signature as %#X.\n", pLUExt->FakeDiskSignature);

                        *(PULONG)(mbr + 0x01B8) = pLUExt->FakeDiskSignature;
                    }
                }

                /// For write operations, temporary buffer holds read data.
                /// Copy that to system buffer.
                if ((pSrb->Cdb[0] == SCSIOP_READ) | (pSrb->Cdb[0] == SCSIOP_READ16))
                {
                    RtlMoveMemory(sysaddress, buffer, pSrb->DataTransferLength);
                }
            }

            ImScsiAcquireLock(&pLUExt->LastIoLock, &LockHandle, lowest_assumed_irql);

            if (pLUExt->LastIoBuffer != NULL)
                ExFreePoolWithTag(pLUExt->LastIoBuffer, MP_TAG_GENERAL);

            pLUExt->LastIoStartSector = startingSector.QuadPart;
            pLUExt->LastIoLength = pSrb->DataTransferLength;
            pLUExt->LastIoBuffer = buffer;

            ImScsiReleaseLock(&LockHandle, &lowest_assumed_irql);

            ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
        }
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
