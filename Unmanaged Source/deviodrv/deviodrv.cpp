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

#include <wdm.h>
#include <ntdddisk.h>

#include "deviodrv.h"

#pragma code_seg("INIT")

NTSTATUS
DriverEntry(PDRIVER_OBJECT DriverObject, PUNICODE_STRING)
{
    PAGED_CODE();

    KdPrint(("DevIoDrv loading.\n"));

#if DBG

#if (NTDDI_VERSION >= NTDDI_WS03)
    KdRefreshDebuggerNotPresent();
#endif

    if (!KD_DEBUGGER_NOT_PRESENT)
        DbgBreakPoint();
#endif

    InitializeListHead(&FileTable);

    DevIoDrvInitializeSpinLock(&ReferencedObjectsListLock);

    InitializeListHead(&ReferencedObjects);

    DriverObject->DriverUnload = DevIoDrvUnload;

    UNICODE_STRING dev_path;
    RtlInitUnicodeString(&dev_path, DEVIODRV_DEVICE_NATIVE_NAME);

    PDEVICE_OBJECT DeviceObject;

    NTSTATUS status = IoCreateDevice(DriverObject, 0, &dev_path, FILE_DEVICE_DISK_FILE_SYSTEM, 0, FALSE, &DeviceObject);
    if (!NT_SUCCESS(status))
    {
        DbgPrint("Failed creating device %wZ: %#x\n", &dev_path, status);
        return status;
    }
    UNICODE_STRING symlink_path;
    RtlInitUnicodeString(&symlink_path, DEVIODRV_SYMLINK_NATIVE_NAME);
    
    status = IoCreateUnprotectedSymbolicLink(&symlink_path, &dev_path);
    if (!NT_SUCCESS(status))
    {
        IoDeleteDevice(DeviceObject);
        DbgPrint("Failed creating symlink %wZ to %wZ: %#x\n", &symlink_path, &dev_path, status);
        return status;
    }

    DeviceObject->Flags |= DO_DIRECT_IO;

    DriverObject->MajorFunction[IRP_MJ_CREATE] = DevIoDrvDispatchCreate;
    DriverObject->MajorFunction[IRP_MJ_CLOSE] = DevIoDrvDispatchClose;
    DriverObject->MajorFunction[IRP_MJ_CLEANUP] = DevIoDrvDispatchCleanup;

    DriverObject->MajorFunction[IRP_MJ_READ] = DevIoDrvDispatchReadWrite;
    DriverObject->MajorFunction[IRP_MJ_WRITE] = DevIoDrvDispatchReadWrite;
    DriverObject->MajorFunction[IRP_MJ_DEVICE_CONTROL] = DevIoDrvDispatchControl;
    DriverObject->MajorFunction[IRP_MJ_INTERNAL_DEVICE_CONTROL] = DevIoDrvDispatchControl;
    DriverObject->MajorFunction[IRP_MJ_FILE_SYSTEM_CONTROL] = DevIoDrvDispatchControl;

    DriverObject->MajorFunction[IRP_MJ_QUERY_INFORMATION] = DevIoDrvDispatchQueryInformation;

    DriverObject->MajorFunction[IRP_MJ_FLUSH_BUFFERS] = DevIoDrvDispatchFlushBuffers;

    KdPrint(("DevIoDrv loaded.\n"));

    return STATUS_SUCCESS;
}

#pragma code_seg("PAGE")

void
DevIoDrvInitializeSpinLock(PKSPIN_LOCK Lock)
{
    KeInitializeSpinLock(Lock);
}

void
DevIoDrvUnload(PDRIVER_OBJECT DriverObject)
{
    PAGED_CODE();

    KdPrint(("DevIoDrv unloading.\n"));

    for (;;)
    {
        PLIST_ENTRY list_entry = RemoveTailList(&ReferencedObjects);
        
        if (list_entry == &ReferencedObjects)
        {
            break;
        }

        PREFERENCED_OBJECT record =
            CONTAINING_RECORD(list_entry, REFERENCED_OBJECT, list_entry);

        ObDereferenceObject(record->file_object);

        ExFreePoolWithTag(record, POOL_TAG);
    }

    UNICODE_STRING symlink_path;
    RtlInitUnicodeString(&symlink_path, DEVIODRV_SYMLINK_NATIVE_NAME);

    NTSTATUS status = IoDeleteSymbolicLink(&symlink_path);
    if (!NT_SUCCESS(status))
    {
        DbgPrint("Failed removing symlink %wZ: %#x\n", &symlink_path, status);
    }

    while (DriverObject->DeviceObject != NULL)
    {
#pragma warning(suppress: 6001)
        IoDeleteDevice(DriverObject->DeviceObject);
    }
}
