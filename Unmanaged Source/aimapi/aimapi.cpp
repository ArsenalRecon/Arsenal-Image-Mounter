
/// aimapi.cpp
/// Implementation of public API routines.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///

// aimapi.cpp : Implements exported functions for managing virtual disks and driver setup.
//

#include "stdafx.h"

#include "..\phdskmnt\inc\ntumapi.h"
#include "..\phdskmnt\inc\phdskmntver.h"

#include <imdisk.h>
#include <imdproxy.h>

#include "aimapi.h"

#include "winstrct.hpp"

#include <shlobj.h>
#include <dbt.h>
#include <cfgmgr32.h>

#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "imdisk.lib")
#pragma comment(lib, "ntdll.lib")

const int max_extent_count = 8;

// Known file format offsets
typedef struct _KNOWN_FORMAT
{
    LPCWSTR Extension;
    LONGLONG Offset;
} KNOWN_FORMAT, *PKNOWN_FORMAT;

KNOWN_FORMAT KnownFormats[] = {
    { L"nrg", 600 << 9 },
    { L"sdi", 8 << 9 }
};

ULONGLONG APIFlags;

AIMAPI_API ULONGLONG
WINAPI
ImScsiGetAPIFlags()
{
    return APIFlags;
}

AIMAPI_API ULONGLONG
WINAPI
ImScsiSetAPIFlags(ULONGLONG Flags)
{
    ULONGLONG old_flags = APIFlags;
    APIFlags = Flags;
    return old_flags;
}

void
WINAPI
ImScsiSetStatusMsg(IN HWND hWnd,
LPCWSTR Text)
{
    WPreserveLastError ple;

    ImScsiDebugMessage(L"%1", Text);

    if (hWnd != NULL)
    {
        SetWindowText(hWnd, Text);
    }
}

int
WINAPI
ImScsiDebugMsgBox(
__in_opt HWND hWnd,
__in_opt LPCWSTR lpText,
__in_opt LPCWSTR lpCaption,
__in UINT uType)
{
    WPreserveLastError ple;

    ImScsiDebugMessage(L"%1", lpText);

    if (hWnd != NULL)
    {
        return MessageBox(hWnd, lpText, lpCaption, uType);
    }
    else
    {
        return 0;
    }
}

int
CDECL
ImScsiMsgBoxPrintF(HWND hWnd, UINT uStyle, LPCWSTR lpTitle,
LPCWSTR lpMessage, ...)
{
    WPreserveLastError ple;

    va_list param_list;
    LPWSTR lpBuf = NULL;
    int msg_result;

    va_start(param_list, lpMessage);

    if (!FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER |
        FORMAT_MESSAGE_FROM_STRING, lpMessage, 0, 0,
        (LPWSTR)&lpBuf, 0, &param_list))
    {
        return 0;
    }

    msg_result = ImScsiDebugMsgBox(hWnd, lpBuf, lpTitle, uStyle);

    LocalFree(lpBuf);

    return msg_result;
}

VOID
WINAPI
ImScsiMsgBoxLastError(HWND hWnd, LPCWSTR Prefix)
{
    WPreserveLastError ple;

    LPWSTR MsgBuf = NULL;

    FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER |
        FORMAT_MESSAGE_FROM_SYSTEM |
        FORMAT_MESSAGE_IGNORE_INSERTS,
        NULL, GetLastError(), 0, (LPWSTR)&MsgBuf, 0, NULL);

    ImScsiMsgBoxPrintF(hWnd, MB_ICONEXCLAMATION,
        L"Arsenal Image Mounter",
        L"%1\r\n\r\n%2", Prefix, MsgBuf);

    LocalFree(MsgBuf);
}

AIMAPI_API HANDLE
WINAPI
ImScsiOpenScsiAdapter(OUT LPBYTE PortNumber)
{
    LPWSTR hwinstances = NULL;
    DWORD length = ImScsiAllocateDeviceInstanceListForService(L"phdskmnt",
        &hwinstances);

    if (length == 0)
    {
        SetLastError(ERROR_FILE_NOT_FOUND);
        return NULL;
    }

    WMem<WCHAR> allocated(hwinstances);

    ULONG name_buffer_size = 519;
    WHeapMem<WCHAR> name_buffer(name_buffer_size,
        HEAP_GENERATE_EXCEPTIONS | HEAP_ZERO_MEMORY);

    for (size_t i = 0;
        i < length;
        i += wcslen(hwinstances) + 1)
    {
        if (hwinstances[i] == 0)
            continue;

        DEVINST devInst;

        auto status = CM_Locate_DevNode(&devInst, hwinstances + i, 0);

        if (status != CR_SUCCESS)
        {
            if (status != CR_NO_SUCH_DEVNODE)
            {
                ImScsiDebugMessage(L"Device '%1!ws!' CM_Locate_DevNode error 0x%2!X!", hwinstances + i,
                    status);
            }

            continue;
        }

        status = CM_Get_DevNode_Registry_Property(devInst,
            CM_DRP_PHYSICAL_DEVICE_OBJECT_NAME, NULL, name_buffer,
            &name_buffer_size, 0);

        if (status != CR_SUCCESS || name_buffer_size <= 2)
        {
            ImScsiDebugMessage(L"Device '%1!ws!' CM_Get_DevNode_Registry_Property error 0x%2!X!", hwinstances + i,
                status);

            continue;
        }

        UNICODE_STRING name;

        RtlInitUnicodeString(&name, name_buffer);

        HANDLE handle = ImDiskOpenDeviceByName(&name,
            GENERIC_READ | GENERIC_WRITE);

        if (handle == INVALID_HANDLE_VALUE)
        {
            return INVALID_HANDLE_VALUE;
        }

        if (!ImScsiCheckDriverVersion(handle))
        {
            NtClose(handle);
            SetLastError(ERROR_REVISION_MISMATCH);
            return INVALID_HANDLE_VALUE;
        }

        if (PortNumber != NULL)
        {
            SCSI_ADDRESS scsi_address;

            DWORD dw;

            if (DeviceIoControl(handle, IOCTL_SCSI_GET_ADDRESS, NULL, 0,
                &scsi_address, sizeof scsi_address, &dw, NULL))
            {
                *PortNumber = scsi_address.PortNumber;
            }
            else
            {
                *PortNumber = IMSCSI_ANY_PORT_NUMBER;
            }
        }

        return handle;
    }

    SetLastError(ERROR_FILE_NOT_FOUND);

    return INVALID_HANDLE_VALUE;
}

AIMAPI_API HANDLE
WINAPI
ImScsiOpenScsiAdapterByScsiPortNumber(IN BYTE PortNumber)
{
    WMem<WCHAR> target(ImDiskAllocPrintF(L"\\??\\Scsi%1!u!:",
        (DWORD)PortNumber));

    if (!target)
    {
        return INVALID_HANDLE_VALUE;
    }

    UNICODE_STRING name;
    RtlInitUnicodeString(&name, target);

    HANDLE handle = ImDiskOpenDeviceByName(&name,
        GENERIC_READ | GENERIC_WRITE);

    if (handle == INVALID_HANDLE_VALUE)
    {
        return INVALID_HANDLE_VALUE;
    }

    SRB_IMSCSI_CHECK check;
    ImScsiInitializeSrbIoBlock(&check.SrbIoControl, sizeof(check),
        SMP_IMSCSI_CHECK, 0);

    DWORD dw;
    if (!DeviceIoControl(handle,
        IOCTL_SCSI_MINIPORT,
        &check,
        sizeof(check),
        &check,
        sizeof(check),
        &dw,
        NULL))
    {
        NtClose(handle);
        SetLastError(ERROR_FILE_NOT_FOUND);
        return INVALID_HANDLE_VALUE;
    }

    return handle;
}

AIMAPI_API
BOOL
WINAPI
ImScsiVolumeUsesDisk(IN HANDLE Volume,
IN DWORD DiskNumber)
{
    WHeapMem<VOLUME_DISK_EXTENTS> disk_extents(
        FIELD_OFFSET(VOLUME_DISK_EXTENTS, Extents) + 8 * sizeof(DISK_EXTENT),
        HEAP_GENERATE_EXCEPTIONS);

    DWORD dw;

    if (!DeviceIoControl(Volume,
        IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
        NULL, 0,
        disk_extents, (DWORD)disk_extents.GetSize(),
        &dw, NULL))
    {
        return FALSE;
    }

    BOOL found = FALSE;
    for (int i = 0;
        (i < (int)disk_extents->NumberOfDiskExtents) &&
        ((LPBYTE)(disk_extents->Extents + i + 1) -
        (LPBYTE)(LPVOID)disk_extents <= (int)dw); i++)
    {
        if (disk_extents->Extents[i].DiskNumber == DiskNumber)
        {
            found = TRUE;
            break;
        }
    }

    SetLastError(NO_ERROR);

    return found;
}

