
/// iodisp.c
/// Routines called from worker thread at PASSIVE_LEVEL to complete work items
/// queued form miniport dispatch routines.
/// 
/// Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code is available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

#define WkRtnVer     "1.013"

//#define _MP_H_skip_includes

#include "phdskmnt.h"

//#pragma warning(push)
//#pragma warning(disable : 4204)                       /* Prevent C4204 messages from stortrce.h. */
//#include <stortrce.h>
//#pragma warning(pop)
//
//#include "trace.h"
//#include "iodisp.tmh"

#define SECTOR_SIZE_CD_ROM     4096
#define SECTOR_SIZE_HDD        512

/**************************************************************************************************/
/*                                                                                                */
/* Globals, forward definitions, etc.                                                             */
/*                                                                                                */
/**************************************************************************************************/

VOID
ImScsiCleanupLU(
__in pHW_LU_EXTENSION     pLUExt
)
{
    pHW_HBA_EXT             pHBAExt = pLUExt->pHBAExt;
    pHW_LU_EXTENSION *      ppLUExt = NULL;
    PLIST_ENTRY             list_ptr;
    pMP_WorkRtnParms        free_worker_params = NULL;

#if defined(_AMD64_)
    KLOCK_QUEUE_HANDLE      LockHandle;
#else
    KIRQL                   SaveIrql;
#endif

    KdPrint(("PhDskMnt::ImScsiCleanupLU: Removing device: %d:%d:%d pLUExt=%p\n",
        pLUExt->DeviceNumber.PathId,
        pLUExt->DeviceNumber.TargetId,
        pLUExt->DeviceNumber.Lun,
        pLUExt));

    free_worker_params = ExAllocatePoolWithTag(
        NonPagedPool, sizeof(MP_WorkRtnParms), MP_TAG_GENERAL);

    if (free_worker_params == NULL)
    {
        DbgPrint("PhDskMnt::ImScsiCleanupLU: Memory allocation error.\n");
        return;
    }

    RtlZeroMemory(free_worker_params, sizeof(MP_WorkRtnParms));
    free_worker_params->pHBAExt = pHBAExt;
    free_worker_params->pLUExt = pLUExt;

#if defined(_AMD64_)
    KeAcquireInStackQueuedSpinLock(                   // Serialize the linked list of LUN extensions.              
        &pHBAExt->LUListLock, &LockHandle);
#else
    KeAcquireSpinLock(&pHBAExt->LUListLock, &SaveIrql);
#endif

    ppLUExt = (pHW_LU_EXTENSION*)StoragePortGetLogicalUnit(
        pHBAExt,
        pLUExt->DeviceNumber.PathId,
        pLUExt->DeviceNumber.TargetId,
        pLUExt->DeviceNumber.Lun
        );

    if (ppLUExt != NULL)
        *ppLUExt = NULL;

    for (list_ptr = pHBAExt->LUList.Flink;
        list_ptr != &pHBAExt->LUList;
        list_ptr = list_ptr->Flink
        )
    {
        pHW_LU_EXTENSION object;
        object = CONTAINING_RECORD(list_ptr, HW_LU_EXTENSION, List);

        if (object == pLUExt)
        {
            list_ptr->Blink->Flink = list_ptr->Flink;
            list_ptr->Flink->Blink = list_ptr->Blink;

            KdPrint(("PhDskMnt::ImScsiCleanupLU: Setting request to wait for LU worker thread.\n"));

            ExInterlockedInsertTailList(
                &pMPDrvInfoGlobal->RequestList,
                &free_worker_params->RequestListEntry,
                &pMPDrvInfoGlobal->RequestListLock);

            KeSetEvent(&pMPDrvInfoGlobal->RequestEvent, (KPRIORITY)0, FALSE);

            break;
        }
    }

#if defined(_AMD64_)
    KeReleaseInStackQueuedSpinLock(&LockHandle);
#else
    KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

    /// ToDo: Cleanup all file handles, object name buffers,
    /// proxy refs etc.
    if (pLUExt->UseProxy)
    {
        ImScsiCloseProxy(&pLUExt->Proxy);
    }

    if (pLUExt->LastIoBuffer != NULL)
    {
        ExFreePoolWithTag(pLUExt->LastIoBuffer, MP_TAG_GENERAL);
        pLUExt->LastIoBuffer = NULL;
    }

    if (pLUExt->VMDisk)
    {
        SIZE_T free_size = 0;
        if (pLUExt->ImageBuffer != NULL)
            ZwFreeVirtualMemory(NtCurrentProcess(),
            (PVOID*)&pLUExt->ImageBuffer,
            &free_size, MEM_RELEASE);

        pLUExt->ImageBuffer = NULL;
    }
    else
    {
        if (pLUExt->ImageFile != NULL)
            ZwClose(pLUExt->ImageFile);

        pLUExt->ImageFile = NULL;
    }

    if (pLUExt->ObjectName.Buffer != NULL)
    {
        ExFreePoolWithTag(pLUExt->ObjectName.Buffer, MP_TAG_GENERAL);
        pLUExt->ObjectName.Buffer = NULL;
        pLUExt->ObjectName.Length = 0;
        pLUExt->ObjectName.MaximumLength = 0;
    }

    KdPrint(("PhDskMnt::ImScsiCleanupLU: Done.\n"));
}

