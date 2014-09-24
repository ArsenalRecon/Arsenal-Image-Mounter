
/// imscsi.c
/// Driver entry routines, miniport callback definitions and other support
/// routines.
/// 
/// Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#include "phdskmnt.h"

#pragma warning(push)
#pragma warning(disable : 4204)                       /* Prevent C4204 messages from stortrce.h. */
#include <stortrce.h>
#pragma warning(pop)

//#include "trace.h"
//#include "phdskmnt.tmh"
#include "hbapiwmi.h"

#pragma warning(disable : 4204)
#pragma warning(disable : 4221)

#ifdef ALLOC_PRAGMA
#pragma alloc_text( INIT, DriverEntry )
#endif // ALLOC_PRAGMA

/**************************************************************************************************/ 
/*                                                                                                */ 
/* Globals.                                                                                       */ 
/*                                                                                                */ 
/**************************************************************************************************/ 

#ifdef MP_DrvInfo_Inline

MPDriverInfo  lclDriverInfo;

#endif

pMPDriverInfo pMPDrvInfoGlobal = NULL;

#ifdef USE_SCSIPORT
NTSTATUS
ImScsiGetAdapterDeviceObject()
{
    NTSTATUS status = STATUS_OBJECT_NAME_NOT_FOUND;
    int i;
    UNICODE_STRING objname = { 0 };
    PFILE_OBJECT file_object = NULL;
    PDEVICE_OBJECT device_object = NULL;
    WCHAR objstr[] = L"\\Device\\Scsi\\PhDskMnt00";

    if (pMPDrvInfoGlobal->DeviceObject != NULL)
        return STATUS_SUCCESS;

    for (i = 0; i < 100; i++)
    {
        LARGE_INTEGER wait_time;
        int r;

        if ((i & 7) == 7)
        {
            wait_time.QuadPart = -1;
            KeDelayExecutionThread(KernelMode, FALSE, &wait_time);
        }

        _snwprintf(objstr, sizeof(objstr)/sizeof(*objstr), L"\\Device\\Scsi\\PhDskMnt%i", i);

        RtlInitUnicodeString(&objname, objstr);

        for (r = 0; r < 120; r++)
        {
            KdPrint2(("PhDskMnt::ImScsiGetAdapterDeviceObject: Attempt to open %ws...\n", objstr));
    
            status = IoGetDeviceObjectPointer(&objname, GENERIC_ALL, &file_object, &device_object);

            // Not yet ready? (In case port driver not yet intialized)
            if (status == STATUS_DEVICE_NOT_READY)
            {
                DbgPrint("PhDskMnt::ImScsiGetAdapterDeviceObject: Object %ws not ready, waiting...\n", objstr);
                wait_time.QuadPart = -5000000;
                KeDelayExecutionThread(KernelMode, FALSE, &wait_time);
                continue;
            }

            break;
        }

        if (!NT_SUCCESS(status))
        {
            DbgPrint("PhDskMnt::ImScsiGetAdapterDeviceObject: Attempt to open %ws failed: status=0x%X\n", objstr, status);
            continue;
        }

        if (device_object->DriverObject != pMPDrvInfoGlobal->pDriverObj)
        {
            DbgPrint("PhDskMnt::ImScsiGetAdapterDeviceObject: %ws was not our device.\n", objstr, status);
            continue;
        }

        pMPDrvInfoGlobal->DeviceObject = device_object;

        break;
    }

    if (NT_SUCCESS(status))
        DbgPrint("PhDskMnt::ImScsiGetAdapterDeviceObject: Successfully opened %ws.\n", objstr);
    else
        DbgPrint("PhDskMnt::ImScsiGetAdapterDeviceObject: Could not locate SCSI adapter device object by name.\n");

    return status;
}
#endif

VOID
ImScsiFreeGlobalResources()
{
    DbgPrint("PhDskMnt::ImScsiFreeGlobalResources: Unloading.\n");

    if (pMPDrvInfoGlobal != NULL)
    {
        if ((pMPDrvInfoGlobal->GlobalsInitialized) &
            (pMPDrvInfoGlobal->WorkerThread != NULL))
        {
            KeSetEvent(&pMPDrvInfoGlobal->StopWorker, (KPRIORITY) 0, TRUE);
            KeWaitForSingleObject(
                pMPDrvInfoGlobal->WorkerThread,
                Executive,
                KernelMode,
                FALSE,
                NULL);

            ObDereferenceObject(pMPDrvInfoGlobal->WorkerThread);
            pMPDrvInfoGlobal->WorkerThread = NULL;
        }

#ifndef MP_DrvInfo_Inline
        ExFreePoolWithTag(pMPDrvInfoGlobal, MP_TAG_GENERAL);
#endif
    }
}

