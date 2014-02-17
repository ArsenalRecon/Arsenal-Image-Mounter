
/// iodisp.c
/// Worker thread and related dispatch routines, running in system thread at
/// PASSIVE_LEVEL. Work from miniport callback routines are queued here for
/// completion at an IRQL where waiting and communicating is possible.
/// 
/// Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#define WkRtnVer     "1.013"

//#define _MP_H_skip_includes

#include "phdskmnt.h"

//#pragma warning(push)
//#pragma warning(disable : 4204)                       /* Prevent C4204 messages from stortrce.h. */
//#include <stortrce.h>
//#pragma warning(pop)
//
//#include "trace.h"
//#include "iodisp.tmh"

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
    pHW_HBA_EXT                 pHBAExt = NULL;
    pMP_WorkRtnParms            pWkRtnParms = NULL;
    PLIST_ENTRY                 request_list = NULL;
    PKSPIN_LOCK                 request_list_lock = NULL;
    PKEVENT                     wait_objects[2] = { NULL };

    KeSetPriorityThread(KeGetCurrentThread(), LOW_REALTIME_PRIORITY);

    if (pLUExt != NULL)
    {
        pHBAExt = pLUExt->pHBAExt;
        request_list = &pLUExt->RequestList;
        request_list_lock = &pLUExt->RequestListLock;
        wait_objects[0] = &pLUExt->RequestEvent;


        // If this is a VM backed disk that should be pre-loaded with an image file
        // we have to load the contents of that file now before entering the service
        // loop.
        if (pLUExt->VMDisk && (pLUExt->ImageFile != NULL))
            if (!ImScsiFillMemoryDisk(pLUExt))
                KeSetEvent(&pLUExt->StopThread, (KPRIORITY) 0, FALSE);
    }
    else
    {
        request_list = &pMPDrvInfoGlobal->RequestList;
        request_list_lock = &pMPDrvInfoGlobal->RequestListLock;
        wait_objects[0] = &pMPDrvInfoGlobal->RequestEvent;
    }
    wait_objects[1] = &pMPDrvInfoGlobal->StopWorker;

    KdPrint(("PhDskMnt::ImScsiWorkerThread start. pHBAExt = 0x%p\n", pHBAExt));

    for (;;)
    {
        PLIST_ENTRY                 request;

#ifdef USE_SCSIPORT

        NTSTATUS                    status = STATUS_SUCCESS;
        PIRP                        irp = NULL;
        SRB_IMSCSI_CHECK            completion_srb = { 0 };
        IO_STATUS_BLOCK             io_status = { 0 };
        KEVENT                      finished_event;

        ImScsiGetAdapterDeviceObject();

        if (pMPDrvInfoGlobal->DeviceObject != NULL)
        {
            KdPrint2(("PhDskMnt::ImScsiWorkerThread: Pre-building IRP for next SMB_IMSCSI_CHECK.\n"));

            completion_srb.sic.HeaderLength = sizeof(SRB_IO_CONTROL);
            RtlCopyMemory(completion_srb.sic.Signature, FUNCTION_SIGNATURE, strlen(FUNCTION_SIGNATURE));
            completion_srb.sic.ControlCode = SMP_IMSCSI_CHECK;

            KeInitializeEvent(&finished_event, NotificationEvent, FALSE);

            irp = IoBuildDeviceIoControlRequest(IOCTL_SCSI_MINIPORT,
                pMPDrvInfoGlobal->DeviceObject,
                &completion_srb,
                sizeof(completion_srb),
                NULL,
                0,
                FALSE,
                &finished_event,
                &io_status);
        }
#endif

        for (;;)
        {
            request =
                ExInterlockedRemoveHeadList(request_list,
                request_list_lock);

            if (request != NULL)
                break;

            if (KeReadStateEvent(&pMPDrvInfoGlobal->StopWorker) |
                ((pLUExt != NULL) ? (KeReadStateEvent(&pLUExt->StopThread)) : FALSE))
            {
                KdPrint2(("PhDskMnt::ImScsiWorkerThread shutting down.\n"));

                if (pLUExt != NULL)
                    ImScsiCleanupLU(pLUExt);

                PsTerminateSystemThread(STATUS_SUCCESS);
                return;
            }

            KdPrint2(("PhDskMnt::ImScsiWorkerThread waiting for request.\n"));
            KeWaitForMultipleObjects(2, (PVOID*)wait_objects, WaitAny, Executive, KernelMode, FALSE, NULL, NULL);
        }

        pWkRtnParms = CONTAINING_RECORD(request, MP_WorkRtnParms, RequestListEntry);

        KdPrint2(("PhDskMnt::ImScsiWorkerThread got request. pWkRtnParms = 0x%p\n", pWkRtnParms));

        ImScsiDispatchWork(pWkRtnParms);

#ifdef USE_SCSIPORT
        ExInterlockedInsertTailList(
            &pMPDrvInfoGlobal->ResponseList,
            &pWkRtnParms->ResponseListEntry,
            &pMPDrvInfoGlobal->ResponseListLock);

        if (irp == NULL)
        {
            DbgPrint("ImScsi Warning: IoBuildDeviceIoControlRequest failed or no DeviceObject found.\n");
            continue;
        }

        KdPrint2(("PhDskMnt::ImScsiWorkerThread: Calling SMB_IMSCSI_CHECK for work: 0x%p.\n", pWkRtnParms));

        status = IoCallDriver(pMPDrvInfoGlobal->DeviceObject, irp);

        if (status == STATUS_PENDING)
        {
            KdPrint(("PhDskMnt::ImScsiWorkerThread: Waiting for SMB_IMSCSI_CHECK to finish for work 0x%p.\n", pWkRtnParms));
            KeWaitForSingleObject(&finished_event, Executive, KernelMode, FALSE, NULL);
            status = io_status.Status;
        }

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
                    KdPrint(("PhDskMnt::ImScsiDispatchWork: Request to create new device.\n"));

                    ImScsiCreateLU(pHBAExt, pSrb, pReqThread);
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
		KIRQL SaveIrql;

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

		__try
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
			DbgPrint("PhDskMnt::ImScsiDispatchWork: I/O error status=0x%X\n", status);
			if (status == STATUS_INVALID_BUFFER_SIZE)
			{
			    DbgPrint("PhDskMnt::ImScsiDispatchWork: STATUS_INVALID_BUFFER_SIZE from image I/O. Reporting SCSI_SENSE_ILLEGAL_REQUEST/SCSI_ADSENSE_INVALID_CDB/0x00.\n");
			    ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_INVALID_CDB, 0);
			    ExFreePoolWithTag(buffer, MP_TAG_GENERAL);
			    break;
			}
			else
			{
			    ScsiSetError(pSrb, SRB_STATUS_PARITY_ERROR);
			    ExFreePoolWithTag(buffer, MP_TAG_GENERAL);
			    break;
			}
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
		__except (EXCEPTION_EXECUTE_HANDLER)
		{
		    ScsiSetError(pSrb, SRB_STATUS_PARITY_ERROR);

		    DbgPrint("PhDskMnt::ImScsiDispatchWork: Exception caught!\n");

		    ExFreePoolWithTag(buffer, MP_TAG_GENERAL);

		    break;
		}

                KeAcquireSpinLock(&pLUExt->LastIoLock, &SaveIrql);
    
                if (pLUExt->LastIoBuffer != NULL)
                    ExFreePoolWithTag(pLUExt->LastIoBuffer, MP_TAG_GENERAL);

                pLUExt->LastIoStartSector = startingSector.QuadPart;
                pLUExt->LastIoLength = pSrb->DataTransferLength;
                pLUExt->LastIoBuffer = buffer;

                KeReleaseSpinLock(&pLUExt->LastIoLock, SaveIrql);

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

