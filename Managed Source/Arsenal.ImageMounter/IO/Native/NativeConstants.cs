//  
//  Copyright (c) 2012-2026, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator

#pragma warning disable CA1069 // Enums values should not be duplicated
#pragma warning disable IDE0032 // Use auto property
#pragma warning disable IDE1006 // Naming Styles

namespace Arsenal.ImageMounter.IO.Native;

public static class NativeConstants
{
#if WINDOWS
    public const string SUPPORTED_WINDOWS_PLATFORM = "windows7.0";
#else
    public const string SUPPORTED_WINDOWS_PLATFORM = "windows";
#endif

    [SupportedOSPlatform(SUPPORTED_WINDOWS_PLATFORM)]
    public const FileSystemRights STANDARD_RIGHTS_REQUIRED = (FileSystemRights)0xF0000;

    public const FileAttributes FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS = (FileAttributes)0x400000;

    public const FileOptions FILE_FLAG_BACKUP_SEMANTICS = (FileOptions)0x2000000;
    public const FileOptions FILE_FLAG_OPEN_REPARSE_POINT = (FileOptions)0x200000;
    public const FileOptions FILE_FLAG_NO_BUFFERING = (FileOptions)0x20000000;

    public const int VOLUME_NAME_DOS = 0x0;
    public const int VOLUME_NAME_GUID = 0x1;
    public const int VOLUME_NAME_NONE = 0x4;
    public const int VOLUME_NAME_NT = 0x2;
    public const int FILE_NAME_NORMALIZED = 0x0;
    public const int FILE_NAME_OPENED = 0x8;

    public const uint OPEN_ALWAYS = 4U;
    public const uint OPEN_EXISTING = 3U;
    public const uint CREATE_ALWAYS = 2U;
    public const uint CREATE_NEW = 1U;
    public const uint TRUNCATE_EXISTING = 5U;

    [SupportedOSPlatform(SUPPORTED_WINDOWS_PLATFORM)]
    public const FileSystemRights EVENT_QUERY_STATE = FileSystemRights.ReadData;

    [SupportedOSPlatform(SUPPORTED_WINDOWS_PLATFORM)]
    public const FileSystemRights EVENT_MODIFY_STATE = FileSystemRights.WriteData;

    [SupportedOSPlatform(SUPPORTED_WINDOWS_PLATFORM)]
    public const FileSystemRights EVENTALLACCESS = STANDARD_RIGHTS_REQUIRED | FileSystemRights.Synchronize | FileSystemRights.ReadData | FileSystemRights.WriteData;

    public const int NO_ERROR = 0;
    public const int ERROR_INVALID_FUNCTION = 1;
    public const int ERROR_IO_DEVICE = 0x45D;
    public const int ERROR_FILE_NOT_FOUND = 2;
    public const int ERROR_PATH_NOT_FOUND = 3;
    public const int ERROR_ACCESS_DENIED = 5;
    public const int ERROR_NO_MORE_FILES = 18;
    public const int ERROR_HANDLE_EOF = 38;
    public const int ERROR_NOT_SUPPORTED = 50;
    public const int ERROR_DEV_NOT_EXIST = 55;
    public const int ERROR_INVALID_PARAMETER = 87;
    public const int ERROR_ALREADY_EXISTS = 183;
    public const int ERROR_MORE_DATA = 234;
    public const int ERROR_IO_PENDING = 997;
    public const int ERROR_NOT_ALL_ASSIGNED = 1300;
    public const int ERROR_INSUFFICIENT_BUFFER = 122;
    public const int ERROR_IN_WOW64 = unchecked((int)0xE0000235);

    public const int EPERM = 1;
    public const int ENOENT = 2;
    public const int EINTR = 4;
    public const int EIO = 5;
    public const int E2BIG = 7;
    public const int EBADF = 9;
    public const int ENOMEM = 12;
    public const int EACCES = 13;
    public const int EFAULT = 14;
    public const int EBUSY = 16;
    public const int EEXIST = 17;
    public const int EISDIR = 21;
    public const int ECONNRESET = 54;

    public const int WSABASEERR = 10000;
    public const int WSAEINTR = WSABASEERR + EINTR;
    public const int WSAECONNRESET = WSABASEERR + ECONNRESET;

