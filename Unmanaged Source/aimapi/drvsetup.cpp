
/// imscsi.c
/// Driver setup routines. Usually ImScsiInstsallDriver() or ImScsiUninstallDriver() are
/// called from applications.
/// 
/// Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
/// This source code and API are available under the terms of the Affero General Public
/// License v3.
///
/// Please see LICENSE.txt for full license terms, including the availability of
/// proprietary exceptions.
/// Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
///


#include "stdafx.h"

#include <devguid.h>
#include <SetupAPI.h>
#include <cfgmgr32.h>
#include <newdev.h>

#include "..\phdskmnt\inc\ntumapi.h"
#include "..\phdskmnt\inc\phdskmntver.h"

#include <imdisk.h>
#include <imdproxy.h>

#include "aimapi.h"

#include "winstrct.hpp"

#include "wscm.h"

#include <dbt.h>

#include <stdio.h>

#pragma comment(lib, "version.lib")
#pragma comment(lib, "newdev.lib")
#pragma comment(lib, "setupapi.lib")

LPCWSTR KernelPlatformCode = NULL;
BOOL KernelSupportsStorPort = FALSE;
BOOL ProcessRunningInWow64 = FALSE;

#ifdef _M_IX86

pfGetVolumePathNamesForVolumeNameW fpGetVolumePathNamesForVolumeNameW = NULL;

WCHAR
WINAPI
ImScsiGetDriveLetterForVolumeName(
IN LPCWSTR lpszVolumeName)
{
    WCHAR vol_target[MAX_PATH];

    wcsncpy(vol_target, lpszVolumeName + 4, 44);
    vol_target[44] = 0;

    if (!QueryDosDevice(vol_target, vol_target, _countof(vol_target)))
    {
        return FALSE;
    }

    WHeapMem<WCHAR> dosdevs(UNICODE_STRING_MAX_BYTES,
        HEAP_GENERATE_EXCEPTIONS);

    if (!QueryDosDevice(NULL, dosdevs, (DWORD)dosdevs.Count()))
    {
        return FALSE;
    }

    WCHAR dev_target[MAX_PATH];

    SIZE_T length;
    for (LPCWSTR ptr = dosdevs;
        (length = wcslen(ptr)) != 0;
        ptr += length + 1)
    {
        if ((length != 2) ||
            (ptr[1] != L':') ||
            (!QueryDosDevice(ptr, dev_target, _countof(dev_target))) ||
            (_wcsicmp(dev_target, vol_target) != 0))
        {
            continue;
        }

        return *ptr;
    }

    return 0;
}

