
/// proxy.c
/// SCSI support routines, called from miniport callback functions, normally
/// at DISPATCH_LEVEL. This includes responding to control requests and
/// queueing work items for requests that need to be carried out at
/// PASSIVE_LEVEL.
/// 
/// Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#define MPScsiFile     "2.025"

#include "phdskmnt.h"

#include <Ntddcdrm.h>

#define TOC_DATA_TRACK                   0x04

#pragma warning(push)
#pragma warning(disable : 4204)                       /* Prevent C4204 messages from stortrce.h. */
#include <stortrce.h>
#pragma warning(pop)

#include "trace.h"
//#include "scsi.tmh"

#ifdef USE_SCSIPORT
/**************************************************************************************************/     
/*                                                                                                */     
/**************************************************************************************************/     
VOID
ScsiExecuteRaidControllerUnit(
            __in pHW_HBA_EXT          pHBAExt,    // Adapter device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb,
            __in PUCHAR               pResult
            )
{
    KdPrint2(("PhDskMnt::ScsiExecuteRaidControllerUnit: pSrb = 0x%p, CDB = 0x%X Path: %x TID: %x Lun: %x\n",
                      pSrb, pSrb->Cdb[0], pSrb->PathId, pSrb->TargetId, pSrb->Lun));

    *pResult = ResultDone;

    switch (pSrb->Cdb[0])
    {
    case SCSIOP_TEST_UNIT_READY:
    case SCSIOP_SYNCHRONIZE_CACHE:
    case SCSIOP_START_STOP_UNIT:
    case SCSIOP_VERIFY:
        ScsiSetSuccess(pSrb, 0);
        break;

    case SCSIOP_INQUIRY:
        ScsiOpInquiryRaidControllerUnit(pHBAExt, pSrb);
        break;

    case SCSIOP_REPORT_LUNS:
        ScsiOpReportLuns(pHBAExt, pSrb);
        break;

    default:
        KdPrint(("PhDskMnt::ScsiExecuteRaidControllerUnit: Unknown opcode=0x%X\n", (int)pSrb->Cdb[0]));
        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_INVALID_CDB, 0);
        break;
    }
}

VOID
ScsiOpInquiryRaidControllerUnit(
              __in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
              __in PSCSI_REQUEST_BLOCK  pSrb
             )
{
    PINQUIRYDATA          pInqData = (PINQUIRYDATA)pSrb->DataBuffer;// Point to Inquiry buffer.
    PCDB                  pCdb;

    KdPrint2(("PhDskMnt::ScsiOpInquiryRaidControllerUnit:  pHBAExt = 0x%p, pSrb=0x%p\n", pHBAExt, pSrb));

    RtlZeroMemory((PUCHAR)pSrb->DataBuffer, pSrb->DataTransferLength);

    pCdb = (PCDB)pSrb->Cdb;

    if (pCdb->CDB6INQUIRY3.EnableVitalProductData == 1)
    {
        KdPrint(("PhDskMnt::ScsiOpInquiry: Received VPD request for page 0x%X\n", pCdb->CDB6INQUIRY.PageCode));

        // Current implementation of ScsiOpVPDRaidControllerUnit seems somewhat dangerous and could cause buffer
        // overruns. For now, just skip Vital Product Data requests.
#if 1
        ScsiOpVPDRaidControllerUnit(pHBAExt, pSrb);
#else
        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_INVALID_CDB, 0);
#endif

        goto done;
    }

    pInqData->DeviceType = ARRAY_CONTROLLER_DEVICE;

    pInqData->CommandQueue = TRUE;

    RtlMoveMemory(pInqData->VendorId, pHBAExt->VendorId, 8);
    RtlMoveMemory(pInqData->ProductId, pHBAExt->ProductId, 16);
    RtlMoveMemory(pInqData->ProductRevisionLevel, pHBAExt->ProductRevision, 4);

    ScsiSetSuccess(pSrb, sizeof(INQUIRYDATA));

done:
    KdPrint2(("PhDskMnt::ScsiOpInquiry: End: status=0x%X\n", (int)pSrb->SrbStatus));

    return;
}                                                     // End ScsiOpInquiry.
#endif

