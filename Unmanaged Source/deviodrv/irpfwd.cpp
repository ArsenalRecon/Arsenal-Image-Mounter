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

#include <ntifs.h>
#include <wdm.h>
#include <ntdddisk.h>

#include <imdproxy.h>
#include <common.h>
#include <ntkmapi.h>

#include "deviodrv.h"

NTSTATUS
DevIoDrvCancelAll(PFILE_OBJECT FileObject,
    PKIRQL LowestAssumedIrql)
{
    POBJECT_CONTEXT context = (POBJECT_CONTEXT)FileObject->FsContext2;
    KLOCK_QUEUE_HANDLE lock_handle;

    KdPrint(("Request to cancel all pending IRPs for FileObject=%p.\n",
        FileObject));

    context->Server = NULL;

    ImScsiAcquireLock(&context->IrpListLock, &lock_handle, *LowestAssumedIrql);

    KIRQL raised_irql = DISPATCH_LEVEL;

    for (;;)
    {
        KIRQL cancel_irql;
        IoAcquireCancelSpinLock(&cancel_irql);

        PLIST_ENTRY entry = RemoveHeadList(&context->ServerRequestIrpList);

        if (entry == &context->ServerRequestIrpList)
        {
            entry = RemoveHeadList(&context->ServerMemoryIrpList);

            if (entry == &context->ServerMemoryIrpList)
            {
                IoReleaseCancelSpinLock(cancel_irql);
                break;
            }
        }

        PIRP_QUEUE_ITEM item = CONTAINING_RECORD(entry, IRP_QUEUE_ITEM, ListEntry);

        IoSetCancelRoutine(item->Irp, NULL);

        IoReleaseCancelSpinLock(cancel_irql);

        if (item->MemoryIrp != NULL)
        {
            item->MappedBuffer = NULL;

            for (PLIST_ENTRY mem_entry = context->ServerMemoryIrpList.Flink;
                mem_entry != &context->ServerMemoryIrpList;
                mem_entry = mem_entry->Flink)
            {
                PIRP_QUEUE_ITEM mem_item = CONTAINING_RECORD(mem_entry, IRP_QUEUE_ITEM, ListEntry);

                if (mem_item == item->MemoryIrp)
                {
                    item->MappedBuffer = mem_item->MappedBuffer;
                    item->MappedBufferSize = mem_item->MappedBufferSize;
                    break;
                }
            }

            item->MemoryIrp = NULL;
        }

        //KdPrint(("Closing server connection '%wZ' IRP=%p\n", &context->Name, item->Irp));

        if (item->MappedBuffer == NULL)
        {
            DevIoDrvCompleteIrpQueueItem(item, STATUS_DEVICE_DOES_NOT_EXIST, 0, &raised_irql);
        }
        else
        {
            item->MappedBuffer->request_code = IMDPROXY_REQ_CLOSE;
            item->MappedBuffer->io_tag = 0;
            PIMDPROXY_CLOSE_REQ header = (PIMDPROXY_CLOSE_REQ)(item->MappedBuffer + 1);
            header->request_code = IMDPROXY_REQ_CLOSE;
            DevIoDrvCompleteIrpQueueItem(item, STATUS_SUCCESS, IMDPROXY_HEADER_SIZE, &raised_irql);
        }
    }

    while (!IsListEmpty(&context->ClientReceivedIrpList))
    {
        PLIST_ENTRY entry = context->ClientReceivedIrpList.Flink;
        PIRP_QUEUE_ITEM item = CONTAINING_RECORD(entry, IRP_QUEUE_ITEM, ListEntry);

        KdPrint(("Canceling received client IRP=%p\n", item->Irp));

        RemoveEntryList(entry);

        DevIoDrvCompleteIrpQueueItem(item, STATUS_DEVICE_DOES_NOT_EXIST, 0, &raised_irql);
    }

    while (!IsListEmpty(&context->ClientSentIrpList))
    {
        PLIST_ENTRY entry = context->ClientSentIrpList.Flink;
        PIRP_QUEUE_ITEM item = CONTAINING_RECORD(entry, IRP_QUEUE_ITEM, ListEntry);

        KdPrint(("Canceling processed client IRP=%p\n", item->Irp));

        RemoveEntryList(entry);

        DevIoDrvCompleteIrpQueueItem(item, STATUS_DEVICE_DOES_NOT_EXIST, 0, &raised_irql);
    }

    ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

    return STATUS_SUCCESS;
}

PIRP_QUEUE_ITEM
DevIoDrvGetSentClientRequest(POBJECT_CONTEXT File,
    ULONGLONG Tag,
    PKIRQL LowestAssumedIrql)
{
    KLOCK_QUEUE_HANDLE lock_handle;

    PIRP_QUEUE_ITEM found_item = NULL;

    ImScsiAcquireLock(&File->IrpListLock, &lock_handle, *LowestAssumedIrql);

    for (PLIST_ENTRY entry = File->ClientSentIrpList.Flink; entry != &File->ClientSentIrpList; entry = entry->Flink)
    {
        if ((ULONGLONG)(ULONG_PTR)entry == Tag)
        {
            RemoveEntryList(entry);
            found_item = CONTAINING_RECORD(entry, IRP_QUEUE_ITEM, ListEntry);
            break;
        }
    }

    ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

    return found_item;
}

