
/// imscsi.h
/// Definitions for functions and global constants for use in kernel mode
/// components.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#ifndef _MP_H_
#define _MP_H_

#ifdef __cplusplus
extern "C" {
#endif

#ifndef VERBOSE_DEBUG_TRACE
#define VERBOSE_DEBUG_TRACE 0
#endif

#if VERBOSE_DEBUG_TRACE >= 1
#define KdPrint2(x) KdPrint(x)
#else
#define KdPrint2(x)
#endif

#if !defined(_MP_User_Mode_Only)                      // User-mode only.

#if !defined(_MP_H_skip_WDM_includes)

#pragma warning(disable: 4482)

#pragma warning(push)
#pragma warning(disable: 20051)

#include <ntifs.h>
#include <ntddk.h>
#include <wdm.h>
#include <ntverp.h>

#pragma warning(pop)

#ifdef NT4_COMPATIBLE
#ifdef ExFreePoolWithTag
#undef ExFreePoolWithTag
#endif
#ifdef ExFreePool
#undef ExFreePool
#endif
#define ExFreePoolWithTag(m,t) ExFreePool(m)
#ifdef ObReferenceObject
#undef ObReferenceObject
#endif
#define ObReferenceObject(o) ObReferenceObjectByPointer(o,0,NULL,KernelMode)
#endif

#endif // !defined(_MP_H_skip_WDM_includes)

#include <ntdef.h>
#ifdef USE_SCSIPORT
#include <scsi.h>
#endif
#ifdef USE_STORPORT
#include <storport.h>
#endif
#include <ntdddisk.h>
#include <devioctl.h>
#include <ntddscsi.h>
#include <scsiwmi.h>

#endif

#pragma warning(disable: 28719)
#pragma warning(disable: 4201)

#include "common.h"
#include "imdproxy.h"
#include "phdskmntver.h"

#if !defined(_MP_User_Mode_Only)                      // User-mode only.

#if !defined(_MP_H_skip_includes)

#include <stdio.h>
#include <stdarg.h>

#endif // !defined(_MP_H_skip_includes)

#define VENDOR_ID                   L"Arsenal"
#define VENDOR_ID_ascii             "Arsenal"
#define PRODUCT_ID                  L"Virtual"
#define PRODUCT_ID_ascii            "Virtual"
#define PRODUCT_REV                 L"0001"
#define PRODUCT_REV_ascii           "0001"
#define MP_TAG_GENERAL              'cSmI'

#define MAX_TARGETS                 8
#define MAX_LUNS                    24
#define MP_MAX_TRANSFER_SIZE        (32 * 1024)
#define TIME_INTERVAL               (1 * 1000 * 1000) //1 second.
#define DEVLIST_BUFFER_SIZE         1024
#define DEVICE_NOT_FOUND            0xFF
#define SECTOR_NOT_FOUND            0xFFFF

#define MINIMUM_DISK_SIZE           (1540 * 1024)    // Minimum size required for Disk Manager
#define MAXIMUM_MAP_DISK_SIZE       (256 * 1024)

#define DEFAULT_BLOCK_POWER         9
#define BUF_SIZE                    (1540 * 1024)
#define MAX_BLOCKS                  (BUF_SIZE >> MP_BLOCK_POWER)

#define DEFAULT_SECTOR_SIZE_CD_ROM  2048
#define DEFAULT_SECTOR_SIZE_HDD     512

#define DEFAULT_BREAK_ON_ENTRY      0                // No break
#define DEFAULT_DEBUG_LEVEL         2               
#define DEFAULT_INITIATOR_ID        7
#define DEFAULT_NUMBER_OF_BUSES     1

#define GET_FLAG(Flags, Bit)        ((Flags) & (Bit))
#define SET_FLAG(Flags, Bit)        ((Flags) |= (Bit))
#define CLEAR_FLAG(Flags, Bit)      ((Flags) &= ~(Bit))

#ifndef SCSIOP_UNMAP
#define SCSIOP_UNMAP 0x42
#endif

#ifndef _Dispatch_type_
#define _Dispatch_type_(x)
#endif

    typedef struct _MPDriverInfo         MPDriverInfo, *pMPDriverInfo;
    typedef struct _MP_REG_INFO          MP_REG_INFO, *pMP_REG_INFO;
    typedef struct _HW_LU_EXTENSION      HW_LU_EXTENSION, *pHW_LU_EXTENSION;
    typedef struct _LBA_LIST             LBA_LIST, *PLBA_LIST;

    extern
        pMPDriverInfo pMPDrvInfoGlobal;

    extern
        BOOLEAN PortSupportsGetOriginalMdl;

    typedef struct _MP_REG_INFO {
        UNICODE_STRING   VendorId;
        UNICODE_STRING   ProductId;
        UNICODE_STRING   ProductRevision;
        ULONG            NumberOfBuses;       // Number of buses (paths) supported by this adapter
        ULONG            InitiatorID;        // Adapter's target ID
    } MP_REG_INFO, *pMP_REG_INFO;

    typedef struct _MPDriverInfo {                        // The master miniport object. In effect, an extension of the driver object for the miniport.
        MP_REG_INFO                    MPRegInfo;
        KSPIN_LOCK                     DrvInfoLock;
        LIST_ENTRY                     ListMPHBAObj;      // Header of list of HW_HBA_EXT objects.
        PDRIVER_OBJECT                 pDriverObj;
        PDRIVER_UNLOAD                 pChainUnload;
        PDRIVER_DISPATCH               pChainDeviceControl;
        UCHAR                          GlobalsInitialized;
        KEVENT                         StopWorker;
        PKTHREAD                       WorkerThread;
        LIST_ENTRY                     RequestList;
        KSPIN_LOCK                     RequestListLock;
        KEVENT                         RequestEvent;
#ifdef USE_SCSIPORT
        PDEVICE_OBJECT                 ControllerObject;
        LIST_ENTRY                     ResponseList;
        KSPIN_LOCK                     ResponseListLock;
        KEVENT                         ResponseEvent;
#endif
        ULONG                          DrvInfoNbrMPHBAObj;// Count of items in ListMPHBAObj.
        ULONG                          RandomSeed;
    } MPDriverInfo, *pMPDriverInfo;

    typedef struct _DEVICE_THREAD {
        LIST_ENTRY                     ListEntry;
        PKTHREAD                       Thread;
    } DEVICE_THREAD, *PDEVICE_THREAD;

    typedef struct _LUNInfo {
        UCHAR     bReportLUNsDontUse;
        UCHAR     bIODontUse;
    } LUNInfo, *pLUNInfo;

    typedef struct _HW_HBA_EXT {                          // Adapter device-object extension allocated by port driver.
        LIST_ENTRY                     List;              // Pointers to next and previous HW_HBA_EXT objects.
        LIST_ENTRY                     LUList;
        KSPIN_LOCK                     LUListLock;
#ifdef USE_SCSIPORT
        LONG                           WorkItems;
#endif
        PDRIVER_OBJECT                 DriverObject;
        ULONG                          SRBsSeen;
        UCHAR                          HostTargetId;
        SCSI_ADAPTER_CONTROL_TYPE      AdapterState;
        UCHAR                          VendorId[9];
        UCHAR                          ProductId[17];
        UCHAR                          ProductRevision[5];
        BOOLEAN                        bReportAdapterDone;
    } HW_HBA_EXT, *pHW_HBA_EXT;

    // Flag definitions for LUFlags.

#define LU_DEVICE_INITIALIZED   0x0001

    typedef struct _PROXY_CONNECTION
    {
        enum PROXY_CONNECTION_TYPE
        {
            PROXY_CONNECTION_DEVICE,
            PROXY_CONNECTION_SHM
        } connection_type;       // Connection type

        union
        {
            // Valid if connection_type is PROXY_CONNECTION_DEVICE
            PFILE_OBJECT device;     // Pointer to proxy communication object

                                     // Valid if connection_type is PROXY_CONNECTION_SHM
            struct
            {
                HANDLE request_event_handle;
                PKEVENT request_event;
                HANDLE response_event_handle;
                PKEVENT response_event;
                PUCHAR shared_memory;
                ULONG_PTR shared_memory_size;
            };
        };
    } PROXY_CONNECTION, *PPROXY_CONNECTION;

    typedef struct _HW_LU_EXTENSION {                     // LUN extension allocated by port driver.
        LIST_ENTRY            List;                       // Pointers to next and previous HW_LU_EXTENSION objects, used in HW_HBA_EXT.
        pHW_HBA_EXT           pHBAExt;
        LIST_ENTRY            RequestList;
        KSPIN_LOCK            RequestListLock;
        KEVENT                RequestEvent;
        KEVENT                Initialized;
        PKTHREAD              WorkerThread;
        KEVENT                StopThread;
        LARGE_INTEGER         ImageOffset;
        LARGE_INTEGER         DiskSize;
        UCHAR                 BlockPower;
        ULONG                 Flags;
        UCHAR                 DeviceType;
        BOOLEAN               RemovableMedia;
        BOOLEAN               ReadOnly;
        ULONG                 FakeDiskSignature;
        UCHAR                 LastReportedEvent;
        PVOID                 LastIoBuffer;
        LONGLONG              LastIoStartSector;
        ULONG                 LastIoLength;
        KSPIN_LOCK            LastIoLock;
        DEVICE_NUMBER         DeviceNumber;
        UNICODE_STRING        ObjectName;
        HANDLE                ImageFile;
        PROXY_CONNECTION      Proxy;
        BOOLEAN               VMDisk;
        BOOLEAN               AWEAllocDisk;
        BOOLEAN               SharedImage;
        HANDLE                ReservationKeyFile;
        ULONG                 Generation;
        ULONGLONG             RegistrationKey;
        ULONGLONG             ReservationKey;
        UCHAR                 ReservationType;
        UCHAR                 ReservationScope;
        BOOLEAN               Modified;
        BOOLEAN               SupportsUnmap;
        BOOLEAN               SupportsZero;
        BOOLEAN               NoFileLevelTrim;
        BOOLEAN               ProvisioningType;
        PUCHAR                ImageBuffer;
        BOOLEAN               UseProxy;
        PFILE_OBJECT          FileObject;
        UCHAR                 UniqueId[16];
        CHAR                  GuidString[39];
        UNICODE_STRING        WriteOverlayFileName;
        HANDLE                WriteOverlay;
    } HW_LU_EXTENSION, *pHW_LU_EXTENSION;

    typedef struct _HW_SRB_EXTENSION {
        SCSIWMI_REQUEST_CONTEXT WmiRequestContext;
    } HW_SRB_EXTENSION, *PHW_SRB_EXTENSION;

    typedef struct _MP_WorkRtnParms {
        LIST_ENTRY           RequestListEntry;
#ifdef USE_SCSIPORT
        LIST_ENTRY           ResponseListEntry;
#endif
        pHW_HBA_EXT          pHBAExt;
        pHW_LU_EXTENSION     pLUExt;
        PSCSI_REQUEST_BLOCK  pSrb;
        PMDL                 pOriginalMdl;
        PETHREAD             pReqThread;
        KIRQL                LowestAssumedIrql;
        PVOID                MappedSystemBuffer;
        PVOID                AllocatedBuffer;
        BOOLEAN              CopyBack;
        PKEVENT              CallerWaitEvent;
    } MP_WorkRtnParms, *pMP_WorkRtnParms;

    typedef enum ResultType {
        ResultDone,
        ResultQueued
    } *pResultType;

#define RegWkBfrSz  0x1000

    typedef struct _RegWorkBuffer {
        pHW_HBA_EXT          pAdapterExt;
        UCHAR                Work[256];
    } RegWorkBuffer, *pRegWorkBuffer;

    IO_COMPLETION_ROUTINE
        ImScsiIoCtlCallCompletion;

    _Dispatch_type_(IRP_MJ_DEVICE_CONTROL) DRIVER_DISPATCH
        ImScsiDeviceControl;

    DRIVER_UNLOAD
        ImScsiUnload;

    EXTERN_C DRIVER_INITIALIZE
        DriverEntry;

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
            );

    VOID
        MpHwTimer(
            __in pHW_HBA_EXT DevExt
            );

    BOOLEAN
        MpHwInitialize(
            __in PVOID
            );

    void
        MpHwReportAdapter(
            __in pHW_HBA_EXT
            );

    void
        MpHwReportLink(
            __in pHW_HBA_EXT
            );

    void
        MpHwReportLog(__in pHW_HBA_EXT);

    VOID
        MpHwFreeAdapterResources(
            __in PVOID DeviceExtension
            );

    BOOLEAN
        MpHwStartIo(
            __in PVOID DeviceExtension,
            __inout __deref PSCSI_REQUEST_BLOCK Srb
            );

    UCHAR
        ScsiResetLun(
            __in PVOID,
            __in PSCSI_REQUEST_BLOCK
            );

    UCHAR
        ScsiResetDevice(
            __in PVOID,
            __in PSCSI_REQUEST_BLOCK
            );

    BOOLEAN
        MpHwResetBus(
            __in PVOID DeviceExtension,
            __in ULONG PathId
            );

    BOOLEAN
        MpHwAdapterState(
            __in  PVOID HwDeviceExtension,
            __in  PVOID Context,
            __in  BOOLEAN SaveState
            );

    SCSI_ADAPTER_CONTROL_STATUS
        MpHwAdapterControl(
            __in PVOID DeviceExtension,
            __in SCSI_ADAPTER_CONTROL_TYPE ControlType,
            __in PVOID Parameters
            );

    VOID
        ScsiIoControl(
            __in pHW_HBA_EXT DevExt,
            __in PSCSI_REQUEST_BLOCK,
            __in pResultType,
            __inout __deref PKIRQL
            );

    VOID
        ScsiExecute(
            __in pHW_HBA_EXT DevExt,
            __in PSCSI_REQUEST_BLOCK,
            __in pResultType,
            __in PKIRQL
            );

    VOID
        ScsiPnP(
            __in pHW_HBA_EXT              pHBAExt,
            __in PSCSI_PNP_REQUEST_BLOCK  pSrb,
            __inout __deref PKIRQL             LowestAssumedIrql
            );

    VOID
        ScsiOpStartStopUnit(
            __in pHW_HBA_EXT DevExt,
            __in pHW_LU_EXTENSION LuExt,
            __in PSCSI_REQUEST_BLOCK Srb,
            __inout __deref PKIRQL
            );

    VOID
        ScsiOpInquiry(
            __in pHW_HBA_EXT DevExt,
            __in pHW_LU_EXTENSION LuExt,
            __in PSCSI_REQUEST_BLOCK Srb
            );

    VOID
        ScsiOpInquiryRaidControllerUnit(
            __in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb
            );

    UCHAR
        ScsiGetLUExtension(
            __in pHW_HBA_EXT								pHBAExt,      // Adapter device-object extension from port driver.
            pHW_LU_EXTENSION * ppLUExt,
            __in UCHAR									PathId,
            __in UCHAR									TargetId,
            __in UCHAR									Lun,
            __in PKIRQL                                   LowestAssumedIrql
            );

    VOID
        ScsiOpReadCapacity(
            __in pHW_HBA_EXT DevExt,
            __in pHW_LU_EXTENSION LuExt,
            __in __deref PSCSI_REQUEST_BLOCK Srb
            );

    VOID
        ScsiOpModeSense(
            __in pHW_HBA_EXT         DevExt,
            __in pHW_LU_EXTENSION    LuExt,
            __in __deref PSCSI_REQUEST_BLOCK pSrb
            );

    VOID
        ScsiOpModeSense10(
            __in pHW_HBA_EXT         DevExt,
            __in pHW_LU_EXTENSION    LuExt,
            __in __deref PSCSI_REQUEST_BLOCK pSrb
        );

    VOID
        ImScsiDispatchPersistentReserveIn(
            __in pHW_HBA_EXT         DevExt,
            __in pHW_LU_EXTENSION    LuExt,
            __in __deref PSCSI_REQUEST_BLOCK pSrb
        );

    VOID
        ImScsiDispatchPersistentReserveOut(
            __in pHW_HBA_EXT         DevExt,
            __in pHW_LU_EXTENSION    LuExt,
            __in __deref PSCSI_REQUEST_BLOCK pSrb
        );

    VOID
        ScsiOpReportLuns(
            __inout         pHW_HBA_EXT          DevExt,
            __in __deref    PSCSI_REQUEST_BLOCK  Srb,
            __inout __deref PKIRQL               LowestAssumedIrql
            );

    VOID
        MpQueryRegParameters(
            __in __deref PUNICODE_STRING,
            __out __deref pMP_REG_INFO
            );

    VOID
        ScsiSetCheckCondition(
            __in __deref PSCSI_REQUEST_BLOCK pSrb,
            __in UCHAR               SrbStatus,
            __in UCHAR               SenseKey,
            __in UCHAR               AdditionalSenseCode,
            __in UCHAR               AdditionalSenseCodeQualifier OPTIONAL
            );