/**************************************************************************************************/     
/*                                                                                                */     
/**************************************************************************************************/     
VOID
ScsiExecute(
            __in pHW_HBA_EXT          pHBAExt,    // Adapter device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb,
            __in PUCHAR               pResult
            )
{
    pHW_LU_EXTENSION  pLUExt = NULL;
    UCHAR             status;

    KdPrint2(("PhDskMnt::ScsiExecute: pSrb = 0x%p, CDB = 0x%X Path: %x TID: %x Lun: %x\n",
                      pSrb, pSrb->Cdb[0], pSrb->PathId, pSrb->TargetId, pSrb->Lun));

#ifdef USE_SCSIPORT
    // In case of SCSIPORT (Win XP), we need to show at least one working device connected to this
    // adapter. Otherwise, SCSIPORT may not even let control requests through, which could cause
    // a scenario where even new devices cannot be added. So, for device path 0:0:0, always answer
    // 'success' (without actual response data) to some basic requests.
    if ((pSrb->PathId|pSrb->TargetId|pSrb->Lun) == 0)
    {
        ScsiExecuteRaidControllerUnit(pHBAExt, pSrb, pResult);
        goto Done;
    }
#endif

    *pResult = ResultDone;

    // Get the LU extension from port driver.
    status = ScsiGetLUExtension(pHBAExt, &pLUExt, pSrb->PathId, pSrb->TargetId, pSrb->Lun);

    // Set SCSI check conditions if LU is not ready
    if (status == SRB_STATUS_INVALID_LUN)
    {
        ScsiSetCheckCondition(
            pSrb,
            SRB_STATUS_INVALID_LUN,
            SCSI_SENSE_ILLEGAL_REQUEST,
            SCSI_ADSENSE_INVALID_LUN,
            SCSI_SENSEQ_CAUSE_NOT_REPORTABLE
            );
    }
    else if (status == SRB_STATUS_NOT_POWERED)
    {
        ScsiSetCheckCondition(
            pSrb,
            SRB_STATUS_NOT_POWERED,
            SCSI_SENSE_NOT_READY,
            SCSI_ADSENSE_LUN_NOT_READY,
            SCSI_SENSEQ_BECOMING_READY
            );
    }
    else if (status != SRB_STATUS_SUCCESS)
        ScsiSetError(pSrb, status);

    if (pLUExt == NULL)
    {
        KdPrint2(("PhDskMnt::ScsiExecute: No LUN object yet for device %d:%d:%d\n", pSrb->PathId, pSrb->TargetId, pSrb->Lun));

        goto Done;
    }

    // Handle sufficient opcodes to support a LUN suitable for a file system. Other opcodes are failed.

    switch (pSrb->Cdb[0])
    {
    case SCSIOP_TEST_UNIT_READY:
    case SCSIOP_SYNCHRONIZE_CACHE:
    case SCSIOP_SYNCHRONIZE_CACHE16:
    case SCSIOP_START_STOP_UNIT:
    case SCSIOP_VERIFY:
    case SCSIOP_VERIFY16:
        ScsiSetSuccess(pSrb, 0);
        break;

    case SCSIOP_INQUIRY:
        ScsiOpInquiry(pHBAExt, pLUExt, pSrb);
        break;
    
    case SCSIOP_READ_CAPACITY:
    case SCSIOP_READ_CAPACITY16:
        ScsiOpReadCapacity(pHBAExt, pLUExt, pSrb);
        break;

    case SCSIOP_READ:
    case SCSIOP_READ16:
    case SCSIOP_WRITE:
    case SCSIOP_WRITE16:
        ScsiOpReadWrite(pHBAExt, pLUExt, pSrb, pResult);
        break;

    case SCSIOP_READ_TOC:
        ScsiOpReadTOC(pHBAExt, pLUExt, pSrb);
        break;

    case SCSIOP_MEDIUM_REMOVAL:
        ScsiOpMediumRemoval(pHBAExt, pLUExt, pSrb);
        break;

    case SCSIOP_MODE_SENSE:
        ScsiOpModeSense(pHBAExt, pLUExt, pSrb);
        break;

    case SCSIOP_MODE_SENSE10:
        ScsiOpModeSense10(pHBAExt, pLUExt, pSrb);
        break;

    case SCSIOP_REPORT_LUNS:
        ScsiOpReportLuns(pHBAExt, pSrb);
        break;

    default:
        KdPrint(("PhDskMnt::ScsiExecute: Unknown opcode=0x%X\n", (int)pSrb->Cdb[0]));
        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_INVALID_CDB, 0);
        break;

    } // switch (pSrb->Cdb[0])

Done:
    KdPrint2(("PhDskMnt::ScsiExecute: End: status=0x%X, *pResult=%i\n", (int)pSrb->SrbStatus, (INT)*pResult));

    return;
}                                                     // End ScsiExecute.

VOID
ScsiOpMediumRemoval(__in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
                    __in pHW_LU_EXTENSION     device_extension,       // LUN device-object extension from port driver.
                    __in PSCSI_REQUEST_BLOCK  pSrb
                    )
{
    UNREFERENCED_PARAMETER(pHBAExt);
    UNREFERENCED_PARAMETER(device_extension);

    KdPrint(("ImDisk: ScsiOMediumRemoval for device %i:%i:%i.\n",
        (int)device_extension->DeviceNumber.PathId,
        (int)device_extension->DeviceNumber.TargetId,
        (int)device_extension->DeviceNumber.Lun));

    if (device_extension->DeviceType != READ_ONLY_DIRECT_ACCESS_DEVICE)
    {
        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_INVALID_CDB, 0);
        return;
    }

    RtlZeroMemory(pSrb->DataBuffer, pSrb->DataTransferLength);

    ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
}

VOID
ScsiOpReadTOC(__in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
              __in pHW_LU_EXTENSION     device_extension,       // LUN device-object extension from port driver.
              __in PSCSI_REQUEST_BLOCK  pSrb
             )
{
    PUCHAR data_buffer = (PUCHAR) pSrb->DataBuffer;
    PCDB   cdb         = (PCDB)   pSrb->Cdb;

    UNREFERENCED_PARAMETER(pHBAExt);

    KdPrint(("ImDisk: ScsiOpReadTOC for device %i:%i:%i.\n",
        (int)device_extension->DeviceNumber.PathId,
        (int)device_extension->DeviceNumber.TargetId,
        (int)device_extension->DeviceNumber.Lun));

    if (device_extension->DeviceType != READ_ONLY_DIRECT_ACCESS_DEVICE)
    {
        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_INVALID_CDB, 0);
        return;
    }