void
DevIoDrvCompleteIrpQueueItem(PIRP_QUEUE_ITEM Item,
    NTSTATUS Status,
    ULONG_PTR Length,
    PKIRQL LowestAssumedIrql)
{
    if (Status == STATUS_BUFFER_TOO_SMALL &&
        Item->MemoryIrp != NULL)
    {
        DevIoDrvCompleteIrpQueueItem(Item->MemoryIrp, Status, Length, LowestAssumedIrql);
        Item->MemoryIrp = NULL;
    }
    else if (Item->MemoryIrp != NULL)
    {
        POBJECT_CONTEXT context = DevIoDrvGetContext(IoGetCurrentIrpStackLocation(Item->Irp));

        DevIoDrvQueueMemoryIrp(Item->MemoryIrp, context, LowestAssumedIrql);
    }

    DevIoDrvCompleteIrp(Item->Irp, Status, Length);

    ExFreePoolWithTag(Item, POOL_TAG);
}

void
DevIoDrvCompleteIrp(PIRP Irp,
    NTSTATUS Status,
    ULONG_PTR Length)
{
    KdPrint(("Completing IRP=%p with status %#x\n", Irp, Status));

    Irp->IoStatus.Information = Length;
    Irp->IoStatus.Status = Status;

    IoCompleteRequest(Irp, Status == STATUS_SUCCESS ? IO_DISK_INCREMENT : IO_NO_INCREMENT);
}

NTSTATUS
DevIoDrvReserveMemoryIrp(PIRP RequestIrp,
    PIRP_QUEUE_ITEM *MemoryIrp,
    PIMDPROXY_DEVIODRV_BUFFER_HEADER *MemoryBuffer,
    PULONG BufferSize,
    PKIRQL LowestAssumedIrql)
{
    *MemoryIrp = NULL;
    *MemoryBuffer = NULL;
    *BufferSize = 0;

    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(RequestIrp);
    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    if (io_stack->Parameters.DeviceIoControl.OutputBufferLength > 0)
    {
        *MemoryBuffer =
            (PIMDPROXY_DEVIODRV_BUFFER_HEADER)MmGetSystemAddressForMdlSafe(RequestIrp->MdlAddress,
                NormalPagePriority);

        if (*MemoryBuffer != NULL)
        {
            *BufferSize = io_stack->Parameters.DeviceIoControl.OutputBufferLength;
            return STATUS_SUCCESS;
        }
        else
        {
            return STATUS_INSUFFICIENT_RESOURCES;
        }
    }

    if (io_stack->Parameters.DeviceIoControl.InputBufferLength == 0)
    {
        return STATUS_INVALID_PARAMETER;
    }

    KLOCK_QUEUE_HANDLE lock_handle;

    ImScsiAcquireLock(&context->IrpListLock, &lock_handle, *LowestAssumedIrql);

    for (PLIST_ENTRY entry = context->ServerMemoryIrpList.Flink; entry != &context->ServerMemoryIrpList; entry = entry->Flink)
    {
        PIRP_QUEUE_ITEM item = CONTAINING_RECORD(entry, IRP_QUEUE_ITEM, ListEntry);
        PIO_STACK_LOCATION mem_irp_io_stack = IoGetCurrentIrpStackLocation(item->Irp);

        if (io_stack->Parameters.DeviceIoControl.InputBufferLength == mem_irp_io_stack->Parameters.DeviceIoControl.InputBufferLength &&
            RtlEqualMemory(RequestIrp->AssociatedIrp.SystemBuffer, item->Irp->AssociatedIrp.SystemBuffer,
                io_stack->Parameters.DeviceIoControl.InputBufferLength))
        {
            KIRQL cancel_irql;

            IoAcquireCancelSpinLock(&cancel_irql);
            
            if (item->Irp->Cancel)
            {
                IoReleaseCancelSpinLock(cancel_irql);
                continue;
            }

            IoSetCancelRoutine(item->Irp, NULL);

            IoReleaseCancelSpinLock(cancel_irql);

            RemoveEntryList(&item->ListEntry);

            *MemoryIrp = item;
            *MemoryBuffer = item->MappedBuffer;
            *BufferSize = mem_irp_io_stack->Parameters.DeviceIoControl.OutputBufferLength;

            ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

            return STATUS_SUCCESS;
        }
    }

    ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

    return STATUS_INVALID_PARAMETER;
}

void
DevIoDrvQueueMemoryIrpUnsafe(PIRP_QUEUE_ITEM MemoryIrp,
    POBJECT_CONTEXT Context)
{
    if (MemoryIrp == NULL)
    {
        return;
    }

    IoSetCancelRoutine(MemoryIrp->Irp, DevIoDrvServerIrpCancelRoutine);

    IoMarkIrpPending(MemoryIrp->Irp);

    InsertTailList(&Context->ServerMemoryIrpList, &MemoryIrp->ListEntry);
}