AIMAPI_API
BOOL
WINAPI
ImScsiGetDeviceNumbersForVolume(IN HANDLE Volume,
    IN DWORD PortNumber,
    OUT PDEVICE_NUMBER DeviceNumbers,
    IN DWORD NumberOfItems,
    OUT LPDWORD NeededNumberOfItems)
{
    if (PortNumber > MAXBYTE)
    {
        SetLastError(ERROR_INVALID_PARAMETER);
        return FALSE;
    }

    WHeapMem<BYTE> port_numbers(NumberOfItems, HEAP_GENERATE_EXCEPTIONS);

    if (!ImScsiGetDeviceNumbersForVolumeEx(Volume, DeviceNumbers,
        port_numbers, NumberOfItems, NeededNumberOfItems))
    {
        return FALSE;
    }

    if (NumberOfItems > * NeededNumberOfItems)
    {
        NumberOfItems = *NeededNumberOfItems;
    }

    if (PortNumber == IMSCSI_ANY_PORT_NUMBER)
    {
        return TRUE;
    }

    for (DWORD i = 0; i < NumberOfItems; )
    {
        if (port_numbers[i] == PortNumber)
        {
            i++;
        }
        else
        {
            --NumberOfItems;
            --* NeededNumberOfItems;

            if (NumberOfItems > 0)
            {
                DeviceNumbers[i] = DeviceNumbers[NumberOfItems];
                port_numbers[i] = port_numbers[NumberOfItems];
            }
        }
    }

    return TRUE;
}

AIMAPI_API
BOOL
WINAPI
ImScsiGetDeviceNumbersForVolumeEx(IN HANDLE Volume,
    OUT PDEVICE_NUMBER DeviceNumbers,
    OUT LPBYTE PortNumbers,
    IN DWORD NumberOfItems,
    OUT LPDWORD NeededNumberOfItems)
{
    WHeapMem<VOLUME_DISK_EXTENTS> disk_extents(
        FIELD_OFFSET(VOLUME_DISK_EXTENTS, Extents) +
        NumberOfItems * sizeof(DISK_EXTENT),
        HEAP_GENERATE_EXCEPTIONS);

    DWORD dw;

    *NeededNumberOfItems = 0;

    if (!DeviceIoControl(Volume,
        IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
        NULL, 0,
        disk_extents, (DWORD)disk_extents.GetSize(),
        &dw, NULL))
    {
        return FALSE;
    }

    for (int i = 0;
        (i < (int)disk_extents->NumberOfDiskExtents) &&
        ((LPBYTE)(disk_extents->Extents + i + 1) -
        (LPBYTE)(LPVOID)disk_extents <= (int)dw); i++)
    {
        WMem<WCHAR> disk_path(ImDiskAllocPrintF(L"\\\\?\\PhysicalDrive%1!u!",
            disk_extents->Extents[i].DiskNumber));

        if (!disk_path)
        {
            return FALSE;
        }

        HANDLE disk = CreateFile(disk_path, GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL);

        if (disk == INVALID_HANDLE_VALUE)
        {
            WPreserveLastError le;

            WErrMsg errmsg;

            ImScsiDebugMessage(
                L"Error opening %1!ws!: %2!ws!%%n", disk_path, (LPCWSTR)errmsg);

            continue;
        }

        DEVICE_NUMBER device_number;
        BYTE port_number;

        if (!ImScsiGetDeviceNumberForDiskEx(disk, &device_number, &port_number))
        {
            CloseHandle(disk);

            SetLastError(ERROR_INVALID_FUNCTION);

            ImScsiDebugMessage(
                L"Device %1!ws! is not an Arsenal Image Mounter device.",
                disk_path);

            continue;
        }

        CloseHandle(disk);

        if (*NeededNumberOfItems < NumberOfItems)
        {
            DeviceNumbers[*NeededNumberOfItems] = device_number;
            PortNumbers[*NeededNumberOfItems] = port_number;
        }

        ++*NeededNumberOfItems;
    }

    if (*NeededNumberOfItems > NumberOfItems)
    {
        SetLastError(ERROR_MORE_DATA);
        return FALSE;
    }

    if (*NeededNumberOfItems == 0)
    {
        SetLastError(ERROR_INVALID_FUNCTION);
        return FALSE;
    }

    return TRUE;
}

AIMAPI_API
BOOL
WINAPI
ImScsiGetScsiAddressesForVolume(IN HANDLE Volume,
    OUT PSCSI_ADDRESS ScsiAddresses,
    IN DWORD NumberOfItems,
    OUT LPDWORD NeededNumberOfItems)
{
    WHeapMem<DWORD> disk_numbers(NumberOfItems * sizeof(DWORD),
        HEAP_GENERATE_EXCEPTIONS);

    return ImScsiGetScsiAddressesForVolumeEx(Volume, ScsiAddresses,
        disk_numbers, NumberOfItems, NeededNumberOfItems);
}

AIMAPI_API
BOOL
WINAPI
ImScsiGetScsiAddressesForVolumeEx(IN HANDLE Volume,
OUT PSCSI_ADDRESS ScsiAddresses,
OUT LPDWORD DiskNumbers,
IN DWORD NumberOfItems,
OUT LPDWORD NeededNumberOfItems)
{
    WHeapMem<VOLUME_DISK_EXTENTS> disk_extents(
        FIELD_OFFSET(VOLUME_DISK_EXTENTS, Extents) +
        NumberOfItems * sizeof(DISK_EXTENT),
        HEAP_GENERATE_EXCEPTIONS);

    *NeededNumberOfItems = 0;

    DWORD dw;

    if (!DeviceIoControl(Volume,
        IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS,
        NULL, 0,
        disk_extents, (DWORD)disk_extents.GetSize(),
        &dw, NULL))
    {
        return FALSE;
    }

    for (int i = 0;
        (i < (int)disk_extents->NumberOfDiskExtents) &&
        ((LPBYTE)(disk_extents->Extents + i + 1) -
        (LPBYTE)(LPVOID)disk_extents <= (int)dw); i++)
    {
        WMem<WCHAR> disk_path(ImDiskAllocPrintF(L"\\\\?\\PhysicalDrive%1!u!",
            disk_extents->Extents[i].DiskNumber));

        if (!disk_path)
        {
            return FALSE;
        }

        HANDLE disk = CreateFile(disk_path, GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL);

        if (disk == INVALID_HANDLE_VALUE)
        {
            return FALSE;
        }

        SCSI_ADDRESS address;
        STORAGE_DEVICE_NUMBER device_number;

        if (!ImScsiGetScsiAddressForDiskEx(disk, &address, &device_number))
        {
            CloseHandle(disk);
            return FALSE;
        }

        CloseHandle(disk);

        if (*NeededNumberOfItems < NumberOfItems)
        {
            DiskNumbers[*NeededNumberOfItems] =
                disk_extents->Extents[i].DiskNumber;

            ScsiAddresses[*NeededNumberOfItems] = address;
        }

        ++*NeededNumberOfItems;
    }

    if (*NeededNumberOfItems > NumberOfItems)
    {
        SetLastError(ERROR_MORE_DATA);
        return FALSE;
    }

    return TRUE;
}

AIMAPI_API
HANDLE
WINAPI
ImScsiOpenDiskByDeviceNumber(IN DEVICE_NUMBER DeviceNumber,
    IN DWORD PortNumber,
    OUT LPDWORD DiskNumber OPTIONAL)
{
    if (PortNumber > MAXBYTE)
    {
        SetLastError(ERROR_INVALID_PARAMETER);
        return INVALID_HANDLE_VALUE;
    }

    STORAGE_DEVICE_NUMBER device_number;

    HANDLE device = ImScsiOpenDiskByDeviceNumberEx(DeviceNumber,
        (LPBYTE)&PortNumber, &device_number);

    if (device == INVALID_HANDLE_VALUE)
    {
        return INVALID_HANDLE_VALUE;
    }

    if (DiskNumber != NULL)
    {
        *DiskNumber = device_number.DeviceNumber;
    }

    return device;
}

