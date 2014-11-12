
/// imscsi.h
/// Definitions for functions and global constants for use in kernel mode
/// components.
/// 
/// Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#ifndef _MP_H_
#define _MP_H_

#define IMSCSI_DRIVER_VERSION ((ULONG) 0x0101)

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

#include <ntifs.h>
#include <ntddk.h>
#include <wdm.h>

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

#include "common.h"
#include "imdproxy.h"
#include "phdskmntver.h"

#if !defined(_MP_User_Mode_Only)                      // User-mode only.

#if       !defined(_MP_H_skip_includes)

#include <stdio.h>
#include <stdarg.h>

#endif // !defined(_MP_H_skip_includes)

#if _NT_TARGET_VERSION <= 0x0500

typedef
VOID
KSTART_ROUTINE (
    __in PVOID StartContext
    );
typedef KSTART_ROUTINE *PKSTART_ROUTINE;

#define PsDereferenceImpersonationToken(T)	\
  ((ARGUMENT_PRESENT((T))) ?			\
   (ObDereferenceObject((T))) : 0)

#define PsDereferencePrimaryToken(T) (ObDereferenceObject((T)))

#endif

#define VENDOR_ID                   L"Arsenal Recon "
#define VENDOR_ID_ascii             "Arsenal Recon "
#define PRODUCT_ID                  L"Virtual "
#define PRODUCT_ID_ascii            "Virtual "
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

#define DEFAULT_BREAK_ON_ENTRY      0                // No break
#define DEFAULT_DEBUG_LEVEL         2               
#define DEFAULT_INITIATOR_ID        7
#define DEFAULT_NUMBER_OF_BUSES     1

#define GET_FLAG(Flags, Bit)        ((Flags) & (Bit))
#define SET_FLAG(Flags, Bit)        ((Flags) |= (Bit))
#define CLEAR_FLAG(Flags, Bit)      ((Flags) &= ~(Bit))

typedef struct _MPDriverInfo         MPDriverInfo, *pMPDriverInfo;
typedef struct _MP_REG_INFO          MP_REG_INFO, *pMP_REG_INFO;
typedef struct _HW_LU_EXTENSION      HW_LU_EXTENSION, *pHW_LU_EXTENSION;
typedef struct _LBA_LIST             LBA_LIST, *PLBA_LIST;

extern 
pMPDriverInfo pMPDrvInfoGlobal;  

typedef struct _MP_REG_INFO {
    UNICODE_STRING   VendorId;
    UNICODE_STRING   ProductId;
    UNICODE_STRING   ProductRevision;
    ULONG            NumberOfBuses;       // Number of buses (paths) supported by this adapter
    ULONG            InitiatorID;        // Adapter's target ID
} MP_REG_INFO, * pMP_REG_INFO;

typedef struct _MPDriverInfo {                        // The master miniport object. In effect, an extension of the driver object for the miniport.
#ifdef USE_SCSIPORT
    PDEVICE_OBJECT                 DeviceObject;
#endif
    MP_REG_INFO                    MPRegInfo;
    KSPIN_LOCK                     DrvInfoLock;
    LIST_ENTRY                     ListMPHBAObj;      // Header of list of HW_HBA_EXT objects.
    PDRIVER_OBJECT                 pDriverObj;
    PDRIVER_UNLOAD                 pChainUnload;
    UCHAR                          GlobalsInitialized;
    KEVENT                         StopWorker;
    PKTHREAD                       WorkerThread;
    LIST_ENTRY                     RequestList;
    KSPIN_LOCK                     RequestListLock;   
    KEVENT                         RequestEvent;
#ifdef USE_SCSIPORT
    LIST_ENTRY                     ResponseList;
    KSPIN_LOCK                     ResponseListLock;
    KEVENT                         ResponseEvent;
#endif
    ULONG                          DrvInfoNbrMPHBAObj;// Count of items in ListMPHBAObj.
	ULONG                          RandomSeed;
} MPDriverInfo, * pMPDriverInfo;

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
    UCHAR                          AdapterState;
    UCHAR                          VendorId[9];
    UCHAR                          ProductId[17];
    UCHAR                          ProductRevision[5];
    BOOLEAN                        bReportAdapterDone;
} HW_HBA_EXT, * pHW_HBA_EXT;

