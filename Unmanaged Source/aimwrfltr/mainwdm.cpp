#include "aimwrfltr.h"

#include <common.h>

#if DBG
#pragma warning(disable: 28175)
#endif

const UCHAR diff_file_magic[FIELD_SIZE(AIMWRFLTR_VBR_HEAD_FIELDS, Magic)] =
{ 0xF4, 0xEB, 0xFD, 0x00, 0x00, 0x00, 0x00, 'A', 'I', 'M', 'W', 'r', 'F', 'l', 't', 'r' };

const USHORT vbr_signature = 0xAA55;

const ULONG major_version = 1UL;

const ULONG minor_version = 0UL;

HANDLE AIMWrFltrParametersKey = NULL;
PKEVENT AIMWrFltrDiffFullEvent = NULL;
PDRIVER_OBJECT AIMWrFltrDriverObject = NULL;
bool AIMWrFltrLinksCreated = false;
bool QueueWithoutCache = false;

//
// Define the sections that allow for discarding (i.e. paging) some of
// the code.
//

#ifdef ALLOC_PRAGMA
#pragma alloc_text (INIT, DriverEntry)
#pragma alloc_text (PAGE, AIMWrFltrCreate)
#pragma alloc_text (PAGE, AIMWrFltrAddDevice)
#pragma alloc_text (PAGE, AIMWrFltrGetDiffDevicePath)
#pragma alloc_text (PAGE, AIMWrFltrStartDevice)
#pragma alloc_text (PAGE, AIMWrFltrRemoveDevice)
#ifndef DBG
#pragma alloc_text (PAGE, AIMWrFltrPnp)
#pragma alloc_text (PAGE, AIMWrFltrCleanupDevice)
#endif
#pragma alloc_text (PAGE, AIMWrFltrTrim)
#pragma alloc_text (PAGE, AIMWrFltrDeviceUsageNotification)
#pragma alloc_text (PAGE, AIMWrFltrForwardIrpSynchronous)
#pragma alloc_text (PAGE, AIMWrFltrUnload)
#pragma alloc_text (PAGE, AIMWrFltrSyncFilterWithTarget)
#endif

NTSTATUS
DriverEntry(IN PDRIVER_OBJECT DriverObject, IN PUNICODE_STRING RegistryPath)
/*++

Routine Description:

Installable driver initialization entry point.
This entry point is called directly by the I/O manager to set up the disk
filter driver. The driver object is set up and then the Pnp manager
calls AIMWrFltrAddDevice to attach to the boot devices.

Arguments:

DriverObject - The write filter driver object.

RegistryPath - pointer to a unicode string representing the path,
to driver-specific key in the registry.

Return Value:

STATUS_SUCCESS if successful

--*/
{
    AIMWrFltrDriverObject = DriverObject;

#if DBG
    if (!KdRefreshDebuggerNotPresent())
    {
        DbgBreakPoint();
    }
#endif

    NTSTATUS status;
    HANDLE event_handle;

    UNICODE_STRING event_path;
    RtlInitUnicodeString(&event_path,
        L"\\Device\\" AIMWRFLTR_DIFF_FULL_EVENT_NAME);

    WPagedPoolMem<SECURITY_DESCRIPTOR> event_security_descriptor(
        SECURITY_DESCRIPTOR_MIN_LENGTH);

    if (event_security_descriptor)
    {
        status = STATUS_SUCCESS;
    }
    else
    {
        status = STATUS_INSUFFICIENT_RESOURCES;
        KdPrint(("AIMWrFltr:DriverEntry: Memory allocation error for security descriptor.\n"));
    }

    if (NT_SUCCESS(status))
    {
        status = RtlCreateSecurityDescriptor(
            event_security_descriptor,
            SECURITY_DESCRIPTOR_REVISION);
    }

    if (NT_SUCCESS(status))
    {
#pragma warning(suppress: 6248)
        status = RtlSetDaclSecurityDescriptor(
            event_security_descriptor,
            TRUE,
            NULL,
            FALSE);
    }

    OBJECT_ATTRIBUTES event_obj_attrs;
    InitializeObjectAttributes(&event_obj_attrs,
        &event_path,
        OBJ_PERMANENT | OBJ_OPENIF,
        NULL,
        event_security_descriptor);

    status = ZwCreateEvent(
        &event_handle,
        EVENT_ALL_ACCESS,
        &event_obj_attrs,
        NotificationEvent,
        FALSE);

    if (!NT_SUCCESS(status))
    {
        DbgPrint("AIMWrFltr:DriverEntry: Cannot create diff full event '%wZ': %#x\n",
            event_obj_attrs.ObjectName, status);

        KdBreakPoint();
    }
    else
    {
        status = ObReferenceObjectByHandle(event_handle, EVENT_ALL_ACCESS,
            *ExEventObjectType, KernelMode, (PVOID*)&AIMWrFltrDiffFullEvent,
            NULL);

        if (!NT_SUCCESS(status))
        {
            DbgPrint("AIMWrFltr:DriverEntry: Cannot reference diff full event '%wZ': %#x\n",
                event_obj_attrs.ObjectName, status);

            AIMWrFltrDiffFullEvent = NULL;
        }

        ZwClose(event_handle);
    }

    //
    // Remember registry path
    //

    static const WCHAR parameters_suffix[] = L"\\Parameters";

    UNICODE_STRING param_key_path;

    param_key_path.MaximumLength = RegistryPath->Length
        + sizeof(parameters_suffix);

    WPagedPoolMem<WCHAR> param_key_buffer(param_key_path.MaximumLength);

    if (!param_key_buffer)
    {
        KdPrint(("AIMWrFltr::DriverEntry: Memory allocation error.\n"));

        KdBreakPoint();

        return STATUS_INSUFFICIENT_RESOURCES;
    }

    param_key_path.Buffer = param_key_buffer;

    RtlCopyUnicodeString(&param_key_path, RegistryPath);
    RtlAppendUnicodeToString(&param_key_path, parameters_suffix);
    param_key_path.Buffer[param_key_path.Length / sizeof(WCHAR)] = 0;

    OBJECT_ATTRIBUTES param_key_obj_attrs;
    InitializeObjectAttributes(&param_key_obj_attrs, &param_key_path,
        OBJ_CASE_INSENSITIVE | OBJ_OPENIF, NULL, NULL);

    ULONG key_disposition;
    status = ZwCreateKey(&AIMWrFltrParametersKey, KEY_READ,
        &param_key_obj_attrs, 0, NULL, REG_OPTION_NON_VOLATILE,
        &key_disposition);

    if (!NT_SUCCESS(status))
    {
        DbgPrint("AIMWrFltr::DriverEntry: Error opening key '%wZ': %#x\n",
            param_key_obj_attrs.ObjectName, status);

        KdBreakPoint();

        return STATUS_SUCCESS;
    }
    
    UNICODE_STRING queue_without_cache_str;
    RtlInitUnicodeString(&queue_without_cache_str, L"QueueWithoutCache");
    union
    {
        KEY_VALUE_PARTIAL_INFORMATION queue_without_cache_value;
        UCHAR buffer[FIELD_OFFSET(KEY_VALUE_PARTIAL_INFORMATION, Data) + sizeof(ULONG)];
    };
    ULONG length;
    status = ZwQueryValueKey(AIMWrFltrParametersKey, &queue_without_cache_str,
        KeyValuePartialInformation, &queue_without_cache_value, sizeof(queue_without_cache_value), &length);

    if (NT_SUCCESS(status))
    {
        QueueWithoutCache = *(bool*)queue_without_cache_value.Data;
        DbgPrint("AIMWrFltr: QueueWithoutCache = 0x%X\n", QueueWithoutCache);
    }

    //
    // Create dispatch points
    //
    ULONG ulIndex;
    PDRIVER_DISPATCH *dispatch;
    for (
        ulIndex = 0, dispatch = DriverObject->MajorFunction;
        ulIndex <= IRP_MJ_MAXIMUM_FUNCTION;
        ulIndex++, dispatch++)
    {
        *dispatch = AIMWrFltrSendToNextDriver;
    }

    //
    // Set up the device driver entry points.
    //

    DriverObject->MajorFunction[IRP_MJ_CREATE] = AIMWrFltrCreate;

    DriverObject->MajorFunction[IRP_MJ_READ] = AIMWrFltrRead;
    DriverObject->MajorFunction[IRP_MJ_WRITE] = AIMWrFltrWrite;
    DriverObject->MajorFunction[IRP_MJ_DEVICE_CONTROL] = AIMWrFltrDeviceControl;
    DriverObject->MajorFunction[IRP_MJ_INTERNAL_DEVICE_CONTROL] = AIMWrFltrDeviceControl;

    DriverObject->MajorFunction[IRP_MJ_FLUSH_BUFFERS] = AIMWrFltrFlushBuffers;

    DriverObject->MajorFunction[IRP_MJ_PNP] = AIMWrFltrPnp;

    DriverObject->DriverExtension->AddDevice = AIMWrFltrAddDevice;

    DriverObject->DriverUnload = AIMWrFltrUnload;

    return STATUS_SUCCESS;

}				// end DriverEntry()

