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
#include <ntdddisk.h>

#include "deviodrv.h"

#include <common.h>

#pragma warning(disable: 4201)

//
// ReferencedObjects list lock
//
KSPIN_LOCK ReferencedObjectsListLock;

//
// List of objects referenced using
// IOCTL_DEVIODRV_REFERENCE_HANDLE
//
LIST_ENTRY ReferencedObjects;

NTSTATUS
DevIoDrvDispatchCreate(PDEVICE_OBJECT, PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    Irp->IoStatus.Information = 0;

    union CREATE_OPTIONS
    {
        struct
        {
            ULONG CreateOptions : 24;
            ULONG CreateDisposition : 8;
        };
        ULONG Options;
    } Options;
    Options.Options = io_stack->Parameters.Create.Options;
    
    KdPrint(("Creating handle for '%wZ' FileObject=%p DesiredAccess=%#x CreateDisposition=%#x CreateOptions=%#x Flags=%#x\n",
        &io_stack->FileObject->FileName, io_stack->FileObject, io_stack->Parameters.Create.SecurityContext->DesiredAccess,
        Options.CreateDisposition, Options.CreateOptions, io_stack->FileObject->Flags));

    NTSTATUS status;

    if (io_stack->FileObject->FileName.Length == 0) // No file name, access to some IOCTL commands
    {
        status = STATUS_SUCCESS;
    }
    else if (Options.CreateDisposition == FILE_CREATE) // Create server end
    {
        status = DevIoDrvCreateFileTableEntry(io_stack->FileObject);

        if (status == STATUS_SUCCESS)
        {
            Irp->IoStatus.Information = FILE_CREATED;
        }
        else if (status == STATUS_OBJECT_NAME_COLLISION)
        {
            Irp->IoStatus.Information = FILE_EXISTS;
        }
    }
    else if (Options.CreateDisposition == FILE_OPEN) // Open client end
    {
        status = DevIoDrvOpenFileTableEntry(io_stack->FileObject,
            io_stack->Parameters.Create.SecurityContext->DesiredAccess);

        if (status == STATUS_SUCCESS)
        {
            Irp->IoStatus.Information = FILE_OPENED;
        }
        else if (status == STATUS_OBJECT_NAME_NOT_FOUND ||
            status == STATUS_SHARING_VIOLATION)
        {
            Irp->IoStatus.Information = FILE_DOES_NOT_EXIST;
        }
    }
    else
    {
        status = STATUS_INVALID_DEVICE_REQUEST;
    }

    Irp->IoStatus.Status = status;

    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return status;
}

NTSTATUS
DevIoDrvDispatchClose(PDEVICE_OBJECT, PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    Irp->IoStatus.Information = 0;
    Irp->IoStatus.Status = STATUS_SUCCESS;

    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    if (context != NULL)
    {
        KdPrint(("Closing handle for FileObject=%p Name='%wZ'\n",
            io_stack->FileObject, &context->Name));

        while (!NT_SUCCESS(DevIoDrvCloseFileTableEntry(io_stack->FileObject)))
        {
            LARGE_INTEGER interval;
            interval.QuadPart = -200000;
            KeDelayExecutionThread(KernelMode, TRUE, &interval);
        }
    }
    else
    {
        KdPrint(("Closing handle for FileObject=%p\n",
            io_stack->FileObject));
    }

    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return STATUS_SUCCESS;
}

NTSTATUS
DevIoDrvDispatchCleanup(PDEVICE_OBJECT, PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    Irp->IoStatus.Information = 0;

    NTSTATUS status;

    if (context != NULL &&
        (io_stack->FileObject == context->Server ||
            (!FlagOn(context->ServiceFlags, IMDPROXY_FLAG_KEEP_OPEN) &&
                (io_stack->FileObject->ReadAccess || io_stack->FileObject->WriteAccess))))
    {
        KdPrint(("IRP=%p Cleanup request for FileObject=%p Name='%wZ'\n",
            Irp, io_stack->FileObject, &context->Name));

        KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

        status = DevIoDrvCancelAll(io_stack->FileObject, &lowest_assumed_irql);
    }
    else
    {
        KdPrint(("IRP=%p Cleanup request for FileObject=%p\n",
            Irp, io_stack->FileObject));

        status = STATUS_SUCCESS;
    }

    Irp->IoStatus.Status = status;

    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return status;
}

