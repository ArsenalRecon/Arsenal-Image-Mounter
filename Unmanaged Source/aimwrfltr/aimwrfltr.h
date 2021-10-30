/*
* AIMWrFltr.h
*
* Based on LoopBack Filter Driver by Mayur Thigale.
* Refer for loopback driver http://www.codeproject.com/KB/system/loopback.aspx
*/

#pragma once

#include <Ntifs.h>
#include <ntdddisk.h>
#include <ntddscsi.h>
#include <stdarg.h>
#include <stdio.h>
#include <ntddvol.h>

#include <mountdev.h>

#include <ntstrsafe.h>

#define INITGUID

#include "inc\fltstats.h"

//
// Tags used for kernel mode allocations and locks.
// Useful with tools like poolmon etc.
//
#define POOL_TAG                    'FrWA'
#define LOCK_TAG                    'FrWA'

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
#define DIFF_BLOCK_OFFSET_MASK                  (DIFF_BLOCK_SIZE - 1)
#define DIFF_BLOCK_BASE_MASK                    (~(DIFF_BLOCK_SIZE - 1))
#define DIFF_GET_BLOCK_NUMBER(a)                ((a) >> DIFF_BLOCK_BITS)
#define DIFF_GET_NUMBER_OF_BLOCKS(a)            (((a) + DIFF_BLOCK_OFFSET_MASK) >> DIFF_BLOCK_BITS)
#define DIFF_GET_BLOCK_OFFSET(a)                ((ULONG)((a) & DIFF_BLOCK_OFFSET_MASK))
#define DIFF_GET_BLOCK_BASE_FROM_ABS_OFFSET(a)  ((a) & DIFF_BLOCK_BASE_MASK)

#define DIFF_BLOCK_UNALLOCATED                  (0x00000000UL)

#define SECTOR_BITS                             9
#define SECTOR_SIZE                             (1L << SECTOR_BITS)

#define IDLE_TRIM_BLOCKS_INTERVAL               32

#define ACCESS_FROM_CTL_CODE(ctrlCode)          ((UCHAR)((ctrlCode >> 14) & 0x03))
#define FUNCTN_FROM_CTL_CODE(ctrlCode)          (((ctrlCode) >> 2) & 0xfff)

#ifndef _countof
#define _countof(_Array) (sizeof(_Array) / sizeof(_Array[0]))
#endif

#ifndef FIELD_SIZE
#define FIELD_SIZE(type, field) (sizeof(((type *)0)->field))
#endif

#ifdef _WIN64
#define InterlockedExchangeAddPtr InterlockedExchangeAdd64
#else
#define InterlockedExchangeAddPtr InterlockedExchangeAdd
#endif

#ifdef POOL_TAGGING
#ifdef ExAllocatePool
#undef ExAllocatePool
#endif
#define ExAllocatePool(a,b) ExAllocatePoolWithTag(a,b,POOL_TAG)
#ifdef ExFreePool
#undef ExFreePool
#endif
#define ExFreePool(a) ExFreePoolWithTag(a,POOL_TAG)
#endif

#pragma warning(disable: 4200)

inline void *operator_new(size_t Size, UCHAR FillByte)
{
    void * result = ExAllocatePoolWithTag(NonPagedPool, Size, POOL_TAG);

    if (result != NULL)
    {
        RtlFillMemory(result, Size, FillByte);
    }

    return result;
}

inline void operator_delete(void *Ptr)
{
    if (Ptr != NULL)
    {
        ExFreePoolWithTag(Ptr, POOL_TAG);
    }
}

template<typename T> class WPoolMem
{
protected:
    T *ptr;
    SIZE_T bytecount;

    explicit WPoolMem(T* pBlk, SIZE_T AllocationSize)
        : ptr(pBlk),
        bytecount(pBlk != NULL ? AllocationSize : 0) { }

    bool ReAlloc(T* pBlk, SIZE_T AllocateSize)
    {
        Free();
        ptr = pBlk;
        if (ptr != NULL)
        {
            bytecount = AllocateSize;
            return true;
        }
        return false;
    }

public:
    operator bool()
    {
        return ptr != NULL;
    }

    bool operator!()
    {
        return ptr == NULL;
    }

    operator T*()
    {
        return ptr;
    }

    T* operator ->()
    {
        return ptr;
    }

    T& operator[](int i)
    {
        return ptr[i];
    }

    T* operator+(int i)
    {
        return ptr + i;
    }

    T* operator-(int i)
    {
        return ptr - i;
    }

    T* operator =(T *pBlk)
    {
        Free();
        return ptr = pBlk;
    }

    SIZE_T Count() const
    {
        return GetSize() / sizeof(T);
    }

    SIZE_T GetSize() const
    {
        return ptr != NULL ? bytecount : 0;
    }

    void Free()
    {
        if (ptr != NULL)
        {
            ExFreePool(ptr);
            ptr = NULL;
        }
        bytecount = 0;
    }

    void Clear()
    {
        if ((ptr != NULL) && (bytecount > 0))
        {
            RtlZeroMemory(ptr, bytecount);
        }
    }

    T* Abandon()
    {
        T* ab_ptr = ptr;
        ptr = NULL;
        bytecount = 0;
        return ab_ptr;
    }

    WPoolMem() :
        ptr(NULL),
        bytecount(0) { }

    ~WPoolMem()
    {
        Free();
    }
};

