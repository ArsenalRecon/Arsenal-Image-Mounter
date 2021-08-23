
/// iodisp.c
/// Routines called from worker thread at PASSIVE_LEVEL to complete work items
/// queued form miniport dispatch routines.
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

//#pragma warning(push)
//#pragma warning(disable : 4204)                       /* Prevent C4204 messages from stortrce.h. */
//#include <stortrce.h>
//#pragma warning(pop)
//
//#include "trace.h"
//#include "iodisp.tmh"

//
// Number of bits to use in block size mask. For instance,
// 21 = 2 MB, 19 = 512 KB, 16 = 64 K, 12 = 4 K etc.
// The smaller block size the more non-paged pool is needed
// for the allocation table. On the other hand, smaller block
// sizes mean less space likely wasted on diff device to fill
// up complete blocks as new blocks are allocated by small
// write requests.
//
#define DIFF_BLOCK_BITS                         16

//
// Macros for easier block/offset calculation
//
#define DIFF_BLOCK_SIZE                         (1ULL << DIFF_BLOCK_BITS)


/**************************************************************************************************/
/*                                                                                                */
/* Globals, forward definitions, etc.                                                             */
/*                                                                                                */
/**************************************************************************************************/

VOID
ImScsiCleanupLU(
__in pHW_LU_EXTENSION     pLUExt,
__inout __deref PKIRQL         LowestAssumedIrql
)
{
    pHW_HBA_EXT             pHBAExt = pLUExt->pHBAExt;
    pHW_LU_EXTENSION *      ppLUExt = NULL;
    PLIST_ENTRY             list_ptr;
    pMP_WorkRtnParms        free_worker_params = NULL;
    KLOCK_QUEUE_HANDLE      LockHandle;

    KdPrint(("PhDskMnt::ImScsiCleanupLU: Removing device: %d:%d:%d pLUExt=%p\n",
        pLUExt->DeviceNumber.PathId,
        pLUExt->DeviceNumber.TargetId,
        pLUExt->DeviceNumber.Lun,
        pLUExt));

    free_worker_params = (pMP_WorkRtnParms)ExAllocatePoolWithTag(
        NonPagedPool, sizeof(MP_WorkRtnParms), MP_TAG_GENERAL);

    if (free_worker_params == NULL)
    {
        DbgPrint("PhDskMnt::ImScsiCleanupLU: Memory allocation error.\n");
        return;
    }

    RtlZeroMemory(free_worker_params, sizeof(MP_WorkRtnParms));
    free_worker_params->pHBAExt = pHBAExt;
    free_worker_params->pLUExt = pLUExt;
    
    ImScsiAcquireLock(                   // Serialize the linked list of LUN extensions.              
        &pHBAExt->LUListLock, &LockHandle, *LowestAssumedIrql);

    ppLUExt = (pHW_LU_EXTENSION*)StoragePortGetLogicalUnit(
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
            KLOCK_QUEUE_HANDLE inner_lock_handle;
            KIRQL inner_assumed_irql = DISPATCH_LEVEL;

            list_ptr->Blink->Flink = list_ptr->Flink;
            list_ptr->Flink->Blink = list_ptr->Blink;

            // If a worker thread has started, we are now in that
            // thread context and will terminate that thread after
            // this function has run to completion. Instruct the
            // global worker thread to wait for this thread. It
            // will then free our LUExt in a place where it is
            // guaranteed to be unused.
            //
            // If a worker thread has not started, we are now in
            // the context of global worker thread. Instruct it
            // to free LUExt as next request, which will happen
            // after this function has run to completion.
            KdPrint(("PhDskMnt::ImScsiCleanupLU: Setting request to wait for LU worker thread.\n"));

            ImScsiAcquireLock(&pMPDrvInfoGlobal->RequestListLock,
                &inner_lock_handle, inner_assumed_irql);

            InsertTailList(&pMPDrvInfoGlobal->RequestList,
                &free_worker_params->RequestListEntry);

            ImScsiReleaseLock(&inner_lock_handle, &inner_assumed_irql);

            KeSetEvent(&pMPDrvInfoGlobal->RequestEvent, (KPRIORITY)0, FALSE);

            break;
        }
    }

    ImScsiReleaseLock(&LockHandle, LowestAssumedIrql);

    /// Cleanup all file handles, object name buffers,
    /// proxy refs etc.
    if (pLUExt->UseProxy)
    {
        ImScsiCloseProxy(&pLUExt->Proxy);
    }

    if (pLUExt->LastIoBuffer != NULL)
    {
        ExFreePoolWithTag(pLUExt->LastIoBuffer, MP_TAG_GENERAL);
        pLUExt->LastIoBuffer = NULL;
    }

    if (pLUExt->VMDisk)
    {
        SIZE_T free_size = 0;
        if (pLUExt->ImageBuffer != NULL)
        {
            ZwFreeVirtualMemory(NtCurrentProcess(),
                (PVOID*)&pLUExt->ImageBuffer,
                &free_size, MEM_RELEASE);

            pLUExt->ImageBuffer = NULL;
        }
    }
    else
    {
        if (pLUExt->FileObject != NULL)
        {
            ObDereferenceObject(pLUExt->FileObject);
            pLUExt->FileObject = NULL;
        }

        if (pLUExt->ImageFile != NULL)
        {
            ZwClose(pLUExt->ImageFile);
            pLUExt->ImageFile = NULL;
        }

        if (pLUExt->WriteOverlay != NULL)
        {
            ZwClose(pLUExt->WriteOverlay);
            pLUExt->WriteOverlay = NULL;
        }

        if (pLUExt->ReservationKeyFile != NULL)
        {
            ZwClose(pLUExt->ReservationKeyFile);
            pLUExt->ReservationKeyFile = NULL;
        }
    }
    
    if (pLUExt->ObjectName.Buffer != NULL)
    {
        ExFreePoolWithTag(pLUExt->ObjectName.Buffer, MP_TAG_GENERAL);
        pLUExt->ObjectName.Buffer = NULL;
        pLUExt->ObjectName.Length = 0;
        pLUExt->ObjectName.MaximumLength = 0;
    }

    if (pLUExt->WriteOverlayFileName.Buffer != NULL)
    {
        ExFreePoolWithTag(pLUExt->WriteOverlayFileName.Buffer, MP_TAG_GENERAL);
        pLUExt->WriteOverlayFileName.Buffer = NULL;
        pLUExt->WriteOverlayFileName.Length = 0;
        pLUExt->WriteOverlayFileName.MaximumLength = 0;
    }

    KdPrint(("PhDskMnt::ImScsiCleanupLU: Done.\n"));
}

#ifdef USE_SCSIPORT

NTSTATUS
ImScsiIoCtlCallCompletion(
PDEVICE_OBJECT DeviceObject,
PIRP Irp,
PVOID Context)
{
    UNREFERENCED_PARAMETER(DeviceObject);
    UNREFERENCED_PARAMETER(Context);

    if (!NT_SUCCESS(Irp->IoStatus.Status))
        DbgPrint("PhDskMnt::ImScsiIoCtlCallCompletion: SMB_IMSCSI_CHECK failed: 0x%X\n",
        Irp->IoStatus.Status);
    else
        KdPrint2(("PhDskMnt::ImScsiIoCtlCallCompletion: Finished SMB_IMSCSI_CHECK.\n"));

    ExFreePoolWithTag(Irp->AssociatedIrp.SystemBuffer, MP_TAG_GENERAL);

    ImScsiFreeIrpWithMdls(Irp);

    return STATUS_MORE_PROCESSING_REQUIRED;
}

#endif