NTSTATUS
DevIoDrvDispatchControl(PDEVICE_OBJECT, PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    Irp->IoStatus.Information = 0;

    KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;

    if (context == NULL)
    {
        switch (io_stack->Parameters.DeviceIoControl.IoControlCode)
        {
        case IOCTL_DEVIODRV_REFERENCE_HANDLE:
        {
            KLOCK_QUEUE_HANDLE lock_handle = { 0 };
            PREFERENCED_OBJECT record;

            KdPrint((__FUNCTION__ ": IOCTL_DEVIODRV_REFERENCE_HANDLE.\n"));

            // This IOCTL requires work that must be done at IRQL < DISPATCH_LEVEL
            // but must be done in the thread context of the calling application and
            // not by the worker thread so therefore this check is done. Also, the
            // control device does not have a worker thread so that is another
            // reason.
            if (KeGetCurrentIrql() > PASSIVE_LEVEL)
            {
                KdPrint((__FUNCTION__ ": IOCTL_DEVIODRV_REFERENCE_HANDLE not accessible "
                    "at current IRQL (%i).", KeGetCurrentIrql()));

                status = STATUS_ACCESS_DENIED;

                break;
            }

            if ((io_stack->MajorFunction != IRP_MJ_INTERNAL_DEVICE_CONTROL) &&
                !SeSinglePrivilegeCheck(RtlConvertLongToLuid(SE_TCB_PRIVILEGE),
                    Irp->RequestorMode))
            {
                KdPrint((__FUNCTION__ ": IOCTL_DEVIODRV_REFERENCE_HANDLE not accessible, "
                    "privilege not held by calling thread.\n"));

                status = STATUS_ACCESS_DENIED;

                break;
            }

            if ((io_stack->Parameters.DeviceIoControl.InputBufferLength <
                sizeof(HANDLE)) ||
                (io_stack->Parameters.DeviceIoControl.OutputBufferLength <
                    sizeof(PFILE_OBJECT)))
            {
                status = STATUS_INVALID_PARAMETER;
                break;
            }

            record = (PREFERENCED_OBJECT)
                ExAllocatePoolWithTag(NonPagedPool,
                    sizeof(REFERENCED_OBJECT), POOL_TAG);

            if (record == NULL)
            {
                status = STATUS_INSUFFICIENT_RESOURCES;
                break;
            }

            KdPrint((__FUNCTION__ ": Referencing handle %p.\n",
                *(PHANDLE)Irp->AssociatedIrp.SystemBuffer));

            status = ObReferenceObjectByHandle(
                *(PHANDLE)Irp->AssociatedIrp.SystemBuffer,
                FILE_READ_ATTRIBUTES |
                FILE_READ_DATA |
                FILE_WRITE_DATA,
                *IoFileObjectType,
                Irp->RequestorMode,
                (PVOID*)&record->file_object,
                NULL);

            KdPrint((__FUNCTION__ ": Status=%#x, PFILE_OBJECT %p.\n",
                status,
                record->file_object));

            if (!NT_SUCCESS(status))
            {
                ExFreePoolWithTag(record, POOL_TAG);
                break;
            }

            ImScsiAcquireLock(&ReferencedObjectsListLock, &lock_handle, lowest_assumed_irql);

            InsertTailList(&ReferencedObjects, &record->list_entry);

            ImScsiReleaseLock(&lock_handle, &lowest_assumed_irql);

            *(PFILE_OBJECT*)Irp->AssociatedIrp.SystemBuffer = record->file_object;
            Irp->IoStatus.Information = sizeof(PFILE_OBJECT);

            break;
        }

        case IOCTL_DEVIODRV_GET_REFERENCED_HANDLE:
        {
            KLOCK_QUEUE_HANDLE lock_handle = { 0 };
            PLIST_ENTRY list_entry;

            if (io_stack->MajorFunction != IRP_MJ_INTERNAL_DEVICE_CONTROL)
            {
                status = STATUS_ACCESS_DENIED;
                break;
            }

            if (io_stack->Parameters.DeviceIoControl.InputBufferLength <
                sizeof(PFILE_OBJECT))
            {
                status = STATUS_INVALID_PARAMETER;
                break;
            }

            status = STATUS_OBJECT_NAME_NOT_FOUND;

            ImScsiAcquireLock(&ReferencedObjectsListLock, &lock_handle, lowest_assumed_irql);

            for (list_entry = ReferencedObjects.Flink;
                list_entry != &ReferencedObjects;
                list_entry = list_entry->Flink)
            {
                PREFERENCED_OBJECT record =
                    CONTAINING_RECORD(list_entry, REFERENCED_OBJECT, list_entry);

                if (record->file_object ==
                    *(PFILE_OBJECT*)Irp->AssociatedIrp.SystemBuffer)
                {
                    list_entry->Blink->Flink = list_entry->Flink;
                    list_entry->Flink->Blink = list_entry->Blink;

                    ExFreePoolWithTag(record, POOL_TAG);

                    status = STATUS_SUCCESS;
                    break;
                }
            }

            ImScsiReleaseLock(&lock_handle, &lowest_assumed_irql);

            if (NT_SUCCESS(status))
            {
                KdPrint((__FUNCTION__ ": Successfully claimed referenced object %p.\n",
                    *(PFILE_OBJECT*)Irp->AssociatedIrp.SystemBuffer));
            }
            else
            {
                DbgPrint(__FUNCTION__ ": Warning: Requested %p not in referenced objects list.\n",
                    *(PFILE_OBJECT*)Irp->AssociatedIrp.SystemBuffer);
            }

            break;
        }
        }

        Irp->IoStatus.Status = status;

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return status;
    }

    if (!(io_stack->FileObject->ReadAccess | io_stack->FileObject->WriteAccess) ||
        (io_stack->MajorFunction != IRP_MJ_DEVICE_CONTROL &&
            io_stack->MajorFunction != IRP_MJ_INTERNAL_DEVICE_CONTROL &&
        !(io_stack->MajorFunction == IRP_MJ_FILE_SYSTEM_CONTROL &&
        (io_stack->MinorFunction == IRP_MN_KERNEL_CALL ||
            io_stack->MinorFunction == IRP_MN_USER_FS_REQUEST))))
    {
        DevIoDrvCompleteIrp(Irp, STATUS_INVALID_DEVICE_REQUEST, 0);

        return STATUS_INVALID_DEVICE_REQUEST;
    }

    switch (io_stack->Parameters.DeviceIoControl.IoControlCode)
    {
    case FSCTL_SET_ZERO_DATA:
    {
        if (!FlagOn(context->ServiceFlags, IMDPROXY_FLAG_SUPPORTS_ZERO))
        {
            break;
        }

        PFILE_ZERO_DATA_INFORMATION zero = (PFILE_ZERO_DATA_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.FileSystemControl.InputBufferLength != sizeof FILE_ZERO_DATA_INFORMATION ||
            zero->BeyondFinalZero.QuadPart < zero->FileOffset.QuadPart)
        {
            status = STATUS_INVALID_PARAMETER;
            break;
        }

        return DevIoDrvDispatchClientRequest(Irp, &lowest_assumed_irql);
    }

    case FSCTL_FILE_LEVEL_TRIM:
    {
        if (!FlagOn(context->ServiceFlags, IMDPROXY_FLAG_SUPPORTS_UNMAP))
        {
            break;
        }

        PFILE_LEVEL_TRIM trim = (PFILE_LEVEL_TRIM)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.FileSystemControl.InputBufferLength < sizeof FILE_LEVEL_TRIM ||
            io_stack->Parameters.FileSystemControl.InputBufferLength <
            FIELD_OFFSET(FILE_LEVEL_TRIM, Ranges) + trim->NumRanges * (ULONGLONG)sizeof(*trim->Ranges))
        {
            status = STATUS_INVALID_PARAMETER;
            break;
        }

        return DevIoDrvDispatchClientRequest(Irp, &lowest_assumed_irql);
    }

    case IOCTL_DEVIODRV_EXCHANGE_IO:
    {
        if (context->Server != io_stack->FileObject)
        {
            status = STATUS_INVALID_DEVICE_REQUEST;
            break;
        }

        return DevIoDrvDispatchServerIORequest(Irp, &lowest_assumed_irql);
    }

    case IOCTL_DEVIODRV_LOCK_MEMORY:
    {
        if (context->Server != io_stack->FileObject)
        {
            status = STATUS_INVALID_DEVICE_REQUEST;
            break;
        }

        return DevIoDrvDispatchServerLockMemory(Irp, &lowest_assumed_irql);
    }

    case IOCTL_DISK_GET_LENGTH_INFO:
    {
        PGET_LENGTH_INFORMATION info =
            (PGET_LENGTH_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.QueryFile.Length < sizeof(GET_LENGTH_INFORMATION))
        {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }

        RtlZeroMemory(info, sizeof(GET_LENGTH_INFORMATION));

        if (context != NULL)
        {
            info->Length = context->FileSize;
        }

        Irp->IoStatus.Information = sizeof(GET_LENGTH_INFORMATION);
        status = STATUS_SUCCESS;
        break;
    }
    }

    Irp->IoStatus.Status = status;

    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return status;
}

