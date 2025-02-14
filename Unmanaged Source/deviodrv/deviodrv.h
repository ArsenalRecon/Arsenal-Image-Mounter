/*

  General License for Open Source projects published by
  Olof Lagerkvist - LTR Data.

    Copyright (c) Olof Lagerkvist, LTR Data
    http://www.ltr-data.se
    olof@ltr-data.se

  The above copyright notice shall be included in all copies or
  substantial portions of the Software.

    Permission is hereby granted, free of charge, to any person
    obtaining a copy of this software and associated documentation
    files (the "Software"), to deal in the Software without
    restriction, including without limitation the rights to use,
    copy, modify, merge, publish, distribute, sublicense, and/or
    sell copies of the Software, and to permit persons to whom the
    Software is furnished to do so, subject to the following
    conditions:

  As a discretionary option, the above permission notice may be
  included in copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
    OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
    HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
    WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
    FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
    OTHER DEALINGS IN THE SOFTWARE.

    */

#pragma once

#include <ntkmapi.h>

#include <imdproxy.h>

typedef struct _OBJECT_CONTEXT
{
    LIST_ENTRY ListEntry;

    UNICODE_STRING Name;

    LONG RefCount;

    LARGE_INTEGER FileSize;

    ULONG AlignmentRequirement;

    ULONGLONG ServiceFlags;

    PFILE_OBJECT Server;

    PFILE_OBJECT Client;

    KSPIN_LOCK IrpListLock;

    LIST_ENTRY ServerMemoryIrpList;

    LIST_ENTRY ServerRequestIrpList;

    LIST_ENTRY ClientReceivedIrpList;

    LIST_ENTRY ClientSentIrpList;

} OBJECT_CONTEXT, *POBJECT_CONTEXT;

__forceinline
POBJECT_CONTEXT DevIoDrvGetContext(PIO_STACK_LOCATION IoStack)
{
    return IoStack->FileObject != NULL
        ? (POBJECT_CONTEXT)IoStack->FileObject->FsContext2
        : NULL;
}

typedef struct _IRP_QUEUE_ITEM
{
    LIST_ENTRY ListEntry;

    PIRP Irp;

    struct _IRP_QUEUE_ITEM *MemoryIrp;

    PIMDPROXY_DEVIODRV_BUFFER_HEADER MappedBuffer;

    ULONG MappedBufferSize;

} IRP_QUEUE_ITEM, *PIRP_QUEUE_ITEM;

#ifdef _NTDDK_

#define POOL_TAG 'oIvD'

IO_COMPLETION_ROUTINE
DevIoDrvIoCtlCallCompletion;

DRIVER_UNLOAD
DevIoDrvUnload;

EXTERN_C DRIVER_INITIALIZE
DriverEntry;

__drv_dispatchType(IRP_MJ_CREATE)
DRIVER_DISPATCH DevIoDrvDispatchCreate;

__drv_dispatchType(IRP_MJ_CREATE_NAMED_PIPE)
DRIVER_DISPATCH DevIoDrvDispatchCreateNamedPipe;

__drv_dispatchType(IRP_MJ_CLOSE)
DRIVER_DISPATCH DevIoDrvDispatchClose;

__drv_dispatchType(IRP_MJ_CLEANUP)
DRIVER_DISPATCH DevIoDrvDispatchCleanup;

__drv_dispatchType(IRP_MJ_QUERY_INFORMATION)
DRIVER_DISPATCH DevIoDrvDispatchQueryInformation;

__drv_dispatchType(IRP_MJ_SET_INFORMATION)
DRIVER_DISPATCH DevIoDrvDispatchSetInformation;

__drv_dispatchType(IRP_MJ_FLUSH_BUFFERS)
DRIVER_DISPATCH DevIoDrvDispatchFlushBuffers;

__drv_dispatchType(IRP_MJ_READ)
__drv_dispatchType(IRP_MJ_WRITE)
DRIVER_DISPATCH DevIoDrvDispatchReadWrite;

__drv_dispatchType(IRP_MJ_DEVICE_CONTROL)
__drv_dispatchType(IRP_MJ_FILE_SYSTEM_CONTROL)
DRIVER_DISPATCH DevIoDrvDispatchControl;

DRIVER_CANCEL DevIoDrvServerIrpCancelRoutine;

NTSTATUS
DevIoDrvOpenFileTableEntry(PFILE_OBJECT FileObject, ULONG DesiredAccess);

NTSTATUS
DevIoDrvCreateFileTableEntry(PFILE_OBJECT FileObject);

NTSTATUS
DevIoDrvCloseFileTableEntry(PFILE_OBJECT FileObject);

NTSTATUS
DevIoDrvCancelAll(PFILE_OBJECT FileObject,
    PKIRQL LowestAssumedIrql);

void
DevIoDrvSendClientRequestToServer(POBJECT_CONTEXT File,
    PIRP_QUEUE_ITEM ClientItem,
    PIRP_QUEUE_ITEM ServerItem,
    NTSTATUS *ClientStatus,
    NTSTATUS *ServerStatus,
    PKIRQL LowestAssumedIrql);

NTSTATUS
DevIoDrvDispatchServerLockMemory(PIRP Irp,
    PKIRQL LowestAssumedIrql);

NTSTATUS
DevIoDrvDispatchServerIORequest(PIRP Irp,
    PKIRQL LowestAssumedIrql);

NTSTATUS
DevIoDrvDispatchClientRequest(PIRP Irp,
    PKIRQL LowestAssumedIrql);

void
DevIoDrvCompleteIrpQueueItem(PIRP_QUEUE_ITEM Item,
    NTSTATUS Status,
    ULONG_PTR Length,
    PKIRQL LowestAssumedIrql);

void
DevIoDrvCompleteIrp(PIRP Irp,
    NTSTATUS Status,
    ULONG_PTR Length);

NTSTATUS
DevIoDrvReserveMemoryIrp(PIRP RequestIrp,
    PIRP_QUEUE_ITEM *MemoryIrp,
    PIMDPROXY_DEVIODRV_BUFFER_HEADER *MemoryBuffer,
    PULONG BufferSize,
    PKIRQL LowestAssumedIrql);

void
DevIoDrvQueueMemoryIrpUnsafe(PIRP_QUEUE_ITEM MemoryIrp,
    POBJECT_CONTEXT Context);

void
DevIoDrvQueueMemoryIrp(PIRP_QUEUE_ITEM MemoryIrp,
    POBJECT_CONTEXT Context,
    PKIRQL LowestAssumedIrql);

void
DevIoDrvInitializeSpinLock(PKSPIN_LOCK Lock);

extern LIST_ENTRY FileTable;

typedef struct _REFERENCED_OBJECT
{
    LIST_ENTRY list_entry;
    PFILE_OBJECT file_object;
} REFERENCED_OBJECT, * PREFERENCED_OBJECT;

//
// ReferencedObjects list lock
//
extern KSPIN_LOCK ReferencedObjectsListLock;

//
// List of objects referenced using
// IOCTL_DEVIODRV_REFERENCE_HANDLE
//
extern LIST_ENTRY ReferencedObjects;

#endif
