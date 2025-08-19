/*
AWE Allocation Driver for Windows 2000/XP and later.

Copyright (C) 2005-2025 Olof Lagerkvist, Arsenal Recon.

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or
sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

*/

#include <ntifs.h>
#include <wdm.h>
#include <ntdddisk.h>
#include <ntintsafe.h>
#include <ntverp.h>

#include "..\phdskmnt\inc\ntkmapi.h"

///
/// Definitions and imports are now in the "sources" file and managed by the
/// build utility.
///

#ifndef DEBUG_LEVEL
#define DEBUG_LEVEL 0
#endif

#if DEBUG_LEVEL >= 2
#define KdPrint2(x) DbgPrint x
#else
#define KdPrint2(x)
#endif

#if DEBUG_LEVEL >= 1
#undef KdPrint
#define KdPrint(x)  DbgPrint x
#endif

/* d099e6fd-9287-4c62-a728-dfa1d7ab15b1 */
const GUID AWEAllocFsCtlGuid = \
{ 0xd099e6fd, 0x9287, 0x4c62, { 0xa7, 0x28, 0xdf, 0xa1, 0xd7, 0xab, 0x15, 0xb1 } };

#define FILE_DEVICE_IMDISK                  0x8372

#define FSCTL_AWEALLOC_QUERY_CONTEXT        CTL_CODE(FILE_DEVICE_FILE_SYSTEM, 2393, METHOD_BUFFERED, FILE_ANY_ACCESS)

#define POOL_TAG                            'AEWA'

#define AWEALLOC_DEVICE_NAME                L"\\Device\\AWEAlloc"
#define AWEALLOC_SYMLINK_NAME               L"\\DosDevices\\AWEAlloc"

#define HIGH_MEMORY_CONDITION_NAME          L"\\KernelObjects\\HighMemoryCondition"

#define AWEALLOC_STATUS_ALLOCATION_LOW_MEMORY ((STATUS_SEVERITY_INFORMATIONAL << 30) | \
    (1 << 29) | \
    (STATUS_INSUFFICIENT_RESOURCES & 0x1FFFFFFF))

// Linked list of MDLs that describe physical memory allocations

typedef struct _BLOCK_DESCRIPTOR
{
    LONGLONG Offset;

    PMDL Mdl;

    struct _BLOCK_DESCRIPTOR *NextBlock;

} BLOCK_DESCRIPTOR, *PBLOCK_DESCRIPTOR;

#define INVALID_OFFSET (-1LL)

// Context information for a page mapped into virtual address space

typedef struct _PAGE_CONTEXT
{
    LONGLONG UsageCount;
    LONGLONG PageBase;
    PMDL Mdl;
    PUCHAR Ptr;
} PAGE_CONTEXT, *PPAGE_CONTEXT;

// FILE_OBJECT::FsContext2 for "files" handled by this driver

typedef struct _OBJECT_CONTEXT
{
    LONGLONG VirtualSize;

    PBLOCK_DESCRIPTOR FirstBlock;

    PAGE_CONTEXT LatestPageContext;

    volatile LONGLONG AsynchronousExchangeHit;

    KSPIN_LOCK IOLock;

    volatile LONG ActiveReaders;

    volatile LONG ActiveWriters;

    volatile LONGLONG ReadRequestLockConflicts;

    volatile LONGLONG WriteRequestLockConflicts;

    BOOLEAN UseNumaNumber;

    LONG NumaNumber;

} OBJECT_CONTEXT, *POBJECT_CONTEXT;

//
// Default page size 2 MB
//
#define ALLOC_PAGE_SIZE (2ULL << 20)

//
// Macros for easier page/offset calculation
//
#define ALLOC_PAGE_BASE_MASK   (~(ALLOC_PAGE_SIZE-1))
#define AWEAllocGetPageBaseFromAbsOffset(absolute_offset) \
  ((absolute_offset) & ALLOC_PAGE_BASE_MASK)
#define ALLOC_PAGE_OFFSET_MASK (ALLOC_PAGE_SIZE-1)
#define AWEAllocGetPageOffsetFromAbsOffset(absolute_offset) \
  ((ULONG)((absolute_offset) & ALLOC_PAGE_OFFSET_MASK))
#define AWEAllocGetRequiredPagesForSize(size) \
  (((size) + ALLOC_PAGE_OFFSET_MASK) & ALLOC_PAGE_BASE_MASK)
#define MAX_BLOCK_SIZE (ULONG_MAX - ALLOC_PAGE_OFFSET_MASK)

#ifndef __drv_dispatchType
#define __drv_dispatchType(f)
#endif

//
// Prototypes for functions defined in this driver
//

DRIVER_INITIALIZE DriverEntry;

DRIVER_UNLOAD AWEAllocUnload;

__drv_dispatchType(IRP_MJ_CREATE)
DRIVER_DISPATCH AWEAllocCreate;

__drv_dispatchType(IRP_MJ_CLOSE)
DRIVER_DISPATCH AWEAllocClose;

__drv_dispatchType(IRP_MJ_DEVICE_CONTROL)
__drv_dispatchType(IRP_MJ_FILE_SYSTEM_CONTROL)
DRIVER_DISPATCH AWEAllocControl;

__drv_dispatchType(IRP_MJ_QUERY_INFORMATION)
DRIVER_DISPATCH AWEAllocQueryInformation;

__drv_dispatchType(IRP_MJ_SET_INFORMATION)
DRIVER_DISPATCH AWEAllocSetInformation;

__drv_dispatchType(IRP_MJ_FLUSH_BUFFERS)
DRIVER_DISPATCH AWEAllocFlushBuffers;

__drv_dispatchType(IRP_MJ_READ)
__drv_dispatchType(IRP_MJ_WRITE)
DRIVER_DISPATCH AWEAllocReadWrite;