VOID
ImScsiCreateLU(
__in pHW_HBA_EXT             pHBAExt,
__in PSCSI_REQUEST_BLOCK     pSrb,
__in PETHREAD                pReqThread
)
{
    PSRB_IMSCSI_CREATE_DATA new_device = (PSRB_IMSCSI_CREATE_DATA)pSrb->DataBuffer;
    PLIST_ENTRY             list_ptr;
    pHW_LU_EXTENSION        pLUExt = NULL;
    NTSTATUS                ntstatus;

#if defined(_AMD64_)
    KLOCK_QUEUE_HANDLE      LockHandle;
#else
    KIRQL                   SaveIrql;
#endif

    KdPrint(("PhDskMnt::ImScsiCreateLU: Initializing new device: %d:%d:%d\n",
        new_device->DeviceNumber.PathId,
        new_device->DeviceNumber.TargetId,
        new_device->DeviceNumber.Lun));

#if defined(_AMD64_)
    KeAcquireInStackQueuedSpinLock(                   // Serialize the linked list of LUN extensions.              
        &pHBAExt->LUListLock, &LockHandle);
#else
    KeAcquireSpinLock(&pHBAExt->LUListLock, &SaveIrql);
#endif

    for (list_ptr = pHBAExt->LUList.Flink;
        list_ptr != &pHBAExt->LUList;
        list_ptr = list_ptr->Flink
        )
    {
        pHW_LU_EXTENSION object;
        object = CONTAINING_RECORD(list_ptr, HW_LU_EXTENSION, List);

        if (object->DeviceNumber.LongNumber ==
            new_device->DeviceNumber.LongNumber)
        {
            pLUExt = object;
            break;
        }
    }

    if (pLUExt != NULL)
    {
#if defined(_AMD64_)
        KeReleaseInStackQueuedSpinLock(&LockHandle);
#else
        KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

        ntstatus = STATUS_OBJECT_NAME_EXISTS;

        goto Done;
    }

    pLUExt = (pHW_LU_EXTENSION)ExAllocatePoolWithTag(NonPagedPool, sizeof(HW_LU_EXTENSION), MP_TAG_GENERAL);

    if (pLUExt == NULL)
    {
#if defined(_AMD64_)
        KeReleaseInStackQueuedSpinLock(&LockHandle);
#else
        KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

        ntstatus = STATUS_INSUFFICIENT_RESOURCES;

        goto Done;
    }

    RtlZeroMemory(pLUExt, sizeof(HW_LU_EXTENSION));

    pLUExt->DeviceNumber = new_device->DeviceNumber;

    KeInitializeEvent(&pLUExt->StopThread, NotificationEvent, FALSE);

    InsertHeadList(&pHBAExt->LUList, &pLUExt->List);

#if defined(_AMD64_)
    KeReleaseInStackQueuedSpinLock(&LockHandle);
#else
    KeReleaseSpinLock(&pHBAExt->LUListLock, SaveIrql);
#endif

    pLUExt->pHBAExt = pHBAExt;

    ntstatus = ImScsiInitializeLU(pLUExt, new_device, pReqThread);
    if (!NT_SUCCESS(ntstatus))
    {
        ImScsiCleanupLU(pLUExt);
        goto Done;
    }

Done:
    new_device->SrbIoControl.ReturnCode = ntstatus;

    ScsiSetSuccess(pSrb, pSrb->DataTransferLength);

    return;
}

NTSTATUS
ImScsiReadDevice(
__in pHW_LU_EXTENSION pLUExt,
__in PVOID            Buffer,
__in PLARGE_INTEGER   Offset,
__in PULONG           Length
)
{
    IO_STATUS_BLOCK io_status = { 0 };
    NTSTATUS status = STATUS_NOT_IMPLEMENTED;
    LARGE_INTEGER byteoffset;

    byteoffset.QuadPart = Offset->QuadPart + pLUExt->ImageOffset.QuadPart;

    KdPrint2(("PhDskMnt::ImScsiReadDevice: pLUExt=%p, Buffer=%p, Offset=0x%I64X, EffectiveOffset=0x%I64X, Length=0x%X\n", pLUExt, Buffer, *Offset, byteoffset, *Length));

    if (pLUExt->VMDisk)
    {
#ifdef _WIN64
        ULONG_PTR vm_offset = Offset->QuadPart;
#else
        ULONG_PTR vm_offset = Offset->LowPart;
#endif

        RtlCopyMemory(Buffer,
            pLUExt->ImageBuffer + vm_offset,
            *Length);

        status = STATUS_SUCCESS;
        io_status.Status = status;
        io_status.Information = *Length;
    }
    else if (pLUExt->UseProxy)
        status = ImScsiReadProxy(
        &pLUExt->Proxy,
        &io_status,
        &pLUExt->StopThread,
        Buffer,
        *Length,
        &byteoffset);
    else if (pLUExt->ImageFile != NULL)
        status = NtReadFile(
        pLUExt->ImageFile,
        NULL,
        NULL,
        NULL,
        &io_status,
        Buffer,
        *Length,
        &byteoffset,
        NULL);

    if ((status == STATUS_END_OF_FILE) &
        (io_status.Information == 0))
    {
        KdPrint2(("PhDskMnt::ImScsiReadDevice pLUExt=%p, status=STATUS_END_OF_FILE, Length=0x%X. Returning zeroed buffer with requested length.\n", pLUExt, *Length));
        RtlZeroMemory(Buffer, *Length);
        status = STATUS_SUCCESS;
    }
    else
    {
        *Length = (ULONG)io_status.Information;
    }

    KdPrint2(("PhDskMnt::ImScsiReadDevice Result: pLUExt=%p, status=0x%X, Length=0x%X\n", pLUExt, status, *Length));

    return status;
}