// This does the same as GetVolumePathNamesForVolumeNameW() on Windows XP and
// later. It is built into 32 bit versions of this library to make sure that
// the DLL can load correctly on Windows 2000 as well.
BOOL
WINAPI
ImScsiLegacyGetVolumePathNamesForVolumeName(
__in   LPCWSTR lpszVolumeName,
__out  LPWSTR  lpszVolumePathNames,
__in   DWORD   cchBufferLength,
__out  PDWORD  lpcchReturnLength)
{
    *lpcchReturnLength = 0;

    DWORD dw;
    dw;

    LPWSTR cur_ptr = lpszVolumePathNames;
    LPWSTR end_ptr = lpszVolumePathNames + cchBufferLength;

    WCHAR vol_target[MAX_PATH];

    wcsncpy(vol_target, lpszVolumeName + 4, 44);
    vol_target[44] = 0;

    if (!QueryDosDevice(vol_target, vol_target, _countof(vol_target)))
    {
        return FALSE;
    }

    WHeapMem<WCHAR> dosdevs(UNICODE_STRING_MAX_BYTES,
        HEAP_GENERATE_EXCEPTIONS);

    if (!QueryDosDevice(NULL, dosdevs, (DWORD)dosdevs.Count()))
    {
        return FALSE;
    }

    DWORD good = cchBufferLength >= 2;
    
    *lpcchReturnLength = 2;

    WCHAR dev_target[MAX_PATH];

    SIZE_T length;
    for (LPCWSTR ptr = dosdevs;
        (length = wcslen(ptr)) != 0;
        ptr += length + 1)
    {
        if (good)
        {
            *cur_ptr = 0;
        }

        if ((length != 2) ||
            (ptr[1] != L':') ||
            (!QueryDosDevice(ptr, dev_target, _countof(dev_target))) ||
            (_wcsicmp(dev_target, vol_target) != 0))
        {
            continue;
        }

        *lpcchReturnLength += 4;

        if ((cur_ptr + 4) >= end_ptr)
        {
            good = FALSE;
        }

        if (good)
        {
            swprintf(cur_ptr, L"%ws\\", ptr);
            cur_ptr += 4;
        }
    }

    WCHAR vol_name[50];

    HANDLE volume = FindFirstVolume(vol_name, _countof(vol_name));

    if (volume == INVALID_HANDLE_VALUE)
    {
        return FALSE;
    }

    DWORD error_mode = SetErrorMode(SEM_FAILCRITICALERRORS |
        SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);

    do
    {
        HANDLE vol_mnt = FindFirstVolumeMountPoint(vol_name, dosdevs,
            (DWORD)dosdevs.Count());

        if (vol_mnt == INVALID_HANDLE_VALUE)
        {
            continue;
        }

        do
        {
            WMem<WCHAR> mnt_path;
            
            mnt_path = ImDiskAllocPrintF(L"%1!ws!%2!ws!", vol_name, dosdevs);

            if (!mnt_path)
            {
                continue;
            }

            WCHAR mnt_vol_name[50];
            if (!GetVolumeNameForVolumeMountPoint(mnt_path, mnt_vol_name,
                _countof(mnt_vol_name)))
            {
                continue;
            }

            if (_wcsicmp(mnt_vol_name, lpszVolumeName) == 0)
            {
                if (ImScsiLegacyGetVolumePathNamesForVolumeName(vol_name,
                    vol_target, _countof(vol_target), &dw))
                {
                    mnt_path = ImDiskAllocPrintF(L"%1!ws!%2!ws!", vol_target,
                        dosdevs);

                }

                size_t len = wcslen(mnt_path) + 1;

                *lpcchReturnLength += (DWORD)len;

                if ((cur_ptr + len) >= end_ptr)
                {
                    good = FALSE;
                }

                if (good)
                {
                    wcscpy(cur_ptr, mnt_path);
                    cur_ptr += len;
                }
            }

        } while (FindNextVolumeMountPoint(vol_mnt, dosdevs,
            (DWORD)dosdevs.Count()));
        
        FindVolumeMountPointClose(vol_mnt);

    } while (FindNextVolume(volume, vol_name, _countof(vol_name)));

    FindVolumeClose(volume);

    SetErrorMode(error_mode);

    if (cur_ptr >= end_ptr)
    {
        good = FALSE;
    }

    if (good)
    {
        *cur_ptr = 0;
        ++*lpcchReturnLength;
    }
    else
    {
        SetLastError(ERROR_MORE_DATA);
    }

    return good;
}

AIMAPI_API
BOOL WINAPI ImScsiGetVolumePathNamesForVolumeName(
__in   LPCWSTR lpszVolumeName,
__out  LPWSTR  lpszVolumePathNames,
__in   DWORD   cchBufferLength,
__out  PDWORD  lpcchReturnLength)
{
    if (fpGetVolumePathNamesForVolumeNameW == NULL)
    {
        fpGetVolumePathNamesForVolumeNameW =
            (pfGetVolumePathNamesForVolumeNameW)GetProcAddress(
            GetModuleHandle(L"kernel32.dll"),
            "GetVolumePathNamesForVolumeNameW");
    }

    if (fpGetVolumePathNamesForVolumeNameW == NULL)
    {
        fpGetVolumePathNamesForVolumeNameW =
            ImScsiLegacyGetVolumePathNamesForVolumeName;
    }

    return fpGetVolumePathNamesForVolumeNameW(
        lpszVolumeName,
        lpszVolumePathNames,
        cchBufferLength,
        lpcchReturnLength);
}

AIMAPI_API
BOOL
WINAPI
ImScsiSetupSetNonInteractiveMode(BOOL NotInteractiveFlag)
{
    __try
    {
        return SetupSetNonInteractiveMode(NotInteractiveFlag);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return NotInteractiveFlag;
    }
}

#endif