NTSTATUS
ImScsiParallelReadWriteImageCompletion(
PDEVICE_OBJECT DeviceObject,
PIRP Irp,
PVOID Context)
{
    __analysis_assume(Context != NULL);

    pMP_WorkRtnParms pWkRtnParms = (pMP_WorkRtnParms)Context;
    PKTHREAD thread = NULL;
    KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

    UNREFERENCED_PARAMETER(DeviceObject);

    if (!NT_SUCCESS(Irp->IoStatus.Status))
    {
        switch (Irp->IoStatus.Status)
        {
        case STATUS_INVALID_BUFFER_SIZE:
        {
            DbgPrint("PhDskMnt::ImScsiParallelReadWriteImageCompletion: STATUS_INVALID_BUFFER_SIZE from image I/O. Reporting SCSI_SENSE_ILLEGAL_REQUEST/SCSI_ADSENSE_INVALID_CDB/0x00.\n");
            
            ScsiSetCheckCondition(
                pWkRtnParms->pSrb,
                SRB_STATUS_ERROR,
                SCSI_SENSE_ILLEGAL_REQUEST,
                SCSI_ADSENSE_INVALID_CDB,
                0);
            
            break;
        }

        case STATUS_DEVICE_BUSY:
        {
            DbgPrint("PhDskMnt::ImScsiParallelReadWriteImageCompletion: STATUS_DEVICE_BUSY from image I/O. Reporting SRB_STATUS_BUSY/SCSI_SENSE_NOT_READY/SCSI_ADSENSE_LUN_NOT_READY/SCSI_SENSEQ_BECOMING_READY.\n");
            
            ScsiSetCheckCondition(
                pWkRtnParms->pSrb,
                SRB_STATUS_BUSY,
                SCSI_SENSE_NOT_READY,
                SCSI_ADSENSE_LUN_NOT_READY,
                SCSI_SENSEQ_BECOMING_READY
                );

            break;
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

            ImScsiRemoveDevice(pWkRtnParms->pHBAExt, &pWkRtnParms->pLUExt->DeviceNumber, &lowest_assumed_irql);

            ScsiSetCheckCondition(
                pWkRtnParms->pSrb,
                SRB_STATUS_BUSY,
                SCSI_SENSE_NOT_READY,
                SCSI_ADSENSE_LUN_COMMUNICATION,
                SCSI_SENSEQ_NOT_REACHABLE
            );

            break;
        }

        default:
        {
            KdPrint(("PhDskMnt::ImScsiParallelReadWriteImageCompletion: Parallel I/O failed with status %#x\n",
                Irp->IoStatus.Status));

            ScsiSetCheckCondition(
                pWkRtnParms->pSrb,
                SRB_STATUS_ERROR,
                SCSI_SENSE_HARDWARE_ERROR,
                SCSI_ADSENSE_NO_SENSE,
                0);
            break;
        }
        }
    }
    else
    {
        ScsiSetSuccess(pWkRtnParms->pSrb, (ULONG)Irp->IoStatus.Information);

        if (pWkRtnParms->CopyBack)
        {
            RtlCopyMemory(pWkRtnParms->MappedSystemBuffer,
                pWkRtnParms->AllocatedBuffer,
                Irp->IoStatus.Information);
        }
    }

    if (Irp->MdlAddress != pWkRtnParms->pOriginalMdl)
    {
        ImScsiFreeIrpWithMdls(Irp);
    }
    else
    {
        IoFreeIrp(Irp);
    }

    if (pWkRtnParms->AllocatedBuffer != NULL)
    {
        PCDB pCdb = (PCDB)pWkRtnParms->pSrb->Cdb;
        LARGE_INTEGER startingSector;
        KLOCK_QUEUE_HANDLE LockHandle;

        if ((pCdb->AsByte[0] == SCSIOP_READ16) ||
            (pCdb->AsByte[0] == SCSIOP_WRITE16))
        {
            REVERSE_BYTES_QUAD(&startingSector, pCdb->CDB16.LogicalBlock);
        }
        else
        {
            REVERSE_BYTES(&startingSector, &pCdb->CDB10.LogicalBlockByte0);
        }

        if (thread == NULL)
        {
            thread = PsGetCurrentThread();
        }

        if (pWkRtnParms->pReqThread == thread)
        {
            lowest_assumed_irql = pWkRtnParms->LowestAssumedIrql;
        }

        ImScsiAcquireLock(&pWkRtnParms->pLUExt->LastIoLock, &LockHandle,
            lowest_assumed_irql);

        if (pWkRtnParms->pLUExt->LastIoBuffer != NULL)
        {
            ExFreePoolWithTag(pWkRtnParms->pLUExt->LastIoBuffer,
                MP_TAG_GENERAL);
        }

        pWkRtnParms->pLUExt->LastIoStartSector = startingSector.QuadPart;
        pWkRtnParms->pLUExt->LastIoLength =
            pWkRtnParms->pSrb->DataTransferLength;
        pWkRtnParms->pLUExt->LastIoBuffer = pWkRtnParms->AllocatedBuffer;

        ImScsiReleaseLock(&LockHandle, &lowest_assumed_irql);
    }

#ifdef USE_SCSIPORT

    if (thread == NULL)
    {
        thread = PsGetCurrentThread();
    }

    if (pWkRtnParms->pReqThread == thread)
    {
        KdPrint2(("PhDskMnt::ImScsiParallelReadWriteImageCompletion sending 'RequestComplete', 'NextRequest' and 'NextLuRequest' to ScsiPort.\n"));

        ScsiPortNotification(RequestComplete, pWkRtnParms->pHBAExt, pWkRtnParms->pSrb);
        ScsiPortNotification(NextRequest, pWkRtnParms->pHBAExt);
        ScsiPortNotification(NextLuRequest, pWkRtnParms->pHBAExt, 0, 0, 0);

        ExFreePoolWithTag(pWkRtnParms, MP_TAG_GENERAL);
    }
    else
    {
        PIRP ioctl_irp = NULL;
        KIRQL known_irql;

        if (pWkRtnParms->AllocatedBuffer != NULL)
        {
            known_irql = lowest_assumed_irql;
        }
        else
        {
            known_irql = KeGetCurrentIrql();
        }
        
        if (known_irql == PASSIVE_LEVEL)
        {
            ioctl_irp = ImScsiBuildCompletionIrp();

            if (ioctl_irp == NULL)
            {
                DbgPrint("PhDskMnt::ImScsiParallelReadWriteImageCompletion: ImScsiBuildCompletionIrp failed.\n");
            }
        }
        else
        {
            KdPrint2(("PhDskMnt::ImScsiParallelReadWriteImageCompletion: IRQL too high to call for Srb completion through SMP_IMSCSI_CHECK. Queuing for timer instead.\n"));
        }

        KdPrint2(("PhDskMnt::ImScsiParallelReadWriteImageCompletion calling for Srb completion.\n"));

        ImScsiCallForCompletion(ioctl_irp, pWkRtnParms, &lowest_assumed_irql);
    }

#endif

#ifdef USE_STORPORT

    KdPrint2(("PhDskMnt::ImScsiParallelReadWriteImageCompletion sending 'RequestComplete' to port StorPort.\n"));
    StorPortNotification(RequestComplete, pWkRtnParms->pHBAExt, pWkRtnParms->pSrb);

    ExFreePoolWithTag(pWkRtnParms, MP_TAG_GENERAL);

#endif

    return STATUS_MORE_PROCESSING_REQUIRED;
}

#ifdef USE_SCSIPORT
PIRP
ImScsiBuildCompletionIrp()
{
    PIRP ioctl_irp;
    PIO_STACK_LOCATION ioctl_stack;
    PSRB_IMSCSI_CHECK completion_srb;

    completion_srb = (PSRB_IMSCSI_CHECK)ExAllocatePoolWithTag(NonPagedPool,
        sizeof(*completion_srb), MP_TAG_GENERAL);

    if (completion_srb == NULL)
    {
        return NULL;
    }

    ImScsiInitializeSrbIoBlock(&completion_srb->SrbIoControl,
        sizeof(*completion_srb), SMP_IMSCSI_CHECK, 0);

    ioctl_irp = IoAllocateIrp(
        pMPDrvInfoGlobal->ControllerObject->StackSize, FALSE);

    if (ioctl_irp == NULL)
    {
        ExFreePoolWithTag(completion_srb, MP_TAG_GENERAL);
        return NULL;
    }

    ioctl_irp->AssociatedIrp.SystemBuffer = completion_srb;

    ioctl_stack = IoGetNextIrpStackLocation(ioctl_irp);
    ioctl_stack->MajorFunction = IRP_MJ_DEVICE_CONTROL;
    ioctl_stack->Parameters.DeviceIoControl.InputBufferLength =
        sizeof(*completion_srb);
    ioctl_stack->Parameters.DeviceIoControl.IoControlCode =
        IOCTL_SCSI_MINIPORT;

    IoSetCompletionRoutine(ioctl_irp, ImScsiIoCtlCallCompletion,
        NULL, TRUE, TRUE, TRUE);

    return ioctl_irp;
}