/*
#define READ_TOC_FORMAT_TOC         0x00
#define READ_TOC_FORMAT_SESSION     0x01
#define READ_TOC_FORMAT_FULL_TOC    0x02
#define READ_TOC_FORMAT_PMA         0x03
#define READ_TOC_FORMAT_ATIP        0x04
*/

    KdPrint2(("PhDskMnt::ScsiOpReadTOC: Msf = %d\n", cdb->READ_TOC.Msf));
    KdPrint2(("PhDskMnt::ScsiOpReadTOC: LogicalUnitNumber = %d\n", cdb->READ_TOC.LogicalUnitNumber));
    KdPrint2(("PhDskMnt::ScsiOpReadTOC: Format2 = %d\n", cdb->READ_TOC.Format2));
    KdPrint2(("PhDskMnt::ScsiOpReadTOC: StartingTrack = %d\n", cdb->READ_TOC.StartingTrack));
    KdPrint2(("PhDskMnt::ScsiOpReadTOC: AllocationLength = %d\n", (cdb->READ_TOC.AllocationLength[0] << 8) | cdb->READ_TOC.AllocationLength[1]));
    KdPrint2(("PhDskMnt::ScsiOpReadTOC: Control = %d\n", cdb->READ_TOC.Control));
    KdPrint2(("PhDskMnt::ScsiOpReadTOC: Format = %d\n", cdb->READ_TOC.Format));

    switch (cdb->READ_TOC.Format2)
    {
    case READ_TOC_FORMAT_TOC:
        if (pSrb->DataTransferLength < 12)
        {
            ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
            break;
        }

        data_buffer[0] = 0; // length MSB
        data_buffer[1] = 10; // length LSB
        data_buffer[2] = 1; // First Track
        data_buffer[3] = 1; // Last Track
        data_buffer[4] = 0; // Reserved
        data_buffer[5] = 0x14; // current position data + uninterrupted data
        data_buffer[6] = 1; // last complete track
        data_buffer[7] = 0; // reserved
        data_buffer[8] = 0; // MSB Block
        data_buffer[9] = 0;
        data_buffer[10] = 0;
        data_buffer[11] = 0; // LSB Block
        ScsiSetSuccess(pSrb, 12);
        break;

    case READ_TOC_FORMAT_SESSION:
    case READ_TOC_FORMAT_FULL_TOC:
    case READ_TOC_FORMAT_PMA:
    case READ_TOC_FORMAT_ATIP:
        ScsiSetError(pSrb, SRB_STATUS_ERROR);
        break;

    default:
        ScsiSetError(pSrb, SRB_STATUS_ERROR);
        break;
    }
}

/**************************************************************************************************/     
/*                                                                                                */     
/**************************************************************************************************/     
VOID
ScsiOpInquiry(
              __in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
              __in pHW_LU_EXTENSION     pLUExt,       // LUN device-object extension from port driver.
              __in PSCSI_REQUEST_BLOCK  pSrb
             )
{
    PINQUIRYDATA          pInqData = (PINQUIRYDATA)pSrb->DataBuffer;// Point to Inquiry buffer.
    PCDB                  pCdb;

    KdPrint2(("PhDskMnt::ScsiOpInquiry:  pHBAExt = 0x%p, pLUExt=0x%p, pSrb=0x%p\n", pHBAExt, pLUExt, pSrb));

    RtlZeroMemory((PUCHAR)pSrb->DataBuffer, pSrb->DataTransferLength);

    pCdb = (PCDB)pSrb->Cdb;

    if (pCdb->CDB6INQUIRY3.EnableVitalProductData == 1)
    {
        KdPrint(("PhDskMnt::ScsiOpInquiry: Received VPD request for page 0x%X\n", pCdb->CDB6INQUIRY.PageCode));

        // Current implementation of ScsiOpVPDRaidControllerUnit seems somewhat dangerous and could cause buffer
        // overruns. For now, just skip Vital Product Data requests.
#if 0
        ScsiOpVPDRaidControllerUnit(pHBAExt, pLUExt, pSrb);
#else
        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_INVALID_CDB, 0);
#endif

        goto done;
    }

    pInqData->DeviceType = pLUExt->DeviceType;
    pInqData->RemovableMedia = pLUExt->RemovableMedia;

    pInqData->CommandQueue = TRUE;

    RtlMoveMemory(pInqData->VendorId, pHBAExt->VendorId, 8);
    RtlMoveMemory(pInqData->ProductId, pHBAExt->ProductId, 16);
    RtlMoveMemory(pInqData->ProductRevisionLevel, pHBAExt->ProductRevision, 4);

    ScsiSetSuccess(pSrb, sizeof(INQUIRYDATA));

done:
    KdPrint2(("PhDskMnt::ScsiOpInquiry: End: status=0x%X\n", (int)pSrb->SrbStatus));

    return;
}                                                     // End ScsiOpInquiry.