BOOL
ImScsiInitializeSystemVersionInfo()
{
    OSVERSIONINFO os_version = { sizeof(os_version) };
    if (!ImScsiGetOSVersion(&os_version))
    {
        return FALSE;
    }

#ifdef _M_IX86
    if ((os_version.dwMajorVersion >= 5) &&
        (os_version.dwMinorVersion >= 1))
    {
        auto fpIsWow64Process = (pfIsWow64Process)
            GetProcAddress(GetModuleHandle(L"kernel32.dll"),
            "IsWow64Process");

        if (fpIsWow64Process != NULL)
        {
            fpIsWow64Process(GetCurrentProcess(), &ProcessRunningInWow64);
        }
    }
#endif

    ImScsiDebugMessage(L"Detected Windows kernel version %1!u!.%2!u!.%3!u!.",
        os_version.dwMajorVersion, os_version.dwMinorVersion,
        os_version.dwBuildNumber);

    if (ProcessRunningInWow64)
    {
        ImScsiDebugMessage(
            L"This is a 32 bit process running on a 64 bit version of Windows. This may cause setup to fail.");
    }

    auto version_number = (os_version.dwMajorVersion << 8) |
        os_version.dwMinorVersion;

    if (version_number >= 0x0A00) {
        KernelPlatformCode = L"Win10";
        KernelSupportsStorPort = true;
    }
    else if (version_number >= 0x0603) {
        KernelPlatformCode = L"Win8.1";
        KernelSupportsStorPort = true;
    }
    else if (version_number >= 0x0602) {
        KernelPlatformCode = L"Win8";
        KernelSupportsStorPort = true;
    }
    else if (version_number >= 0x0601) {
        KernelPlatformCode = L"Win7";
        KernelSupportsStorPort = true;
    }
    else if (version_number >= 0x0600) {
        KernelPlatformCode = L"WinLH";
        KernelSupportsStorPort = true;
    }
    else if (version_number >= 0x0502) {
        KernelPlatformCode = L"WinNET";
        KernelSupportsStorPort = true;
    }
    else if (version_number >= 0x0501) {
        KernelPlatformCode = L"WinXP";
        KernelSupportsStorPort = false;
    }
    else if (version_number >= 0x0500) {
        KernelPlatformCode = L"Win2K";
        KernelSupportsStorPort = false;
    }
    else {
        ImScsiDebugMessage(L"Unsupported Windows version.");

        SetLastError(ERROR_NOT_SUPPORTED);
        return FALSE;
    }

    ImScsiDebugMessage(L"Platform code: '%1'. Using port driver %2.\n",
        KernelPlatformCode,
        KernelSupportsStorPort ? L"storport.sys" : L"scsiport.sys");

    return TRUE;
}

AIMAPI_API
LPCWSTR
WINAPI
ImScsiGetKernelPlatformCode(LPBOOL SupportsStorPort,
LPBOOL RunningInWow64)
{
    if (KernelPlatformCode == NULL)
    {
        if (!ImScsiInitializeSystemVersionInfo())
        {
            return NULL;
        }
    }

    if (SupportsStorPort != NULL)
    {
        *SupportsStorPort = KernelSupportsStorPort;
    }

    if (RunningInWow64 != NULL)
    {
        *RunningInWow64 = ProcessRunningInWow64;
    }

    return KernelPlatformCode;
}

AIMAPI_API
DWORD
WINAPI
ImScsiAllocateDeviceInstanceListForService(LPCWSTR service,
LPWSTR *instances)
{
    DWORD length = 0;

    auto status = CM_Get_Device_ID_List_Size(&length, service,
        CM_GETIDLIST_FILTER_SERVICE);

    if (status != CR_SUCCESS)
    {
        SetLastError(ERROR_INSTALL_FAILURE);
        return 0;
    }

    *instances = (LPWSTR)LocalAlloc(LMEM_FIXED, sizeof(**instances) * length);

    if (*instances == NULL)
    {
        return 0;
    }

    status = CM_Get_Device_ID_List(service, *instances, length,
        CM_GETIDLIST_FILTER_SERVICE);

    if (status != CR_SUCCESS)
    {
        LocalFree(instances);

        ImScsiDebugMessage(
            L"Error enumerating instances for service %1!ws!: %2!#x!",
            service, status);

        SetLastError(ERROR_INSTALL_FAILURE);
        return 0;
    }

    return length;
}