NTSTATUS
ImScsiCallForCompletion(__in __deref PIRP Irp OPTIONAL,
__in __deref pMP_WorkRtnParms pWkRtnParms,
__inout __deref PKIRQL LowestAssumedIrql)
{
    KLOCK_QUEUE_HANDLE lock_handle;

    KdPrint2(("PhDskMnt::ImScsiCallForCompletion: Invoking SMB_IMSCSI_CHECK for work: 0x%p.\n",
        pWkRtnParms));

    ImScsiAcquireLock(&pMPDrvInfoGlobal->ResponseListLock,
        &lock_handle, *LowestAssumedIrql);

    InsertTailList(
        &pMPDrvInfoGlobal->ResponseList,
        &pWkRtnParms->ResponseListEntry);

    ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

    if (Irp != NULL)
    {
        return IoCallDriver(pMPDrvInfoGlobal->ControllerObject, Irp);
    }
    else
    {
        return STATUS_SUCCESS;
    }
}
#endif // USE_SCSIPORT

VOID
ImScsiParallelReadWriteImage(
__in pMP_WorkRtnParms       pWkRtnParms,
__inout __deref pResultType pResult,
__inout __deref PKIRQL      LowestAssumedIrql
)
{
    PCDB pCdb = (PCDB)pWkRtnParms->pSrb->Cdb;
    PIO_STACK_LOCATION lower_io_stack = NULL;
    PDEVICE_OBJECT lower_device =
        IoGetRelatedDeviceObject(pWkRtnParms->pLUExt->FileObject);
    PIRP lower_irp;
    LARGE_INTEGER starting_sector = { 0 };
    LARGE_INTEGER starting_offset;
    UCHAR function = 0;
    BOOLEAN use_mdl = FALSE;

    if ((pCdb->AsByte[0] == SCSIOP_READ16) ||
        (pCdb->AsByte[0] == SCSIOP_WRITE16))
    {
        REVERSE_BYTES_QUAD(&starting_sector, pCdb->CDB16.LogicalBlock);
    }
    else
    {
        REVERSE_BYTES(&starting_sector, &pCdb->CDB10.LogicalBlockByte0);
    }

    starting_offset.QuadPart = (starting_sector.QuadPart <<
        pWkRtnParms->pLUExt->BlockPower) +
        pWkRtnParms->pLUExt->ImageOffset.QuadPart;

    KdPrint2(("PhDskMnt::ImScsiParallelReadWriteImage starting sector: 0x%I64X\n",
        starting_sector));

    if ((pWkRtnParms->pSrb->Cdb[0] == SCSIOP_READ) ||
        (pWkRtnParms->pSrb->Cdb[0] == SCSIOP_READ16))
    {
        function = IRP_MJ_READ;
    }
    else if ((pWkRtnParms->pSrb->Cdb[0] == SCSIOP_WRITE) ||
        (pWkRtnParms->pSrb->Cdb[0] == SCSIOP_WRITE16))
    {
        function = IRP_MJ_WRITE;
    }

#ifdef USE_STORPORT
    // Try to use original MDL if available
    if (PortSupportsGetOriginalMdl &&
        (lower_device->Flags & DO_DIRECT_IO))
    {
        ULONG result = StorPortGetOriginalMdl(pWkRtnParms->pHBAExt,
            pWkRtnParms->pSrb, (PVOID*)&pWkRtnParms->pOriginalMdl);

        if (result == STOR_STATUS_SUCCESS)
        {
            use_mdl = TRUE;
        }
        else if (result == STOR_STATUS_NOT_IMPLEMENTED)
        {
            PortSupportsGetOriginalMdl = FALSE;
        }
    }
#endif

    if (use_mdl)
    {
        lower_irp = IoAllocateIrp(lower_device->StackSize, FALSE);

        if (lower_irp != NULL)
        {
            lower_io_stack = IoGetNextIrpStackLocation(lower_irp);

            lower_io_stack->MajorFunction = function;
            lower_io_stack->Parameters.Read.ByteOffset = starting_offset;
            lower_io_stack->Parameters.Read.Length =
                pWkRtnParms->pSrb->DataTransferLength;

            lower_irp->MdlAddress = pWkRtnParms->pOriginalMdl;
        }
    }
    else
    {
        ULONG storage_status =
            StoragePortGetSystemAddress(pWkRtnParms->pHBAExt,
            pWkRtnParms->pSrb, &pWkRtnParms->MappedSystemBuffer);

        if ((storage_status != STORAGE_STATUS_SUCCESS) ||
            (pWkRtnParms->MappedSystemBuffer == NULL))
        {
            DbgPrint("PhDskMnt::ImScsiParallelReadWriteImage: Memory allocation failed: status=0x%X address=0x%p translated=0x%p\n",
                storage_status,
                pWkRtnParms->pSrb->DataBuffer,
                pWkRtnParms->MappedSystemBuffer);

            ScsiSetCheckCondition(pWkRtnParms->pSrb, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR,
                SCSI_ADSENSE_NO_SENSE, 0);

            return;
        }

        pWkRtnParms->AllocatedBuffer =
            ExAllocatePoolWithTag(NonPagedPool,
            pWkRtnParms->pSrb->DataTransferLength, MP_TAG_GENERAL);

        if (pWkRtnParms->AllocatedBuffer == NULL)
        {
            DbgPrint("PhDskMnt::ImScsiParallelReadWriteImage: Memory allocation failed: status=0x%X address=0x%p translated=0x%p\n",
                storage_status,
                pWkRtnParms->pSrb->DataBuffer,
                pWkRtnParms->MappedSystemBuffer);

            ScsiSetCheckCondition(pWkRtnParms->pSrb, SRB_STATUS_ERROR,
                SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_NO_SENSE, 0);

            return;
        }

        if (function == IRP_MJ_WRITE)
        {
            RtlCopyMemory(pWkRtnParms->AllocatedBuffer,
                pWkRtnParms->MappedSystemBuffer,
                pWkRtnParms->pSrb->DataTransferLength);
        }
        else if (function == IRP_MJ_READ)
        {
            pWkRtnParms->CopyBack = TRUE;
        }

        if (lower_device->Flags & DO_DIRECT_IO)
        {
            lower_irp = IoBuildAsynchronousFsdRequest(function,
                lower_device, pWkRtnParms->AllocatedBuffer,
                pWkRtnParms->pSrb->DataTransferLength,
                &starting_offset, NULL);

            if (lower_irp != NULL)
            {
                lower_io_stack = IoGetNextIrpStackLocation(lower_irp);
            }
        }
        else
        {
            lower_irp = IoAllocateIrp(lower_device->StackSize, FALSE);

            if (lower_irp != NULL)
            {
                lower_io_stack = IoGetNextIrpStackLocation(lower_irp);

                lower_io_stack->MajorFunction = function;
                lower_io_stack->Parameters.Read.ByteOffset =
                    starting_offset;
                lower_io_stack->Parameters.Read.Length =
                    pWkRtnParms->pSrb->DataTransferLength;

                if (lower_device->Flags & DO_BUFFERED_IO)
                {
                    lower_irp->AssociatedIrp.SystemBuffer =
                        pWkRtnParms->AllocatedBuffer;
                }
                else
                {
                    lower_irp->UserBuffer = pWkRtnParms->AllocatedBuffer;
                }
            }
        }
    }

    if (lower_irp == NULL)
    {
        if (pWkRtnParms->AllocatedBuffer != NULL)
        {
            ExFreePoolWithTag(pWkRtnParms->AllocatedBuffer, MP_TAG_GENERAL);
            pWkRtnParms->AllocatedBuffer = NULL;
        }

        DbgPrint("PhDskMnt::ImScsiParallelReadWriteImage: IRP allocation failed: data length=0x%u\n",
            pWkRtnParms->pSrb->DataTransferLength);

        ScsiSetCheckCondition(pWkRtnParms->pSrb, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_NO_SENSE, 0);

        return;
    }

    lower_irp->Tail.Overlay.Thread = NULL;

    if (function == IRP_MJ_READ)
    {
        lower_irp->Flags |= IRP_READ_OPERATION;
    }
    else if (function == IRP_MJ_WRITE)
    {
        lower_irp->Flags |= IRP_WRITE_OPERATION;
        lower_io_stack->Flags |= SL_WRITE_THROUGH;
    }

    lower_irp->Flags |= IRP_NOCACHE;

    lower_io_stack->FileObject = pWkRtnParms->pLUExt->FileObject;

    if ((function == IRP_MJ_WRITE) &&
        (!pWkRtnParms->pLUExt->Modified))
    {
        pWkRtnParms->pLUExt->Modified = TRUE;
    }

    pWkRtnParms->pReqThread = PsGetCurrentThread();
    pWkRtnParms->LowestAssumedIrql = *LowestAssumedIrql;

    IoSetCompletionRoutine(lower_irp, ImScsiParallelReadWriteImageCompletion,
        pWkRtnParms, TRUE, TRUE, TRUE);

    IoCallDriver(lower_device, lower_irp);

    *pResult = ResultQueued;

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
    {
        status = ImScsiReadProxy(
            &pLUExt->Proxy,
            &io_status,
            &pLUExt->StopThread,
            Buffer,
            *Length,
            &byteoffset);
    }
    else if (pLUExt->ImageFile != NULL)
    {
        status = NtReadFile(
            pLUExt->ImageFile,
            NULL,
            NULL,
            NULL,
            &io_status,
            Buffer,
            *Length,
            &byteoffset,
            NULL);
    }

    if (status == STATUS_END_OF_FILE)
    {
        KdPrint2(("PhDskMnt::ImScsiReadDevice pLUExt=%p, status=STATUS_END_OF_FILE, Length=0x%X. Returning zeroed buffer with requested length.\n", pLUExt, *Length));
        RtlZeroMemory(Buffer, *Length);
        status = STATUS_SUCCESS;
    }
    else if (NT_SUCCESS(status))
    {
        *Length = (ULONG)io_status.Information;
    }
    else
    {
        *Length = 0;
    }

    KdPrint2(("PhDskMnt::ImScsiReadDevice Result: pLUExt=%p, status=0x%X, Length=0x%X\n", pLUExt, status, *Length));

    return status;
}