NTSTATUS
DevIoDrvDispatchSetInformation(PDEVICE_OBJECT, PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    Irp->IoStatus.Information = 0;

    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;

    switch (io_stack->Parameters.SetFile.FileInformationClass)
    {
    case FilePositionInformation:
    {
        PFILE_POSITION_INFORMATION position_info =
            (PFILE_POSITION_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.SetFile.Length <
            sizeof(FILE_POSITION_INFORMATION))
        {
            status = STATUS_INVALID_PARAMETER;
            break;
        }

        if (io_stack->FileObject != NULL)
        {
            io_stack->FileObject->CurrentByteOffset =
                position_info->CurrentByteOffset;
        }

        status = STATUS_SUCCESS;
        break;
    }

    case FileBasicInformation:
    case FileDispositionInformation:
    case FileValidDataLengthInformation:
        status = STATUS_SUCCESS;
        break;

    default:
    {
        KdPrint((__FUNCTION__ ": Unknown SetInformation function %#x.\n",
            io_stack->Parameters.SetFile.FileInformationClass));

        status = STATUS_INVALID_DEVICE_REQUEST;
    }
    }

    Irp->IoStatus.Status = status;

    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return status;
}

NTSTATUS
DevIoDrvDispatchReadWrite(PDEVICE_OBJECT, PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    Irp->IoStatus.Information = 0;

    KdPrint(("IRP=%p Read for FileObject=%p\n", Irp, io_stack->FileObject));

    if ((io_stack->Parameters.Read.ByteOffset.QuadPart < 0) ||
        ((io_stack->Parameters.Read.ByteOffset.QuadPart +
            io_stack->Parameters.Read.Length) < 0))
    {
        KdPrint((__FUNCTION__ ": Read/write attempt on negative offset.\n"));

        Irp->IoStatus.Status = STATUS_INVALID_PARAMETER;

        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        return STATUS_INVALID_PARAMETER;
    }

    NTSTATUS status;

    KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    if (context == NULL ||
        (io_stack->MajorFunction == IRP_MJ_READ && !io_stack->FileObject->ReadAccess) ||
        (io_stack->MajorFunction == IRP_MJ_WRITE && !io_stack->FileObject->WriteAccess))
    {
        status = STATUS_ACCESS_DENIED;
    }
    else if (context->Server == NULL)
    {
        KdPrint((__FUNCTION__ ": IRP=%p Request to disconnected device.\n", Irp));

        status = STATUS_DEVICE_DOES_NOT_EXIST;
    }
    else if (io_stack->MajorFunction == IRP_MJ_READ &&
        io_stack->Parameters.Read.Length == 0 &&
        io_stack->Parameters.Read.ByteOffset.QuadPart < context->FileSize.QuadPart)
    {
        status = STATUS_SUCCESS;
    }
    else if (io_stack->MajorFunction == IRP_MJ_WRITE &&
        FlagOn(context->ServiceFlags, IMDPROXY_FLAG_RO))
    {
        status = STATUS_MEDIA_WRITE_PROTECTED;
    }
    else if (io_stack->MajorFunction == IRP_MJ_WRITE &&
        io_stack->Parameters.Write.Length == 0 &&
        io_stack->Parameters.Write.ByteOffset.QuadPart < context->FileSize.QuadPart)
    {
        status = STATUS_SUCCESS;
    }
    else if (context->Client == io_stack->FileObject ||
        context->Server == io_stack->FileObject)
    {
        return DevIoDrvDispatchClientRequest(Irp, &lowest_assumed_irql);
    }
    else
    {
        status = STATUS_INTERNAL_ERROR;
    }

    Irp->IoStatus.Status = status;

    IoCompleteRequest(Irp, status == STATUS_SUCCESS ? IO_DISK_INCREMENT : IO_NO_INCREMENT);

    return status;
}