NTSTATUS
ImScsiCleanupLU(
                 __in pHW_LU_EXTENSION     pLUExt
                 )
{
    LARGE_INTEGER           wait_timeout;
    pHW_HBA_EXT             pHBAExt = pLUExt->pHBAExt;
    pHW_LU_EXTENSION *      ppLUExt = NULL;
    PLIST_ENTRY             list_ptr;
#if defined(_AMD64_)
    KLOCK_QUEUE_HANDLE      LockHandle;
#else
    KIRQL                   SaveIrql;
#endif

    KdPrint(("PhDskMnt::ImScsiCleanupLU: Removing device: %d:%d:%d\n",
        pLUExt->DeviceNumber.PathId,
        pLUExt->DeviceNumber.TargetId,
        pLUExt->DeviceNumber.Lun));

#if defined(_AMD64_)
    KeAcquireInStackQueuedSpinLock(                   // Serialize the linked list of LUN extensions.              
                                   &pHBAExt->LUListLock, &LockHandle);
#else
    KeAcquireSpinLock(&pHBAExt->LUListLock, &SaveIrql);
#endif

    ppLUExt = (pHW_LU_EXTENSION*) StoragePortGetLogicalUnit(
        pHBAExt,
        pLUExt->DeviceNumber.PathId,
        pLUExt->DeviceNumber.TargetId,
        pLUExt->DeviceNumber.Lun
        );

    if (ppLUExt != NULL)
        *ppLUExt = NULL;

    for (list_ptr = pHBAExt->LUList.Flink;
        list_ptr != &pHBAExt->LUList;
        list_ptr = list_ptr->Flink
        )
    {
        pHW_LU_EXTENSION object;
        object = CONTAINING_RECORD(list_ptr, HW_LU_EXTENSION, List);

        if (object == pLUExt)
        {
            list_ptr->Blink->Flink = list_ptr->Flink;
            list_ptr->Flink->Blink = list_ptr->Blink;
            break;
        }
    }

#if defined(_AMD64_)
    KeReleaseInStackQueuedSpinLock(&LockHandle);      
#else
    KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

    /// ToDo: Cleanup all file handles, object name buffers,
    /// proxy refs etc.
    if (pLUExt->UseProxy)
        ImScsiCloseProxy(&pLUExt->Proxy);

    if (pLUExt->LastIoBuffer != NULL)
    {
	ExFreePoolWithTag(pLUExt->LastIoBuffer, MP_TAG_GENERAL);
	pLUExt->LastIoBuffer = NULL;
    }

    if (pLUExt->VMDisk)
    {
	SIZE_T free_size = 0;
	if (pLUExt->ImageBuffer != NULL)
            ZwFreeVirtualMemory(NtCurrentProcess(),
                (PVOID*)&pLUExt->ImageBuffer,
                &free_size, MEM_RELEASE);

	pLUExt->ImageBuffer = NULL;
    }
    else
    {
	if (pLUExt->ImageFile != NULL)
            ZwClose(pLUExt->ImageFile);

	pLUExt->ImageFile = NULL;
    }

    if (pLUExt->ObjectName.Buffer != NULL)
    {
	ExFreePoolWithTag(pLUExt->ObjectName.Buffer, MP_TAG_GENERAL);
	pLUExt->ObjectName.Buffer = NULL;
	pLUExt->ObjectName.Length = 0;
	pLUExt->ObjectName.MaximumLength = 0;
    }

    KdPrint(("PhDskMnt::ImScsiCleanupLU: Waiting for device to be reported missing.\n"));

    // Wait one second, or until this LU is reported missing.
    // Just to make sure there are no outstanding requests that need the
    // LU extension data block that we are about to free.
    wait_timeout.QuadPart = -10000000;
    KeWaitForSingleObject(&pLUExt->Missing, Executive, KernelMode, FALSE, &wait_timeout);

    KdPrint(("PhDskMnt::ImScsiCleanupLU: Freeing LUExt.\n"));

    ExFreePoolWithTag(pLUExt, MP_TAG_GENERAL);

    return STATUS_SUCCESS;
}