NTSTATUS
ImScsiWriteDevice(
__in pHW_LU_EXTENSION pLUExt,
__in PVOID            Buffer,
__in PLARGE_INTEGER   Offset,
__in PULONG           Length
)
{
    IO_STATUS_BLOCK io_status = { 0 };
    NTSTATUS status = STATUS_NOT_IMPLEMENTED;
    LARGE_INTEGER byteoffset;

    byteoffset.QuadPart = Offset->QuadPart + pLUExt->ImageOffset.QuadPart;

    KdPrint2(("PhDskMnt::ImScsiWriteDevice: pLUExt=%p, Buffer=%p, Offset=0x%I64X, EffectiveOffset=0x%I64X, Length=0x%X\n", pLUExt, Buffer, *Offset, byteoffset, *Length));

    pLUExt->Modified = TRUE;

    if (pLUExt->VMDisk)
    {
#ifdef _WIN64
        ULONG_PTR vm_offset = Offset->QuadPart;
#else
        ULONG_PTR vm_offset = Offset->LowPart;
#endif

        RtlCopyMemory(pLUExt->ImageBuffer + vm_offset,
            Buffer,
            *Length);

        status = STATUS_SUCCESS;
        io_status.Status = status;
        io_status.Information = *Length;
    }
    else if (pLUExt->UseProxy)
        status = ImScsiWriteProxy(
        &pLUExt->Proxy,
        &io_status,
        &pLUExt->StopThread,
        Buffer,
        *Length,
        &byteoffset);
    else if (pLUExt->ImageFile != NULL)
        status = NtWriteFile(
        pLUExt->ImageFile,
        NULL,
        NULL,
        NULL,
        &io_status,
        Buffer,
        *Length,
        &byteoffset,
        NULL);

    *Length = (ULONG)io_status.Information;

    KdPrint2(("PhDskMnt::ImScsiWriteDevice Result: pLUExt=%p, status=0x%X, Length=0x%X\n", pLUExt, status, *Length));

    return status;
}

NTSTATUS
ImScsiInitializeLU(IN pHW_LU_EXTENSION LUExtension,
IN OUT PSRB_IMSCSI_CREATE_DATA CreateData,
IN PETHREAD ClientThread)
{
    UNICODE_STRING file_name;
    HANDLE thread_handle = NULL;
    NTSTATUS status;
    HANDLE file_handle = NULL;
    PUCHAR image_buffer = NULL;
    PROXY_CONNECTION proxy = { 0 };
    ULONG alignment_requirement;

    ASSERT(CreateData != NULL);

    KdPrint
        (("PhDskMnt: Got request to create a virtual disk. Request data:\n"
        "DeviceNumber   = %#x\n"
        "DiskSize       = %I64u\n"
        "ImageOffset    = %I64u\n"
        "SectorSize     = %u\n"
        "Flags          = %#x\n"
        "FileNameLength = %u\n"
        "FileName       = '%.*ws'\n",
        CreateData->DeviceNumber,
        CreateData->DiskSize.QuadPart,
        CreateData->ImageOffset.QuadPart,
        CreateData->BytesPerSector,
        CreateData->Flags,
        CreateData->FileNameLength,
        (int)(CreateData->FileNameLength / sizeof(*CreateData->FileName)),
        CreateData->FileName));

    // Auto-select type if not specified.
    if (IMSCSI_TYPE(CreateData->Flags) == 0)
        if (CreateData->FileNameLength == 0)
            CreateData->Flags |= IMSCSI_TYPE_VM;
        else
            CreateData->Flags |= IMSCSI_TYPE_FILE;

    // Blank filenames only supported for non-zero VM disks.
    if (((CreateData->FileNameLength == 0) &
        (IMSCSI_TYPE(CreateData->Flags) != IMSCSI_TYPE_VM)) |
        ((CreateData->FileNameLength == 0) &
        (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM) &
        (CreateData->DiskSize.QuadPart == 0)))
    {
        KdPrint(("PhDskMnt: Blank filenames only supported for non-zero length "
            "vm type disks.\n"));

        ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
            0,
            0,
            NULL,
            0,
            1000,
            STATUS_INVALID_PARAMETER,
            102,
            STATUS_INVALID_PARAMETER,
            0,
            0,
            NULL,
            L"Blank filenames only supported for non-zero length "
            L"vm type disks."));

        return STATUS_INVALID_PARAMETER;
    }

    // Cannot create >= 2 GB VM disk in 32 bit version.
#ifndef _WIN64
    if ((IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM) &
        ((CreateData->DiskSize.QuadPart & 0xFFFFFFFF80000000) !=
        0))
    {
        KdPrint(("PhDskMnt: Cannot create >= 2GB vm disks on 32-bit system.\n"));

        return STATUS_INVALID_PARAMETER;
    }