#define FILTER_DEVICE_PROPOGATE_FLAGS            0
#define FILTER_DEVICE_PROPOGATE_CHARACTERISTICS (FILE_REMOVABLE_MEDIA |  \
                                                 FILE_READ_ONLY_DEVICE | \
                                                 FILE_FLOPPY_DISKETTE    \
                                                 )

VOID
AIMWrFltrSyncFilterWithTarget(IN PDEVICE_OBJECT FilterDevice,
    IN PDEVICE_OBJECT TargetDevice)
{
    ULONG prop_flags;

    PAGED_CODE();

    //
    // Propagate all useful flags from target to AIMWrFltr. MountMgr will look
    // at the AIMWrFltr object capabilities to figure out if the disk is
    // a removable and perhaps other things.
    //
    prop_flags = TargetDevice->Flags & FILTER_DEVICE_PROPOGATE_FLAGS;
    FilterDevice->Flags |= prop_flags;

    prop_flags =
        TargetDevice->Characteristics & FILTER_DEVICE_PROPOGATE_CHARACTERISTICS;
    FilterDevice->Characteristics |= prop_flags;
}

NTSTATUS
AIMWrFltSaveDiffHeader(IN PDEVICE_EXTENSION DeviceExtension)
{
    LARGE_INTEGER offset;
    IO_STATUS_BLOCK io_status;

    offset.QuadPart = 0;

    NTSTATUS status = AIMWrFltrSynchronousReadWrite(
        DeviceExtension->DiffDeviceObject,
        DeviceExtension->DiffFileObject,
        IRP_MJ_WRITE,
        DeviceExtension->Statistics.DiffDeviceVbr.Raw.Bytes,
        sizeof(DeviceExtension->Statistics.DiffDeviceVbr),
        &offset,
        NULL,
        &io_status);

    if (!NT_SUCCESS(status) || io_status.Information !=
        sizeof(DeviceExtension->Statistics.DiffDeviceVbr))
    {
        DbgPrint("AIMWrFiltr: Error writing diff file header: %#x\n", status);
        return status;
    }

    offset.QuadPart =
        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.OffsetToAllocationTable;

    ULONG alloc_table_size = (ULONG)DeviceExtension->Statistics.
        DiffDeviceVbr.Fields.Head.AllocationTableBlocks << DIFF_BLOCK_BITS;

    status = AIMWrFltrSynchronousReadWrite(
        DeviceExtension->DiffDeviceObject,
        DeviceExtension->DiffFileObject,
        IRP_MJ_WRITE,
        (PVOID)DeviceExtension->AllocationTable,
        alloc_table_size,
        &offset,
        NULL,
        &io_status);

    if (io_status.Information != alloc_table_size || !NT_SUCCESS(status))
    {
        DbgPrint("AIMWrFiltr: Error writing diff allocation table: %#x\n", status);
        return status;
    }

    return STATUS_SUCCESS;
}

VOID
AIMWrFltrCleanupDevice(IN PDEVICE_EXTENSION DeviceExtension)
{
#ifndef DBG
    PAGED_CODE();
#endif

    if (DeviceExtension->WorkerThread != NULL)
    {
#if DBG

        KIRQL current_irql = PASSIVE_LEVEL;
        KLOCK_QUEUE_HANDLE lock_handle;
        ULONG items_in_queue = 0;

        AIMWrFltrAcquireLock(&DeviceExtension->ListLock, &lock_handle,
            current_irql);

        for (PLIST_ENTRY entry = DeviceExtension->ListHead.Flink;
            entry != &DeviceExtension->ListHead;
            entry = entry->Flink)
        {
            items_in_queue++;
        }
        
        AIMWrFltrReleaseLock(&lock_handle, &current_irql);

        DbgPrint("AIMWrFltrCleanupDevice: Shutting down worker thread with %i items in queue\n",
            items_in_queue);

#endif

        DeviceExtension->ShutdownThread = true;
        KeSetEvent(&DeviceExtension->ListEvent, 0, FALSE);
        ZwWaitForSingleObject(DeviceExtension->WorkerThread, FALSE, NULL);
        ZwClose(DeviceExtension->WorkerThread);
        DeviceExtension->WorkerThread = NULL;
    }

    if (DeviceExtension->AllocationTable != NULL &&
        DeviceExtension->Statistics.Initialized)
    {
        AIMWrFltSaveDiffHeader(DeviceExtension);

        delete[] DeviceExtension->AllocationTable;
        DeviceExtension->AllocationTable = NULL;
    }

    if (DeviceExtension->DiffFileObject != NULL)
    {
        ObDereferenceObject(DeviceExtension->DiffFileObject);
        DeviceExtension->DiffDeviceObject = NULL;
        DeviceExtension->DiffFileObject = NULL;
    }

    if (DeviceExtension->DiffDeviceHandle != NULL)
    {
        IO_STATUS_BLOCK io_status;
        ZwFlushBuffersFile(DeviceExtension->DiffDeviceHandle, &io_status);
        ZwClose(DeviceExtension->DiffDeviceHandle);
        DeviceExtension->DiffDeviceHandle = NULL;
    }
}

NTSTATUS
AIMWrFltrGetDiffDevicePath(IN PUNICODE_STRING MountDevName,
    OUT PKEY_VALUE_PARTIAL_INFORMATION DiffDevicePath,
    IN ULONG DiffDevicePathSize)
{
    PAGED_CODE();

    KdPrint(("AIMWrFltrGetDiffDevicePath: Device assigned link '%wZ'\n",
        MountDevName));

    ULONG length;
    NTSTATUS status = ZwQueryValueKey(AIMWrFltrParametersKey, MountDevName,
        KeyValuePartialInformation, DiffDevicePath, DiffDevicePathSize, &length);

    if (!NT_SUCCESS(status))
    {
        KdPrint(("AIMWrFltrGetDiffDevicePath: Cannot query registry settings for device '%wZ' status 0x%X.\n",
            MountDevName, status));

        return status;
    }

    status = ZwDeleteValueKey(AIMWrFltrParametersKey, MountDevName);

    if (!NT_SUCCESS(status))
    {
        DbgPrint(
            "AIMWrFltrGetDiffDevicePath: Warning: Failed removing registry value for device '%wZ': 0x%#X\n",
            MountDevName, status);
    }

    if (DiffDevicePath->DataLength < 4)
    {
        DbgPrint("AIMWrFltrGetDiffDevicePath: Empty diff device for volume '%wZ'.\n",
            MountDevName);

        return STATUS_DEVICE_CONFIGURATION_ERROR;
    }

    KdPrint(("AIMWrFltrGetDiffDevicePath: Setting diff device for volume '%wZ' to '%ws'.\n",
        MountDevName, (PCWSTR)DiffDevicePath->Data));

    return STATUS_SUCCESS;
}

NTSTATUS
AIMWrFltrSynchronousDeviceControl(
    IN PDEVICE_OBJECT DeviceObject,
    IN PFILE_OBJECT FileObject,
    IN UCHAR MajorFunction,
    IN ULONG IoControlCode,
    IN OUT PVOID SystemBuffer,
    IN ULONG InputBufferLength,
    IN ULONG OutputBufferLength,
    OUT PIO_STATUS_BLOCK IoStatus)
{
    if (DeviceObject == NULL)
    {
        DeviceObject = IoGetRelatedDeviceObject(FileObject);
    }

    PIRP ioctl_irp = IoAllocateIrp(
        DeviceObject->StackSize,
        FALSE);

    if (ioctl_irp == NULL)
    {
        KdBreakPoint();

        return STATUS_INSUFFICIENT_RESOURCES;
    }

    ioctl_irp->AssociatedIrp.SystemBuffer = SystemBuffer;

    PIO_STACK_LOCATION ioctl_stack = IoGetNextIrpStackLocation(ioctl_irp);

    ioctl_stack->MajorFunction = MajorFunction;
    ioctl_stack->FileObject = FileObject;

    ioctl_stack->Parameters.DeviceIoControl.InputBufferLength =
        InputBufferLength;
    ioctl_stack->Parameters.DeviceIoControl.OutputBufferLength =
        OutputBufferLength;
    ioctl_stack->Parameters.DeviceIoControl.IoControlCode =
        IoControlCode;

    KEVENT event;
    KeInitializeEvent(&event, NotificationEvent, FALSE);

    IoSetCompletionRoutine(ioctl_irp, AIMWrFltrSynchronousIrpCompletion,
        &event, TRUE, TRUE, TRUE);

    NTSTATUS status = IoCallDriver(DeviceObject, ioctl_irp);

    if (status == STATUS_PENDING)
    {
        KeWaitForSingleObject(&event, Executive, KernelMode, FALSE, NULL);
    }

    status = ioctl_irp->IoStatus.Status;

    if (IoStatus != NULL)
    {
        *IoStatus = ioctl_irp->IoStatus;
    }

    AIMWrFltrFreeIrpWithMdls(ioctl_irp);

    return status;
}

