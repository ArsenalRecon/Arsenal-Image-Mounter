#include "aimwrfltr.h"

PIRP SCATTERED_IRP::BuildIrp(
    UCHAR MajorFunction,
    PDEVICE_OBJECT DeviceObject,
    PFILE_OBJECT FileObject,
    ULONG OriginalIrpOffset,
    ULONG BytesThisIrp,
    PLARGE_INTEGER LowerDeviceOffset)
{
    if (DeviceObject == NULL)
    {
        DeviceObject = IoGetRelatedDeviceObject(FileObject);
    }

    PIO_STACK_LOCATION io_stack = IoGetCurrentIrpStackLocation(OriginalIrp);

    PIRP lower_irp = NULL;
    PIO_STACK_LOCATION lower_io_stack = NULL;
    bool copy_back = false;

    if ((DeviceObject->Flags & DO_DIRECT_IO) &&
        (OriginalDeviceObject->Flags & DO_BUFFERED_IO))
    {
        lower_irp = IoBuildAsynchronousFsdRequest(
            MajorFunction,
            DeviceObject,
            SystemBuffer + OriginalIrpOffset,
            BytesThisIrp,
            LowerDeviceOffset,
            NULL);

        if (lower_irp != NULL)
        {
            lower_io_stack = IoGetNextIrpStackLocation(lower_irp);
        }
    }
    else
    {
        lower_irp =
            IoAllocateIrp(DeviceObject->StackSize,
            FALSE);

        if (lower_irp != NULL)
        {
            lower_io_stack = IoGetNextIrpStackLocation(lower_irp);
            lower_io_stack->MajorFunction = MajorFunction;
            lower_io_stack->Parameters.Write.ByteOffset =
                *LowerDeviceOffset;
            lower_io_stack->Parameters.Write.Length = BytesThisIrp;

            if (DeviceObject->Flags & DO_DIRECT_IO)
            {
                PMDL mdl = IoAllocateMdl(
                    (PUCHAR)
                    MmGetMdlVirtualAddress(OriginalIrp->MdlAddress) +
                    OriginalIrpOffset,
                    BytesThisIrp, FALSE, FALSE, lower_irp);

                if (mdl == NULL)
                {
                    IoFreeIrp(lower_irp);
                    InterlockedExchange(&LastFailedStatus,
                        STATUS_INSUFFICIENT_RESOURCES);
                    return NULL;
                }

                IoBuildPartialMdl(
                    OriginalIrp->MdlAddress,
                    mdl, (PUCHAR)
                    MmGetMdlVirtualAddress(OriginalIrp->MdlAddress) +
                    OriginalIrpOffset,
                    BytesThisIrp);
            }
            else
            {
                if (SystemBuffer == NULL)
                {
                    SystemBuffer =
                        (PUCHAR)MmGetSystemAddressForMdlSafe(
                            OriginalIrp->MdlAddress, HighPagePriority);

                    if (SystemBuffer == NULL)
                    {
                        IoFreeIrp(lower_irp);
                        InterlockedExchange(&LastFailedStatus,
                            STATUS_INSUFFICIENT_RESOURCES);
                        return NULL;
                    }

                    AllocatedBuffer =
                        new UCHAR[io_stack->Parameters.Write.Length];

                    if (AllocatedBuffer == NULL)
                    {
                        IoFreeIrp(lower_irp);
                        InterlockedExchange(&LastFailedStatus,
                            STATUS_INSUFFICIENT_RESOURCES);
                        return NULL;
                    }

                    if (MajorFunction == IRP_MJ_WRITE)
                    {
                        RtlCopyMemory(AllocatedBuffer,
                            SystemBuffer,
                            io_stack->Parameters.Write.Length);
                    }
                }

                if (MajorFunction == IRP_MJ_READ)
                {
                    copy_back = true;
                }

                lower_irp->AssociatedIrp.SystemBuffer =
                    lower_irp->UserBuffer =
                    AllocatedBuffer + OriginalIrpOffset;
            }
        }
    }

    if (lower_irp == NULL)
    {
        InterlockedExchange(&LastFailedStatus,
            STATUS_INSUFFICIENT_RESOURCES);

        return NULL;
    }

    PPARTIAL_IRP partial = new PARTIAL_IRP;

    if (partial == NULL)
    {
        AIMWrFltrFreeIrpWithMdls(lower_irp);

        InterlockedExchange(&LastFailedStatus,
            STATUS_INSUFFICIENT_RESOURCES);

        return NULL;
    }

    partial->Scatter = this;
    partial->CopyBack = copy_back;
    partial->OriginalIrpOffset = OriginalIrpOffset;
    partial->BytesThisIrp = BytesThisIrp;
#ifdef  DBG
    partial->LowerDeviceOffset = LowerDeviceOffset->QuadPart;
#endif //  DBG

    lower_irp->Tail.Overlay.Thread = OriginalIrp->Tail.Overlay.Thread;

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

    lower_io_stack->FileObject = FileObject;

    IoSetCompletionRoutine(lower_irp, IrpCompletionRoutine,
        partial, TRUE, TRUE, TRUE);

    InterlockedIncrement(&ScatterCount);

    return lower_irp;
}

NTSTATUS
SCATTERED_IRP::IrpCompletionRoutine(
PDEVICE_OBJECT DeviceObject,
PIRP Irp,
PVOID Context
)
{
    UNREFERENCED_PARAMETER(DeviceObject);

    __analysis_assume(Context != NULL);

    PPARTIAL_IRP partial = (PPARTIAL_IRP)Context;

    PSCATTERED_IRP scatter = partial->Scatter;

    if (NT_SUCCESS(Irp->IoStatus.Status))
    {
        if (partial->BytesThisIrp != Irp->IoStatus.Information)
        {
            KdBreakPoint();

            KdPrint(("AIMWrFltr::Complete: Scattered IRP resulted in 0x%IX bytes, requested 0x%X\n",
                Irp->IoStatus.Information, partial->BytesThisIrp));
        }

        if (partial->CopyBack)
        {
            RtlCopyMemory(scatter->SystemBuffer + partial->OriginalIrpOffset,
                scatter->AllocatedBuffer + partial->OriginalIrpOffset,
                Irp->IoStatus.Information);
        }

        InterlockedExchangeAddPtr(&scatter->BytesCompleted, Irp->IoStatus.Information);
    }
    else
    {
        KdPrint(("AIMWrFltr::IrpCompletionRoutine: Lower level I/O 0x%X bytes at 0x%I64X failed: 0x%X\n",
            partial->BytesThisIrp, partial->LowerDeviceOffset, Irp->IoStatus.Status));

        KdBreakPoint();

        InterlockedExchange(&scatter->LastFailedStatus, Irp->IoStatus.Status);
    }

    delete partial;

    if (Irp->MdlAddress != scatter->OriginalIrp->MdlAddress)
    {
        AIMWrFltrFreeIrpWithMdls(Irp);
    }
    else
    {
        IoFreeIrp(Irp);
    }

    scatter->Complete();

    return STATUS_MORE_PROCESSING_REQUIRED;
}