BOOL
WINAPI
ImScsiRemovePnPDevice(HWND OwnerWindow, LPCWSTR hwid)
{
    auto DeviceInfoSet = SetupDiCreateDeviceInfoList(NULL, OwnerWindow);

    if (DeviceInfoSet == INVALID_HANDLE_VALUE)
    {
        return FALSE;
    }

    if (!SetupDiOpenDeviceInfo(DeviceInfoSet, hwid, OwnerWindow, 0, NULL))
    {
        SetupDiDestroyDeviceInfoList(DeviceInfoSet);
        return FALSE;
    }

    SP_DEVINFO_DATA DeviceInfoData = { sizeof(DeviceInfoData) };
    int i = 0;
    int done = 0;

    while (SetupDiEnumDeviceInfo(DeviceInfoSet, i, &DeviceInfoData))
    {
        if (SetupDiCallClassInstaller(DIF_REMOVE, DeviceInfoSet,
            &DeviceInfoData))
        {
            done++;
        }

        i += 1;
    }

    if (i == 0)
    {
        return FALSE;
    }

    SetupDiDestroyDeviceInfoList(DeviceInfoSet);

    return TRUE;
}

AIMAPI_API
BOOL
WINAPI
ImScsiRemoveDevices(HWND OwnerWindow)
{
    LPWSTR hwinstances = NULL;
    DWORD length = ImScsiAllocateDeviceInstanceListForService(L"phdskmnt",
        &hwinstances);

    if (length == 0)
    {
        return 0;
    }

    WMem<WCHAR> allocated(hwinstances);

    BOOL result = TRUE;

    for (size_t i = 0;
        i < length;
        i += wcslen(hwinstances) + 1)
    {
        if (hwinstances[i] == 0)
            continue;

        result &= ImScsiRemovePnPDevice(OwnerWindow, hwinstances + i);
    }

    return result;
}

AIMAPI_API
BOOL
WINAPI
ImScsiRemoveDriver(LPBOOL RebootRequired)
{
    if (RebootRequired != NULL)
    {
        *RebootRequired = FALSE;
    }

    WCHAR sys_dir[MAX_PATH];
    if (!GetSystemDirectory(sys_dir, _countof(sys_dir)))
    {
        WPreserveLastError ple;

        WErrMsg errmsg;

        ImScsiDebugMessage(L"Error getting Windows system directory: %1!ws!",
            (LPCWSTR)errmsg);

        return FALSE;
    }

    WMem<WCHAR> drv_file_path(ImDiskAllocPrintF(
        L"%1!ws!\\drivers\\phdskmnt.sys", sys_dir));

    if ((!DeleteFile(drv_file_path)) &&
        (GetLastError() != ERROR_FILE_NOT_FOUND))
    {
        WPreserveLastError ple;

        WErrMsg errmsg;

        ImScsiDebugMessage(L"Error removing file %1!ws!: %2!ws!",
            (LPCWSTR)drv_file_path, (LPCWSTR)errmsg);

        return FALSE;
    }

    WSCManager scm;

    if (!scm)
    {
        return FALSE;
    }

    WSCService service(scm.Handle(), L"phdskmnt");

    if (service)
    {
        if (!DeleteService(service.Handle()))
        {
            if (GetLastError() == ERROR_SERVICE_MARKED_FOR_DELETE)
            {
                if (RebootRequired != NULL)
                {
                    *RebootRequired = TRUE;
                }

                ImScsiDebugMessage(
                    L"Driver is loaded but marked for deletion. It will be removed after reboot.");
            }
            else
            {
                WPreserveLastError ple;

                WErrMsg errmsg;

                ImScsiDebugMessage(L"Error removing driver: %1!ws!",
                    (LPCWSTR)errmsg);

                return FALSE;
            }
        }

        SERVICE_STATUS service_status;
        if (!QueryServiceStatus(service.Handle(), &service_status))
        {
            WPreserveLastError ple;

            WErrMsg errmsg;

            ImScsiDebugMessage(L"Error querying service status: %1!ws!",
                (LPCWSTR)errmsg);

            return FALSE;
        }
        
        if (service_status.dwCurrentState != SERVICE_STOPPED)
        {
            if (RebootRequired != NULL)
            {
                *RebootRequired = TRUE;
            }

            ImScsiDebugMessage(L"Driver failed to unload.");
        }
    }

    return TRUE;
}

AIMAPI_API
DWORD
WINAPI
ImScsiScanForHardwareChanges(LPWSTR rootid, DWORD flags)
{
    DEVINST devInst;
    auto status = CM_Locate_DevNode(&devInst, rootid, 0);

    if (status != CR_SUCCESS)
    {
        ImScsiDebugMessage(
            L"Error scanning for hardware changes: %1!#x!",
            status);

        return status;
    }

    return CM_Reenumerate_DevNode(devInst, flags);
}