template<typename T> class WPagedPoolMem : public WPoolMem < T >
{
public:
    explicit WPagedPoolMem(SIZE_T AllocateSize)
        : WPoolMem((T*)ExAllocatePool(PagedPool, AllocateSize), AllocateSize)
    {
    }

    WPagedPoolMem()
        : WPoolMem()
    {
    }

    bool ReAlloc(SIZE_T AllocateSize)
    {
        return WPoolMem::ReAlloc((T*)ExAllocatePool(PagedPool, AllocateSize), AllocateSize);
    }
};

template<typename T> class WNonPagedPoolMem : public WPoolMem < T >
{
public:
    explicit WNonPagedPoolMem(SIZE_T AllocateSize)
        : WPoolMem((T*)ExAllocatePool(NonPagedPool, AllocateSize), AllocateSize)
    {
    }

    WNonPagedPoolMem()
        : WPoolMem()
    {
    }

    bool ReAlloc(SIZE_T AllocateSize)
    {
        return WPoolMem::ReAlloc((T*)ExAllocatePool(NonPagedPool, AllocateSize), AllocateSize);
    }
};

class WHandle
{
private:
    HANDLE h;

public:
    operator bool()
    {
        return h != NULL;
    }

    bool operator !()
    {
        return h == NULL;
    }

    operator HANDLE()
    {
        return h;
    }

    void Close()
    {
        if (h != NULL)
        {
            ZwClose(h);
            h = NULL;
        }
    }

    WHandle() :
        h(NULL) { }

    explicit WHandle(HANDLE h) :
        h(h) { }

    ~WHandle()
    {
        Close();
    }
};

//
// Device Extension
//

typedef struct _DEVICE_EXTENSION
{

    //
    // Back pointer to our device object
    //
    PDEVICE_OBJECT DeviceObject;

    //
    // Target Device Object, next in stack
    //
    PDEVICE_OBJECT TargetDeviceObject;

    //
    // Physical device object, last in stack
    //
    PDEVICE_OBJECT PhysicalDeviceObject;

    //
    // Statistics, performance counters etc accessible
    // using IOCTL from user mode
    //
    AIMWRFLTR_DEVICE_STATISTICS Statistics;

    //
    // Pointer to diff block allocation table
    //
    LONG volatile * AllocationTable;

    //
    // FILE_OBJECT for diff device
    //
    PFILE_OBJECT DiffFileObject;

    //
    // DEVICE_OBJECT for diff device
    //
    PDEVICE_OBJECT DiffDeviceObject;

    //
    // Handle to diff device
    //
    HANDLE DiffDeviceHandle;

    //
    // RemoveLock prevents removal of a device while it is busy.
    //
    IO_REMOVE_LOCK RemoveLock;

    //
    // 
    //
    LIST_ENTRY ListHead;

    //
    // 
    //
    KSPIN_LOCK ListLock;

    //
    // 
    //
    KEVENT ListEvent;

    //
    //
    //
    KEVENT InitializationEvent;

    //
    //
    //
    KGUARDED_MUTEX InitializationMutex;

    //
    // Request to shutdown worker thread
    //
    bool ShutdownThread;

    bool QueryRemoveDeviceSent;
    bool SurpriseRemoveDeviceSent;
    bool CancelRemoveDeviceSent;

    //
    // Handle to running worker thread
    //
    HANDLE WorkerThread;

    //
    // must synchronize paging path notifications
    //
    KEVENT PagingPathCountEvent;

    //
    // must synchronize paging path notifications
    //
    KGUARDED_MUTEX PagingPathCountMutex;

    //
    //
    //
    PIRP CompletingIrp;

} DEVICE_EXTENSION, *PDEVICE_EXTENSION;