NTSTATUS
ImScsiZeroDevice(
    __in pHW_LU_EXTENSION pLUExt,
    __in PLARGE_INTEGER   Offset,
    __in ULONG            Length
    )
{
    IO_STATUS_BLOCK io_status = { 0 };
    NTSTATUS status = STATUS_NOT_IMPLEMENTED;
    LARGE_INTEGER byteoffset;

    byteoffset.QuadPart = Offset->QuadPart + pLUExt->ImageOffset.QuadPart;

    KdPrint2(("PhDskMnt::ImScsiZeroDevice: pLUExt=%p, Offset=0x%I64X, EffectiveOffset=0x%I64X, Length=0x%X\n",
        pLUExt, *Offset, byteoffset, Length));

    pLUExt->Modified = TRUE;

    if (pLUExt->VMDisk)
    {
#ifdef _WIN64
        ULONG_PTR vm_offset = Offset->QuadPart;
#else
        ULONG_PTR vm_offset = Offset->LowPart;
#endif

        RtlZeroMemory(pLUExt->ImageBuffer + vm_offset,
            Length);

        status = STATUS_SUCCESS;
    }
    else if (pLUExt->UseProxy)
    {
        DEVICE_DATA_SET_RANGE range;
        range.StartingOffset = Offset->QuadPart;
        range.LengthInBytes = Length;

        status = ImScsiUnmapOrZeroProxy(
            &pLUExt->Proxy,
            IMDPROXY_REQ_ZERO,
            &io_status,
            &pLUExt->StopThread,
            1,
            &range);
    }
    else if (pLUExt->ImageFile != NULL)
    {
        FILE_ZERO_DATA_INFORMATION zerodata;
        zerodata.FileOffset = *Offset;
        zerodata.BeyondFinalZero.QuadPart = Offset->QuadPart + Length;

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
    }

    KdPrint2(("PhDskMnt::ImScsiZeroDevice Result: pLUExt=%p, status=0x%X\n",
        pLUExt, status));

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

    if (pLUExt->SupportsZero &&
        ImScsiIsBufferZero(Buffer, *Length))
    {
        status = ImScsiZeroDevice(pLUExt, Offset, *Length);

        if (NT_SUCCESS(status))
        {
            KdPrint2(("PhDskMnt::ImScsiWriteDevice: Zero block set at %I64i, bytes: %u.\n",
                Offset->QuadPart, *Length));

            return status;
        }

        KdPrint(("PhDskMnt::ImScsiWriteDevice: Volume does not support "
            "FSCTL_SET_ZERO_DATA: 0x%#X\n", status));

        pLUExt->SupportsZero = FALSE;
    }

    byteoffset.QuadPart = Offset->QuadPart + pLUExt->ImageOffset.QuadPart;

    KdPrint2(("PhDskMnt::ImScsiWriteDevice: pLUExt=%p, Buffer=%p, Offset=0x%I64X, EffectiveOffset=0x%I64X, Length=0x%X\n",
        pLUExt, Buffer, *Offset, byteoffset, *Length));

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
    {
        status = ImScsiWriteProxy(
            &pLUExt->Proxy,
            &io_status,
            &pLUExt->StopThread,
            Buffer,
            *Length,
            &byteoffset);
    }
    else if (pLUExt->ImageFile != NULL)
    {
        status = NtWriteFile(
            pLUExt->ImageFile,
            NULL,
            NULL,
            NULL,
            &io_status,
            Buffer,
            *Length,
            &byteoffset,
            NULL);
    }

    if (NT_SUCCESS(status))
    {
        *Length = (ULONG)io_status.Information;
    }
    else
    {
        *Length = 0;
    }

    KdPrint2(("PhDskMnt::ImScsiWriteDevice Result: pLUExt=%p, status=0x%X, Length=0x%X\n", pLUExt, status, *Length));

    return status;
}

VOID
ImScsiGenerateUniqueId(pHW_LU_EXTENSION pLUExt)
{
    IO_STATUS_BLOCK io_status;
    NTSTATUS status = STATUS_SUCCESS;

    if (pLUExt->ImageFile != NULL &&
        !pLUExt->UseProxy)
    {
        KdPrint(("PhDskMnt::ImScsiGenerateUniqueId: Image file, attempting to generate UID from volume/file ids\n"));

        WPoolMem<FILE_FS_VOLUME_INFORMATION, NonPagedPool> volume_info(
            sizeof(FILE_FS_VOLUME_INFORMATION) + MAXIMUM_VOLUME_LABEL_LENGTH);

        if (!volume_info)
        {
            status = STATUS_INSUFFICIENT_RESOURCES;
        }

        if (NT_SUCCESS(status))
        {
            status = ZwQueryVolumeInformationFile(
                pLUExt->ImageFile,
                &io_status,
                volume_info,
                (ULONG)volume_info.GetSize(),
                FS_INFORMATION_CLASS::FileFsVolumeInformation);
        }

        FILE_INTERNAL_INFORMATION internal_info = { 0 };

        if (NT_SUCCESS(status))
        {
            status = ZwQueryInformationFile(
                pLUExt->ImageFile,
                &io_status,
                &internal_info,
                sizeof(internal_info),
                FILE_INFORMATION_CLASS::FileInternalInformation);
        }

        if (NT_SUCCESS(status))
        {
            RtlCopyMemory(pLUExt->UniqueId, "AIMF", 4);
            *(PULONG)(pLUExt->UniqueId + 4) = volume_info->VolumeSerialNumber;
            *(PLARGE_INTEGER)(pLUExt->UniqueId + 8) = internal_info.IndexNumber;

            return;
        }

        KdPrint(("PhDskMnt::ImScsiGenerateUniqueId: Failed generating UID from volume/file ids: 0x%X\n",
            status));
    }

    KdPrint(("PhDskMnt::ImScsiGenerateUniqueId: Generating UID from new GUID\n"));

    for (;;)
    {
        status = ExUuidCreate((PGUID)pLUExt->UniqueId);

        if (NT_SUCCESS(status))
        {
            return;
        }

        KdPrint(("PhDskMnt::ImScsiGenerateUniqueId: Error generating GUID: 0x%X\n",
            status));

        LARGE_INTEGER interval;
        interval.QuadPart = -10000L * 20;    // 20 ms
        
        KeDelayExecutionThread(KernelMode, FALSE, &interval);
    }
}