/**************************************************************************************************/ 
/*                                                                                                */ 
/*                                                                                                */ 
/**************************************************************************************************/ 
NTSTATUS                                                                                                                                              
DriverEntry(
            __in PDRIVER_OBJECT  pDrvObj,
            __in PUNICODE_STRING pRegistryPath
           )
{
    NTSTATUS                       status = STATUS_SUCCESS;
#ifdef USE_STORPORT
    VIRTUAL_HW_INITIALIZATION_DATA hwInitData = { 0 };
#endif
#ifdef USE_SCSIPORT
    HW_INITIALIZATION_DATA         hwInitData = { 0 };
#endif
    pMPDriverInfo                  pMPDrvInfo;
	LARGE_INTEGER                  liTickCount;

    KdPrint2(("PhDskMnt::DriverEntry: Begin.\n"));
    
#ifdef MP_DrvInfo_Inline

    // Because there's no good way to clean up the allocation of the global driver information, 
    // the global information is kept in an inline structure.

    pMPDrvInfo = &lclDriverInfo;

#else

    //
    // Allocate basic miniport driver object (shared across instances of miniport). The pointer is kept in the driver binary's static storage.
    //
    // Because there's no good way to clean up the allocation of the global driver information, 
    // the global information will be leaked.  This is deemed acceptable since it's not expected
    // that DriverEntry will be invoked often in the life of a Windows boot.
    //

    pMPDrvInfo = ExAllocatePoolWithTag(NonPagedPool, sizeof(MPDriverInfo), MP_TAG_GENERAL);

    if (!pMPDrvInfo) {                                // No good?
        status = STATUS_INSUFFICIENT_RESOURCES;

        goto Done;
    }

#endif

	// Initialize driver globals structure

    pMPDrvInfoGlobal = pMPDrvInfo;                    // Save pointer in binary's storage.

    RtlZeroMemory(pMPDrvInfo, sizeof(MPDriverInfo));  // Set pMPDrvInfo's storage to a known state.

    pMPDrvInfo->pDriverObj = pDrvObj;                 // Save pointer to driver object.

    KeInitializeSpinLock(&pMPDrvInfo->DrvInfoLock);   // Initialize spin lock.

    InitializeListHead(&pMPDrvInfo->ListMPHBAObj);    // Initialize list head.

	KeQueryTickCount(&liTickCount);
	pMPDrvInfo->RandomSeed = liTickCount.LowPart;     // Initialize random seed.

    // Get registry parameters.

    MpQueryRegParameters(pRegistryPath, &pMPDrvInfo->MPRegInfo);

    // Set up information for ScsiPortInitialize().

#ifdef USE_STORPORT
    hwInitData.HwInitializationDataSize = sizeof(VIRTUAL_HW_INITIALIZATION_DATA);
#endif
#ifdef USE_SCSIPORT
#if NT4_COMPATIBLE
    hwInitData.HwInitializationDataSize = FIELD_OFFSET(HW_INITIALIZATION_DATA, HwAdapterControl);
#else
    hwInitData.HwInitializationDataSize = sizeof(HW_INITIALIZATION_DATA);
#endif
#endif

    hwInitData.HwInitialize             = MpHwInitialize;           // Required for all ports.
    hwInitData.HwStartIo                = MpHwStartIo;              // Required for all ports.
    hwInitData.HwFindAdapter            = MpHwFindAdapter;          // Required for all ports.
    hwInitData.HwResetBus               = MpHwResetBus;             // Required for all ports.
#ifndef NT4_COMPATIBLE
    hwInitData.HwAdapterControl         = MpHwAdapterControl;       // Required for all post NT4 ports.
#endif
#ifdef USE_STORPORT
    hwInitData.HwFreeAdapterResources   = MpHwFreeAdapterResources; // Required for virtual StorPort.
#endif

    hwInitData.AutoRequestSense         = TRUE;
    hwInitData.TaggedQueuing            = TRUE;
    hwInitData.MultipleRequestPerLu     = TRUE;

    hwInitData.MapBuffers               = STORAGE_MAP_BUFFERS_SETTING;

    hwInitData.DeviceExtensionSize      = sizeof(HW_HBA_EXT);
    hwInitData.SpecificLuExtensionSize  = sizeof(PVOID);
    hwInitData.SrbExtensionSize         = sizeof(HW_SRB_EXTENSION);

    hwInitData.AdapterInterfaceType     = STORAGE_INTERFACE_TYPE;

    status =  StoragePortInitialize(                     // Tell port driver we're here.
                                    pDrvObj,
                                    pRegistryPath,
                                    (PHW_INITIALIZATION_DATA)&hwInitData,
                                    NULL
                                    );

    DbgPrint("PhDskMnt::DriverEntry: StoragePortInitialize returned 0x%X\n", status);

    if (NT_SUCCESS(status))
    {
        // Register our own unload routine

        pMPDrvInfo->pChainUnload = pDrvObj->DriverUnload;
        pDrvObj->DriverUnload = ImScsiUnload;

    }
    else
    {
        ImScsiFreeGlobalResources();
    }

    KdPrint2(("PhDskMnt::DriverEntry: End. status=0x%X\n", status));
    
    return status;
}                                                     // End DriverEntry().

/**************************************************************************************************/ 
/*                                                                                                */ 
/* Unload routine                                                                                 */ 
/*                                                                                                */ 
/**************************************************************************************************/ 
VOID
ImScsiUnload(PDRIVER_OBJECT pDrvObj)
{
    KdPrint(("PhDskMnt::ImScsiUnload.\n"));

    if (pMPDrvInfoGlobal != NULL ? pMPDrvInfoGlobal->pChainUnload != NULL : FALSE)
    {
        KdPrint(("PhDskMnt::ImScsiUnload: Calling next in chain 0x%p.\n", pMPDrvInfoGlobal->pChainUnload));
        pMPDrvInfoGlobal->pChainUnload(pDrvObj);
    }

    // Free our own resources
    ImScsiFreeGlobalResources();

    KdPrint(("PhDskMnt::ImScsiUnload: Done.\n"));
}