typedef struct _CACHED_IRP
{
    //
    //
    //
    LIST_ENTRY ListEntry;

    //
    // Copy of IO_STACK_LOCATION from original IRP
    //
    IO_STACK_LOCATION IoStack;

    //
    // Enqueued IRP that should be issued from worker thread
    //
    PIRP Irp;

    //
    // DeviceObject to use when issuing enqueued IRP from worker thread
    //
    PDEVICE_OBJECT DeviceObject;

    //
    // Buffer with copy of data to write 
    //
    UCHAR Buffer[];

    //
    // Creates a work item for a pending IRP that will be forwarded to a target device by worker
    // thread
    //
    static _CACHED_IRP* CreateEnqueuedForwardIrp(PDEVICE_OBJECT DeviceObject, PIRP Irp)
    {
        PCACHED_IRP cached_irp = new CACHED_IRP;

        if (cached_irp == NULL)
        {
            return NULL;
        }

        cached_irp->DeviceObject = DeviceObject;
        cached_irp->Irp = Irp;

        return cached_irp;
    }

    //
    // Creates a work item for a pending IRP that will be completed by worker
    // thread
    //
    static _CACHED_IRP* CreateEnqueuedIrp(PIRP Irp)
    {
        PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

        PCACHED_IRP cached_irp = new CACHED_IRP;

        if (cached_irp == NULL)
        {
            return NULL;
        }

        cached_irp->Irp = Irp;
        cached_irp->IoStack = *io_stack;

        return cached_irp;
    }

    //
    // Creates a cache work item based on a write IRP. All required data is cached
    // so that the supplied IRP is not needed by worker thread later
    //
    static _CACHED_IRP *CreateFromWriteIrp(PDEVICE_OBJECT DeviceObject, PIRP Irp)
    {
        PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

        PCACHED_IRP cached_irp = NULL;

        if (io_stack->MajorFunction == IRP_MJ_WRITE)
        {
            PUCHAR buffer = NULL;

            if (DeviceObject->Flags & DO_BUFFERED_IO)
            {
                buffer = (PUCHAR)Irp->AssociatedIrp.SystemBuffer;
            }
            else
            {
                buffer = (PUCHAR)MmGetSystemAddressForMdlSafe(
                    Irp->MdlAddress, NormalPagePriority);
            }

            if (buffer == NULL)
            {
                return NULL;
            }

            SIZE_T cached_irp_size = FIELD_OFFSET(CACHED_IRP, Buffer) + (SIZE_T)io_stack->Parameters.Write.Length;
            cached_irp = (PCACHED_IRP)ExAllocatePool(NonPagedPool, cached_irp_size);

            if (cached_irp == NULL)
            {
                return NULL;
            }

            RtlZeroMemory(cached_irp, cached_irp_size);
            cached_irp->IoStack = *io_stack;
            RtlCopyMemory(cached_irp->Buffer, buffer, io_stack->Parameters.Write.Length);
        }
        else if ((io_stack->MajorFunction == IRP_MJ_DEVICE_CONTROL ||
            io_stack->MajorFunction == IRP_MJ_FILE_SYSTEM_CONTROL ||
            io_stack->MajorFunction == IRP_MJ_INTERNAL_DEVICE_CONTROL) &&
            METHOD_FROM_CTL_CODE(io_stack->Parameters.DeviceIoControl.IoControlCode) == METHOD_BUFFERED)
        {
            PUCHAR buffer = (PUCHAR)Irp->AssociatedIrp.SystemBuffer;

            SIZE_T cached_irp_size = FIELD_OFFSET(CACHED_IRP, Buffer) + (SIZE_T)io_stack->Parameters.DeviceIoControl.InputBufferLength;
            cached_irp = (PCACHED_IRP)ExAllocatePool(NonPagedPool, cached_irp_size);
                
            if (cached_irp == NULL)
            {
                return NULL;
            }

            RtlZeroMemory(cached_irp, cached_irp_size);
            cached_irp->IoStack = *io_stack;
            if (io_stack->Parameters.DeviceIoControl.InputBufferLength > 0)
            {
                RtlCopyMemory(cached_irp->Buffer, buffer, io_stack->Parameters.DeviceIoControl.InputBufferLength);
            }
        }
        else
        {
            cached_irp = new CACHED_IRP;

            if (cached_irp == NULL)
            {
                return NULL;
            }

            cached_irp->IoStack = *io_stack;
        }

        return cached_irp;
    }

} CACHED_IRP, *PCACHED_IRP;