NTSTATUS
ImScsiInitializeLU(__inout __deref pHW_LU_EXTENSION pLUExt,
__inout __deref PSRB_IMSCSI_CREATE_DATA CreateData,
__in __deref PETHREAD ClientThread)
{
    UNICODE_STRING file_name = { 0 };
    HANDLE thread_handle = NULL;
    NTSTATUS status;
    HANDLE file_handle = NULL;
    PUCHAR image_buffer = NULL;
    PROXY_CONNECTION proxy = { };
    ULONG alignment_requirement;
    BOOLEAN proxy_supports_unmap = FALSE;
    BOOLEAN proxy_supports_zero = FALSE;

    ASSERT(CreateData != NULL);

    KdPrint((
        "PhDskMnt: Got request to create a virtual disk. Request data:\n"
        "DeviceNumber   = %#x\n"
        "DiskSize       = %I64u\n"
        "ImageOffset    = %I64u\n"
        "SectorSize     = %u\n"
        "Flags          = %#x\n"
        "FileNameLength = %u\n"
        "FileName       = '%.*ws'\n"
        "WriteOverlayFileNameLength = %u\n"
        "FileName       = '%.*ws'\n",
        CreateData->Fields.DeviceNumber.LongNumber,
        CreateData->Fields.DiskSize.QuadPart,
        CreateData->Fields.ImageOffset.QuadPart,
        CreateData->Fields.BytesPerSector,
        CreateData->Fields.Flags,
        CreateData->Fields.FileNameLength,
        (int)(CreateData->Fields.FileNameLength / sizeof(*CreateData->Fields.FileName)),
        CreateData->Fields.FileName,
        CreateData->Fields.WriteOverlayFileNameLength,
        (int)(CreateData->Fields.WriteOverlayFileNameLength / sizeof(*CreateData->Fields.FileName)),
        CreateData->Fields.FileName + (CreateData->Fields.FileNameLength / sizeof(*CreateData->Fields.FileName))));

    // Auto-select type if not specified.
    if (IMSCSI_TYPE(CreateData->Fields.Flags) == 0)
        if (CreateData->Fields.FileNameLength == 0)
            CreateData->Fields.Flags |= IMSCSI_TYPE_VM;
        else
            CreateData->Fields.Flags |= IMSCSI_TYPE_FILE;

    // Blank filenames only supported for non-zero VM disks.
    if ((CreateData->Fields.FileNameLength == 0) &&
        !(((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_VM) &&
        (CreateData->Fields.DiskSize.QuadPart > 0)) ||
        ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE) &&
        (IMSCSI_FILE_TYPE(CreateData->Fields.Flags) == IMSCSI_FILE_TYPE_AWEALLOC) &&
        (CreateData->Fields.DiskSize.QuadPart > 0))))
    {
        DbgPrint("PhDskMnt: Blank filenames only supported for non-zero length "
            "vm type disks.\n");

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

    if (IMSCSI_WRITE_OVERLAY(CreateData->Fields.Flags) &&
        (!IMSCSI_READONLY(CreateData->Fields.Flags) ||
            CreateData->Fields.WriteOverlayFileNameLength == 0 ||
            IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_VM))
    {
        DbgPrint("PhDskMnt: Write overlay mode requires read-only image mounting and a write overlay image path.\n");

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
            L"Write overlay mode requires read-only image mounting and a write overlay image path."));

        return STATUS_INVALID_PARAMETER;
    }

    if (IMSCSI_BYTE_SWAP(CreateData->Fields.Flags))
    {
        KdPrint(("PhDskMnt: IMSCSI_OPTION_BYTE_SWAP not implemented.\n"));

        return STATUS_NOT_IMPLEMENTED;
    }

    // Cannot create >= 2 GB VM disk in 32 bit version.
#ifndef _WIN64
    if ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_VM) &
        ((CreateData->Fields.DiskSize.QuadPart & 0xFFFFFFFF80000000) !=
        0))
    {
        KdPrint(("PhDskMnt: Cannot create >= 2GB vm disks on 32-bit system.\n"));

        return STATUS_INVALID_PARAMETER;
    }
#endif

    file_name.Length = CreateData->Fields.FileNameLength;
    file_name.MaximumLength = CreateData->Fields.FileNameLength;
    file_name.Buffer = NULL;

    // If a file is to be opened or created, allocate name buffer and open that
    // file...
    if ((CreateData->Fields.FileNameLength > 0) ||
        ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE) &&
        (IMSCSI_FILE_TYPE(CreateData->Fields.Flags) == IMSCSI_FILE_TYPE_AWEALLOC)))
    {
        IO_STATUS_BLOCK io_status;
        OBJECT_ATTRIBUTES object_attributes;
        UNICODE_STRING real_file_name;
        ACCESS_MASK desired_access = 0;
        ULONG share_access = 0;
        ULONG create_options = 0;

        if (CreateData->Fields.FileNameLength > 0)
        {
            file_name.Buffer = (PWCHAR)ExAllocatePoolWithTag(NonPagedPool,
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

            RtlCopyMemory(file_name.Buffer, CreateData->Fields.FileName,
                CreateData->Fields.FileNameLength);
        }

        // If no device-type specified, check if filename ends with .iso or .nrg.
        // In that case, set device-type automatically to FILE_DEVICE_CDROM
        if ((IMSCSI_DEVICE_TYPE(CreateData->Fields.Flags) == 0) &&
            (CreateData->Fields.FileNameLength >= (4 * sizeof(*CreateData->Fields.FileName))))
        {
            LPWSTR name = CreateData->Fields.FileName +
        	(CreateData->Fields.FileNameLength / sizeof(*CreateData->Fields.FileName)) - 4;
            if ((_wcsnicmp(name, L".iso", 4) == 0) ||
        	(_wcsnicmp(name, L".bin", 4) == 0) ||
        	(_wcsnicmp(name, L".nrg", 4) == 0))
        	CreateData->Fields.Flags |= IMSCSI_DEVICE_TYPE_CD | IMSCSI_OPTION_RO;
        }

        if (IMSCSI_DEVICE_TYPE(CreateData->Fields.Flags) ==
            IMSCSI_DEVICE_TYPE_CD)
        {
            CreateData->Fields.Flags |= IMSCSI_OPTION_RO;
        }
        else if (IMSCSI_DEVICE_TYPE(CreateData->Fields.Flags) ==
            IMSCSI_DEVICE_TYPE_FD)
        {
            KdPrint(("PhDskMnt: IMSCSI_DEVICE_TYPE_FD not implemented.\n"));
        }

        KdPrint((
            "PhDskMnt: Done with device type auto-selection by file ext.\n"));

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
        }
        else
            KdPrint(("PhDskMnt: No impersonation information.\n"));

        if ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE) &&
            (IMSCSI_FILE_TYPE(CreateData->Fields.Flags) ==
            IMSCSI_FILE_TYPE_AWEALLOC))
        {
            real_file_name.MaximumLength = sizeof(AWEALLOC_DEVICE_NAME) +
                file_name.Length;

            real_file_name.Buffer = (PWCHAR)
                ExAllocatePoolWithTag(PagedPool,
                real_file_name.MaximumLength,
                MP_TAG_GENERAL);

            if (real_file_name.Buffer == NULL)
            {
                KdPrint(("ImDisk: Out of memory while allocating %#x bytes\n",
                    real_file_name.MaximumLength));

                if (file_name.Buffer != NULL)
                    ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                return STATUS_INSUFFICIENT_RESOURCES;
            }

            real_file_name.Length = 0;

            status =
                RtlAppendUnicodeToString(&real_file_name,
                AWEALLOC_DEVICE_NAME);

            if (NT_SUCCESS(status) && (file_name.Length > 0))
                status =
                RtlAppendUnicodeStringToString(&real_file_name,
                &file_name);

            if (!NT_SUCCESS(status))
            {
                KdPrint(("ImDisk: Internal error: "
                    "RtlAppendUnicodeStringToString failed with "
                    "pre-allocated buffers.\n"));

                if (file_name.Buffer != NULL)
                    ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                ExFreePoolWithTag(real_file_name.Buffer, MP_TAG_GENERAL);
                
                return STATUS_DRIVER_INTERNAL_ERROR;
            }

            InitializeObjectAttributes(&object_attributes,
                &real_file_name,
                OBJ_CASE_INSENSITIVE |
                OBJ_FORCE_ACCESS_CHECK,
                NULL,
                NULL);
        }
        else if ((IMSCSI_TYPE(CreateData->Fields.Flags) ==
            IMSCSI_TYPE_PROXY) &&
            ((IMSCSI_PROXY_TYPE(CreateData->Fields.Flags) ==
            IMSCSI_PROXY_TYPE_TCP) ||
            (IMSCSI_PROXY_TYPE(CreateData->Fields.Flags) ==
            IMSCSI_PROXY_TYPE_COMM)))
        {
            RtlInitUnicodeString(&real_file_name,
                IMDPROXY_SVC_PIPE_NATIVE_NAME);

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

        if ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_PROXY) &&
            (IMSCSI_PROXY_TYPE(CreateData->Fields.Flags) ==
            IMSCSI_PROXY_TYPE_SHM))
        {
            proxy.connection_type = PROXY_CONNECTION::PROXY_CONNECTION_SHM;

            status =
                ZwOpenSection(&file_handle,
                GENERIC_READ | GENERIC_WRITE,
                &object_attributes);
        }
        else
        {
            desired_access = GENERIC_READ;

            if ((IMSCSI_TYPE(CreateData->Fields.Flags) ==
                IMSCSI_TYPE_PROXY) ||
                ((IMSCSI_TYPE(CreateData->Fields.Flags) != IMSCSI_TYPE_VM) &&
                    !IMSCSI_READONLY(CreateData->Fields.Flags)))
            {
                desired_access |= GENERIC_WRITE;
            }

            share_access = FILE_SHARE_READ | FILE_SHARE_DELETE;

            if (IMSCSI_READONLY(CreateData->Fields.Flags) ||
                (IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_VM) ||
                IMSCSI_SHARED_IMAGE(CreateData->Fields.Flags))
            {
                share_access |= FILE_SHARE_WRITE;
            }

            create_options = FILE_NON_DIRECTORY_FILE |
                FILE_SYNCHRONOUS_IO_NONALERT;

            if (IMSCSI_FILE_TYPE(CreateData->Fields.Flags) != IMSCSI_FILE_TYPE_BUFFERED_IO)
                create_options |= FILE_NO_INTERMEDIATE_BUFFERING;

            if (IMSCSI_SPARSE_FILE(CreateData->Fields.Flags))
                create_options |= FILE_OPEN_FOR_BACKUP_INTENT;

            if (IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_PROXY)
                create_options |= FILE_SEQUENTIAL_ONLY;
            else
                create_options |= FILE_RANDOM_ACCESS;

            if (IMSCSI_SHARED_IMAGE(CreateData->Fields.Flags) &&
                !IMSCSI_READONLY(CreateData->Fields.Flags))
            {
                create_options |= FILE_WRITE_THROUGH;
            }

            KdPrint(("PhDskMnt::ImScsiCreateLU: Passing DesiredAccess=%#x ShareAccess=%#x CreateOptions=%#x\n",
                desired_access, share_access, create_options));

            status = ZwCreateFile(
                &file_handle,
                desired_access,
                &object_attributes,
                &io_status,
                NULL,
                FILE_ATTRIBUTE_NORMAL,
                share_access,
                FILE_OPEN,
                create_options,
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

            if ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_PROXY) &
                (IMSCSI_PROXY_TYPE(CreateData->Fields.Flags) == IMSCSI_PROXY_TYPE_SHM))
            {
                proxy.connection_type = PROXY_CONNECTION_SHM;

                status =
                    ZwOpenSection(&file_handle,
                    GENERIC_READ | GENERIC_WRITE,
                    &object_attributes);
            }
            else
            {
                status = ZwCreateFile(
                    &file_handle,
                    desired_access,
                    &object_attributes,
                    &io_status,
                    NULL,
                    FILE_ATTRIBUTE_NORMAL,
                    share_access,
                    FILE_OPEN,
                    create_options,
                    NULL,
                    0);
            }
        }