VOID
ImScsiCreateLU(
               __in pHW_HBA_EXT             pHBAExt,
               __in PSCSI_REQUEST_BLOCK     pSrb,
               __in PETHREAD                pReqThread
               )
{
    PSRB_IMSCSI_CREATE_DATA new_device = (PSRB_IMSCSI_CREATE_DATA)pSrb->DataBuffer;
    PLIST_ENTRY             list_ptr;
    pHW_LU_EXTENSION        pLUExt = NULL;
    NTSTATUS                ntstatus;

#if defined(_AMD64_)
    KLOCK_QUEUE_HANDLE      LockHandle;
#else
    KIRQL                   SaveIrql;
#endif

    KdPrint(("PhDskMnt::ImScsiCreateLU: Initializing new device: %d:%d:%d\n",
        new_device->DeviceNumber.PathId,
        new_device->DeviceNumber.TargetId,
        new_device->DeviceNumber.Lun));

#if defined(_AMD64_)
    KeAcquireInStackQueuedSpinLock(                   // Serialize the linked list of LUN extensions.              
                                   &pHBAExt->LUListLock, &LockHandle);
#else
    KeAcquireSpinLock(&pHBAExt->LUListLock, &SaveIrql);
#endif

    for (list_ptr = pHBAExt->LUList.Flink;
        list_ptr != &pHBAExt->LUList;
        list_ptr = list_ptr->Flink
        )
    {
        pHW_LU_EXTENSION object;
        object = CONTAINING_RECORD(list_ptr, HW_LU_EXTENSION, List);

        if (object->DeviceNumber.LongNumber ==
            new_device->DeviceNumber.LongNumber)
        {
            pLUExt = object;
            break;
        }
    }

    if (pLUExt != NULL)
    {
#if defined(_AMD64_)
        KeReleaseInStackQueuedSpinLock(&LockHandle);      
#else
        KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

        ntstatus = STATUS_OBJECT_NAME_EXISTS;

        goto Done;
    }

    pLUExt = (pHW_LU_EXTENSION) ExAllocatePoolWithTag(NonPagedPool, sizeof(HW_LU_EXTENSION), MP_TAG_GENERAL);

    if (pLUExt == NULL)
    {
#if defined(_AMD64_)
        KeReleaseInStackQueuedSpinLock(&LockHandle);      
#else
        KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

        ntstatus = STATUS_INSUFFICIENT_RESOURCES;

        goto Done;
    }

    RtlZeroMemory(pLUExt, sizeof(HW_LU_EXTENSION));

    pLUExt->DeviceNumber = new_device->DeviceNumber;

    KeInitializeEvent(&pLUExt->StopThread, NotificationEvent, FALSE);

    KeInitializeEvent(&pLUExt->Missing, NotificationEvent, FALSE);

    InsertHeadList(&pHBAExt->LUList, &pLUExt->List);

#if defined(_AMD64_)
    KeReleaseInStackQueuedSpinLock(&LockHandle);      
#else
    KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

    pLUExt->pHBAExt = pHBAExt;

    ntstatus = ImScsiInitializeLU(pLUExt, new_device, pReqThread);
    if (!NT_SUCCESS(ntstatus))
    {
        ImScsiCleanupLU(pLUExt);
        goto Done;
    }

Done:
    new_device->SrbIoControl.ReturnCode = ntstatus;

    ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

    return;
}

NTSTATUS
ImScsiReadDevice(
                 __in pHW_LU_EXTENSION pLUExt,
                 __in PVOID            Buffer,
                 __in PLARGE_INTEGER   Offset,
                 __in PULONG           Length
                 )
{
    IO_STATUS_BLOCK io_status = { 0 };
    NTSTATUS status = STATUS_NOT_IMPLEMENTED;
    LARGE_INTEGER byteoffset;

    byteoffset.QuadPart = Offset->QuadPart + pLUExt->ImageOffset.QuadPart;

    KdPrint2(("PhDskMnt::ImScsiReadDevice: pLUExt=%p, Buffer=%p, Offset=0x%I64X, EffectiveOffset=0x%I64X, Length=0x%X\n", pLUExt, Buffer, *Offset, byteoffset, *Length));

    if (pLUExt->VMDisk)
    {
#ifdef _WIN64
        ULONG_PTR vm_offset = Offset->QuadPart;
#else
        ULONG_PTR vm_offset = Offset->LowPart;
#endif

        RtlCopyMemory(Buffer,
            pLUExt->ImageBuffer + vm_offset,
            *Length);

        status = STATUS_SUCCESS;
        io_status.Status = status;
        io_status.Information = *Length;
    }
    else if (pLUExt->UseProxy)
        status = ImScsiReadProxy(
            &pLUExt->Proxy,
            &io_status,
            &pLUExt->StopThread,
            Buffer,
            *Length,
            &byteoffset);
    else if (pLUExt->ImageFile != NULL)
	status = ZwReadFile(
            pLUExt->ImageFile,
            NULL,
            NULL,
            NULL,
            &io_status,
            Buffer,
            *Length,
            &byteoffset,
            NULL);

    if ((status == STATUS_END_OF_FILE) &
        (io_status.Information == 0))
    {
        KdPrint2(("PhDskMnt::ImScsiReadDevice pLUExt=%p, status=STATUS_END_OF_FILE, Length=0x%X. Returning zeroed buffer with requested length.\n", pLUExt, *Length));
        RtlZeroMemory(Buffer, *Length);
        status = STATUS_SUCCESS;
    }
    else
    {
        *Length = (ULONG)io_status.Information;
    }

    KdPrint2(("PhDskMnt::ImScsiReadDevice Result: pLUExt=%p, status=0x%X, Length=0x%X\n", pLUExt, status, *Length));

    return status;
}

NTSTATUS
ImScsiWriteDevice(
                 __in pHW_LU_EXTENSION pLUExt,
                 __in PVOID            Buffer,
                 __in PLARGE_INTEGER   Offset,
                 __in PULONG           Length
                 )
{
    IO_STATUS_BLOCK io_status = { 0 };
    NTSTATUS status = STATUS_NOT_IMPLEMENTED;
    LARGE_INTEGER byteoffset;

    byteoffset.QuadPart = Offset->QuadPart + pLUExt->ImageOffset.QuadPart;

    KdPrint2(("PhDskMnt::ImScsiWriteDevice: pLUExt=%p, Buffer=%p, Offset=0x%I64X, EffectiveOffset=0x%I64X, Length=0x%X\n", pLUExt, Buffer, *Offset, byteoffset, *Length));

    pLUExt->Modified = TRUE;

    if (pLUExt->VMDisk)
    {
#ifdef _WIN64
        ULONG_PTR vm_offset = Offset->QuadPart;
#else
        ULONG_PTR vm_offset = Offset->LowPart;
#endif

        RtlCopyMemory(pLUExt->ImageBuffer + vm_offset,
            Buffer,
            *Length);

        status = STATUS_SUCCESS;
        io_status.Status = status;
        io_status.Information = *Length;
    }
    else if (pLUExt->UseProxy)
        status = ImScsiWriteProxy(
            &pLUExt->Proxy,
            &io_status,
            &pLUExt->StopThread,
            Buffer,
            *Length,
            &byteoffset);
    else if (pLUExt->ImageFile != NULL)
	status = ZwWriteFile(
            pLUExt->ImageFile,
            NULL,
            NULL,
            NULL,
            &io_status,
            Buffer,
            *Length,
            &byteoffset,
            NULL);

    *Length = (ULONG)io_status.Information;

    KdPrint2(("PhDskMnt::ImScsiWriteDevice Result: pLUExt=%p, status=0x%X, Length=0x%X\n", pLUExt, status, *Length));

    return status;
}