void
DevIoDrvQueueMemoryIrp(PIRP_QUEUE_ITEM MemoryIrp,
    POBJECT_CONTEXT Context,
    PKIRQL LowestAssumedIrql)
{
    if (MemoryIrp == NULL)
    {
        return;
    }

    KLOCK_QUEUE_HANDLE lock_handle;
    ImScsiAcquireLock(&Context->IrpListLock, &lock_handle, *LowestAssumedIrql);

    KIRQL cancel_irql;

    IoAcquireCancelSpinLock(&cancel_irql);

    DevIoDrvQueueMemoryIrpUnsafe(MemoryIrp, Context);

    IoReleaseCancelSpinLock(cancel_irql);

    ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);
}

NTSTATUS
DevIoDrvDispatchServerLockMemory(PIRP Irp,
    PKIRQL LowestAssumedIrql)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    KdPrint(("IRP=%p Server Memory Lock request.\n", Irp));

    if (io_stack->Parameters.DeviceIoControl.OutputBufferLength < IMDPROXY_HEADER_SIZE)
    {
        KdPrint(("IRP=%p Buffer too small: %u.\n",
            Irp, io_stack->Parameters.DeviceIoControl.OutputBufferLength));

        DevIoDrvCompleteIrp(Irp, STATUS_BUFFER_TOO_SMALL, 0);

        return STATUS_BUFFER_TOO_SMALL;
    }

    PIMDPROXY_DEVIODRV_BUFFER_HEADER buffer =
        (PIMDPROXY_DEVIODRV_BUFFER_HEADER)MmGetSystemAddressForMdlSafe(Irp->MdlAddress,
            NormalPagePriority);

    if (buffer == NULL)
    {
        KdPrint(("IRP=%p Failed getting direct I/O address.\n",
            Irp));

        DevIoDrvCompleteIrp(Irp, STATUS_INSUFFICIENT_RESOURCES, 0);

        return STATUS_INSUFFICIENT_RESOURCES;
    }
    
    KdPrint(("IRP=%p Request to lock memory. Queuing server IRP.\n", Irp));

    PIRP_QUEUE_ITEM memory_irp = (PIRP_QUEUE_ITEM)
        ExAllocatePoolWithTag(NonPagedPool, sizeof IRP_QUEUE_ITEM, POOL_TAG);

    if (memory_irp == NULL)
    {
        DevIoDrvCompleteIrp(Irp, STATUS_INSUFFICIENT_RESOURCES, 0);

        return STATUS_INSUFFICIENT_RESOURCES;
    }

    RtlZeroMemory(memory_irp, sizeof(*memory_irp));

    memory_irp->Irp = Irp;
    memory_irp->MappedBuffer = buffer;

    DevIoDrvQueueMemoryIrp(memory_irp, context, LowestAssumedIrql);

    return STATUS_PENDING;
}