/**************************************************************************************************/     
/*                                                                                                */     
/**************************************************************************************************/     
UCHAR
ScsiGetLUExtension(
              __in pHW_HBA_EXT								pHBAExt,      // Adapter device-object extension from port driver.
              pHW_LU_EXTENSION * ppLUExt,
              __in UCHAR									PathId,
              __in UCHAR									TargetId,
              __in UCHAR									Lun
             )
{
    PLIST_ENTRY           list_ptr;
    pHW_LU_EXTENSION *    pPortLunExt = NULL;
    UCHAR                 status;
#if defined(_AMD64_)
    KLOCK_QUEUE_HANDLE    LockHandle;
#else
    KIRQL                 SaveIrql;
#endif

    *ppLUExt = NULL;

    KdPrint2(("PhDskMnt::ScsiGetLUExtension: %d:%d:%d\n", PathId, TargetId, Lun));

#if defined(_AMD64_)
    KeAcquireInStackQueuedSpinLock(                   // Serialize the linked list of LUN extensions.              
                                   &pHBAExt->LUListLock, &LockHandle);
#else
    KeAcquireSpinLock(&pHBAExt->LUListLock, &SaveIrql);
#endif

    pPortLunExt = (pHW_LU_EXTENSION*) StoragePortGetLogicalUnit(pHBAExt, PathId, TargetId, Lun);

    if (pPortLunExt == NULL)
    {
        KdPrint(("PhDskMnt::ScsiGetLUExtension: StoragePortGetLogicalUnit failed for %d:%d:%d\n",
            PathId,
            TargetId,
            Lun));

        status = SRB_STATUS_INVALID_LUN;
        goto done;
    }

    if (*pPortLunExt != NULL)
    {
        pHW_LU_EXTENSION pLUExt = *pPortLunExt;

        if ((pLUExt->DeviceNumber.PathId != PathId) |
            (pLUExt->DeviceNumber.TargetId != TargetId) |
            (pLUExt->DeviceNumber.Lun != Lun))
        {
            DbgPrint("PhDskMnt::ScsiGetLUExtension: LUExt for device %i:%i:%i returned!\n",
                (int)pLUExt->DeviceNumber.PathId,
                (int)pLUExt->DeviceNumber.TargetId,
                (int)pLUExt->DeviceNumber.Lun);
        }
        else if (KeReadStateEvent(&pLUExt->StopThread))
        {
            DbgPrint("PhDskMnt::ScsiGetLUExtension: Warning: Device is stopped!\n");

            KeSetEvent(&pLUExt->Missing, (KPRIORITY) 0, FALSE);

            status = SRB_STATUS_INVALID_LUN;

            goto done;
        }
        else
        {
            if (!pLUExt->Initialized)
                DbgPrint("PhDskMnt::ScsiGetLUExtension: Warning: Device is not yet initialized!\n");
            
            *ppLUExt = pLUExt;

            KdPrint2(("PhDskMnt::ScsiGetLUExtension: Device %d:%d:%d has pLUExt=0x%p\n", PathId, TargetId, Lun, *ppLUExt));

            status = SRB_STATUS_SUCCESS;

            goto done;
        }
    }

    for (list_ptr = pHBAExt->LUList.Flink;
        list_ptr != &pHBAExt->LUList;
        list_ptr = list_ptr->Flink
        )
    {
        pHW_LU_EXTENSION object;
        object = CONTAINING_RECORD(list_ptr, HW_LU_EXTENSION, List);

        if ((object->DeviceNumber.PathId == PathId) &
            (object->DeviceNumber.TargetId == TargetId) &
            (object->DeviceNumber.Lun == Lun))
        {
            *ppLUExt = object;
            break;
        }
    }

    if (*ppLUExt == NULL)
    {
        KdPrint2(("PhDskMnt::ScsiGetLUExtension: No saved data for Lun.\n"));

        status = SRB_STATUS_INVALID_LUN;

        goto done;
    }

    *pPortLunExt = *ppLUExt;

    status = SRB_STATUS_SUCCESS;

    KdPrint(("PhDskMnt::ScsiGetLUExtension: Device %d:%d:%d get pLUExt=0x%p\n", PathId, TargetId, Lun, *ppLUExt));

done:

#if defined(_AMD64_)
    KeReleaseInStackQueuedSpinLock(&LockHandle);      
#else
    KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

    KdPrint2(("PhDskMnt::ScsiGetLUExtension: End: status=0x%X\n", (int)status));

    return status;
}                                                     // End ScsiOpInquiry.