/**************************************************************************************************/ 
/*                                                                                                */ 
/* Callback for a new HBA.                                                                        */ 
/*                                                                                                */ 
/**************************************************************************************************/ 
ULONG
MpHwFindAdapter(
                __in       PVOID                           DeviceExtension,
                __in       PVOID                           pReservedArg1,
                __in       PVOID                           pReservedArg2,
#ifdef USE_STORPORT
                __in       PVOID                           pReservedArg3,
#endif
                __in       PCHAR                           ArgumentString,
                __in __out PPORT_CONFIGURATION_INFORMATION pConfigInfo,
                __out      PBOOLEAN                        pBAgain
)
{
    ULONG              i,
                       len,
                       status = SP_RETURN_FOUND;
    PCHAR              pChar;
    pHW_HBA_EXT        pHBAExt = (pHW_HBA_EXT)DeviceExtension;
    NTSTATUS           ntstatus;
    
#if defined(_AMD64_)

    KLOCK_QUEUE_HANDLE LockHandle;

#else

    KIRQL              SaveIrql;

#endif

    UNREFERENCED_PARAMETER(pReservedArg1);
    UNREFERENCED_PARAMETER(pReservedArg2);
#ifdef USE_STORPORT
    UNREFERENCED_PARAMETER(pReservedArg3);
#endif
    UNREFERENCED_PARAMETER(ArgumentString);

    KdPrint2(("PhDskMnt::MpHwFindAdapter:  Arg=%s%s%s, pHBAExt = 0x%p, pConfigInfo = 0x%p, IRQL=%i\n",
        ArgumentString != NULL ? "\"" : "(",
        ArgumentString != NULL ? ArgumentString : "null",
        ArgumentString != NULL ? "\"" : ")",
        pHBAExt,
        pConfigInfo,
        KeGetCurrentIrql()));

#if VERBOSE_DEBUG_TRACE > 0

    if (!KD_DEBUGGER_NOT_PRESENT)
        DbgBreakPoint();

#endif

    if (pMPDrvInfoGlobal->GlobalsInitialized)
    {
        LARGE_INTEGER wait_time;

        DbgPrint("PhDskMnt::MpHwFindAdapter: Already initialized.\n");

        wait_time.QuadPart = -1000000;
        KeDelayExecutionThread(KernelMode, FALSE, &wait_time);
    }

    KeInitializeSpinLock(&pHBAExt->LUListLock);
    InitializeListHead(&pHBAExt->LUList);

    pHBAExt->HostTargetId = (UCHAR)pMPDrvInfoGlobal->MPRegInfo.InitiatorID;

    pConfigInfo->WmiDataProvider                = FALSE;                       // Indicate WMI provider.

    pConfigInfo->NumberOfPhysicalBreaks         = 4096;

    pConfigInfo->MaximumTransferLength          = 8 << 20;                     // 8 MB.

#ifdef USE_STORPORT

    pConfigInfo->VirtualDevice                  = TRUE;                        // Inidicate no real hardware.
    pConfigInfo->SynchronizationModel           = StorSynchronizeFullDuplex;

    if (pConfigInfo->Dma64BitAddresses == SCSI_DMA64_SYSTEM_SUPPORTED)
        pConfigInfo->Dma64BitAddresses = SCSI_DMA64_MINIPORT_FULL64BIT_SUPPORTED;

#endif
#ifdef USE_SCSIPORT

    //if (pConfigInfo->NumberOfPhysicalBreaks == SP_UNINITIALIZED_VALUE)
    //    pConfigInfo->NumberOfPhysicalBreaks     = 4096;

    //if (pConfigInfo->MaximumTransferLength > (64 << 10))
    //    pConfigInfo->MaximumTransferLength      = 64 << 10;                     // 64 KB.

    pConfigInfo->Dma64BitAddresses = SCSI_DMA64_MINIPORT_SUPPORTED;

#endif

    pConfigInfo->AlignmentMask                  = 0x3;                         // Indicate DWORD alignment.
    pConfigInfo->CachesData                     = FALSE;                       // Indicate miniport wants flush and shutdown notification.
    pConfigInfo->MaximumNumberOfTargets         = SCSI_MAXIMUM_TARGETS;        // Indicate maximum targets.
    pConfigInfo->NumberOfBuses                  =
        (UCHAR) pMPDrvInfoGlobal->MPRegInfo.NumberOfBuses;                     // Indicate number of busses.
    pConfigInfo->ScatterGather                  = TRUE;                        // Indicate scatter-gather (explicit setting needed for Win2003 at least).
    pConfigInfo->AutoRequestSense               = TRUE;
    pConfigInfo->TaggedQueuing                  = TRUE;
    pConfigInfo->MultipleRequestPerLu           = TRUE;

    // Save Vendor Id, Product Id, Revision in device extension.

    pChar = (PCHAR)pMPDrvInfoGlobal->MPRegInfo.VendorId.Buffer;
    len = min(8, (pMPDrvInfoGlobal->MPRegInfo.VendorId.Length/2));
    for ( i = 0; i < len; i++, pChar+=2)
      pHBAExt->VendorId[i] = *pChar;

    pChar = (PCHAR)pMPDrvInfoGlobal->MPRegInfo.ProductId.Buffer;
    len = min(16, (pMPDrvInfoGlobal->MPRegInfo.ProductId.Length/2));
    for ( i = 0; i < len; i++, pChar+=2)
      pHBAExt->ProductId[i] = *pChar;

    pChar = (PCHAR)pMPDrvInfoGlobal->MPRegInfo.ProductRevision.Buffer;
    len = min(4, (pMPDrvInfoGlobal->MPRegInfo.ProductRevision.Length/2));
    for ( i = 0; i < len; i++, pChar+=2)
      pHBAExt->ProductRevision[i] = *pChar;

    // Add HBA extension to master driver object's linked list.

#if defined(_AMD64_)

    KeAcquireInStackQueuedSpinLock(&pMPDrvInfoGlobal->DrvInfoLock, &LockHandle);

#else

    KeAcquireSpinLock(&pMPDrvInfoGlobal->DrvInfoLock, &SaveIrql);

#endif

    InsertTailList(&pMPDrvInfoGlobal->ListMPHBAObj, &pHBAExt->List);

    pMPDrvInfoGlobal->DrvInfoNbrMPHBAObj++;

#if defined(_AMD64_)

    KeReleaseInStackQueuedSpinLock(&LockHandle);

#else

    KeReleaseSpinLock(&pMPDrvInfoGlobal->DrvInfoLock, SaveIrql);

#endif

    if (!pMPDrvInfoGlobal->GlobalsInitialized)
    {
        HANDLE thread_handle;
        OBJECT_ATTRIBUTES object_attributes;

        KeInitializeSpinLock(&pMPDrvInfoGlobal->RequestListLock);
        InitializeListHead(&pMPDrvInfoGlobal->RequestList);
        KeInitializeEvent(&pMPDrvInfoGlobal->RequestEvent, SynchronizationEvent, FALSE);

#ifdef USE_SCSIPORT
        KeInitializeSpinLock(&pMPDrvInfoGlobal->ResponseListLock);
        KeInitializeEvent(&pMPDrvInfoGlobal->ResponseEvent, SynchronizationEvent, FALSE);
        InitializeListHead(&pMPDrvInfoGlobal->ResponseList);
#endif

        KeInitializeEvent(&pMPDrvInfoGlobal->StopWorker, NotificationEvent, FALSE);

        pMPDrvInfoGlobal->GlobalsInitialized = TRUE;

        InitializeObjectAttributes(&object_attributes, NULL, OBJ_KERNEL_HANDLE, NULL, NULL);

        ntstatus = PsCreateSystemThread(
            &thread_handle,
            (ACCESS_MASK) 0L,
            &object_attributes,
            NULL,
            NULL,
            ImScsiWorkerThread,
            NULL);

        if (!NT_SUCCESS(ntstatus))
        {
            DbgPrint("PhDskMnt::ScsiGetLUExtension: Cannot create worker thread. (%#x)\n", ntstatus);

            status = SP_RETURN_ERROR;
        }
        else
        {
            ObReferenceObjectByHandle(
                thread_handle,
                FILE_READ_ATTRIBUTES | SYNCHRONIZE,
                *PsThreadType,
                KernelMode,
                (PVOID*)&pMPDrvInfoGlobal->WorkerThread,
                NULL
                );

            ZwClose(thread_handle);

            //for (i = 0; i < pHBAExt->NbrLUNsperHBA; i++)
            //    ImScsiCreateLU(pHBAExt, 0, (UCHAR)i, 0);
        }
    }

//Done:
    *pBAgain = FALSE;    
        
    KdPrint2(("PhDskMnt::MpHwFindAdapter: End, status = 0x%X\n", status));

    return status;
}                                                     // End MpHwFindAdapter().

