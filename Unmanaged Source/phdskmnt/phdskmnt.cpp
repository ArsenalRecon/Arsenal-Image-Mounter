
/// imscsi.c
/// Driver entry routines, miniport callback definitions and other support
/// routines.
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

#ifdef USE_STORPORT
BOOLEAN PortSupportsGetOriginalMdl = TRUE;
#endif

#ifdef USE_SCSIPORT

NTSTATUS
ImScsiGetControllerObject()
{
    PDEVICE_OBJECT dev_obj;

    if (pMPDrvInfoGlobal->ControllerObject != NULL)
    {
        return STATUS_SUCCESS;
    }

    KdBreakPoint();

    for (dev_obj = pMPDrvInfoGlobal->pDriverObj->DeviceObject;
        dev_obj != NULL;
#pragma warning(suppress: 28175)
        dev_obj = dev_obj->NextDevice)
    {
        if (dev_obj->DeviceType == FILE_DEVICE_CONTROLLER)
        {
            ObReferenceObject(dev_obj);

            pMPDrvInfoGlobal->ControllerObject = dev_obj;

#if DBG
            {
                POBJECT_NAME_INFORMATION objstr = (POBJECT_NAME_INFORMATION)
                    ExAllocatePoolWithTag(NonPagedPool,
                        1024, MP_TAG_GENERAL);

                if (objstr != NULL)
                {
                    ULONG retl;
                    NTSTATUS status;

                    status = ObQueryNameString(pMPDrvInfoGlobal->ControllerObject,
                        objstr, 1024, &retl);

                    if (NT_SUCCESS(status))
                    {
                        DbgPrint("PhDskMnt::ImScsiGetControllerObject: Successfully opened '%wZ'.\n",
                            &objstr->Name);
                    }

                    ExFreePoolWithTag(objstr, MP_TAG_GENERAL);
                }
            }
#endif

            return STATUS_SUCCESS;
        }
    }
    

    DbgPrint("PhDskMnt::ImScsiGetControllerObject: Could not locate SCSI adapter device object by name.\n");

    return STATUS_DEVICE_DOES_NOT_EXIST;
}

#endif

BOOLEAN
ImScsiVirtualDrivesPresent(__inout __deref PKIRQL LowestAssumedIrql)
{
    PLIST_ENTRY           list_ptr;
    BOOLEAN		  result = FALSE;
    KLOCK_QUEUE_HANDLE    LockHandle;

    ImScsiAcquireLock(                   // Serialize the linked list of HBA.
        &pMPDrvInfoGlobal->DrvInfoLock, &LockHandle, *LowestAssumedIrql);

    for (list_ptr = pMPDrvInfoGlobal->ListMPHBAObj.Flink;
        list_ptr != &pMPDrvInfoGlobal->ListMPHBAObj;
        list_ptr = list_ptr->Flink
        )
    {
        pHW_HBA_EXT pHBAExt;

        pHBAExt = CONTAINING_RECORD(list_ptr, HW_HBA_EXT, List);

        if (!IsListEmpty(&pHBAExt->LUList))
        {
            result = TRUE;
            break;
        }
    }

    ImScsiReleaseLock(&LockHandle, LowestAssumedIrql);

    return result;
}