/**************************************************************************************************/     
/*                                                                                                */     
/**************************************************************************************************/     
#ifdef USE_SCSIPORT
VOID
ScsiOpVPDRaidControllerUnit(
          __in pHW_HBA_EXT          pHBAExt,          // Adapter device-object extension from port driver.
          __in PSCSI_REQUEST_BLOCK  pSrb
         )
{
    struct _CDB6INQUIRY3 * pVpdInquiry = (struct _CDB6INQUIRY3 *)&pSrb->Cdb;

    UNREFERENCED_PARAMETER(pHBAExt);

    ASSERT(pSrb->DataTransferLength>0);

    KdPrint(("PhDskMnt::ScsiOpVPDRaidControllerUnit:  pHBAExt = 0x%p, pSrb=0x%p\n", pHBAExt, pSrb));

    if (pSrb->DataTransferLength == 0)
    {
        DbgPrint("PhDskMnt::ScsiOpVPDRaidControllerUnit: pSrb->DataTransferLength = 0\n");

        ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
        return;
    }

    RtlZeroMemory((PUCHAR)pSrb->DataBuffer,           // Clear output buffer.
                  pSrb->DataTransferLength);

    switch (pVpdInquiry->PageCode)
    {
    case VPD_SUPPORTED_PAGES:
        { // Inquiry for supported pages?
            PVPD_SUPPORTED_PAGES_PAGE pSupportedPages;
            ULONG len;

            len = sizeof(VPD_SUPPORTED_PAGES_PAGE) + 8;

            if (pSrb->DataTransferLength < len)
            {
                ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
                return;
            }

            pSupportedPages = (PVPD_SUPPORTED_PAGES_PAGE) pSrb->DataBuffer;             // Point to output buffer.

            pSupportedPages->DeviceType = ARRAY_CONTROLLER_DEVICE;
            pSupportedPages->DeviceTypeQualifier = 0;
            pSupportedPages->PageCode = VPD_SERIAL_NUMBER;
            pSupportedPages->PageLength = 8;                // Enough space for 4 VPD values.
            pSupportedPages->SupportedPageList[0] =         // Show page 0x80 supported.
                VPD_SERIAL_NUMBER;
            pSupportedPages->SupportedPageList[1] =         // Show page 0x83 supported.
                VPD_DEVICE_IDENTIFIERS;

            ScsiSetSuccess(pSrb, len);
        }
        break;

    case VPD_SERIAL_NUMBER:
        {   // Inquiry for serial number?
            PVPD_SERIAL_NUMBER_PAGE pVpd;
            ULONG len;
            
            len = sizeof(VPD_SERIAL_NUMBER_PAGE) + 8 + 32;
            if (pSrb->DataTransferLength < len)
            {
                ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
                return;
            }

            pVpd = (PVPD_SERIAL_NUMBER_PAGE) pSrb->DataBuffer;                        // Point to output buffer.

            pVpd->DeviceType = ARRAY_CONTROLLER_DEVICE;
            pVpd->DeviceTypeQualifier = 0;
            pVpd->PageCode = VPD_SERIAL_NUMBER;                
            pVpd->PageLength = 8 + 32;

            /* Generate a changing serial number. */
            //sprintf((char *)pVpd->SerialNumber, "%03d%02d%02d%03d0123456789abcdefghijABCDEFGH\n", 
            //    pMPDrvInfoGlobal->DrvInfoNbrMPHBAObj, pLUExt->DeviceNumber.PathId, pLUExt->DeviceNumber.TargetId, pLUExt->DeviceNumber.Lun);

            KdPrint(("PhDskMnt::ScsiOpVPDRaidControllerUnit:  VPD Page: %X Serial No.: %s",
                (int)pVpd->PageCode, (const char *)pVpd->SerialNumber));

            ScsiSetSuccess(pSrb, len);
        }
        break;

    case VPD_DEVICE_IDENTIFIERS:
        { // Inquiry for device ids?
            PVPD_IDENTIFICATION_PAGE pVpid;
            PVPD_IDENTIFICATION_DESCRIPTOR pVpidDesc;
            ULONG len;

#define VPIDNameSize 32
#define VPIDName     "PSSLUNxxx"

            len = sizeof(VPD_IDENTIFICATION_PAGE) + sizeof(VPD_IDENTIFICATION_DESCRIPTOR) + VPIDNameSize;

            if (pSrb->DataTransferLength < len)
            {
                ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
                return;
            }

            pVpid = (PVPD_IDENTIFICATION_PAGE) pSrb->DataBuffer;                     // Point to output buffer.

            pVpid->PageCode = VPD_DEVICE_IDENTIFIERS;

            pVpidDesc =                                   // Point to first (and only) descriptor.
                (PVPD_IDENTIFICATION_DESCRIPTOR)pVpid->Descriptors;

            pVpidDesc->CodeSet = VpdCodeSetAscii;         // Identifier contains ASCII.
            pVpidDesc->IdentifierType =                   // 
                VpdIdentifierTypeFCPHName;

            /* Generate a changing serial number. */
            _snprintf((char *)pVpidDesc->Identifier, pVpidDesc->IdentifierLength,
                "%03d%02d%02d%03d0123456789abcdefgh\n", pMPDrvInfoGlobal->DrvInfoNbrMPHBAObj,
                pSrb->PathId, pSrb->TargetId, pSrb->Lun);

            pVpidDesc->IdentifierLength =                 // Size of Identifier.
                (UCHAR)strlen((const char *)pVpidDesc->Identifier) - 1;
            pVpid->PageLength =                           // Show length of remainder.
                (UCHAR)(FIELD_OFFSET(VPD_IDENTIFICATION_PAGE, Descriptors) + 
                FIELD_OFFSET(VPD_IDENTIFICATION_DESCRIPTOR, Identifier) + 
                pVpidDesc->IdentifierLength);

            KdPrint(("PhDskMnt::ScsiOpVPDRaidControllerUnit:  VPD Page 0x83. Identifier=%.*s\n",
                pVpidDesc->IdentifierLength, pVpidDesc->Identifier));

            ScsiSetSuccess(pSrb, len);
        }
        break;

    default:
        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_INVALID_CDB, 0);
    }

    KdPrint2(("PhDskMnt::ScsiOpVPDRaidControllerUnit:  End: status=0x%X\n", (int)pSrb->SrbStatus));

    return;
}                                                     // End ScsiOpVPDRaidControllerUnit().
#endif

/**************************************************************************************************/     
/*                                                                                                */     
/**************************************************************************************************/     
VOID
ScsiOpReadCapacity(
                   __in pHW_HBA_EXT          pHBAExt, // Adapter device-object extension from port driver.
                   __in pHW_LU_EXTENSION     pLUExt,  // LUN device-object extension from port driver.
                   __in PSCSI_REQUEST_BLOCK  pSrb
                  )
{
    PREAD_CAPACITY_DATA     readCapacity = (PREAD_CAPACITY_DATA) pSrb->DataBuffer;
    PREAD_CAPACITY_DATA_EX  readCapacity16 = (PREAD_CAPACITY_DATA_EX) pSrb->DataBuffer;
    ULARGE_INTEGER          maxBlocks;
    ULONG                   blockSize;

    UNREFERENCED_PARAMETER(pHBAExt);

    KdPrint2(("PhDskMnt::ScsiOpReadCapacity:  pHBAExt = 0x%p, pLUExt=0x%p, pSrb=0x%p, Action=0x%X\n", pHBAExt, pLUExt, pSrb, (int)pSrb->Cdb[0]));

    RtlZeroMemory((PUCHAR)pSrb->DataBuffer, pSrb->DataTransferLength );

    //if ((pLUExt == NULL) ? TRUE : ((pLUExt->DiskSize.QuadPart == 0) | (!pLUExt->Initialized)))
    //{
    //    KdPrint(("PhDskMnt::ScsiOpReadWrite: Rejected. Device not initialized.\n"));

    //    ScsiSetCheckCondition(
    //        pSrb,
    //        SRB_STATUS_NOT_POWERED,
    //        SCSI_SENSE_NOT_READY,
    //        SCSI_ADSENSE_LUN_NOT_READY,
    //        SCSI_SENSEQ_BECOMING_READY);

    //    return;
    //}

    blockSize = 1UL << pLUExt->BlockPower;

    KdPrint2(("PhDskMnt::ScsiOpReadCapacity: Block Size: 0x%X\n", blockSize));

    if (pLUExt->DiskSize.QuadPart > 0)
        maxBlocks.QuadPart = (pLUExt->DiskSize.QuadPart >> pLUExt->BlockPower) - 1;
    else
        maxBlocks.QuadPart = 0;

    if (pSrb->Cdb[0] == SCSIOP_READ_CAPACITY)
        if (maxBlocks.QuadPart > MAXULONG)
            maxBlocks.QuadPart = MAXULONG;

    KdPrint2(("PhDskMnt::ScsiOpReadCapacity: Max Blocks: 0x%I64X\n", maxBlocks));
    
    if (pSrb->Cdb[0] == SCSIOP_READ_CAPACITY)
    {
        REVERSE_BYTES(&readCapacity->BytesPerBlock, &blockSize);
        REVERSE_BYTES(&readCapacity->LogicalBlockAddress, &maxBlocks.LowPart);
    }
    else if (pSrb->Cdb[0] == SCSIOP_READ_CAPACITY16)
    {
        REVERSE_BYTES(&readCapacity16->BytesPerBlock, &blockSize);
        REVERSE_BYTES_QUAD(&readCapacity16->LogicalBlockAddress, &maxBlocks);
    }

    KdPrint2(("PhDskMnt::ScsiOpReadCapacity:  End.\n"));

    ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
    return;
}                                                     // End ScsiOpReadCapacity.