#endif

        if (!NT_SUCCESS(status))
        {
            KdPrint(("PhDskMnt: Error opening file '%.*ws'. Status: %#x SpecSize: %i WritableFile: %i DevTypeFile: %i Flags: %#x\n",
                (int)(real_file_name.Length / sizeof(WCHAR)),
                real_file_name.Buffer,
                status,
                CreateData->Fields.DiskSize.QuadPart != 0,
                !IMSCSI_READONLY(CreateData->Fields.Flags),
                IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE,
                CreateData->Fields.Flags));
        }

        // If not found we will create the file if a new non-zero size is
        // specified, read-only virtual disk is not specified and we are
        // creating a type 'file' virtual disk.
        if (((status == STATUS_OBJECT_NAME_NOT_FOUND) ||
            (status == STATUS_NO_SUCH_FILE)) &&
            (CreateData->Fields.DiskSize.QuadPart != 0) &&
            (!IMSCSI_READONLY(CreateData->Fields.Flags)) &&
            (IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE))
        {

            status = ZwCreateFile(
                &file_handle,
                GENERIC_READ |
                GENERIC_WRITE,
                &object_attributes,
                &io_status,
                NULL,
                FILE_ATTRIBUTE_NORMAL,
                share_access,
                FILE_OPEN_IF,
                create_options, NULL, 0);

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
                    (int)(CreateData->Fields.FileNameLength /
                    sizeof(*CreateData->Fields.FileName)),
                    CreateData->Fields.FileName,
                    status));

                if ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE) &&
                    (IMSCSI_FILE_TYPE(CreateData->Fields.Flags) == IMSCSI_FILE_TYPE_AWEALLOC))
                    ExFreePoolWithTag(real_file_name.Buffer, MP_TAG_GENERAL);

                if (file_name.Buffer != NULL)
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

            if ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE) &&
                (IMSCSI_FILE_TYPE(CreateData->Fields.Flags) == IMSCSI_FILE_TYPE_AWEALLOC))
                ExFreePoolWithTag(real_file_name.Buffer, MP_TAG_GENERAL);

            if (file_name.Buffer != NULL)
                ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

            return status;
        }

        KdPrint(("PhDskMnt: File '%.*ws' opened successfully.\n",
            (int)(real_file_name.Length / sizeof(WCHAR)),
            real_file_name.Buffer));

        if ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE) &&
            (IMSCSI_FILE_TYPE(CreateData->Fields.Flags) == IMSCSI_FILE_TYPE_AWEALLOC))
            ExFreePoolWithTag(real_file_name.Buffer, MP_TAG_GENERAL);

        if (IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_PROXY)
        {
            if (IMSCSI_PROXY_TYPE(CreateData->Fields.Flags) == IMSCSI_PROXY_TYPE_SHM)
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

                if (file_name.Buffer != NULL)
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

            KdPrint(("PhDskMnt: Got reference to proxy object %p.\n",
                proxy.connection_type == PROXY_CONNECTION::PROXY_CONNECTION_DEVICE ?
                (PVOID)proxy.device :
                (PVOID)proxy.shared_memory));

            if (IMSCSI_PROXY_TYPE(CreateData->Fields.Flags) != IMSCSI_PROXY_TYPE_DIRECT)
                status = ImScsiConnectProxy(&proxy,
                &io_status,
                NULL,
                CreateData->Fields.Flags,
                CreateData->Fields.FileName,
                CreateData->Fields.FileNameLength);

            if (!NT_SUCCESS(status))
            {
                ImScsiCloseProxy(&proxy);

                ZwClose(file_handle);

                if (file_name.Buffer != NULL)
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
        if (IMSCSI_TYPE(CreateData->Fields.Flags) != IMSCSI_TYPE_PROXY)
        {
            LARGE_INTEGER disk_size;

            status = ImScsiGetDiskSize(
                file_handle,
                &io_status,
                &disk_size);

            if (!NT_SUCCESS(status))
            {
                ZwClose(file_handle);

                if (file_name.Buffer != NULL)
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
                    L"Error getting image size."));

                KdPrint
                    (("PhDskMnt: Error getting image size (%#x).\n",
                    status));

                return status;
            }

            // Allocate virtual memory for 'vm' type.
            if (IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_VM)
            {
                SIZE_T max_size;

                // If no size given for VM disk, use size of pre-load image file.
                // This code is somewhat easier for 64 bit architectures.

#ifdef _WIN64
                if (CreateData->Fields.DiskSize.QuadPart == 0)
                {
                    CreateData->Fields.DiskSize.QuadPart =
                        disk_size.QuadPart -
                        CreateData->Fields.ImageOffset.QuadPart;
                }

                max_size = CreateData->Fields.DiskSize.QuadPart;
#else
                if (CreateData->Fields.DiskSize.QuadPart == 0)
                {
                    // Check that file size < 2 GB.
                    if ((disk_size.QuadPart -
                        CreateData->Fields.ImageOffset.QuadPart) &
                        0xFFFFFFFF80000000)
                    {
                        ZwClose(file_handle);

                        if (file_name.Buffer != NULL)
                            ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                        KdPrint(("PhDskMnt: VM disk >= 2GB not supported.\n"));

                        return STATUS_INSUFFICIENT_RESOURCES;
                    }
                    else
                    {
                        CreateData->Fields.DiskSize.QuadPart =
                            disk_size.QuadPart -
                            CreateData->Fields.ImageOffset.QuadPart;
                    }
                }

                max_size = CreateData->Fields.DiskSize.LowPart;
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

                    if (file_name.Buffer != NULL)
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

                    if (file_name.Buffer != NULL)
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
                if (IMSCSI_SPARSE_FILE(CreateData->Fields.Flags))
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

                if (CreateData->Fields.DiskSize.QuadPart == 0)
                    CreateData->Fields.DiskSize.QuadPart =
                    disk_size.QuadPart -
                    CreateData->Fields.ImageOffset.QuadPart;
                else if ((disk_size.QuadPart <
                    CreateData->Fields.DiskSize.QuadPart +
                    CreateData->Fields.ImageOffset.QuadPart) &&
                    (!IMSCSI_READONLY(CreateData->Fields.Flags)))
                {
                    LARGE_INTEGER new_image_size;
                    new_image_size.QuadPart =
                        CreateData->Fields.DiskSize.QuadPart +
                        CreateData->Fields.ImageOffset.QuadPart;

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

                        if (file_name.Buffer != NULL)
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

                if (file_name.Buffer != NULL)
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

            if (CreateData->Fields.DiskSize.QuadPart == 0)
                CreateData->Fields.DiskSize.QuadPart = proxy_info.file_size;

            if ((proxy_info.req_alignment - 1 > FILE_512_BYTE_ALIGNMENT) ||
                (CreateData->Fields.DiskSize.QuadPart == 0))
            {
                ImScsiCloseProxy(&proxy);
                ZwClose(file_handle);

                if (file_name.Buffer != NULL)
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
                    "Got 0x%I64x size and 0x%I64x alignment.\n",
                    proxy_info.file_size,
                    proxy_info.req_alignment));

                return STATUS_INVALID_PARAMETER;
            }

            alignment_requirement = (ULONG)proxy_info.req_alignment - 1;

            if (proxy_info.flags & IMDPROXY_FLAG_RO)
                CreateData->Fields.Flags |= IMSCSI_OPTION_RO;

            if (proxy_info.flags & IMDPROXY_FLAG_SUPPORTS_UNMAP)
                proxy_supports_unmap = TRUE;

            if (proxy_info.flags & IMDPROXY_FLAG_SUPPORTS_ZERO)
                proxy_supports_zero = TRUE;

            if ((proxy_info.flags & IMDPROXY_FLAG_SUPPORTS_SHARED) == 0)
                CreateData->Fields.Flags &= ~IMSCSI_OPTION_SHARED_IMAGE;

            KdPrint(("PhDskMnt: Got from proxy: Siz=0x%08x%08x Flg=%#x Alg=%#x.\n",
                CreateData->Fields.DiskSize.HighPart,
                CreateData->Fields.DiskSize.LowPart,
                (ULONG)proxy_info.flags,
                (ULONG)proxy_info.req_alignment));
        }

        if (CreateData->Fields.DiskSize.QuadPart == 0)
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
        max_size = CreateData->Fields.DiskSize.QuadPart;