VOID
ImScsiFreeGlobalResources()
{
    KdPrint(("PhDskMnt::ImScsiFreeGlobalResources: Unloading.\n"));

    if (pMPDrvInfoGlobal != NULL)
    {
        KdPrint(("PhDskMnt::ImScsiFreeGlobalResources: Ready to stop worker threads and free global data.\n"));

        if ((pMPDrvInfoGlobal->GlobalsInitialized) &&
            (pMPDrvInfoGlobal->WorkerThread != NULL))
        {

            KdPrint(("PhDskMnt::ImScsiFreeGlobalResources: Waiting for global worker thread %p.\n",
                pMPDrvInfoGlobal->WorkerThread));

#pragma warning(suppress: 28160)
            KeSetEvent(&pMPDrvInfoGlobal->StopWorker, (KPRIORITY)0, TRUE);

            KeWaitForSingleObject(
                pMPDrvInfoGlobal->WorkerThread,
                Executive,
                KernelMode,
                FALSE,
                NULL);

            ObDereferenceObject(pMPDrvInfoGlobal->WorkerThread);
            pMPDrvInfoGlobal->WorkerThread = NULL;
        }

#ifdef USE_SCSIPORT
        if (pMPDrvInfoGlobal->ControllerObject != NULL)
        {
            ObDereferenceObject(pMPDrvInfoGlobal->ControllerObject);
            pMPDrvInfoGlobal->ControllerObject = NULL;
        }
#endif

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

    KdPrint(("PhDskMnt::DriverEntry: Begin '%wZ'.\n", pRegistryPath));
    
#if DBG

#if (NTDDI_VERSION >= NTDDI_WS03)
    KdRefreshDebuggerNotPresent();
#endif

    if (!KD_DEBUGGER_NOT_PRESENT)
        DbgBreakPoint();
#endif

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
#ifdef NT4_COMPATIBLE
    hwInitData.HwInitializationDataSize = FIELD_OFFSET(HW_INITIALIZATION_DATA, HwAdapterControl);
#else
    hwInitData.HwInitializationDataSize = sizeof(HW_INITIALIZATION_DATA);
#endif
#endif

    hwInitData.HwInitialize = MpHwInitialize;           // Required for all ports.
    hwInitData.HwStartIo = MpHwStartIo;              // Required for all ports.
    hwInitData.HwFindAdapter = MpHwFindAdapter;          // Required for all ports.
    hwInitData.HwResetBus = MpHwResetBus;             // Required for all ports.
#ifndef NT4_COMPATIBLE
    hwInitData.HwAdapterControl = MpHwAdapterControl;       // Required for all > NT4 ports.
#endif

#ifdef USE_STORPORT
    hwInitData.HwFreeAdapterResources = MpHwFreeAdapterResources; // Required for virtual StorPort.
#endif

    hwInitData.AutoRequestSense = TRUE;

    hwInitData.TaggedQueuing = TRUE;
    hwInitData.MultipleRequestPerLu = TRUE;

    hwInitData.MapBuffers = STORAGE_MAP_BUFFERS_SETTING;

    hwInitData.DeviceExtensionSize = sizeof(HW_HBA_EXT);
    hwInitData.SpecificLuExtensionSize = sizeof(PVOID);
    hwInitData.SrbExtensionSize = sizeof(HW_SRB_EXTENSION);

    hwInitData.AdapterInterfaceType = STORAGE_INTERFACE_TYPE;

    status = StoragePortInitialize(                     // Tell port driver we're here.
        pDrvObj,
        pRegistryPath,
        (PHW_INITIALIZATION_DATA)&hwInitData,
        NULL
        );

    DbgPrint("PhDskMnt::DriverEntry: StoragePortInitialize returned 0x%X\n", status);

    if (NT_SUCCESS(status))
    {
        // Register our own dispatch hooks

        pMPDrvInfo->pChainUnload = pDrvObj->DriverUnload;
        pMPDrvInfo->pChainDeviceControl = pDrvObj->MajorFunction[IRP_MJ_DEVICE_CONTROL];

        pDrvObj->DriverUnload = ImScsiUnload;
        pDrvObj->MajorFunction[IRP_MJ_DEVICE_CONTROL] = ImScsiDeviceControl;
    }
    else
    {
        ImScsiFreeGlobalResources();
    }

    KdPrint2(("PhDskMnt::DriverEntry: End. status=0x%X\n", status));

    return status;
}                                                     // End DriverEntry().

NTSTATUS
ImScsiDeviceControl(PDEVICE_OBJECT DeviceObject, PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    NTSTATUS status;

    if (io_stack->MajorFunction == IRP_MJ_DEVICE_CONTROL)
    {
        PSRB_IMSCSI_CREATE_DATA create_data =
            (PSRB_IMSCSI_CREATE_DATA)Irp->AssociatedIrp.SystemBuffer;

        ULONG size = max(io_stack->Parameters.DeviceIoControl.OutputBufferLength,
            io_stack->Parameters.DeviceIoControl.InputBufferLength);

        if (Irp->RequestorMode == KernelMode &&
            io_stack->Parameters.DeviceIoControl.IoControlCode == IOCTL_SCSI_MINIPORT &&
            size >= sizeof(*create_data) &&
            memcmp(create_data->SrbIoControl.Signature, IMSCSI_FUNCTION_SIGNATURE, sizeof(create_data->SrbIoControl.Signature)) == 0 &&
            create_data->SrbIoControl.ControlCode == SMP_IMSCSI_QUERY_DEVICE)
        {
            KLOCK_QUEUE_HANDLE LockHandle;
            KIRQL LowestAssumedIrql = PASSIVE_LEVEL;
            PLIST_ENTRY list_ptr;
            pHW_HBA_EXT pHBAExt = NULL;

            ImScsiAcquireLock(                   // Serialize the linked list of HBA.
                &pMPDrvInfoGlobal->DrvInfoLock, &LockHandle, LowestAssumedIrql);

            for (list_ptr = pMPDrvInfoGlobal->ListMPHBAObj.Flink;
                list_ptr != &pMPDrvInfoGlobal->ListMPHBAObj;
                list_ptr = list_ptr->Flink
                )
            {
                pHBAExt = CONTAINING_RECORD(list_ptr, HW_HBA_EXT, List);
                if (!IsListEmpty(&pHBAExt->LUList))
                {
                    break;
                }

                pHBAExt = NULL;
            }

            ImScsiReleaseLock(&LockHandle, &LowestAssumedIrql);
         
            if (pHBAExt == NULL)
            {
                status = STATUS_INVALID_DEVICE_REQUEST;
                size = 0;
            }
            else
            {
                status = ImScsiQueryDevice(pHBAExt, create_data, &size, &LowestAssumedIrql);
            }

            Irp->IoStatus.Information = size;
            Irp->IoStatus.Status = status;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
        }
        else
        {
            status = pMPDrvInfoGlobal->pChainDeviceControl(DeviceObject, Irp);
        }
    }
    else
    {
        status = STATUS_DRIVER_INTERNAL_ERROR;
        Irp->IoStatus.Status = status;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
    }

    return status;
}

/**************************************************************************************************/
/*                                                                                                */
/* Unload routine                                                                                 */
/*                                                                                                */
/**************************************************************************************************/
VOID
ImScsiUnload(PDRIVER_OBJECT pDrvObj)
{
    DbgPrint("PhDskMnt::ImScsiUnload.\n");

    if ((pMPDrvInfoGlobal != NULL) &&
        (pMPDrvInfoGlobal->pChainUnload != NULL))
    {
        KdPrint(("PhDskMnt::ImScsiUnload: Calling next in chain 0x%p.\n",
            pMPDrvInfoGlobal->pChainUnload));
        pMPDrvInfoGlobal->pChainUnload(pDrvObj);
    }

    // Free our own resources
    ImScsiFreeGlobalResources();

    DbgPrint("PhDskMnt::ImScsiUnload: Done.\n");
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
__inout __deref PPORT_CONFIGURATION_INFORMATION pConfigInfo,
__out      PBOOLEAN                        pBAgain
)
{
    ULONG              i,
        len,
        status = SP_RETURN_FOUND;
    PCHAR              pChar;
    pHW_HBA_EXT        pHBAExt = (pHW_HBA_EXT)DeviceExtension;
    NTSTATUS           ntstatus;
    KLOCK_QUEUE_HANDLE LockHandle;
    KIRQL              lowest_assumed_irql = PASSIVE_LEVEL;

    UNREFERENCED_PARAMETER(pReservedArg1);
    UNREFERENCED_PARAMETER(pReservedArg2);
#ifdef USE_STORPORT
    UNREFERENCED_PARAMETER(pReservedArg3);
#endif
    UNREFERENCED_PARAMETER(ArgumentString);

    KdPrint(("PhDskMnt::MpHwFindAdapter: Arg=%s%s%s, pHBAExt = 0x%p, pConfigInfo = 0x%p, IRQL=%i\n",
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

    pConfigInfo->WmiDataProvider = FALSE;                       // Indicate WMI provider.

    pConfigInfo->Master = TRUE;

    pConfigInfo->NumberOfPhysicalBreaks = 4096;

    pConfigInfo->MaximumTransferLength = 8 << 20;                     // 8 MB.

#ifdef USE_STORPORT

    pConfigInfo->VirtualDevice = TRUE;                        // Indicate no real hardware.
    pConfigInfo->SynchronizationModel = StorSynchronizeFullDuplex;

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

    pConfigInfo->AlignmentMask = 0x3;                         // Indicate DWORD alignment.
    pConfigInfo->CachesData = FALSE;                       // Indicate miniport wants flush and shutdown notification.
    pConfigInfo->MaximumNumberOfTargets = SCSI_MAXIMUM_TARGETS;        // Indicate maximum targets.
    pConfigInfo->NumberOfBuses =
        (UCHAR)pMPDrvInfoGlobal->MPRegInfo.NumberOfBuses;                     // Indicate number of buses.
    pConfigInfo->ScatterGather = TRUE;                        // Indicate scatter-gather (explicit setting needed for Win2003 at least).
    pConfigInfo->AutoRequestSense = TRUE;
    pConfigInfo->TaggedQueuing = TRUE;
    pConfigInfo->MultipleRequestPerLu = TRUE;

    // Save Vendor Id, Product Id, Revision in device extension.

    pChar = (PCHAR)pMPDrvInfoGlobal->MPRegInfo.VendorId.Buffer;
    len = min(8, (pMPDrvInfoGlobal->MPRegInfo.VendorId.Length / 2));
    for (i = 0; i < len; i++, pChar += 2)
        pHBAExt->VendorId[i] = *pChar;

    pChar = (PCHAR)pMPDrvInfoGlobal->MPRegInfo.ProductId.Buffer;
    len = min(16, (pMPDrvInfoGlobal->MPRegInfo.ProductId.Length / 2));
    for (i = 0; i < len; i++, pChar += 2)
        pHBAExt->ProductId[i] = *pChar;

    pChar = (PCHAR)pMPDrvInfoGlobal->MPRegInfo.ProductRevision.Buffer;
    len = min(4, (pMPDrvInfoGlobal->MPRegInfo.ProductRevision.Length / 2));
    for (i = 0; i < len; i++, pChar += 2)
        pHBAExt->ProductRevision[i] = *pChar;

    // Add HBA extension to master driver object's linked list.

    ImScsiAcquireLock(&pMPDrvInfoGlobal->DrvInfoLock, &LockHandle, lowest_assumed_irql);

    InsertTailList(&pMPDrvInfoGlobal->ListMPHBAObj, &pHBAExt->List);

    pMPDrvInfoGlobal->DrvInfoNbrMPHBAObj++;

    ImScsiReleaseLock(&LockHandle, &lowest_assumed_irql);

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
            (ACCESS_MASK)0L,
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
            ntstatus = ObReferenceObjectByHandle(
                thread_handle,
                FILE_READ_ATTRIBUTES | SYNCHRONIZE,
                *PsThreadType,
                KernelMode,
                (PVOID*)&pMPDrvInfoGlobal->WorkerThread,
                NULL
                );

            if (!NT_SUCCESS(ntstatus))
            {
                DbgPrint("PhDskMnt::ScsiGetLUExtension: Cannot reference worker thread. (%#x)\n", ntstatus);
                KeSetEvent(&pMPDrvInfoGlobal->StopWorker, (KPRIORITY)0, FALSE);
                ZwWaitForSingleObject(thread_handle, FALSE, NULL);

                status = SP_RETURN_ERROR;
            }

            ZwClose(thread_handle);
        }
    }

    //Done:
    *pBAgain = FALSE;

    KdPrint(("PhDskMnt::MpHwFindAdapter: End, status = 0x%X\n", status));

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
    __in PVOID              DeviceExtension,       // Adapter device-object extension from port driver.
    __in ULONG              BusId
    )
{
    UNREFERENCED_PARAMETER(DeviceExtension);
    UNREFERENCED_PARAMETER(BusId);

    // To do: At some future point, it may be worthwhile to ensure that any SRBs being handled be completed at once.
    //        Practically speaking, however, it seems that the only SRBs that would not be completed very quickly
    //        would be those handled by the worker thread. In the future, therefore, there might be a global flag
    //        set here to instruct the thread to complete outstanding I/Os as they appear; but a period for that
    //        happening would have to be devised (such completion shouldn't be unbounded).

    DbgPrint("PhDskMnt::MpHwResetBus:  pHBAExt = 0x%p, BusId = %u. Ignored.\n", DeviceExtension, BusId);

    return TRUE;
}                                               // End MpHwResetBus().
#else
BOOLEAN
MpHwResetBus(
    __in PVOID              DeviceExtension,       // Adapter device-object extension from port driver.
    __in ULONG              BusId
    )
{
    // To do: At some future point, it may be worthwhile to ensure that any SRBs being handled be completed at once.
    //        Practically speaking, however, it seems that the only SRBs that would not be completed very quickly
    //        would be those handled by the worker thread. In the future, therefore, there might be a global flag
    //        set here to instruct the thread to complete outstanding I/Os as they appear; but a period for that
    //        happening would have to be devised (such completion shouldn't be unbounded).

    DbgPrint("PhDskMnt::MpHwResetBus:  pHBAExt = 0x%p, BusId = %u. Calling ScsiPortCompleteRequest().\n", DeviceExtension, BusId);

    ScsiPortCompleteRequest(DeviceExtension,
        (UCHAR)BusId,
        SP_UNTAGGED,
        SP_UNTAGGED,
        SRB_STATUS_BUS_RESET);

    return TRUE;
}                                                     // End MpHwResetBus().
#endif

#ifdef USE_SCSIPORT
LONG
ImScsiCompletePendingSrbs(
__in pHW_HBA_EXT pHBAExt,  // Adapter device-object extension from port driver.
__inout __deref PKIRQL LowestAssumedIrql
)
{
    pMP_WorkRtnParms pWkRtnParms;
    LONG done = 0;

    KdPrint2(("PhDskMnt::ImScsiCompletePendingSrbs start. pHBAExt = 0x%p\n", pHBAExt));

    for (;;)
    {
        KLOCK_QUEUE_HANDLE lock_handle;
        PLIST_ENTRY request;

        ImScsiAcquireLock(&pMPDrvInfoGlobal->ResponseListLock,
            &lock_handle, *LowestAssumedIrql);

        request = RemoveHeadList(&pMPDrvInfoGlobal->ResponseList);
        if (request == &pMPDrvInfoGlobal->ResponseList)
        {
            request = NULL;
        }

        ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

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
    KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

    KdPrint2(("PhDskMnt::MpHwTimer start. pHBAExt = 0x%p\n", pHBAExt));

    pending = ImScsiCompletePendingSrbs(pHBAExt, &lowest_assumed_irql);

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
__in                PVOID                DeviceExtension,  // Adapter device-object extension from port driver.
__inout __deref     PSCSI_REQUEST_BLOCK  pSrb
)
{
    KIRQL                   lowest_assumed_irql = PASSIVE_LEVEL;
    ResultType              Result = ResultDone;
    pHW_HBA_EXT             pHBAExt = (pHW_HBA_EXT)DeviceExtension;
#ifdef USE_SCSIPORT
    UCHAR                   PathId = pSrb->PathId;
    UCHAR                   TargetId = pSrb->TargetId;
    UCHAR                   Lun = pSrb->Lun;
#endif

    KdPrint2(("PhDskMnt::MpHwStartIo:  pHBAExt = 0x%p, pSrb = 0x%p, Path=%i, Target=%i, Lun=%i, IRQL=%i\n",
        pHBAExt,
        pSrb,
        (int)pSrb->PathId,
        (int)pSrb->TargetId,
        (int)pSrb->Lun,
        KeGetCurrentIrql()));

    pSrb->SrbStatus = SRB_STATUS_PENDING;
    pSrb->ScsiStatus = SCSISTAT_GOOD;

    ImScsiCompletePendingSrbs(pHBAExt, &lowest_assumed_irql);

    _InterlockedExchangeAdd((volatile LONG *)&pHBAExt->SRBsSeen, 1);   // Bump count of SRBs encountered.

    // Next, if true, will cause port driver to remove the associated LUNs if, for example, devmgmt.msc is asked "scan for hardware changes."
    //if (pHBAExt->bDontReport)
    //{                       // Act as though the HBA/path is gone?
    //    pSrb->SrbStatus = SRB_STATUS_NO_DEVICE;
    //    goto done;
    //}

    switch (pSrb->Function)
    {
    case SRB_FUNCTION_IO_CONTROL:
        ScsiIoControl(pHBAExt, pSrb, &Result, &lowest_assumed_irql);
        break;

    case SRB_FUNCTION_EXECUTE_SCSI:
        if (pSrb->Cdb[0] == SCSIOP_REPORT_LUNS)
        {
            ScsiOpReportLuns(pHBAExt, pSrb, &lowest_assumed_irql);
        }
        else
        {
            ScsiExecute(pHBAExt, pSrb, &Result, &lowest_assumed_irql);
        }
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
        ScsiPnP(pHBAExt, (PSCSI_PNP_REQUEST_BLOCK)pSrb, &lowest_assumed_irql);
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

        //StorPortLogError(pHBAExt, pSrb, pSrb->PathId, pSrb->TargetId, pSrb->Lun, SP_PROTOCOL_ERROR, 0x0200 | pSrb->Cdb[0]);

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
        ScsiPortNotification(RequestTimerCall, pHBAExt, MpHwTimer, (ULONG)1);
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
__in PVOID                      DeviceExtension, // Adapter device-object extension from port driver.
__in SCSI_ADAPTER_CONTROL_TYPE  ControlType,
__in PVOID                      pParameters
)
{
    pHW_HBA_EXT                 pHBAExt = (pHW_HBA_EXT)DeviceExtension;
    ULONG                       i;

    KdPrint2(("PhDskMnt::MpHwAdapterControl:  pHBAExt = 0x%p, ControlType = 0x%p, pParameters=0x%p\n", pHBAExt, ControlType, pParameters));

    pHBAExt->AdapterState = ControlType;

    switch (ControlType)
    {
    case ScsiQuerySupportedControlTypes:
    {
        PSCSI_SUPPORTED_CONTROL_TYPE_LIST pCtlTypList =
            (PSCSI_SUPPORTED_CONTROL_TYPE_LIST)pParameters;

        KdPrint2(("PhDskMnt::MpHwAdapterControl: ScsiQuerySupportedControlTypes\n"));

        // Get pointer to control type list
        // Cycle through list to set TRUE for each type supported
        // making sure not to go past the MaxControlType
        for (i = 0; i < pCtlTypList->MaxControlType; i++)
        {
            if (i == ScsiQuerySupportedControlTypes ||
                i == ScsiStopAdapter || i == ScsiRestartAdapter ||
                i == ScsiSetBootConfig || i == ScsiSetRunningConfig)
            {
                pCtlTypList->SupportedTypeList[i] = TRUE;
            }
        }
        break;
    }

    case ScsiStopAdapter:
        KdPrint2(("PhDskMnt::MpHwAdapterControl: ScsiStopAdapter\n"));

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
__in pHW_HBA_EXT pHBAExt,         // Adapter device-object extension from port driver.
__inout __deref PKIRQL LowestAssumedIrql
)
{
    DEVICE_NUMBER rem_data;

    KdPrint2(("PhDskMnt::ImScsiStopAdapter:  pHBAExt = 0x%p\n", pHBAExt));

    // Remove all devices, using "wildcard" device number.
    rem_data.LongNumber = IMSCSI_ALL_DEVICES;

    ImScsiRemoveDevice(pHBAExt, &rem_data, LowestAssumedIrql);

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
MpHwFreeAdapterResources(__in PVOID DeviceExtension)
{
    PLIST_ENTRY         pNextEntry;
    pHW_HBA_EXT         pLclHBAExt;
    KLOCK_QUEUE_HANDLE  LockHandle;
    KIRQL               lowest_assumed_irql = PASSIVE_LEVEL;
    pHW_HBA_EXT         pHBAExt = (pHW_HBA_EXT)DeviceExtension;

    KdPrint2(("PhDskMnt::MpHwFreeAdapterResources:  pHBAExt = 0x%p\n", pHBAExt));

    // Free memory allocated for disk
    ImScsiStopAdapter(pHBAExt, &lowest_assumed_irql);

    ImScsiAcquireLock(&pMPDrvInfoGlobal->DrvInfoLock, &LockHandle, lowest_assumed_irql);

    for (                                             // Go through linked list of HBA extensions.
        pNextEntry = pMPDrvInfoGlobal->ListMPHBAObj.Flink;
        pNextEntry != &pMPDrvInfoGlobal->ListMPHBAObj;
        pNextEntry = pNextEntry->Flink
        )
    {
        pLclHBAExt = CONTAINING_RECORD(pNextEntry, HW_HBA_EXT, List);

        if (pLclHBAExt == pHBAExt)
        {                    // Is this entry the same as pHBAExt?
            RemoveEntryList(pNextEntry);
            pMPDrvInfoGlobal->DrvInfoNbrMPHBAObj--;
            break;
        }
    }

    ImScsiReleaseLock(&LockHandle, &lowest_assumed_irql);

}                                                     // End MpHwFreeAdapterResources().
#endif

NTSTATUS
ImScsiGetDiskSize(
__in HANDLE FileHandle,
__out __deref PIO_STATUS_BLOCK IoStatus,
__inout __deref PLARGE_INTEGER DiskSize)
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

        KdPrint(("PhDskMnt::FileStandardInformation not supported for "
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

        KdPrint(("PhDskMnt::IOCTL_DISK_GET_LENGTH_INFO not supported "
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

        KdPrint(("PhDskMnt::IOCTL_DISK_GET_PARTITION_INFO not supported "
            "for target device. %#x\n", status));
    }

    return status;
}

VOID
ImScsiLogDbgError(__in __deref PVOID Object,
 UCHAR MajorFunctionCode,
 UCHAR RetryCount,
 PULONG DumpData,
 USHORT DumpDataSize,
 USHORT EventCategory,
 NTSTATUS ErrorCode,
 ULONG UniqueErrorValue,
 NTSTATUS FinalStatus,
 ULONG SequenceNumber,
 ULONG IoControlCode,
 PLARGE_INTEGER DeviceOffset,
 PWCHAR Message)
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
        KdPrint(("PhDskMnt::Warning: Too large error log packet.\n"));
        return;
    }

    error_log_packet =
        (PIO_ERROR_LOG_PACKET)IoAllocateErrorLogEntry(Object,
        (UCHAR)packet_size);

    if (error_log_packet == NULL)
    {
        KdPrint(("PhDskMnt::Warning: IoAllocateErrorLogEntry() returned NULL.\n"));
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
ImScsiSafeReadFile(__in HANDLE FileHandle,
__out __deref PIO_STATUS_BLOCK IoStatusBlock,
PVOID Buffer,
__in SIZE_T Length,
__in __deref PLARGE_INTEGER Offset)
{
    NTSTATUS status = STATUS_SUCCESS;
    SIZE_T length_done = 0;
    PUCHAR intermediate_buffer = NULL;
    ULONG request_length;

    ASSERT(FileHandle != NULL);
    ASSERT(IoStatusBlock != NULL);
    ASSERT(Buffer != NULL);

    if (Length > (8UL << 20))
    {
        request_length = (8UL << 20);
    }
    else
    {
        request_length = (ULONG)Length;
    }

    while (length_done < Length)
    {
        SIZE_T LongRequestLength = Length - length_done;
        if (LongRequestLength < request_length)
        {
            request_length = (ULONG)LongRequestLength;
        }

        for (;;)
        {
            LARGE_INTEGER current_file_offset;

            current_file_offset.QuadPart = Offset->QuadPart + length_done;

            if (intermediate_buffer == NULL)
            {
                intermediate_buffer = (PUCHAR)ExAllocatePoolWithTag(NonPagedPool,
                    request_length,
                    MP_TAG_GENERAL);

                if (intermediate_buffer == NULL)
                {
                    DbgPrint("PhDskMnt::ImScsiSafeReadFile: Insufficient paged pool to allocate "
                        "intermediate buffer (%u bytes).\n", request_length);

                    IoStatusBlock->Status = STATUS_INSUFFICIENT_RESOURCES;
                    IoStatusBlock->Information = 0;
                    return IoStatusBlock->Status;
                }
            }

            status = ZwReadFile(FileHandle,
                NULL,
                NULL,
                NULL,
                IoStatusBlock,
                intermediate_buffer,
                request_length,
                &current_file_offset,
                NULL);

            if (((status == STATUS_INSUFFICIENT_RESOURCES) ||
                (status == STATUS_INVALID_BUFFER_SIZE) ||
                (status == STATUS_INVALID_PARAMETER)) &&
                (request_length >= 2048))
            {
                ExFreePoolWithTag(intermediate_buffer, MP_TAG_GENERAL);
                intermediate_buffer = NULL;

                DbgPrint("PhDskMnt::ImScsiSafeReadFile: ZwReadFile error reading "
                    "%u bytes. Retrying with smaller read size. (Status 0x%X)\n",
                    request_length,
                    status);

                request_length >>= 2;

                continue;
            }

            if (!NT_SUCCESS(status))
            {
                DbgPrint("PhDskMnt::ImScsiSafeReadFile: ZwReadFile error reading "
                    "%u bytes. (Status 0x%X)\n",
                    request_length,
                    status);

                break;
            }

            RtlCopyMemory((PUCHAR)Buffer + length_done, intermediate_buffer,
                IoStatusBlock->Information);

            break;
        }

        if (!NT_SUCCESS(status))
        {
            IoStatusBlock->Information = length_done;
            break;
        }

        if (IoStatusBlock->Information == 0)
        {
            DbgPrint("PhDskMnt::ImScsiSafeReadFile: IoStatusBlock->Information == 0, "
                "returning STATUS_CONNECTION_RESET.\n");

            status = STATUS_CONNECTION_RESET;
            break;
        }

        KdPrint(("PhDskMnt::ImScsiSafeReadFile: Done %u bytes.\n",
            (ULONG)IoStatusBlock->Information));

        length_done += IoStatusBlock->Information;
    }

    if (intermediate_buffer != NULL)
    {
        ExFreePoolWithTag(intermediate_buffer, MP_TAG_GENERAL);
        intermediate_buffer = NULL;
    }

    if (!NT_SUCCESS(status))
    {
        DbgPrint("PhDskMnt::ImScsiSafeReadFile: Error return "
            "(Status 0x%X)\n", status);
    }
    else
    {
        KdPrint(("PhDskMnt::ImScsiSafeReadFile: Successful.\n"));
    }

    IoStatusBlock->Status = status;
    IoStatusBlock->Information = length_done;
    return status;
}

NTSTATUS
ImScsiSafeIOStream(__in PFILE_OBJECT FileObject,
__in UCHAR MajorFunction,
__out __deref PIO_STATUS_BLOCK IoStatusBlock,
__in PKEVENT CancelEvent,
PVOID Buffer,
__in ULONG Length)
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

#pragma warning(suppress: 6102)
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

            KeClearEvent(&io_complete_event);

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
        } while ((status == STATUS_INVALID_BUFFER_SIZE) ||
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

pMP_WorkRtnParms ImScsiCreateWorkItem(pHW_HBA_EXT pHBAExt,
    pHW_LU_EXTENSION pLUExt,
    PSCSI_REQUEST_BLOCK pSrb)
{
    pMP_WorkRtnParms pWkRtnParms =                                     // Allocate parm area for work routine.
        (pMP_WorkRtnParms)ExAllocatePoolWithTag(NonPagedPool, sizeof(MP_WorkRtnParms), MP_TAG_GENERAL);

    if (pWkRtnParms == NULL)
    {
        DbgPrint("PhDskMnt::ImScsiCreateWorkItem Failed to allocate work parm structure\n");
        return NULL;
    }

    RtlZeroMemory(pWkRtnParms, sizeof(MP_WorkRtnParms));

    pWkRtnParms->pHBAExt = pHBAExt;
    pWkRtnParms->pLUExt = pLUExt;
    pWkRtnParms->pSrb = pSrb;

    return pWkRtnParms;
}

VOID ImScsiScheduleWorkItem(pMP_WorkRtnParms pWkRtnParms, PKIRQL LowestAssumedIrql)
{
    KdPrint2(("PhDskMnt::ImScsiScheduleWorkItem: Queuing work=0x%p\n", pWkRtnParms));

    KLOCK_QUEUE_HANDLE lock_handle;

    if (pWkRtnParms->pLUExt == NULL)
    {
        ImScsiAcquireLock(&pMPDrvInfoGlobal->RequestListLock, &lock_handle, *LowestAssumedIrql);

        InsertTailList(&pMPDrvInfoGlobal->RequestList, &pWkRtnParms->RequestListEntry);

        ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

        KeSetEvent(&pMPDrvInfoGlobal->RequestEvent, (KPRIORITY)0, FALSE);

    }
    else
    {
        ImScsiAcquireLock(&pWkRtnParms->pLUExt->RequestListLock, &lock_handle, *LowestAssumedIrql);

        InsertTailList(&pWkRtnParms->pLUExt->RequestList, &pWkRtnParms->RequestListEntry);

        ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

        KeSetEvent(&pWkRtnParms->pLUExt->RequestEvent, (KPRIORITY)0, FALSE);
    }
}