// Flag definitions for LUFlags.

#define LU_DEVICE_INITIALIZED   0x0001

typedef struct _HW_LU_EXTENSION {                     // LUN extension allocated by port driver.
    LIST_ENTRY            List;                       // Pointers to next and previous HW_LU_EXTENSION objects, used in HW_HBA_EXT.
    pHW_HBA_EXT           pHBAExt;
    LIST_ENTRY            RequestList;
    KSPIN_LOCK            RequestListLock;   
    KEVENT                RequestEvent;
    BOOLEAN               Initialized;
    KEVENT                StopThread;
    KEVENT                Missing;
    LARGE_INTEGER         ImageOffset;
    LARGE_INTEGER         DiskSize;
    UCHAR                 BlockPower;
    ULONG                 Flags;
    UCHAR                 DeviceType;
    BOOLEAN               RemovableMedia;
    BOOLEAN               ReadOnly;
    ULONG                 FakeDiskSignature;
    PVOID                 LastIoBuffer;
    LONGLONG              LastIoStartSector;
    ULONG                 LastIoLength;
    KSPIN_LOCK            LastIoLock;
    DEVICE_NUMBER         DeviceNumber;
    UNICODE_STRING        ObjectName;
    HANDLE                ImageFile;
    PROXY_CONNECTION      Proxy;
    BOOLEAN               VMDisk;
    BOOLEAN               Modified;
    PUCHAR                ImageBuffer;
    BOOLEAN               UseProxy;
} HW_LU_EXTENSION, * pHW_LU_EXTENSION;

typedef struct _HW_SRB_EXTENSION {
    SCSIWMI_REQUEST_CONTEXT WmiRequestContext;
} HW_SRB_EXTENSION, * PHW_SRB_EXTENSION;

typedef struct _MP_WorkRtnParms {
  LIST_ENTRY           RequestListEntry;
  LIST_ENTRY           ResponseListEntry;
  pHW_HBA_EXT          pHBAExt;
  pHW_LU_EXTENSION     pLUExt;
  PSCSI_REQUEST_BLOCK  pSrb;
  PETHREAD             pReqThread;
} MP_WorkRtnParms, * pMP_WorkRtnParms;

enum ResultType {
  ResultDone,
  ResultQueued
} ;

#define RegWkBfrSz  0x1000

typedef struct _RegWorkBuffer {
  pHW_HBA_EXT          pAdapterExt;
  UCHAR                Work[256];
} RegWorkBuffer, * pRegWorkBuffer;

DRIVER_UNLOAD
ImScsiUnload;

DRIVER_INITIALIZE
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
                __in __out PPORT_CONFIGURATION_INFORMATION pConfigInfo,
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
    __in PVOID
);

BOOLEAN
MpHwStartIo(
            __in PVOID,
            __in PSCSI_REQUEST_BLOCK
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
             __in PVOID,
             __in ULONG       
            );

BOOLEAN
MpHwAdapterState(
  __in  PVOID HwDeviceExtension,
  __in  PVOID Context,
  __in  BOOLEAN SaveState
);

SCSI_ADAPTER_CONTROL_STATUS
MpHwAdapterControl(
    __in PVOID DevExt,
    __in SCSI_ADAPTER_CONTROL_TYPE ControlType, 
    __in PVOID Parameters 
);

VOID
ScsiIoControl(
              __in pHW_HBA_EXT DevExt,
              __in PSCSI_REQUEST_BLOCK,
              __in PUCHAR             
              );