NTSTATUS
AIMWrFltrSynchronousReadWrite(
    IN PDEVICE_OBJECT DeviceObject,
    IN PFILE_OBJECT FileObject,
    IN UCHAR MajorFunction,
    IN OUT PVOID SystemBuffer,
    IN ULONG BufferLength,
    IN PLARGE_INTEGER StartingOffset,
    IN PETHREAD Thread,
    OUT PIO_STATUS_BLOCK IoStatus)
{
    if (DeviceObject == NULL)
    {
        DeviceObject = IoGetRelatedDeviceObject(FileObject);
    }

    PIRP lower_irp = IoBuildAsynchronousFsdRequest(
        MajorFunction,
        DeviceObject,
        SystemBuffer,
        BufferLength,
        StartingOffset,
        IoStatus);

    if (lower_irp == NULL)
    {
        KdBreakPoint();

        return STATUS_INSUFFICIENT_RESOURCES;
    }

    PIO_STACK_LOCATION lower_io_stack = IoGetNextIrpStackLocation(lower_irp);
    
    lower_io_stack->FileObject = FileObject;

    lower_irp->Tail.Overlay.Thread = Thread;

    if (MajorFunction == IRP_MJ_WRITE)
    {
        if (FileObject == NULL || (FileObject->Flags & FO_NO_INTERMEDIATE_BUFFERING) != 0)
        {
            lower_irp->Flags |= IRP_WRITE_OPERATION | IRP_NOCACHE;
            lower_io_stack->Flags |= SL_WRITE_THROUGH;
        }
        else
        {
            lower_irp->Flags |= IRP_WRITE_OPERATION;
        }
    }
    else if (MajorFunction == IRP_MJ_READ)
    {
        if (FileObject == NULL || (FileObject->Flags & FO_NO_INTERMEDIATE_BUFFERING) != 0)
        {
            lower_irp->Flags |= IRP_READ_OPERATION | IRP_NOCACHE;
        }
        else
        {
            lower_irp->Flags |= IRP_READ_OPERATION;
        }
    }

    KEVENT event;
    KeInitializeEvent(&event, NotificationEvent, FALSE);

    IoSetCompletionRoutine(lower_irp, AIMWrFltrSynchronousIrpCompletion,
        &event, TRUE, TRUE, TRUE);

    NTSTATUS status = IoCallDriver(DeviceObject, lower_irp);

    if (status == STATUS_PENDING)
    {
        KeWaitForSingleObject(&event, Executive, KernelMode, FALSE, NULL);
    }

    status = lower_irp->IoStatus.Status;

    if (IoStatus != NULL)
    {
        *IoStatus = lower_irp->IoStatus;
    }

    AIMWrFltrFreeIrpWithMdls(lower_irp);

#ifdef DBG
    if (!NT_SUCCESS(status) && MajorFunction == IRP_MJ_WRITE)
    {
        DbgPrint("AIMWrFltrSynchronousReadWrite: Failed 0x%X\n", status);
    }
#endif

    return status;
}


NTSTATUS
AIMWrFltrReadDiffDeviceVbr(IN PDEVICE_EXTENSION DeviceExtension)
{
    // Read diff volume VBR
    if (*(PULONGLONG)DeviceExtension->Statistics.DiffDeviceVbr.Raw.Bytes == 0)
    {
        LARGE_INTEGER lower_offset = { 0 };

        IO_STATUS_BLOCK io_status;

        NTSTATUS status = AIMWrFltrSynchronousReadWrite(
            DeviceExtension->DiffDeviceObject,
            DeviceExtension->DiffFileObject,
            IRP_MJ_READ,
            DeviceExtension->Statistics.DiffDeviceVbr.Raw.Bytes,
            sizeof(DeviceExtension->Statistics.DiffDeviceVbr),
            &lower_offset,
            NULL,
            &io_status);

        if (!NT_SUCCESS(status) && status != STATUS_END_OF_FILE)
        {
            DbgPrint("AIMWrFltrReadDiffDeviceVbr: Error reading diff device for %p: 0x%X\n",
                DeviceExtension->DeviceObject, status);

            KdBreakPoint();

            DeviceExtension->Statistics.LastErrorCode = status;

            return status;
        }

        if (io_status.Information !=
            sizeof(DeviceExtension->Statistics.DiffDeviceVbr))
        {
            RtlZeroMemory(
                DeviceExtension->Statistics.DiffDeviceVbr.Raw.Bytes +
                io_status.Information,
                sizeof(DeviceExtension->Statistics.DiffDeviceVbr) -
                io_status.Information);
        }

        // If this is the wrong file type = VBR magic mismatch
        if (RtlCompareMemoryUlong(
            DeviceExtension->Statistics.DiffDeviceVbr.Raw.Bytes,
            sizeof(DeviceExtension->Statistics.DiffDeviceVbr), 0) !=
            sizeof(DeviceExtension->Statistics.DiffDeviceVbr) &&
            ((DeviceExtension->Statistics.DiffDeviceVbr.Fields.Foot.
                VbrSignature != vbr_signature) ||
                !RtlEqualMemory(diff_file_magic,
                    DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
                    Magic, sizeof(diff_file_magic)) ||
                DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
                DiffBlockBits != DIFF_BLOCK_BITS))
        {
            DbgPrint("AIMWrFltrReadDiffDeviceVbr: Diff device VBR for %p is invalid.\n",
                DeviceExtension->DeviceObject);

            KdBreakPoint();

            DeviceExtension->Statistics.LastErrorCode = STATUS_WRONG_VOLUME;

            if (DeviceExtension->DiffDeviceHandle != NULL)
            {
                ZwClose(DeviceExtension->DiffDeviceHandle);
                DeviceExtension->DiffDeviceHandle = NULL;
            }

            ObDereferenceObject(DeviceExtension->DiffFileObject);
            DeviceExtension->DiffFileObject = NULL;
            DeviceExtension->DiffDeviceObject = NULL;

            return STATUS_WRONG_VOLUME;
        }

        if (DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
            MajorVersion != major_version)
        {
            DbgPrint("AIMWrFltrReadDiffDeviceVbr: Overwriting incompatible version. Found in VBR %i:%i, expected %i:%i.\n",
                DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
                MajorVersion,
                DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
                MinorVersion,
                major_version,
                minor_version);

            RtlZeroMemory(DeviceExtension->Statistics.DiffDeviceVbr.Raw.Bytes,
                sizeof(DeviceExtension->Statistics.DiffDeviceVbr));
        }
        else if (DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
            MinorVersion != minor_version)
        {
            DbgPrint("AIMWrFltrReadDiffDeviceVbr: Minor version mismatch. Found in VBR %i:%i, expected %i:%i.\n",
                DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
                MajorVersion,
                DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
                MinorVersion,
                major_version,
                minor_version);
        }

        RtlCopyMemory(DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
            Magic, diff_file_magic, sizeof(diff_file_magic));

        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Foot.VbrSignature =
            vbr_signature;

        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.MajorVersion =
            major_version;

        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.DiffBlockBits =
            DIFF_BLOCK_BITS;

        if (DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
            OffsetToAllocationTable == 0)
        {
            DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
                OffsetToAllocationTable = DIFF_BLOCK_SIZE;
        }
    }

    return STATUS_SUCCESS;
}