/**************************************************************************************************/ 
/*                                                                                                */ 
/**************************************************************************************************/ 
BOOLEAN
MpHwInitialize(__in PVOID pHBAExt)
{
  UNREFERENCED_PARAMETER(pHBAExt);

  KdPrint2(("PhDskMnt::MpHwInitialize:  pHBAExt = 0x%p. IRQL=%i\n", pHBAExt, KeGetCurrentIrql()));

  return TRUE;
}                                                     // End MpHwInitialize().

/**************************************************************************************************/ 
/*                                                                                                */ 
/**************************************************************************************************/ 
#if 1
BOOLEAN
MpHwResetBus(
             __in pHW_HBA_EXT          pHBAExt,       // Adapter device-object extension from port driver.
             __in ULONG                BusId
            )
{
    UNREFERENCED_PARAMETER(pHBAExt);
    UNREFERENCED_PARAMETER(BusId);

    // To do: At some future point, it may be worthwhile to ensure that any SRBs being handled be completed at once.
    //        Practically speaking, however, it seems that the only SRBs that would not be completed very quickly
    //        would be those handled by the worker thread. In the future, therefore, there might be a global flag
    //        set here to instruct the thread to complete outstanding I/Os as they appear; but a period for that
    //        happening would have to be devised (such completion shouldn't be unbounded).

    DbgPrint("PhDskMnt::MpHwResetBus:  pHBAExt = 0x%p, BusId = %u. Ignored.\n", pHBAExt, BusId);

    return TRUE;
}                                               // End MpHwResetBus().
#else
BOOLEAN
MpHwResetBus(
             __in pHW_HBA_EXT          pHBAExt,       // Adapter device-object extension from port driver.
             __in ULONG                BusId
            )
{
    // To do: At some future point, it may be worthwhile to ensure that any SRBs being handled be completed at once.
    //        Practically speaking, however, it seems that the only SRBs that would not be completed very quickly
    //        would be those handled by the worker thread. In the future, therefore, there might be a global flag
    //        set here to instruct the thread to complete outstanding I/Os as they appear; but a period for that
    //        happening would have to be devised (such completion shouldn't be unbounded).

    DbgPrint("PhDskMnt::MpHwResetBus:  pHBAExt = 0x%p, BusId = %u. Calling ScsiPortCompleteRequest().\n", pHBAExt, BusId);

    ScsiPortCompleteRequest(pHBAExt,
                (UCHAR) BusId,
                SP_UNTAGGED,
                SP_UNTAGGED,
                SRB_STATUS_BUS_RESET);

    return TRUE;
}                                                     // End MpHwResetBus().
#endif

/**************************************************************************************************/ 
/*                                                                                                */ 
/**************************************************************************************************/ 
NTSTATUS                                              
ImScsiHandleRemoveDevice(
                         __in pHW_HBA_EXT             pHBAExt,// Adapter device-object extension from port driver.
                         __in PSCSI_PNP_REQUEST_BLOCK pSrb
                         )
{
    UNREFERENCED_PARAMETER(pHBAExt);

    KdPrint(("PhDskMnt::ImScsiHandleRemoveDevice:  pHBAExt = 0x%p, pSrb = 0x%p\n", pHBAExt, pSrb));

    pSrb->SrbStatus = SRB_STATUS_BAD_FUNCTION;

    return STATUS_UNSUCCESSFUL;
}                                                     // End ImScsiHandleRemoveDevice().

/**************************************************************************************************/ 
/*                                                                                                */ 
/**************************************************************************************************/ 
NTSTATUS                                           
ImScsiHandleQueryCapabilities(
                              __in pHW_HBA_EXT             pHBAExt,// Adapter device-object extension from port driver.
                              __in PSCSI_PNP_REQUEST_BLOCK pSrb
                              )
{
    NTSTATUS                  status = STATUS_SUCCESS;
    PSTOR_DEVICE_CAPABILITIES pStorageCapabilities = (PSTOR_DEVICE_CAPABILITIES)pSrb->DataBuffer;

    UNREFERENCED_PARAMETER(pHBAExt);

    KdPrint(("PhDskMnt::ImScsiHandleQueryCapabilities:  pHBAExt = 0x%p, pSrb = 0x%p\n", pHBAExt, pSrb));

    RtlZeroMemory(pStorageCapabilities, pSrb->DataTransferLength);

    pStorageCapabilities->Removable = FALSE;
    pStorageCapabilities->SurpriseRemovalOK = FALSE;

    pSrb->SrbStatus = SRB_STATUS_SUCCESS;

    return status;
}                                                     // End ImScsiHandleQueryCapabilities().

/**************************************************************************************************/ 
/*                                                                                                */ 
/**************************************************************************************************/ 
NTSTATUS                                              
MpHwHandlePnP(
              __in pHW_HBA_EXT              pHBAExt,  // Adapter device-object extension from port driver.
              __in PSCSI_PNP_REQUEST_BLOCK  pSrb
             )
{
    NTSTATUS status = STATUS_SUCCESS;

    KdPrint2(("PhDskMnt::MpHwHandlePnP:  pHBAExt = 0x%p, pSrb = 0x%p\n", pHBAExt, pSrb));

    switch(pSrb->PnPAction)
    {

      case StorRemoveDevice:
        status = ImScsiHandleRemoveDevice(pHBAExt, pSrb);

        break;

      case StorQueryCapabilities:
        status = ImScsiHandleQueryCapabilities(pHBAExt, pSrb);

        break;

      default:
        pSrb->SrbStatus = SRB_STATUS_SUCCESS;         // Do nothing.
    }

    if (STATUS_SUCCESS!=status) {
    }

    KdPrint2(("PhDskMnt::MpHwHandlePnP:  status = 0x%X\n", status));

    return status;
}                                                     // End MpHwHandlePnP().

#ifdef USE_SCSIPORT
LONG
ImScsiCompletePendingSrbs(
                          __in pHW_HBA_EXT pHBAExt  // Adapter device-object extension from port driver.
)
{
  pMP_WorkRtnParms pWkRtnParms;
  LONG done = 0;

  KdPrint2(("PhDskMnt::ImScsiCompletePendingSrbs start. pHBAExt = 0x%p\n", pHBAExt));

  for (;;)
  {
    PLIST_ENTRY request =
      ExInterlockedRemoveHeadList(&pMPDrvInfoGlobal->ResponseList,
      &pMPDrvInfoGlobal->ResponseListLock);

    if (request == NULL)
    {
      LONG was_pending = _InterlockedExchangeAdd((volatile LONG*)&pHBAExt->WorkItems, -done);
      KdPrint2(("PhDskMnt::ImScsiCompletePendingSrbs finished.\n"));
      return was_pending - done;
    }

    ++done;

    pWkRtnParms = (pMP_WorkRtnParms)CONTAINING_RECORD(request, MP_WorkRtnParms, ResponseListEntry);

    KdPrint2(("PhDskMnt::ImScsiCompletePendingSrbs: Completing pWkRtnParms = 0x%p, pSrb = 0x%p\n", pWkRtnParms, pWkRtnParms->pSrb));

    ScsiPortNotification(RequestComplete, pWkRtnParms->pHBAExt, pWkRtnParms->pSrb);
    ScsiPortNotification(NextRequest, pWkRtnParms->pHBAExt);

    ExFreePoolWithTag(pWkRtnParms, MP_TAG_GENERAL);      // Free parm list.
  }
}