#endif

    file_name.Length = CreateData->FileNameLength;
    file_name.MaximumLength = CreateData->FileNameLength;
    file_name.Buffer = NULL;

    // If a file is to be opened or created, allocate name buffer and open that
    // file...
    if (CreateData->FileNameLength > 0)
    {
        IO_STATUS_BLOCK io_status;
        OBJECT_ATTRIBUTES object_attributes;
        UNICODE_STRING real_file_name;

        file_name.Buffer = (PWCHAR)ExAllocatePoolWithTag(NonPagedPool,
            file_name.MaximumLength, MP_TAG_GENERAL);

        if (file_name.Buffer == NULL)
        {
            KdPrint(("PhDskMnt: Error allocating buffer for filename.\n"));

            ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                0,
                0,
                NULL,
                0,
                1000,
                STATUS_INSUFFICIENT_RESOURCES,
                102,
                STATUS_INSUFFICIENT_RESOURCES,
                0,
                0,
                NULL,
                L"Memory allocation error."));

            return STATUS_INSUFFICIENT_RESOURCES;
        }

        RtlCopyMemory(file_name.Buffer, CreateData->FileName,
            CreateData->FileNameLength);

        // If no device-type specified, check if filename ends with .iso or .nrg.
        // In that case, set device-type automatically to FILE_DEVICE_CDROM
        //if ((IMSCSI_DEVICE_TYPE(CreateData->Flags) == 0) &
        //    (CreateData->FileNameLength >= (4 * sizeof(*CreateData->FileName))))
        //{
        //    LPWSTR name = CreateData->FileName +
        //	(CreateData->FileNameLength / sizeof(*CreateData->FileName)) - 4;
        //    if ((_wcsnicmp(name, L".iso", 4) == 0) ||
        //	(_wcsnicmp(name, L".bin", 4) == 0) ||
        //	(_wcsnicmp(name, L".nrg", 4) == 0))
        //	CreateData->Flags |= IMSCSI_DEVICE_TYPE_CD | IMSCSI_OPTION_RO;
        //}

        if (IMSCSI_DEVICE_TYPE(CreateData->Flags) == IMSCSI_DEVICE_TYPE_CD)
            CreateData->Flags |= IMSCSI_OPTION_RO;

        KdPrint(("PhDskMnt: Done with device type auto-selection by file ext.\n"));

        if (ClientThread != NULL)
        {
            SECURITY_QUALITY_OF_SERVICE security_quality_of_service;
            SECURITY_CLIENT_CONTEXT security_client_context;

            RtlZeroMemory(&security_quality_of_service,
                sizeof(SECURITY_QUALITY_OF_SERVICE));

            security_quality_of_service.Length =
                sizeof(SECURITY_QUALITY_OF_SERVICE);
            security_quality_of_service.ImpersonationLevel =
                SecurityImpersonation;
            security_quality_of_service.ContextTrackingMode =
                SECURITY_STATIC_TRACKING;
            security_quality_of_service.EffectiveOnly = FALSE;

            status =
                SeCreateClientSecurity(
                ClientThread,
                &security_quality_of_service,
                FALSE,
                &security_client_context);

            if (NT_SUCCESS(status))
            {
                KdPrint(("PhDskMnt: Impersonating client thread token.\n"));
                SeImpersonateClient(&security_client_context, NULL);
                SeDeleteClientSecurity(&security_client_context);
            }
            else
                DbgPrint("PhDskMnt: Error impersonating client thread token: %#X\n", status);
        }
        else
            KdPrint(("PhDskMnt: No impersonation information.\n"));

        if ((IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY) &
            ((IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_TCP) |
            (IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_COMM)))
        {
            RtlInitUnicodeString(&real_file_name, IMDPROXY_SVC_PIPE_NATIVE_NAME);

            InitializeObjectAttributes(&object_attributes,
                &real_file_name,
                OBJ_CASE_INSENSITIVE,
                NULL,
                NULL);
        }
        else
        {
            real_file_name = file_name;

            InitializeObjectAttributes(&object_attributes,
                &real_file_name,
                OBJ_CASE_INSENSITIVE |
                OBJ_FORCE_ACCESS_CHECK,
                NULL,
                NULL);
        }

        if ((IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY) &
            (IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_SHM))
        {
            proxy.connection_type = PROXY_CONNECTION_SHM;

            status =
                ZwOpenSection(&file_handle,
                GENERIC_READ | GENERIC_WRITE,
                &object_attributes);
        }
        else
        {
            KdPrint(("PhDskMnt: Passing WriteMode=%#x and WriteShare=%#x\n",
                (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY) |
                ((IMSCSI_TYPE(CreateData->Flags) != IMSCSI_TYPE_VM) &
                !IMSCSI_READONLY(CreateData->Flags)),
                IMSCSI_READONLY(CreateData->Flags) |
                (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM)));

            status =
                ZwCreateFile(&file_handle,
                GENERIC_READ |
                ((IMSCSI_TYPE(CreateData->Flags) ==
                IMSCSI_TYPE_PROXY) |
                (((IMSCSI_TYPE(CreateData->Flags) !=
                IMSCSI_TYPE_VM) &
                !IMSCSI_READONLY(CreateData->Flags))) ?
            GENERIC_WRITE : 0),
                            &object_attributes,
                            &io_status,
                            NULL,
                            FILE_ATTRIBUTE_NORMAL,
                            FILE_SHARE_READ |
                            FILE_SHARE_DELETE |
                            (IMSCSI_READONLY(CreateData->Flags) |
                            (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM) ?
                        FILE_SHARE_WRITE : 0),
                                           FILE_OPEN,
                                           IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY ?
                                           FILE_NON_DIRECTORY_FILE |
                                           FILE_SEQUENTIAL_ONLY |
                                           FILE_NO_INTERMEDIATE_BUFFERING |
                                       FILE_SYNCHRONOUS_IO_NONALERT :
                                                                    FILE_NON_DIRECTORY_FILE |
                                                                    FILE_RANDOM_ACCESS |
                                                                    FILE_NO_INTERMEDIATE_BUFFERING |
                                                                    FILE_SYNCHRONOUS_IO_NONALERT,
                                                                    NULL,
                                                                    0);
        }

        // For 32 bit driver running on Windows 2000 and earlier, the above
        // call will fail because OBJ_FORCE_ACCESS_CHECK is not supported. If so,
        // STATUS_INVALID_PARAMETER is returned and we go on without any access
        // checks in that case.