NTSTATUS
DevIoDrvDispatchServerIORequest(PIRP Irp,
    PKIRQL LowestAssumedIrql)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    KdPrint(("IRP=%p Server I/O request.\n", Irp));

    if (io_stack->Parameters.DeviceIoControl.OutputBufferLength > 0 &&
        io_stack->Parameters.DeviceIoControl.OutputBufferLength < IMDPROXY_HEADER_SIZE)
    {
        KdPrint(("IRP=%p Buffer too small: %u.\n",
            Irp, io_stack->Parameters.DeviceIoControl.OutputBufferLength));

        DevIoDrvCompleteIrp(Irp, STATUS_BUFFER_TOO_SMALL, 0);

        return STATUS_BUFFER_TOO_SMALL;
    }

    PIRP_QUEUE_ITEM memory_irp;
    PIMDPROXY_DEVIODRV_BUFFER_HEADER buffer;
    ULONG buffer_size;

    NTSTATUS status = DevIoDrvReserveMemoryIrp(Irp, &memory_irp, &buffer, &buffer_size, LowestAssumedIrql);

    if (!NT_SUCCESS(status))
    {
        KdPrint(("IRP=%p Failed getting direct I/O address. %#x\n",
            Irp, status));

        DevIoDrvCompleteIrp(Irp, status, 0);

        return status;
    }

    KdPrint(("IRP=%p I/O tag %#I64x, request code %#I64x flags %#I64x.\n",
        Irp, buffer->io_tag, buffer->request_code, buffer->flags));

    PIRP_QUEUE_ITEM client_item = NULL;

    if (buffer->io_tag != 0)
    {
        client_item = DevIoDrvGetSentClientRequest(context, buffer->io_tag, LowestAssumedIrql);

        if (client_item == NULL)
        {
            KdPrint(("IRP=%p Error finding client request with tag %#I64x.\n",
                Irp, buffer->io_tag));

            DevIoDrvCompleteIrp(Irp, STATUS_INVALID_PARAMETER, 0);

            DevIoDrvQueueMemoryIrp(memory_irp, context, LowestAssumedIrql);

            return STATUS_INVALID_PARAMETER;
        }

        KdPrint(("IRP=%p Server request completing client IRP=%p.\n",
            Irp, client_item->Irp));
    }

    PIMDPROXY_READ_RESP read_write_response = (PIMDPROXY_READ_RESP)(buffer + 1);

    if (client_item == NULL && buffer->request_code == IMDPROXY_REQ_INFO)
    {
        KdPrint(("IRP=%p IMDPROXY_REQ_INFO.\n",
            Irp));

        PIMDPROXY_INFO_RESP response = (PIMDPROXY_INFO_RESP)(buffer + 1);
        context->FileSize.QuadPart = response->file_size;
        context->AlignmentRequirement = (ULONG)(response->req_alignment - 1);
        context->ServiceFlags = response->flags;
    }
    else if (client_item != NULL &&
        IoGetCurrentIrpStackLocation(client_item->Irp)->MajorFunction == IRP_MJ_READ &&
        buffer->request_code == IMDPROXY_REQ_READ &&
        IoGetCurrentIrpStackLocation(client_item->Irp)->Parameters.Read.Length >= read_write_response->length &&
        buffer_size >= IMDPROXY_HEADER_SIZE + read_write_response->length)
    {
        KdPrint(("IRP=%p IMDPROXY_REQ_READ.\n", Irp));

        PIMDPROXY_READ_RESP response = (PIMDPROXY_READ_RESP)(buffer + 1);

        if (response->length > 0)
        {
            PVOID client_buffer = MmGetSystemAddressForMdlSafe(client_item->Irp->MdlAddress, NormalPagePriority);

            if (client_buffer == NULL)
            {
                DevIoDrvCompleteIrpQueueItem(client_item, STATUS_INSUFFICIENT_RESOURCES, 0, LowestAssumedIrql);
                DevIoDrvCompleteIrp(Irp, STATUS_INSUFFICIENT_RESOURCES, 0);
                DevIoDrvQueueMemoryIrp(memory_irp, context, LowestAssumedIrql);

                return STATUS_INSUFFICIENT_RESOURCES;
            }

            RtlCopyMemory(client_buffer, ((PUCHAR)buffer) + IMDPROXY_HEADER_SIZE, (SIZE_T)response->length);
        }

        KIRQL irql;

        KeRaiseIrql(APC_LEVEL, &irql);

        KIRQL new_irql = APC_LEVEL;

        if (response->errorno == 0)
        {
            DevIoDrvCompleteIrpQueueItem(client_item, STATUS_SUCCESS, (ULONG_PTR)response->length, &new_irql);
        }
        else
        {
            DevIoDrvCompleteIrpQueueItem(client_item, STATUS_IO_DEVICE_ERROR, (ULONG_PTR)response->length, &new_irql);
        }

        KeLowerIrql(irql);
    }
    else if (client_item != NULL &&
        IoGetCurrentIrpStackLocation(client_item->Irp)->MajorFunction == IRP_MJ_WRITE &&
        buffer->request_code == IMDPROXY_REQ_WRITE &&
        IoGetCurrentIrpStackLocation(client_item->Irp)->Parameters.Write.Length >= read_write_response->length)
    {
        KdPrint(("IRP=%p IMDPROXY_REQ_WRITE.\n",
            Irp));

        PIMDPROXY_WRITE_RESP response = (PIMDPROXY_WRITE_RESP)(buffer + 1);

        KIRQL irql;

        KeRaiseIrql(APC_LEVEL, &irql);

        KIRQL new_irql = APC_LEVEL;

        if (response->errorno == 0)
        {
            DevIoDrvCompleteIrpQueueItem(client_item, STATUS_SUCCESS, (ULONG_PTR)response->length, &new_irql);
        }
        else
        {
            DevIoDrvCompleteIrpQueueItem(client_item, STATUS_IO_DEVICE_ERROR, (ULONG_PTR)response->length, &new_irql);
        }

        KeLowerIrql(irql);
    }
    else if (client_item != NULL &&
        IoGetCurrentIrpStackLocation(client_item->Irp)->MajorFunction == IRP_MJ_FILE_SYSTEM_CONTROL &&
        buffer->request_code == IMDPROXY_REQ_ZERO &&
        IoGetCurrentIrpStackLocation(client_item->Irp)->Parameters.FileSystemControl.FsControlCode == FSCTL_SET_ZERO_DATA)
    {
        KdPrint(("IRP=%p IMDPROXY_REQ_ZERO.\n",
            Irp));

        PIMDPROXY_ZERO_RESP response = (PIMDPROXY_ZERO_RESP)(buffer + 1);

        KIRQL irql;

        KeRaiseIrql(APC_LEVEL, &irql);

        KIRQL new_irql = APC_LEVEL;

        if (response->errorno == 0)
        {
            DevIoDrvCompleteIrpQueueItem(client_item, STATUS_SUCCESS, 0, &new_irql);
        }
        else
        {
            DevIoDrvCompleteIrpQueueItem(client_item, STATUS_IO_DEVICE_ERROR, 0, &new_irql);
        }

        KeLowerIrql(irql);
    }
    else if (client_item != NULL &&
        IoGetCurrentIrpStackLocation(client_item->Irp)->MajorFunction == IRP_MJ_FILE_SYSTEM_CONTROL &&
        buffer->request_code == IMDPROXY_REQ_UNMAP &&
        IoGetCurrentIrpStackLocation(client_item->Irp)->Parameters.FileSystemControl.FsControlCode == FSCTL_FILE_LEVEL_TRIM)
    {
        KdPrint(("IRP=%p IMDPROXY_REQ_UNMAP.\n",
            Irp));

        PIMDPROXY_UNMAP_RESP response = (PIMDPROXY_UNMAP_RESP)(buffer + 1);

        KIRQL irql;

        KeRaiseIrql(APC_LEVEL, &irql);

        KIRQL new_irql = APC_LEVEL;

        if (response->errorno == 0)
        {
            if (IoGetCurrentIrpStackLocation(client_item->Irp)->Parameters.FileSystemControl.OutputBufferLength >=
                sizeof(FILE_LEVEL_TRIM_OUTPUT))
            {
                PFILE_LEVEL_TRIM in_buffer = (PFILE_LEVEL_TRIM)client_item->Irp->AssociatedIrp.SystemBuffer;
                PFILE_LEVEL_TRIM_OUTPUT out_buffer = (PFILE_LEVEL_TRIM_OUTPUT)client_item->Irp->AssociatedIrp.SystemBuffer;
                out_buffer->NumRangesProcessed = in_buffer->NumRanges;
            }

            DevIoDrvCompleteIrpQueueItem(client_item, STATUS_SUCCESS, sizeof(FILE_LEVEL_TRIM_OUTPUT), &new_irql);
        }
        else
        {
            DevIoDrvCompleteIrpQueueItem(client_item, STATUS_IO_DEVICE_ERROR, 0, &new_irql);
        }

        KeLowerIrql(irql);
    }
    else if (client_item == NULL && buffer->request_code == IMDPROXY_REQ_NULL)
    {
        // no response here, just wants next request

        KdPrint(("IRP=%p IMDPROXY_REQ_NULL.\n",
            Irp));
    }
    else
    {
        DbgPrint("IRP=%p Bad server request.\n", Irp);

        KdBreakPoint();

        if (client_item != NULL)
        {
            DevIoDrvCompleteIrpQueueItem(client_item, STATUS_IO_DEVICE_ERROR, 0, LowestAssumedIrql);
        }

        DevIoDrvCompleteIrp(Irp, STATUS_INVALID_PARAMETER, 0);
        DevIoDrvQueueMemoryIrp(memory_irp, context, LowestAssumedIrql);
        return STATUS_INVALID_PARAMETER;
    }

    PIRP_QUEUE_ITEM server_item = (PIRP_QUEUE_ITEM)ExAllocatePoolWithTag(NonPagedPool, sizeof IRP_QUEUE_ITEM, POOL_TAG);

    if (server_item == NULL)
    {
        DevIoDrvCompleteIrp(Irp, STATUS_INSUFFICIENT_RESOURCES, 0);
        DevIoDrvQueueMemoryIrp(memory_irp, context, LowestAssumedIrql);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    RtlZeroMemory(server_item, sizeof *server_item);

    server_item->Irp = Irp;

    KLOCK_QUEUE_HANDLE lock_handle;
    ImScsiAcquireLock(&context->IrpListLock, &lock_handle, *LowestAssumedIrql);

    PLIST_ENTRY entry = RemoveHeadList(&context->ClientReceivedIrpList);

    if (entry == &context->ClientReceivedIrpList)
    {
        KdPrint(("IRP=%p No client request available. Queuing server IRP.\n", Irp));

        KIRQL cancel_irql;

        IoAcquireCancelSpinLock(&cancel_irql);

        DevIoDrvQueueMemoryIrpUnsafe(memory_irp, context);

        IoSetCancelRoutine(Irp, DevIoDrvServerIrpCancelRoutine);

        IoMarkIrpPending(Irp);

        InsertTailList(&context->ServerRequestIrpList, &server_item->ListEntry);

        IoReleaseCancelSpinLock(cancel_irql);

        ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

        return STATUS_PENDING;
    }

    ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

    server_item->MemoryIrp = memory_irp;
    server_item->MappedBuffer = buffer;
    server_item->MappedBufferSize = buffer_size;

    KdPrint(("IRP=%p Client request found. Completing server request in-thread.\n",
        Irp));

    client_item = CONTAINING_RECORD(entry, IRP_QUEUE_ITEM, ListEntry);

    NTSTATUS client_status, server_status;
    DevIoDrvSendClientRequestToServer(context, client_item, server_item, &client_status, &server_status, LowestAssumedIrql);

    return server_status;
}