#define ScsiSetSuccess(pSrb, Length) \
{ \
    (pSrb)->SrbStatus = SRB_STATUS_SUCCESS; \
    (pSrb)->ScsiStatus = SCSISTAT_GOOD; \
    (pSrb)->DataTransferLength = (Length); \
    (pSrb)->SenseInfoBufferLength = 0; \
}

#define ScsiSetError(pSrb, Status) \
{ \
    (pSrb)->SrbStatus = (Status); \
    (pSrb)->ScsiStatus = SCSISTAT_GOOD; \
    (pSrb)->DataTransferLength = 0; \
    (pSrb)->SenseInfoBufferLength = 0; \
}

#define ScsiSetErrorScsiStatus(pSrb, Status, ScsiStat) \
{ \
    (pSrb)->SrbStatus = (Status); \
    (pSrb)->ScsiStatus = (ScsiStat); \
    (pSrb)->DataTransferLength = 0; \
    (pSrb)->SenseInfoBufferLength = 0; \
}

#ifdef USE_SCSIPORT

#define STORAGE_MAP_BUFFERS_SETTING                             TRUE

#define STORAGE_INTERFACE_TYPE                                  Isa

#define STORAGE_STATUS_SUCCESS                                  (0x00000000L)

    ULONG
        inline
        StoragePortGetSystemAddress(
            __in PVOID /*pHBAExt*/,
            __in __deref PSCSI_REQUEST_BLOCK pSrb,
            __out PVOID *pPtr
            )
    {
        *pPtr = pSrb->DataBuffer;
        return STORAGE_STATUS_SUCCESS;
    }

    NTSTATUS
        ImScsiGetControllerObject(
            );

    LONG
        ImScsiCompletePendingSrbs(
            __in pHW_HBA_EXT pHBAExt,  // Adapter device-object extension from port driver.
            __inout __deref PKIRQL LowestAssumedIrql
            );