AIMAPI_API
HANDLE
WINAPI
ImScsiOpenDiskByDeviceNumberEx(IN DEVICE_NUMBER DeviceNumber,
    IN OUT LPBYTE PortNumber,
    OUT PSTORAGE_DEVICE_NUMBER DiskNumber OPTIONAL)
{
    LPWSTR hwinstances = NULL;
    DWORD length = ImScsiAllocateDeviceInstanceListForService(L"phdskmnt",
        &hwinstances);

    if (length == 0)
    {
        return 0;
    }

    WMem<WCHAR> allocated(hwinstances);

    ULONG name_buffer_size = 519;
    WHeapMem<WCHAR> dev_path(name_buffer_size,
        HEAP_GENERATE_EXCEPTIONS | HEAP_ZERO_MEMORY);

    for (size_t i = 0;
        i < length;
        i += wcslen(hwinstances) + 1)
    {
        if (hwinstances[i] == 0)
            continue;

        DEVINST devInst;

        auto status = CM_Locate_DevNode(&devInst, hwinstances + i, 0);

        if (status != CR_SUCCESS)
        {
            if (status != CR_NO_SUCH_DEVNODE)
            {
                ImScsiDebugMessage(L"Device '%1!ws!' CM_Locate_DevNode error 0x%2!X!", hwinstances + i,
                    status);
            }

            continue;
        }

        DEVINST child;

        for (status = CM_Get_Child(&child, devInst, 0);
            status == CR_SUCCESS;
            status = CM_Get_Sibling(&child, child, 0))
        {
            status = CM_Get_DevNode_Registry_Property(child,
                CM_DRP_PHYSICAL_DEVICE_OBJECT_NAME, NULL, dev_path,
                &name_buffer_size, 0);

            if (status != CR_SUCCESS || name_buffer_size <= 2)
            {
                ImScsiDebugMessage(L"Device '%1!u!' CM_Get_DevNode_Registry_Property error 0x%2!X!", child, status);
                continue;
            }

            UNICODE_STRING name;

            RtlInitUnicodeString(&name, dev_path);

            HANDLE disk = ImDiskOpenDeviceByName(&name,
                GENERIC_READ | GENERIC_WRITE);

            if (disk == INVALID_HANDLE_VALUE)
            {
                ImScsiDebugMessage(L"Error opening device '%1!ws!', error 0x%2!X!", dev_path, GetLastError());
                continue;
            }

            DWORD dw;

            SCSI_ADDRESS address;

            if (!DeviceIoControl(disk, IOCTL_SCSI_GET_ADDRESS, NULL, 0,
                &address, sizeof(address), &dw, NULL))
            {
#ifndef _DEBUG
                switch (GetLastError())
                {
                case ERROR_INVALID_PARAMETER:
                case ERROR_INVALID_FUNCTION:
                case ERROR_NOT_SUPPORTED:
                case ERROR_IO_DEVICE:
                    break;

                default:
#endif
                {
                    WErrMsg errmsg;

                    ImScsiDebugMessage(L"Cannot get SCSI address of '%1!ws!': %2!ws!",
                        (LPCWSTR)dev_path, (LPCWSTR)errmsg);
                }
#ifndef _DEBUG
                }
#endif

                CloseHandle(disk);
                disk = INVALID_HANDLE_VALUE;
                continue;
            }

            if (((*PortNumber != IMSCSI_ANY_PORT_NUMBER) &&
                (address.PortNumber != *PortNumber)) ||
                (address.PathId != DeviceNumber.PathId) ||
                (address.TargetId != DeviceNumber.TargetId) ||
                (address.Lun != DeviceNumber.Lun))
            {
#ifdef _DEBUG
                ImScsiDebugMessage(
                    L"Disk %1!ws! has port:path:target:lun %2!i!:%3!i!:%4!i!:%5!i!, looking for %6!i!:%7!i!:%8!i!:%9!i!.",
                    (LPCWSTR)dev_path,
                    (int)address.PortNumber, (int)address.PathId, (int)address.TargetId, (int)address.Lun,
                    (int)*PortNumber, (int)DeviceNumber.PathId, (int)DeviceNumber.TargetId, (int)DeviceNumber.Lun);
#endif
                CloseHandle(disk);
                disk = INVALID_HANDLE_VALUE;
                continue;
            }

            STORAGE_DEVICE_NUMBER device_number;

            if (!DeviceIoControl(disk, IOCTL_STORAGE_GET_DEVICE_NUMBER,
                NULL, 0,
                &device_number, sizeof(device_number), &dw, NULL))
            {
                WErrMsg errmsg;

                ImScsiDebugMessage(
                    L"Cannot get storage device number of '%1!ws!': %2!ws!",
                    (LPCWSTR)dev_path, (LPCWSTR)errmsg);

                CloseHandle(disk);
                disk = INVALID_HANDLE_VALUE;
                continue;
            }

            if (((device_number.DeviceType != FILE_DEVICE_DISK) &&
                (device_number.DeviceType != FILE_DEVICE_CD_ROM) ||
                (((LONG)device_number.PartitionNumber) > 0)))
            {
                ImScsiDebugMessage(
                    L"Disk %1!ws! has some unexpected properties: DeviceNumber=%2!u! DeviceType=%3!#x! PartitionNumber=%4!i!",
                    (LPCWSTR)dev_path, device_number.DeviceNumber,
                    device_number.DeviceType, device_number.PartitionNumber);

                    CloseHandle(disk);
                    disk = INVALID_HANDLE_VALUE;

                    continue;
            }

            DeviceIoControl(disk, FSCTL_ALLOW_EXTENDED_DASD_IO, NULL, 0, NULL, 0,
                &dw, NULL);

#ifdef _DEBUG
            LPCWSTR device_type = NULL;

            switch (device_number.DeviceType)
            {
            case FILE_DEVICE_DISK:
                device_type = L"\\\\?\\PhysicalDrive";
                break;

            case FILE_DEVICE_CD_ROM:
                device_type = L"\\\\?\\CdRom";
                break;
            }

            ImScsiDebugMessage(
                L"Device %1!i!:%2!i!:%3!i!:%4!i! is %5!ws!%6!u!",
                (int)address.PortNumber,
                (int)address.PathId,
                (int)address.TargetId,
                (int)address.Lun,
                device_type, device_number.DeviceNumber);
#endif

            if (DiskNumber != NULL)
            {
                *DiskNumber = device_number;
            }

            *PortNumber = address.PortNumber;

            return disk;
        }
    }

    SetLastError(ERROR_FILE_NOT_FOUND);

    return INVALID_HANDLE_VALUE;
}

