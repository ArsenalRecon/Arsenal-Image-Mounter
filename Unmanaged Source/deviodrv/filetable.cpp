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

#include "deviodrv.h"

#pragma code_seg("PAGE")

LIST_ENTRY FileTable;

LONG FileTableListLock;

NTSTATUS DevIoDrvOpenFileTableEntry(PFILE_OBJECT FileObject, ULONG DesiredAccess)
{
    PAGED_CODE();

    BOOLEAN check_attributes_only =
        (DesiredAccess & 0xF001017FL) == 0;

    BOOLEAN requests_write_access =
        (DesiredAccess & (GENERIC_WRITE | GENERIC_ALL | FILE_WRITE_DATA)) != 0;

    if (InterlockedExchange(&FileTableListLock, 1) != 0)
    {
        return STATUS_DEVICE_BUSY;
    }

    PLIST_ENTRY entry = FileTable.Flink;

    NTSTATUS status = STATUS_OBJECT_NAME_NOT_FOUND;

    POBJECT_CONTEXT context = NULL;

    LONG ref_number = 0;

    while (entry != &FileTable)
    {
        context = CONTAINING_RECORD(entry, OBJECT_CONTEXT, ListEntry);

        if (RtlEqualUnicodeString(&FileObject->FileName, &context->Name, TRUE))
        {
            ref_number = InterlockedIncrement(&context->RefCount);

            if (ref_number == 2)
            {
                context->Client = FileObject;
            }

            FileObject->FsContext2 = context;

            status = STATUS_SUCCESS;

            break;
        }

        entry = entry->Flink;
    }

    InterlockedExchange(&FileTableListLock, 0);

    if (status == STATUS_SUCCESS)
    {
        // Looking for file metadata only and server side not yet
        // initialized
        if (check_attributes_only &&
            context->FileSize.QuadPart == 0)
        {
            status = STATUS_OBJECT_NAME_NOT_FOUND;
        }
        // Opening client side can only be done once
        else if (!check_attributes_only &&
            ref_number != 2)
        {
            status = STATUS_SHARING_VIOLATION;
        }
        // Opening client side for writing when server side
        // is read-only
        else if (requests_write_access &&
            FlagOn(context->ServiceFlags, IMDPROXY_FLAG_RO))
        {
            status = STATUS_MEDIA_WRITE_PROTECTED;
        }
        else if (!check_attributes_only)
        {
            FileObject->ReadAccess = TRUE;

            if (requests_write_access)
            {
                FileObject->WriteAccess = TRUE;
            }
        }
    }

    if (!NT_SUCCESS(status)
        && ref_number > 0)
    {
        DevIoDrvCloseFileTableEntry(FileObject);
    }

    return status;
}

NTSTATUS DevIoDrvCreateFileTableEntry(PFILE_OBJECT FileObject)
{
    PAGED_CODE();

    if (InterlockedExchange(&FileTableListLock, 1) != 0)
    {
        return STATUS_DEVICE_BUSY;
    }

    PLIST_ENTRY entry = FileTable.Flink;

    NTSTATUS status = STATUS_SUCCESS;

    POBJECT_CONTEXT context = NULL;

    while (entry != &FileTable)
    {
        context = CONTAINING_RECORD(entry, OBJECT_CONTEXT, ListEntry);

        if (RtlEqualUnicodeString(&FileObject->FileName, &context->Name, TRUE))
        {
            status = STATUS_OBJECT_NAME_COLLISION;
            break;
        }

        entry = entry->Flink;
    }

    if (NT_SUCCESS(status))
    {
        context = (POBJECT_CONTEXT)ExAllocatePoolWithTag(NonPagedPool, sizeof OBJECT_CONTEXT, POOL_TAG);

        if (context == NULL)
        {
            status = STATUS_INSUFFICIENT_RESOURCES;
        }
    }

    if (NT_SUCCESS(status))
    {
        RtlZeroMemory(context, sizeof OBJECT_CONTEXT);

        context->Name.Buffer = (PWCHAR)ExAllocatePoolWithTag(NonPagedPool, FileObject->FileName.Length, POOL_TAG);

        if (context->Name.Buffer == NULL)
        {
            ExFreePoolWithTag(context, POOL_TAG);
            status = STATUS_INSUFFICIENT_RESOURCES;
        }
    }

    if (NT_SUCCESS(status))
    {
        context->Name.MaximumLength = context->Name.Length = FileObject->FileName.Length;
        RtlCopyUnicodeString(&context->Name, &FileObject->FileName);

        DevIoDrvInitializeSpinLock(&context->IrpListLock);
        InitializeListHead(&context->ServerRequestIrpList);
        InitializeListHead(&context->ServerMemoryIrpList);
        InitializeListHead(&context->ClientReceivedIrpList);
        InitializeListHead(&context->ClientSentIrpList);

        context->Server = FileObject;
        context->RefCount = 1;
        FileObject->FsContext2 = context;

        InsertHeadList(&FileTable, &context->ListEntry);

        FileObject->ReadAccess = TRUE;
        FileObject->WriteAccess = TRUE;
    }

    InterlockedExchange(&FileTableListLock, 0);

    return status;
}

NTSTATUS DevIoDrvCloseFileTableEntry(PFILE_OBJECT FileObject)
{
    PAGED_CODE();

    if (InterlockedExchange(&FileTableListLock, 1) != 0)
    {
        return STATUS_RESOURCE_IN_USE;
    }

    POBJECT_CONTEXT context = (POBJECT_CONTEXT)FileObject->FsContext2;

    if (FileObject == context->Server)
    {
        context->Server = NULL;
    }
    else if (FileObject == context->Client)
    {
        context->Client = NULL;
    }

    if (InterlockedDecrement(&context->RefCount) > 0)
    {
        KdPrint(("Reference to file '%wZ' closing. Not last reference.\n", &context->Name));

        InterlockedExchange(&FileTableListLock, 0);
        return STATUS_SUCCESS;
    }

    KdPrint(("Last reference to file '%wZ' closing.\n", &context->Name));

    RemoveEntryList(&context->ListEntry);

    FileObject->FsContext2 = NULL;

    InterlockedExchange(&FileTableListLock, 0);

    ExFreePoolWithTag(context->Name.Buffer, POOL_TAG);

    ExFreePoolWithTag(context, POOL_TAG);

    return STATUS_SUCCESS;
}