VOID
ScsiExecute(
            __in pHW_HBA_EXT DevExt,
            __in PSCSI_REQUEST_BLOCK,
            __in PUCHAR             
            );

VOID
ScsiPnP(
	__in pHW_HBA_EXT              pHBAExt,
	__in PSCSI_PNP_REQUEST_BLOCK  pSrb
	);

VOID
ScsiOpStartStopUnit(
__in pHW_HBA_EXT DevExt,
__in pHW_LU_EXTENSION LuExt,
__in PSCSI_REQUEST_BLOCK Srb
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
              __in UCHAR									Lun
             );

VOID
ScsiOpReadCapacity(
    IN pHW_HBA_EXT DevExt,
    IN pHW_LU_EXTENSION LuExt,
    IN PSCSI_REQUEST_BLOCK Srb
    );

VOID
ScsiOpModeSense(
    IN pHW_HBA_EXT         DevExt,
    IN pHW_LU_EXTENSION    LuExt,
    IN PSCSI_REQUEST_BLOCK pSrb
    );

VOID
ScsiOpModeSense10(
    IN pHW_HBA_EXT         DevExt,
    IN pHW_LU_EXTENSION    LuExt,
    IN PSCSI_REQUEST_BLOCK pSrb
    );

VOID                                                                        
ScsiOpReportLuns(                                 
    IN pHW_HBA_EXT          DevExt,
    IN PSCSI_REQUEST_BLOCK  Srb
    );                                                                                   

VOID
MpQueryRegParameters(
    IN PUNICODE_STRING,
    IN pMP_REG_INFO       
    );