AIMAPI_API
BOOL
WINAPI
ImScsiRescanScsiAdapter()
{
    LPWSTR hwinstances = NULL;
    DWORD length = ImScsiAllocateDeviceInstanceListForService(L"phdskmnt",
        &hwinstances);

    if (length <= 2)
    {
        SetLastError(ERROR_FILE_NOT_FOUND);
        return FALSE;
    }

    WMem<WCHAR> allocated(hwinstances);

    BOOL result = FALSE;
    SetLastError(ERROR_FILE_NOT_FOUND);

    for (size_t i = 0;
        i < length;
        i += wcslen(hwinstances) + 1)
    {
        if (hwinstances[i] == 0)
        {
            continue;
        }

        ImScsiDebugMessage(L"Rescanning %1...", hwinstances + i);

        auto status = ImScsiScanForHardwareChanges(hwinstances + i, 0);

        if (status == CR_SUCCESS)
        {
            result = TRUE;
        }
        else
        {
            ImScsiDebugMessage(L"Rescanning of %1 failed: %2!#x!",
                hwinstances + 1, status);

            SetLastError(status);
        }
    }

    return result;
}

DWORD
WINAPI
ImScsiRescanScsiAdapterThread(HANDLE Event)
{
    ImScsiRescanScsiAdapter();

    SetEvent(Event);

    return 0;
}

AIMAPI_API
HANDLE
WINAPI
ImScsiRescanScsiAdapterAsync(BOOL AsyncFlag)
{
    HANDLE event = CreateEvent(NULL, TRUE, FALSE, NULL);

    if (event == NULL)
    {
        return NULL;
    }

    if (AsyncFlag)
    {
        if (!QueueUserWorkItem(ImScsiRescanScsiAdapterThread, event,
            WT_EXECUTEINPERSISTENTTHREAD))
        {
            CloseHandle(event);
            return NULL;
        }
    }
    else
    {
        ImScsiRescanScsiAdapterThread(event);
    }

    return event;
}

DWORD
WINAPI
ImScsiScanForHardwareChangesThread(HANDLE Event)
{
    ImScsiScanForHardwareChanges();

    SetEvent(Event);

    return 0;
}

AIMAPI_API
HANDLE
WINAPI
ImScsiScanForHardwareChangesAsync(BOOL AsyncFlag)
{
    HANDLE event = CreateEvent(NULL, TRUE, FALSE, NULL);

    if (event == NULL)
    {
        return NULL;
    }

    if (AsyncFlag)
    {
        if (!QueueUserWorkItem(ImScsiScanForHardwareChangesThread, event,
            WT_EXECUTEINPERSISTENTTHREAD))
        {
            CloseHandle(event);
            return NULL;
        }
    }
    else
    {
        ImScsiScanForHardwareChangesThread(event);
    }

    return event;
}

AIMAPI_API
BOOL
WINAPI
ImScsiInstallDriver(LPWSTR SetupSource,
HWND OwnerWindow,
LPBOOL RebootRequired)
{
    LPCWSTR kernel = ImScsiGetKernelPlatformCode(NULL, NULL);

    if (kernel == NULL)
    {
        return FALSE;
    }

    auto ImScsipfInstallDriver = KernelSupportsStorPort ?
    ImScsiInstallStorPortDriver : ImScsiInstallScsiPortDriver;

    return ImScsipfInstallDriver(SetupSource, OwnerWindow, RebootRequired);
}

