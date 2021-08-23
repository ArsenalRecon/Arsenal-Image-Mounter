#include "aimwrfltr.h"

#include <common.h>

void
AIMWrFltrDeviceWorkerThread(PVOID Context)
{
    PDEVICE_EXTENSION device_extension = (PDEVICE_EXTENSION)Context;

    KdPrint(("AIMWrFltr: Worker thread started for device %p\n",
        device_extension->DeviceObject));

    PUCHAR block_buffer = new UCHAR[DIFF_BLOCK_SIZE];

    if (block_buffer == NULL)
    {
        PsTerminateSystemThread(STATUS_INSUFFICIENT_RESOURCES);
        return;
    }

    PLIST_ENTRY request = &device_extension->ListHead;

    for (;;)
    {
        KLOCK_QUEUE_HANDLE lock_handle;

        KIRQL lowest_assumed_irql = PASSIVE_LEVEL;

        AIMWrFltrAcquireLock(&device_extension->ListLock, &lock_handle,
            lowest_assumed_irql);

        if (request != &device_extension->ListHead)
        {
            RemoveEntryList(request);

            delete CONTAINING_RECORD(request, CACHED_IRP, ListEntry);
        }

        request = device_extension->ListHead.Flink;

        AIMWrFltrReleaseLock(&lock_handle, &lowest_assumed_irql);

        if (request == &device_extension->ListHead &&
            device_extension->ShutdownThread)
        {
            KdPrint(("AIMWrFltr: Worker thread queue empty and device shutting down\n",
                device_extension->DeviceObject));

            break;
        }

        if (!AIMWrFltrLinksCreated)
        {
            UNICODE_STRING event_path;
            RtlInitUnicodeString(&event_path,
                L"\\Device\\" AIMWRFLTR_DIFF_FULL_EVENT_NAME);

            UNICODE_STRING event_link;
            RtlInitUnicodeString(&event_link,
                L"\\BaseNamedObjects\\Global\\"
                AIMWRFLTR_DIFF_FULL_EVENT_NAME);

            NTSTATUS status = IoCreateUnprotectedSymbolicLink(&event_link,
                &event_path);

            KdPrint(("AIMWrFltr:DeviceWorkerThread: Link creation status: %#x\n",
                status));

            if (NT_SUCCESS(status) ||
                status == STATUS_OBJECT_NAME_COLLISION)
            {
                AIMWrFltrLinksCreated = true;
            }
        }

        if (request == &device_extension->ListHead)
        {
            KeWaitForSingleObject(&device_extension->ListEvent, Executive,
                KernelMode, FALSE, NULL);

            continue;
        }

        PCACHED_IRP cached_irp = CONTAINING_RECORD(request, CACHED_IRP, ListEntry);

        if (device_extension->ShutdownThread && device_extension->Statistics.IgnoreFlushBuffers)
        {
            if (cached_irp->Irp != NULL)
            {
                AIMWrFltrHandleRemovedDevice(cached_irp->Irp);
            }
        }
        else if (cached_irp->DeviceObject != NULL && cached_irp->Irp != NULL)
        {
            IoCallDriver(cached_irp->DeviceObject, cached_irp->Irp);
        }
        else
        {
            NTSTATUS status;
            PIO_STACK_LOCATION io_stack = &cached_irp->IoStack;

            switch (io_stack->MajorFunction)
            {
            case IRP_MJ_READ:
                status = AIMWrFltrDeferredRead(device_extension, cached_irp->Irp, block_buffer);
                break;

            case IRP_MJ_WRITE:
                status = AIMWrFltrDeferredWrite(device_extension, cached_irp, block_buffer);
                break;

            case IRP_MJ_FLUSH_BUFFERS:
                status = AIMWrFltrDeferredFlushBuffers(device_extension, cached_irp);
                break;

            case IRP_MJ_DEVICE_CONTROL:
                switch (io_stack->Parameters.DeviceIoControl.IoControlCode)
                {
#ifdef FSCTL_FILE_LEVEL_TRIM
                case IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES:
                    status = AIMWrFltrDeferredManageDataSetAttributes(device_extension, cached_irp,
                        block_buffer);

                    break;
#endif

                default:
                    status = STATUS_INTERNAL_ERROR;
                    KdPrint(("AimWrFltrDeviceWorkerThread: Internal error.\n"));
                    KdBreakPoint();
#pragma warning(suppress: 4065)
                }

                break;

            default:
                status = STATUS_INTERNAL_ERROR;
                KdPrint(("AimWrFltrDeviceWorkerThread: Internal error.\n"));
                KdBreakPoint();
            }

            if (cached_irp->Irp != NULL)
            {
                cached_irp->Irp->IoStatus.Status = status;
                IoCompleteRequest(cached_irp->Irp, IO_NO_INCREMENT);
            }
        }

        IoReleaseRemoveLock(&device_extension->RemoveLock, cached_irp);
    }

    KdPrint(("AIMWrFltr: Terminating worker thread for device %p\n",
        device_extension->DeviceObject));

    delete[] block_buffer;

    PsTerminateSystemThread(STATUS_SUCCESS);
}