#define StoragePortInitialize                                   ScsiPortInitialize

#define StoragePortGetLogicalUnit                               ScsiPortGetLogicalUnit

#define StoragePortLogError                                     ScsiPortLogError

#define StoragePortNotification                                 ScsiPortNotification

#endif

#ifdef USE_STORPORT

#define STORAGE_MAP_BUFFERS_SETTING                             STOR_MAP_NON_READ_WRITE_BUFFERS

#define STORAGE_INTERFACE_TYPE                                  Internal

#define STORAGE_STATUS_SUCCESS                                  STOR_STATUS_SUCCESS
#define StoragePortGetSystemAddress                             StorPortGetSystemAddress

#define ImScsiGetControllerObject()

#define ImScsiCompletePendingSrbs(pHBAExt, Irql)

#define StoragePortInitialize                                   StorPortInitialize

#define StoragePortGetLogicalUnit                               StorPortGetLogicalUnit

#define StoragePortLogError                                     StorPortLogError

#define StoragePortNotification                                 StorPortNotification

#endif

#if VER_PRODUCTBUILD < 7600
    typedef VOID
        KSTART_ROUTINE(
            __in PVOID StartContext);

#define PsDereferenceImpersonationToken(T)	\
  ((ARGUMENT_PRESENT((T))) ?			\
   (ObDereferenceObject((T))) : 0)