void
DevIoDrvSendClientRequestToServer(POBJECT_CONTEXT File,
    PIRP_QUEUE_ITEM ClientItem,
    PIRP_QUEUE_ITEM ServerItem,
    NTSTATUS *ClientStatus,
    NTSTATUS *ServerStatus,
    PKIRQL LowestAssumedIrql)
{
    if (ServerItem->MappedBuffer == NULL)
    {
        NTSTATUS status = DevIoDrvReserveMemoryIrp(ServerItem->Irp, &ServerItem->MemoryIrp, &ServerItem->MappedBuffer, &ServerItem->MappedBufferSize, LowestAssumedIrql);

        if (!NT_SUCCESS(status))
        {
            KdPrint(("IRP=%p Failed getting direct I/O address. %#x\n",
                ServerItem->Irp, status));

            DevIoDrvCompleteIrpQueueItem(ClientItem, status, 0, LowestAssumedIrql);
            DevIoDrvCompleteIrpQueueItem(ServerItem, status, 0, LowestAssumedIrql);

            *ClientStatus = *ServerStatus = status;

            return;
        }
    }

    ServerItem->MappedBuffer->flags = 0;
    ServerItem->MappedBuffer->io_tag = (ULONGLONG)(ULONG_PTR)&ClientItem->ListEntry;

    KdPrint(("Sending client IRP=%p to server IRP=%p. I/O tag = %#I64x\n",
        ClientItem->Irp, ServerItem->Irp, ServerItem->MappedBuffer->io_tag));

    PIO_STACK_LOCATION io_stack_client = IoGetCurrentIrpStackLocation(ClientItem->Irp);

    if (io_stack_client->MajorFunction == IRP_MJ_READ)
    {
        if (ServerItem->MappedBufferSize < IMDPROXY_HEADER_SIZE + io_stack_client->Parameters.Read.Length)
        {
            IoMarkIrpPending(ClientItem->Irp);
            ImScsiInterlockedInsertHeadList(&File->ClientReceivedIrpList, &ClientItem->ListEntry, &File->IrpListLock, LowestAssumedIrql);

            DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_BUFFER_TOO_SMALL, 0, LowestAssumedIrql);

            *ClientStatus = STATUS_PENDING;
            *ServerStatus = STATUS_BUFFER_TOO_SMALL;

            return;
        }

        ServerItem->MappedBuffer->request_code = IMDPROXY_REQ_READ;

        PIMDPROXY_READ_REQ request = (PIMDPROXY_READ_REQ)(ServerItem->MappedBuffer + 1);
        request->request_code = IMDPROXY_REQ_READ;
        request->offset = io_stack_client->Parameters.Read.ByteOffset.QuadPart;
        request->length = io_stack_client->Parameters.Read.Length;

        IoMarkIrpPending(ClientItem->Irp);

        ImScsiInterlockedInsertTailList(&File->ClientSentIrpList, &ClientItem->ListEntry, &File->IrpListLock, LowestAssumedIrql);

        DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_SUCCESS, IMDPROXY_HEADER_SIZE, LowestAssumedIrql);

        *ClientStatus = STATUS_PENDING;
        *ServerStatus = STATUS_SUCCESS;
    }
    else if (io_stack_client->MajorFunction == IRP_MJ_WRITE)
    {
        if (ServerItem->MappedBufferSize < IMDPROXY_HEADER_SIZE + io_stack_client->Parameters.Write.Length)
        {
            IoMarkIrpPending(ClientItem->Irp);
            ImScsiInterlockedInsertHeadList(&File->ClientReceivedIrpList, &ClientItem->ListEntry, &File->IrpListLock, LowestAssumedIrql);

            DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_BUFFER_TOO_SMALL, 0, LowestAssumedIrql);

            *ClientStatus = STATUS_PENDING;
            *ServerStatus = STATUS_BUFFER_TOO_SMALL;
            
            return;
        }

        if (io_stack_client->Parameters.Write.Length > 0)
        {
            PVOID client_buffer = MmGetSystemAddressForMdlSafe(ClientItem->Irp->MdlAddress, NormalPagePriority);

            if (client_buffer == NULL)
            {
                DevIoDrvCompleteIrpQueueItem(ClientItem, STATUS_INSUFFICIENT_RESOURCES, 0, LowestAssumedIrql);
                DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_INSUFFICIENT_RESOURCES, 0, LowestAssumedIrql);

                *ClientStatus = *ServerStatus = STATUS_INSUFFICIENT_RESOURCES;
        
                return;
            }

            RtlCopyMemory(((PUCHAR)ServerItem->MappedBuffer) + IMDPROXY_HEADER_SIZE, client_buffer, io_stack_client->Parameters.Write.Length);
        }

        ServerItem->MappedBuffer->request_code = IMDPROXY_REQ_WRITE;

        PIMDPROXY_WRITE_REQ request = (PIMDPROXY_WRITE_REQ)(ServerItem->MappedBuffer + 1);
        request->request_code = IMDPROXY_REQ_WRITE;
        request->offset = io_stack_client->Parameters.Write.ByteOffset.QuadPart;
        request->length = io_stack_client->Parameters.Write.Length;

        IoMarkIrpPending(ClientItem->Irp);

        ImScsiInterlockedInsertTailList(&File->ClientSentIrpList, &ClientItem->ListEntry, &File->IrpListLock, LowestAssumedIrql);

        DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_SUCCESS, IMDPROXY_HEADER_SIZE + (ULONG_PTR)io_stack_client->Parameters.Write.Length, LowestAssumedIrql);

        *ClientStatus = STATUS_PENDING;
        *ServerStatus = STATUS_SUCCESS;
    }
    else if (io_stack_client->MajorFunction == IRP_MJ_FILE_SYSTEM_CONTROL &&
        io_stack_client->Parameters.FileSystemControl.FsControlCode == FSCTL_SET_ZERO_DATA)
    {
        if (ServerItem->MappedBufferSize < IMDPROXY_HEADER_SIZE + sizeof DEVICE_DATA_SET_RANGE)
        {
            IoMarkIrpPending(ClientItem->Irp);
            ImScsiInterlockedInsertHeadList(&File->ClientReceivedIrpList, &ClientItem->ListEntry, &File->IrpListLock, LowestAssumedIrql);

            DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_BUFFER_TOO_SMALL, 0, LowestAssumedIrql);

            *ClientStatus = STATUS_PENDING;
            *ServerStatus = STATUS_BUFFER_TOO_SMALL;
            return;
        }

        PFILE_ZERO_DATA_INFORMATION zero_info = (PFILE_ZERO_DATA_INFORMATION)ClientItem->Irp->AssociatedIrp.SystemBuffer;

        ServerItem->MappedBuffer->request_code = IMDPROXY_REQ_ZERO;

        PIMDPROXY_ZERO_REQ request = (PIMDPROXY_ZERO_REQ)(ServerItem->MappedBuffer + 1);
        request->request_code = IMDPROXY_REQ_ZERO;
        request->length = sizeof DEVICE_DATA_SET_RANGE;

        PDEVICE_DATA_SET_RANGE range = (PDEVICE_DATA_SET_RANGE)((PUCHAR)ServerItem->MappedBuffer + IMDPROXY_HEADER_SIZE);
        range->StartingOffset = zero_info->FileOffset.QuadPart;
        range->LengthInBytes = zero_info->BeyondFinalZero.QuadPart - zero_info->FileOffset.QuadPart;

        IoMarkIrpPending(ClientItem->Irp);

        ImScsiInterlockedInsertTailList(&File->ClientSentIrpList, &ClientItem->ListEntry, &File->IrpListLock, LowestAssumedIrql);

        DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_SUCCESS, IMDPROXY_HEADER_SIZE, LowestAssumedIrql);

        *ClientStatus = STATUS_PENDING;
        *ServerStatus = STATUS_SUCCESS;
    }
    else if (io_stack_client->MajorFunction == IRP_MJ_FILE_SYSTEM_CONTROL &&
        io_stack_client->Parameters.FileSystemControl.FsControlCode == FSCTL_FILE_LEVEL_TRIM)
    {
        PFILE_LEVEL_TRIM trim_info = (PFILE_LEVEL_TRIM)ClientItem->Irp->AssociatedIrp.SystemBuffer;

        if (ServerItem->MappedBufferSize < IMDPROXY_HEADER_SIZE + trim_info->NumRanges * sizeof DEVICE_DATA_SET_RANGE)
        {
            IoMarkIrpPending(ClientItem->Irp);
            ImScsiInterlockedInsertHeadList(&File->ClientReceivedIrpList, &ClientItem->ListEntry, &File->IrpListLock, LowestAssumedIrql);

            DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_BUFFER_TOO_SMALL, 0, LowestAssumedIrql);

            *ClientStatus = STATUS_PENDING;
            *ServerStatus = STATUS_BUFFER_TOO_SMALL;
            return;
        }

        ServerItem->MappedBuffer->request_code = IMDPROXY_REQ_UNMAP;

        PIMDPROXY_ZERO_REQ request = (PIMDPROXY_ZERO_REQ)(ServerItem->MappedBuffer + 1);
        request->request_code = IMDPROXY_REQ_UNMAP;
        request->length = trim_info->NumRanges * sizeof DEVICE_DATA_SET_RANGE;

        PDEVICE_DATA_SET_RANGE range = (PDEVICE_DATA_SET_RANGE)((PUCHAR)ServerItem->MappedBuffer + IMDPROXY_HEADER_SIZE);

        for (ULONG i = 0; i < trim_info->NumRanges; i++)
        {
            range[i].StartingOffset = trim_info->Ranges[i].Offset;
            range[i].LengthInBytes = trim_info->Ranges[i].Length;
        }

        IoMarkIrpPending(ClientItem->Irp);

        ImScsiInterlockedInsertTailList(&File->ClientSentIrpList, &ClientItem->ListEntry, &File->IrpListLock, LowestAssumedIrql);

        DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_SUCCESS, IMDPROXY_HEADER_SIZE, LowestAssumedIrql);

        *ClientStatus = STATUS_PENDING;
        *ServerStatus = STATUS_SUCCESS;
    }
    else
    {
        DevIoDrvCompleteIrpQueueItem(ClientItem, STATUS_INTERNAL_ERROR, 0, LowestAssumedIrql);
        DevIoDrvCompleteIrpQueueItem(ServerItem, STATUS_INTERNAL_ERROR, 0, LowestAssumedIrql);

        *ClientStatus = *ServerStatus = STATUS_INTERNAL_ERROR;
    }
}