/******************************************************************************************************/     
/*                                                                                                    */     
/* This routine does the setup for reading or writing. Thread ImScsiDispatchWork is going to be the   */     
/* place to do the work since it gets control at PASSIVE_LEVEL and so could do real I/O, could        */     
/* wait, etc, etc.                                                                                    */     
/*                                                                                                    */     
/******************************************************************************************************/     
VOID
ScsiOpReadWrite(
                __in pHW_HBA_EXT          pHBAExt, // Adapter device-object extension from port driver.
                __in pHW_LU_EXTENSION     pLUExt,  // LUN device-object extension from port driver.        
                __in PSCSI_REQUEST_BLOCK  pSrb,
                __in PUCHAR               pResult
                )
{
    PCDB                         pCdb = (PCDB)pSrb->Cdb;
    LONGLONG                     startingSector;
    LONGLONG                     startingOffset;
    ULONG                        numBlocks;
    pMP_WorkRtnParms             pWkRtnParms;
    KIRQL                        SaveIrql;

    KdPrint2(("PhDskMnt::ScsiOpReadWrite:  pHBAExt = 0x%p, pLUExt=0x%p, pSrb=0x%p\n", pHBAExt, pLUExt, pSrb));

    *pResult = ResultDone;                            // Assume no queuing.

    if ((pCdb->AsByte[0] == SCSIOP_READ16) |
        (pCdb->AsByte[0] == SCSIOP_WRITE16))
    {
        REVERSE_BYTES_QUAD(&startingSector, pCdb->CDB16.LogicalBlock);
    }
    else
    {
        startingSector = 0;
        REVERSE_BYTES(&startingSector, &pCdb->CDB10.LogicalBlockByte0);
    }

    if (startingSector & ~(MAXLONGLONG >> pLUExt->BlockPower))
    {      // Check if startingSector << blockPower fits within a LONGLONG.
        KdPrint(("PhDskMnt::ScsiOpReadWrite: Too large sector number: %I64X\n", startingSector));

        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_ILLEGAL_BLOCK, 0);

        return;
    }

    startingOffset = startingSector << pLUExt->BlockPower;

    numBlocks      = pSrb->DataTransferLength >> pLUExt->BlockPower;

    KdPrint2(("PhDskMnt::ScsiOpReadWrite action: 0x%X, starting sector: 0x%I64X, number of blocks: 0x%X\n", (int)pSrb->Cdb[0], startingSector, numBlocks));
    KdPrint2(("PhDskMnt::ScsiOpReadWrite pSrb: 0x%p, pSrb->DataBuffer: 0x%p\n", pSrb, pSrb->DataBuffer));

    if (!pLUExt->Initialized)
    {
        KdPrint(("PhDskMnt::ScsiOpReadWrite: Rejected. Device not initialized.\n"));

        ScsiSetCheckCondition(
            pSrb,
            SRB_STATUS_NOT_POWERED,
            SCSI_SENSE_NOT_READY,
            SCSI_ADSENSE_LUN_NOT_READY,
            SCSI_SENSEQ_BECOMING_READY);

        return;
    }

    // Check device shutdown condition
    if (KeReadStateEvent(&pLUExt->StopThread))
    {
        KdPrint(("PhDskMnt::ScsiOpReadWrite: Rejected. Device shutting down.\n"));

        ScsiSetError(pSrb, SRB_STATUS_INVALID_LUN);

        return;
    }

    // Check write protection
    if (((pSrb->Cdb[0] == SCSIOP_WRITE) |
         (pSrb->Cdb[0] == SCSIOP_WRITE16)) &&
         pLUExt->ReadOnly)
    {
        KdPrint(("PhDskMnt::ScsiOpReadWrite: Rejected. Write attempt on read-only device.\n"));

        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_WRITE_PROTECT, 0);

        return;
    }

    // Check disk bounds
    if ((startingSector + numBlocks) > (pLUExt->DiskSize.QuadPart >> pLUExt->BlockPower))
    {      // Starting sector beyond the bounds?
        KdPrint(("PhDskMnt::ScsiOpReadWrite: Out of bounds: sector: %I64X, blocks: %d\n", startingSector, numBlocks));

        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_ILLEGAL_BLOCK, 0);

        return;
    }

    // Intermediate non-paged cache

    KeAcquireSpinLock(&pLUExt->LastIoLock, &SaveIrql);
    
    if (((pSrb->Cdb[0] == SCSIOP_READ) |
         (pSrb->Cdb[0] == SCSIOP_READ16)) &
        (pLUExt->LastIoBuffer != NULL) &
        (pLUExt->LastIoStartSector <= startingSector) &
        ((startingOffset - (pLUExt->LastIoStartSector << pLUExt->BlockPower) + pSrb->DataTransferLength) <= pLUExt->LastIoLength))
    {
        PVOID sysaddress = NULL;
        ULONG storage_status;

        storage_status = StoragePortGetSystemAddress(pHBAExt, pSrb, &sysaddress);
        if ((storage_status != STORAGE_STATUS_SUCCESS) | (sysaddress == NULL))
        {
            DbgPrint("PhDskMnt::ScsiOpReadWrite: StorPortGetSystemAddress failed: status=0x%X address=0x%p translated=0x%p\n",
                storage_status,
                pSrb->DataBuffer,
                sysaddress);

            ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_NO_SENSE, 0);
        }
        else
        {
            KdPrint2(("PhDskMnt::ScsiOpReadWrite: Read request satisfied by last I/O operation.\n"));
            __try
            {
                RtlMoveMemory(
                    sysaddress,
                    (PUCHAR)pLUExt->LastIoBuffer + startingOffset - (pLUExt->LastIoStartSector << pLUExt->BlockPower),
                    pSrb->DataTransferLength
                    );

                ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
            }
            __except(EXCEPTION_EXECUTE_HANDLER)
            {
                DbgPrint("PhDskMnt::ScsiOpReadWrite: Exception caught!\n");

                ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_NO_SENSE, 0);
            }
        }

        KeReleaseSpinLock(&pLUExt->LastIoLock, SaveIrql);

        return;
    }

    KeReleaseSpinLock(&pLUExt->LastIoLock, SaveIrql);

    pWkRtnParms =                                     // Allocate parm area for work routine.
      (pMP_WorkRtnParms)ExAllocatePoolWithTag(NonPagedPool, sizeof(MP_WorkRtnParms), MP_TAG_GENERAL);

    if (pWkRtnParms == NULL)
    {
      DbgPrint("PhDskMnt::ScsiOpReadWrite Failed to allocate work parm structure\n");

      ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_HARDWARE_ERROR, SCSI_ADSENSE_NO_SENSE, 0);
      return;
    }

    RtlZeroMemory(pWkRtnParms, sizeof(MP_WorkRtnParms)); 

    pWkRtnParms->pHBAExt     = pHBAExt;
    pWkRtnParms->pLUExt      = pLUExt;
    pWkRtnParms->pSrb        = pSrb;

    // Queue work item, which will run in the System process.

    KdPrint2(("PhDskMnt::ScsiOpReadWrite: Queueing work=0x%p\n", pWkRtnParms));

    ExInterlockedInsertTailList(
      &pLUExt->RequestList,
      &pWkRtnParms->RequestListEntry,
      &pLUExt->RequestListLock);
  
    KeSetEvent(&pLUExt->RequestEvent, (KPRIORITY) 0, FALSE);
    
    *pResult = ResultQueued;                          // Indicate queuing.

    KdPrint2(("PhDskMnt::ScsiOpReadWrite:  End. *Result=%i\n", (INT)*pResult));
}                                                     // End ScsiReadWriteSetup.