AIMAPI_API
BOOL
WINAPI
ImScsiCheckDriverVersion(HANDLE Device)
{
    SRB_IMSCSI_QUERY_VERSION check;
    ImScsiInitializeSrbIoBlock(&check.SrbIoControl,
        sizeof(check), SMP_IMSCSI_QUERY_VERSION, 0);

    DWORD dw;
    if (!DeviceIoControl(Device,
        IOCTL_SCSI_MINIPORT,
        &check, sizeof(check),
        &check, sizeof(check),
        &dw, NULL))
    {
        return FALSE;
    }

    SetLastError(NO_ERROR);

    if (dw < sizeof(check))
    {
        SetLastError(ERROR_INVALID_FUNCTION);
        return FALSE;
    }

    if (check.SrbIoControl.ReturnCode != IMSCSI_DRIVER_VERSION)
    {
        SetLastError(ERROR_REVISION_MISMATCH);
        return FALSE;
    }

    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiDeviceIoControl(HANDLE Device,
DWORD ControlCode,
PSRB_IO_CONTROL SrbIoControl,
DWORD Size,
DWORD Timeout,
LPDWORD ReturnLength)
{
    ImScsiInitializeSrbIoBlock(SrbIoControl,
        Size, ControlCode, Timeout);

    if (!DeviceIoControl(Device,
        IOCTL_SCSI_MINIPORT,
        SrbIoControl, Size,
        SrbIoControl, Size,
        ReturnLength,
        NULL))
    {
        return FALSE;
    }

    SetLastError(RtlNtStatusToDosError(SrbIoControl->ReturnCode));

    return NT_SUCCESS(SrbIoControl->ReturnCode);
}

AIMAPI_API BOOL
WINAPI
ImScsiGetVersion(PULONG LibraryVersion,
PULONG DriverVersion)
{
    if (LibraryVersion != NULL)
        *LibraryVersion = PHDSKMNT_VERSION_ULONG;

    if (DriverVersion != NULL)
    {
        HANDLE driver;
        DWORD dw;

        *DriverVersion = 0;

        for (;;)
        {
            driver = ImScsiOpenScsiAdapter(NULL);

            if (driver != INVALID_HANDLE_VALUE)
                break;

            return FALSE;
        }

        SRB_IMSCSI_QUERY_VERSION check;
        ImScsiInitializeSrbIoBlock(&check.SrbIoControl,
            sizeof(check), SMP_IMSCSI_QUERY_VERSION, 0);

        if (!DeviceIoControl(driver,
            IOCTL_SCSI_MINIPORT,
            &check, sizeof(check),
            &check, sizeof(check),
            &dw, NULL))
        {
            NtClose(driver);
            return FALSE;
        }

        NtClose(driver);

        if (dw < sizeof(ULONG))
            return FALSE;

        *DriverVersion = check.SubVersion;
    }

    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiGetDeviceList(IN ULONG ListLength,
IN HANDLE Adapter,
OUT PDEVICE_NUMBER DeviceList,
OUT PULONG NumberOfDevices)
{
    WHeapMem<SRB_IMSCSI_QUERY_ADAPTER> buffer(
        FIELD_OFFSET(SRB_IMSCSI_QUERY_ADAPTER, DeviceList) +
        ListLength * sizeof(*DeviceList), HEAP_GENERATE_EXCEPTIONS);

    ULONG dw;

    if (!ImScsiDeviceIoControl(Adapter,
        SMP_IMSCSI_QUERY_ADAPTER,
        &buffer->SrbIoControl, (DWORD)buffer.GetSize(),
        0, &dw))
    {
        return FALSE;
    }

    if (dw < FIELD_OFFSET(SRB_IMSCSI_QUERY_ADAPTER, DeviceList))
    {
        SetLastError(ERROR_INVALID_PARAMETER);
        return FALSE;
    }

    *NumberOfDevices = buffer->NumberOfDevices;

    if (ListLength > 0)
    {
        RtlCopyMemory(DeviceList, buffer->DeviceList,
            sizeof(DEVICE_NUMBER) * min(ListLength,
            buffer->NumberOfDevices));
    }

    if ((FIELD_OFFSET(SRB_IMSCSI_QUERY_ADAPTER, DeviceList) +
        (*NumberOfDevices * sizeof(DEVICE_NUMBER))) > dw)
    {
        SetLastError(ERROR_MORE_DATA);
        return FALSE;
    }
    else
    {
        SetLastError(NO_ERROR);
        return TRUE;
    }
}

AIMAPI_API BOOL
WINAPI
ImScsiQueryDevice(IN HANDLE Adapter,
IN OUT PIMSCSI_DEVICE_CONFIGURATION Config,
IN ULONG ConfigSize)
{
    DWORD create_data_size =
        FIELD_OFFSET(SRB_IMSCSI_CREATE_DATA, Fields) +
        ConfigSize;

    WHeapMem<SRB_IMSCSI_CREATE_DATA> create_data(create_data_size,
        HEAP_GENERATE_EXCEPTIONS);

    create_data->Fields.DeviceNumber = Config->DeviceNumber;

    DWORD dw;

    if (!ImScsiDeviceIoControl(Adapter,
        SMP_IMSCSI_QUERY_DEVICE,
        &create_data->SrbIoControl,
        create_data_size,
        0, &dw))
    {
        return FALSE;
    }

    if (dw < (DWORD)FIELD_OFFSET(SRB_IMSCSI_CREATE_DATA, Fields.FileName))
    {
        SetLastError(ERROR_INVALID_PARAMETER);
        return FALSE;
    }

    RtlCopyMemory(Config, &create_data->Fields,
        min(dw - FIELD_OFFSET(SRB_IMSCSI_CREATE_DATA, Fields),
        ConfigSize));

    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiCreateDeviceEx(IN HWND hWnd OPTIONAL,
    IN HANDLE Adapter OPTIONAL,
    IN OUT PDEVICE_NUMBER DeviceNumber OPTIONAL,
    IN OUT PLARGE_INTEGER DiskSize OPTIONAL,
    IN OUT LPDWORD BytesPerSector OPTIONAL,
    IN PLARGE_INTEGER ImageOffset OPTIONAL,
    IN OUT LPDWORD Flags OPTIONAL,
    IN LPCWSTR FileName OPTIONAL,
    IN LPCWSTR WriteOverlayFileName OPTIONAL,
    IN BOOL NativePath,
    IN LPWSTR MountPoint OPTIONAL,
    IN BOOL CreatePartition)
{
    if (Adapter == INVALID_HANDLE_VALUE)
    {
        ImScsiSetStatusMsg(hWnd, L"Opening Arsenal Image Mounter...");

        Adapter = ImScsiOpenScsiAdapter(NULL);

        if (Adapter == INVALID_HANDLE_VALUE)
        {
            return FALSE;
        }
    }

    DWORD dw;

    if (!ImScsiCheckDriverVersion(Adapter))
    {
        ImScsiDebugMsgBox(hWnd,
            L"The version of Arsenal Image Mounter driver "
            L"installed on this system does not match "
            L"the version of this API library. Please reinstall "
            L"Arsenal Image Mounter to make sure that all components of "
            L"it on this "
            L"system are from the same install package. You may have "
            L"to restart your computer if you still see this message "
            L"after reinstalling.",
            L"Arsenal Image Mounter", MB_ICONSTOP);
    }

    // Physical memory allocation requires the AWEAlloc driver.
    if (((IMSCSI_TYPE(*Flags) == IMSCSI_TYPE_FILE) ||
        (IMSCSI_TYPE(*Flags) == 0)) &&
        (IMSCSI_FILE_TYPE(*Flags) == IMSCSI_FILE_TYPE_AWEALLOC))
    {
        HANDLE awealloc;
        UNICODE_STRING file_name;

        RtlInitUnicodeString(&file_name, AWEALLOC_DEVICE_NAME);

        for (;;)
        {
            awealloc = ImDiskOpenDeviceByName(&file_name,
                GENERIC_READ | GENERIC_WRITE);

            if (awealloc != INVALID_HANDLE_VALUE)
            {
                NtClose(awealloc);
                break;
            }

            if (GetLastError() != ERROR_FILE_NOT_FOUND)
                break;

            if (ImDiskStartService(AWEALLOC_DRIVER_NAME))
            {
                continue;
            }

            switch (GetLastError())
            {
            case ERROR_SERVICE_DOES_NOT_EXIST:

                ImScsiDebugMsgBox(hWnd,
                    L"The AWEAlloc driver is not installed. Please "
                    L"install ImDisk Virtual Disk Driver.",
                    L"Arsenal Image Mounter", MB_ICONSTOP);
                break;

            case ERROR_PATH_NOT_FOUND:
            case ERROR_FILE_NOT_FOUND:

                ImScsiDebugMsgBox(hWnd,
                    L"Cannot load the AWEAlloc driver. Please "
                    L"install ImDisk Virtual Disk Driver.",
                    L"Arsenal Image Mounter", MB_ICONSTOP);
                break;

            case ERROR_SERVICE_DISABLED:

                ImScsiDebugMsgBox(hWnd,
                    L"The AWEAlloc driver is disabled.",
                    L"Arsenal Image Mounter", MB_ICONSTOP);
                break;

            default:

                ImScsiMsgBoxLastError(hWnd, L"Error loading AWEAlloc driver:");
            }

            return FALSE;
        }
    }
    // Proxy reconnection types requires the user mode service.
    else if ((IMSCSI_TYPE(*Flags) == IMSCSI_TYPE_PROXY) &&
        ((IMSCSI_PROXY_TYPE(*Flags) == IMSCSI_PROXY_TYPE_TCP) ||
        (IMSCSI_PROXY_TYPE(*Flags) == IMSCSI_PROXY_TYPE_COMM)))
    {
        if (!WaitNamedPipe(IMDPROXY_SVC_PIPE_DOSDEV_NAME, 0) &&
            GetLastError() == ERROR_FILE_NOT_FOUND)
        {
            if (ImDiskStartService(IMDPROXY_SVC))
            {
                while (!WaitNamedPipe(IMDPROXY_SVC_PIPE_DOSDEV_NAME, 0))
                    if (GetLastError() == ERROR_FILE_NOT_FOUND)
                        Sleep(500);
                    else
                        break;

                ImScsiSetStatusMsg
                (hWnd,
                    L"ImDisk Virtual Disk Driver Helper Service started.");
            }
            else
            {
                switch (GetLastError())
                {
                case ERROR_SERVICE_DOES_NOT_EXIST:

                    ImScsiDebugMsgBox(hWnd,
                        L"The ImDisk Virtual Disk Driver Helper "
                        L"Service is not installed. Please install "
                        L"ImDisk Virtual Disk Driver.",
                        L"Arsenal Image Mounter", MB_ICONSTOP);
                    break;

                case ERROR_PATH_NOT_FOUND:
                case ERROR_FILE_NOT_FOUND:

                    ImScsiDebugMsgBox(hWnd,
                        L"Cannot start the ImDisk Virtual Disk Driver "
                        L"Helper Service. Please install ImDisk Virtual Disk Driver.",
                        L"Arsenal Image Mounter", MB_ICONSTOP);
                    break;

                case ERROR_SERVICE_DISABLED:

                    ImScsiDebugMsgBox(hWnd,
                        L"The ImDisk Virtual Disk Driver Helper "
                        L"Service is disabled.",
                        L"Arsenal Image Mounter", MB_ICONSTOP);
                    break;

                default:

                    ImScsiMsgBoxLastError
                    (hWnd,
                        L"Error starting ImDisk Virtual Disk Driver Helper "
                        L"Service:");
                }

                return FALSE;
            }
        }
    }

    UNICODE_STRING file_name;

    if (FileName == NULL)
    {
        RtlInitUnicodeString(&file_name, NULL);
    }
    else if (NativePath)
    {
        if (!RtlCreateUnicodeString(&file_name, FileName))
        {

            ImScsiDebugMsgBox(hWnd, L"Memory allocation error.",
                L"Arsenal Image Mounter", MB_ICONSTOP);
            return FALSE;
        }
    }
    else if ((IMSCSI_TYPE(*Flags) == IMSCSI_TYPE_PROXY) &&
        (IMSCSI_PROXY_TYPE(*Flags) == IMSCSI_PROXY_TYPE_SHM))
    {
        LPWSTR namespace_prefix;

        HANDLE h = CreateFile(L"\\\\?\\Global", 0, FILE_SHARE_READ, NULL,
            OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);

        if ((h == INVALID_HANDLE_VALUE) &&
            (GetLastError() == ERROR_FILE_NOT_FOUND))
        {
            namespace_prefix = L"\\BaseNamedObjects\\";
        }
        else
        {
            namespace_prefix = L"\\BaseNamedObjects\\Global\\";
        }

        if (h != INVALID_HANDLE_VALUE)
        {
            CloseHandle(h);
        }

        WHeapMem<WCHAR> prefixed_name(
            ((wcslen(namespace_prefix) + wcslen(FileName)) << 1) + 1,
            HEAP_GENERATE_EXCEPTIONS);

        wcscpy(prefixed_name, namespace_prefix);
        wcscat(prefixed_name, FileName);

        if (!RtlCreateUnicodeString(&file_name, prefixed_name))
        {
            ImScsiDebugMsgBox(hWnd, L"Memory allocation error.",
                L"Arsenal Image Mounter", MB_ICONSTOP);
            return FALSE;
        }
    }
    else
    {
        if (!RtlDosPathNameToNtPathName_U(FileName, &file_name, NULL, NULL))
        {
            ImScsiDebugMsgBox(hWnd, L"Memory allocation error.",
                L"Arsenal Image Mounter", MB_ICONSTOP);
            return FALSE;
        }
    }

    UNICODE_STRING write_overlay_file_name;

    if (WriteOverlayFileName == NULL)
    {
        RtlInitUnicodeString(&write_overlay_file_name, NULL);
    }
    else if (NativePath)
    {
        if (!RtlCreateUnicodeString(&write_overlay_file_name, WriteOverlayFileName))
        {
            ImScsiDebugMsgBox(hWnd, L"Memory allocation error.",
                L"Arsenal Image Mounter", MB_ICONSTOP);
            return FALSE;
        }
    }
    else
    {
        if (!RtlDosPathNameToNtPathName_U(WriteOverlayFileName, &write_overlay_file_name, NULL, NULL))
        {
            ImScsiDebugMsgBox(hWnd, L"Memory allocation error.",
                L"Arsenal Image Mounter", MB_ICONSTOP);
            return FALSE;
        }
    }

    ImScsiSetStatusMsg(hWnd, L"Creating virtual disk...");

    WHeapMem<SRB_IMSCSI_CREATE_DATA> create_data(
        sizeof(SRB_IMSCSI_CREATE_DATA) + file_name.Length +
        write_overlay_file_name.Length,
        HEAP_GENERATE_EXCEPTIONS | HEAP_ZERO_MEMORY);

    if (DeviceNumber != NULL)
    {
        create_data->Fields.DeviceNumber = *DeviceNumber;
    }
    else
    {
        create_data->Fields.DeviceNumber.LongNumber =
            IMSCSI_AUTO_DEVICE_NUMBER;
    }

    if (ImageOffset != NULL)
        create_data->Fields.ImageOffset = *ImageOffset;

    if (DiskSize != NULL)
        create_data->Fields.DiskSize = *DiskSize;

    if (BytesPerSector != NULL)
        create_data->Fields.BytesPerSector = *BytesPerSector;

    if (Flags != NULL)
        create_data->Fields.Flags = *Flags;

    if (file_name.Length != 0)
    {
        create_data->Fields.FileNameLength = file_name.Length;
        
        memcpy(&create_data->Fields.FileName,
            file_name.Buffer,
            file_name.Length);
        
        RtlFreeUnicodeString(&file_name);
    }

    if (write_overlay_file_name.Length != 0)
    {
        create_data->Fields.WriteOverlayFileNameLength =
            write_overlay_file_name.Length;

        memcpy(((PUCHAR)&create_data->Fields.FileName) +
            create_data->Fields.FileNameLength,
            write_overlay_file_name.Buffer,
            write_overlay_file_name.Length);
        
        RtlFreeUnicodeString(&write_overlay_file_name);
    }

    if (!ImScsiDeviceIoControl(Adapter,
        SMP_IMSCSI_CREATE_DEVICE,
        &create_data->SrbIoControl,
        (DWORD)create_data.GetSize(),
        0, &dw))
    {

        ImScsiMsgBoxLastError(hWnd, L"Error creating virtual disk:");

        return FALSE;
    }

    if (!ImScsiRescanScsiAdapter())
    {
        WErrMsg errmsg;

        ImScsiDebugMessage(L"SCSI bus rescan error: %1!ws!", (LPCWSTR)errmsg);
    }

    if (DeviceNumber != NULL)
        *DeviceNumber = create_data->Fields.DeviceNumber;

    if (ImageOffset != NULL)
        *ImageOffset = create_data->Fields.ImageOffset;

    if (DiskSize != NULL)
        *DiskSize = create_data->Fields.DiskSize;

    if (BytesPerSector != NULL)
        *BytesPerSector = create_data->Fields.BytesPerSector;

    if (Flags != NULL)
        *Flags = create_data->Fields.Flags;

    if ((MountPoint == NULL) && !CreatePartition)
    {
        return TRUE;
    }

    STORAGE_DEVICE_NUMBER disk_number;

    for (;;)
    {
        BYTE port_number = IMSCSI_ANY_PORT_NUMBER;

        HANDLE disk = ImScsiOpenDiskByDeviceNumberEx(
            create_data->Fields.DeviceNumber, &port_number,
            &disk_number);

        if (disk != INVALID_HANDLE_VALUE)
        {
            CloseHandle(disk);
            break;
        }

        if (hWnd == NULL)
        {
            ImScsiRescanScsiAdapter();
        }
        else
        {
            HANDLE event = ImScsiRescanScsiAdapterAsync(TRUE);

            if (event == NULL)
            {
                return FALSE;
            }

            while (MsgWaitForMultipleObjects(1, &event, FALSE, INFINITE,
                QS_ALLEVENTS) == WAIT_OBJECT_0 + 1)
            {
                ImScsiSetStatusMsg(hWnd, L"Scanning for attached disk...");
                ImDiskFlushWindowMessages(NULL);
            }

            CloseHandle(event);
        }
    }

    WMem<WCHAR> disk_path(ImDiskAllocPrintF(L"\\\\?\\PhysicalDrive%1!u!",
        disk_number));

    if (!disk_path)
    {
        return FALSE;
    }

    HANDLE disk = CreateFile(disk_path,
        GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
        NULL, OPEN_EXISTING, 0, NULL);

    if (disk == INVALID_HANDLE_VALUE)
    {
        ImScsiMsgBoxLastError(hWnd,
            L"Error opening attached disk in read/write mode");
    }
    else
    {
        DeviceIoControl(disk, FSCTL_ALLOW_EXTENDED_DASD_IO, NULL, 0, NULL, 0,
            &dw, NULL);

        GET_LENGTH_INFORMATION disk_size;
        if (DeviceIoControl(disk, IOCTL_DISK_GET_LENGTH_INFO, NULL, 0,
            &disk_size, sizeof(disk_size), &dw, NULL))
        {
            LONGLONG diff = disk_size.Length.QuadPart -
                create_data->Fields.DiskSize.QuadPart;
            if ((diff > create_data->Fields.BytesPerSector) ||
                (diff < -(LONG)create_data->Fields.BytesPerSector))
            {
                ImScsiDebugMessage(
                    L"Disk %1!ws! has unexpected size: %2!I64u!",
                    (LPCWSTR)disk_path, disk_size.Length.QuadPart);

                CreatePartition = FALSE;
            }
        }
        else if (GetLastError() != ERROR_INVALID_FUNCTION)
        {
            WErrMsg errmsg;

            ImScsiDebugMessage(
                L"Cannot query size of disk %1!ws!: %2!ws!",
                (LPCWSTR)disk_path, (LPCWSTR)errmsg);

            CreatePartition = FALSE;
        }

        if (CreatePartition)
        {
            ImScsiSetStatusMsg(hWnd, L"Creating partition...");

#pragma warning(suppress: 28159)
            ULONG rand_seed = GetTickCount();

            for (;;)
            {
                DRIVE_LAYOUT_INFORMATION drive_layout = { 0 };
                drive_layout.Signature = RtlRandom(&rand_seed);
                drive_layout.PartitionCount = 1;
                drive_layout.PartitionEntry[0].StartingOffset.QuadPart = 1048576;
                drive_layout.PartitionEntry[0].PartitionLength.QuadPart =
                    create_data->Fields.DiskSize.QuadPart -
                    drive_layout.PartitionEntry[0].StartingOffset.QuadPart;
                drive_layout.PartitionEntry[0].PartitionNumber = 1;
                drive_layout.PartitionEntry[0].PartitionType = PARTITION_IFS;
                drive_layout.PartitionEntry[0].BootIndicator = TRUE;
                drive_layout.PartitionEntry[0].RecognizedPartition = TRUE;
                drive_layout.PartitionEntry[0].RewritePartition = TRUE;

                if (DeviceIoControl(disk, IOCTL_DISK_SET_DRIVE_LAYOUT, &drive_layout,
                    sizeof(drive_layout), NULL, 0, &dw, NULL))
                {
                    break;
                }

                if (IMSCSI_READONLY(create_data->Fields.Flags) ||
                    (GetLastError() != ERROR_WRITE_PROTECT))
                {
                    ImScsiMsgBoxLastError(hWnd, L"Error creating partition:");

                    CloseHandle(disk);

                    return FALSE;
                }

                ImScsiSetStatusMsg(hWnd, L"Disk not yet ready, waiting...");

                SET_DISK_ATTRIBUTES disk_attributes = { sizeof(disk_attributes) };
                disk_attributes.AttributesMask =
                    DISK_ATTRIBUTE_OFFLINE | DISK_ATTRIBUTE_READ_ONLY;

                if (!DeviceIoControl(disk, IOCTL_DISK_SET_DISK_ATTRIBUTES,
                    &disk_attributes, sizeof(disk_attributes),
                    NULL, 0,
                    &dw, NULL))
                {
                    Sleep(400);
                }
                else
                {
                    Sleep(0);
                }
            }

            ImScsiSetStatusMsg(hWnd, L"Successfully created partition.");
        }

        if ((!DeviceIoControl(disk, IOCTL_DISK_UPDATE_PROPERTIES,
            NULL, 0, NULL, 0, &dw, NULL)) &&
            (GetLastError() != ERROR_INVALID_FUNCTION))
        {
            ImScsiMsgBoxLastError(hWnd,
                L"Error updating disk properties");
        }

        CloseHandle(disk);
    }

    ImScsiSetStatusMsg(hWnd, L"Scanning for attached disk volumes...");

    WCHAR vol_name[50];

    HANDLE vol_hanle = FindFirstVolume(vol_name, _countof(vol_name));

    if (vol_hanle == INVALID_HANDLE_VALUE)
    {
        return FALSE;
    }

    do
    {
        vol_name[48] = 0;

        HANDLE vol_handle = CreateFile(vol_name, 0,
            FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0,
            NULL);

        if (vol_handle == INVALID_HANDLE_VALUE)
        {
            continue;
        }

        if (!ImScsiVolumeUsesDisk(vol_handle, disk_number.DeviceNumber))
        {
            CloseHandle(vol_handle);
            continue;
        }

        CloseHandle(vol_handle);

        ImScsiDebugMessage(L"Attached disk volume %1!ws!",
            (LPCWSTR)vol_name);

        vol_handle = CreateFile(vol_name, GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0,
            NULL);

        if (vol_handle != INVALID_HANDLE_VALUE)
        {
            if (!DeviceIoControl(vol_handle, IOCTL_VOLUME_ONLINE,
                NULL, 0, NULL, 0,
                &dw, NULL))
            {
                WErrMsg errmsg;

                ImScsiDebugMessage(
                    L"Failed to online the volume: %1!ws!",
                    (LPCWSTR)errmsg);
            }

            CloseHandle(vol_handle);
        }
        else
        {
            WErrMsg errmsg;

            ImScsiDebugMessage(
                L"Failed to open the volume for writing: %1!ws!",
                (LPCWSTR)errmsg);
        }

        wcscat(vol_name + 48, L"\\");

        if ((MountPoint != NULL) && (MountPoint[0] != 0))
        {
            WMem<WCHAR> mount_point_buffer;

            if (MountPoint[wcslen(MountPoint) - 1] != L'\\')
            {
                mount_point_buffer =
                    ImDiskAllocPrintF(L"%1\\", MountPoint);

                if (mount_point_buffer)
                {
                    MountPoint = mount_point_buffer;
                }
            }

            if ((MountPoint != NULL) && (MountPoint[0] != 0) &&
                (wcscmp(MountPoint + 1, L":\\") == 0))
            {
                WHeapMem<WCHAR> vol_mnt(UNICODE_STRING_MAX_BYTES,
                    HEAP_GENERATE_EXCEPTIONS);

                if (!ImScsiGetVolumePathNamesForVolumeName(vol_name,
                    vol_mnt, (DWORD)vol_mnt.Count(), &dw))
                {
                    ImScsiMsgBoxLastError(hWnd,
                        L"Error enumerating mount points for volume");

                    break;
                }

                bool mount_point_found = false;
                size_t length;
                for (LPWSTR mnt = vol_mnt;
                    (length = wcslen(mnt)) != 0;
                    mnt += length + 1)
                {
                    if ((length == 3) &&
                        (MountPoint != NULL) && (MountPoint[0] != 0) &&
                        (wcscmp(MountPoint + 1, L":\\") == 0) &&
                        (MountPoint[0] != L'#') &&
                        (wcscmp(mnt + 1, L":\\") == 0) &&
                        (wcsicmp(MountPoint, mnt) != 0))
                    {
                        if (!DeleteVolumeMountPoint(mnt))
                        {
                            WErrMsg errmsg;

                            ImScsiMsgBoxPrintF(hWnd,
                                MB_ICONEXCLAMATION,
                                L"Arsenal Image Mounter",
                                L"Error removing old mount point '%1!ws!': %2!ws!",
                                mnt, (LPCWSTR)errmsg);
                        }
                    }
                    else
                    {
                        mount_point_found = true;

                        ImScsiDebugMessage(L"Mounted at %1!ws!", mnt);
                    }
                }

                if ((wcscmp(MountPoint, L"#:\\") != 0) || !mount_point_found)
                {
                    if (wcscmp(MountPoint, L"#:\\") == 0)
                    {
                        MountPoint[0] = ImDiskFindFreeDriveLetter();

                        if (MountPoint[0] == 0)
                        {
                            ImScsiMsgBoxPrintF(hWnd,
                                MB_ICONEXCLAMATION,
                                L"Arsenal Image Mounter",
                                L"All drive letters are in use.");

                            return FALSE;
                        }
                    }

                    if (!SetVolumeMountPoint(MountPoint, vol_name))
                    {
                        WErrMsg errmsg;

                        ImScsiMsgBoxPrintF(hWnd,
                            MB_ICONEXCLAMATION,
                            L"Arsenal Image Mounter",
                            L"Error setting volume '%1!ws!' mount point to '%2!ws!':",
                            vol_name, MountPoint, (LPCWSTR)errmsg);

                        FindVolumeClose(vol_hanle);

                        return FALSE;
                    }
                    else
                    {
                        ImScsiDebugMessage(L"New mount point created at %1!ws!", MountPoint);
                    }
                }
            }

            MountPoint = NULL;
        }

    } while (FindNextVolume(vol_hanle, vol_name, _countof(vol_name)));

    FindVolumeClose(vol_hanle);


    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiCreateDevice(IN HWND hWnd OPTIONAL,
    IN HANDLE Adapter OPTIONAL,
    IN OUT PDEVICE_NUMBER DeviceNumber OPTIONAL,
    IN OUT PLARGE_INTEGER DiskSize OPTIONAL,
    IN OUT LPDWORD BytesPerSector OPTIONAL,
    IN PLARGE_INTEGER ImageOffset OPTIONAL,
    IN OUT LPDWORD Flags OPTIONAL,
    IN LPCWSTR FileName OPTIONAL,
    IN BOOL NativePath,
    IN LPWSTR MountPoint OPTIONAL,
    IN BOOL CreatePartition)
{
    return ImScsiCreateDeviceEx(
        hWnd,
        Adapter,
        DeviceNumber,
        DiskSize,
        BytesPerSector,
        ImageOffset,
        Flags,
        FileName,
        NULL,
        NativePath,
        MountPoint,
        CreatePartition);
}

AIMAPI_API BOOL
WINAPI
ImScsiRemoveDeviceByNumber(HWND hWnd,
HANDLE Adapter,
DEVICE_NUMBER DeviceNumber)
{
    if (DeviceNumber.LongNumber == IMSCSI_ALL_DEVICES)
    {
        ImScsiSetStatusMsg(hWnd,
            L"Removing all devices...");
    }
    else
    {
        LPWSTR msg = ImDiskAllocPrintF(
            L"Removing device %1!.6X!...",
            DeviceNumber.LongNumber);

        ImScsiSetStatusMsg(hWnd, msg);

        LocalFree(msg);
    }

    SRB_IMSCSI_REMOVE_DEVICE remove_device;

    ImScsiInitializeSrbIoBlock(&remove_device.SrbIoControl,
        sizeof(SRB_IMSCSI_REMOVE_DEVICE),
        SMP_IMSCSI_REMOVE_DEVICE, 0);

    remove_device.DeviceNumber = DeviceNumber;

    DWORD dw;

    if (!ImScsiDeviceIoControl(Adapter,
        SMP_IMSCSI_REMOVE_DEVICE,
        &remove_device.SrbIoControl,
        sizeof(remove_device),
        0, &dw))
    {
        ImScsiMsgBoxLastError(hWnd, L"Error removing virtual disk:");

        return FALSE;
    }

    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiRemoveDeviceByMountPoint(HWND hWnd,
LPCWSTR MountPoint)
{
    DWORD dw;
    BOOL force_dismount = FALSE;

    if ((MountPoint[0] != 0) &&
        ((wcscmp(MountPoint + 1, L":") == 0) ||
        (wcscmp(MountPoint + 1, L":\\") == 0)))
    {
        WCHAR drive_letter_path[] = L"\\\\.\\ :";
        drive_letter_path[4] = MountPoint[0];

        // Notify processes that this device is about to be removed.
        if (((APIFlags & IMSCSI_API_NO_BROADCAST_NOTIFY) == 0) &&
            (MountPoint[0] >= L'A') && (MountPoint[0] <= L'Z'))
        {

            ImScsiSetStatusMsg
                (hWnd,
                L"Notifying applications that device is being removed...");

            ImDiskNotifyRemovePending(hWnd, MountPoint[0]);
        }
    }

    ImScsiSetStatusMsg(hWnd, L"Opening device...");

    HANDLE device = ImDiskOpenDeviceByMountPoint(MountPoint,
        GENERIC_READ | GENERIC_WRITE);

    if (device == INVALID_HANDLE_VALUE)
        device = ImDiskOpenDeviceByMountPoint(MountPoint,
        GENERIC_READ);

    if (device == INVALID_HANDLE_VALUE)
        device = ImDiskOpenDeviceByMountPoint(MountPoint,
        FILE_READ_ATTRIBUTES);

    if (device == INVALID_HANDLE_VALUE)
    {

        ImScsiMsgBoxLastError(hWnd, L"Error opening device:");
        return FALSE;
    }

    DEVICE_NUMBER device_numbers[max_extent_count];
    BYTE port_numbers[max_extent_count];
    DWORD number_of_disks = 1;

    if (!ImScsiGetDeviceNumberForDiskEx(device, device_numbers, port_numbers) &&
        !ImScsiGetDeviceNumbersForVolumeEx(device, device_numbers,
            port_numbers, max_extent_count, &number_of_disks))
    {
        ImScsiMsgBoxPrintF(hWnd, MB_ICONERROR, L"Arsenal Image Mounter",
            L"Not an Arsenal Image Mounter device: '%1!ws!'", MountPoint);

        CloseHandle(device);

        return FALSE;
    }

    ImScsiSetStatusMsg(hWnd, L"Flushing file buffers...");

    FlushFileBuffers(device);

    ImScsiSetStatusMsg(hWnd, L"Locking volume...");

    if (!DeviceIoControl(device,
        FSCTL_LOCK_VOLUME,
        NULL,
        0,
        NULL,
        0,
        &dw,
        NULL))
    {
        if (APIFlags & IMSCSI_API_FORCE_DISMOUNT)
        {
            ImScsiDebugMessage(L"Failed locking device, forcing dismount...");

            force_dismount = TRUE;
        }
        else if (hWnd == NULL)
        {
            ImScsiMsgBoxLastError(hWnd, L"Error locking device:");

            NtClose(device);
            return FALSE;
        }
        else if (ImScsiDebugMsgBox(hWnd,
            L"Cannot lock the device. The device may be in use by "
            L"another process or you may not have permission to "
            L"lock it. Do you want to try to force dismount of "
            L"the volume? (Unsaved data on the volume will be "
            L"lost.)",
            L"Arsenal Image Mounter",
            MB_ICONEXCLAMATION | MB_YESNO | MB_DEFBUTTON2) !=
            IDYES)
        {
            NtClose(device);
            return FALSE;
        }
        else
            force_dismount = TRUE;
    }


    ImScsiSetStatusMsg(hWnd, L"Dismounting filesystem...");

    DeviceIoControl(device,
        FSCTL_DISMOUNT_VOLUME,
        NULL,
        0,
        NULL,
        0,
        &dw,
        NULL);

    if (force_dismount)
        DeviceIoControl(device,
        FSCTL_LOCK_VOLUME,
        NULL,
        0,
        NULL,
        0,
        &dw,
        NULL);

    ImScsiSetStatusMsg(hWnd, L"Offline disk...");

    SET_DISK_ATTRIBUTES disk_attrs = { sizeof(disk_attrs) };
    disk_attrs.Attributes |= DISK_ATTRIBUTE_OFFLINE;
    disk_attrs.AttributesMask |= DISK_ATTRIBUTE_OFFLINE;

    if (!DeviceIoControl(device,
        IOCTL_DISK_SET_DISK_ATTRIBUTES,
        &disk_attrs,
        sizeof(disk_attrs),
        NULL,
        0,
        &dw,
        NULL))
    {
        if (GetLastError() != ERROR_INVALID_FUNCTION)
        {
            NtClose(device);

            ImScsiMsgBoxLastError(hWnd, L"Error taking disk offline:");

            return FALSE;
        }

        if (!DeviceIoControl(device,
            IOCTL_VOLUME_OFFLINE,
            NULL,
            0,
            NULL,
            0,
            &dw,
            NULL))
        {
            if (GetLastError() != ERROR_INVALID_FUNCTION)
            {
                NtClose(device);

                ImScsiMsgBoxLastError(hWnd,
                    L"Error taking disk volume offline:");

                return FALSE;
            }

            if (DeviceIoControl(device,
                IOCTL_STORAGE_EJECT_MEDIA,
                NULL,
                0,
                NULL,
                0,
                &dw,
                NULL))
            {
                ImScsiSetStatusMsg(hWnd, L"Done.");
                return TRUE;
            }
            else if (!force_dismount)
            {
                NtClose(device);

                ImScsiMsgBoxLastError(hWnd,
                    L"Error ejecting disk:");

                return FALSE;
            }
        }
    }

    ImScsiSetStatusMsg(hWnd, L"Removing device...");

    for (DWORD i = 0; i < number_of_disks; i++)
    {
        HANDLE adapter = ImScsiOpenScsiAdapterByScsiPortNumber(
            port_numbers[i]);

        if (adapter == INVALID_HANDLE_VALUE)
        {

            ImScsiMsgBoxLastError(hWnd, L"Error opening device:");

            CloseHandle(device);

            return FALSE;
        }

        if (!ImScsiRemoveDeviceByNumber(hWnd, adapter, device_numbers[i]))
        {
            NtClose(device);

            ImScsiMsgBoxLastError(hWnd, L"Error removing device:");

            return FALSE;
        }
    }

    DeviceIoControl(device,
        FSCTL_UNLOCK_VOLUME,
        NULL,
        0,
        NULL,
        0,
        &dw,
        NULL);

    NtClose(device);

    ImScsiSetStatusMsg(hWnd, L"Done.");

    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiChangeFlags(HWND hWnd,
HANDLE Adapter,
DEVICE_NUMBER DeviceNumber,
DWORD FlagsToChange,
DWORD Flags)
{
    DWORD dw;


    ImScsiSetStatusMsg(hWnd, L"Setting device flags...");

    SRB_IMSCSI_SET_DEVICE_FLAGS device_flags;

    device_flags.DeviceNumber = DeviceNumber;
    device_flags.FlagsToChange = FlagsToChange;
    device_flags.FlagValues = Flags;

    if (!ImScsiDeviceIoControl(Adapter,
        SMP_IMSCSI_SET_DEVICE_FLAGS,
        &device_flags.SrbIoControl,
        sizeof(device_flags),
        0, &dw))
    {

        ImScsiMsgBoxLastError(hWnd, L"Error setting device flags:");

        return FALSE;
    }

    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiExtendDevice(HWND hWnd,
    HANDLE Adapter,
    DEVICE_NUMBER DeviceNumber,
    PLARGE_INTEGER ExtendSize)
{
    DWORD dw;


    ImScsiSetStatusMsg(hWnd, L"Extending virtual disk size...");

    SRB_IMSCSI_EXTEND_DEVICE device_flags;

    device_flags.DeviceNumber = DeviceNumber;
    device_flags.ExtendSize = *ExtendSize;

    if (!ImScsiDeviceIoControl(Adapter,
        SMP_IMSCSI_EXTEND_DEVICE,
        &device_flags.SrbIoControl,
        sizeof(device_flags),
        0, &dw))
    {

        ImScsiMsgBoxLastError(hWnd, L"Error extending disk size:");

        return FALSE;
    }

    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiSaveRegistrySettings(PIMSCSI_DEVICE_CONFIGURATION Config)
{
    LONG err_code;
    HKEY hkey;
    DWORD load_devices;
    DWORD value_size;
    LPWSTR value_name;

    err_code = RegCreateKey(HKEY_LOCAL_MACHINE,
        L"SYSTEM\\CurrentControlSet\\Services\\phdskmnt"
        IMSCSI_CFG_PARAMETER_KEY,
        &hkey);

    if (err_code != ERROR_SUCCESS)
    {
        SetLastError(err_code);
        return FALSE;
    }

    load_devices = 0;
    value_size = sizeof(load_devices);
    err_code = RegQueryValueEx(hkey,
        IMSCSI_CFG_LOAD_DEVICES_VALUE,
        NULL,
        NULL,
        (LPBYTE)&load_devices,
        &value_size);

    if (err_code == ERROR_SUCCESS)
        if (Config->DeviceNumber.LongNumber == IMSCSI_AUTO_DEVICE_NUMBER)
        {
            Config->DeviceNumber.LongNumber = load_devices;
            ++load_devices;
        }
        else
            load_devices = max(load_devices, Config->DeviceNumber.LongNumber + 1);
    else
        if (Config->DeviceNumber.LongNumber == IMSCSI_AUTO_DEVICE_NUMBER)
        {
            Config->DeviceNumber.LongNumber = 0;
            load_devices = 1;
        }
        else
            load_devices = Config->DeviceNumber.LongNumber + 1;

    err_code = RegSetValueEx(hkey,
        IMSCSI_CFG_LOAD_DEVICES_VALUE,
        0,
        REG_DWORD,
        (LPBYTE)&load_devices,
        sizeof(load_devices));

    if (err_code != ERROR_SUCCESS)
    {
        RegCloseKey(hkey);
        SetLastError(err_code);
        return FALSE;
    }

    value_name = ImDiskAllocPrintF(IMSCSI_CFG_IMAGE_FILE_PREFIX L"%1!u!",
        Config->DeviceNumber);
    if (value_name == NULL)
    {
        RegCloseKey(hkey);
        return FALSE;
    }

    if (Config->FileNameLength > 0)
    {
        LPWSTR value_data =
            ImDiskAllocPrintF(L"%1!.*ws!",
            (int)(Config->FileNameLength /
            sizeof(*Config->FileName)),
            Config->FileName);

        if (value_data == NULL)
        {
            RegCloseKey(hkey);
            return FALSE;
        }

        err_code = RegSetValueEx(hkey,
            value_name,
            0,
            REG_SZ,
            (LPBYTE)value_data,
            (DWORD)((wcslen(value_data) + 1) << 1));

        LocalFree(value_data);

        if (err_code != ERROR_SUCCESS)
        {
            RegCloseKey(hkey);
            SetLastError(err_code);
            return FALSE;
        }
    }
    else
        RegDeleteValue(hkey, value_name);

    LocalFree(value_name);

    value_name = ImDiskAllocPrintF(IMSCSI_CFG_SIZE_PREFIX L"%1!u!",
        Config->DeviceNumber);
    if (value_name == NULL)
    {
        RegCloseKey(hkey);
        return FALSE;
    }

    if (Config->DiskSize.QuadPart > 0)
    {
        err_code = RegSetValueEx(hkey,
            value_name,
            0,
            REG_QWORD,
            (LPBYTE)&Config->DiskSize,
            sizeof(Config->DiskSize));

        if (err_code != ERROR_SUCCESS)
        {
            RegCloseKey(hkey);
            SetLastError(err_code);
            return FALSE;
        }
    }
    else
        RegDeleteValue(hkey, value_name);

    LocalFree(value_name);

    value_name = ImDiskAllocPrintF(IMSCSI_CFG_FLAGS_PREFIX L"%1!u!",
        Config->DeviceNumber);
    if (value_name == NULL)
    {
        RegCloseKey(hkey);
        return FALSE;
    }

    if (Config->Flags != 0)
    {
        err_code = RegSetValueEx(hkey,
            value_name,
            0,
            REG_DWORD,
            (LPBYTE)&Config->Flags,
            sizeof(Config->Flags));

        if (err_code != ERROR_SUCCESS)
        {
            RegCloseKey(hkey);
            SetLastError(err_code);
            return FALSE;
        }
    }
    else
        RegDeleteValue(hkey, value_name);

    LocalFree(value_name);

    value_name = ImDiskAllocPrintF(IMSCSI_CFG_OFFSET_PREFIX L"%1!u!",
        Config->DeviceNumber);
    if (value_name == NULL)
    {
        RegCloseKey(hkey);
        return FALSE;
    }

    if (Config->ImageOffset.QuadPart > 0)
    {
        err_code = RegSetValueEx(hkey,
            value_name,
            0,
            REG_QWORD,
            (LPBYTE)&Config->ImageOffset,
            sizeof(Config->ImageOffset));

        if (err_code != ERROR_SUCCESS)
        {
            RegCloseKey(hkey);
            SetLastError(err_code);
            return FALSE;
        }
    }
    else
        RegDeleteValue(hkey, value_name);

    LocalFree(value_name);

    RegCloseKey(hkey);

    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiRemoveRegistrySettings(DEVICE_NUMBER DeviceNumber)
{
    LONG err_code;
    HKEY hkey;
    DWORD load_devices;
    DWORD value_size;
    LPWSTR value_name;

    err_code = RegOpenKey(HKEY_LOCAL_MACHINE,
        L"SYSTEM\\CurrentControlSet\\Services\\phdskmnt"
        IMSCSI_CFG_PARAMETER_KEY,
        &hkey);

    if (err_code != ERROR_SUCCESS)
    {
        SetLastError(err_code);
        return FALSE;
    }

    load_devices = 0;
    value_size = sizeof(load_devices);
    err_code = RegQueryValueEx(hkey,
        IMSCSI_CFG_LOAD_DEVICES_VALUE,
        NULL,
        NULL,
        (LPBYTE)&load_devices,
        &value_size);

    if (err_code != ERROR_SUCCESS)
    {
        RegCloseKey(hkey);
        SetLastError(err_code);
        return FALSE;
    }

    if (load_devices == DeviceNumber.LongNumber + 1)
    {
        --load_devices;

        err_code = RegSetValueEx(hkey,
            IMSCSI_CFG_LOAD_DEVICES_VALUE,
            0,
            REG_DWORD,
            (LPBYTE)&load_devices,
            sizeof(load_devices));

        if (err_code != ERROR_SUCCESS)
        {
            RegCloseKey(hkey);
            SetLastError(err_code);
            return FALSE;
        }
    }

    value_name = ImDiskAllocPrintF(IMSCSI_CFG_IMAGE_FILE_PREFIX L"%1!u!",
        DeviceNumber);
    if (value_name == NULL)
    {
        RegCloseKey(hkey);
        return FALSE;
    }

    RegDeleteValue(hkey, value_name);

    LocalFree(value_name);

    value_name = ImDiskAllocPrintF(IMSCSI_CFG_SIZE_PREFIX L"%1!u!",
        DeviceNumber);
    if (value_name == NULL)
    {
        RegCloseKey(hkey);
        return FALSE;
    }

    RegDeleteValue(hkey, value_name);

    LocalFree(value_name);

    value_name = ImDiskAllocPrintF(IMSCSI_CFG_FLAGS_PREFIX L"%1!u!",
        DeviceNumber);
    if (value_name == NULL)
    {
        RegCloseKey(hkey);
        return FALSE;
    }

    RegDeleteValue(hkey, value_name);

    LocalFree(value_name);

    value_name = ImDiskAllocPrintF(IMSCSI_CFG_OFFSET_PREFIX L"%1!u!",
        DeviceNumber);
    if (value_name == NULL)
    {
        RegCloseKey(hkey);
        return FALSE;
    }

    RegDeleteValue(hkey, value_name);

    LocalFree(value_name);

    RegCloseKey(hkey);

    return TRUE;
}

AIMAPI_API BOOL
WINAPI
ImScsiGetRegistryAutoLoadDevices(LPDWORD LoadDevicesValue)
{
    LONG err_code;
    HKEY hkey;
    DWORD value_size;

    err_code = RegOpenKey(HKEY_LOCAL_MACHINE,
        L"SYSTEM\\CurrentControlSet\\Services\\phdskmnt"
        IMSCSI_CFG_PARAMETER_KEY,
        &hkey);

    if (err_code != ERROR_SUCCESS)
    {
        SetLastError(err_code);
        return FALSE;
    }

    *LoadDevicesValue = 0;
    value_size = sizeof(*LoadDevicesValue);
    err_code = RegQueryValueEx(hkey,
        IMSCSI_CFG_LOAD_DEVICES_VALUE,
        NULL,
        NULL,
        (LPBYTE)LoadDevicesValue,
        &value_size);

    RegCloseKey(hkey);

    if (err_code != ERROR_SUCCESS)
    {
        SetLastError(err_code);
        return FALSE;
    }

    return TRUE;
}

AIMAPI_API
BOOL
WINAPI
ImScsiGetDeviceNumberForDisk(HANDLE Device,
    PDEVICE_NUMBER DeviceNumber,
    LPDWORD PortNumber)
{
    if (PortNumber != NULL && *PortNumber > MAXBYTE)
    {
        SetLastError(ERROR_INVALID_PARAMETER);
        return FALSE;
    }

    return ImScsiGetDeviceNumberForDiskEx(Device, DeviceNumber,
        (LPBYTE)PortNumber);
}

AIMAPI_API
BOOL
WINAPI
ImScsiGetDeviceNumberForDiskEx(HANDLE Device,
    PDEVICE_NUMBER DeviceNumber,
    LPBYTE PortNumber)
{
    SCSI_ADDRESS address;
    STORAGE_DEVICE_NUMBER disk_number;

    if (!ImScsiGetScsiAddressForDiskEx(Device,
        &address, &disk_number))
    {
        return FALSE;
    }

    DeviceNumber->TargetId = address.TargetId;
    DeviceNumber->PathId = address.PathId;
    DeviceNumber->Lun = address.Lun;

    *PortNumber = address.PortNumber;

    STORAGE_DEVICE_NUMBER opened_disk_number;

    HANDLE disk = ImScsiOpenDiskByDeviceNumberEx(*DeviceNumber, PortNumber,
        &opened_disk_number);

    if (disk == INVALID_HANDLE_VALUE)
    {
        SetLastError(ERROR_INVALID_FUNCTION);
        return FALSE;
    }

    NtClose(disk);

    if (memcmp(&disk_number, &opened_disk_number, sizeof disk_number) != 0)
    {
        SetLastError(ERROR_INVALID_FUNCTION);
        return FALSE;
    }

    return TRUE;
}

AIMAPI_API
BOOL
WINAPI
ImScsiGetScsiAddressForDisk(IN HANDLE Device,
    OUT PSCSI_ADDRESS ScsiAddress)
{
    DWORD dw;

    if (DeviceIoControl(Device,
        IOCTL_SCSI_GET_ADDRESS,
        NULL,
        0,
        ScsiAddress,
        sizeof(*ScsiAddress),
        &dw,
        NULL))
    {
        return TRUE;
    }

    return FALSE;
}

AIMAPI_API
BOOL
WINAPI
ImScsiGetScsiAddressForDiskEx(HANDLE Device,
    PSCSI_ADDRESS ScsiAddress,
    PSTORAGE_DEVICE_NUMBER DeviceNumber)
{
    DWORD dw;

    if (DeviceIoControl(Device,
        IOCTL_STORAGE_GET_DEVICE_NUMBER,
        NULL,
        0,
        DeviceNumber,
        sizeof(*DeviceNumber),
        &dw,
        NULL) && DeviceIoControl(Device,
            IOCTL_SCSI_GET_ADDRESS,
            NULL,
            0,
            ScsiAddress,
            sizeof(*ScsiAddress),
            &dw,
            NULL))
    {
        return TRUE;
    }

    return FALSE;
}