AIMAPI_API
BOOL
WINAPI
ImScsiInstallScsiPortDriver(LPWSTR SetupSource,
HWND OwnerWindow,
LPBOOL RebootRequired)
{
    LPCWSTR kernel = ImScsiGetKernelPlatformCode(NULL, NULL);

    if (kernel == NULL)
    {
        return FALSE;
    }

    if (RebootRequired != NULL)
    {
        *RebootRequired = FALSE;
    }

    if (wcschr(SetupSource, L' ') != NULL)
    {
        ImScsiDebugMessage(
            L"Invalid directory path '%1!ws!': Paths that contain spaces are not supported.",
            SetupSource);

        if (OwnerWindow != NULL)
        {
            ImDiskMsgBoxPrintF(OwnerWindow,
                MB_ICONEXCLAMATION,
                L"Arsenal Image Mounter",
                L"Invalid directory path '%1!ws!': Paths that contain spaces are not supported.",
                SetupSource);
        }

        SetLastError(ERROR_INVALID_PARAMETER);

        return FALSE;
    }

    WMem<WCHAR> ctlunit_inf(ImDiskAllocPrintF(L"%1!ws!\\CtlUnit\\ctlunit.inf",
        SetupSource));

    if (!ctlunit_inf)
    {
        return FALSE;
    }

    ImScsiDebugMessage(L"Preinstalling control unit inf file...");

    WCHAR dest_name[MAX_PATH];
    if (!SetupCopyOEMInf(ctlunit_inf, NULL, 0, 0, dest_name, _countof(dest_name),
        NULL, NULL))
    {
        WErrMsg errmsg;

        ImScsiDebugMessage(
            L"Error installing '%1!ws!': %2!ws!",
            ctlunit_inf, (LPWSTR)errmsg);

        if (OwnerWindow != NULL)
        {
            ImDiskMsgBoxPrintF(OwnerWindow,
                MB_ICONEXCLAMATION,
                L"Arsenal Image Mounter",
                L"Error installing '%1!ws!': %2!ws!",
                ctlunit_inf, (LPWSTR)errmsg);
        }

        return FALSE;
    }

    ImScsiDebugMessage(L"Installing driver...");

    if (!SetCurrentDirectory(SetupSource))
    {
        WErrMsg errmsg;

        ImScsiDebugMessage(
            L"Error changing current directory to '%1!ws!': %2!ws!.",
            ctlunit_inf, (LPWSTR)errmsg);

        if (OwnerWindow != NULL)
        {
            ImDiskMsgBoxPrintF(OwnerWindow,
                MB_ICONEXCLAMATION,
                L"Arsenal Image Mounter",
                L"Error changing current directory to '%1!ws!': %2!ws!",
                ctlunit_inf, (LPWSTR)errmsg);
        }

        return FALSE;
    }

    ImScsiSetupSetNonInteractiveMode(FALSE);

    WMem<WCHAR> install_cmd(ImDiskAllocPrintF(
        L"DefaultInstall 132 .\\%1!ws!\\phdskmnt.inf", kernel));

    if (!install_cmd)
    {
        return FALSE;
    }

    InstallHinfSection(
        OwnerWindow != NULL ? OwnerWindow : GetConsoleWindow(),
        NULL, install_cmd, 0);

    ImScsiDebugMessage(L"Loading driver...");

    WSCManager scm;

    if (!scm)
    {
        return FALSE;
    }

    WSCService service(scm.Handle(), L"phdskmnt");

    if (!service)
    {
        if (GetLastError() == ERROR_SERVICE_DOES_NOT_EXIST)
        {
            SetLastError(ERROR_INSTALL_FAILURE);
        }

        return FALSE;
    }

    for (;;)
    {
        SERVICE_STATUS status;
        if (!QueryServiceStatus(service.Handle(), &status))
        {
            return FALSE;
        }

        if (status.dwCurrentState == SERVICE_RUNNING)
        {
            break;
        }

        if (!StartService(service.Handle(), 0, NULL))
        {
            if (GetLastError() == ERROR_SERVICE_ALREADY_RUNNING)
            {
                break;
            }

            if (GetLastError() == ERROR_SERVICE_REQUEST_TIMEOUT)
            {
                Sleep(1000);
                continue;
            }

            return FALSE;
        }
    }

    ImScsiDebugMessage(L"Detecting installed devices...");

    if (OwnerWindow == NULL)
    {
        ImScsiScanForHardwareChanges();
    }
    else
    {
        HANDLE event = ImScsiScanForHardwareChangesAsync(TRUE);

        if (event == NULL)
        {
            return FALSE;
        }

        while (MsgWaitForMultipleObjects(1, &event, FALSE, INFINITE,
            QS_ALLEVENTS) == WAIT_OBJECT_0 + 1)
        {
            ImDiskFlushWindowMessages(NULL);
        }

        CloseHandle(event);
    }

    return TRUE;
}

AIMAPI_API
SIZE_T
WINAPI
ImScsiGetMultiStringByteLength(LPCWSTR MultiString)
{
    LPCWSTR ptr = MultiString;

    while (*ptr != 0)
    {
        ptr += wcslen(ptr) + 1;
    }

    return (PUCHAR)(ptr + 2) - (PUCHAR)MultiString;
}