#define PsDereferencePrimaryToken(T) (ObDereferenceObject((T)))

#ifndef __drv_maxIRQL
#define __drv_maxIRQL(i)
#endif
#ifndef __drv_requiresIRQL
#define __drv_requiresIRQL(i)
#endif
#ifndef __drv_savesIRQLGlobal
#define __drv_savesIRQLGlobal(i,n)
#endif
#ifndef __drv_setsIRQL
#define __drv_setsIRQL(i)
#endif
#ifndef __drv_restoresIRQLGlobal
#define __drv_restoresIRQLGlobal(i,n)
#endif
#ifndef __drv_when
#define __drv_when(c,s)
#endif
#ifndef __drv_acquiresExclusiveResource
#define __drv_acquiresExclusiveResource(t)
#endif
#ifndef __drv_releasesExclusiveResource
#define __drv_releasesExclusiveResource(t)
#endif
#ifndef __in
#define __in
#endif
#ifndef __out
#define __out
#endif
#ifndef __inout
#define __inout
#endif
#ifndef __deref
#define __deref
#endif
#endif

    UCHAR MpFindRemovedDevice(
        __in pHW_HBA_EXT,
        __in PSCSI_REQUEST_BLOCK
        );

    VOID ImScsiStopAdapter(
        __in pHW_HBA_EXT DevExt,
        __inout __deref PKIRQL
        );

    VOID
        ImScsiTracingInit(
            __in PVOID,
            __in PVOID
            );

    VOID
        ImScsiTracingCleanup(__in PVOID);

    VOID
        ScsiOpVPDCdRomUnit(
            __in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     pLUExt,       // LUN device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb
            );

    VOID
        ScsiOpVPDDiskUnit(
            __in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     pLUExt,       // LUN device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb
            );

    VOID
        ScsiOpVPDRaidControllerUnit(
            __in pHW_HBA_EXT,
            __in PSCSI_REQUEST_BLOCK
            );

    void
        InitializeWmiContext(__in pHW_HBA_EXT);

    BOOLEAN
        HandleWmiSrb(
            __in       pHW_HBA_EXT,
            __inout __deref PSCSI_WMI_REQUEST_BLOCK
            );

    VOID
        ScsiOpMediumRemoval(__in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     device_extension,       // LUN device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb
            );

    VOID
        ScsiOpReadDiscInformation(__in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     device_extension,       // LUN device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb
            );

    VOID
        ScsiOpReadTrackInformation(__in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     device_extension,       // LUN device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb
            );

    VOID
        ScsiOpGetConfiguration(__in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     device_extension,       // LUN device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb
            );

    VOID
        ScsiOpGetEventStatus(__in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     device_extension,       // LUN device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb
            );

    VOID
        ScsiOpUnmap(
            __in pHW_HBA_EXT          pHBAExt, // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     pLUExt,  // LUN device-object extension from port driver.        
            __in PSCSI_REQUEST_BLOCK  pSrb,
            __in pResultType          pResult,
            __in PKIRQL               LowestAssumedIrql
        );

    VOID
        ScsiOpPersistentReserveInOut(
            __in pHW_HBA_EXT          pHBAExt, // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     pLUExt,  // LUN device-object extension from port driver.        
            __in PSCSI_REQUEST_BLOCK  pSrb,
            __in pResultType          pResult,
            __in PKIRQL               LowestAssumedIrql
        );

    VOID
        ScsiOpReadTOC(__in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
            __in pHW_LU_EXTENSION     device_extension,       // LUN device-object extension from port driver.
            __in PSCSI_REQUEST_BLOCK  pSrb
            );

    VOID
        ScsiOpReadWrite(
            __in pHW_HBA_EXT          pDevExt,
            __in pHW_LU_EXTENSION     pLUExt,
            __in PSCSI_REQUEST_BLOCK  pSrb,
            __in pResultType          pResult,
            __in PKIRQL               LowestAssumedIrql
            );

    VOID
        ImScsiDispatchWork(
            __in pMP_WorkRtnParms        pWkRtnParms
            );

    NTSTATUS
        ImScsiCallDriverAndWait(__in PDEVICE_OBJECT DeviceObject,
            __in PIRP Irp,
            __in PKEVENT FinishedEvent);

    VOID
        ImScsiCleanupLU(
            __in pHW_LU_EXTENSION     pLUExt,
            __inout __deref PKIRQL
            );

    KSTART_ROUTINE
        ImScsiWorkerThread;

    VOID
        ImScsiCreateLU(
            __in pHW_HBA_EXT             pHBAExt,
            __in PSCSI_REQUEST_BLOCK     pSrb,
            __in PETHREAD                pReqThread,
            __inout __deref PKIRQL
            );

    VOID
        ImScsiCreateDevice(
            __in pHW_HBA_EXT            pHBAExt,
            __in PSCSI_REQUEST_BLOCK    pSrb,
            __inout __deref pResultType pResult,
            __inout __deref PKIRQL      LowestAssumedIrql
            );

    NTSTATUS
        ImScsiRemoveDevice(
            __in pHW_HBA_EXT          pDevExt,
            __in PDEVICE_NUMBER       DeviceNumber,
            __inout __deref PKIRQL         LowestAssumedIrql
            );

    NTSTATUS
        ImScsiExtendLU(
            pHW_HBA_EXT pHBAExt,
            pHW_LU_EXTENSION device_extension,
            PSRB_IMSCSI_EXTEND_DEVICE extend_device_data);

    NTSTATUS
        ImScsiQueryDevice(
            __in pHW_HBA_EXT               pHBAExt,
            __in PSRB_IMSCSI_CREATE_DATA   create_data,
            __in PULONG                    Length,
            __inout __deref PKIRQL              LowestAssumedIrql
            );

    NTSTATUS
        ImScsiQueryAdapter(
            __in pHW_HBA_EXT                     pDevExt,
            __inout __deref PSRB_IMSCSI_QUERY_ADAPTER query_data,
            __in ULONG                           max_length,
            __inout __deref PKIRQL                    LowestAssumedIrql
            );

    NTSTATUS
        ImScsiSetFlagsDevice(
            __in pHW_HBA_EXT          pDevExt,
            __inout __deref PSRB_IMSCSI_SET_DEVICE_FLAGS set_flags_data,
            __inout __deref PKIRQL          LowestAssumedIrql
            );

    VOID
        ImScsiExtendDevice(
            __in pHW_HBA_EXT            pHBAExt,
            __in PSCSI_REQUEST_BLOCK    pSrb,
            __inout __deref pResultType pResult,
            __inout __deref PKIRQL      LowestAssumedIrql,
            __inout __deref PSRB_IMSCSI_EXTEND_DEVICE       extend_device_data
            );

    NTSTATUS
        ImScsiInitializeLU(__inout __deref pHW_LU_EXTENSION pLUExt,
            __inout __deref PSRB_IMSCSI_CREATE_DATA CreateData,
            __in __deref PETHREAD ClientThread);

    NTSTATUS
        ImScsiCloseDevice(
            __in pHW_LU_EXTENSION pLUExt,
            __inout __deref PKIRQL LowestAssumedIrql
            );

    VOID
        ImScsiCloseProxy(__in __deref PPROXY_CONNECTION Proxy);

    NTSTATUS
        ImScsiCallProxy(__in __deref PPROXY_CONNECTION Proxy,
            __out __deref PIO_STATUS_BLOCK IoStatusBlock,
            __in __deref PKEVENT CancelEvent OPTIONAL,
            __in __deref PVOID RequestHeader,
            __in ULONG RequestHeaderSize,
            __drv_when(RequestDataSize > 0, __in __deref) PVOID RequestData,
            __in ULONG RequestDataSize,
            __drv_when(ResponseHeaderSize > 0, __out __deref) PVOID ResponseHeader,
            __in ULONG ResponseHeaderSize,
            __drv_when(ResponseDataBufferSize > 0 && *ResponseDataSize > 0, __out) __drv_when(ResponseDataBufferSize > 0, __deref) PVOID ResponseData,
            __in ULONG ResponseDataBufferSize,
            __drv_when(ResponseDataBufferSize > 0, __inout __deref) ULONG *ResponseDataSize);

    NTSTATUS
        ImScsiConnectProxy(__inout __deref PPROXY_CONNECTION Proxy,
            __out __deref PIO_STATUS_BLOCK IoStatusBlock,
            __in __deref PKEVENT CancelEvent OPTIONAL,
            __in ULONG Flags,
            __in __deref PWSTR ConnectionString,
            __in USHORT ConnectionStringLength);

    NTSTATUS
        ImScsiQueryInformationProxy(__in __deref PPROXY_CONNECTION Proxy,
            __out __deref PIO_STATUS_BLOCK IoStatusBlock,
            __in __deref PKEVENT CancelEvent,
            __out __deref PIMDPROXY_INFO_RESP ProxyInfoResponse,
            __in ULONG ProxyInfoResponseLength);

    NTSTATUS
        ImScsiReadProxy(__in __deref PPROXY_CONNECTION Proxy,
            __out __deref PIO_STATUS_BLOCK IoStatusBlock,
            __in __deref PKEVENT CancelEvent,
            PVOID Buffer,
            __in ULONG Length,
            __in __deref PLARGE_INTEGER ByteOffset);

    NTSTATUS
        ImScsiWriteProxy(__in __deref PPROXY_CONNECTION Proxy,
            __out __deref PIO_STATUS_BLOCK IoStatusBlock,
            __in __deref PKEVENT CancelEvent,
            PVOID Buffer,
            __in ULONG Length,
            __in __deref PLARGE_INTEGER ByteOffset);

    IMDPROXY_SHARED_RESP_CODE
        ImScsiSharedKeyProxy(__in __deref pHW_LU_EXTENSION LuExt,
            __in __deref PIMDPROXY_SHARED_REQ Request,
            PULONGLONG KeyList,
            PULONG KeyItems);

    NTSTATUS
        ImScsiConnectReservationKeyStore(pHW_LU_EXTENSION LuExt);

    IO_COMPLETION_ROUTINE
        ImScsiParallelReadWriteImageCompletion;

    VOID
        ImScsiParallelReadWriteImage(
            __in pMP_WorkRtnParms       pWkRtnParms,
            __inout __deref pResultType pResult,
            __inout __deref PKIRQL      LowestAssumedIrql
            );

    NTSTATUS
        ImScsiReadDevice(
            __in pHW_LU_EXTENSION pLUExt,
            __in PVOID            Buffer,
            __in PLARGE_INTEGER   ByteOffset,
            __in PULONG           Length
            );

    NTSTATUS
        ImScsiWriteDevice(
            __in pHW_LU_EXTENSION pLUExt,
            __in PVOID            Buffer,
            __in PLARGE_INTEGER   ByteOffset,
            __in PULONG           Length
            );

    VOID
        ImScsiDispatchUnmapDevice(
            __in pHW_HBA_EXT pHBAExt,
            __in pHW_LU_EXTENSION pLUExt,
            __in PSCSI_REQUEST_BLOCK pSrb);

    NTSTATUS
        ImScsiSafeIOStream(__in PFILE_OBJECT FileObject,
            __in UCHAR MajorFunction,
            __out __deref PIO_STATUS_BLOCK IoStatusBlock,
            __in PKEVENT CancelEvent,
            PVOID Buffer,
            __in ULONG Length);

    NTSTATUS
        ImScsiGetDiskSize(__in HANDLE FileHandle,
            __out __deref PIO_STATUS_BLOCK IoStatus,
            __inout __deref PLARGE_INTEGER DiskSize);

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
            PWCHAR Message);

    BOOLEAN
        ImScsiFillMemoryDisk(pHW_LU_EXTENSION pLUExt);

    NTSTATUS
        ImScsiSafeReadFile(__in HANDLE FileHandle,
            __out __deref PIO_STATUS_BLOCK IoStatusBlock,
            PVOID Buffer,
            __in SIZE_T Length,
            __in __deref PLARGE_INTEGER Offset);

    PIRP
        ImScsiBuildCompletionIrp();

    NTSTATUS
        ImScsiCallForCompletion(PIRP Irp,
            pMP_WorkRtnParms pWkRtnParms,
            PKIRQL LowestAssumedIrql);

    pMP_WorkRtnParms ImScsiCreateWorkItem(pHW_HBA_EXT pHBAExt,
        pHW_LU_EXTENSION pLUExt,
        PSCSI_REQUEST_BLOCK pSrb);

    VOID ImScsiScheduleWorkItem(pMP_WorkRtnParms pWkRtnParms,
        PKIRQL LowestAssumedIrql);

    FORCEINLINE
        BOOLEAN
        ImScsiIsBufferZero(PVOID Buffer, ULONG Length)
    {
        PULONGLONG ptr;

        if (Length < sizeof(ULONGLONG))
            return FALSE;

        for (ptr = (PULONGLONG)Buffer;
        (ptr <= (PULONGLONG)((PUCHAR)Buffer + Length - sizeof(ULONGLONG))) &&
            (*ptr == 0); ptr++);

            return (BOOLEAN)(ptr == (PULONGLONG)((PUCHAR)Buffer + Length));
    }