#ifdef NT4_COMPATIBLE
        if (status == STATUS_INVALID_PARAMETER)
        {
            InitializeObjectAttributes(&object_attributes,
                &real_file_name,
                OBJ_CASE_INSENSITIVE,
                NULL,
                NULL);

            if ((IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY) &
                (IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_SHM))
            {
                proxy.connection_type = PROXY_CONNECTION_SHM;

                status =
                    ZwOpenSection(&file_handle,
                    GENERIC_READ | GENERIC_WRITE,
                    &object_attributes);
            }
            else
            {
                status =
                    ZwCreateFile(&file_handle,
                    GENERIC_READ |
                    ((IMSCSI_TYPE(CreateData->Flags) ==
                    IMSCSI_TYPE_PROXY) |
                    (((IMSCSI_TYPE(CreateData->Flags) !=
                    IMSCSI_TYPE_VM) &
                    !IMSCSI_READONLY(CreateData->Flags))) ?
                GENERIC_WRITE : 0),
                                &object_attributes,
                                &io_status,
                                NULL,
                                FILE_ATTRIBUTE_NORMAL,
                                FILE_SHARE_READ |
                                FILE_SHARE_DELETE |
                                (IMSCSI_READONLY(CreateData->Flags) |
                                (IMSCSI_TYPE(CreateData->Flags) ==
                                IMSCSI_TYPE_VM) ?
                            FILE_SHARE_WRITE : 0),
                                               FILE_OPEN,
                                               IMSCSI_TYPE(CreateData->Flags) ==
                                               IMSCSI_TYPE_PROXY ?
                                               FILE_NON_DIRECTORY_FILE |
                                               FILE_SEQUENTIAL_ONLY |
                                               FILE_NO_INTERMEDIATE_BUFFERING |
                                           FILE_SYNCHRONOUS_IO_NONALERT :
                                                                        FILE_NON_DIRECTORY_FILE |
                                                                        FILE_RANDOM_ACCESS |
                                                                        FILE_NO_INTERMEDIATE_BUFFERING |
                                                                        FILE_SYNCHRONOUS_IO_NONALERT,
                                                                        NULL,
                                                                        0);
            }
        }