NTSTATUS
AIMWrFltrOpenDiffDevice(IN PDEVICE_EXTENSION DeviceExtension,
    IN PUNICODE_STRING DiffDevicePath)
{
    NTSTATUS status;

    // Open diff device
    if (DeviceExtension->DiffFileObject == NULL)
    {
        KdPrint((
            "AIMWrFltrOpenDiffDevice: Attempting to open device '%wZ' as diff device for %p.\n",
            DiffDevicePath, DeviceExtension->DeviceObject));

        OBJECT_ATTRIBUTES obj_attrs;
        InitializeObjectAttributes(&obj_attrs,
            DiffDevicePath,
            OBJ_CASE_INSENSITIVE | OBJ_OPENIF,
            NULL,
            NULL);

        LARGE_INTEGER allocation_size;
        allocation_size.QuadPart = DIFF_BLOCK_SIZE;

        IO_STATUS_BLOCK io_status;

        // Open diff file and make sure the file system where it is
        // located is or gets mounted

        status = ZwCreateFile(
            &DeviceExtension->DiffDeviceHandle,
            GENERIC_READ | GENERIC_WRITE | DELETE,
            &obj_attrs,
            &io_status,
            &allocation_size,
            FILE_ATTRIBUTE_NORMAL,
            FILE_SHARE_READ | FILE_SHARE_DELETE,
            FILE_OPEN_IF,
            FILE_NON_DIRECTORY_FILE | FILE_SYNCHRONOUS_IO_NONALERT |
            FILE_NO_INTERMEDIATE_BUFFERING | FILE_RANDOM_ACCESS,
            NULL,
            0);

        PFILE_OBJECT file_object = NULL;

        if (NT_SUCCESS(status))
        {
            status = ObReferenceObjectByHandle(
                DeviceExtension->DiffDeviceHandle,
                SYNCHRONIZE | FILE_READ_ATTRIBUTES | FILE_READ_DATA |
                FILE_WRITE_DATA | FILE_WRITE_ATTRIBUTES,
                *IoFileObjectType,
                KernelMode,
                (PVOID*)&file_object,
                NULL);
        }

        if (!NT_SUCCESS(status))
        {
            DbgPrint(
                "AIMWrFltrOpenDiffDevice: Open failed for device '%wZ' status 0x%X.\n",
                DiffDevicePath,
                status);

            KdBreakPoint();

            DeviceExtension->Statistics.LastErrorCode = status;

            return status;
        }

        __analysis_assume(file_object != NULL);

        DbgPrint(
            "AIMWrFltrOpenDiffDevice: Successfully opened device '%wZ' as diff device.\n",
            DiffDevicePath);

        KdPrint((
            "AIMWrFltrOpenDiffDevice: Successfully opened device diff device for %p. Diff volume FS driver '%wZ'.\n",
            DeviceExtension->DeviceObject,
            &(file_object->Vpb != NULL && file_object->Vpb->DeviceObject != NULL ?
                file_object->Vpb->DeviceObject : file_object->DeviceObject)->DriverObject->DriverName));

        // Save fs stack device and file objects for diff device
        DeviceExtension->DiffDeviceObject = IoGetRelatedDeviceObject(file_object);
        DeviceExtension->DiffFileObject = file_object;

        KdPrint((
            "AIMWrFltrInitializeDiffDevice: Diff device driver: '%wZ'\n",
            &DeviceExtension->DiffFileObject->DeviceObject->DriverObject->DriverName));
    }

    return AIMWrFltrReadDiffDeviceVbr(DeviceExtension);
}

NTSTATUS
AIMWrFltrInitializePhDskMntDiffDevice(IN PDEVICE_EXTENSION DeviceExtension,
    IN HANDLE DiffDeviceHandle,
    IN PLARGE_INTEGER DiskSize)
{
    NTSTATUS status;

    // Open diff device
    if (DeviceExtension->DiffFileObject == NULL)
    {
        KdPrint((
            "AIMWrFltrInitializePhDskMntDiffDevice: Attempting to duplicate handle %p as diff device for %p.\n",
            DiffDeviceHandle, DeviceExtension->DeviceObject));

        // Open diff file and make sure the file system where it is
        // located is or gets mounted

        status = ZwDuplicateObject(
            NtCurrentProcess(),
            DiffDeviceHandle,
            NtCurrentProcess(),
            &DeviceExtension->DiffDeviceHandle,
            0,
            0,
            DUPLICATE_SAME_ATTRIBUTES |
            DUPLICATE_SAME_ACCESS);

        PFILE_OBJECT file_object = NULL;

        if (NT_SUCCESS(status))
        {
            status = ObReferenceObjectByHandle(
                DeviceExtension->DiffDeviceHandle,
                SYNCHRONIZE | FILE_READ_ATTRIBUTES | FILE_READ_DATA |
                FILE_WRITE_DATA | FILE_WRITE_ATTRIBUTES,
                *IoFileObjectType,
                KernelMode,
                (PVOID*)&file_object,
                NULL);
        }

        if (!NT_SUCCESS(status))
        {
            DbgPrint(
                "AIMWrFltrInitializePhDskMntDiffDevice: Duplicate failed for handle %p status 0x%X.\n",
                DiffDeviceHandle,
                status);

            KdBreakPoint();

            DeviceExtension->Statistics.LastErrorCode = status;

            return status;
        }

        __analysis_assume(file_object != NULL);

        DbgPrint(
            "AIMWrFltrInitializePhDskMntDiffDevice: Successfully duplicated handle %p as diff device.\n",
            DiffDeviceHandle);

        KdPrint((
            "AIMWrFltrInitializePhDskMntDiffDevice: Successfully opened device diff device for %p. Diff volume FS driver '%wZ'.\n",
            DeviceExtension->DeviceObject,
            &(file_object->Vpb != NULL && file_object->Vpb->DeviceObject != NULL ?
                file_object->Vpb->DeviceObject : file_object->DeviceObject)->DriverObject->DriverName));

        // Save fs stack device and file objects for diff device
        DeviceExtension->DiffDeviceObject = IoGetRelatedDeviceObject(file_object);
        DeviceExtension->DiffFileObject = file_object;

        KdPrint((
            "AIMWrFltrInitializePhDskMntDiffDevice: Diff device driver: '%wZ'\n",
            &DeviceExtension->DiffFileObject->DeviceObject->DriverObject->DriverName));
    }

    status = AIMWrFltrReadDiffDeviceVbr(DeviceExtension);

    if (!NT_SUCCESS(status))
    {
        return status;
    }

    if (DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart ==
        0)
    {
        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size =
            *DiskSize;
    }

    DeviceExtension->Statistics.IsProtected = TRUE;

    return AIMWrFltrInitializeDiffDeviceUnsafe(DeviceExtension);
}