#if DBG

    char *DbgGetScsiOpStr(PSCSI_REQUEST_BLOCK Srb);

#endif

#define ImScsiLogError(x) ImScsiLogDbgError x

    FORCEINLINE
        VOID
        ImScsiFreeIrpWithMdls(__in __deref PIRP Irp)
    {
        if (Irp->MdlAddress != NULL)
        {
            PMDL mdl;
            PMDL nextMdl;
            for (mdl = Irp->MdlAddress; mdl != NULL; mdl = nextMdl)
            {
                nextMdl = mdl->Next;

                if (mdl->MdlFlags & MDL_PAGES_LOCKED)
                {
                    MmUnlockPages(mdl);
                }

                IoFreeMdl(mdl);
            }
            Irp->MdlAddress = NULL;
        }

        IoFreeIrp(Irp);
    }

#if _NT_TARGET_VERSION >= 0x501

    FORCEINLINE
        VOID
        __drv_maxIRQL(DISPATCH_LEVEL)
        __drv_savesIRQLGlobal(QueuedSpinLock, LockHandle)
        __drv_setsIRQL(DISPATCH_LEVEL)
        ImScsiAcquireLock_x64(__inout __deref PKSPIN_LOCK SpinLock,
            __out __deref __drv_acquiresExclusiveResource(KeQueuedSpinLockType)
            PKLOCK_QUEUE_HANDLE LockHandle,
            __in KIRQL LowestAssumedIrql)
    {
        if (LowestAssumedIrql >= DISPATCH_LEVEL)
        {
            ASSERT(KeGetCurrentIrql() >= DISPATCH_LEVEL);

            KeAcquireInStackQueuedSpinLockAtDpcLevel(SpinLock, LockHandle);
        }
        else
        {
            KeAcquireInStackQueuedSpinLock(SpinLock, LockHandle);
        }
    }

    FORCEINLINE
        VOID
        __drv_requiresIRQL(DISPATCH_LEVEL)
        __drv_restoresIRQLGlobal(QueuedSpinLock, LockHandle)
        ImScsiReleaseLock_x64(
            __in __deref __drv_releasesExclusiveResource(KeQueuedSpinLockType)
            PKLOCK_QUEUE_HANDLE LockHandle,
            __inout __deref PKIRQL LowestAssumedIrql)
    {
        ASSERT(KeGetCurrentIrql() >= DISPATCH_LEVEL);

        if (*LowestAssumedIrql >= DISPATCH_LEVEL)
        {
            KeReleaseInStackQueuedSpinLockFromDpcLevel(LockHandle);
        }
        else
        {
            KeReleaseInStackQueuedSpinLock(LockHandle);
            *LowestAssumedIrql = LockHandle->OldIrql;
        }
    }