NTSTATUS
ImScsiInitializeLU(IN pHW_LU_EXTENSION LUExtension,
		 IN OUT PSRB_IMSCSI_CREATE_DATA CreateData,
		 IN PETHREAD ClientThread)
{
  UNICODE_STRING file_name;
  HANDLE thread_handle = NULL;
  NTSTATUS status;
  HANDLE file_handle = NULL;
  PUCHAR image_buffer = NULL;
  PROXY_CONNECTION proxy = { 0 };
  ULONG alignment_requirement;

  ASSERT(CreateData != NULL);

  KdPrint
    (("PhDskMnt: Got request to create a virtual disk. Request data:\n"
      "DeviceNumber   = %#x\n"
      "DiskSize       = %I64u\n"
      "ImageOffset    = %I64u\n"
      "SectorSize     = %u\n"
      "Flags          = %#x\n"
      "FileNameLength = %u\n"
      "FileName       = '%.*ws'\n",
      CreateData->DeviceNumber,
      CreateData->DiskSize.QuadPart,
      CreateData->ImageOffset.QuadPart,
      CreateData->BytesPerSector,
      CreateData->Flags,
      CreateData->FileNameLength,
      (int)(CreateData->FileNameLength / sizeof(*CreateData->FileName)),
      CreateData->FileName));

  // Auto-select type if not specified.
  if (IMSCSI_TYPE(CreateData->Flags) == 0)
    if (CreateData->FileNameLength == 0)
      CreateData->Flags |= IMSCSI_TYPE_VM;
    else
      CreateData->Flags |= IMSCSI_TYPE_FILE;

  // Blank filenames only supported for non-zero VM disks.
  if (((CreateData->FileNameLength == 0) &
       (IMSCSI_TYPE(CreateData->Flags) != IMSCSI_TYPE_VM)) |
      ((CreateData->FileNameLength == 0) &
       (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM) &
       (CreateData->DiskSize.QuadPart == 0)))
    {
      KdPrint(("PhDskMnt: Blank filenames only supported for non-zero length "
	       "vm type disks.\n"));

      ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
		      0,
		      0,
		      NULL,
		      0,
		      1000,
		      STATUS_INVALID_PARAMETER,
		      102,
		      STATUS_INVALID_PARAMETER,
		      0,
		      0,
		      NULL,
		      L"Blank filenames only supported for non-zero length "
		      L"vm type disks."));

      return STATUS_INVALID_PARAMETER;
    }

  // Cannot create >= 2 GB VM disk in 32 bit version.
#ifndef _WIN64
  if ((IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM) &
      ((CreateData->DiskSize.QuadPart & 0xFFFFFFFF80000000) !=
       0))
    {
      KdPrint(("PhDskMnt: Cannot create >= 2GB vm disks on 32-bit system.\n"));

      return STATUS_INVALID_PARAMETER;
    }
#endif

  file_name.Length = CreateData->FileNameLength;
  file_name.MaximumLength = CreateData->FileNameLength;
  file_name.Buffer = NULL;

  // If a file is to be opened or created, allocate name buffer and open that
  // file...
  if (CreateData->FileNameLength > 0)
    {
      IO_STATUS_BLOCK io_status;
      OBJECT_ATTRIBUTES object_attributes;
      UNICODE_STRING real_file_name;

      file_name.Buffer = (PWCHAR) ExAllocatePoolWithTag(NonPagedPool,
          file_name.MaximumLength, MP_TAG_GENERAL);

      if (file_name.Buffer == NULL)
	{
	  KdPrint(("PhDskMnt: Error allocating buffer for filename.\n"));

	  ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			  0,
			  0,
			  NULL,
			  0,
			  1000,
			  STATUS_INSUFFICIENT_RESOURCES,
			  102,
			  STATUS_INSUFFICIENT_RESOURCES,
			  0,
			  0,
			  NULL,
			  L"Memory allocation error."));

	  return STATUS_INSUFFICIENT_RESOURCES;
	}

      RtlCopyMemory(file_name.Buffer, CreateData->FileName,
		    CreateData->FileNameLength);
      // If no device-type specified, check if filename ends with .iso, .nrg or
      // .bin. In that case, set device-type automatically to FILE_DEVICE_CDROM
      if ((IMSCSI_DEVICE_TYPE(CreateData->Flags) == 0) &
	  (CreateData->FileNameLength >= (4 * sizeof(*CreateData->FileName))))
	{
	  LPWSTR name = CreateData->FileName +
	    (CreateData->FileNameLength / sizeof(*CreateData->FileName)) - 4;
	  if ((_wcsnicmp(name, L".iso", 4) == 0) |
	      (_wcsnicmp(name, L".nrg", 4) == 0) |
	      (_wcsnicmp(name, L".bin", 4) == 0))
	    CreateData->Flags |= IMSCSI_DEVICE_TYPE_CD | IMSCSI_OPTION_RO;
	}

      if (IMSCSI_DEVICE_TYPE(CreateData->Flags) == IMSCSI_DEVICE_TYPE_CD)
	CreateData->Flags |= IMSCSI_OPTION_RO;

      KdPrint(("PhDskMnt: Done with device type auto-selection by file ext.\n"));

      if (ClientThread != NULL)
	{
	  SECURITY_QUALITY_OF_SERVICE security_quality_of_service;
	  SECURITY_CLIENT_CONTEXT security_client_context;

	  RtlZeroMemory(&security_quality_of_service,
			sizeof(SECURITY_QUALITY_OF_SERVICE));

	  security_quality_of_service.Length =
	    sizeof(SECURITY_QUALITY_OF_SERVICE);
	  security_quality_of_service.ImpersonationLevel =
	    SecurityImpersonation;
	  security_quality_of_service.ContextTrackingMode =
	    SECURITY_STATIC_TRACKING;
	  security_quality_of_service.EffectiveOnly = FALSE;

          status =
              SeCreateClientSecurity(
              ClientThread,
              &security_quality_of_service,
              FALSE,
              &security_client_context);

          if (NT_SUCCESS(status))
          {
              KdPrint(("PhDskMnt: Impersonating client thread token.\n"));
              SeImpersonateClient(&security_client_context, NULL);
              SeDeleteClientSecurity(&security_client_context);
          }
          else
              DbgPrint("PhDskMnt: Error impersonating client thread token: %#X\n", status);

          ObDereferenceObject(ClientThread);
	}
      else
	KdPrint(("PhDskMnt: No impersonation information.\n"));

      if ((IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY) &
	  ((IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_TCP) |
	   (IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_COMM)))
	{
	  RtlInitUnicodeString(&real_file_name, IMDPROXY_SVC_PIPE_NATIVE_NAME);

	  InitializeObjectAttributes(&object_attributes,
				     &real_file_name,
				     OBJ_CASE_INSENSITIVE,
				     NULL,
				     NULL);
	}
      else
	{
	  real_file_name = file_name;

	  InitializeObjectAttributes(&object_attributes,
				     &real_file_name,
				     OBJ_CASE_INSENSITIVE |
				     OBJ_FORCE_ACCESS_CHECK,
				     NULL,
				     NULL);
	}

      if ((IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY) &
	  (IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_SHM))
	{
	  proxy.connection_type = PROXY_CONNECTION_SHM;

	  status =
	    ZwOpenSection(&file_handle,
			  GENERIC_READ | GENERIC_WRITE,
			  &object_attributes);
	}
      else
	{
	  KdPrint(("PhDskMnt: Passing WriteMode=%#x and WriteShare=%#x\n",
		   (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY) |
		   ((IMSCSI_TYPE(CreateData->Flags) != IMSCSI_TYPE_VM) &
		    !IMSCSI_READONLY(CreateData->Flags)),
		   IMSCSI_READONLY(CreateData->Flags) |
		   (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM)));

	  status =
	    ZwCreateFile(&file_handle,
			 GENERIC_READ |
			 ((IMSCSI_TYPE(CreateData->Flags) ==
			   IMSCSI_TYPE_PROXY) |
			  (((IMSCSI_TYPE(CreateData->Flags) !=
			     IMSCSI_TYPE_VM) &
			    !IMSCSI_READONLY(CreateData->Flags))) ?
			  GENERIC_WRITE : 0),
			 &object_attributes,
			 &io_status,
			 NULL,
			 FILE_ATTRIBUTE_NORMAL,
			 FILE_SHARE_READ |
			 FILE_SHARE_DELETE |
			 (IMSCSI_READONLY(CreateData->Flags) |
			  (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM) ?
			  FILE_SHARE_WRITE : 0),
			 FILE_OPEN,
			 IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY ?
			 FILE_NON_DIRECTORY_FILE |
			 FILE_SEQUENTIAL_ONLY |
			 FILE_NO_INTERMEDIATE_BUFFERING |
			 FILE_SYNCHRONOUS_IO_NONALERT :
			 FILE_NON_DIRECTORY_FILE |
			 FILE_RANDOM_ACCESS |
			 FILE_NO_INTERMEDIATE_BUFFERING |
			 FILE_SYNCHRONOUS_IO_NONALERT,
			 NULL,
			 0);
	}

      // For 32 bit driver running on Windows 2000 and earlier, the above
      // call will fail because OBJ_FORCE_ACCESS_CHECK is not supported. If so,
      // STATUS_INVALID_PARAMETER is returned and we go on without any access
      // checks in that case.