/**************************************************************************************************/     
/*                                                                                                */     
/**************************************************************************************************/     
VOID
ScsiOpModeSense(
                __in pHW_HBA_EXT          pHBAExt,    // Adapter device-object extension from port driver.
                __in pHW_LU_EXTENSION     pLUExt,     // LUN device-object extension from port driver.
                __in PSCSI_REQUEST_BLOCK  pSrb
               )
{
    PMODE_PARAMETER_HEADER mph = (PMODE_PARAMETER_HEADER)pSrb->DataBuffer;
  
    UNREFERENCED_PARAMETER(pHBAExt);

    KdPrint2(("PhDskMnt::ScsiOpModeSense:  pHBAExt = 0x%p, pLUExt=0x%p, pSrb=0x%p\n", pHBAExt, pLUExt, pSrb));

    RtlZeroMemory((PUCHAR)pSrb->DataBuffer, pSrb->DataTransferLength);

    if (pSrb->DataTransferLength < sizeof(MODE_PARAMETER_HEADER))
    {
        KdPrint(("PhDskMnt::ScsiOpModeSense:  Invalid request length.\n"));
        ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
        return;
    }
    
    mph->ModeDataLength = sizeof(MODE_PARAMETER_HEADER);
    if (pLUExt != NULL ? pLUExt->ReadOnly : FALSE)
        mph->DeviceSpecificParameter = MODE_DSP_WRITE_PROTECT;

    if (pLUExt != NULL ? pLUExt->RemovableMedia : FALSE)
        mph->MediumType = RemovableMedia;

    ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
}