#endif >= XP

    FORCEINLINE
        VOID
        __drv_maxIRQL(DISPATCH_LEVEL)
        __drv_savesIRQLGlobal(SpinLock, OldIrql)
        __drv_setsIRQL(DISPATCH_LEVEL)
        ImScsiAcquireLock_x86(__inout __deref __drv_acquiresExclusiveResource(KeSpinLockType) PKSPIN_LOCK SpinLock,
            __out __deref __drv_when(LowestAssumedIrql < DISPATCH_LEVEL, __drv_savesIRQL) PKIRQL OldIrql,
            __in KIRQL LowestAssumedIrql)
    {
        if (LowestAssumedIrql >= DISPATCH_LEVEL)
        {
            ASSERT(KeGetCurrentIrql() >= DISPATCH_LEVEL);

            *OldIrql = DISPATCH_LEVEL;

            KeAcquireSpinLockAtDpcLevel(SpinLock);
        }
        else
        {
            KeAcquireSpinLock(SpinLock, OldIrql);
        }
    }

    FORCEINLINE
        VOID
        __drv_requiresIRQL(DISPATCH_LEVEL)
        __drv_restoresIRQLGlobal(SpinLock, OldIrql)
        ImScsiReleaseLock_x86(
            __inout __deref __drv_releasesExclusiveResource(KeSpinLockType) PKSPIN_LOCK SpinLock,
            __in KIRQL OldIrql,
            __inout __deref PKIRQL LowestAssumedIrql)
    {
        ASSERT(KeGetCurrentIrql() >= DISPATCH_LEVEL);

        if (*LowestAssumedIrql >= DISPATCH_LEVEL)
        {
            KeReleaseSpinLockFromDpcLevel(SpinLock);
        }
        else
        {
            KeReleaseSpinLock(SpinLock, OldIrql);
            *LowestAssumedIrql = OldIrql;
        }
    }

#ifdef _AMD64_

#define ImScsiAcquireLock ImScsiAcquireLock_x64

#define ImScsiReleaseLock ImScsiReleaseLock_x64

#else

#define ImScsiAcquireLock(SpinLock, LockHandle, LowestAssumedIrql) \
    { \
        (LockHandle)->LockQueue.Lock = (SpinLock); \
        ImScsiAcquireLock_x86((LockHandle)->LockQueue.Lock, &(LockHandle)->OldIrql, (LowestAssumedIrql)); \
    }

#define ImScsiReleaseLock(LockHandle, LowestAssumedIrql) \
    { \
        ImScsiReleaseLock_x86((LockHandle)->LockQueue.Lock, (LockHandle)->OldIrql, (LowestAssumedIrql)); \
    }

#endif

#endif    //   #if !defined(_MP_User_Mode_Only)

#ifdef __cplusplus
}

#if !defined(_MP_User_Mode_Only)

#define POOL_TAG MP_TAG_GENERAL

#include <wkmem.hpp>

#endif

#endif

#endif    // _MP_H_