//
// Function to free a driver allocated IRP, including unlocking and
// freeing all MDLs assigned to the IRP.
//
FORCEINLINE
VOID
AIMWrFltrFreeIrpWithMdls(IN PIRP Irp)
{
    if (Irp->MdlAddress != NULL)
    {
        PMDL mdl = Irp->MdlAddress;
        while (mdl != NULL)
        {
            PMDL nextMdl = mdl->Next;

            if (mdl->MdlFlags & MDL_PAGES_LOCKED)
            {
                MmUnlockPages(mdl);
            }

            IoFreeMdl(mdl);

            mdl = nextMdl;
        }
        Irp->MdlAddress = NULL;
    }

    IoFreeIrp(Irp);
}

typedef class SCATTERED_IRP
{
    PIRP OriginalIrp;

    PDEVICE_OBJECT OriginalDeviceObject;

    volatile LONG ScatterCount;

    volatile NTSTATUS LastFailedStatus;

    volatile LONG_PTR BytesCompleted;

    PIO_REMOVE_LOCK RemoveLock;

    PUCHAR SystemBuffer;

    PUCHAR AllocatedBuffer;

    static IO_COMPLETION_ROUTINE IrpCompletionRoutine;

    ~SCATTERED_IRP()
    {
        OriginalIrp->IoStatus.Status = LastFailedStatus;

        if (NT_SUCCESS(LastFailedStatus))
        {
            OriginalIrp->IoStatus.Information = BytesCompleted;

            IoCompleteRequest(OriginalIrp, IO_DISK_INCREMENT);
        }
        else
        {
            KdPrint(("AIMWrFltr::Complete: Lower level I/O failed: 0x%X\n",
                LastFailedStatus));

            //KdBreakPoint();

            OriginalIrp->IoStatus.Information = 0;

            IoCompleteRequest(OriginalIrp, IO_NO_INCREMENT);
        }

        IoReleaseRemoveLock(RemoveLock, OriginalIrp);

        delete[] AllocatedBuffer;
    }

public:
    void Complete()
    {
        LONG scatter_items = InterlockedDecrement(&ScatterCount);
        if (scatter_items == 0)
        {
            delete this;
        }
    }

    static NTSTATUS Create(
        SCATTERED_IRP **Object,
        PDEVICE_OBJECT OriginalDeviceObject,
        PIRP OriginalIrp,
        PIO_REMOVE_LOCK RemoveLock,
        PUCHAR SystemBuffer = NULL)
    {
        //
        // Acquire the remove lock so that device will not be removed while
        // processing original irp.
        //
        NTSTATUS status = IoAcquireRemoveLock(RemoveLock, OriginalIrp);
        if (!NT_SUCCESS(status))
        {
            DbgPrint("AIMWrFltr::Create: Remove lock failed Irp type %i\n",
                IoGetCurrentIrpStackLocation(OriginalIrp)->MajorFunction);

            return status;
        }

        *Object = new SCATTERED_IRP;

        if (*Object == NULL)
        {
            DbgPrint("AIMWrFltr::Create: Memory allocation error.\n");

            IoReleaseRemoveLock(RemoveLock, OriginalIrp);

            return STATUS_INSUFFICIENT_RESOURCES;
        }

        (*Object)->OriginalIrp = OriginalIrp;
        (*Object)->OriginalDeviceObject = OriginalDeviceObject;
        (*Object)->RemoveLock = RemoveLock;
        (*Object)->ScatterCount = 1;
        (*Object)->SystemBuffer = SystemBuffer;

        IoMarkIrpPending(OriginalIrp);

        return STATUS_SUCCESS;
    }

    PIRP BuildIrp(
        UCHAR MajorFunction,
        PDEVICE_OBJECT DeviceObject,
        PFILE_OBJECT FileObject,
        ULONG OriginalIrpOffset,
        ULONG BytesThisIrp,
        PLARGE_INTEGER LowerDeviceOffset);

} *PSCATTERED_IRP;

typedef struct PARTIAL_IRP
{
    PSCATTERED_IRP Scatter;

    ULONG OriginalIrpOffset;

    ULONG BytesThisIrp;

#ifdef DBG
    ULONGLONG LowerDeviceOffset;
#endif

    bool CopyBack;

} *PPARTIAL_IRP;

//
// Legacy compatibility
//
#ifndef _In_