NTSTATUS
AIMWrFltrInitializeDiffDeviceUnsafe(IN PDEVICE_EXTENSION DeviceExtension)
{
    NTSTATUS status;

    if (!DeviceExtension->Statistics.IsProtected)
    {
        DbgPrint("AIMWrFltrInitializeDiffDevice: Diff init called for not-protected volume. Previous calls may have failed.\n");
        return STATUS_SUCCESS;
    }

    // Query volume size
    if (DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart ==
        0)
    {
        GET_LENGTH_INFORMATION length;

        status = AIMWrFltrSynchronousDeviceControl(
            DeviceExtension->TargetDeviceObject,
            NULL,
            IRP_MJ_DEVICE_CONTROL,
            IOCTL_DISK_GET_LENGTH_INFO,
            &length,
            0,
            sizeof length);

        if (!NT_SUCCESS(status))
        {
            KdPrint(("AIMWrFltrInitializeDiffDevice: Error querying volume size: %#x.\n",
                status));

            DeviceExtension->Statistics.LastErrorCode = status;

            // No point doing more work here right now. Continue next time this
            // function is called, when underlying device is started and ready.
            return status;
        }

        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size =
            length.Length;
    }

    //KdBreakPoint();

    ULONGLONG number_of_blocks = DIFF_GET_NUMBER_OF_BLOCKS(
        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size.
        QuadPart);

    DeviceExtension->Statistics.DiffDeviceVbr.Fields.
        Head.AllocationTableBlocks = (LONG)
        DIFF_GET_NUMBER_OF_BLOCKS(sizeof(LONG) * number_of_blocks) + 1;

    DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
        SizeOfAllocationTable = (LONGLONG)DeviceExtension->Statistics.
        DiffDeviceVbr.Fields.Head.AllocationTableBlocks <<
        (DIFF_BLOCK_BITS - SECTOR_BITS);

    LONGLONG free_offset = DIFF_BLOCK_SIZE;

    if (DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
        OffsetToLogData > free_offset)
    {
        free_offset = DeviceExtension->Statistics.DiffDeviceVbr.Fields.
            Head.OffsetToLogData + DeviceExtension->Statistics.
            DiffDeviceVbr.Fields.Head.SizeOfLogData;
    }

    if (DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
        OffsetToPrivateData > free_offset)
    {
        free_offset = DeviceExtension->Statistics.DiffDeviceVbr.Fields.
            Head.OffsetToPrivateData + DeviceExtension->Statistics.
            DiffDeviceVbr.Fields.Head.SizeOfPrivateData;
    }

    free_offset <<= SECTOR_BITS;

    free_offset = (free_offset + DIFF_BLOCK_OFFSET_MASK) &
        DIFF_BLOCK_BASE_MASK;

    free_offset >>= SECTOR_BITS;

    DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
        OffsetToAllocationTable = free_offset;

    DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
        OffsetToFirstAllocatedBlock = DeviceExtension->Statistics.
        DiffDeviceVbr.Fields.Head.OffsetToAllocationTable +
        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
        SizeOfAllocationTable;

    if (DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
        LastAllocatedBlock == 0)
    {
        DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
            LastAllocatedBlock = (LONG)
            (DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
                OffsetToFirstAllocatedBlock >>
                (DIFF_BLOCK_BITS - SECTOR_BITS));
    }

    LARGE_INTEGER lower_offset = { 0 };

    IO_STATUS_BLOCK io_status;

    status = AIMWrFltrSynchronousReadWrite(
        DeviceExtension->DiffDeviceObject,
        DeviceExtension->DiffFileObject,
        IRP_MJ_WRITE,
        DeviceExtension->Statistics.DiffDeviceVbr.Raw.Bytes,
        sizeof(DeviceExtension->Statistics.DiffDeviceVbr),
        &lower_offset,
        NULL,
        &io_status);

    if (!NT_SUCCESS(status) ||
        io_status.Information !=
        sizeof(DeviceExtension->Statistics.DiffDeviceVbr))
    {
        DbgPrint("AIMWrFltrInitializeDiffDevice: Error writing diff device for %p: 0x%X\n",
            DeviceExtension->DeviceObject, status);
    }

    // Create allocation table
    if (DeviceExtension->AllocationTable == NULL)
    {
        LONG alloc_table_blocks = (LONG)
            DIFF_GET_NUMBER_OF_BLOCKS(sizeof(LONG) * number_of_blocks) + 1;

        if ((number_of_blocks + alloc_table_blocks) >= MAXLONG)
        {
            DbgPrint("AIMWrFltr: FATAL: Filtered volume is %I64u bytes which is too large. Max = %I64u.\n",
                DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.Size.QuadPart,
                ((LONGLONG)MAXLONG - 1) << DIFF_BLOCK_BITS);

            KdBreakPoint();

            status = STATUS_NOT_SUPPORTED;

            DeviceExtension->Statistics.LastErrorCode = status;

            return status;
        }

        DeviceExtension->AllocationTable = new LONG[
            (size_t)alloc_table_blocks << DIFF_BLOCK_BITS];

        if (DeviceExtension->AllocationTable == NULL)
        {
            DbgPrint(
                "AIMWrFltrInitializeDiffDevice: Memory allocation error.\n");

            KdBreakPoint();

            status = STATUS_INSUFFICIENT_RESOURCES;

            DeviceExtension->Statistics.LastErrorCode = status;

            return status;
        }

        lower_offset.QuadPart =
            DeviceExtension->Statistics.DiffDeviceVbr.Fields.Head.
            OffsetToAllocationTable;

        status = AIMWrFltrSynchronousReadWrite(
            DeviceExtension->DiffDeviceObject,
            DeviceExtension->DiffFileObject,
            IRP_MJ_READ,
            (PVOID)DeviceExtension->AllocationTable,
            (ULONG)alloc_table_blocks << DIFF_BLOCK_BITS,
            &lower_offset,
            NULL,
            &io_status);

        if (!NT_SUCCESS(status) && status != STATUS_END_OF_FILE)
        {
            DbgPrint(
                "AIMWrFltrInitializeDiffDevice: Error reading diff device for %p: 0x%X\n",
                DeviceExtension->DeviceObject, status);

            KdBreakPoint();

            DeviceExtension->Statistics.LastErrorCode = status;

            return status;
        }

        if (io_status.Information !=
            ((ULONG_PTR)alloc_table_blocks << DIFF_BLOCK_BITS))
        {
            RtlZeroMemory((PUCHAR)DeviceExtension->AllocationTable +
                io_status.Information,
                ((ULONG_PTR)alloc_table_blocks << DIFF_BLOCK_BITS) -
                io_status.Information);
        }
    }

    DeviceExtension->Statistics.Initialized = TRUE;

    status = STATUS_SUCCESS;

    DeviceExtension->Statistics.LastErrorCode = status;

    return status;
}

NTSTATUS
AIMWrFltrInitializeDiffDevice(IN PDEVICE_EXTENSION DeviceExtension)
{
    KeAcquireGuardedMutex(&DeviceExtension->InitializationMutex);

    //static LARGE_INTEGER wait_timeout = { 0 };

    NTSTATUS status = KeWaitForSingleObject(&DeviceExtension->InitializationEvent,
        Executive, KernelMode, FALSE, NULL);

    KeReleaseGuardedMutex(&DeviceExtension->InitializationMutex);

    if (status == STATUS_SUCCESS)
    {
        KdPrint(("AIMWrFltr:InitializeDiffDevice: Acquired Mutex, calling init function.\n"));

        status = AIMWrFltrInitializeDiffDeviceUnsafe(DeviceExtension);

        KeSetEvent(&DeviceExtension->InitializationEvent, 0, FALSE);
    }
    else
    {
        KdPrint(("AIMWrFltr:InitializeDiffDevice: Error acquiring Mutex, status = 0x%X.\n",
            status));
    }

    return status;
}