#ifdef NT4_COMPATIBLE
      if (status == STATUS_INVALID_PARAMETER)
	{
	  InitializeObjectAttributes(&object_attributes,
				     &real_file_name,
				     OBJ_CASE_INSENSITIVE,
				     NULL,
				     NULL);

	  if ((IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY) &
	      (IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_SHM))
	    {
	      proxy.connection_type = PROXY_CONNECTION_SHM;
	      
	      status =
		ZwOpenSection(&file_handle,
			      GENERIC_READ | GENERIC_WRITE,
			      &object_attributes);
	    }
	  else
	    {
	      status =
		ZwCreateFile(&file_handle,
			     GENERIC_READ |
			     ((IMSCSI_TYPE(CreateData->Flags) ==
			       IMSCSI_TYPE_PROXY) |
			      (((IMSCSI_TYPE(CreateData->Flags) !=
				 IMSCSI_TYPE_VM) &
				!IMSCSI_READONLY(CreateData->Flags))) ?
			      GENERIC_WRITE : 0),
			     &object_attributes,
			     &io_status,
			     NULL,
			     FILE_ATTRIBUTE_NORMAL,
			     FILE_SHARE_READ |
			     FILE_SHARE_DELETE |
			     (IMSCSI_READONLY(CreateData->Flags) |
			      (IMSCSI_TYPE(CreateData->Flags) ==
			       IMSCSI_TYPE_VM) ?
			      FILE_SHARE_WRITE : 0),
			     FILE_OPEN,
			     IMSCSI_TYPE(CreateData->Flags) ==
			     IMSCSI_TYPE_PROXY ?
			     FILE_NON_DIRECTORY_FILE |
			     FILE_SEQUENTIAL_ONLY |
			     FILE_NO_INTERMEDIATE_BUFFERING |
			     FILE_SYNCHRONOUS_IO_NONALERT :
			     FILE_NON_DIRECTORY_FILE |
			     FILE_RANDOM_ACCESS |
			     FILE_NO_INTERMEDIATE_BUFFERING |
			     FILE_SYNCHRONOUS_IO_NONALERT,
			     NULL,
			     0);
	    }
	}