    public const uint FSCTL_GET_COMPRESSION = 0x9003CU;
    public const uint FSCTL_SET_COMPRESSION = 0x9C040U;
    public const ushort COMPRESSION_FORMAT_NONE = 0;
    public const ushort COMPRESSION_FORMAT_DEFAULT = 1;
    public const uint FSCTL_SET_SPARSE = 0x900C4U;
    public const uint FSCTL_GET_RETRIEVAL_POINTERS = 0x90073U;
    public const uint FSCTL_ALLOW_EXTENDED_DASD_IO = 0x90083U;
    public const uint FSCTL_GET_VOLUME_BITMAP = (((0x00000009) << 16) | ((0) << 14) | ((27) << 2) | (3));

    public const uint FSCTL_LOCK_VOLUME = 0x90018U;
    public const uint FSCTL_DISMOUNT_VOLUME = 0x90020U;

    public const uint FSCTL_SET_REPARSE_POINT = 0x900A4U;
    public const uint FSCTL_GET_REPARSE_POINT = 0x900A8U;
    public const uint FSCTL_DELETE_REPARSE_POINT = 0x900ACU;
    public const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003U;
    public const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;

    public const uint IOCTL_SCSI_MINIPORT = 0x4D008U;
    public const uint IOCTL_SCSI_GET_ADDRESS = 0x41018U;
    public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400U;
    public const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x2D1080U;
    public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x70000U;
    public const uint IOCTL_DISK_GET_LENGTH_INFO = 0x7405CU;
    public const uint IOCTL_DISK_GET_PARTITION_INFO = 0x74004U;
    public const uint IOCTL_DISK_GET_PARTITION_INFO_EX = 0x70048U;
    public const uint IOCTL_DISK_GET_DRIVE_LAYOUT = 0x7400CU;
    public const uint IOCTL_DISK_GET_DRIVE_LAYOUT_EX = 0x70050U;
    public const uint IOCTL_DISK_SET_DRIVE_LAYOUT_EX = 0x7C054U;
    public const uint IOCTL_DISK_CREATE_DISK = 0x7C058U;
    public const uint IOCTL_STORAGE_MANAGE_DATA_SET_ATTRIBUTES = 0x2D9404;
    public const uint IOCTL_DISK_GROW_PARTITION = 0x7C0D0U;
    public const uint IOCTL_DISK_UPDATE_PROPERTIES = 0x70140U;
    public const uint IOCTL_DISK_IS_WRITABLE = 0x70024U;
    public const uint IOCTL_STORAGE_CHECK_VERIFY = 0x2D4800;
    public const uint IOCTL_STORAGE_CHECK_VERIFY2 = 0x2D0800;
    public const uint IOCTL_SCSI_RESCAN_BUS = 0x4101CU;

    public const uint IOCTL_DISK_GET_DISK_ATTRIBUTES = 0x700F0U;
    public const uint IOCTL_DISK_SET_DISK_ATTRIBUTES = 0x7C0F4U;
    public const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x560000U;
    public const uint IOCTL_VOLUME_OFFLINE = 0x56C00CU;
    public const uint IOCTL_VOLUME_ONLINE = 0x56C008U;

    public const uint FILE_DEVICE_DISK = 0x7U;

    public const int ERROR_WRITE_PROTECT = 19;
    public const int ERROR_NOT_READY = 21;
    public const int ERROR_NO_SUCH_DEVICE = 433;
    public const int FVE_E_LOCKED_VOLUME = unchecked((int)0x80310000);
    public const int ERROR_NO_SUCH_DEVINST = unchecked((int)0xe000020b);

    public const uint SC_MANAGER_CREATE_SERVICE = 0x2U;
    public const uint SC_MANAGER_ALL_ACCESS = 0xF003FU;
    public const uint SERVICE_KERNEL_DRIVER = 0x1U;
    public const uint SERVICE_FILE_SYSTEM_DRIVER = 0x2U;
    public const uint SERVICE_WIN32_OWN_PROCESS = 0x10U; // Service that runs in its own process. 
    public const uint SERVICE_WIN32_INTERACTIVE = 0x100U; // Service that runs in its own process. 
    public const uint SERVICE_WIN32_SHARE_PROCESS = 0x20U;