#endif

        if (!NT_SUCCESS(status))
            KdPrint(("PhDskMnt: Error opening file '%.*ws'. Status: %#x SpecSize: %i WritableFile: %i DevTypeFile: %i Flags: %#x\n",
            (int)(real_file_name.Length / sizeof(WCHAR)),
            real_file_name.Buffer,
            status,
            CreateData->DiskSize.QuadPart != 0,
            !IMSCSI_READONLY(CreateData->Flags),
            IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_FILE,
            CreateData->Flags));

        // If not found we will create the file if a new non-zero size is
        // specified, read-only virtual disk is not specified and we are
        // creating a type 'file' virtual disk.
        if (((status == STATUS_OBJECT_NAME_NOT_FOUND) |
            (status == STATUS_NO_SUCH_FILE)) &
            (CreateData->DiskSize.QuadPart != 0) &
            (!IMSCSI_READONLY(CreateData->Flags)) &
            (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_FILE))
        {

            status =
                ZwCreateFile(&file_handle,
                GENERIC_READ |
                GENERIC_WRITE,
                &object_attributes,
                &io_status,
                &CreateData->DiskSize,
                FILE_ATTRIBUTE_NORMAL,
                FILE_SHARE_READ |
                FILE_SHARE_DELETE,
                FILE_OPEN_IF,
                FILE_NON_DIRECTORY_FILE |
                FILE_RANDOM_ACCESS |
                FILE_NO_INTERMEDIATE_BUFFERING |
                FILE_SYNCHRONOUS_IO_NONALERT, NULL, 0);

            if (!NT_SUCCESS(status))
            {
                ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                    0,
                    0,
                    NULL,
                    0,
                    1000,
                    status,
                    102,
                    status,
                    0,
                    0,
                    NULL,
                    L"Cannot create image file."));

                KdPrint(("PhDskMnt: Cannot create '%.*ws'. (%#x)\n",
                    (int)(CreateData->FileNameLength /
                    sizeof(*CreateData->FileName)),
                    CreateData->FileName,
                    status));

                ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                return status;
            }
        }
        else if (!NT_SUCCESS(status))
        {
            ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                0,
                0,
                NULL,
                0,
                1000,
                status,
                102,
                status,
                0,
                0,
                NULL,
                L"Cannot open image file."));

            KdPrint(("PhDskMnt: Cannot open file '%.*ws'. Status: %#x\n",
                (int)(real_file_name.Length / sizeof(WCHAR)),
                real_file_name.Buffer,
                status));

            ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

            return status;
        }

        KdPrint(("PhDskMnt: File '%.*ws' opened successfully.\n",
            (int)(real_file_name.Length / sizeof(WCHAR)),
            real_file_name.Buffer));

        if (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY)
        {
            if (IMSCSI_PROXY_TYPE(CreateData->Flags) == IMSCSI_PROXY_TYPE_SHM)
                status =
                ZwMapViewOfSection(file_handle,
                NtCurrentProcess(),
                (PVOID*)&proxy.shared_memory,
                0,
                0,
                NULL,
                &proxy.shared_memory_size,
                ViewUnmap,
                0,
                PAGE_READWRITE);
            else
                status =
                ObReferenceObjectByHandle(file_handle,
                FILE_READ_ATTRIBUTES |
                FILE_READ_DATA |
                FILE_WRITE_DATA,
                *IoFileObjectType,
                KernelMode,
                (PVOID*)&proxy.device,
                NULL);

            if (!NT_SUCCESS(status))
            {
                ZwClose(file_handle);
                ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                    0,
                    0,
                    NULL,
                    0,
                    1000,
                    status,
                    102,
                    status,
                    0,
                    0,
                    NULL,
                    L"Error referencing proxy device."));

                KdPrint(("PhDskMnt: Error referencing proxy device (%#x).\n",
                    status));

                return status;
            }

            KdPrint(("PhDskMnt: Got reference to proxy object %#x.\n",
                proxy.connection_type == PROXY_CONNECTION_DEVICE ?
                (PVOID)proxy.device :
                (PVOID)proxy.shared_memory));

            if (IMSCSI_PROXY_TYPE(CreateData->Flags) != IMSCSI_PROXY_TYPE_DIRECT)
                status = ImScsiConnectProxy(&proxy,
                &io_status,
                NULL,
                CreateData->Flags,
                CreateData->FileName,
                CreateData->FileNameLength);

            if (!NT_SUCCESS(status))
            {
                ImScsiCloseProxy(&proxy);
                ZwClose(file_handle);
                ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                    0,
                    0,
                    NULL,
                    0,
                    1000,
                    status,
                    102,
                    status,
                    0,
                    0,
                    NULL,
                    L"Error connecting proxy."));

                KdPrint(("PhDskMnt: Error connecting proxy (%#x).\n", status));

                return status;
            }
        }

        // Get the file size of the disk file.
        if (IMSCSI_TYPE(CreateData->Flags) != IMSCSI_TYPE_PROXY)
        {
            LARGE_INTEGER disk_size;

            status = ImScsiGetDiskSize(
                file_handle,
                &io_status,
                &disk_size);

            if (!NT_SUCCESS(status))
            {
                ZwClose(file_handle);
                ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                    0,
                    0,
                    NULL,
                    0,
                    1000,
                    status,
                    102,
                    status,
                    0,
                    0,
                    NULL,
                    L"Error getting image size."));

                KdPrint
                    (("PhDskMnt: Error getting image size (%#x).\n",
                    status));

                return status;
            }

            // Allocate virtual memory for 'vm' type.
            if (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM)
            {
                SIZE_T max_size;

                // If no size given for VM disk, use size of pre-load image file.
                // This code is somewhat easier for 64 bit architectures.

#ifdef _WIN64
                if (CreateData->DiskSize.QuadPart == 0)
                    CreateData->DiskSize.QuadPart =
                    disk_size.QuadPart -
                    CreateData->ImageOffset.QuadPart;

                max_size = CreateData->DiskSize.QuadPart;
#else
                if (CreateData->DiskSize.QuadPart == 0)
                    // Check that file size < 2 GB.
                    if ((disk_size.QuadPart -
                        CreateData->ImageOffset.QuadPart) & 0xFFFFFFFF80000000)
                    {
                        ZwClose(file_handle);
                        ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                        KdPrint(("PhDskMnt: VM disk >= 2GB not supported.\n"));

                        return STATUS_INSUFFICIENT_RESOURCES;
                    }
                    else
                        CreateData->DiskSize.QuadPart =
                        disk_size.QuadPart -
                        CreateData->ImageOffset.QuadPart;

                max_size = CreateData->DiskSize.LowPart;
#endif

                status =
                    ZwAllocateVirtualMemory(NtCurrentProcess(),
                    (PVOID*)&image_buffer,
                    0,
                    &max_size,
                    MEM_COMMIT,
                    PAGE_READWRITE);
                if (!NT_SUCCESS(status))
                {
                    ZwClose(file_handle);
                    ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                    ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                        0,
                        0,
                        NULL,
                        0,
                        1000,
                        status,
                        102,
                        status,
                        0,
                        0,
                        NULL,
                        L"Not enough memory for VM disk."));

                    KdPrint(("PhDskMnt: Error allocating vm for image. (%#x)\n",
                        status));

                    return STATUS_NO_MEMORY;
                }

                alignment_requirement = FILE_BYTE_ALIGNMENT;

                // Loading of image file has been moved to be done just before
                // the service loop.
            }
            else
            {
                FILE_ALIGNMENT_INFORMATION file_alignment;

                status = ZwQueryInformationFile(file_handle,
                    &io_status,
                    &file_alignment,
                    sizeof
                    (FILE_ALIGNMENT_INFORMATION),
                    FileAlignmentInformation);

                if (!NT_SUCCESS(status))
                {
                    ZwClose(file_handle);
                    ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                    ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                        0,
                        0,
                        NULL,
                        0,
                        1000,
                        status,
                        102,
                        status,
                        0,
                        0,
                        NULL,
                        L"Error getting alignment information."));

                    KdPrint(("PhDskMnt: Error querying file alignment (%#x).\n",
                        status));

                    return status;
                }

                // If creating a sparse image file
                if (IMSCSI_SPARSE_FILE(CreateData->Flags))
                {
                    status = ZwFsControlFile(
                        file_handle,
                        NULL,
                        NULL,
                        NULL,
                        &io_status,
                        FSCTL_SET_SPARSE,
                        NULL,
                        0,
                        NULL,
                        0
                        );

                    if (NT_SUCCESS(status))
                        KdPrint(("PhDskMnt::ImScsiInitializeLU: Sparse attribute set on image file.\n"));
                    else
                        DbgPrint("PhDskMnt::ImScsiInitializeLU: Cannot set sparse attribute on image file: 0x%X\n", status);
                }

                if (CreateData->DiskSize.QuadPart == 0)
                    CreateData->DiskSize.QuadPart =
                    disk_size.QuadPart -
                    CreateData->ImageOffset.QuadPart;
                else if ((disk_size.QuadPart <
                    CreateData->DiskSize.QuadPart +
                    CreateData->ImageOffset.QuadPart) &
                    (!IMSCSI_READONLY(CreateData->Flags)))
                {
                    LARGE_INTEGER new_image_size;
                    new_image_size.QuadPart =
                        CreateData->DiskSize.QuadPart +
                        CreateData->ImageOffset.QuadPart;

                    // Adjust the file length to the requested virtual disk size.
                    status = ZwSetInformationFile(
                        file_handle,
                        &io_status,
                        &new_image_size,
                        sizeof(FILE_END_OF_FILE_INFORMATION),
                        FileEndOfFileInformation);

                    if (!NT_SUCCESS(status))
                    {
                        ZwClose(file_handle);
                        ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                        ImScsiLogError((
                            pMPDrvInfoGlobal->pDriverObj,
                            0,
                            0,
                            NULL,
                            0,
                            1000,
                            status,
                            102,
                            status,
                            0,
                            0,
                            NULL,
                            L"Error setting file size."));

                        DbgPrint("PhDskMnt: Error setting eof (%#x).\n", status);
                        return status;
                    }
                }

                alignment_requirement = file_alignment.AlignmentRequirement;
            }
        }
        else
            // If proxy is used, get the image file size from the proxy instead.
        {
            IMDPROXY_INFO_RESP proxy_info;

            status = ImScsiQueryInformationProxy(&proxy,
                &io_status,
                NULL,
                &proxy_info,
                sizeof(IMDPROXY_INFO_RESP));

            if (!NT_SUCCESS(status))
            {
                ImScsiCloseProxy(&proxy);
                ZwClose(file_handle);
                ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                    0,
                    0,
                    NULL,
                    0,
                    1000,
                    status,
                    102,
                    status,
                    0,
                    0,
                    NULL,
                    L"Error querying proxy."));

                KdPrint(("PhDskMnt: Error querying proxy (%#x).\n", status));

                return status;
            }

            if (CreateData->DiskSize.QuadPart == 0)
                CreateData->DiskSize.QuadPart = proxy_info.file_size;

            if ((proxy_info.req_alignment - 1 > FILE_512_BYTE_ALIGNMENT) |
                (CreateData->DiskSize.QuadPart == 0))
            {
                ImScsiCloseProxy(&proxy);
                ZwClose(file_handle);
                ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);

                ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                    0,
                    0,
                    NULL,
                    0,
                    1000,
                    status,
                    102,
                    status,
                    0,
                    0,
                    NULL,
                    L"Unsupported sizes."));

                KdPrint(("PhDskMnt: Unsupported sizes. "
                    "Got 0x%08x%08x size and 0x%08x%08x alignment.\n",
                    proxy_info.file_size,
                    proxy_info.req_alignment));

                return STATUS_INVALID_PARAMETER;
            }

            alignment_requirement = (ULONG)proxy_info.req_alignment - 1;

            if (proxy_info.flags & IMDPROXY_FLAG_RO)
                CreateData->Flags |= IMSCSI_OPTION_RO;

            KdPrint(("PhDskMnt: Got from proxy: Siz=0x%08x%08x Flg=%#x Alg=%#x.\n",
                CreateData->DiskSize.HighPart,
                CreateData->DiskSize.LowPart,
                (ULONG)proxy_info.flags,
                (ULONG)proxy_info.req_alignment));
        }

        if (CreateData->DiskSize.QuadPart == 0)
        {
            SIZE_T free_size = 0;

            ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                0,
                0,
                NULL,
                0,
                1000,
                status,
                102,
                status,
                0,
                0,
                NULL,
                L"Disk size equals zero."));

            KdPrint(("PhDskMnt: Fatal error: Disk size equals zero.\n"));

            ImScsiCloseProxy(&proxy);
            if (file_handle != NULL)
                ZwClose(file_handle);
            if (file_name.Buffer != NULL)
                ExFreePoolWithTag(file_name.Buffer, MP_TAG_GENERAL);
            if (image_buffer != NULL)
                ZwFreeVirtualMemory(NtCurrentProcess(),
                (PVOID*)&image_buffer,
                &free_size, MEM_RELEASE);

            return STATUS_INVALID_PARAMETER;
        }
    }
    // Blank vm-disk, just allocate...
    else
    {
        SIZE_T max_size;
#ifdef _WIN64
        max_size = CreateData->DiskSize.QuadPart;
#else
        max_size = CreateData->DiskSize.LowPart;
#endif

        image_buffer = NULL;
        status =
            ZwAllocateVirtualMemory(NtCurrentProcess(),
            (PVOID*)&image_buffer,
            0,
            &max_size,
            MEM_COMMIT,
            PAGE_READWRITE);
        if (!NT_SUCCESS(status))
        {
            KdPrint
                (("PhDskMnt: Error allocating virtual memory for vm disk (%#x).\n",
                status));

            ImScsiLogError((pMPDrvInfoGlobal->pDriverObj,
                0,
                0,
                NULL,
                0,
                1000,
                status,
                102,
                status,
                0,
                0,
                NULL,
                L"Not enough free memory for VM disk."));

            return STATUS_NO_MEMORY;
        }

        alignment_requirement = FILE_BYTE_ALIGNMENT;
    }

    KdPrint(("PhDskMnt: Done with file/memory checks.\n"));

    // If no device-type specified and size matches common floppy sizes,
    // auto-select FILE_DEVICE_DISK with FILE_FLOPPY_DISKETTE and
    // FILE_REMOVABLE_MEDIA.
    // If still no device-type specified, specify FILE_DEVICE_DISK with no
    // particular characteristics. This will emulate a hard disk partition.
    if (IMSCSI_DEVICE_TYPE(CreateData->Flags) == 0)
        CreateData->Flags |= IMSCSI_DEVICE_TYPE_HD;

    KdPrint(("PhDskMnt: Done with device type selection for floppy sizes.\n"));

    // If some parts of the DISK_GEOMETRY structure are zero, auto-fill with
    // typical values for this type of disk.
    if (IMSCSI_DEVICE_TYPE(CreateData->Flags) == IMSCSI_DEVICE_TYPE_CD)
    {
        if (CreateData->BytesPerSector == 0)
            CreateData->BytesPerSector = SECTOR_SIZE_CD_ROM;

        CreateData->Flags |= IMSCSI_OPTION_REMOVABLE | IMSCSI_OPTION_RO;
    }
    else
    {
        if (CreateData->BytesPerSector == 0)
            CreateData->BytesPerSector = SECTOR_SIZE_HDD;
    }

    KdPrint(("PhDskMnt: Done with disk geometry setup.\n"));

    // Now build real DeviceType and DeviceCharacteristics parameters.
    if (IMSCSI_DEVICE_TYPE(CreateData->Flags) == IMSCSI_DEVICE_TYPE_CD)
        LUExtension->DeviceType = READ_ONLY_DIRECT_ACCESS_DEVICE;
    else
        LUExtension->DeviceType = DIRECT_ACCESS_DEVICE;

    if (IMSCSI_READONLY(CreateData->Flags))
        LUExtension->ReadOnly = TRUE;
    if (IMSCSI_REMOVABLE(CreateData->Flags))
        LUExtension->RemovableMedia = TRUE;

    if (alignment_requirement > CreateData->BytesPerSector)
        CreateData->BytesPerSector = alignment_requirement + 1;

    KdPrint
        (("PhDskMnt: After checks and translations we got this create data:\n"
        "DeviceNumber   = %#x\n"
        "DiskSize       = %I64u\n"
        "ImageOffset    = %I64u\n"
        "SectorSize     = %u\n"
        "Flags          = %#x\n"
        "FileNameLength = %u\n"
        "FileName       = '%.*ws'\n",
        CreateData->DeviceNumber,
        CreateData->DiskSize.QuadPart,
        CreateData->ImageOffset.QuadPart,
        CreateData->BytesPerSector,
        CreateData->Flags,
        CreateData->FileNameLength,
        (int)(CreateData->FileNameLength / sizeof(*CreateData->FileName)),
        CreateData->FileName));

    LUExtension->ObjectName = file_name;

    LUExtension->DiskSize = CreateData->DiskSize;

    while (CreateData->BytesPerSector >>= 1)
        LUExtension->BlockPower++;
    if (LUExtension->BlockPower == 0)
        LUExtension->BlockPower = DEFAULT_BLOCK_POWER;
    CreateData->BytesPerSector = 1UL << LUExtension->BlockPower;

    LUExtension->ImageOffset = CreateData->ImageOffset;

    // VM disk.
    if (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_VM)
        LUExtension->VMDisk = TRUE;
    else
        LUExtension->VMDisk = FALSE;

    LUExtension->ImageBuffer = image_buffer;
    LUExtension->ImageFile = file_handle;

    // Use proxy service.
    if (IMSCSI_TYPE(CreateData->Flags) == IMSCSI_TYPE_PROXY)
    {
        LUExtension->Proxy = proxy;
        LUExtension->UseProxy = TRUE;
    }
    else
        LUExtension->UseProxy = FALSE;

    // If we are going to fake a disk signature if existing one
    // is all zeroes and device is read-only, prepare that fake
    // disk sig here.
    if ((CreateData->Flags & IMSCSI_FAKE_DISK_SIG_IF_ZERO) &&
        IMSCSI_READONLY(CreateData->Flags))
        LUExtension->FakeDiskSignature =
        (RtlRandomEx(&pMPDrvInfoGlobal->RandomSeed) |
        0x80808081UL) & 0xFEFEFEFFUL;

    KeInitializeSpinLock(&LUExtension->RequestListLock);
    InitializeListHead(&LUExtension->RequestList);
    KeInitializeEvent(&LUExtension->RequestEvent, SynchronizationEvent, FALSE);

    KeInitializeEvent(&LUExtension->Initialized, NotificationEvent, FALSE);

    KeInitializeSpinLock(&LUExtension->LastIoLock);

    KeSetEvent(&LUExtension->Initialized, (KPRIORITY)0, FALSE);

    KdPrint(("PhDskMnt::ImScsiCreateLU: Creating worker thread for pLUExt=0x%p.\n",
        LUExtension));

    status = PsCreateSystemThread(
        &thread_handle,
        (ACCESS_MASK)0L,
        NULL,
        NULL,
        NULL,
        ImScsiWorkerThread,
        LUExtension);

    if (!NT_SUCCESS(status))
    {
        DbgPrint("PhDskMnt::ImScsiCreateLU: Cannot create device worker thread. (%#x)\n", status);

        return status;
    }

    KeWaitForSingleObject(
        &LUExtension->Initialized,
        Executive,
        KernelMode,
        FALSE,
        NULL);

    status = ObReferenceObjectByHandle(
        thread_handle,
        FILE_READ_ATTRIBUTES | SYNCHRONIZE,
        *PsThreadType,
        KernelMode,
        (PVOID*)&LUExtension->WorkerThread,
        NULL
        );

    if (!NT_SUCCESS(status))
    {
        LUExtension->WorkerThread = NULL;

        DbgPrint("PhDskMnt::ImScsiCreateLU: Cannot reference device worker thread. (%#x)\n", status);
        KeSetEvent(&LUExtension->StopThread, (KPRIORITY)0, FALSE);
        ZwWaitForSingleObject(thread_handle, FALSE, NULL);
    }
    else
    {
        KdPrint(("PhDskMnt::ImScsiCreateLU: Device created and ready.\n"));
    }

    ZwClose(thread_handle);

    return status;
}


BOOLEAN
ImScsiFillMemoryDisk(pHW_LU_EXTENSION LUExtension)
{
    LARGE_INTEGER byte_offset = LUExtension->ImageOffset;
    IO_STATUS_BLOCK io_status;
    NTSTATUS status;
#ifdef _WIN64
    SIZE_T disk_size = LUExtension->DiskSize.QuadPart;
#else
    SIZE_T disk_size = LUExtension->DiskSize.LowPart;
#endif

    KdPrint(("PhDskMnt: Reading image file into vm disk buffer.\n"));

    status =
        ImScsiSafeReadFile(
        LUExtension->ImageFile,
        &io_status,
        LUExtension->ImageBuffer,
        disk_size,
        &byte_offset);

    ZwClose(LUExtension->ImageFile);
    LUExtension->ImageFile = NULL;

    // Failure to read pre-load image is now considered a fatal error
    if (!NT_SUCCESS(status))
    {
        KdPrint(("PhDskMnt: Failed to read image file (%#x).\n", status));

        return FALSE;
    }

    KdPrint(("PhDskMnt: Image loaded successfully.\n"));

    return TRUE;
}