#endif

      if (!NT_SUCCESS(status))
	KdPrint(("PhDskMnt: Error opening file '%.*ws'. Status: %#x SpecSize: %i WritableFile: %i DevTypeFile: %i Flags: %#x\n",
		 (int)(real_file_name.Length / sizeof(WCHAR)),
		 real_file_name.Buffer,
		 status,
		 CreateData->DiskSize.QuadPart != 0,
		 !IMSCSI_READONLY(CreateData->Flags),
		 IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_FILE,
		 CreateData->Flags));

      // If not found we will create the file if a new non-zero size is
      // specified, read-only virtual disk is not specified and we are
      // creating a type 'file' virtual disk.
      if (((status == STATUS_OBJECT_NAME_NOT_FOUND) |
	   (status == STATUS_NO_SUCH_FILE)) &
	  (CreateData->DiskSize.QuadPart != 0) &
	  (!IMSCSI_READONLY(CreateData->Flags)) &
	  (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_FILE))
	{

	  status =
	    ZwCreateFile(&file_handle,
			 GENERIC_READ |
			 GENERIC_WRITE,
			 &object_attributes,
			 &io_status,
			 &CreateData->DiskSize,
			 FILE_ATTRIBUTE_NORMAL,
			 FILE_SHARE_READ |
			 FILE_SHARE_DELETE,
			 FILE_OPEN_IF,
			 FILE_NON_DIRECTORY_FILE |
			 FILE_RANDOM_ACCESS |
			 FILE_NO_INTERMEDIATE_BUFFERING |
			 FILE_SYNCHRONOUS_IO_NONALERT, NULL, 0);
      
	  if (!NT_SUCCESS(status))
	    {
	      ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			      0,
			      0,
			      NULL,
			      0,
			      1000,
			      status,
			      102,
			      status,
			      0,
			      0,
			      NULL,
			      L"Cannot create image file."));

	      KdPrint(("PhDskMnt: Cannot create '%.*ws'. (%#x)\n",
		       (int)(CreateData->FileNameLength /
			     sizeof(*CreateData->FileName)),
		       CreateData->FileName,
		       status));
	      
	      ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

	      return status;
	    }
	}
      else if (!NT_SUCCESS(status))
	{
	  ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			  0,
			  0,
			  NULL,
			  0,
			  1000,
			  status,
			  102,
			  status,
			  0,
			  0,
			  NULL,
			  L"Cannot open image file."));

	  KdPrint(("PhDskMnt: Cannot open file '%.*ws'. Status: %#x\n",
		   (int)(real_file_name.Length / sizeof(WCHAR)),
		   real_file_name.Buffer,
		   status));

	  ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);
	  
	  return status;
	}

      KdPrint(("PhDskMnt: File '%.*ws' opened successfully.\n",
	       (int)(real_file_name.Length / sizeof(WCHAR)),
	       real_file_name.Buffer));

      if (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY)
	{
	  if (IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_SHM)
	    status =
	      ZwMapViewOfSection(file_handle,
				 NtCurrentProcess(),
				 (PVOID*)&proxy.shared_memory,
				 0,
				 0,
				 NULL,
				 &proxy.shared_memory_size,
				 ViewUnmap,
				 0,
				 PAGE_READWRITE);
	  else
	    status =
	      ObReferenceObjectByHandle(file_handle,
					FILE_READ_ATTRIBUTES |
					FILE_READ_DATA |
					FILE_WRITE_DATA,
					*IoFileObjectType,
					KernelMode,
					(PVOID*)&proxy.device,
					NULL);

	  if (!NT_SUCCESS(status))
	    {
	      ZwClose(file_handle);
	      ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

	      ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			      0,
			      0,
			      NULL,
			      0,
			      1000,
			      status,
			      102,
			      status,
			      0,
			      0,
			      NULL,
			      L"Error referencing proxy device."));

	      KdPrint(("PhDskMnt: Error referencing proxy device (%#x).\n",
		       status));

	      return status;
	    }

	  KdPrint(("PhDskMnt: Got reference to proxy object %#x.\n",
		   proxy.connection_type == PROXY_CONNECTION_DEVICE ?
		   (PVOID) proxy.device :
		   (PVOID) proxy.shared_memory));

	  if (IMSCSI_PROXY_TYPE(CreateData->Flags) != IMSCSI_PROXY_TYPE_DIRECT)
	    status = ImScsiConnectProxy(&proxy,
					&io_status,
					NULL,
					CreateData->Flags,
					CreateData->FileName,
					CreateData->FileNameLength);

	  if (!NT_SUCCESS(status))
	    {
	      ImScsiCloseProxy(&proxy);
	      ZwClose(file_handle);
	      ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

	      ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			      0,
			      0,
			      NULL,
			      0,
			      1000,
			      status,
			      102,
			      status,
			      0,
			      0,
			      NULL,
			      L"Error connecting proxy."));

	      KdPrint(("PhDskMnt: Error connecting proxy (%#x).\n", status));

	      return status;
	    }
	}

      // Get the file size of the disk file.
      if (IMSCSI_TYPE(CreateData->Flags) != IMSCSI_TYPE_PROXY)
	{
	  FILE_STANDARD_INFORMATION file_standard;

	  status = ZwQueryInformationFile(file_handle,
					  &io_status,
					  &file_standard,
					  sizeof(FILE_STANDARD_INFORMATION),
					  FileStandardInformation);

	  if (!NT_SUCCESS(status))
	    {
	      ZwClose(file_handle);
	      ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

	      ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			      0,
			      0,
			      NULL,
			      0,
			      1000,
			      status,
			      102,
			      status,
			      0,
			      0,
			      NULL,
			      L"Error getting FILE_STANDARD_INFORMATION."));

	      KdPrint
		(("PhDskMnt: Error getting FILE_STANDARD_INFORMATION (%#x).\n",
		  status));

	      return status;
	    }

	  // Allocate virtual memory for 'vm' type.
	  if (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM)
	    {
	      SIZE_T max_size;

	      // If no size given for VM disk, use size of pre-load image file.
	      // This code is somewhat easier for 64 bit architectures.

#ifdef _WIN64
	      if (CreateData->DiskSize.QuadPart == 0)
		CreateData->DiskSize.QuadPart =
		  file_standard.EndOfFile.QuadPart -
		  CreateData->ImageOffset.QuadPart;

	      max_size = CreateData->DiskSize.QuadPart;
#else
	      if (CreateData->DiskSize.QuadPart == 0)
		// Check that file size < 2 GB.
		if ((file_standard.EndOfFile.QuadPart -
		     CreateData->ImageOffset.QuadPart) & 0xFFFFFFFF80000000)
		  {
		    ZwClose(file_handle);
		    ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

		    KdPrint(("PhDskMnt: VM disk >= 2GB not supported.\n"));

		    return STATUS_INSUFFICIENT_RESOURCES;
		  }
		else
		  CreateData->DiskSize.QuadPart =
		    file_standard.EndOfFile.QuadPart -
		    CreateData->ImageOffset.QuadPart;

	      max_size = CreateData->DiskSize.LowPart;
#endif

	      status =
		ZwAllocateVirtualMemory(NtCurrentProcess(),
					(PVOID*)&image_buffer,
					0,
					&max_size,
					MEM_COMMIT,
					PAGE_READWRITE);
	      if (!NT_SUCCESS(status))
		{
		  ZwClose(file_handle);
		  ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

		  ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
				  0,
				  0,
				  NULL,
				  0,
				  1000,
				  status,
				  102,
				  status,
				  0,
				  0,
				  NULL,
				  L"Not enough memory for VM disk."));

		  KdPrint(("PhDskMnt: Error allocating vm for image. (%#x)\n",
			   status));
		  
		  return STATUS_NO_MEMORY;
		}

	      alignment_requirement = FILE_BYTE_ALIGNMENT;

	      // Loading of image file has been moved to be done just before
	      // the service loop.
	    }
	  else
	    {
	      FILE_ALIGNMENT_INFORMATION file_alignment;

	      status = ZwQueryInformationFile(file_handle,
					      &io_status,
					      &file_alignment,
					      sizeof
					      (FILE_ALIGNMENT_INFORMATION),
					      FileAlignmentInformation);
	  
	      if (!NT_SUCCESS(status))
		{
		  ZwClose(file_handle);
		  ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

		  ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
				  0,
				  0,
				  NULL,
				  0,
				  1000,
				  status,
				  102,
				  status,
				  0,
				  0,
				  NULL,
				  L"Error getting alignment information."));

		  KdPrint(("PhDskMnt: Error querying file alignment (%#x).\n",
			   status));

		  return status;
		}

              // If creating a sparse image file
              if (IMSCSI_SPARSE_FILE(CreateData->Flags))
              {
                  status = ZwFsControlFile(
                      file_handle,
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

                  if (NT_SUCCESS(status))
                      KdPrint(("PhDskMnt::ImScsiInitializeLU: Sparse attribute set on image file.\n"));
                  else
                      DbgPrint("PhDskMnt::ImScsiInitializeLU: Cannot set sparse attribute on image file: 0x%X\n", status);
              }

	      if (CreateData->DiskSize.QuadPart == 0)
                  CreateData->DiskSize.QuadPart =
                  file_standard.EndOfFile.QuadPart -
                  CreateData->ImageOffset.QuadPart;
              else if ((file_standard.EndOfFile.QuadPart <
                  CreateData->DiskSize.QuadPart +
                  CreateData->ImageOffset.QuadPart) &
                  (!IMSCSI_READONLY(CreateData->Flags)))
              {
                  LARGE_INTEGER new_image_size;
                  new_image_size.QuadPart =
                      CreateData->DiskSize.QuadPart +
                      CreateData->ImageOffset.QuadPart;

                  // Adjust the file length to the requested virtual disk size.
                  status = ZwSetInformationFile(
                      file_handle,
                      &io_status,
                      &new_image_size,
                      sizeof(FILE_END_OF_FILE_INFORMATION),
                      FileEndOfFileInformation);

                  if (!NT_SUCCESS(status))
                  {
                      ZwClose(file_handle);
                      ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                      ImScsiLogError((
                          pMPDrvInfoGlobal->pDriverObj,
                          0,
                          0,
                          NULL,
                          0,
                          1000,
                          status,
                          102,
                          status,
                          0,
                          0,
                          NULL,
                          L"Error setting file size."));

                      DbgPrint("PhDskMnt: Error setting eof (%#x).\n", status);
                      return status;
                  }
              }

              alignment_requirement = file_alignment.AlignmentRequirement;
            }
        }
      else
	// If proxy is used, get the image file size from the proxy instead.
	{
	  IMDPROXY_INFO_RESP proxy_info;

	  status = ImScsiQueryInformationProxy(&proxy,
					       &io_status,
					       NULL,
					       &proxy_info,
					       sizeof(IMDPROXY_INFO_RESP));

	  if (!NT_SUCCESS(status))
	    {
	      ImScsiCloseProxy(&proxy);
	      ZwClose(file_handle);
	      ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

	      ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			      0,
			      0,
			      NULL,
			      0,
			      1000,
			      status,
			      102,
			      status,
			      0,
			      0,
			      NULL,
			      L"Error querying proxy."));

	      KdPrint(("PhDskMnt: Error querying proxy (%#x).\n", status));

	      return status;
	    }

	  if (CreateData->DiskSize.QuadPart == 0)
	    CreateData->DiskSize.QuadPart = proxy_info.file_size;

	  if ((proxy_info.req_alignment - 1 > FILE_512_BYTE_ALIGNMENT) |
	      (CreateData->DiskSize.QuadPart == 0))
	    {
	      ImScsiCloseProxy(&proxy);
	      ZwClose(file_handle);
	      ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

	      ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			      0,
			      0,
			      NULL,
			      0,
			      1000,
			      status,
			      102,
			      status,
			      0,
			      0,
			      NULL,
			      L"Unsupported sizes."));

	      KdPrint(("PhDskMnt: Unsupported sizes. "
		       "Got 0x%08x%08x size and 0x%08x%08x alignment.\n",
		       proxy_info.file_size,
		       proxy_info.req_alignment));

	      return STATUS_INVALID_PARAMETER;
	    }

	  alignment_requirement = (ULONG) proxy_info.req_alignment - 1;

	  if (proxy_info.flags & IMDPROXY_FLAG_RO)
	    CreateData->Flags |= IMSCSI_OPTION_RO;

	  KdPrint(("PhDskMnt: Got from proxy: Siz=0x%08x%08x Flg=%#x Alg=%#x.\n",
		   CreateData->DiskSize.HighPart,
		   CreateData->DiskSize.LowPart,
		   (ULONG) proxy_info.flags,
		   (ULONG) proxy_info.req_alignment));
	}

      if (CreateData->DiskSize.QuadPart == 0)
	{
	  SIZE_T free_size = 0;
      
	  ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			  0,
			  0,
			  NULL,
			  0,
			  1000,
			  status,
			  102,
			  status,
			  0,
			  0,
			  NULL,
			  L"Disk size equals zero."));

	  KdPrint(("PhDskMnt: Fatal error: Disk size equals zero.\n"));

	  ImScsiCloseProxy(&proxy);
	  if (file_handle != NULL)
	    ZwClose(file_handle);
	  if (file_name.Buffer != NULL)
	    ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);
	  if (image_buffer != NULL)
	    ZwFreeVirtualMemory(NtCurrentProcess(),
				(PVOID*)&image_buffer,
				&free_size, MEM_RELEASE);

	  return STATUS_INVALID_PARAMETER;
	}
    }
  // Blank vm-disk, just allocate...
  else
    {
      SIZE_T max_size;
#ifdef _WIN64
      max_size = CreateData->DiskSize.QuadPart;
#else
      max_size = CreateData->DiskSize.LowPart;
#endif

      image_buffer = NULL;
      status =
	ZwAllocateVirtualMemory(NtCurrentProcess(),
				(PVOID*)&image_buffer,
				0,
				&max_size,
				MEM_COMMIT,
				PAGE_READWRITE);
      if (!NT_SUCCESS(status))
	{
	  KdPrint
	    (("PhDskMnt: Error allocating virtual memory for vm disk (%#x).\n",
	      status));

	  ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
			  0,
			  0,
			  NULL,
			  0,
			  1000,
			  status,
			  102,
			  status,
			  0,
			  0,
			  NULL,
			  L"Not enough free memory for VM disk."));

	  return STATUS_NO_MEMORY;
	}

      alignment_requirement = FILE_BYTE_ALIGNMENT;
    }

  KdPrint(("PhDskMnt: Done with file/memory checks.\n"));

  // If no device-type specified and size matches common floppy sizes,
  // auto-select FILE_DEVICE_DISK with FILE_FLOPPY_DISKETTE and
  // FILE_REMOVABLE_MEDIA.
  // If still no device-type specified, specify FILE_DEVICE_DISK with no
  // particular characteristics. This will emulate a hard disk partition.
  if (IMSCSI_DEVICE_TYPE(CreateData->Flags) == 0)
      CreateData->Flags |= IMSCSI_DEVICE_TYPE_HD;
	  
  KdPrint(("PhDskMnt: Done with device type selection for floppy sizes.\n"));

  // If some parts of the DISK_GEOMETRY structure are zero, auto-fill with
  // typical values for this type of disk.
  if (IMSCSI_DEVICE_TYPE(CreateData->Flags) == IMSCSI_DEVICE_TYPE_CD)
    {
      if (CreateData->BytesPerSector == 0)
	CreateData->BytesPerSector = SECTOR_SIZE_CD_ROM;

      CreateData->Flags |= IMSCSI_OPTION_REMOVABLE | IMSCSI_OPTION_RO;
    }
  else
      if (CreateData->BytesPerSector == 0)
	CreateData->BytesPerSector = SECTOR_SIZE_HDD;

  KdPrint(("PhDskMnt: Done with disk geometry setup.\n"));

  // Now build real DeviceType and DeviceCharacteristics parameters.
  if (IMSCSI_DEVICE_TYPE(CreateData->Flags) == IMSCSI_DEVICE_TYPE_CD)
      LUExtension->DeviceType = READ_ONLY_DIRECT_ACCESS_DEVICE;
  else
      LUExtension->DeviceType = DIRECT_ACCESS_DEVICE;

  if (IMSCSI_READONLY(CreateData->Flags))
      LUExtension->ReadOnly = TRUE;
  if (IMSCSI_REMOVABLE(CreateData->Flags))
      LUExtension->RemovableMedia = TRUE;

  if (alignment_requirement > CreateData->BytesPerSector)
      CreateData->BytesPerSector = alignment_requirement + 1;

    KdPrint
    (("PhDskMnt: After checks and translations we got this create data:\n"
        "DeviceNumber   = %#x\n"
        "DiskSize       = %I64u\n"
        "ImageOffset    = %I64u\n"
        "SectorSize     = %u\n"
        "Flags          = %#x\n"
        "FileNameLength = %u\n"
        "FileName       = '%.*ws'\n",
        CreateData->DeviceNumber,
        CreateData->DiskSize.QuadPart,
        CreateData->ImageOffset.QuadPart,
        CreateData->BytesPerSector,
        CreateData->Flags,
        CreateData->FileNameLength,
        (int)(CreateData->FileNameLength / sizeof(*CreateData->FileName)),
        CreateData->FileName));

    LUExtension->ObjectName = file_name;

    LUExtension->DiskSize = CreateData->DiskSize;

    while (CreateData->BytesPerSector >>= 1)
        LUExtension->BlockPower++;
    if (LUExtension->BlockPower == 0)
        LUExtension->BlockPower = DEFAULT_BLOCK_POWER;
    CreateData->BytesPerSector = 1UL << LUExtension->BlockPower;

    LUExtension->ImageOffset = CreateData->ImageOffset;

    // VM disk.
    if (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM)
    LUExtension->VMDisk = TRUE;
    else
    LUExtension->VMDisk = FALSE;

    LUExtension->ImageBuffer = image_buffer;
    LUExtension->ImageFile = file_handle;

    // Use proxy service.
    if (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY)
    {
        LUExtension->Proxy = proxy;
        LUExtension->UseProxy = TRUE;
    }
    else
        LUExtension->UseProxy = FALSE;

    // If we are going to fake a disk signature if existing one
    // is all zeroes and device is read-only, prepare that fake
    // disk sig here.
    if ((CreateData->Flags & IMSCSI_FAKE_DISK_SIG_IF_ZERO) &&
        IMSCSI_READONLY(CreateData->Flags))
        LUExtension->FakeDiskSignature =
            (RtlRandomEx(&pMPDrvInfoGlobal->RandomSeed) |
            0x80808081UL) &	0xFEFEFEFFUL;

    KeInitializeSpinLock(&LUExtension->RequestListLock);
    InitializeListHead(&LUExtension->RequestList);
    KeInitializeEvent(&LUExtension->RequestEvent, SynchronizationEvent, FALSE);

    KeInitializeSpinLock(&LUExtension->LastIoLock);

    LUExtension->Initialized = TRUE;

    KdPrint(("PhDskMnt::ImScsiCreateLU: Creating worker thread for pLUExt=0x%p.\n", LUExtension));

    status = PsCreateSystemThread(
        &thread_handle,
        (ACCESS_MASK) 0L,
        NULL,
        NULL,
        NULL,
        ImScsiWorkerThread,
        LUExtension);

    if (!NT_SUCCESS(status))
    {
        DbgPrint("PhDskMnt::ImScsiDispatchWork: Cannot create worker thread. (%#x)\n", status);
        return status;
    }

    ZwClose(thread_handle);

    KdPrint(("PhDskMnt: Device created.\n"));

    return STATUS_SUCCESS;
}

BOOLEAN
ImScsiFillMemoryDisk(pHW_LU_EXTENSION LUExtension)
{
    LARGE_INTEGER byte_offset = LUExtension->ImageOffset;
    IO_STATUS_BLOCK io_status;
    NTSTATUS status;
#ifdef _WIN64
    SIZE_T disk_size = LUExtension->DiskSize.QuadPart;
#else
    SIZE_T disk_size = LUExtension->DiskSize.LowPart;
#endif

    KdPrint(("PhDskMnt: Reading image file into vm disk buffer.\n"));

    status =
        ImScsiSafeReadFile(
        LUExtension->ImageFile,
        &io_status,
        LUExtension->ImageBuffer,
        disk_size,
        &byte_offset);

    ZwClose(LUExtension->ImageFile);
    LUExtension->ImageFile = NULL;

    // Failure to read pre-load image is now considered a fatal error
    if (!NT_SUCCESS(status))
    {
	KdPrint(("PhDskMnt: Failed to read image file (%#x).\n", status));

	return FALSE;
    }

    KdPrint(("PhDskMnt: Image loaded successfully.\n"));

    return TRUE;
}