NTSTATUS
DevIoDrvDispatchClientRequest(PIRP Irp,
    PKIRQL LowestAssumedIrql)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    if (context->Server == NULL)
    {
        KdPrint(("DevIoDrv: IRP=%p Request to disconnected device.\n", Irp));
        
        DevIoDrvCompleteIrp(Irp, STATUS_DEVICE_DOES_NOT_EXIST, 0);
        return STATUS_DEVICE_DOES_NOT_EXIST;
    }

    PIRP_QUEUE_ITEM client_item = (PIRP_QUEUE_ITEM)ExAllocatePoolWithTag(NonPagedPool, sizeof IRP_QUEUE_ITEM, POOL_TAG);

    if (client_item == NULL)
    {
        DevIoDrvCompleteIrp(Irp, STATUS_INSUFFICIENT_RESOURCES, 0);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    RtlZeroMemory(client_item, sizeof *client_item);

    client_item->Irp = Irp;

    KLOCK_QUEUE_HANDLE lock_handle;

    ImScsiAcquireLock(&context->IrpListLock, &lock_handle, *LowestAssumedIrql);

    if (context->Server == NULL)
    {
        ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

        KdPrint(("DevIoDrv: IRP=%p Request to disconnected device.\n", Irp));

        DevIoDrvCompleteIrpQueueItem(client_item, STATUS_DEVICE_DOES_NOT_EXIST, 0, LowestAssumedIrql);

        return STATUS_DEVICE_DOES_NOT_EXIST;
    }

    KIRQL cancel_irql;

    IoAcquireCancelSpinLock(&cancel_irql);

    PLIST_ENTRY entry;
    PIRP_QUEUE_ITEM server_item;

    for (;;)
    {
        entry = RemoveHeadList(&context->ServerRequestIrpList);
        server_item = CONTAINING_RECORD(entry, IRP_QUEUE_ITEM, ListEntry);

        if (entry != &context->ServerRequestIrpList)
        {
            if (server_item->Irp->Cancel)
            {
                continue;
            }

            IoSetCancelRoutine(server_item->Irp, NULL);
        }

        break;
    }

    IoReleaseCancelSpinLock(cancel_irql);

    if (entry == &context->ServerRequestIrpList)
    {
        IoMarkIrpPending(Irp);

        InsertTailList(&context->ClientReceivedIrpList, &client_item->ListEntry);

        ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

        KdPrint(("IRP=%p FileObject=%p No server irp available. Queuing client request.\n",
            Irp, io_stack->FileObject));

        return STATUS_PENDING;
    }

    ImScsiReleaseLock(&lock_handle, LowestAssumedIrql);

    KdPrint(("IRP=%p FileObject=%p Found pending server IRP=%p. Completing client request in-thread.\n",
        Irp, io_stack->FileObject, server_item->Irp));

    NTSTATUS client_status, server_status;
    DevIoDrvSendClientRequestToServer(context, client_item, server_item, &client_status, &server_status, LowestAssumedIrql);

    return client_status;
}