VOID
MpHwTimer(
  __in pHW_HBA_EXT pHBAExt
)
{
  ULONG wait = 40000;
  LONG pending;

  KdPrint2(("PhDskMnt::MpHwTimer start. pHBAExt = 0x%p\n", pHBAExt));

  pending = ImScsiCompletePendingSrbs(pHBAExt);

  if (pending > 0)
  {
    KdPrint2(("PhDskMnt::MpHwTimer finished, %i items pending, restarting in %u µs.\n", pending, wait));
    ScsiPortNotification(RequestTimerCall, pHBAExt, MpHwTimer, wait);
  }
  else
    KdPrint2(("PhDskMnt::MpHwTimer finished, nothing left to do.\n"));
}
#endif


/**************************************************************************************************/ 
/*                                                                                                */ 
/**************************************************************************************************/ 
BOOLEAN
MpHwStartIo(
            __in       pHW_HBA_EXT          pHBAExt,  // Adapter device-object extension from port driver.
            __in __out PSCSI_REQUEST_BLOCK  pSrb
           )
{
    UCHAR                     Result = ResultDone;
#ifdef USE_SCSIPORT
    UCHAR                     PathId = pSrb->PathId;
    UCHAR                     TargetId = pSrb->TargetId;
    UCHAR                     Lun = pSrb->Lun;
#endif

    KdPrint2(("PhDskMnt::MpHwStartIo:  pHBAExt = 0x%p, pSrb = 0x%p, Path=%i, Target=%i, Lun=%i, IRQL=%i\n",
        pHBAExt,
        pSrb,
        (int) pSrb->PathId,
        (int) pSrb->TargetId,
        (int) pSrb->Lun,
        KeGetCurrentIrql()));

    pSrb->SrbStatus = SRB_STATUS_PENDING;
    pSrb->ScsiStatus = SCSISTAT_GOOD;

    ImScsiCompletePendingSrbs(pHBAExt);

    _InterlockedExchangeAdd((volatile LONG *)&pHBAExt->SRBsSeen, 1);   // Bump count of SRBs encountered.

    // Next, if true, will cause port driver to remove the associated LUNs if, for example, devmgmt.msc is asked "scan for hardware changes."
    //if (pHBAExt->bDontReport)
    //{                       // Act as though the HBA/path is gone?
    //    pSrb->SrbStatus = SRB_STATUS_INVALID_LUN;
    //    goto done;
    //}

    switch (pSrb->Function)
    {
    case SRB_FUNCTION_IO_CONTROL:
        ScsiIoControl(pHBAExt, pSrb, &Result);
        break;

    case SRB_FUNCTION_EXECUTE_SCSI:
        ScsiExecute(pHBAExt, pSrb, &Result);
        break;

    case SRB_FUNCTION_RESET_LOGICAL_UNIT:
        DbgPrint("PhDskMnt::MpHwStartIo: SRB_FUNCTION_RESET_LOGICAL_UNIT.\n");
        pSrb->SrbStatus = ScsiResetLun(pHBAExt, pSrb);
        break;

    case SRB_FUNCTION_RESET_DEVICE:
        DbgPrint("PhDskMnt::MpHwStartIo: SRB_FUNCTION_RESET_DEVICE.\n");
        pSrb->SrbStatus = ScsiResetDevice(pHBAExt, pSrb);
        break;
            
    case SRB_FUNCTION_RESET_BUS:
        DbgPrint("PhDskMnt::MpHwStartIo: SRB_FUNCTION_RESET_BUS.\n");
        pSrb->SrbStatus = MpHwResetBus(pHBAExt, pSrb->PathId);
        break;
            
    case SRB_FUNCTION_PNP:                        
        MpHwHandlePnP(pHBAExt, (PSCSI_PNP_REQUEST_BLOCK)pSrb);
        break;

    case SRB_FUNCTION_POWER:                      
        KdPrint(("PhDskMnt::MpHwStartIo: SRB_FUNCTION_POWER.\n"));
        // Do nothing.
        pSrb->SrbStatus = SRB_STATUS_SUCCESS;

        break;

    case SRB_FUNCTION_SHUTDOWN:
	KdPrint(("PhDskMnt::MpHwStartIo: SRB_FUNCTION_SHUTDOWN.\n"));
        // Do nothing.
        pSrb->SrbStatus = SRB_STATUS_SUCCESS;

        break;

    default:
        KdPrint(("PhDskMnt::MpHwStartIo: Unknown pSrb Function = 0x%X\n", pSrb->Function));
        
        ScsiSetCheckCondition(pSrb, SRB_STATUS_ERROR, SCSI_SENSE_ILLEGAL_REQUEST, SCSI_ADSENSE_ILLEGAL_COMMAND, 0);

        break;

    } // switch (pSrb->Function)

    if (Result == ResultDone)
    {                         // Complete now?
#ifdef USE_SCSIPORT
        KdPrint2(("PhDskMnt::MpHwStartIo sending 'RequestComplete', 'NextRequest' and 'NextLuRequest' to ScsiPort.\n"));
        ScsiPortNotification(RequestComplete, pHBAExt, pSrb);
        ScsiPortNotification(NextRequest, pHBAExt);
        ScsiPortNotification(NextLuRequest, pHBAExt, 0, 0, 0);
#endif
#ifdef USE_STORPORT
        KdPrint2(("PhDskMnt::MpHwStartIo sending 'RequestComplete' to port StorPort.\n"));
        StorPortNotification(RequestComplete, pHBAExt, pSrb);
#endif
    }
    else
    {
#ifdef USE_SCSIPORT
        _InterlockedExchangeAdd((volatile LONG*)&pHBAExt->WorkItems, 1);

        KdPrint2(("PhDskMnt::MpHwStartIo sending 'RequestTimerCall' and 'NextLuRequest' to ScsiPort.\n"));
        ScsiPortNotification(RequestTimerCall, pHBAExt, MpHwTimer, (ULONG) 1);
        ScsiPortNotification(NextLuRequest, pHBAExt, PathId, TargetId, Lun);
        ScsiPortNotification(NextLuRequest, pHBAExt, 0, 0, 0);
#endif
    }

    KdPrint2(("PhDskMnt::MpHwStartIo End.\n"));

    return TRUE;
}                                                     // End MpHwStartIo().