#define _In_
#define _Inout_
#define _In_reads_opt_(x)
#define _Dispatch_type_(n)
#define _IRQL_requires_max_(n)

typedef
NTSTATUS
DRIVER_DISPATCH(
    _In_ struct _DEVICE_OBJECT *DeviceObject,
    _Inout_ struct _IRP *Irp
);

typedef DRIVER_DISPATCH *PDRIVER_DISPATCH;

#endif

//
// Function declarations
//

extern "C"
{
    DRIVER_INITIALIZE DriverEntry;

    DRIVER_ADD_DEVICE AIMWrFltrAddDevice;

    _IRQL_requires_max_(APC_LEVEL)
        DRIVER_DISPATCH AIMWrFltrForwardIrpSynchronous;

    _Dispatch_type_(IRP_MJ_PNP)
        _IRQL_requires_max_(APC_LEVEL)
        DRIVER_DISPATCH AIMWrFltrPnp;

    DRIVER_DISPATCH AIMWrFltrSendToNextDriver;

    _IRQL_requires_max_(APC_LEVEL)
        _Dispatch_type_(IRP_MJ_CREATE)
        DRIVER_DISPATCH AIMWrFltrCreate;

    _Dispatch_type_(IRP_MJ_CLEANUP) DRIVER_DISPATCH AIMWrFltrCleanup;

    _Dispatch_type_(IRP_MJ_CLOSE) DRIVER_DISPATCH AIMWrFltrClose;

    _Dispatch_type_(IRP_MJ_READ) DRIVER_DISPATCH AIMWrFltrRead;

    _Dispatch_type_(IRP_MJ_WRITE) DRIVER_DISPATCH AIMWrFltrWrite;

    _Dispatch_type_(IRP_MJ_DEVICE_CONTROL)
        _Dispatch_type_(IRP_MJ_INTERNAL_DEVICE_CONTROL)
        DRIVER_DISPATCH AIMWrFltrDeviceControl;

    _Dispatch_type_(IRP_MJ_FLUSH_BUFFERS)
        DRIVER_DISPATCH AIMWrFltrFlushBuffers;

    DRIVER_DISPATCH AIMWrFltrTrim;

    _IRQL_requires_max_(APC_LEVEL)
        DRIVER_DISPATCH AIMWrFltrStartDevice;

    _IRQL_requires_max_(APC_LEVEL)
        DRIVER_DISPATCH AIMWrFltrRemoveDevice;

    _IRQL_requires_max_(APC_LEVEL)
        DRIVER_DISPATCH AIMWrFltrDeviceUsageNotification;

    IO_COMPLETION_ROUTINE AIMWrFltrSynchronousIrpCompletion;

    DRIVER_UNLOAD AIMWrFltrUnload;

    KSTART_ROUTINE AIMWrFltrDeviceWorkerThread;

    NTSTATUS
        AIMWrFltrDeferredRead(
            PDEVICE_EXTENSION DeviceExtension,
            PIRP Irp,
            PUCHAR BlockBuffer);

    NTSTATUS
        AIMWrFltrDeferredWrite(
            PDEVICE_EXTENSION DeviceExtension,
            PCACHED_IRP Irp,
            PUCHAR BlockBuffer);

    NTSTATUS
        AIMWrFltrDeferredFlushBuffers(
            PDEVICE_EXTENSION DeviceExtension,
            PCACHED_IRP Irp);

    NTSTATUS
        AIMWrFltrDeferredManageDataSetAttributes(
            PDEVICE_EXTENSION DeviceExtension,
            PCACHED_IRP Irp,
            PUCHAR BlockBuffer);

    VOID
        AIMWrFltrIdleTrim(
            PDEVICE_EXTENSION DeviceExtension,
            PUCHAR BlockBuffer);

    VOID
        AIMWrFltrLogError(IN PDEVICE_OBJECT DeviceObject,
            IN ULONG UniqueId,
            IN NTSTATUS ErrorCode, IN NTSTATUS Status);

    VOID
        AIMWrFltrSyncFilterWithTarget(IN PDEVICE_OBJECT FilterDevice,
            IN PDEVICE_OBJECT TargetDevice);

    VOID
        AIMWrFltrCleanupDevice(IN PDEVICE_EXTENSION DeviceExtension);

    NTSTATUS
        AIMWrFltrSynchronousDeviceControl(
            IN PDEVICE_OBJECT DeviceObject,
            IN PFILE_OBJECT FileObject,
            IN UCHAR MajorFunction,
            IN ULONG IoControlCode,
            IN OUT PVOID SystemBuffer = NULL,
            IN ULONG InputBufferLength = 0,
            IN ULONG OutputBufferLength = 0,
            OUT PIO_STATUS_BLOCK IoStatus = NULL);

#if DBG
    PCSTR
        AIMWrFltrGetIoctlDeviceTypeName(ULONG ctrlCode);

    PCSTR
        AIMWrFltrGetIoctlMethodName(ULONG ctrlCode);

    PCSTR
        AIMWrFltrGetIoctlAccessName(ULONG ctrlCode);

    PCSTR
        AIMWrFltrGetIoctlFunctionName(ULONG ctrlCode);
#endif

    NTSTATUS
        AIMWrFltrSynchronousReadWrite(
            IN PDEVICE_OBJECT DeviceObject,
            IN PFILE_OBJECT FileObject,
            IN UCHAR MajorFunction,
            IN OUT PVOID SystemBuffer = NULL,
            IN ULONG BufferLength = 0,
            IN PLARGE_INTEGER StartingOffset = NULL,
            IN PETHREAD Thread = NULL,
            OUT PIO_STATUS_BLOCK IoStatus = NULL);

    NTSTATUS
        AIMWrFltrGetDiffDevicePath(IN PUNICODE_STRING MountDevName,
            OUT PKEY_VALUE_PARTIAL_INFORMATION DiffDevicePath,
            IN ULONG DiffDevicePathSize);

    NTSTATUS
        AIMWrFltrInitializeDiffDevice(IN PDEVICE_EXTENSION DeviceExtension);

    NTSTATUS
        AIMWrFltrInitializeDiffDeviceUnsafe(IN PDEVICE_EXTENSION DeviceExtension);

    FORCEINLINE
        PDEVICE_OBJECT
        AIMWrFltrGetLowerDeviceObjectAndDereference(
            IN PDEVICE_OBJECT DeviceObject)
    {
        PDEVICE_OBJECT lower_device = IoGetLowerDeviceObject(DeviceObject);
        ObDereferenceObject(DeviceObject);
        return lower_device;
    }

    FORCEINLINE
        NTSTATUS
        AIMWrFltrHandleRemovedDevice(PIRP Irp)
    {
        KdPrint(("AIMWrFltr: IRP %p MJ %#x sent to device that is being removed.\n",
            Irp, (int)IoGetCurrentIrpStackLocation(Irp)->MajorFunction));

        Irp->IoStatus.Information = 0;
        Irp->IoStatus.Status = STATUS_DEVICE_DOES_NOT_EXIST;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_DEVICE_DOES_NOT_EXIST;
    }

    extern HANDLE AIMWrFltrParametersKey;
    extern PKEVENT AIMWrFltrDiffFullEvent;
    extern PDRIVER_OBJECT AIMWrFltrDriverObject;
    extern bool AIMWrFltrLinksCreated;
    extern bool QueueWithoutCache;

#if _NT_TARGET_VERSION >= 0x501

    FORCEINLINE
        VOID
        __drv_maxIRQL(DISPATCH_LEVEL)
        __drv_savesIRQLGlobal(QueuedSpinLock, LockHandle)
        __drv_setsIRQL(DISPATCH_LEVEL)
        AIMWrFltrAcquireLock_x64(__inout __deref PKSPIN_LOCK SpinLock,
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
        AIMWrFltrReleaseLock_x64(
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
        AIMWrFltrAcquireLock_x86(__inout __deref __drv_acquiresExclusiveResource(KeSpinLockType) PKSPIN_LOCK SpinLock,
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
        AIMWrFltrReleaseLock_x86(
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

#define AIMWrFltrAcquireLock AIMWrFltrAcquireLock_x64

#define AIMWrFltrReleaseLock AIMWrFltrReleaseLock_x64

#else

#define AIMWrFltrAcquireLock(SpinLock, LockHandle, LowestAssumedIrql) \
    { \
        (LockHandle)->LockQueue.Lock = (SpinLock); \
        AIMWrFltrAcquireLock_x86((LockHandle)->LockQueue.Lock, &(LockHandle)->OldIrql, (LowestAssumedIrql)); \
    }

#define AIMWrFltrReleaseLock(LockHandle, LowestAssumedIrql) \
    { \
        AIMWrFltrReleaseLock_x86((LockHandle)->LockQueue.Lock, (LockHandle)->OldIrql, (LowestAssumedIrql)); \
    }

#endif

}