/**************************************************************************************************/     
/*                                                                                                */     
/**************************************************************************************************/     
VOID
ScsiOpModeSense10(
                  __in pHW_HBA_EXT          pHBAExt,    // Adapter device-object extension from port driver.
                  __in pHW_LU_EXTENSION     pLUExt,     // LUN device-object extension from port driver.
                  __in PSCSI_REQUEST_BLOCK  pSrb
                  )
{
    PMODE_PARAMETER_HEADER10 mph = (PMODE_PARAMETER_HEADER10)pSrb->DataBuffer;
  
    UNREFERENCED_PARAMETER(pHBAExt);
    UNREFERENCED_PARAMETER(pLUExt);

    KdPrint(("PhDskMnt::ScsiOpModeSense10:  pHBAExt = 0x%p, pLUExt=0x%p, pSrb=0x%p\n", pHBAExt, pLUExt, pSrb));

    RtlZeroMemory((PUCHAR)pSrb->DataBuffer, pSrb->DataTransferLength);

    if (pSrb->DataTransferLength < sizeof(MODE_PARAMETER_HEADER10))
    {
        KdPrint(("PhDskMnt::ScsiOpModeSense10:  Invalid request length.\n"));
        ScsiSetError(pSrb, SRB_STATUS_DATA_OVERRUN);
        return;
    }

    mph->ModeDataLength[1] = sizeof(MODE_PARAMETER_HEADER10);
    if (pLUExt != NULL ? pLUExt->ReadOnly : FALSE)
        mph->DeviceSpecificParameter = MODE_DSP_WRITE_PROTECT;

    if (pLUExt != NULL ? pLUExt->RemovableMedia : FALSE)
        mph->MediumType = RemovableMedia;

    ScsiSetSuccess(pSrb, pSrb->DataTransferLength);
}

/**************************************************************************************************/     
/*                                                                                                */     
/**************************************************************************************************/     
VOID
ScsiOpReportLuns(                                     
                 __in __out pHW_HBA_EXT         pHBAExt,   // Adapter device-object extension from port driver.
                 __in       PSCSI_REQUEST_BLOCK pSrb
                )
{
    UCHAR                 count;
    PLIST_ENTRY           list_ptr;
    PLUN_LIST             pLunList = (PLUN_LIST)pSrb->DataBuffer; // Point to LUN list.
#if defined(_AMD64_)
    KLOCK_QUEUE_HANDLE    LockHandle;
#else
    KIRQL                 SaveIrql;
#endif

    KdPrint(("PhDskMnt::ScsiOpReportLuns:  pHBAExt = 0x%p, pSrb=0x%p\n", pHBAExt, pSrb));

    // This opcode will be one of the earliest I/O requests for a new HBA (and may be received later, too).
    pHBAExt->bReportAdapterDone = TRUE;

    RtlZeroMemory((PUCHAR)pSrb->DataBuffer, pSrb->DataTransferLength);

#if defined(_AMD64_)
    KeAcquireInStackQueuedSpinLock(                   // Serialize the linked list of LUN extensions.              
                                   &pHBAExt->LUListLock, &LockHandle);
#else
    KeAcquireSpinLock(&pHBAExt->LUListLock, &SaveIrql);
#endif

    for (count = 0, list_ptr = pHBAExt->LUList.Flink;
        (count < MAX_LUNS) & (list_ptr != &pHBAExt->LUList);
        list_ptr = list_ptr->Flink
        )
    {
        pHW_LU_EXTENSION object;
        object = CONTAINING_RECORD(list_ptr, HW_LU_EXTENSION, List);

        if ((object->DeviceNumber.PathId == pSrb->PathId) &
            (object->DeviceNumber.TargetId == pSrb->TargetId))
            if (pSrb->DataTransferLength>=FIELD_OFFSET(LUN_LIST, Lun) + (sizeof(pLunList->Lun[0])*count))
                pLunList->Lun[count++][1] = object->DeviceNumber.Lun;
            else
                break;
    }

#if defined(_AMD64_)
    KeReleaseInStackQueuedSpinLock(&LockHandle);      
#else
    KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

    KdPrint(("PhDskMnt::ScsiOpReportLuns:  Reported %i LUNs\n", (int)count));

    pLunList->LunListLength[3] =                  // Set length needed for LUNs.
        (UCHAR)(8*count);

    // Set the LUN numbers if there is enough room, and set only those LUNs to be reported.

    ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

    KdPrint2(("PhDskMnt::ScsiOpReportLuns:  End: status=0x%X\n", (int)pSrb->SrbStatus));
}                                                     // End ScsiOpReportLuns.

VOID
ScsiSetCheckCondition(
    IN PSCSI_REQUEST_BLOCK pSrb,
    IN UCHAR               SrbStatus,
    IN UCHAR               SenseKey,
    IN UCHAR               AdditionalSenseCode,
    IN UCHAR               AdditionalSenseCodeQualifier OPTIONAL
    )
{
    PSENSE_DATA mph = (PSENSE_DATA)pSrb->SenseInfoBuffer;

    pSrb->SrbStatus = SrbStatus | SRB_STATUS_AUTOSENSE_VALID;
    pSrb->ScsiStatus = SCSISTAT_CHECK_CONDITION;
    pSrb->DataTransferLength = 0;

    RtlZeroMemory((PUCHAR)pSrb->SenseInfoBuffer, pSrb->SenseInfoBufferLength);

    if (pSrb->SenseInfoBufferLength < sizeof(SENSE_DATA))
    {
        DbgPrint("PhDskMnt::ScsiSetCheckCondition:  Insufficient sense data buffer.\n");
        return;
    }

    mph->SenseKey = SenseKey;
    mph->AdditionalSenseCode = AdditionalSenseCode;
    mph->AdditionalSenseCodeQualifier = AdditionalSenseCodeQualifier;
}

UCHAR
ScsiResetLun(
            __in PVOID               pHBAExt,
            __in PSCSI_REQUEST_BLOCK pSrb
)
{
    UNREFERENCED_PARAMETER(pHBAExt);

    ScsiSetSuccess(pSrb, 0);
    return SRB_STATUS_SUCCESS;
}

UCHAR
ScsiResetDevice(
            __in PVOID               pHBAExt,
            __in PSCSI_REQUEST_BLOCK pSrb
)
{
    UNREFERENCED_PARAMETER(pHBAExt);

    ScsiSetSuccess(pSrb, 0);
    return SRB_STATUS_SUCCESS;
}