/**************************************************************************************************/ 
/*                                                                                                */ 
/**************************************************************************************************/ 
SCSI_ADAPTER_CONTROL_STATUS
MpHwAdapterControl(
                   __in pHW_HBA_EXT               pHBAExt, // Adapter device-object extension from port driver.
                   __in SCSI_ADAPTER_CONTROL_TYPE ControlType,
                   __in PVOID                     pParameters
                  )
{
    PSCSI_SUPPORTED_CONTROL_TYPE_LIST pCtlTypList;
    ULONG                             i;

    KdPrint2(("PhDskMnt::MpHwAdapterControl:  pHBAExt = 0x%p, ControlType = 0x%p, pParameters=0x%p\n", pHBAExt, ControlType, pParameters));

    pHBAExt->AdapterState = ControlType;

    switch (ControlType) {
        case ScsiQuerySupportedControlTypes:
            KdPrint2(("PhDskMnt::MpHwAdapterControl: ScsiQuerySupportedControlTypes\n"));

            // Ggt pointer to control type list
            pCtlTypList = (PSCSI_SUPPORTED_CONTROL_TYPE_LIST)pParameters;

            // Cycle through list to set TRUE for each type supported
            // making sure not to go past the MaxControlType
            for (i = 0; i < pCtlTypList->MaxControlType; i++)
                if ( i == ScsiQuerySupportedControlTypes ||
                     i == ScsiStopAdapter   || i == ScsiRestartAdapter ||
                     i == ScsiSetBootConfig || i == ScsiSetRunningConfig )
                {
                    pCtlTypList->SupportedTypeList[i] = TRUE;
                }
            break;

        case ScsiStopAdapter:
            KdPrint2(("PhDskMnt::MpHwAdapterControl: ScsiStopAdapter\n"));

            // Free memory allocated for disk
            ImScsiStopAdapter(pHBAExt);

            break;

        case ScsiRestartAdapter:
            KdPrint2(("PhDskMnt::MpHwAdapterControl: ScsiRestartAdapter\n"));

            /* To Do: Add some function. */

            break;

        case ScsiSetBootConfig:
            KdPrint2(("PhDskMnt::MpHwAdapterControl: ScsiSetBootConfig\n"));

            break;
            
        case ScsiSetRunningConfig:
            KdPrint2(("PhDskMnt::MpHwAdapterControl: ScsiSetRunningConfig\n"));

            break;

        default:
            KdPrint2(("PhDskMnt::MpHwAdapterControl: UNKNOWN: 0x%X\n", ControlType));

            break;
    } 

    KdPrint2(("PhDskMnt::MpHwAdapterControl End: status=0x%X\n", ScsiAdapterControlSuccess));

    return ScsiAdapterControlSuccess;
}                                                     // End MpHwAdapterControl().

/**************************************************************************************************/ 
/*                                                                                                */ 
/**************************************************************************************************/ 
VOID
ImScsiStopAdapter(
                  __in pHW_HBA_EXT pHBAExt         // Adapter device-object extension from port driver.
                  )
{
    SRB_IMSCSI_REMOVE_DEVICE rem_data = { 0 };

    KdPrint2(("PhDskMnt::ImScsiStopAdapter:  pHBAExt = 0x%p\n", pHBAExt));

    // Remove all devices, using "wildcard" device number.
    rem_data.DeviceNumber.LongNumber = IMSCSI_ALL_DEVICES;

    ImScsiRemoveDevice(pHBAExt, &rem_data);

    KdPrint2(("PhDskMnt::ImScsiStopAdapter End.\n"));

    return;
}                                                     // End ImScsiStopAdapter().

///**************************************************************************************************/                         
///*                                                                                                */                         
///* ImScsiTracingInit.                                                                             */                         
///*                                                                                                */                         
///**************************************************************************************************/                         
//VOID                                                                                                                         
//ImScsiTracingInit(                                                                                                            
//              __in PVOID pArg1,                                                                                  
//              __in PVOID pArg2
//             )                                                                                                            
//{                                                                                                                            
//    WPP_INIT_TRACING(pArg1, pArg2);
//}                                                     // End ImScsiTracingInit().

///**************************************************************************************************/                         
///*                                                                                                */                         
///* MPTracingCleanUp.                                                                              */                         
///*                                                                                                */                         
///* This is called when the driver is being unloaded.                                              */                         
///*                                                                                                */                         
///**************************************************************************************************/                         
//VOID                                                                                                                         
//ImScsiTracingCleanup(__in PVOID pArg1)                                                                                                            
//{                                                                                                                            
//    WPP_CLEANUP(pArg1);
//}                                                     // End ImScsiTracingCleanup().

#ifdef USE_STORPORT
/**************************************************************************************************/                         
/*                                                                                                */                         
/* MpHwFreeAdapterResources.                                                                      */                         
/*                                                                                                */                         
/**************************************************************************************************/                         
VOID
MpHwFreeAdapterResources(__in pHW_HBA_EXT pHBAExt)
{
    PLIST_ENTRY           pNextEntry; 
    pHW_HBA_EXT           pLclHBAExt;
#if defined(_AMD64_)
    KLOCK_QUEUE_HANDLE    LockHandle;
#else
    KIRQL                 SaveIrql;
#endif

    KdPrint2(("PhDskMnt::MpHwFreeAdapterResources:  pHBAExt = 0x%p\n", pHBAExt));

#if defined(_AMD64_)
    KeAcquireInStackQueuedSpinLock(&pMPDrvInfoGlobal->DrvInfoLock, &LockHandle);
#else
    KeAcquireSpinLock(&pMPDrvInfoGlobal->DrvInfoLock, &SaveIrql);
#endif

    for (                                             // Go through linked list of HBA extensions.
         pNextEntry =  pMPDrvInfoGlobal->ListMPHBAObj.Flink;
         pNextEntry != &pMPDrvInfoGlobal->ListMPHBAObj;
         pNextEntry =  pNextEntry->Flink
        ) {
        pLclHBAExt = CONTAINING_RECORD(pNextEntry, HW_HBA_EXT, List);

        if (pLclHBAExt==pHBAExt) {                    // Is this entry the same as pHBAExt?
            RemoveEntryList(pNextEntry);
            pMPDrvInfoGlobal->DrvInfoNbrMPHBAObj--;
            break;
        }
    }

#if defined(_AMD64_)
    KeReleaseInStackQueuedSpinLock(&LockHandle);
#else
    KeReleaseSpinLock(&pMPDrvInfoGlobal->DrvInfoLock, SaveIrql);
#endif

}                                                     // End MpHwFreeAdapterResources().
#endif