    public const uint SERVICE_BOOT_START = 0x0U;
    public const uint SERVICE_SYSTEM_START = 0x1U;
    public const uint SERVICE_AUTO_START = 0x2U;
    public const uint SERVICE_DEMAND_START = 0x3U;
    public const uint SERVICE_ERROR_IGNORE = 0x0U;
    public const uint SERVICE_CONTROL_STOP = 0x1U;
    public const uint ERROR_SERVICE_DOES_NOT_EXIST = 1060U;
    public const uint ERROR_SERVICE_ALREADY_RUNNING = 1056U;

    public const uint DIGCF_DEFAULT = 0x1U;
    public const uint DIGCF_PRESENT = 0x2U;
    public const uint DIGCF_ALLCLASSES = 0x4U;
    public const uint DIGCF_PROFILE = 0x8U;
    public const uint DIGCF_DEVICEINTERFACE = 0x10U;

    public const uint DRIVER_PACKAGE_DELETE_FILES = 0x20U;
    public const uint DRIVER_PACKAGE_FORCE = 0x4U;
    public const uint DRIVER_PACKAGE_SILENT = 0x2U;

    public const uint CM_GETIDLIST_FILTER_SERVICE = 0x00000002;
    public const uint CM_GETIDLIST_FILTER_CLASS = 0x00000200;
    public const uint CM_GETIDLIST_FILTER_PRESENT = 0x00000100;

    public const uint DIF_PROPERTYCHANGE = 0x12U;
    public const uint DICS_FLAG_CONFIGSPECIFIC = 0x2U;  // make change in specified profile only
    public const uint DICS_PROPCHANGE = 0x3U;

    public const uint CR_SUCCESS = 0x0U;
    public const uint CR_FAILURE = 0x13U;
    public const uint CR_NO_SUCH_VALUE = 0x25U;
    public const uint CR_NO_SUCH_REGISTRY_KEY = 0x2EU;

    public static Guid SerenumBusEnumeratorGuid { get; } = new Guid("{4D36E97B-E325-11CE-BFC1-08002BE10318}");
    public static Guid DiskDriveGuid { get; } = new Guid("{4D36E967-E325-11CE-BFC1-08002BE10318}");

    public static Guid DiskClassGuid { get; } = new Guid("{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}");
    public static Guid CdRomClassGuid { get; } = new Guid("{53F56308-B6BF-11D0-94F2-00A0C91EFB8B}");
    public static Guid StoragePortClassGuid { get; } = new Guid("{2ACCFE60-C130-11D2-B082-00A0C91EFB8B}");
    public static Guid ComPortClassGuid { get; } = new Guid("{86E0D1E0-8089-11D0-9CE4-08003E301F73}");

    public const string SE_BACKUP_NAME = "SeBackupPrivilege";
    public const string SE_RESTORE_NAME = "SeRestorePrivilege";
    public const string SE_SECURITY_NAME = "SeSecurityPrivilege";
    public const string SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege";
    public const string SE_DEBUG_NAME = "SeDebugPrivilege";
    public const string SE_TCB_NAME = "SeTcbPrivilege";
    public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

    public const uint PROCESS_DUP_HANDLE = 0x40U;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000U;

    public const uint TOKEN_QUERY = 0x8U;
    public const int TOKEN_ADJUST_PRIVILEGES = 0x20;

    public const int KEY_READ = 0x20019;
    public const int REG_OPTION_BACKUP_RESTORE = 0x4;

    public const int SE_PRIVILEGE_ENABLED = 0x2;

    public const int STATUS_INFO_LENGTH_MISMATCH = unchecked((int)0xC0000004);
    public const int STATUS_BUFFER_TOO_SMALL = unchecked((int)0xC0000023);
    public const int STATUS_BUFFER_OVERFLOW = unchecked((int)0x80000005);
    public const int STATUS_OBJECT_NAME_NOT_FOUND = unchecked((int)0xC0000034);
    public const int STATUS_BAD_COMPRESSION_BUFFER = unchecked((int)0xC0000242);

    public const int FILE_BEGIN = 0;
    public const int FILE_CURRENT = 1;
    public const int FILE_END = 2;

    public static ReadOnlyMemory<byte> DefaultBootCode { get; } = new byte[] { 0xF4, 0xEB, 0xFD };   // HLT ; JMP -3
}