BOOL
WINAPI
ImScsiCreateRootPnPDevice(HWND OwnerWindow,
LPCWSTR InfPath,
LPCWSTR hwIdList,
LPBOOL RebootRequired)
{
    if (RebootRequired != NULL)
    {
        *RebootRequired = FALSE;
    }

    ImScsiDebugMessage(L"Reading inf file...");

    WCHAR ClassName[32];
    DWORD required_size = 0;
    GUID ClassGUID;
    if (!SetupDiGetINFClass(InfPath, &ClassGUID, ClassName,
        _countof(ClassName), &required_size))
    {
        WPreserveLastError ple;

        WErrMsg errmsg;

        ImScsiDebugMessage(L"Error reading file '%1!ws!': %2!ws!",
            InfPath, (LPCWSTR)errmsg);

        return FALSE;
    }

    auto DeviceInfoSet = SetupDiCreateDeviceInfoList(&ClassGUID, OwnerWindow);

    if (DeviceInfoSet == INVALID_HANDLE_VALUE)
    {
        WPreserveLastError ple;

        WErrMsg errmsg;

        ImScsiDebugMessage(
            L"SetupDiCreateDeviceInfoList failed: %1!ws! (inf = %2!ws!)",
            (LPCWSTR)errmsg, InfPath);

        return FALSE;
    }

    ImScsiDebugMessage(L"Creating device object...");

    SP_DEVINFO_DATA DeviceInfoData = { sizeof(DeviceInfoData) };

    auto result =
        SetupDiCreateDeviceInfo(DeviceInfoSet, ClassName, &ClassGUID, NULL,
        OwnerWindow, DICD_GENERATE_ID, &DeviceInfoData) &&
        SetupDiSetDeviceRegistryProperty(DeviceInfoSet, &DeviceInfoData,
        SPDRP_HARDWAREID, (LPBYTE)hwIdList,
        (DWORD)ImScsiGetMultiStringByteLength(hwIdList)) &&
        SetupDiCallClassInstaller(DIF_REGISTERDEVICE, DeviceInfoSet,
        &DeviceInfoData);

    if (!result)
    {
        WPreserveLastError ple;

        SetupDiDestroyDeviceInfoList(DeviceInfoSet);

        WErrMsg errmsg(ple.Value);

        ImScsiDebugMessage(L"Error creating device object: %1!ws!",
            (LPCWSTR)errmsg);

        return FALSE;
    }

    SetupDiDestroyDeviceInfoList(DeviceInfoSet);

    ImScsiDebugMessage(L"Installing driver for device...");

    return UpdateDriverForPlugAndPlayDevices(OwnerWindow, hwIdList, InfPath,
        INSTALLFLAG_FORCE, RebootRequired);
}

AIMAPI_API
BOOL
WINAPI
ImScsiInstallStorPortDriver(LPWSTR SetupSource,
HWND OwnerWindow,
LPBOOL RebootRequired)
{
    LPCWSTR kernel = ImScsiGetKernelPlatformCode(NULL, NULL);

    if (kernel == NULL)
    {
        return FALSE;
    }

    if (RebootRequired != NULL)
    {
        *RebootRequired = FALSE;
    }

    if (!ImScsiRemoveDevices(OwnerWindow))
    {
        WPreserveLastError ple;

        WErrMsg errmsg;

        ImScsiDebugMessage(L"Error removing existing devices: %1!ws!",
            (LPCWSTR)errmsg);
    }

    WMem<WCHAR> infPath(ImDiskAllocPrintF(L"%1!ws!\\%2!ws!\\phdskmnt.inf",
        SetupSource, kernel));

    if (!infPath)
    {
        return FALSE;
    }

    return ImScsiCreateRootPnPDevice(OwnerWindow, infPath,
        L"root\\phdskmnt\0", RebootRequired);
}

AIMAPI_API BOOL
WINAPI
ImScsiGetOSVersion(
__inout __deref
POSVERSIONINFOW lpVersionInformation)
{
    NTSTATUS status = RtlGetVersion(lpVersionInformation);
    if (!NT_SUCCESS(status))
    {
        SetLastError(RtlNtStatusToDosError(status));
        return FALSE;
    }
    return TRUE;
}