NTSTATUS
ImScsiGetDiskSize(
    IN HANDLE FileHandle,
    IN OUT PIO_STATUS_BLOCK IoStatus,
    IN OUT PLARGE_INTEGER DiskSize)
{
    NTSTATUS status;

    {
	FILE_STANDARD_INFORMATION file_standard;

	status = ZwQueryInformationFile(FileHandle,
	    IoStatus,
	    &file_standard,
	    sizeof(FILE_STANDARD_INFORMATION),
	    FileStandardInformation);

	if (NT_SUCCESS(status))
	{
	    *DiskSize = file_standard.EndOfFile;
	    return status;
	}

	KdPrint(("ImScsi: FileStandardInformation not supported for "
	    "target device. %#x\n", status));
    }

    // Retry with IOCTL_DISK_GET_LENGTH_INFO instead
    {
	GET_LENGTH_INFORMATION part_info = { 0 };

	status =
	    ZwDeviceIoControlFile(FileHandle,
	    NULL,
	    NULL,
	    NULL,
	    IoStatus,
	    IOCTL_DISK_GET_LENGTH_INFO,
	    NULL,
	    0,
	    &part_info,
	    sizeof(part_info));

	if (status == STATUS_PENDING)
	{
	    ZwWaitForSingleObject(FileHandle, FALSE, NULL);
	    status = IoStatus->Status;
	}

	if (NT_SUCCESS(status))
	{
	    *DiskSize = part_info.Length;
	    return status;
	}

	KdPrint(("ImScsi: IOCTL_DISK_GET_LENGTH_INFO not supported "
	    "for target device. %#x\n", status));
    }

    // Retry with IOCTL_DISK_GET_PARTITION_INFO instead
    {
	PARTITION_INFORMATION part_info = { 0 };

	status =
	    ZwDeviceIoControlFile(FileHandle,
	    NULL,
	    NULL,
	    NULL,
	    IoStatus,
	    IOCTL_DISK_GET_PARTITION_INFO,
	    NULL,
	    0,
	    &part_info,
	    sizeof(part_info));

	if (status == STATUS_PENDING)
	{
	    ZwWaitForSingleObject(FileHandle, FALSE, NULL);
	    status = IoStatus->Status;
	}

	if (NT_SUCCESS(status))
	{
	    *DiskSize = part_info.PartitionLength;
	    return status;
	}

	KdPrint(("ImScsi: IOCTL_DISK_GET_PARTITION_INFO not supported "
	    "for target device. %#x\n", status));
    }

    return status;
}

VOID
ImScsiLogDbgError(IN PVOID Object,
                  IN UCHAR MajorFunctionCode,
                  IN UCHAR RetryCount,
                  IN PULONG DumpData,
                  IN USHORT DumpDataSize,
                  IN USHORT EventCategory,
                  IN NTSTATUS ErrorCode,
                  IN ULONG UniqueErrorValue,
                  IN NTSTATUS FinalStatus,
                  IN ULONG SequenceNumber,
                  IN ULONG IoControlCode,
                  IN PLARGE_INTEGER DeviceOffset,
                  IN PWCHAR Message)
{
  ULONG_PTR string_byte_size;
  ULONG_PTR packet_size;
  PIO_ERROR_LOG_PACKET error_log_packet;

  if (KeGetCurrentIrql() > DISPATCH_LEVEL)
    return;

  string_byte_size = (wcslen(Message) + 1) << 1;

  packet_size =
    sizeof(IO_ERROR_LOG_PACKET) + DumpDataSize + string_byte_size;

  if (packet_size > ERROR_LOG_MAXIMUM_SIZE)
    {
      KdPrint(("ImScsi: Warning: Too large error log packet.\n"));
      return;
    }

  error_log_packet =
    (PIO_ERROR_LOG_PACKET) IoAllocateErrorLogEntry(Object,
						   (UCHAR) packet_size);

  if (error_log_packet == NULL)
    {
      KdPrint(("ImScsi: Warning: IoAllocateErrorLogEntry() returned NULL.\n"));
      return;
    }

  error_log_packet->MajorFunctionCode = MajorFunctionCode;
  error_log_packet->RetryCount = RetryCount;
  error_log_packet->StringOffset = sizeof(IO_ERROR_LOG_PACKET) + DumpDataSize;
  error_log_packet->EventCategory = EventCategory;
  error_log_packet->ErrorCode = ErrorCode;
  error_log_packet->UniqueErrorValue = UniqueErrorValue;
  error_log_packet->FinalStatus = FinalStatus;
  error_log_packet->SequenceNumber = SequenceNumber;
  error_log_packet->IoControlCode = IoControlCode;
  if (DeviceOffset != NULL)
    error_log_packet->DeviceOffset = *DeviceOffset;
  error_log_packet->DumpDataSize = DumpDataSize;

  if (DumpDataSize != 0)
    memcpy(error_log_packet->DumpData, DumpData, DumpDataSize);

  if (Message == NULL)
    error_log_packet->NumberOfStrings = 0;
  else
    {
      error_log_packet->NumberOfStrings = 1;
      memcpy((PUCHAR)error_log_packet + error_log_packet->StringOffset,
	     Message,
	     string_byte_size);
    }

  IoWriteErrorLogEntry(error_log_packet);
}