#else
        max_size = CreateData->Fields.DiskSize.LowPart;
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
    if (IMSCSI_DEVICE_TYPE(CreateData->Fields.Flags) == 0)
    {
        CreateData->Fields.Flags |= IMSCSI_DEVICE_TYPE_HD;
    }

    KdPrint(("PhDskMnt: Done with device type selection for floppy sizes.\n"));

    // If some parts of the DISK_GEOMETRY structure are zero, auto-fill with
    // typical values for this type of disk.
    if (IMSCSI_DEVICE_TYPE(CreateData->Fields.Flags) == IMSCSI_DEVICE_TYPE_CD)
    {
        if (CreateData->Fields.BytesPerSector == 0)
            CreateData->Fields.BytesPerSector = DEFAULT_SECTOR_SIZE_CD_ROM;

        CreateData->Fields.Flags |= IMSCSI_OPTION_REMOVABLE | IMSCSI_OPTION_RO;
    }
    else
    {
        if (CreateData->Fields.BytesPerSector == 0)
            CreateData->Fields.BytesPerSector = DEFAULT_SECTOR_SIZE_HDD;
    }

    KdPrint(("PhDskMnt: Done with disk geometry setup.\n"));

    // Now build real DeviceType and DeviceCharacteristics parameters.
    switch (IMSCSI_DEVICE_TYPE(CreateData->Fields.Flags))
    {
    case IMSCSI_DEVICE_TYPE_CD:
        pLUExt->DeviceType = READ_ONLY_DIRECT_ACCESS_DEVICE;
        CreateData->Fields.Flags |= IMSCSI_OPTION_REMOVABLE;
        break;

    case IMSCSI_DEVICE_TYPE_RAW:
        pLUExt->DeviceType = ARRAY_CONTROLLER_DEVICE;
        break;

    default:
        pLUExt->DeviceType = DIRECT_ACCESS_DEVICE;
    }

    if (IMSCSI_READONLY(CreateData->Fields.Flags))
        pLUExt->ReadOnly = TRUE;

    if (IMSCSI_REMOVABLE(CreateData->Fields.Flags))
        pLUExt->RemovableMedia = TRUE;

    if (alignment_requirement > CreateData->Fields.BytesPerSector)
        CreateData->Fields.BytesPerSector = alignment_requirement + 1;

    KdPrint
        (("PhDskMnt: After checks and translations we got this create data:\n"
        "DeviceNumber   = %#x\n"
        "DiskSize       = %I64u\n"
        "ImageOffset    = %I64u\n"
        "SectorSize     = %u\n"
        "Flags          = %#x\n"
        "FileNameLength = %u\n"
        "FileName       = '%.*ws'\n",
        CreateData->Fields.DeviceNumber.LongNumber,
        CreateData->Fields.DiskSize.QuadPart,
        CreateData->Fields.ImageOffset.QuadPart,
        CreateData->Fields.BytesPerSector,
        CreateData->Fields.Flags,
        CreateData->Fields.FileNameLength,
        (int)(CreateData->Fields.FileNameLength / sizeof(*CreateData->Fields.FileName)),
        CreateData->Fields.FileName));

    pLUExt->ObjectName = file_name;

    pLUExt->DiskSize = CreateData->Fields.DiskSize;

    while (CreateData->Fields.BytesPerSector >>= 1)
        pLUExt->BlockPower++;
    if (pLUExt->BlockPower == 0)
        pLUExt->BlockPower = DEFAULT_BLOCK_POWER;
    CreateData->Fields.BytesPerSector = 1UL << pLUExt->BlockPower;

    pLUExt->ImageOffset = CreateData->Fields.ImageOffset;

    // VM disk.
    if (IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_VM)
        pLUExt->VMDisk = TRUE;
    else
        pLUExt->VMDisk = FALSE;

    // AWEAlloc disk.
    if ((IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE) &&
        (IMSCSI_FILE_TYPE(CreateData->Fields.Flags) == IMSCSI_FILE_TYPE_AWEALLOC))
        pLUExt->AWEAllocDisk = TRUE;
    else
        pLUExt->AWEAllocDisk = FALSE;

    pLUExt->ImageBuffer = image_buffer;
    pLUExt->ImageFile = file_handle;

    // Use proxy service.
    if (IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_PROXY)
    {
        pLUExt->Proxy = proxy;
        pLUExt->UseProxy = TRUE;
    }
    else
        pLUExt->UseProxy = FALSE;

    // If we are going to fake a disk signature, prepare that fake
    // disk sig here.
    if ((CreateData->Fields.Flags & IMSCSI_FAKE_DISK_SIG) != 0)
    {
        pLUExt->FakeDiskSignature =
            (RtlRandomEx(&pMPDrvInfoGlobal->RandomSeed) |
                0x80808081UL) & 0xFEFEFEFFUL;
    }

    if (IMSCSI_SPARSE_FILE(CreateData->Fields.Flags) ||
        pLUExt->UseProxy)
    {
        pLUExt->ProvisioningType = PROVISIONING_TYPE_THIN;
    }

    if ((pLUExt->FileObject == NULL) &&
        (!pLUExt->AWEAllocDisk) &&
        (!pLUExt->VMDisk) &&
        ((!pLUExt->UseProxy) ||
            proxy_supports_unmap))
    {
        pLUExt->SupportsUnmap = TRUE;
    }

    if ((pLUExt->FileObject == NULL) &&
        (!pLUExt->AWEAllocDisk) &&
        (!pLUExt->VMDisk) &&
        ((!pLUExt->UseProxy) ||
            proxy_supports_zero))
    {
        pLUExt->SupportsZero = TRUE;
    }

    // Image opened for shared writing
    if (IMSCSI_SHARED_IMAGE(CreateData->Fields.Flags))
    {
        pLUExt->SharedImage = TRUE;
    }
    else
    {
        pLUExt->SharedImage = FALSE;
    }

    ImScsiGenerateUniqueId(pLUExt);

    UNICODE_STRING guid;
    status = RtlStringFromGUID(*(PGUID)pLUExt->UniqueId, &guid);
    if (NT_SUCCESS(status))
    {
        ANSI_STRING ansi_guid = {
            0,
            sizeof(pLUExt->GuidString),
            pLUExt->GuidString
        };

        RtlUnicodeStringToAnsiString(&ansi_guid, &guid, FALSE);

        RtlFreeUnicodeString(&guid);

        DbgPrint("PhDskMnt::ImScsiInitializeLU: Unique id: %s\n", pLUExt->GuidString);
    }

    if (IMSCSI_WRITE_OVERLAY(CreateData->Fields.Flags) &&
        CreateData->Fields.WriteOverlayFileNameLength > 0)
    {
        UNICODE_STRING overlay_file_name;
        overlay_file_name.Buffer = (PWCHAR)(((PUCHAR)CreateData->Fields.FileName) +
            CreateData->Fields.FileNameLength);
        overlay_file_name.MaximumLength = overlay_file_name.Length =
            CreateData->Fields.WriteOverlayFileNameLength;

        OBJECT_ATTRIBUTES object_attributes;
        InitializeObjectAttributes(&object_attributes,
            &overlay_file_name,
            OBJ_CASE_INSENSITIVE |
            OBJ_FORCE_ACCESS_CHECK |
            OBJ_OPENIF,
            NULL,
            NULL);

        LARGE_INTEGER allocation_size;
        allocation_size.QuadPart = DIFF_BLOCK_SIZE;

        IO_STATUS_BLOCK io_status;

        HANDLE write_overlay;

        status = ZwCreateFile(
            &write_overlay,
            GENERIC_READ | GENERIC_WRITE | DELETE,
            &object_attributes,
            &io_status,
            &allocation_size,
            FILE_ATTRIBUTE_NORMAL,
            FILE_SHARE_READ | FILE_SHARE_DELETE,
            FILE_OPEN_IF,
            FILE_NON_DIRECTORY_FILE | FILE_RANDOM_ACCESS |
            FILE_SYNCHRONOUS_IO_NONALERT,
            NULL,
            0);

        if (!NT_SUCCESS(status))
        {
            DbgPrint("PhDskMnt::ImScsiCreateLU: Error creating write overlay image '%wZ': %#x\n",
                &overlay_file_name, status);
            
            return status;
        }

        pLUExt->WriteOverlay = write_overlay;
        
        pLUExt->WriteOverlayFileName.Buffer = (PWCHAR)ExAllocatePoolWithTag(
            NonPagedPool, overlay_file_name.Length, POOL_TAG);

        if (pLUExt->WriteOverlayFileName.Buffer == NULL)
        {
            return STATUS_INSUFFICIENT_RESOURCES;
        }

        pLUExt->WriteOverlayFileName.MaximumLength = overlay_file_name.Length;
        RtlCopyUnicodeString(&pLUExt->WriteOverlayFileName, &overlay_file_name);
    }

    KeInitializeSpinLock(&pLUExt->RequestListLock);
    InitializeListHead(&pLUExt->RequestList);
    KeInitializeEvent(&pLUExt->RequestEvent, SynchronizationEvent, FALSE);

    KeInitializeEvent(&pLUExt->Initialized, NotificationEvent, FALSE);

    KeInitializeSpinLock(&pLUExt->LastIoLock);

    KeSetEvent(&pLUExt->Initialized, (KPRIORITY)0, FALSE);

    KdPrint(("PhDskMnt::ImScsiInitializeLU: Creating worker thread for pLUExt=0x%p.\n",
        pLUExt));

    // Get FILE_OBJECT if we will need that later
    if ((file_handle != NULL) &&
        (IMSCSI_TYPE(CreateData->Fields.Flags) == IMSCSI_TYPE_FILE) &&
        ((IMSCSI_FILE_TYPE(CreateData->Fields.Flags) == IMSCSI_FILE_TYPE_AWEALLOC) ||
        (IMSCSI_FILE_TYPE(CreateData->Fields.Flags) == IMSCSI_FILE_TYPE_PARALLEL_IO)))
    {
        status = ObReferenceObjectByHandle(file_handle,
            SYNCHRONIZE | FILE_READ_ATTRIBUTES | FILE_READ_DATA |
            (pLUExt->ReadOnly ?
            0 : FILE_WRITE_DATA | FILE_WRITE_ATTRIBUTES),
            *IoFileObjectType,
            KernelMode, (PVOID*)&pLUExt->FileObject, NULL);

        if (!NT_SUCCESS(status))
        {
            pLUExt->FileObject = NULL;

            DbgPrint("PhDskMnt::ImScsiCreateLU: Error referencing image file handle: %#x\n",
                status);
        }
    }

    status = PsCreateSystemThread(
        &thread_handle,
        (ACCESS_MASK)0L,
        NULL,
        NULL,
        NULL,
        ImScsiWorkerThread,
        pLUExt);

    if (!NT_SUCCESS(status))
    {
        DbgPrint("PhDskMnt::ImScsiCreateLU: Cannot create device worker thread. (%#x)\n", status);

        return status;
    }

    KeWaitForSingleObject(
        &pLUExt->Initialized,
        Executive,
        KernelMode,
        FALSE,
        NULL);

    status = ObReferenceObjectByHandle(
        thread_handle,
        FILE_READ_ATTRIBUTES | SYNCHRONIZE,
        *PsThreadType,
        KernelMode,
        (PVOID*)&pLUExt->WorkerThread,
        NULL
        );

    if (!NT_SUCCESS(status))
    {
        pLUExt->WorkerThread = NULL;

        DbgPrint("PhDskMnt::ImScsiCreateLU: Cannot reference device worker thread. (%#x)\n", status);
        KeSetEvent(&pLUExt->StopThread, (KPRIORITY)0, FALSE);
        ZwWaitForSingleObject(thread_handle, FALSE, NULL);
    }
    else
    {
        KdPrint(("PhDskMnt::ImScsiCreateLU: Device created and ready.\n"));
    }

    ZwClose(thread_handle);

    return status;
}


BOOLEAN
ImScsiFillMemoryDisk(pHW_LU_EXTENSION pLUExt)
{
    LARGE_INTEGER byte_offset = pLUExt->ImageOffset;
    IO_STATUS_BLOCK io_status;
    NTSTATUS status;
#ifdef _WIN64
    SIZE_T disk_size = pLUExt->DiskSize.QuadPart;
#else
    SIZE_T disk_size = pLUExt->DiskSize.LowPart;
#endif

    KdPrint(("PhDskMnt: Reading image file into vm disk buffer.\n"));

    status =
        ImScsiSafeReadFile(
        pLUExt->ImageFile,
        &io_status,
        pLUExt->ImageBuffer,
        disk_size,
        &byte_offset);

    ZwClose(pLUExt->ImageFile);
    pLUExt->ImageFile = NULL;

    // Failure to read pre-load image is now considered a fatal error
    if (!NT_SUCCESS(status))
    {
        KdPrint(("PhDskMnt: Failed to read image file (%#x).\n", status));

        return FALSE;
    }

    KdPrint(("PhDskMnt: Image loaded successfully.\n"));

    return TRUE;
}