VOID
AWEAllocLogError(IN PVOID Object,
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

NTSTATUS
AWEAllocSetSize(IN POBJECT_CONTEXT Context,
IN OUT PIO_STATUS_BLOCK IoStatus,
IN PLARGE_INTEGER EndOfFile);

const PHYSICAL_ADDRESS physical_address_zero = { 0, 0 };
const PHYSICAL_ADDRESS physical_address_max64 = { ULONG_MAX, ULONG_MAX };
#ifndef _WIN64
const PHYSICAL_ADDRESS physical_address_4GB = { 0, 1UL };
const PHYSICAL_ADDRESS physical_address_5GB = { 1UL << 30, 1UL };
const PHYSICAL_ADDRESS physical_address_6GB = { 1UL << 31, 1UL };
const PHYSICAL_ADDRESS physical_address_8GB = { 0, 2UL };
const PHYSICAL_ADDRESS physical_address_max32 = { ULONG_MAX, 0 };
#endif

//
// Optimized spin lock functions
//

#if _WIN32_WINNT >= 0x0502

FORCEINLINE
VOID
__drv_maxIRQL(DISPATCH_LEVEL)
__drv_when(LowestAssumedIrql < DISPATCH_LEVEL, __drv_savesIRQLGlobal(QueuedSpinLock, LockHandle))
__drv_when(LowestAssumedIrql < DISPATCH_LEVEL, __drv_setsIRQL(DISPATCH_LEVEL))
AWEAllocAcquireLock_x64(__inout PKSPIN_LOCK SpinLock,
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
__drv_when(*LowestAssumedIrql < DISPATCH_LEVEL, __drv_restoresIRQLGlobal(QueuedSpinLock, LockHandle))
AWEAllocReleaseLock_x64(
__in __deref __drv_releasesExclusiveResource(KeQueuedSpinLockType)
PKLOCK_QUEUE_HANDLE LockHandle,
__inout PKIRQL LowestAssumedIrql)
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

#endif

FORCEINLINE
VOID
__drv_maxIRQL(DISPATCH_LEVEL)
__drv_when(LowestAssumedIrql < DISPATCH_LEVEL, __drv_setsIRQL(DISPATCH_LEVEL))
AWEAllocAcquireLock_x86(__inout __deref __drv_acquiresExclusiveResource(KeSpinLockType) PKSPIN_LOCK SpinLock,
__drv_when(LowestAssumedIrql < DISPATCH_LEVEL, __out __deref __drv_savesIRQL) PKIRQL OldIrql,
__in KIRQL LowestAssumedIrql)
{
    if (LowestAssumedIrql >= DISPATCH_LEVEL)
    {
        ASSERT(KeGetCurrentIrql() >= DISPATCH_LEVEL);

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
AWEAllocReleaseLock_x86(
__inout __deref __drv_releasesExclusiveResource(KeSpinLockType) PKSPIN_LOCK SpinLock,
__in __drv_when(*LowestAssumedIrql < DISPATCH_LEVEL, __drv_restoresIRQL) KIRQL OldIrql,
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

#define AWEAllocAcquireLock AWEAllocAcquireLock_x64

#define AWEAllocReleaseLock AWEAllocReleaseLock_x64

#else

#define AWEAllocAcquireLock(SpinLock, LockHandle, LowestAssumedIrql) \
    { \
        (LockHandle)->LockQueue.Lock = (SpinLock); \
        AWEAllocAcquireLock_x86((LockHandle)->LockQueue.Lock, &(LockHandle)->OldIrql, (LowestAssumedIrql)); \
    }

#define AWEAllocReleaseLock(LockHandle, LowestAssumedIrql) \
    { \
        AWEAllocReleaseLock_x86((LockHandle)->LockQueue.Lock, (LockHandle)->OldIrql, (LowestAssumedIrql)); \
    }

#endif

PDRIVER_OBJECT AWEAllocDriverObject;

PKEVENT HighMemoryCondition;

#pragma code_seg("INIT")

//
// This is where it all starts...
//
NTSTATUS
DriverEntry(IN PDRIVER_OBJECT DriverObject,
IN PUNICODE_STRING RegistryPath)
{
    NTSTATUS status;
    PDEVICE_OBJECT device_object;
    UNICODE_STRING high_memory_condition_name;
    HANDLE high_memory_condition_handle;
    OBJECT_ATTRIBUTES object_attributes = { 0 };
    UNICODE_STRING ctl_device_name;
    UNICODE_STRING sym_link;

#if DBG
    if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
        DbgBreakPoint();
#endif

    AWEAllocDriverObject = DriverObject;

    MmPageEntireDriver((PVOID)(ULONG_PTR)DriverEntry);

    RtlInitUnicodeString(&high_memory_condition_name,
        HIGH_MEMORY_CONDITION_NAME);

    InitializeObjectAttributes(&object_attributes, &high_memory_condition_name, 0, NULL, NULL);

    status = ZwOpenEvent(&high_memory_condition_handle,
        SYNCHRONIZE | EVENT_QUERY_STATE, &object_attributes);

    if (!NT_SUCCESS(status))
        return status;

    status = ObReferenceObjectByHandle(high_memory_condition_handle,
        SYNCHRONIZE | EVENT_QUERY_STATE, *ExEventObjectType, KernelMode,
        &HighMemoryCondition, NULL);

    ZwClose(high_memory_condition_handle);

    if (!NT_SUCCESS(status))
        return status;

    // Create the control device.
    RtlInitUnicodeString(&ctl_device_name, AWEALLOC_DEVICE_NAME);

    status = IoCreateDevice(DriverObject,
        0,
        &ctl_device_name,
        FILE_DEVICE_NULL,
        0,
        FALSE,
        &device_object);

    if (!NT_SUCCESS(status))
        return status;

    device_object->Flags |= DO_DIRECT_IO;

    RtlInitUnicodeString(&sym_link, AWEALLOC_SYMLINK_NAME);
    status = IoCreateUnprotectedSymbolicLink(&sym_link, &ctl_device_name);
    if (!NT_SUCCESS(status))
    {
        IoDeleteDevice(device_object);
        return status;
    }

    DriverObject->MajorFunction[IRP_MJ_CREATE] = AWEAllocCreate;
    DriverObject->MajorFunction[IRP_MJ_CLOSE] = AWEAllocClose;
    DriverObject->MajorFunction[IRP_MJ_READ] = AWEAllocReadWrite;
    DriverObject->MajorFunction[IRP_MJ_WRITE] = AWEAllocReadWrite;
    DriverObject->MajorFunction[IRP_MJ_FLUSH_BUFFERS] = AWEAllocFlushBuffers;
    DriverObject->MajorFunction[IRP_MJ_FILE_SYSTEM_CONTROL] = AWEAllocControl;
    DriverObject->MajorFunction[IRP_MJ_QUERY_INFORMATION] =
        AWEAllocQueryInformation;
    DriverObject->MajorFunction[IRP_MJ_SET_INFORMATION] =
        AWEAllocSetInformation;

    DriverObject->DriverUnload = AWEAllocUnload;

    KdPrint((__FUNCTION__ ": Initialization done. Leaving DriverEntry().\n"));

    return STATUS_SUCCESS;
}

#pragma code_seg()

NTSTATUS
AWEAllocTryAcquireProtection(IN POBJECT_CONTEXT Context,
IN BOOLEAN ForWriteOperation,
IN BOOLEAN OwnsReadLock OPTIONAL,
IN OUT PKIRQL LowestAssumedIrql)
{
    NTSTATUS status;
    KLOCK_QUEUE_HANDLE lock_handle = { 0 };

    AWEAllocAcquireLock(&Context->IOLock, &lock_handle, *LowestAssumedIrql);
    if (ForWriteOperation)
    {
        if ((Context->ActiveWriters >= 1) |
            (Context->ActiveReaders >= (OwnsReadLock ? 2 : 1)))
        {
            Context->WriteRequestLockConflicts++;
            KdPrint2((__FUNCTION__ "AWEAlloc: I/O write protection busy while requesting lock for writing. Active readers: %i writers: %i\n",
                Context->ActiveReaders, Context->ActiveWriters));

            status = STATUS_DEVICE_BUSY;
        }
        else
        {
            Context->ActiveWriters++;
            if (Context->ActiveWriters <= 0)
            {
                DbgPrint(__FUNCTION__ ": I/O synchronization state corrupt.\n");

#if DBG
                if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                    DbgBreakPoint();
#endif

                status = STATUS_DRIVER_INTERNAL_ERROR;
            }
            else
            {
                KdPrint2((__FUNCTION__ ": Thread %p acquired lock for writing. Active readers: %i writers: %i\n",
                    KeGetCurrentThread(),
                    Context->ActiveReaders, Context->ActiveWriters));

                status = STATUS_SUCCESS;
            }
        }
    }
    else
    {
        if ((Context->ActiveWriters >= 1) | (Context->ActiveReaders >= LONG_MAX))
        {
            Context->ReadRequestLockConflicts++;
            KdPrint2((__FUNCTION__ ": I/O write protection busy while requesting lock for reading. Active readers: %i writers: %i\n",
                Context->ActiveReaders, Context->ActiveWriters));

            status = STATUS_DEVICE_BUSY;
        }
        else
        {
            Context->ActiveReaders++;
            if (Context->ActiveReaders <= 0)
            {
                DbgPrint(__FUNCTION__ ": I/O synchronization state corrupt.\n");

#if DBG
                if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                    DbgBreakPoint();
#endif

                status = STATUS_DRIVER_INTERNAL_ERROR;
            }
            else
            {
                KdPrint2((__FUNCTION__ ": Thread %p acquired lock for reading. Active readers: %i writers: %i\n",
                    KeGetCurrentThread(),
                    Context->ActiveReaders, Context->ActiveWriters));

                status = STATUS_SUCCESS;
            }
        }
    }
    AWEAllocReleaseLock(&lock_handle, LowestAssumedIrql);
    return status;
}

void
AWEAllocReleaseProtection(IN POBJECT_CONTEXT Context,
IN BOOLEAN ForReadOperation,
IN BOOLEAN ForWriteOperation,
IN OUT PKIRQL LowestAssumedIrql)
{
    KLOCK_QUEUE_HANDLE lock_handle = { 0 };
    AWEAllocAcquireLock(&Context->IOLock, &lock_handle, *LowestAssumedIrql);
    if (ForWriteOperation)
    {
        Context->ActiveWriters--;

        KdPrint2((__FUNCTION__ ": Thread %p released lock for writing. Active readers: %i writers: %i\n",
            KeGetCurrentThread(),
            Context->ActiveReaders, Context->ActiveWriters));

        if (Context->ActiveWriters < 0)
        {
            DbgPrint(__FUNCTION__ ": I/O synchronization state corrupt.\n");

#if DBG
            if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                DbgBreakPoint();
#endif
        }
    }
    if (ForReadOperation)
    {
        Context->ActiveReaders--;

        KdPrint2((__FUNCTION__ ": Thread %p released lock for reading. Active readers: %i writers: %i\n",
            KeGetCurrentThread(),
            Context->ActiveReaders, Context->ActiveWriters));

        if (Context->ActiveReaders < 0)
        {
            DbgPrint(__FUNCTION__ ": I/O synchronization state corrupt.\n");

#if DBG
            if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
                DbgBreakPoint();
#endif
        }
    }
    AWEAllocReleaseLock(&lock_handle, LowestAssumedIrql);
}

VOID
AWEAllocExchangeMapPage(IN POBJECT_CONTEXT Context,
IN OUT PPAGE_CONTEXT PageContext,
IN OUT PKIRQL LowestAssumedIrql)
{
    KLOCK_QUEUE_HANDLE lock_handle = { 0 };
    
    PAGE_CONTEXT page_context = *PageContext;

    AWEAllocAcquireLock(&Context->IOLock, &lock_handle, *LowestAssumedIrql);

    *PageContext = Context->LatestPageContext;

    Context->LatestPageContext = page_context;

    AWEAllocReleaseLock(&lock_handle, LowestAssumedIrql);

    PageContext->UsageCount++;
}

NTSTATUS
AWEAllocMapPage(IN POBJECT_CONTEXT Context,
IN LONGLONG NewOffset,
IN OUT PPAGE_CONTEXT CurrentPageContext,
IN OUT PKIRQL LowestAssumedIrql)
{
    LONGLONG page_base = AWEAllocGetPageBaseFromAbsOffset(NewOffset);
    LONGLONG page_base_within_block;
    ULONG size_to_map = ALLOC_PAGE_SIZE;
    PBLOCK_DESCRIPTOR block;

    KdPrint2((__FUNCTION__ ": MapPage request NewOffset=%#I64x BaseAddress=%#I64x.\n",
        NewOffset,
        page_base));
    
    if ((NewOffset >= 0) &&
        (CurrentPageContext->PageBase == page_base) &&
        (CurrentPageContext->Mdl != NULL) &
        (CurrentPageContext->Ptr != NULL))
    {
        KdPrint2((__FUNCTION__ ": MapPage: Region is within already mapped page.\n"));
        return STATUS_SUCCESS;
    }

    AWEAllocExchangeMapPage(Context, CurrentPageContext, LowestAssumedIrql);

    if ((NewOffset >= 0) &&
        (CurrentPageContext->PageBase == page_base) &&
        (CurrentPageContext->Mdl != NULL) &
        (CurrentPageContext->Ptr != NULL))
    {
        KdPrint2((__FUNCTION__ ": MapPage: Region is within previously mapped page.\n"));
        return STATUS_SUCCESS;
    }

    if (CurrentPageContext->Mdl != NULL)
    {
        KdPrint2((__FUNCTION__ ": MapPage: Freeing stored mapped page that we cannot use.\n"));
        IoFreeMdl(CurrentPageContext->Mdl);
    }

    RtlZeroMemory(CurrentPageContext, sizeof(*CurrentPageContext));

    if (NewOffset < 0)
    {
        KdPrint2((__FUNCTION__ ": MapPage: Stored mapped page and returning empty current page.\n"));
        return STATUS_SUCCESS;
    }

    // Find block that contains this page
    for (block = Context->FirstBlock;
        block != NULL &&
        block->Offset > page_base;
        block = block->NextBlock);

    if (block == NULL)
    {
        DbgPrint(__FUNCTION__ ": MapPage: Cannot find block for BaseAddress=%#I64x.\n",
            page_base);

#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        return STATUS_DRIVER_INTERNAL_ERROR;
    }

    page_base_within_block = page_base - block->Offset;

    KdPrint2((__FUNCTION__ ": MapPage found block NewOffset=%#I64x BaseAddress=%#I64x.\n",
        block->Offset, page_base_within_block));

    if (MmGetMdlByteCount(block->Mdl) <= page_base - block->Offset)
    {
        DbgPrint(__FUNCTION__ ": MapPage: Bad sized block BaseAddress=%#I64x.\n",
            page_base);

#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        return STATUS_DRIVER_INTERNAL_ERROR;
    }

    CurrentPageContext->Mdl = IoAllocateMdl(MmGetMdlVirtualAddress(block->Mdl),
        size_to_map, FALSE, FALSE, NULL);

    if (CurrentPageContext->Mdl == NULL)
    {
        DbgPrint(__FUNCTION__ ": IoAllocateMdl() FAILED.\n");

#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        return STATUS_INSUFFICIENT_RESOURCES;
    }

    if ((MmGetMdlByteCount(block->Mdl) - page_base_within_block) <
        size_to_map)
    {
        KdPrint
            ((__FUNCTION__ ": Incomplete page size! Shrinking page size.\n"));
        size_to_map = 0;  // This will map remaining bytes
    }

    IoBuildPartialMdl(block->Mdl, CurrentPageContext->Mdl,
        (PUCHAR)MmGetMdlVirtualAddress(block->Mdl) +
        page_base_within_block, size_to_map);

    CurrentPageContext->Ptr = MmGetSystemAddressForMdlSafe(CurrentPageContext->Mdl,
        HighPagePriority);

    if (CurrentPageContext->Ptr == NULL)
    {
        DbgPrint(__FUNCTION__ ": MmGetSystemAddressForMdlSafe() FAILED.\n");

#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        AWEAllocLogError(AWEAllocDriverObject,
            0,
            0,
            NULL,
            0,
            1000,
            STATUS_INSUFFICIENT_RESOURCES,
            101,
            STATUS_INSUFFICIENT_RESOURCES,
            0,
            0,
            NULL,
            L"MmGetSystemAddressForMdlSafe() failed during "
            L"page mapping.");

        IoFreeMdl(CurrentPageContext->Mdl);
        CurrentPageContext->Mdl = NULL;
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    CurrentPageContext->PageBase = page_base;

    KdPrint2((__FUNCTION__ ": MapPage success BaseAddress=%#I64x.\n",
        page_base));

    return STATUS_SUCCESS;
}

NTSTATUS
AWEAllocControl(IN PDEVICE_OBJECT DeviceObject,
IN PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = io_stack->FileObject->FsContext2;
    NTSTATUS status;

    KdPrint2((__FUNCTION__ ": FileSystemControl request %#x.\n",
        io_stack->Parameters.FileSystemControl.FsControlCode));

    if (context == NULL)
    {
        KdPrint2((__FUNCTION__ ": FileSystemControl request on not initialized device.\n"));

        status = STATUS_INVALID_DEVICE_REQUEST;

        Irp->IoStatus.Status = status;
        Irp->IoStatus.Information = 0;

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return status;
    }

    status = STATUS_SUCCESS;

    if ((io_stack->MajorFunction != IRP_MJ_FILE_SYSTEM_CONTROL) ||
        (io_stack->MinorFunction != IRP_MN_USER_FS_REQUEST) &&
        (io_stack->MajorFunction != IRP_MJ_DEVICE_CONTROL))
    {
        status = STATUS_INVALID_DEVICE_REQUEST;
    }
    else if (METHOD_FROM_CTL_CODE(io_stack->Parameters.FileSystemControl.FsControlCode) !=
        METHOD_BUFFERED)
    {
        status = STATUS_INVALID_DEVICE_REQUEST;
    }
    else if (io_stack->Parameters.FileSystemControl.InputBufferLength <	sizeof(GUID))
    {
        status = STATUS_INVALID_PARAMETER;
    }
    else if (RtlCompareMemory(
        (PGUID)Irp->AssociatedIrp.SystemBuffer,
        &AWEAllocFsCtlGuid, sizeof(GUID)) < sizeof(GUID))
    {
        status = STATUS_INVALID_PARAMETER;
    }

    if (!NT_SUCCESS(status))
    {
        Irp->IoStatus.Status = status;
        Irp->IoStatus.Information = 0;

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return status;
    }

    switch (io_stack->Parameters.FileSystemControl.FsControlCode)
    {
    case FSCTL_AWEALLOC_QUERY_CONTEXT:
        if (io_stack->Parameters.FileSystemControl.OutputBufferLength <
            sizeof(OBJECT_CONTEXT))
        {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }

        RtlCopyMemory(Irp->AssociatedIrp.SystemBuffer,
            context, sizeof(OBJECT_CONTEXT));

        status = STATUS_SUCCESS;
        Irp->IoStatus.Information = sizeof(OBJECT_CONTEXT);

    case IOCTL_DISK_GET_LENGTH_INFO:
        if (io_stack->Parameters.FileSystemControl.OutputBufferLength <
            sizeof(GET_LENGTH_INFORMATION))
        {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }

        ((PGET_LENGTH_INFORMATION)Irp->AssociatedIrp.SystemBuffer)->
            Length.QuadPart = context->VirtualSize;

        status = STATUS_SUCCESS;
        Irp->IoStatus.Information = sizeof(GET_LENGTH_INFORMATION);

    default:
        status = STATUS_INVALID_DEVICE_REQUEST;
    }

    Irp->IoStatus.Status = status;

    if (!NT_SUCCESS(status))
    {
        Irp->IoStatus.Information = 0;
    }

    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return status;
}

NTSTATUS
AWEAllocFlushBuffers(IN PDEVICE_OBJECT DeviceObject,
IN PIRP Irp)
{
    Irp->IoStatus.Information = 0;
    Irp->IoStatus.Status = STATUS_SUCCESS;

    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

NTSTATUS
AWEAllocReadWrite(IN PDEVICE_OBJECT DeviceObject,
IN PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = io_stack->FileObject->FsContext2;
    ULONG length_done = 0;
    NTSTATUS status;
    PUCHAR system_buffer;
    PAGE_CONTEXT current_page_context = { 0 };
    KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

    KdPrint2((__FUNCTION__ ": Read/write request Offset=%#I64x Len=%#x.\n",
        io_stack->Parameters.Read.ByteOffset,
        io_stack->Parameters.Read.Length));

    if ((io_stack->Parameters.Read.ByteOffset.QuadPart < 0) ||
        ((io_stack->Parameters.Read.ByteOffset.QuadPart +
        io_stack->Parameters.Read.Length) < 0))
    {
        KdPrint((__FUNCTION__ ": Read/write attempt on negative offset.\n"));

        Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;
        Irp->IoStatus.Information = 0;

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return STATUS_SUCCESS;
    }

    if (context == NULL)
    {
        KdPrint2((__FUNCTION__ ": Read/write request on not initialized device.\n"));

        Irp->IoStatus.Status = STATUS_INVALID_DEVICE_REQUEST;
        Irp->IoStatus.Information = 0;

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return STATUS_INVALID_DEVICE_REQUEST;
    }

    if (io_stack->Parameters.Read.Length == 0)
    {
        KdPrint2((__FUNCTION__ ": Zero bytes read/write request.\n"));

        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = 0;

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return STATUS_SUCCESS;
    }

    system_buffer =
        MmGetSystemAddressForMdlSafe(Irp->MdlAddress, HighPagePriority);

    if (system_buffer == NULL)
    {
        DbgPrint(__FUNCTION__ ": Failed mapping system buffer.\n");

#if DBG
        if (!KD_REFRESH_DEBUGGER_NOT_PRESENT)
            DbgBreakPoint();
#endif

        Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    KdPrint2((__FUNCTION__ ": System buffer: %p\n", system_buffer));

    status = AWEAllocTryAcquireProtection(context, FALSE, FALSE, &lowest_assumed_irql);
    if (!NT_SUCCESS(status))
    {
        DbgPrint(__FUNCTION__ ": Acquire lock for reading: Page table busy.\n");

        Irp->IoStatus.Status = status;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return status;
    }

    // Read starts beyond EOF?
    if ((io_stack->MajorFunction == IRP_MJ_READ) &&
        (io_stack->Parameters.Read.ByteOffset.QuadPart > context->VirtualSize))
    {
        KdPrint((__FUNCTION__ ": Read request starting past EOF.\n"));

        AWEAllocReleaseProtection(context, TRUE, FALSE,
            &lowest_assumed_irql);

        Irp->IoStatus.Status = STATUS_END_OF_FILE;
        Irp->IoStatus.Information = 0;

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return STATUS_END_OF_FILE;
    }

    if ((io_stack->Parameters.Read.ByteOffset.QuadPart +
        io_stack->Parameters.Read.Length) > context->VirtualSize)
    {
        if (io_stack->MajorFunction == IRP_MJ_WRITE)
        {
            // Write starts or extends beyond EOF. Allocate additional
            // memory increase virtual file size.

            IO_STATUS_BLOCK io_status;
            LARGE_INTEGER new_size = { 0 };

            status = AWEAllocTryAcquireProtection(context, TRUE, TRUE,
                &lowest_assumed_irql);

            if (!NT_SUCCESS(status))
            {
                DbgPrint(__FUNCTION__ ": Acquire lock for writing: Page table busy.\n");

                AWEAllocReleaseProtection(context, TRUE, FALSE,
                    &lowest_assumed_irql);

                Irp->IoStatus.Status = status;
                Irp->IoStatus.Information = 0;
                IoCompleteRequest(Irp, IO_NO_INCREMENT);
                return status;
            }

            new_size.QuadPart =
                io_stack->Parameters.Read.ByteOffset.QuadPart +
                io_stack->Parameters.Read.Length;

            status = AWEAllocSetSize(context,
                &io_status,
                &new_size);

            if (!NT_SUCCESS(status))
            {
                DbgPrint(__FUNCTION__ ": Error growing in-memory file.\n");

                AWEAllocReleaseProtection(context, TRUE, TRUE,
                    &lowest_assumed_irql);

                Irp->IoStatus.Status = status;
                Irp->IoStatus.Information = 0;
                IoCompleteRequest(Irp, IO_NO_INCREMENT);
                return status;
            }

            AWEAllocReleaseProtection(context, FALSE, TRUE,
                &lowest_assumed_irql);
        }

        if ((io_stack->Parameters.Read.ByteOffset.QuadPart +
            io_stack->Parameters.Read.Length) > context->VirtualSize)
        {
            // Read/write starts within file, but extends beyond current EOF.
            // Adjust length to actual remaining range until EOF.

            io_stack->Parameters.Read.Length = (ULONG)
                (context->VirtualSize -
                io_stack->Parameters.Read.ByteOffset.QuadPart);

            KdPrint2((__FUNCTION__ ": Read request towards EOF. Len set to %x\n",
                io_stack->Parameters.Read.Length));
        }
    }

    for (;;)
    {
        LONGLONG abs_offset_this_iter =
            io_stack->Parameters.Read.ByteOffset.QuadPart + length_done;
        ULONG page_offset_this_iter =
            AWEAllocGetPageOffsetFromAbsOffset(abs_offset_this_iter);
        ULONG bytes_this_iter = io_stack->Parameters.Read.Length - length_done;

        if (length_done >= io_stack->Parameters.Read.Length)
        {
            KdPrint2((__FUNCTION__ ": Nothing left to do.\n"));

            AWEAllocMapPage(context, INVALID_OFFSET, &current_page_context,
                &lowest_assumed_irql);

            AWEAllocReleaseProtection(context, TRUE, FALSE,
                &lowest_assumed_irql);

            Irp->IoStatus.Status = STATUS_SUCCESS;
            Irp->IoStatus.Information = length_done;

            IoCompleteRequest(Irp, IO_NO_INCREMENT);

            return STATUS_SUCCESS;
        }

        if (((ULONGLONG)page_offset_this_iter + bytes_this_iter) > ALLOC_PAGE_SIZE)
        {
            bytes_this_iter = ALLOC_PAGE_SIZE - page_offset_this_iter;
        }

        status = AWEAllocMapPage(context, abs_offset_this_iter,
            &current_page_context, &lowest_assumed_irql);

        if (!NT_SUCCESS(status))
        {
            DbgPrint(__FUNCTION__ ": Failed mapping current image page.\n");

            AWEAllocReleaseProtection(context, TRUE, FALSE,
                &lowest_assumed_irql);

            Irp->IoStatus.Status = status;
            Irp->IoStatus.Information = 0;

            IoCompleteRequest(Irp, IO_NO_INCREMENT);

            return status;
        }

        KdPrint2((__FUNCTION__ ": Current image page mdl ptr=%p system ptr=%p.\n",
            current_page_context.Mdl,
            current_page_context.Ptr));

        __analysis_assume(current_page_context.Ptr != NULL);

        switch (io_stack->MajorFunction)
        {
        case IRP_MJ_READ:
        {
            KdPrint2((__FUNCTION__ ": Copying memory image -> I/O buffer.\n"));

            RtlCopyMemory(system_buffer + length_done,
                current_page_context.Ptr + page_offset_this_iter,
                bytes_this_iter);

            break;
        }

        case IRP_MJ_WRITE:
        {
            KdPrint2((__FUNCTION__ ": Copying memory image <- I/O buffer.\n"));
            
            RtlCopyMemory(current_page_context.Ptr + page_offset_this_iter,
                system_buffer + length_done,
                bytes_this_iter);

            break;
        }
        }

        io_stack->FileObject->CurrentByteOffset.QuadPart += bytes_this_iter;

        KdPrint2((__FUNCTION__ ": Copy done.\n"));

        length_done += bytes_this_iter;

    }

}

VOID
AWEAllocLogError(IN PVOID Object,
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
        DbgPrint(__FUNCTION__ ": Warning: Too large error log packet.\n");
        return;
    }

    error_log_packet =
        (PIO_ERROR_LOG_PACKET)IoAllocateErrorLogEntry(Object,
        (UCHAR)packet_size);

    if (error_log_packet == NULL)
    {
        DbgPrint
            (__FUNCTION__ ": Warning: IoAllocateErrorLogEntry() returned NULL.\n");

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

LONGLONG
FORCEINLINE
AWEAllocGetTotalSize(IN POBJECT_CONTEXT Context)
{
    if (Context == NULL || Context->FirstBlock == NULL)
    {
        return 0;
    }

    return Context->FirstBlock->Offset +
        AWEAllocGetPageBaseFromAbsOffset(MmGetMdlByteCount(Context->FirstBlock->Mdl));
}

BOOLEAN
AWEAllocAddBlock(IN POBJECT_CONTEXT Context,
IN PBLOCK_DESCRIPTOR Block)
{
    ULONG block_size;

    if (Block->Mdl == NULL)
        return FALSE;

    block_size = AWEAllocGetPageBaseFromAbsOffset(MmGetMdlByteCount(Block->Mdl));
    if (block_size == 0)
    {
        KdPrint((__FUNCTION__ ": Got %u bytes which is too small for page size.\n",
            MmGetMdlByteCount(Block->Mdl)));

        MmFreePagesFromMdl(Block->Mdl);
        ExFreePool(Block->Mdl);
        Block->Mdl = NULL;
        return FALSE;
    }

    Block->Offset = AWEAllocGetTotalSize(Context);
    Block->NextBlock = Context->FirstBlock;
    Context->FirstBlock = Block;
    return TRUE;
}

NTSTATUS
AWEAllocSetSize(IN POBJECT_CONTEXT Context,
IN OUT PIO_STATUS_BLOCK IoStatus,
IN PLARGE_INTEGER EndOfFile)
{
    KdPrint2((__FUNCTION__ ": Setting size to %u KB.\n",
        (ULONG)(EndOfFile->QuadPart >> 10)));

    if (AWEAllocGetTotalSize(Context) >= EndOfFile->QuadPart)
    {
        if (AWEAllocGetTotalSize(Context) - EndOfFile->QuadPart >= ALLOC_PAGE_SIZE)
        {
            if (Context->LatestPageContext.Mdl != NULL)
            {
                IoFreeMdl(Context->LatestPageContext.Mdl);
                Context->LatestPageContext.Mdl = NULL;
            }

            KdPrint((__FUNCTION__ ": Reducing size from 0x%I64X to 0x%I64X\n",
                AWEAllocGetTotalSize(Context), EndOfFile->QuadPart));

            while (Context->FirstBlock != NULL &&
                Context->FirstBlock->Offset >= EndOfFile->QuadPart)
            {
                PBLOCK_DESCRIPTOR free_block = Context->FirstBlock;

                Context->FirstBlock = free_block->NextBlock;

                KdPrint((__FUNCTION__ ": Freeing block=%p mdl=%p.\n",
                    free_block, free_block->Mdl));

                if (free_block->Mdl != NULL)
                {
                    MmFreePagesFromMdl(free_block->Mdl);
                    ExFreePool(free_block->Mdl);
                }
                ExFreePoolWithTag(free_block, POOL_TAG);
            }
        }

        Context->VirtualSize = EndOfFile->QuadPart;
        IoStatus->Status = STATUS_SUCCESS;
        IoStatus->Information = 0;
        return STATUS_SUCCESS;
    }

    for (;;)
    {
        PBLOCK_DESCRIPTOR block;
        SIZE_T bytes_to_allocate;

        if (AWEAllocGetTotalSize(Context) >= EndOfFile->QuadPart)
        {
            Context->VirtualSize = EndOfFile->QuadPart;
            IoStatus->Status = STATUS_SUCCESS;
            IoStatus->Information = 0;
            return STATUS_SUCCESS;
        }

        // If running out of memory, do not attempt to
        // allocate
        if (KeReadStateEvent(HighMemoryCondition) == 0)
        {
            DbgPrint(__FUNCTION__ ": Risk of running out of memory. Refusing to allocate more at this point.\n");

            IoStatus->Status = STATUS_NO_MEMORY;
            IoStatus->Information = 0;
            return STATUS_NO_MEMORY;
        }

        block = ExAllocatePoolWithTag(NonPagedPool,
            sizeof(BLOCK_DESCRIPTOR), POOL_TAG);

        if (block == NULL)
        {
            DbgPrint(__FUNCTION__ ": Out of pool memory.\n");

            IoStatus->Status = STATUS_NO_MEMORY;
            IoStatus->Information = 0;
            return STATUS_NO_MEMORY;
        }

        RtlZeroMemory(block, sizeof(BLOCK_DESCRIPTOR));

        if ((EndOfFile->QuadPart - AWEAllocGetTotalSize(Context)) > MAX_BLOCK_SIZE)
        {
            bytes_to_allocate = MAX_BLOCK_SIZE;
        }
        else
        {
            bytes_to_allocate = (SIZE_T)
                AWEAllocGetRequiredPagesForSize(EndOfFile->QuadPart -
                    AWEAllocGetTotalSize(Context));
        }

        KdPrint((__FUNCTION__ ": Allocating %u MB.\n",
            (ULONG)(bytes_to_allocate >> 20)));

#ifndef _WIN64

        // On 32-bit, first try to allocate as high as possible
        KdPrint((__FUNCTION__ ": Allocating above 8 GB.\n"));

        block->Mdl = MmAllocatePagesForMdl(physical_address_8GB,
            physical_address_max64,
            physical_address_zero,
            bytes_to_allocate);

        if (AWEAllocAddBlock(Context, block))
            continue;

        KdPrint((__FUNCTION__ ": Not enough memory available above 8 GB.\n"
            "AWEAlloc: Allocating above 6 GB.\n"));

        AWEAllocLogError(AWEAllocDriverObject,
            0,
            0,
            NULL,
            0,
            1000,
            AWEALLOC_STATUS_ALLOCATION_LOW_MEMORY,
            101,
            STATUS_INSUFFICIENT_RESOURCES,
            0,
            0,
            NULL,
            L"Error allocating above 8 GB.");

        block->Mdl = MmAllocatePagesForMdl(physical_address_6GB,
            physical_address_max64,
            physical_address_zero,
            bytes_to_allocate);

        if (AWEAllocAddBlock(Context, block))
            continue;

        KdPrint((__FUNCTION__ ": Not enough memory available above 6 GB.\n"
            "AWEAlloc: Allocating above 5 GB.\n"));

        AWEAllocLogError(AWEAllocDriverObject,
            0,
            0,
            NULL,
            0,
            1000,
            AWEALLOC_STATUS_ALLOCATION_LOW_MEMORY,
            101,
            STATUS_INSUFFICIENT_RESOURCES,
            0,
            0,
            NULL,
            L"Error allocating above 6 GB.");

        block->Mdl = MmAllocatePagesForMdl(physical_address_5GB,
            physical_address_max64,
            physical_address_zero,
            bytes_to_allocate);

        if (AWEAllocAddBlock(Context, block))
            continue;

        KdPrint((__FUNCTION__ ": Not enough memory available above 5 GB.\n"
            "AWEAlloc: Allocating above 4 GB.\n"));

        AWEAllocLogError(AWEAllocDriverObject,
            0,
            0,
            NULL,
            0,
            1000,
            AWEALLOC_STATUS_ALLOCATION_LOW_MEMORY,
            101,
            STATUS_INSUFFICIENT_RESOURCES,
            0,
            0,
            NULL,
            L"Error allocating above 5 GB.");

        block->Mdl = MmAllocatePagesForMdl(physical_address_4GB,
            physical_address_max64,
            physical_address_zero,
            bytes_to_allocate);

        if (AWEAllocAddBlock(Context, block))
            continue;

        KdPrint((__FUNCTION__ ": Not enough memory available above 4 GB.\n"
            "AWEAlloc: Allocating at any available location.\n"));

        AWEAllocLogError(AWEAllocDriverObject,
            0,
            0,
            NULL,
            0,
            1000,
            AWEALLOC_STATUS_ALLOCATION_LOW_MEMORY,
            101,
            STATUS_INSUFFICIENT_RESOURCES,
            0,
            0,
            NULL,
            L"Error allocating above 4 GB.");

#endif // !_WIN64

        block->Mdl = MmAllocatePagesForMdl(physical_address_zero,
            physical_address_max64,
            physical_address_zero,
            bytes_to_allocate);

        if (AWEAllocAddBlock(Context, block))
            continue;

        DbgPrint(__FUNCTION__ ": Failed to allocate memory to increase file size.\n");

        AWEAllocLogError(AWEAllocDriverObject,
            0,
            0,
            NULL,
            0,
            1000,
            STATUS_NO_MEMORY,
            101,
            STATUS_NO_MEMORY,
            0,
            0,
            NULL,
            L"Error allocating physical memory.");

        ExFreePoolWithTag(block, POOL_TAG);

        IoStatus->Status = STATUS_NO_MEMORY;
        IoStatus->Information = 0;
        return STATUS_NO_MEMORY;
    }
}

NTSTATUS
AWEAllocQueryInformation(IN PDEVICE_OBJECT DeviceObject,
IN PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = io_stack->FileObject->FsContext2;

    KdPrint2((__FUNCTION__ ": QueryFileInformation: %u.\n",
        io_stack->Parameters.QueryFile.FileInformationClass));

    RtlZeroMemory(Irp->AssociatedIrp.SystemBuffer,
        io_stack->Parameters.QueryFile.Length);

    switch (io_stack->Parameters.QueryFile.FileInformationClass)
    {
    case FileAlignmentInformation:
    {
        PFILE_ALIGNMENT_INFORMATION alignment_info =
            (PFILE_ALIGNMENT_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.QueryFile.Length <
            sizeof(FILE_ALIGNMENT_INFORMATION))
        {
            Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;
            Irp->IoStatus.Information = 0;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_INVALID_PARAMETER;
        }

        alignment_info->AlignmentRequirement = FILE_BYTE_ALIGNMENT;

        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = sizeof(FILE_ALIGNMENT_INFORMATION);
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_SUCCESS;
    }

    case FileAttributeTagInformation:
    case FileBasicInformation:
    case FileInternalInformation:
        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = io_stack->Parameters.QueryFile.Length;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_SUCCESS;

    case FileNetworkOpenInformation:
    {
        PFILE_NETWORK_OPEN_INFORMATION network_open_info =
            (PFILE_NETWORK_OPEN_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.QueryFile.Length <
            sizeof(FILE_NETWORK_OPEN_INFORMATION))
        {
            Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;
            Irp->IoStatus.Information = 0;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_SUCCESS;
        }

        network_open_info->AllocationSize.QuadPart = AWEAllocGetTotalSize(context);
        network_open_info->EndOfFile.QuadPart = context->VirtualSize;

        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = sizeof(FILE_NETWORK_OPEN_INFORMATION);
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_SUCCESS;
    }

    case FileStandardInformation:
    {
        PFILE_STANDARD_INFORMATION standard_info =
            (PFILE_STANDARD_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.QueryFile.Length <
            sizeof(FILE_STANDARD_INFORMATION))
        {
            Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;
            Irp->IoStatus.Information = 0;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_INVALID_PARAMETER;
        }

        standard_info->AllocationSize.QuadPart = AWEAllocGetTotalSize(context);
        standard_info->EndOfFile.QuadPart = context->VirtualSize;
        standard_info->DeletePending = TRUE;

        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = sizeof(FILE_STANDARD_INFORMATION);
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_SUCCESS;
    }

    case FilePositionInformation:
    {
        PFILE_POSITION_INFORMATION position_info =
            (PFILE_POSITION_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.QueryFile.Length <
            sizeof(FILE_POSITION_INFORMATION))
        {
            Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;
            Irp->IoStatus.Information = 0;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_INVALID_PARAMETER;
        }

        position_info->CurrentByteOffset =
            io_stack->FileObject->CurrentByteOffset;

        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = sizeof(FILE_POSITION_INFORMATION);
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_SUCCESS;
    }

    default:
        KdPrint((__FUNCTION__ ": Unsupported QueryFile.FileInformationClass: %u\n",
            io_stack->Parameters.QueryFile.FileInformationClass));

        Irp->IoStatus.Status = STATUS_INVALID_DEVICE_REQUEST;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INVALID_DEVICE_REQUEST;
    }
}

NTSTATUS
AWEAllocSetInformation(IN PDEVICE_OBJECT DeviceObject,
IN PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = io_stack->FileObject->FsContext2;
    KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

    KdPrint2((__FUNCTION__ ": SetFileInformation: %u.\n",
        io_stack->Parameters.SetFile.FileInformationClass));

    switch (io_stack->Parameters.SetFile.FileInformationClass)
    {
    case FileAllocationInformation:
    case FileEndOfFileInformation:
    {
        NTSTATUS status;
        PFILE_END_OF_FILE_INFORMATION feof_info =
            (PFILE_END_OF_FILE_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.SetFile.Length <
            sizeof(FILE_END_OF_FILE_INFORMATION))
        {
            Irp->IoStatus.Status = STATUS_BUFFER_TOO_SMALL;
            Irp->IoStatus.Information = 0;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_BUFFER_TOO_SMALL;
        }

        status = AWEAllocTryAcquireProtection(context, TRUE, FALSE, &lowest_assumed_irql);

        if (!NT_SUCCESS(status))
        {
            DbgPrint(__FUNCTION__ ": Acquire lock for writing: Page table busy.\n");
            Irp->IoStatus.Status = status;
            Irp->IoStatus.Information = 0;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return status;
        }

        status = AWEAllocSetSize(context,
            &Irp->IoStatus,
            &feof_info->EndOfFile);

        AWEAllocReleaseProtection(context, FALSE, TRUE, &lowest_assumed_irql);

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return status;
    }

    case FilePositionInformation:
    {
        PFILE_POSITION_INFORMATION position_info =
            (PFILE_POSITION_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.SetFile.Length <
            sizeof(FILE_POSITION_INFORMATION))
        {
            Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;
            Irp->IoStatus.Information = 0;
            IoCompleteRequest(Irp, IO_NO_INCREMENT);
            return STATUS_INVALID_PARAMETER;
        }

        io_stack->FileObject->CurrentByteOffset =
            position_info->CurrentByteOffset;

        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_SUCCESS;
    }

    case FileBasicInformation:
    case FileDispositionInformation:
    case FileValidDataLengthInformation:
        Irp->IoStatus.Status = STATUS_SUCCESS;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_SUCCESS;

    default:
        KdPrint((__FUNCTION__ ": Unsupported SetFile.FileInformationClass: %u\n",
            io_stack->Parameters.SetFile.FileInformationClass));

        Irp->IoStatus.Status = STATUS_INVALID_DEVICE_REQUEST;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INVALID_DEVICE_REQUEST;
    }
}

#pragma code_seg("PAGE")

NTSTATUS
AWEAllocLoadImageFile(IN POBJECT_CONTEXT Context,
IN OUT PIO_STATUS_BLOCK IoStatus,
IN PUNICODE_STRING FileName,
IN BOOLEAN OwnsReadLock)
{
    OBJECT_ATTRIBUTES object_attributes = { 0 };
    NTSTATUS status;
    HANDLE file_handle = NULL;
    FILE_STANDARD_INFORMATION file_standard = { 0 };
    LARGE_INTEGER offset = { 0 };
    PAGE_CONTEXT current_page_context = { 0 };
    KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

    PAGED_CODE();

    InitializeObjectAttributes(&object_attributes,
        FileName,
        OBJ_CASE_INSENSITIVE |
        OBJ_FORCE_ACCESS_CHECK |
        OBJ_OPENIF |
        OBJ_KERNEL_HANDLE,
        NULL,
        NULL);

    status = ZwOpenFile(&file_handle,
        SYNCHRONIZE | GENERIC_READ,
        &object_attributes,
        IoStatus,
        FILE_SHARE_READ |
        FILE_SHARE_DELETE,
        FILE_NON_DIRECTORY_FILE |
        FILE_SEQUENTIAL_ONLY |
        FILE_NO_INTERMEDIATE_BUFFERING |
        FILE_SYNCHRONOUS_IO_NONALERT);

    if (!NT_SUCCESS(status))
    {
        KdPrint((__FUNCTION__ ": ZwOpenFile failed: %#x\n", status));
        return status;
    }

    status = ZwQueryInformationFile(file_handle,
        IoStatus,
        &file_standard,
        sizeof(FILE_STANDARD_INFORMATION),
        FileStandardInformation);

    if (!NT_SUCCESS(status))
    {
        KdPrint((__FUNCTION__ ": ZwQueryInformationFile failed: %#x\n", status));
        ZwClose(file_handle);
        return status;
    }

    status = AWEAllocSetSize(Context, IoStatus, &file_standard.EndOfFile);

    if (!NT_SUCCESS(status))
    {
        KdPrint((__FUNCTION__ ": AWEAllocSetSize failed: %#x\n", status));
        ZwClose(file_handle);
        return status;
    }

    for (offset.QuadPart = 0;
        offset.QuadPart < file_standard.EndOfFile.QuadPart;
        offset.QuadPart += ALLOC_PAGE_SIZE)
    {
        status = AWEAllocMapPage(Context, offset.QuadPart,
            &current_page_context, &lowest_assumed_irql);

        if (!NT_SUCCESS(status))
        {
            KdPrint((__FUNCTION__ ": Failed mapping current image page.\n"));

            IoStatus->Status = status;
            IoStatus->Information = 0;

            break;
        }

        __analysis_assume(current_page_context.Ptr != NULL);

        KdPrint2((__FUNCTION__ ": Current image page mdl=%p ptr=%p.\n",
            current_page_context.Mdl,
            current_page_context.Ptr));

        status = ZwReadFile(file_handle,
            NULL,
            NULL,
            NULL,
            IoStatus,
            current_page_context.Ptr,
            ALLOC_PAGE_SIZE,
            &offset,
            NULL);

        if (!NT_SUCCESS(status))
        {
            KdPrint((__FUNCTION__ ": ZwReadFile failed on image file.\n"));
            break;
        }
    }

    ZwClose(file_handle);

    AWEAllocMapPage(Context, INVALID_OFFSET, &current_page_context, &lowest_assumed_irql);

    IoStatus->Information = 0;

    return status;
}

NTSTATUS
AWEAllocCreate(IN PDEVICE_OBJECT DeviceObject,
IN PIRP Irp)
{
    PFILE_OBJECT file_object = IoGetCurrentIrpStackLocation(Irp)->FileObject;
    POBJECT_CONTEXT context;

    KdPrint((__FUNCTION__ ": Create.\n"));

    PAGED_CODE();

    if (file_object == NULL)
    {
        Irp->IoStatus.Status = STATUS_INVALID_DEVICE_REQUEST;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INVALID_DEVICE_REQUEST;
    }

    file_object->FsContext2 =
        context =
        ExAllocatePoolWithTag(NonPagedPool, sizeof(OBJECT_CONTEXT), POOL_TAG);

    if (context == NULL)
    {
        KdPrint((__FUNCTION__ ": Pool allocation failed.\n"));

        Irp->IoStatus.Status = STATUS_INSUFFICIENT_RESOURCES;
        Irp->IoStatus.Information = 0;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    RtlZeroMemory(context, sizeof(OBJECT_CONTEXT));

    file_object->Flags |= FO_DELETE_ON_CLOSE | FO_TEMPORARY_FILE;

    file_object->ReadAccess = TRUE;
    file_object->WriteAccess = TRUE;
    file_object->DeleteAccess = TRUE;
    file_object->SharedRead = TRUE;
    file_object->SharedWrite = FALSE;
    file_object->SharedDelete = FALSE;

    KeInitializeSpinLock(&context->IOLock);

    MmResetDriverPaging((PVOID)(ULONG_PTR)DriverEntry);

    if (file_object->FileName.Length != 0)
    {
        NTSTATUS status;

        KdPrint((__FUNCTION__ ": Image file requested: '%wZ'.\n",
            &file_object->FileName));

        status = AWEAllocLoadImageFile(context, &Irp->IoStatus,
            &file_object->FileName, FALSE);

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        KdPrint((__FUNCTION__ ": Image file status: %#x.\n", status));

        if (!NT_SUCCESS(status))
        {
            AWEAllocLogError(AWEAllocDriverObject,
                0,
                0,
                NULL,
                0,
                4000,
                status,
                401,
                status,
                0,
                0,
                NULL,
                L"Image file failed.");
        }

        return status;
    }

    Irp->IoStatus.Status = STATUS_SUCCESS;
    Irp->IoStatus.Information = 0;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

NTSTATUS
AWEAllocClose(IN PDEVICE_OBJECT DeviceObject,
IN PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = io_stack->FileObject->FsContext2;

    KdPrint((__FUNCTION__ ": Close.\n"));

    PAGED_CODE();

    if (context != NULL)
    {
        LARGE_INTEGER zero = { 0 };

        AWEAllocSetSize(context, &Irp->IoStatus, &zero);

        ExFreePoolWithTag(context, POOL_TAG);
    }

    Irp->IoStatus.Status = STATUS_SUCCESS;
    Irp->IoStatus.Information = 0;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

VOID
AWEAllocUnload(IN PDRIVER_OBJECT DriverObject)
{
    PDEVICE_OBJECT device_object = DriverObject->DeviceObject;
    UNICODE_STRING sym_link;

    KdPrint((__FUNCTION__ ": Unload.\n"));

    PAGED_CODE();

    RtlInitUnicodeString(&sym_link, AWEALLOC_SYMLINK_NAME);
    IoDeleteSymbolicLink(&sym_link);

    while (device_object != NULL)
    {
        PDEVICE_OBJECT next_device = device_object->NextDevice;
        IoDeleteDevice(device_object);
        device_object = next_device;
    }

    ObDereferenceObject(HighMemoryCondition);
}