NTSTATUS
DevIoDrvDispatchQueryInformation(PDEVICE_OBJECT, PIRP Irp)
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    POBJECT_CONTEXT context = DevIoDrvGetContext(io_stack);

    if (context != NULL)
    {
        KdPrint(("IRP=%p Query information for '%wZ' FileObject=%p\n",
            Irp, &context->Name, io_stack->FileObject));
    }
    else
    {
        KdPrint(("IRP=%p Query information for FileObject=%p\n",
            Irp, io_stack->FileObject));
    }

    Irp->IoStatus.Information = 0;

    NTSTATUS status;

    status = STATUS_INVALID_DEVICE_REQUEST;

    switch (io_stack->Parameters.QueryFile.FileInformationClass)
    {
    case FileStandardInformation:
    {
        PFILE_STANDARD_INFORMATION standard_info =
            (PFILE_STANDARD_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.QueryFile.Length < sizeof(FILE_STANDARD_INFORMATION))
        {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }

        RtlZeroMemory(standard_info, sizeof(FILE_STANDARD_INFORMATION));

        if (context != NULL)
        {
            standard_info->AllocationSize =
                standard_info->EndOfFile = context->FileSize;
        }

        Irp->IoStatus.Information = sizeof(FILE_STANDARD_INFORMATION);
        status = STATUS_SUCCESS;
        break;
    }

    case FileNetworkOpenInformation:
    {
        PFILE_NETWORK_OPEN_INFORMATION info =
            (PFILE_NETWORK_OPEN_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.QueryFile.Length < sizeof(FILE_NETWORK_OPEN_INFORMATION))
        {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }

        RtlZeroMemory(info, sizeof(FILE_NETWORK_OPEN_INFORMATION));

        if (context != NULL)
        {
            info->AllocationSize =
                info->EndOfFile = context->FileSize;
        }

        Irp->IoStatus.Information = sizeof(FILE_NETWORK_OPEN_INFORMATION);
        status = STATUS_SUCCESS;
        break;
    }

    case FilePositionInformation:
    {
        PFILE_POSITION_INFORMATION position_info =
            (PFILE_POSITION_INFORMATION)Irp->AssociatedIrp.SystemBuffer;

        if (io_stack->Parameters.QueryFile.Length <
            sizeof(FILE_POSITION_INFORMATION))
        {
            status = STATUS_BUFFER_TOO_SMALL;
            break;
        }

        position_info->CurrentByteOffset =
            io_stack->FileObject->CurrentByteOffset;

        Irp->IoStatus.Information = sizeof(FILE_POSITION_INFORMATION);

        status = STATUS_SUCCESS;
        break;
    }

    default:
        KdPrint((__FUNCTION__ ": Unknown QueryInformation function %#x.\n",
            io_stack->Parameters.QueryFile.FileInformationClass));

        status = STATUS_INVALID_DEVICE_REQUEST;
    }

    Irp->IoStatus.Status = status;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return status;
}

NTSTATUS
DevIoDrvDispatchFlushBuffers(PDEVICE_OBJECT, PIRP Irp)
{
    KdPrint(("IRP=%p Flush buffers for FileObject=%p\n",
        Irp, IoGetCurrentIrpStackLocation(Irp)->FileObject));

    DevIoDrvCompleteIrp(Irp, STATUS_SUCCESS, 0);

    return STATUS_SUCCESS;
}