void
DevIoDrvServerIrpCancelRoutine(PDEVICE_OBJECT, PIRP Irp)
{
    IoSetCancelRoutine(Irp, NULL);

    IoReleaseCancelSpinLock(DISPATCH_LEVEL);

    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    KdPrint(("IRP=%p FileObject=%p Cancel request.\n", Irp, io_stack->FileObject));

    KLOCK_QUEUE_HANDLE lock_handle;

    ImScsiAcquireLock(&context->IrpListLock, &lock_handle, DISPATCH_LEVEL);

    PLIST_ENTRY irp_list = NULL;

    if (io_stack->Parameters.DeviceIoControl.IoControlCode == IOCTL_DEVIODRV_EXCHANGE_IO)
    {
        irp_list = &context->ServerRequestIrpList;
    }
    else if (io_stack->Parameters.DeviceIoControl.IoControlCode == IOCTL_DEVIODRV_LOCK_MEMORY)
    {
        irp_list = &context->ServerMemoryIrpList;
    }
    else
    {
        RtlAssert("Unknown IoControlCode in cancel routine.", __FILE__, __LINE__,
            "Unknown IoControlCode in cancel routine.");
    }

    PIRP_QUEUE_ITEM item = NULL;

    for (PLIST_ENTRY entry = irp_list->Flink; entry != irp_list; entry = entry->Flink)
    {
        item = CONTAINING_RECORD(entry, IRP_QUEUE_ITEM, ListEntry);

        if (item->Irp == Irp)
        {
            RemoveEntryList(entry);
            ExFreePoolWithTag(item, POOL_TAG);
            break;
        }
    }

    KIRQL lowest_assumed_irql = DISPATCH_LEVEL;

    ImScsiReleaseLock(&lock_handle, &lowest_assumed_irql);

    KeLowerIrql(Irp->CancelIrql);

    DevIoDrvCompleteIrp(Irp, STATUS_CANCELLED, 0);
}