NTSTATUS
#pragma warning(suppress: 28152)
AIMWrFltrAddDevice(IN PDRIVER_OBJECT DriverObject,
    IN PDEVICE_OBJECT PhysicalDeviceObject)
    /*++
    Routine Description:

    Creates and initializes a new filter device object FiDO for the
    corresponding PDO.  Then it attaches the device object to the device
    stack of the drivers for the device.

    Arguments:

    DriverObject - Disk filter driver object.
    PhysicalDeviceObject - Physical Device Object from the underlying layered driver

    Return Value:

    NTSTATUS
    --*/
{
    PAGED_CODE();

    //if (PhysicalDeviceObject->Characteristics & FILE_READ_ONLY_DEVICE)
    //{
    //    KdPrint(("AIMWrFltrAddDevice: DeviceObject 0x%p is read-only. Ignored.\n",
    //        PhysicalDeviceObject));

    //    return STATUS_SUCCESS;
    //}

    NTSTATUS status;

    ULONG req_length;

#if DBG
    {
        LONG bus_count = 0;

        WNonPagedPoolMem<WCHAR> ph_obj_name(65536);

        if (!ph_obj_name)
        {
            KdBreakPoint();
            return STATUS_INSUFFICIENT_RESOURCES;
        }

        status = IoGetDeviceProperty(PhysicalDeviceObject,
            DevicePropertyEnumeratorName, (ULONG)(ph_obj_name.GetSize() - 2 * sizeof(WCHAR)),
            ph_obj_name, &req_length);

        if (!NT_SUCCESS(status))
        {
            DbgPrint("AIMWrFltrAddDevice Error getting enumerator name for device: %#x\n",
                status);

            ph_obj_name[0] = 0;
        }

#pragma warning(suppress: 28719)
        wcscat(ph_obj_name, L"\\");

        status = IoGetDeviceProperty(PhysicalDeviceObject,
            DevicePropertyClassName, (ULONG)(ph_obj_name.GetSize() -
                sizeof(WCHAR) * (wcslen(ph_obj_name) + 2)),
                (PWSTR)ph_obj_name + wcslen(ph_obj_name), &req_length);

        if (!NT_SUCCESS(status))
        {
            DbgPrint("AIMWrFltrAddDevice Error getting class name for '%ws' device: %#x\n",
                (PWSTR)ph_obj_name, status);
        }

        DbgPrint("AIMWrFltrAddDevice for Enum\\Class\\Number: '%ws\\%i'\n",
            (PWSTR)ph_obj_name, bus_count);
    }
#endif

    WNonPagedPoolMem<OBJECT_NAME_INFORMATION> obj_name_info(1024);

    if (!obj_name_info)
    {
        KdBreakPoint();
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    UNICODE_STRING phdskmnt_driver_name;
    RtlInitUnicodeString(&phdskmnt_driver_name, L"\\Driver\\phdskmnt");

    //KdBreakPoint();

    status = ObQueryNameString(PhysicalDeviceObject, obj_name_info,
        (ULONG)obj_name_info.GetSize(), &req_length);

    if (!NT_SUCCESS(status))
    {
        DbgPrint("AIMWrFltrAddDevice: ObQueryNameString failed for PhysicalDeviceObject: %#x.\n", status);
        return status;
    }

    //
    // Get registry value for this device name
    //

    WPagedPoolMem<UCHAR> buffer(200000);

    if (!buffer)
    {
        KdBreakPoint();
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    UNICODE_STRING diff_device_path;
    PSRB_IMSCSI_CREATE_DATA create_data = NULL;
    HANDLE diff_device_handle = NULL;

    // Query SCSI address
#pragma warning(suppress: 28175)
    if (RtlEqualUnicodeString(&PhysicalDeviceObject->DriverObject->DriverName,
        &phdskmnt_driver_name, FALSE))
    {
        SCSI_ADDRESS scsi_address;

        status = AIMWrFltrSynchronousDeviceControl(
            PhysicalDeviceObject,
            NULL,
            IRP_MJ_DEVICE_CONTROL,
            IOCTL_SCSI_GET_ADDRESS,
            &scsi_address,
            0,
            sizeof scsi_address);

        if (!NT_SUCCESS(status))
        {
            KdPrint(("AIMWrFltrAddDevice: Error querying SCSI address: %#x.\n",
                status));

            return status;
        }

        buffer.Clear();

        create_data = (PSRB_IMSCSI_CREATE_DATA)(PVOID)buffer;

        ImScsiInitializeSrbIoBlock(&create_data->SrbIoControl,
            (ULONG)buffer.GetSize(), SMP_IMSCSI_QUERY_DEVICE, 0);

        create_data->Fields.DeviceNumber.PathId = scsi_address.PathId;
        create_data->Fields.DeviceNumber.TargetId = scsi_address.TargetId;
        create_data->Fields.DeviceNumber.Lun = scsi_address.Lun;

        IO_STATUS_BLOCK io_status;

        status = AIMWrFltrSynchronousDeviceControl(
            PhysicalDeviceObject,
            NULL,
            IRP_MJ_DEVICE_CONTROL,
            IOCTL_SCSI_MINIPORT,
            buffer,
            (ULONG)buffer.GetSize(),
            (ULONG)buffer.GetSize(),
            &io_status);

        if (!NT_SUCCESS(status))
        {
            KdPrint(("AIMWrFltrAddDevice: Error querying SCSI device: %#x.\n",
                status));

            return status;
        }

        if (io_status.Information < sizeof(SRB_IMSCSI_CREATE_DATA) ||
            (!IMSCSI_WRITE_OVERLAY(create_data->Fields.Flags)) ||
            io_status.Information < (ULONG_PTR)
            FIELD_OFFSET(SRB_IMSCSI_CREATE_DATA, Fields.FileName) +
            create_data->Fields.FileNameLength +
            create_data->Fields.WriteOverlayFileNameLength +
            sizeof(HANDLE))
        {
            DbgPrint("AIMWrFltrAddDevice: This AIM device is not configured with write overlay mode.\n");
            return STATUS_INVALID_DEVICE_REQUEST;
        }

        diff_device_path.Buffer =
            (PWCHAR)(((PUCHAR)create_data->Fields.FileName) +
                create_data->Fields.FileNameLength);
        diff_device_path.MaximumLength = diff_device_path.Length =
            create_data->Fields.WriteOverlayFileNameLength;

        diff_device_handle = *(PHANDLE)(((PUCHAR)create_data->Fields.FileName) +
            create_data->Fields.FileNameLength +
            create_data->Fields.WriteOverlayFileNameLength);

        DbgPrint("AIMWrFltrAddDevice: Configuring diff device '%wZ' handle %p for attached device '%wZ'.\n",
            &diff_device_path,
            diff_device_handle,
            &obj_name_info->Name);

#if DBG

        DbgPrint("AIMWrFltr: Device stack for %p:\n", PhysicalDeviceObject);
        for (PDEVICE_OBJECT devobj = PhysicalDeviceObject; devobj != NULL; devobj = devobj->AttachedDevice)
        {
            DbgPrint("%p %wZ\n", devobj, &devobj->DriverObject->DriverName);
        }

        //DbgBreakPoint();

#endif
    }
    else
    {
        PKEY_VALUE_PARTIAL_INFORMATION diff_device_reg_value = (PKEY_VALUE_PARTIAL_INFORMATION)(PVOID)buffer;

        status = AIMWrFltrGetDiffDevicePath(&obj_name_info->Name,
            diff_device_reg_value, (ULONG)buffer.GetSize());

        if (!NT_SUCCESS(status))
        {
            DbgPrint("AIMWrFltrAddDevice: No diff device configured for attached device '%wZ': %#x.\n",
                &obj_name_info->Name,
                status);

            return status;
        }

        *(PWCHAR)&diff_device_reg_value->Data[diff_device_reg_value->DataLength - sizeof(WCHAR)] = 0;

        RtlInitUnicodeString(&diff_device_path, (PCWSTR)diff_device_reg_value->Data);

        DbgPrint("AIMWrFltrAddDevice: Configuring diff device '%wZ' for attached device '%wZ'.\n",
            &diff_device_path,
            &obj_name_info->Name);
    }

    //
    // Create a filter device object for this device (volume).
    //

    PDEVICE_OBJECT filter_device_object;
    
    status = IoCreateDevice(DriverObject,
        sizeof(DEVICE_EXTENSION),
        NULL,
        FILE_DEVICE_DISK,
        FILE_DEVICE_SECURE_OPEN,
        FALSE, &filter_device_object);

    if (!NT_SUCCESS(status))
    {
        DbgPrint(
            "AIMWrFltrAddDevice: Cannot create filter_device_object. Status 0x%X\n",
            status);

        KdBreakPoint();

        return status;
    }

    PDEVICE_EXTENSION device_extension =
        (PDEVICE_EXTENSION)filter_device_object->DeviceExtension;

    RtlZeroMemory(device_extension, sizeof(DEVICE_EXTENSION));

    device_extension->Statistics.Version = sizeof(AIMWRFLTR_DEVICE_STATISTICS);

    //
    // Initialize the remove lock
    //
    IoInitializeRemoveLock(&device_extension->RemoveLock, LOCK_TAG, 1, 0);

    KeInitializeEvent(&device_extension->PagingPathCountEvent,
        SynchronizationEvent, TRUE);
    KeInitializeGuardedMutex(&device_extension->PagingPathCountMutex);

    KeInitializeSpinLock(&device_extension->ListLock);
    InitializeListHead(&device_extension->ListHead);
    KeInitializeEvent(&device_extension->ListEvent, SynchronizationEvent,
        FALSE);

    KeInitializeEvent(&device_extension->InitializationEvent,
        SynchronizationEvent, TRUE);
    KeInitializeGuardedMutex(&device_extension->InitializationMutex);

    //
    // Save the filter device object in the device extension
    //
    device_extension->DeviceObject = filter_device_object;

    //
    // Attaches the device object to the highest device object in the chain
    // and return the previously highest device object, which is passed to
    // IoCallDriver when pass IRPs down the device stack
    //
    device_extension->PhysicalDeviceObject = PhysicalDeviceObject;

    if (diff_device_handle != NULL && create_data != NULL)
    {
        device_extension->Statistics.IsProtected = FALSE;

        status = AIMWrFltrInitializePhDskMntDiffDevice(device_extension,
            diff_device_handle, &create_data->Fields.DiskSize);

        buffer.Free();

        if (!NT_SUCCESS(status))
        {
            DbgPrint("Diff device initialization failed for device '%wZ': %#x\n",
                &obj_name_info->Name, status);
        }
    }
    else
    {
        UNICODE_STRING non_removable_suffix;
        RtlInitUnicodeString(&non_removable_suffix, L":$NonRemovable");

        if (diff_device_path.Length > non_removable_suffix.Length)
        {
            UNICODE_STRING suffix;
            suffix.MaximumLength = suffix.Length = non_removable_suffix.Length;
            suffix.Buffer = (PWSTR)((PUCHAR)diff_device_path.Buffer + diff_device_path.Length - suffix.Length);

            if (RtlEqualUnicodeString(&suffix, &non_removable_suffix, FALSE))
            {
                KdPrint(("aimwrfltr: Request to fake non-removable properties.\n"));
                diff_device_path.Length -= suffix.Length;
                device_extension->Statistics.FakeNonRemovable = TRUE;
            }
        }

        status = AIMWrFltrOpenDiffDevice(device_extension, &diff_device_path);

        buffer.Free();

        if (NT_SUCCESS(status))
        {
            device_extension->Statistics.IsProtected = TRUE;
        }
        else
        {
            DbgPrint("Diff device open failed for device '%wZ': %#x\n",
                &obj_name_info->Name, status);

            device_extension->Statistics.IsProtected = FALSE;
        }
    }

    device_extension->TargetDeviceObject =
        IoAttachDeviceToDeviceStack(filter_device_object,
            PhysicalDeviceObject);

    if (device_extension->TargetDeviceObject == NULL)
    {
        AIMWrFltrCleanupDevice(device_extension);
        IoDeleteDevice(filter_device_object);

        KdPrint((
            "AIMWrFltrAddDevice: Unable to attach 0x%p to target 0x%p\n",
            filter_device_object, PhysicalDeviceObject));

        KdBreakPoint();

        return STATUS_DEVICE_CONFIGURATION_ERROR;
    }

    if (device_extension->TargetDeviceObject->Flags & DO_DIRECT_IO)
    {
        filter_device_object->Flags |= DO_DIRECT_IO;
    }
    else
    {
        filter_device_object->Flags |= DO_BUFFERED_IO;
    }

    // Try to initialize diff device. This will most likely fail at
    // this point because it is not ready to receive requests yet,
    // so ignore errors and resume that later at some other request.
    //(void)AIMWrFltrInitializeDiffDeviceUnsafe(device_extension);

    KdPrint((
        "AIMWrFltrAddDevice: Attached to device '%wZ' above driver '%wZ'. Filter device flags: %#x Target device flags: %#x Physical device flags: %#x\n",
        &obj_name_info->Name,
        &device_extension->TargetDeviceObject->DriverObject->DriverName,
        filter_device_object->Flags,
        device_extension->TargetDeviceObject->Flags,
        PhysicalDeviceObject->Flags));

    obj_name_info.Free();

    if (device_extension->Statistics.IsProtected)
    {
        status = PsCreateSystemThread(&device_extension->WorkerThread,
            (ACCESS_MASK)0L,
            NULL,
            NULL,
            NULL,
            AIMWrFltrDeviceWorkerThread,
            device_extension);

        if (!NT_SUCCESS(status))
        {
            device_extension->WorkerThread = NULL;
            IoDetachDevice(device_extension->TargetDeviceObject);
            AIMWrFltrCleanupDevice(device_extension);
            IoDeleteDevice(filter_device_object);

            KdBreakPoint();

            return status;
        }
    }

    //
    // default to DO_POWER_PAGABLE
    //

    filter_device_object->Flags |= DO_POWER_PAGABLE;

    //
    // Clear the DO_DEVICE_INITIALIZING flag
    //

    filter_device_object->Flags &= ~DO_DEVICE_INITIALIZING;

    return STATUS_SUCCESS;

}				// end AIMWrFltrAddDevice()


NTSTATUS
AIMWrFltrPnp(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
/*++

Routine Description:

Dispatch for PNP

Arguments:

DeviceObject    - Supplies the device object.

Irp             - Supplies the I/O request packet.

Return Value:

NTSTATUS

--*/
{
    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);
    NTSTATUS status;
    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;
    bool lockHeld = false;

#ifndef DBG
    PAGED_CODE();
#endif

    //KdPrint(("AIMWrFltrPnp: DeviceObject 0x%p Irp 0x%p\n",
    //    DeviceObject, Irp));

    //
    // Acquire the remove lock. If this fails, fail the I/O.
    //

    status = IoAcquireRemoveLock(&device_extension->RemoveLock, Irp);

    if (!NT_SUCCESS(status))
    {

        DbgPrint(
            "IoAcquireRemoveLock failed: DeviceObject %p PNP Irp type [%#02x] Status: 0x%X.\n",
            DeviceObject, io_stack->MinorFunction, status);
        Irp->IoStatus.Status = status;
        IoCompleteRequest(Irp, IO_NO_INCREMENT);

        KdBreakPoint();

        return status;
    }

    //
    // Indicate that the remove lock is held.
    //

    lockHeld = true;

    switch (io_stack->MinorFunction)
    {

    case IRP_MN_START_DEVICE:
        //
        // Call the Start Routine handler to schedule a completion routine
        //
        KdPrint((
            "AIMWrFltrPnp: Schedule completion for START_DEVICE\n"));
        status = AIMWrFltrStartDevice(DeviceObject, Irp);
        break;

    case IRP_MN_REMOVE_DEVICE:
    {
        //
        // In this case a completion routine is not required
        // Free resources, pass the IRP down to the next driver
        // Detach and Delete the device. 
        //
        KdPrint(("AIMWrFltrPnp: Processing REMOVE_DEVICE\n"));
        status = AIMWrFltrRemoveDevice(DeviceObject, Irp);

        //
        // Remove locked released by FpFilterRemoveDevice
        //
        lockHeld = false;

        break;
    }
    case IRP_MN_DEVICE_USAGE_NOTIFICATION:
    {
        KdPrint(("AIMWrFltrPnp: Processing DEVICE_USAGE_NOTIFICATION\n"));

        status = AIMWrFltrDeviceUsageNotification(DeviceObject, Irp);

        break;
    }
    case IRP_MN_QUERY_REMOVE_DEVICE:
    {
        KdPrint(("AIMWrFltrPnp: Processing IRP_MN_QUERY_REMOVE_DEVICE\n"));

        device_extension->QueryRemoveDeviceSent = true;

        status = AIMWrFltrSendToNextDriver(DeviceObject, Irp);

        break;
    }
    case IRP_MN_SURPRISE_REMOVAL:
    {
#if DBG

        KIRQL current_irql = PASSIVE_LEVEL;
        KLOCK_QUEUE_HANDLE lock_handle;
        ULONG items_in_queue = 0;

        AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
            current_irql);

        for (PLIST_ENTRY entry = device_extension->ListHead.Flink;
            entry != &device_extension->ListHead;
            entry = entry->Flink)
        {
            items_in_queue++;
        }

        AIMWrFltrReleaseLock(&lock_handle, &current_irql);

        DbgPrint("AIMWrFltrPnp: Processing IRP_MN_SURPRISE_REMOVAL with %i items in queue\n",
            items_in_queue);

#endif

        device_extension->SurpriseRemoveDeviceSent = true;
        device_extension->ShutdownThread = true;

        status = AIMWrFltrSendToNextDriver(DeviceObject, Irp);

        break;
    }
    case IRP_MN_CANCEL_REMOVE_DEVICE:
    {
        KdPrint(("AIMWrFltrPnp: Processing IRP_MN_CANCEL_REMOVE_DEVICE\n"));

        device_extension->CancelRemoveDeviceSent = true;

        status = AIMWrFltrSendToNextDriver(DeviceObject, Irp);

        break;
    }
    default:
        //KdPrint(("AIMWrFltrPnp: Forwarding irp\n"));

        //
        // Simply forward all other Irps
        //

        status = AIMWrFltrSendToNextDriver(DeviceObject, Irp);

    }


    //
    // If the lock is still held, release it now.
    //

    if (lockHeld)
    {

        //DebugPrint((2,
        //    "AIMWrFltrPnp : Releasing Lock: DeviceObject 0x%p Irp 0x%p\n",
        //    DeviceObject, Irp));
        //
        // Release the remove lock
        //
        IoReleaseRemoveLock(&device_extension->RemoveLock, Irp);
    }

    return status;


}				// end AIMWrFltrPnp()


NTSTATUS
AIMWrFltrSynchronousIrpCompletion(_In_ PDEVICE_OBJECT DeviceObject,
    _In_ PIRP Irp,
    _In_reads_opt_(_Inexpressible_("varies")) PVOID Context)
    /*++

    Routine Description:

    Forwarded IRP completion routine. Set an event and return
    STATUS_MORE_PROCESSING_REQUIRED. Irp forwarder will wait on this
    event and then re-complete the irp after cleaning up.

    Arguments:

    DeviceObject is the device object of the WMI driver
    Irp is the WMI irp that was just completed
    Context is a PKEVENT that forwarder will wait on

    Return Value:

    STATUS_MORE_PORCESSING_REQUIRED

    --*/
{
    PKEVENT event = (PKEVENT)Context;

    UNREFERENCED_PARAMETER(DeviceObject);
    UNREFERENCED_PARAMETER(Irp);

    if (event != NULL)
    {
        KeSetEvent(event, IO_NO_INCREMENT, FALSE);
    }

    return STATUS_MORE_PROCESSING_REQUIRED;

}				// end AIMWrFltrSynchronousIrpCompletion()


NTSTATUS
AIMWrFltrStartDevice(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
/*++

Routine Description:

This routine is called when a Pnp Start Irp is received.

Arguments:

DeviceObject - a pointer to the device object

Irp - a pointer to the irp


Return Value:

Status of processing the Start Irp

--*/
{
    PAGED_CODE();

    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    NTSTATUS status = AIMWrFltrForwardIrpSynchronous(DeviceObject, Irp);

    AIMWrFltrSyncFilterWithTarget(DeviceObject,
        device_extension->TargetDeviceObject);

    //
    // Complete the Irp
    //
    Irp->IoStatus.Status = status;
    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return status;
}


NTSTATUS
AIMWrFltrRemoveDevice(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
/*++

Routine Description:

This routine is called when the device is to be removed.
It will pass the Irp down the stack
then detach itself from the stack before deleting itself.

Arguments:

DeviceObject - a pointer to the device object

Irp - a pointer to the irp


Return Value:

Status of removing the device

--*/
{
    PAGED_CODE();

    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    device_extension->ShutdownThread = true;

    KdPrint(("AIMWrFltr: Waiting for remove lock completion.\n"));

    //
    // Call Remove lock and wait to ensure all outstanding operations
    // have completed
    //
    IoReleaseRemoveLockAndWait(&device_extension->RemoveLock, Irp);

    //
    // Forward the Removal Irp below as per the DDK
    // We aren't required to complete this Irp status should
    // be the return status from the next driver in the stack
    //
    NTSTATUS status = AIMWrFltrSendToNextDriver(DeviceObject, Irp);

    KdPrint(("AIMWrFltr: REMOVE_DEVICE sent to lower device (status %#x), detaching from device stack.\n", status));

    //
    // Detach us from the stack 
    //
    IoDetachDevice(device_extension->TargetDeviceObject);

    KdPrint(("AIMWrFltr: Detached from device stack, cleaning up.\n"));

    AIMWrFltrCleanupDevice(device_extension);

    KdPrint(("AIMWrFltr: Deleting device object %p.\n", DeviceObject));

    IoDeleteDevice(DeviceObject);

    KdPrint(("AIMWrFltr: REMOVE_DEVICE finished (status %#x)\n", status));

    return status;
}


NTSTATUS
AIMWrFltrDeviceUsageNotification(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
{
    PAGED_CODE();

    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(Irp);

    if ((io_stack->Parameters.UsageNotification.Type !=
        DeviceUsageTypePaging) &&
        (io_stack->Parameters.UsageNotification.Type !=
            DeviceUsageTypeHibernation) &&
        (io_stack->Parameters.UsageNotification.Type !=
            DeviceUsageTypeDumpFile))
    {
        return AIMWrFltrSendToNextDriver(DeviceObject, Irp);
    }

    PDEVICE_EXTENSION device_extension =
        (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    //
    // wait on the paging path event
    //

    KeAcquireGuardedMutex(&device_extension->PagingPathCountMutex);

    KeWaitForSingleObject(
        &device_extension->PagingPathCountEvent,
        Executive, KernelMode, FALSE, NULL);

    KeReleaseGuardedMutex(&device_extension->PagingPathCountMutex);

    //
    // if removing last paging device, need to set DO_POWER_PAGABLE
    // bit here, and possible re-set it below on failure.
    //

    bool set_pagable = false;
    if (!io_stack->Parameters.UsageNotification.InPath &&
        device_extension->Statistics.PagingPathCount == 1)
    {

        //
        // removing the last paging file
        // must have DO_POWER_PAGABLE bits set
        //

        if (DeviceObject->Flags & DO_POWER_INRUSH)
        {
            KdPrint(("AIMWrFltrPnp: last paging file "
                "removed but DO_POWER_INRUSH set, so not "
                "setting PAGABLE bit "
                "for DO %p\n", DeviceObject));
        }
        else
        {
            KdPrint(("AIMWrFltrPnp: Setting  PAGABLE "
                "bit for DO %p\n", DeviceObject));
            DeviceObject->Flags |= DO_POWER_PAGABLE;
            set_pagable = true;
        }

    }

    //
    // send the irp synchronously
    //

    NTSTATUS status = AIMWrFltrForwardIrpSynchronous(DeviceObject, Irp);

    //
    // now deal with the failure and success cases.
    // note that we are not allowed to fail the irp
    // once it is sent to the lower drivers.
    //

    if (NT_SUCCESS(status))
    {

        IoAdjustPagingPathCount(&device_extension->Statistics.PagingPathCount,
            io_stack->Parameters.UsageNotification.
            InPath);

        if (io_stack->Parameters.UsageNotification.InPath)
        {
            if (device_extension->Statistics.PagingPathCount == 1)
            {

                //
                // first paging file addition
                //

                KdPrint((
                    "AIMWrFltrPnp: Clearing PAGABLE bit "
                    "for DO %p\n", DeviceObject));
                DeviceObject->Flags &= ~DO_POWER_PAGABLE;
            }
        }

    }
    else
    {
        KdBreakPoint();

        //
        // cleanup the changes done above
        //

        if (set_pagable == true)
        {
            KdPrint((
                "AIMWrFltrPnp: Lower level driver failed, "
                "resetting PAGABLE bit for DO %p\n", DeviceObject));
            DeviceObject->Flags &= ~DO_POWER_PAGABLE;
            set_pagable = false;
        }

    }

    //
    // set the event so the next one can occur.
    //

    KeSetEvent(&device_extension->PagingPathCountEvent,
        IO_NO_INCREMENT, FALSE);

    //
    // and complete the irp
    //

    IoCompleteRequest(Irp, IO_NO_INCREMENT);

    return status;
}


NTSTATUS
AIMWrFltrCreate(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
{
    PAGED_CODE();

    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    if (device_extension->ShutdownThread)
    {
        return AIMWrFltrHandleRemovedDevice(Irp);
    }

    if (device_extension->Statistics.IsProtected)
    {
        KeSetEvent(&device_extension->ListEvent, 0, FALSE);
    }

    return AIMWrFltrSendToNextDriver(DeviceObject, Irp);

}				// end AIMWrFltrSendToNextDriver()


NTSTATUS
AIMWrFltrSendToNextDriver(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
/*++

Routine Description:

This routine sends the Irp to the next driver in line
when the Irp is not processed by this driver.

Arguments:

DeviceObject
Irp

Return Value:

NTSTATUS

--*/
{
    IoSkipCurrentIrpStackLocation(Irp);

    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    return IoCallDriver(device_extension->TargetDeviceObject, Irp);

}				// end AIMWrFltrSendToNextDriver()


NTSTATUS
AIMWrFltrForwardIrpSynchronous(IN PDEVICE_OBJECT DeviceObject, IN PIRP Irp)
/*++

Routine Description:

This routine sends the Irp to the next driver in line
when the Irp needs to be processed by the lower drivers
prior to being processed by this one.

Arguments:

DeviceObject
Irp

Return Value:

NTSTATUS

--*/
{
    PAGED_CODE();

    KEVENT event;
    KeInitializeEvent(&event, NotificationEvent, FALSE);

    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)DeviceObject->DeviceExtension;

    //
    // copy the irpstack for the next device
    //

    IoCopyCurrentIrpStackLocationToNext(Irp);

    //
    // set a completion routine
    //

    IoSetCompletionRoutine(Irp, AIMWrFltrSynchronousIrpCompletion,
        &event, TRUE, TRUE, TRUE);

    //
    // call the next lower device
    //

    NTSTATUS status = IoCallDriver(device_extension->TargetDeviceObject, Irp);

    //
    // wait for the actual completion
    //
    __analysis_assume(status != STATUS_PENDING);
    __analysis_assume(IoGetCurrentIrpStackLocation(Irp)->MinorFunction !=
        IRP_MN_START_DEVICE);

    if (status == STATUS_PENDING)
    {
        KeWaitForSingleObject(&event, Executive, KernelMode, FALSE, NULL);
        status = Irp->IoStatus.Status;
    }

    return status;

}				// end AIMWrFltrForwardIrpSynchronous()

VOID
AIMWrFltrUnload(IN PDRIVER_OBJECT DriverObject)
/*++

Routine Description:

Free all the allocated resources, etc.

Arguments:

DriverObject - pointer to a driver object.

Return Value:

VOID.

--*/
{
    PAGED_CODE();

    UNREFERENCED_PARAMETER(DriverObject);

    DbgPrint("AIMWrFltr: Unloading.\n");

    if (AIMWrFltrParametersKey != NULL)
    {
        ZwClose(AIMWrFltrParametersKey);
        AIMWrFltrParametersKey = NULL;
    }

    if (AIMWrFltrDiffFullEvent != NULL)
    {
        ObDereferenceObject(AIMWrFltrDiffFullEvent);
        AIMWrFltrDiffFullEvent = NULL;
    }
}

void * __CRTDECL operator new(size_t Size)
{
    return operator_new(Size, 0);
}

void * __CRTDECL operator new[](size_t Size)
{
    return operator_new(Size, 0);
}

void * __CRTDECL operator new(size_t Size, UCHAR FillByte)
{
    return operator_new(Size, FillByte);
}

void __CRTDECL operator delete(void * Ptr)
{
    operator_delete(Ptr);
}

void __CRTDECL operator delete(void * Ptr, size_t)
{
    operator_delete(Ptr);
}

void __CRTDECL operator delete[](void * Ptr)
{
    operator_delete(Ptr);
}