//
// Reads in a loop up to "Length" or until eof reached.
//
NTSTATUS
ImScsiSafeReadFile(IN HANDLE FileHandle,
		   OUT PIO_STATUS_BLOCK IoStatusBlock,
		   OUT PVOID Buffer,
		   IN SIZE_T Length,
		   IN PLARGE_INTEGER Offset)
{
  NTSTATUS status;
  SIZE_T LengthDone = 0;

  ASSERT(FileHandle != NULL);
  ASSERT(IoStatusBlock != NULL);
  ASSERT(Buffer != NULL);

  while (LengthDone < Length)
    {
      SIZE_T LongRequestLength = Length - LengthDone;
      ULONG RequestLength;
      if (LongRequestLength > 0x0000000080000000)
	RequestLength = 0x80000000;
      else
	RequestLength = (ULONG) LongRequestLength;

      for (;;)
	{
	  LARGE_INTEGER RequestOffset;
	  PUCHAR InterBuffer = (PUCHAR) ExAllocatePoolWithTag(
              PagedPool, RequestLength, MP_TAG_GENERAL);

	  if (InterBuffer == NULL)
	    {
	      KdPrint(("PhDskMnt: Insufficient paged pool to allocate "
		       "intermediate buffer for ImScsiSafeReadFile() "
		       "(%u bytes).\n", RequestLength));

	      RequestLength >>= 2;
	      continue;
	    }

	  RequestOffset.QuadPart = Offset->QuadPart + LengthDone;

	  status = ZwReadFile(FileHandle,
			      NULL,
			      NULL,
			      NULL,
			      IoStatusBlock,
			      InterBuffer,
			      RequestLength,
			      &RequestOffset,
			      NULL);

	  if ((status == STATUS_INSUFFICIENT_RESOURCES) |
	      (status == STATUS_INVALID_BUFFER_SIZE) |
	      (status == STATUS_INVALID_PARAMETER))
	    {
	      ExFreePoolWithTag(InterBuffer, MP_TAG_GENERAL);

	      RequestLength >>= 2;
	      continue;
	    }

	  if (!NT_SUCCESS(status))
	    {
	      ExFreePoolWithTag(InterBuffer, MP_TAG_GENERAL);
	      break;
	    }

	  RtlCopyMemory((PUCHAR) Buffer + LengthDone, InterBuffer,
			IoStatusBlock->Information);

	  ExFreePoolWithTag(InterBuffer, MP_TAG_GENERAL);
	  break;
	}

      if (!NT_SUCCESS(status))
	{
	  IoStatusBlock->Status = status;
	  IoStatusBlock->Information = LengthDone;
	  return IoStatusBlock->Status;
	}

      if (IoStatusBlock->Information == 0)
	{
	  IoStatusBlock->Status = STATUS_CONNECTION_RESET;
	  IoStatusBlock->Information = LengthDone;
	  return IoStatusBlock->Status;
	}

      LengthDone += IoStatusBlock->Information;
    }

  IoStatusBlock->Status = STATUS_SUCCESS;
  IoStatusBlock->Information = LengthDone;
  return IoStatusBlock->Status;
}

NTSTATUS
ImScsiSafeIOStream(IN PFILE_OBJECT FileObject,
IN UCHAR MajorFunction,
IN OUT PIO_STATUS_BLOCK IoStatusBlock,
IN PKEVENT CancelEvent,
OUT PVOID Buffer,
IN ULONG Length)
{
    NTSTATUS status;
    ULONG length_done = 0;
    KEVENT io_complete_event;
    PIO_STACK_LOCATION io_stack;
    LARGE_INTEGER offset = { 0 };
    PVOID wait_object[] = {
	&io_complete_event,
	CancelEvent
    };
    ULONG number_of_wait_objects = CancelEvent != NULL ? 2 : 1;

    //PAGED_CODE();

    KdPrint2(("ImScsiSafeIOStream: FileObject=%#x, MajorFunction=%#x, "
	"IoStatusBlock=%#x, Buffer=%#x, Length=%#x.\n",
	FileObject, MajorFunction, IoStatusBlock, Buffer, Length));

    ASSERT(FileObject != NULL);
    ASSERT(IoStatusBlock != NULL);
    ASSERT(Buffer != NULL);

    KeInitializeEvent(&io_complete_event,
	NotificationEvent,
	FALSE);

    while (length_done < Length)
    {
	ULONG RequestLength = Length - length_done;

	do
	{
	    PIRP irp;
	    PDEVICE_OBJECT device_object = IoGetRelatedDeviceObject(FileObject);

	    KdPrint2(("ImScsiSafeIOStream: Building IRP...\n"));

	    irp = IoBuildSynchronousFsdRequest(
		MajorFunction,
		device_object,
		(PUCHAR)Buffer + length_done,
		RequestLength,
		&offset,
		&io_complete_event,
		IoStatusBlock);

	    if (irp == NULL)
	    {
		KdPrint(("ImScsiSafeIOStream: Error building IRP.\n"));

		IoStatusBlock->Status = STATUS_INSUFFICIENT_RESOURCES;
		IoStatusBlock->Information = length_done;
		return IoStatusBlock->Status;
	    }

	    KdPrint2(("ImScsiSafeIOStream: Built IRP=%#x.\n", irp));

	    io_stack = IoGetNextIrpStackLocation(irp);
	    io_stack->FileObject = FileObject;

	    KdPrint2(("ImScsiSafeIOStream: MajorFunction=%#x, Length=%#x\n",
		io_stack->MajorFunction,
		io_stack->Parameters.Read.Length));

	    KeResetEvent(&io_complete_event);

	    status = IoCallDriver(device_object, irp);

	    if (status == STATUS_PENDING)
	    {
		status = KeWaitForMultipleObjects(number_of_wait_objects,
		    wait_object,
		    WaitAny,
		    Executive,
		    KernelMode,
		    FALSE,
		    NULL,
		    NULL);

		if (KeReadStateEvent(&io_complete_event) == 0)
		{
		    IoCancelIrp(irp);
		    KeWaitForSingleObject(&io_complete_event,
			Executive,
			KernelMode,
			FALSE,
			NULL);
		}
	    }
	    else if (!NT_SUCCESS(status))
		break;

	    status = IoStatusBlock->Status;

	    KdPrint2(("ImScsiSafeIOStream: IRP %#x completed. Status=0x%X.\n",
		irp, IoStatusBlock->Status));

	    RequestLength >>= 1;
	} while ((status == STATUS_INVALID_BUFFER_SIZE) |
	    (status == STATUS_INVALID_PARAMETER));

	if (!NT_SUCCESS(status))
	{
	    KdPrint2(("ImScsiSafeIOStream: I/O failed. Status=0x%X.\n", status));

	    IoStatusBlock->Status = status;
	    IoStatusBlock->Information = 0;
	    return IoStatusBlock->Status;
	}

	KdPrint2(("ImScsiSafeIOStream: I/O done. Status=0x%X. Length=0x%X\n",
	    status, IoStatusBlock->Information));

	if (IoStatusBlock->Information == 0)
	{
	    IoStatusBlock->Status = STATUS_CONNECTION_RESET;
	    IoStatusBlock->Information = 0;
	    return IoStatusBlock->Status;
	}

	length_done += (ULONG)IoStatusBlock->Information;
    }

    KdPrint2(("ImScsiSafeIOStream: I/O complete.\n"));

    IoStatusBlock->Status = STATUS_SUCCESS;
    IoStatusBlock->Information = length_done;
    return IoStatusBlock->Status;
}