VOID
ScsiSetCheckCondition(
    IN PSCSI_REQUEST_BLOCK pSrb,
    IN UCHAR               SrbStatus,
    IN UCHAR               SenseKey,
    IN UCHAR               AdditionalSenseCode,
    IN UCHAR               AdditionalSenseCodeQualifier OPTIONAL
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

#ifdef USE_SCSIPORT

#define STORAGE_MAP_BUFFERS_SETTING                             TRUE

#define STORAGE_INTERFACE_TYPE                                  Isa

#define STORAGE_STATUS_SUCCESS                                  (0x00000000L)
#define StoragePortGetSystemAddress(pHBAExt, pSrb, pPtr)        ((ULONG_PTR)(*(pPtr)=(pSrb)->DataBuffer) & 0 | STORAGE_STATUS_SUCCESS)

NTSTATUS
ImScsiGetAdapterDeviceObject(
    );

LONG
ImScsiCompletePendingSrbs(
                          __in pHW_HBA_EXT pHBAExt  // Adapter device-object extension from port driver.
);

#define StoragePortInitialize                                   ScsiPortInitialize

#define StoragePortGetLogicalUnit                               ScsiPortGetLogicalUnit

#define StoragePortNotification                                 ScsiPortNotification

#endif

#ifdef USE_STORPORT

#define STORAGE_MAP_BUFFERS_SETTING                             STOR_MAP_NO_BUFFERS

#define STORAGE_INTERFACE_TYPE                                  Internal

#define STORAGE_STATUS_SUCCESS                                  STOR_STATUS_SUCCESS
#define StoragePortGetSystemAddress                             StorPortGetSystemAddress

#define ImScsiGetAdapterDeviceObject()

#define ImScsiCompletePendingSrbs(pHBAExt)

#define StoragePortInitialize                                   StorPortInitialize

#define StoragePortGetLogicalUnit                               StorPortGetLogicalUnit

#define StoragePortNotification                                 StorPortNotification

#endif

UCHAR MpFindRemovedDevice(
    __in pHW_HBA_EXT,
    __in PSCSI_REQUEST_BLOCK
    );

VOID ImScsiStopAdapter(
    __in pHW_HBA_EXT DevExt
    );

VOID                                                                                                                         
ImScsiTracingInit(                                                                                                            
              __in PVOID,                                                                                  
              __in PVOID
             );

VOID                                                                                                                         
ImScsiTracingCleanup(__in PVOID);

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
    __in __out PSCSI_WMI_REQUEST_BLOCK
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
ScsiOpReadTOC(__in pHW_HBA_EXT          pHBAExt,      // Adapter device-object extension from port driver.
              __in pHW_LU_EXTENSION     device_extension,       // LUN device-object extension from port driver.
              __in PSCSI_REQUEST_BLOCK  pSrb
             );

VOID
ScsiOpReadWrite(
           __in pHW_HBA_EXT          pDevExt,
           __in pHW_LU_EXTENSION     pLUExt,
           __in PSCSI_REQUEST_BLOCK  pSrb,
           __in PUCHAR               pResult
          );

VOID                                                                                                                                               
ImScsiDispatchWork(
               __in pMP_WorkRtnParms        pWkRtnParms
               );

NTSTATUS
ImScsiCleanupLU(
                 __in pHW_LU_EXTENSION     pLUExt
                 );

KSTART_ROUTINE
ImScsiWorkerThread;

VOID
ImScsiCreateLU(
                         __in pHW_HBA_EXT             pHBAExt,
                         __in PSCSI_REQUEST_BLOCK     pSrb,
                         __in PETHREAD                pReqThread
                         );

VOID
ImScsiCreateDevice(
                   __in pHW_HBA_EXT          pHBAExt,
                   __in PSCSI_REQUEST_BLOCK  pSrb,
                   __in __out PUCHAR         pResult
                   );

NTSTATUS
ImScsiRemoveDevice(
                   __in pHW_HBA_EXT          pDevExt,
                   __in PSRB_IMSCSI_REMOVE_DEVICE remove_data
                   );

NTSTATUS
ImScsiQueryDevice(
                  __in pHW_HBA_EXT               pHBAExt,
                  __in PSRB_IMSCSI_CREATE_DATA   create_data,
                  __in PULONG                    Length
                  );

NTSTATUS
ImScsiQueryAdapter(
                   __in pHW_HBA_EXT                     pDevExt,
                   __in __out PSRB_IMSCSI_QUERY_ADAPTER query_data,
                   __in ULONG                           max_length
                   );

NTSTATUS
ImScsiSetFlagsDevice(
                     __in pHW_HBA_EXT          pDevExt,
                     __in __out PSRB_IMSCSI_SET_DEVICE_FLAGS set_flags_data
                     );

NTSTATUS
ImScsiInitializeLU(IN pHW_LU_EXTENSION LUExtension,
		 IN OUT PSRB_IMSCSI_CREATE_DATA CreateData,
		 IN PETHREAD ClientThread);

NTSTATUS
ImScsiCloseDevice(
                 __in pHW_LU_EXTENSION pLUExt
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

NTSTATUS
ImScsiSafeIOStream(IN PFILE_OBJECT FileObject,
		   IN UCHAR MajorFunction,
		   IN OUT PIO_STATUS_BLOCK IoStatusBlock,
		   IN PKEVENT CancelEvent,
		   OUT PVOID Buffer,
		   IN ULONG Length);

NTSTATUS
ImScsiGetDiskSize(IN HANDLE FileHandle,
		  IN OUT PIO_STATUS_BLOCK IoStatus,
		  IN OUT PLARGE_INTEGER DiskSize);

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
                  IN PWCHAR Message);

BOOLEAN
ImScsiFillMemoryDisk(pHW_LU_EXTENSION LUExtension);

NTSTATUS
ImScsiSafeReadFile(IN HANDLE FileHandle,
		   OUT PIO_STATUS_BLOCK IoStatusBlock,
		   OUT PVOID Buffer,
		   IN SIZE_T Length,
		   IN PLARGE_INTEGER Offset);

#define ImScsiLogError(x) ImScsiLogDbgError x


#endif    //   #if !defined(_MP_User_Mode_Only)

#endif    // _MP_H_

