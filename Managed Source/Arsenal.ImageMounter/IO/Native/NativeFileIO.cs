//  NativeFileIO.vb
//  Routines for accessing some useful Win32 API functions to access features not
//  directly accessible through .NET Framework.
//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Collections;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Devices;
using DiscUtils.Streams;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.IO.Native;

/// <summary>
/// Provides wrappers for Win32 file API. This makes it possible to open everything that
/// CreateFile() can open and get a FileStream based .NET wrapper around the file handle.
/// </summary>
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public static partial class NativeFileIO
{

    #region Win32 API

    [SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Safe methods")]
    public static partial class SafeNativeMethods
    {
#if NET7_0_OR_GREATER
        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AllocConsole();

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool FreeConsole();

        [LibraryImport("kernel32", SetLastError = true)]
        public static partial IntPtr GetConsoleWindow();

        [LibraryImport("kernel32", SetLastError = true)]
        public static partial uint GetLogicalDrives();

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial FileAttributes GetFileAttributesW(in char lpFileName);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetFileAttributesW(in char lpFileName, FileAttributes dwFileAttributes);

        [LibraryImport("kernel32", SetLastError = true)]
        public static partial long GetTickCount64();
#else
        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllocConsole();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool FreeConsole();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint GetLogicalDrives();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern FileAttributes GetFileAttributesW(in char lpFileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetFileAttributesW(in char lpFileName, FileAttributes dwFileAttributes);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern long GetTickCount64();
#endif
    }

    public static partial class UnsafeNativeMethods
    {
#if NET7_0_OR_GREATER
        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out SafeWaitHandle lpTargetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DuplicateHandle(IntPtr hSourceProcessHandle, SafeHandle hSourceHandle, IntPtr hTargetProcessHandle, out SafeWaitHandle lpTargetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetEvent(SafeWaitHandle hEvent);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetHandleInformation(SafeHandle h, uint mask, uint flags);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetHandleInformation(SafeHandle h, out uint flags);

        [LibraryImport("ntdll")]
        internal static partial int NtCreateFile(out SafeFileHandle hFile, FileSystemRights AccessMask, in ObjectAttributes ObjectAttributes, out IoStatusBlock IoStatusBlock, in long AllocationSize, FileAttributes FileAttributes, FileShare ShareAccess, NtCreateDisposition CreateDisposition, NtCreateOptions CreateOptions, IntPtr EaBuffer, uint EaLength);

        [LibraryImport("ntdll")]
        internal static partial int NtOpenEvent(out SafeWaitHandle hEvent, uint AccessMask, in ObjectAttributes ObjectAttributes);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetFileInformationByHandle(SafeFileHandle hFile, out ByHandleFileInformation lpFileInformation);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetFileTime(SafeFileHandle hFile, [Optional] out long lpCreationTime, [Optional] out long lpLastAccessTime, [Optional] out long lpLastWriteTime);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetFileTime(SafeFileHandle hFile, [Optional] out long lpCreationTime, IntPtr lpLastAccessTime, [Optional] out long lpLastWriteTime);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetFileTime(SafeFileHandle hFile, IntPtr lpCreationTime, IntPtr lpLastAccessTime, [Optional] out long lpLastWriteTime);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetFileTime(SafeFileHandle hFile, [Optional] out long lpCreationTime, IntPtr lpLastAccessTime, IntPtr lpLastWriteTime);

        [LibraryImport("kernel32", EntryPoint = "FindFirstStreamW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeFindHandle FindFirstStreamW(in char lpFileName, uint InfoLevel, out FindStreamData lpszVolumeMountPoint, uint dwFlags);

        [LibraryImport("kernel32", EntryPoint = "FindNextStreamW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool FindNextStream(SafeFindHandle hFindStream, out FindStreamData lpszVolumeMountPoint);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeFindVolumeMountPointHandle FindFirstVolumeMountPointW(in char lpszRootPathName, out char lpszVolumeMountPoint, int cchBufferLength);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool FindNextVolumeMountPointW(SafeFindVolumeMountPointHandle hFindVolumeMountPoint, out char lpszVolumeMountPoint, int cchBufferLength);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeFindVolumeHandle FindFirstVolumeW(out char lpszVolumeName, int cchBufferLength);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool FindNextVolumeW(SafeFindVolumeHandle hFindVolumeMountPoint, out char lpszVolumeName, int cchBufferLength);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeleteVolumeMountPointW(in char lpszVolumeMountPoint);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetVolumeMountPointW(in char lpszVolumeMountPoint, in char lpszVolumeName);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetFilePointerEx(SafeFileHandle hFile, long distance_to_move, out long new_file_pointer, uint move_method);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetFilePointerEx(SafeFileHandle hFile, long distance_to_move, IntPtr ptr_new_file_pointer, uint move_method);

        [LibraryImport("advapi32", SetLastError = true)]
        internal static partial SafeServiceHandle OpenSCManagerW(IntPtr lpMachineName, IntPtr lpDatabaseName, int dwDesiredAccess);

        [LibraryImport("advapi32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeServiceHandle OpenSCManagerW(in char lpMachineName, IntPtr lpDatabaseName, int dwDesiredAccess);

        [LibraryImport("advapi32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeServiceHandle CreateServiceW(SafeServiceHandle hSCManager, in char lpServiceName, in char lpDisplayName, int dwDesiredAccess, int dwServiceType, int dwStartType, int dwErrorControl, in char lpBinaryPathName, in char lpLoadOrderGroup, IntPtr lpdwTagId, in char lpDependencies, in char lp, in char lpPassword);

        [LibraryImport("advapi32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeServiceHandle OpenServiceW(SafeServiceHandle hSCManager, in char lpServiceName, int dwDesiredAccess);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ControlService(SafeServiceHandle hSCManager, int dwControl, ref SERVICE_STATUS lpServiceStatus);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeleteService(SafeServiceHandle hSCObject);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool StartServiceW(SafeServiceHandle hService, int dwNumServiceArgs, IntPtr lpServiceArgVectors);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial IntPtr GetModuleHandleW(in char ModuleName);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial IntPtr LoadLibraryW(in char lpFileName);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool FreeLibrary(IntPtr hModule);

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial Win32FileType GetFileType(IntPtr handle);

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial Win32FileType GetFileType(SafeFileHandle handle);

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial IntPtr GetStdHandle(StdHandle nStdHandle);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetConsoleScreenBufferInfo(SafeFileHandle hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DefineDosDeviceW(DEFINE_DOS_DEVICE_FLAGS dwFlags, in char lpDeviceName, in char lpTargetPath);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int QueryDosDeviceW(in char lpDeviceName, out char lpTargetPath, int ucchMax);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int QueryDosDeviceW(IntPtr lpDeviceName, out char lpTargetPath, int ucchMax);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetVolumePathNamesForVolumeNameW(in char lpszVolumeName, out char lpszVolumePathNames, int cchBufferLength, out int lpcchReturnLength);
        
        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetVolumeNameForVolumeMountPointW(in char lpszVolumeName, out char DestinationInfFileName, int DestinationInfFileNameSize);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetCommTimeouts(SafeFileHandle hFile, out COMMTIMEOUTS lpCommTimeouts);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetCommTimeouts(SafeFileHandle hFile, in COMMTIMEOUTS lpCommTimeouts);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetVolumePathNameW(in char lpszFileName, out char lpszVolumePathName, int cchBufferLength);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeFileHandle CreateFileW(in char lpFileName, FileSystemRights dwDesiredAccess, FileShare dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool FlushFileBuffers(SafeFileHandle handle);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetFileSizeEx(SafeFileHandle hFile, out long liFileSize);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial FileAttributes GetFileAttributesW(in char path);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetDiskFreeSpaceW(in char lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);

        [LibraryImport("kernel32", EntryPoint = "DeviceIoControl", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, [MarshalAs(UnmanagedType.Bool)] in bool lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out byte lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in byte lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in byte lpInBuffer, int nInBufferSize, out byte lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, SafeBuffer? lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, SafeBuffer? lpInBuffer, uint nInBufferSize, SafeBuffer? lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, SafeBuffer? lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out long lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, out GET_DISK_ATTRIBUTES lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in DISK_GROW_PARTITION lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in SET_DISK_ATTRIBUTES lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out DISK_GEOMETRY lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out PARTITION_INFORMATION lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out PARTITION_INFORMATION_EX lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out SCSI_ADDRESS lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out STORAGE_DEVICE_NUMBER lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in STORAGE_PROPERTY_QUERY lpInBuffer, uint nInBufferSize, out STORAGE_DESCRIPTOR_HEADER lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in STORAGE_PROPERTY_QUERY lpInBuffer, uint nInBufferSize, out DEVICE_TRIM_DESCRIPTOR lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in STORAGE_PROPERTY_QUERY lpInBuffer, uint nInBufferSize, out byte lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int GetModuleFileNameW(IntPtr hModule, out char lpFilename, int nSize);

        [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Special Ansi only function")]
        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpEntryName);

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial IntPtr GetProcAddress(IntPtr hModule, IntPtr ordinal);

        [LibraryImport("ntdll", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool RtlDosPathNameToNtPathName_U(in char DosName, out UNICODE_STRING NtName, IntPtr DosFilePath, IntPtr NtFilePath);

        [LibraryImport("ntdll")]
        internal static partial void RtlFreeUnicodeString(ref UNICODE_STRING UnicodeString);

        [LibraryImport("ntdll")]
        internal static partial int RtlNtStatusToDosError(int NtStatus);

        [LibraryImport("ntdll")]
        internal static partial int RtlNtStatusToDosError(uint NtStatus);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int GetPrivateProfileSectionNamesW(out char Names, int NamesSize, in char FileName);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int GetPrivateProfileSectionW(in char SectionName, in char Values, int ValuesSize, in char FileName);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool WritePrivateProfileStringW(in char SectionName, in char SettingName, in char Value, in char FileName);

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool WritePrivateProfileStringW(IntPtr SectionName, IntPtr SettingName, IntPtr Value, IntPtr FileName);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial void InstallHinfSectionW(IntPtr hwndOwner, IntPtr hModule, in char lpCmdLine, int nCmdShow);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupCopyOEMInfW(in char SourceInfFileName, in char OEMSourceMediaLocation, uint OEMSourceMediaType, uint CopyStyle, out char DestinationInfFileName, int DestinationInfFileNameSize, out uint RequiredSize, IntPtr DestinationInfFileNameComponent);

        [LibraryImport("difxapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int DriverPackagePreinstallW(in char SourceInfFileName, uint Options);

        [LibraryImport("difxapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int DriverPackageInstallW(in char SourceInfFileName, uint Options, IntPtr pInstallerInfo, [MarshalAs(UnmanagedType.Bool)] out bool pNeedReboot);

        [LibraryImport("difxapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int DriverPackageUninstallW(in char SourceInfFileName, DriverPackageUninstallFlags Options, IntPtr pInstallerInfo, [MarshalAs(UnmanagedType.Bool)] out bool pNeedReboot);

        [LibraryImport("imagehlp", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int MapFileAndCheckSumW(in char file, out int headerSum, out int checkSum);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial uint CM_Locate_DevNodeW(in uint devInst, in char rootid, uint Flags);

        [LibraryImport("setupapi", SetLastError = true)]
        internal static partial uint CM_Get_DevNode_Registry_PropertyW(uint DevInst, CmDevNodeRegistryProperty Prop, out RegistryValueKind RegDataType, out byte Buffer, in int BufferLength, uint Flags);

        [LibraryImport("setupapi", SetLastError = true)]
        internal static partial uint CM_Set_DevNode_Registry_PropertyW(uint DevInst, CmDevNodeRegistryProperty Prop, in byte Buffer, int length, uint Flags);

        [LibraryImport("setupapi", SetLastError = true)]
        internal static partial uint CM_Get_Class_Registry_PropertyW(in Guid ClassGuid, CmClassRegistryProperty Prop, out RegistryValueKind RegDataType, out byte Buffer, in int BufferLength, uint Flags, IntPtr hMachine = default);

        [LibraryImport("setupapi", SetLastError = true)]
        internal static partial uint CM_Set_Class_Registry_PropertyW(in Guid ClassGuid, CmClassRegistryProperty Prop, in byte Buffer, int length, uint Flags, IntPtr hMachine = default);

        [LibraryImport("setupapi", SetLastError = true)]
        internal static partial uint CM_Get_Child(out uint dnDevInst, uint DevInst, uint Flags);

        [LibraryImport("setupapi", SetLastError = true)]
        internal static partial uint CM_Get_Sibling(out uint dnDevInst, uint DevInst, uint Flags);

        [LibraryImport("setupapi", SetLastError = true)]
        internal static partial uint CM_Reenumerate_DevNode(uint devInst, uint Flags);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial uint CM_Get_Device_ID_List_SizeW(out int Length, in char filter, uint Flags);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial uint CM_Get_Device_ID_ListW(in char filter, out char Buffer, uint BufferLength, uint Flags);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupSetNonInteractiveMode([MarshalAs(UnmanagedType.Bool)] bool state);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeInfHandle SetupOpenInfFileW(in char FileName, in char InfClass, uint InfStyle, out uint ErrorLine);

        public delegate uint SetupFileCallback(IntPtr Context, uint Notification, UIntPtr Param1, UIntPtr Param2);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupInstallFromInfSectionW(IntPtr hWnd, SafeInfHandle InfHandle, in char SectionName, uint Flags, IntPtr RelativeKeyRoot, in char SourceRootPath, uint CopyFlags, SetupFileCallback MsgHandler, IntPtr Context, IntPtr DeviceInfoSet, IntPtr DeviceInfoData);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupInstallFromInfSectionW(IntPtr hWnd, SafeInfHandle InfHandle, in char SectionName, uint Flags, SafeRegistryHandle RelativeKeyRoot, in char SourceRootPath, uint CopyFlags, SetupFileCallback MsgHandler, IntPtr Context, IntPtr DeviceInfoSet, IntPtr DeviceInfoData);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiGetINFClassW(string InfPath, out Guid ClassGuid, out char ClassName, uint ClassNameSize, out uint RequiredSize);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiOpenDeviceInfoW(SafeDeviceInfoSetHandle DevInfoSet, in char Enumerator, IntPtr hWndParent, uint Flags, IntPtr DeviceInfoData);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiOpenDeviceInfoW(SafeDeviceInfoSetHandle DevInfoSet, ref byte Enumerator, IntPtr hWndParent, uint Flags, IntPtr DeviceInfoData);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeDeviceInfoSetHandle SetupDiGetClassDevsW(in Guid ClassGuid, in char Enumerator, IntPtr hWndParent, uint Flags);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeDeviceInfoSetHandle SetupDiGetClassDevsW(IntPtr ClassGuid, in char Enumerator, IntPtr hWndParent, uint Flags);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiEnumDeviceInfo(SafeDeviceInfoSetHandle DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInterfaceData);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiRestartDevices(SafeDeviceInfoSetHandle DeviceInfoSet, in SP_DEVINFO_DATA DeviceInterfaceData);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiSetClassInstallParamsW(SafeDeviceInfoSetHandle DeviceInfoSet, in SP_DEVINFO_DATA DeviceInfoData, in SP_PROPCHANGE_PARAMS ClassInstallParams, int ClassInstallParamsSize);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiEnumDeviceInterfaces(SafeDeviceInfoSetHandle DeviceInfoSet, IntPtr DeviceInfoData, in Guid ClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiEnumDeviceInterfaces(SafeDeviceInfoSetHandle DeviceInfoSet, IntPtr DeviceInfoData, IntPtr ClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeDeviceInfoSetHandle SetupDiCreateDeviceInfoListExW(in Guid ClassGuid, IntPtr hwndParent, in char MachineName, IntPtr Reserved);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeDeviceInfoSetHandle SetupDiCreateDeviceInfoListExW(IntPtr ClassGuid, IntPtr hwndParent, in char MachineName, IntPtr Reserved);

        [LibraryImport("setupapi", SetLastError = true)]
        internal static partial SafeDeviceInfoSetHandle SetupDiCreateDeviceInfoList(in Guid ClassGuid, IntPtr hwndParent);

        [LibraryImport("setupapi", SetLastError = true)]
        internal static partial SafeDeviceInfoSetHandle SetupDiCreateDeviceInfoList(IntPtr ClassGuid, IntPtr hwndParent);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiGetDeviceInterfaceDetailW(SafeDeviceInfoSetHandle DeviceInfoSet, in SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, out int RequiredSize, IntPtr DeviceInfoData);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiGetDeviceInfoListDetailW(SafeDeviceInfoSetHandle devinfo, ref SP_DEVINFO_LIST_DETAIL_DATA DeviceInfoDetailData);

        [LibraryImport("setupapi", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiCreateDeviceInfoW(SafeDeviceInfoSetHandle hDevInfo, in char DeviceName, in Guid ClassGuid, in char DeviceDescription, IntPtr owner, uint CreationFlags, ref SP_DEVINFO_DATA DeviceInfoData);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiSetDeviceRegistryPropertyW(SafeDeviceInfoSetHandle hDevInfo, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, in byte PropertyBuffer, uint PropertyBufferSize);

        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupDiCallClassInstaller(uint InstallFunction, SafeDeviceInfoSetHandle hDevInfo, in SP_DEVINFO_DATA DeviceInfoData);

        [LibraryImport("newdev", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool UpdateDriverForPlugAndPlayDevicesW(IntPtr owner, in char HardwareId, in char InfPath, uint InstallFlags, [MarshalAs(UnmanagedType.Bool)] out bool RebootRequired);

        [LibraryImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ExitWindowsEx(ShutdownFlags flags, ShutdownReasons reason);

        [LibraryImport("ntdll")]
        internal static partial int RtlGetVersion(ref OSVERSIONINFO os_version);

        [LibraryImport("ntdll")]
        internal static partial int RtlGetVersion(ref OSVERSIONINFOEX os_version);

        [LibraryImport("advapi32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool LookupPrivilegeValueW(in char lpSystemName, in char lpName, out long lpLuid);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool OpenThreadToken(IntPtr hThread, uint dwAccess, [MarshalAs(UnmanagedType.Bool)] bool openAsSelf, out SafeFileHandle lpTokenHandle);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool OpenProcessToken(IntPtr hProcess, uint dwAccess, out SafeFileHandle lpTokenHandle);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool AdjustTokenPrivileges(SafeFileHandle TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, in byte NewStates, int BufferLength, out byte PreviousState, out int ReturnLength);

        [LibraryImport("ntdll")]
        internal static partial int NtQuerySystemInformation(SystemInformationClass SystemInformationClass, out byte pSystemInformation, int uSystemInformationLength, out int puReturnLength);

        [LibraryImport("ntdll")]
        internal static partial int NtQueryObject(SafeFileHandle ObjectHandle, ObjectInformationClass ObjectInformationClass, SafeBuffer ObjectInformation, int ObjectInformationLength, out int puReturnLength);

        [LibraryImport("ntdll")]
        internal static partial int NtDuplicateObject(SafeHandle SourceProcessHandle, IntPtr SourceHandle, IntPtr TargetProcessHandle, out SafeFileHandle TargetHandle, uint DesiredAccess, uint HandleAttributes, uint Options);

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial IntPtr GetCurrentProcess();

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial IntPtr GetCurrentThread();

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial SafeFileHandle OpenProcess(uint DesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool InheritHandle, int ProcessId);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CreateHardLinkW(in char newlink, in char existing, IntPtr security);

        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool MoveFileW(in char existing, in char newname);

        [LibraryImport("kernel32", SetLastError = true)]
        internal static unsafe partial char* GetCommandLineW();

        [LibraryImport("shell32", SetLastError = true)]
        internal static unsafe partial nint CommandLineToArgvW(char* cmdLine, out int numArgs);

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial nint LocalAlloc(int flags, nint numBytes);

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial nint LocalReAlloc(nint mem, int flags, nint numBytes);

        [LibraryImport("kernel32", SetLastError = true)]
        internal static partial nint LocalFree(nint mem);

        [LibraryImport("ntdll")]
        internal static partial IntPtr RtlCompareMemoryUlong(in byte buffer, IntPtr length, int v);
#else
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, IntPtr hSourceHandle, IntPtr hTargetProcessHandle, out SafeWaitHandle lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool DuplicateHandle(IntPtr hSourceProcessHandle, SafeHandle hSourceHandle, IntPtr hTargetProcessHandle, out SafeWaitHandle lpTargetHandle, uint dwDesiredAccess, bool bInheritHandle, uint dwOptions);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetEvent(SafeWaitHandle hEvent);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetHandleInformation(SafeHandle h, uint mask, uint flags);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetHandleInformation(SafeHandle h, out uint flags);

        [DllImport("ntdll", CharSet = CharSet.Unicode)]
        internal static extern int NtCreateFile(out SafeFileHandle hFile, FileSystemRights AccessMask, in ObjectAttributes ObjectAttributes, out IoStatusBlock IoStatusBlock, in long AllocationSize, FileAttributes FileAttributes, FileShare ShareAccess, NtCreateDisposition CreateDisposition, NtCreateOptions CreateOptions, IntPtr EaBuffer, uint EaLength);

        [DllImport("ntdll", CharSet = CharSet.Unicode)]
        internal static extern int NtOpenEvent(out SafeWaitHandle hEvent, uint AccessMask, in ObjectAttributes ObjectAttributes);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out ByHandleFileInformation lpFileInformation);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetFileTime(SafeFileHandle hFile, [Optional] out long lpCreationTime, [Optional] out long lpLastAccessTime, [Optional] out long lpLastWriteTime);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetFileTime(SafeFileHandle hFile, [Optional] out long lpCreationTime, IntPtr lpLastAccessTime, [Optional] out long lpLastWriteTime);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetFileTime(SafeFileHandle hFile, IntPtr lpCreationTime, IntPtr lpLastAccessTime, [Optional] out long lpLastWriteTime);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetFileTime(SafeFileHandle hFile, [Optional] out long lpCreationTime, IntPtr lpLastAccessTime, IntPtr lpLastWriteTime);

        [DllImport("kernel32", SetLastError = true, EntryPoint = "FindFirstStreamW", CharSet = CharSet.Unicode)]
        internal static extern SafeFindHandle FindFirstStreamW(in char lpFileName, uint InfoLevel, out FindStreamData lpszVolumeMountPoint, uint dwFlags);

        [DllImport("kernel32", SetLastError = true, EntryPoint = "FindNextStreamW", CharSet = CharSet.Unicode)]
        internal static extern bool FindNextStream(SafeFindHandle hFindStream, out FindStreamData lpszVolumeMountPoint);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFindVolumeMountPointHandle FindFirstVolumeMountPointW(in char lpszRootPathName, out char lpszVolumeMountPoint, int cchBufferLength);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool FindNextVolumeMountPointW(SafeFindVolumeMountPointHandle hFindVolumeMountPoint, out char lpszVolumeMountPoint, int cchBufferLength);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFindVolumeHandle FindFirstVolumeW(out char lpszVolumeName, int cchBufferLength);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool FindNextVolumeW(SafeFindVolumeHandle hFindVolumeMountPoint, out char lpszVolumeName, int cchBufferLength);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool DeleteVolumeMountPointW(in char lpszVolumeMountPoint);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetVolumeMountPointW(in char lpszVolumeMountPoint, in char lpszVolumeName);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool SetFilePointerEx(SafeFileHandle hFile, long distance_to_move, out long new_file_pointer, uint move_method);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool SetFilePointerEx(SafeFileHandle hFile, long distance_to_move, IntPtr ptr_new_file_pointer, uint move_method);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeServiceHandle OpenSCManagerW(IntPtr lpMachineName, IntPtr lpDatabaseName, int dwDesiredAccess);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeServiceHandle OpenSCManagerW(in char lpMachineName, IntPtr lpDatabaseName, int dwDesiredAccess);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeServiceHandle CreateServiceW(SafeServiceHandle hSCManager, in char lpServiceName, in char lpDisplayName, int dwDesiredAccess, int dwServiceType, int dwStartType, int dwErrorControl, in char lpBinaryPathName, in char lpLoadOrderGroup, IntPtr lpdwTagId, in char lpDependencies, in char lp, in char lpPassword);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeServiceHandle OpenServiceW(SafeServiceHandle hSCManager, in char lpServiceName, int dwDesiredAccess);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool ControlService(SafeServiceHandle hSCManager, int dwControl, ref SERVICE_STATUS lpServiceStatus);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool DeleteService(SafeServiceHandle hSCObject);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool StartServiceW(SafeServiceHandle hService, int dwNumServiceArgs, IntPtr lpServiceArgVectors);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandleW(in char ModuleName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibraryW(in char lpFileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern Win32FileType GetFileType(IntPtr handle);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern Win32FileType GetFileType(SafeFileHandle handle);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(StdHandle nStdHandle);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool GetConsoleScreenBufferInfo(SafeFileHandle hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool DefineDosDeviceW(DEFINE_DOS_DEVICE_FLAGS dwFlags, in char lpDeviceName, in char lpTargetPath);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int QueryDosDeviceW(in char lpDeviceName, out char lpTargetPath, int ucchMax);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int QueryDosDeviceW(IntPtr lpDeviceName, out char lpTargetPath, int ucchMax);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetVolumePathNamesForVolumeNameW(in char lpszVolumeName, out char lpszVolumePathNames, int cchBufferLength, out int lpcchReturnLength);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetVolumeNameForVolumeMountPointW(in char lpszVolumeName, out char DestinationInfFileName, int DestinationInfFileNameSize);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetCommTimeouts(SafeFileHandle hFile, out COMMTIMEOUTS lpCommTimeouts);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetCommTimeouts(SafeFileHandle hFile, in COMMTIMEOUTS lpCommTimeouts);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetVolumePathNameW(in char lpszFileName, out char lpszVolumePathName, int cchBufferLength);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFileHandle CreateFileW(in char lpFileName, FileSystemRights dwDesiredAccess, FileShare dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool FlushFileBuffers(SafeFileHandle handle);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool GetFileSizeEx(SafeFileHandle hFile, out long liFileSize);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern FileAttributes GetFileAttributesW(in char path);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetDiskFreeSpaceW(in char lpRootPathName, out uint lpSectorsPerCluster, out uint lpBytesPerSector, out uint lpNumberOfFreeClusters, out uint lpTotalNumberOfClusters);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in bool lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out byte lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in byte lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in byte lpInBuffer, int nInBufferSize, out byte lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, SafeBuffer? lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, SafeBuffer? lpInBuffer, uint nInBufferSize, SafeBuffer? lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, SafeBuffer? lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out long lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, int nInBufferSize, out GET_DISK_ATTRIBUTES lpOutBuffer, int nOutBufferSize, out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in DISK_GROW_PARTITION lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in SET_DISK_ATTRIBUTES lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out DISK_GEOMETRY lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out PARTITION_INFORMATION lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out PARTITION_INFORMATION_EX lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out SCSI_ADDRESS lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, out STORAGE_DEVICE_NUMBER lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in STORAGE_PROPERTY_QUERY lpInBuffer, uint nInBufferSize, out STORAGE_DESCRIPTOR_HEADER lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in STORAGE_PROPERTY_QUERY lpInBuffer, uint nInBufferSize, out DEVICE_TRIM_DESCRIPTOR lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true)]
        internal static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, in STORAGE_PROPERTY_QUERY lpInBuffer, uint nInBufferSize, out byte lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetModuleFileNameW(IntPtr hModule, out char lpFilename, int nSize);

        [SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification = "Special Ansi only function")]
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, [In][MarshalAs(UnmanagedType.LPStr)] string lpEntryName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, IntPtr ordinal);

        [DllImport("ntdll", CharSet = CharSet.Unicode)]
        internal static extern bool RtlDosPathNameToNtPathName_U(in char DosName, out UNICODE_STRING NtName, IntPtr DosFilePath, IntPtr NtFilePath);

        [DllImport("ntdll")]
        internal static extern void RtlFreeUnicodeString(ref UNICODE_STRING UnicodeString);

        [DllImport("ntdll")]
        internal static extern int RtlNtStatusToDosError(int NtStatus);

        [DllImport("ntdll")]
        internal static extern int RtlNtStatusToDosError(uint NtStatus);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetPrivateProfileSectionNamesW(out char Names, int NamesSize, in char FileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetPrivateProfileSectionW(in char SectionName, in char Values, int ValuesSize, in char FileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool WritePrivateProfileStringW(in char SectionName, in char SettingName, in char Value, in char FileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool WritePrivateProfileStringW(IntPtr SectionName, IntPtr SettingName, IntPtr Value, IntPtr FileName);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern void InstallHinfSectionW(IntPtr hwndOwner, IntPtr hModule, in char lpCmdLine, int nCmdShow);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupCopyOEMInfW(in char SourceInfFileName, in char OEMSourceMediaLocation, uint OEMSourceMediaType, uint CopyStyle, out char DestinationInfFileName, int DestinationInfFileNameSize, out uint RequiredSize, IntPtr DestinationInfFileNameComponent);

        [DllImport("difxapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverPackagePreinstallW(in char SourceInfFileName, uint Options);

        [DllImport("difxapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverPackageInstallW(in char SourceInfFileName, uint Options, IntPtr pInstallerInfo, out bool pNeedReboot);

        [DllImport("difxapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int DriverPackageUninstallW(in char SourceInfFileName, DriverPackageUninstallFlags Options, IntPtr pInstallerInfo, out bool pNeedReboot);

        [DllImport("imagehlp", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int MapFileAndCheckSumW(in char file, out int headerSum, out int checkSum);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Locate_DevNodeW(in uint devInst, in char rootid, uint Flags);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Get_DevNode_Registry_PropertyW(uint DevInst, CmDevNodeRegistryProperty Prop, out RegistryValueKind RegDataType, out byte Buffer, in int BufferLength, uint Flags);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Set_DevNode_Registry_PropertyW(uint DevInst, CmDevNodeRegistryProperty Prop, in byte Buffer, int length, uint Flags);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Get_Class_Registry_PropertyW(in Guid ClassGuid, CmClassRegistryProperty Prop, out RegistryValueKind RegDataType, out byte Buffer, in int BufferLength, uint Flags, IntPtr hMachine = default);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Set_Class_Registry_PropertyW(in Guid ClassGuid, CmClassRegistryProperty Prop, in byte Buffer, int length, uint Flags, IntPtr hMachine = default);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Get_Child(out uint dnDevInst, uint DevInst, uint Flags);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Get_Sibling(out uint dnDevInst, uint DevInst, uint Flags);

        [DllImport("setupapi", SetLastError = true)]
        internal static extern uint CM_Reenumerate_DevNode(uint devInst, uint Flags);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Get_Device_ID_List_SizeW(out int Length, in char filter, uint Flags);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint CM_Get_Device_ID_ListW(in char filter, out char Buffer, uint BufferLength, uint Flags);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupSetNonInteractiveMode(bool state);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeInfHandle SetupOpenInfFileW(in char FileName, in char InfClass, uint InfStyle, out uint ErrorLine);

        public delegate uint SetupFileCallback(IntPtr Context, uint Notification, UIntPtr Param1, UIntPtr Param2);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupInstallFromInfSectionW(IntPtr hWnd, SafeInfHandle InfHandle, in char SectionName, uint Flags, IntPtr RelativeKeyRoot, in char SourceRootPath, uint CopyFlags, SetupFileCallback MsgHandler, IntPtr Context, IntPtr DeviceInfoSet, IntPtr DeviceInfoData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupInstallFromInfSectionW(IntPtr hWnd, SafeInfHandle InfHandle, in char SectionName, uint Flags, SafeRegistryHandle RelativeKeyRoot, in char SourceRootPath, uint CopyFlags, SetupFileCallback MsgHandler, IntPtr Context, IntPtr DeviceInfoSet, IntPtr DeviceInfoData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiGetINFClassW(string InfPath, out Guid ClassGuid, out char ClassName, uint ClassNameSize, out uint RequiredSize);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiOpenDeviceInfoW(SafeDeviceInfoSetHandle DevInfoSet, in char Enumerator, IntPtr hWndParent, uint Flags, IntPtr DeviceInfoData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiOpenDeviceInfoW(SafeDeviceInfoSetHandle DevInfoSet, ref byte Enumerator, IntPtr hWndParent, uint Flags, IntPtr DeviceInfoData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeDeviceInfoSetHandle SetupDiGetClassDevsW(in Guid ClassGuid, in char Enumerator, IntPtr hWndParent, uint Flags);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeDeviceInfoSetHandle SetupDiGetClassDevsW(IntPtr ClassGuid, in char Enumerator, IntPtr hWndParent, uint Flags);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiEnumDeviceInfo(SafeDeviceInfoSetHandle DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInterfaceData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiRestartDevices(SafeDeviceInfoSetHandle DeviceInfoSet, in SP_DEVINFO_DATA DeviceInterfaceData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiSetClassInstallParamsW(SafeDeviceInfoSetHandle DeviceInfoSet, in SP_DEVINFO_DATA DeviceInfoData, in SP_PROPCHANGE_PARAMS ClassInstallParams, int ClassInstallParamsSize);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiEnumDeviceInterfaces(SafeDeviceInfoSetHandle DeviceInfoSet, IntPtr DeviceInfoData, in Guid ClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiEnumDeviceInterfaces(SafeDeviceInfoSetHandle DeviceInfoSet, IntPtr DeviceInfoData, IntPtr ClassGuid, uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeDeviceInfoSetHandle SetupDiCreateDeviceInfoListExW(in Guid ClassGuid, IntPtr hwndParent, in char MachineName, IntPtr Reserved);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeDeviceInfoSetHandle SetupDiCreateDeviceInfoListExW(IntPtr ClassGuid, IntPtr hwndParent, in char MachineName, IntPtr Reserved);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeDeviceInfoSetHandle SetupDiCreateDeviceInfoList(in Guid ClassGuid, IntPtr hwndParent);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeDeviceInfoSetHandle SetupDiCreateDeviceInfoList(IntPtr ClassGuid, IntPtr hwndParent);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiGetDeviceInterfaceDetailW(SafeDeviceInfoSetHandle DeviceInfoSet, in SP_DEVICE_INTERFACE_DATA DeviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize, out int RequiredSize, IntPtr DeviceInfoData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiGetDeviceInfoListDetailW(SafeDeviceInfoSetHandle devinfo, ref SP_DEVINFO_LIST_DETAIL_DATA DeviceInfoDetailData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiCreateDeviceInfoW(SafeDeviceInfoSetHandle hDevInfo, in char DeviceName, in Guid ClassGuid, in char DeviceDescription, IntPtr owner, uint CreationFlags, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiSetDeviceRegistryPropertyW(SafeDeviceInfoSetHandle hDevInfo, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, in byte PropertyBuffer, uint PropertyBufferSize);

        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetupDiCallClassInstaller(uint InstallFunction, SafeDeviceInfoSetHandle hDevInfo, in SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("newdev", CharSet = CharSet.Unicode)]
        internal static extern bool UpdateDriverForPlugAndPlayDevicesW(IntPtr owner, in char HardwareId, in char InfPath, uint InstallFlags, [MarshalAs(UnmanagedType.Bool)] out bool RebootRequired);

        [DllImport("user32", CharSet = CharSet.Unicode)]
        internal static extern bool ExitWindowsEx(ShutdownFlags flags, ShutdownReasons reason);

        [DllImport("ntdll", CharSet = CharSet.Unicode)]
        internal static extern int RtlGetVersion(ref OSVERSIONINFO os_version);

        [DllImport("ntdll", CharSet = CharSet.Unicode)]
        internal static extern int RtlGetVersion(ref OSVERSIONINFOEX os_version);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool LookupPrivilegeValueW(in char lpSystemName, in char lpName, out long lpLuid);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool OpenThreadToken(IntPtr hThread, uint dwAccess, bool openAsSelf, out SafeAccessTokenHandle lpTokenHandle);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool OpenProcessToken(IntPtr hProcess, uint dwAccess, out SafeAccessTokenHandle lpTokenHandle);

        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool AdjustTokenPrivileges(SafeAccessTokenHandle TokenHandle, bool DisableAllPrivileges, in byte NewStates, int BufferLength, out byte PreviousState, out int ReturnLength);

        [DllImport("ntdll", CharSet = CharSet.Unicode)]
        internal static extern int NtQuerySystemInformation(SystemInformationClass SystemInformationClass, out byte pSystemInformation, int uSystemInformationLength, out int puReturnLength);

        [DllImport("ntdll", CharSet = CharSet.Unicode)]
        internal static extern int NtQueryObject(SafeFileHandle ObjectHandle, ObjectInformationClass ObjectInformationClass, SafeBuffer ObjectInformation, int ObjectInformationLength, out int puReturnLength);

        [DllImport("ntdll", CharSet = CharSet.Unicode)]
        internal static extern int NtDuplicateObject(SafeHandle SourceProcessHandle, IntPtr SourceHandle, IntPtr TargetProcessHandle, out SafeFileHandle TargetHandle, uint DesiredAccess, uint HandleAttributes, uint Options);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetCurrentThread();

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFileHandle OpenProcess(uint DesiredAccess, bool InheritHandle, int ProcessId);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool CreateHardLinkW(in char newlink, in char existing, IntPtr security);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool MoveFileW(in char existing, in char newname);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern unsafe char* GetCommandLineW();

        [DllImport("shell32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern unsafe nint CommandLineToArgvW(char* cmdLine, out int numArgs);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern nint LocalAlloc(int flags, nint numBytes);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern nint LocalReAlloc(nint mem, int flags, nint numBytes);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern nint LocalFree(nint mem);

        [DllImport("ntdll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr RtlCompareMemoryUlong(in byte buffer, IntPtr length, int v);
#endif
    }
    #endregion

    #region Miniport Control

    /// <summary>
    /// Control methods for direct communication with SCSI miniport.
    /// </summary>
    public static class PhDiskMntCtl
    {

        public const uint SMP_IMSCSI = 0x83730000u;
        public const uint SMP_IMSCSI_QUERY_VERSION = SMP_IMSCSI | 0x800U;
        public const uint SMP_IMSCSI_CREATE_DEVICE = SMP_IMSCSI | 0x801U;
        public const uint SMP_IMSCSI_QUERY_DEVICE = SMP_IMSCSI | 0x802U;
        public const uint SMP_IMSCSI_QUERY_ADAPTER = SMP_IMSCSI | 0x803U;
        public const uint SMP_IMSCSI_CHECK = SMP_IMSCSI | 0x804U;
        public const uint SMP_IMSCSI_SET_DEVICE_FLAGS = SMP_IMSCSI | 0x805U;
        public const uint SMP_IMSCSI_REMOVE_DEVICE = SMP_IMSCSI | 0x806U;
        public const uint SMP_IMSCSI_EXTEND_DEVICE = SMP_IMSCSI | 0x807U;

        /// <summary>
        /// Signature to set in SRB_IO_CONTROL header. This identifies that sender and receiver of
        /// IOCTL_SCSI_MINIPORT requests talk to intended components only.
        /// </summary>
        private static readonly ulong SrbIoCtlSignature = MemoryMarshal.Read<ulong>("PhDskMnt"u8);

        /// <summary>
        /// Sends an IOCTL_SCSI_MINIPORT control request to a SCSI miniport.
        /// </summary>
        /// <param name="adapter">Open handle to SCSI adapter.</param>
        /// <param name="ctrlcode">Control code to set in SRB_IO_CONTROL header.</param>
        /// <param name="timeout">Timeout to set in SRB_IO_CONTROL header.</param>
        /// <param name="databytes">Optional request data after SRB_IO_CONTROL header. The Length field in
        /// SRB_IO_CONTROL header will be automatically adjusted to reflect the amount of data passed by this function.</param>
        /// <param name="returncode">ReturnCode value from SRB_IO_CONTROL header upon return.</param>
        /// <returns>This method returns a BinaryReader object that can be used to read and parse data returned after the
        /// SRB_IO_CONTROL header.</returns>
        public static unsafe Span<byte> SendSrbIoControl(SafeFileHandle adapter, uint ctrlcode, uint timeout, Span<byte> databytes, out int returncode)
        {
            var header = new SRB_IO_CONTROL(SrbIoCtlSignature, timeout, ctrlcode, databytes.Length);

            var bufferSize = header.HeaderLength + databytes.Length;

            byte[]? allocated = null;

            var indata = bufferSize <= 512
                ? stackalloc byte[bufferSize]
                : (allocated = ArrayPool<byte>.Shared.Rent(bufferSize)).AsSpan(0, bufferSize);

            try
            {
                MemoryMarshal.Write(indata, ref header);
                databytes.CopyTo(indata.Slice(header.HeaderLength));

                var Response = DeviceIoControl(adapter, NativeConstants.IOCTL_SCSI_MINIPORT, indata, 0);

                header = MemoryMarshal.Read<SRB_IO_CONTROL>(Response);

                returncode = header.ReturnCode;

                if (!databytes.IsEmpty)
                {
                    var ResponseLength = Math.Min(Math.Min(header.DataLength, Response.Length - header.HeaderLength), databytes.Length);
                    Response.Slice(header.HeaderLength, ResponseLength).CopyTo(databytes);
                    databytes = databytes.Slice(0, ResponseLength);
                }

                return databytes;
            }
            finally
            {
                if (allocated is not null)
                {
                    ArrayPool<byte>.Shared.Return(allocated);
                }
            }
        }
    }

#endregion

    public static string SystemArchitecture { get; } =
        RuntimeInformation.OSArchitecture == Architecture.X64
            ? "amd64"
            : string.Intern(RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant());

    public static string ProcessArchitecture { get; } =
        RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? "amd64"
            : string.Intern(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());

    public static void BrowseTo(string target)
        => Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        })?.Dispose();

    /// <summary>
    /// Encapsulates call to a Win32 API function that returns a BOOL value indicating success
    /// or failure and where an error value is available through a call to GetLastError() in case
    /// of failure. If value True is passed to this method it does nothing. If False is passed,
    /// it calls GetLastError(), converts error code to a HRESULT value and throws a managed
    /// exception for that HRESULT.
    /// </summary>
    /// <param name="result">Return code from a Win32 API function call.</param>
    public static void Win32Try(bool result)
    {

        if (result == false)
        {
            throw new Win32Exception();
        }
    }

    /// <summary>
    /// Encapsulates call to a Win32 API function that returns a value where failure
    /// is indicated as a NULL return and GetLastError() returns an error code. If
    /// non-zero value is passed to this method it just returns that value. If zero
    /// value is passed, it calls GetLastError() and throws a managed exception for
    /// that error code.
    /// </summary>
    /// <param name="result">Return code from a Win32 API function call.</param>
    public static T Win32Try<T>(T result)
        => result is null ? throw new Win32Exception() : result;

    /// <summary>
    /// Encapsulates call to an ntdll.dll API function that returns an NTSTATUS value indicating
    /// success or error status. If result is zero or positive, this function just passes through
    /// that value as return value. If result is negative indicating an error, it converts error
    /// code to a Win32 error code and throws a managed exception for that error code.
    /// </summary>
    /// <param name="result">Return code from a ntdll.dll API function call.</param>
    public static int NtDllTry(int result)
        => result < 0
        ? throw new Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(result))
        : result;

    public static bool OfflineDiskVolumes(string device_path, bool force)
        => OfflineDiskVolumes(device_path, force, CancellationToken.None);

    public static bool OfflineDiskVolumes(string device_path, bool force, CancellationToken cancel)
    {
        var refresh = false;

        foreach (var volume in EnumerateDiskVolumes(device_path))
        {
            cancel.ThrowIfCancellationRequested();

            try
            {
                using var device = new DiskDevice(volume.AsMemory().TrimEnd('\\').ToString(), FileAccess.ReadWrite);

                if (device.IsDiskWritable && !device.DiskPolicyReadOnly.GetValueOrDefault())
                {
                    try
                    {
                        device.FlushBuffers();
                        device.DismountVolumeFilesystem(Force: false);
                    }
                    catch (Win32Exception ex)
                    when (ex.NativeErrorCode is NativeConstants.ERROR_WRITE_PROTECT or NativeConstants.ERROR_NOT_READY or NativeConstants.ERROR_DEV_NOT_EXIST)
                    {
                        device.DismountVolumeFilesystem(Force: true);
                    }
                }
                else
                {
                    device.DismountVolumeFilesystem(Force: true);
                }

                device.SetVolumeOffline(true);

                refresh = true;

                continue;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to safely dismount volume '{volume}': {ex.JoinMessages()}");

                if (!force)
                {
                    var dev_paths = QueryDosDevice(volume.Substring(4, 44)).ToArray();
                    var in_use_apps = EnumerateProcessesHoldingFileHandle(dev_paths).Take(10).Select(FormatProcessName).ToArray();

                    if (in_use_apps.Length > 1)
                    {
                        throw new IOException($@"Failed to safely dismount volume '{volume}'.

Currently, the following applications have files open on this volume:
{string.Join(", ", in_use_apps)}", ex);
                    }
                    else if (in_use_apps.Length == 1)
                    {
                        throw new IOException($@"Failed to safely dismount volume '{volume}'.

Currently, the following application has files open on this volume:
{in_use_apps[0]}", ex);
                    }
                    else
                    {
                        throw new IOException($"Failed to safely dismount volume '{volume}'", ex);
                    }
                }
            }

            cancel.ThrowIfCancellationRequested();

            try
            {
                using var device = new DiskDevice(volume.AsMemory().TrimEnd('\\').ToString(), FileAccess.ReadWrite);
                device.FlushBuffers();
                device.DismountVolumeFilesystem(true);
                device.SetVolumeOffline(true);

                refresh = true;
                continue;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to forcefully dismount volume '{volume}': {ex.JoinMessages()}");
            }

            return false;
        }

        return refresh;

    }

    public static async Task<bool> OfflineDiskVolumesAsync(string device_path, bool force, CancellationToken cancel)
    {
        var refresh = false;

        foreach (var volume in EnumerateDiskVolumes(device_path))
        {
            cancel.ThrowIfCancellationRequested();

            try
            {
                using var device = new DiskDevice(volume.TrimEnd('\\'), FileAccess.ReadWrite);

                if (device.IsDiskWritable && !device.DiskPolicyReadOnly.GetValueOrDefault())
                {

                    Task? t = null;

                    try
                    {
                        device.FlushBuffers();
                        await device.DismountVolumeFilesystemAsync(Force: false, cancel).ConfigureAwait(false);
                    }

                    catch (Win32Exception ex)
                    when (ex.NativeErrorCode is NativeConstants.ERROR_WRITE_PROTECT or NativeConstants.ERROR_NOT_READY or NativeConstants.ERROR_DEV_NOT_EXIST)
                    {

                        t = device.DismountVolumeFilesystemAsync(Force: true, cancel);

                    }

                    if (t is not null)
                    {
                        await t.ConfigureAwait(false);
                    }
                }

                else
                {

                    await device.DismountVolumeFilesystemAsync(Force: true, cancel).ConfigureAwait(false);

                }

                device.SetVolumeOffline(true);

                refresh = true;

                continue;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to safely dismount volume '{volume}': {ex.JoinMessages()}");

                if (!force)
                {
                    var dev_paths = QueryDosDevice(volume.Substring(4, 44)).ToArray();
                    var in_use_apps = EnumerateProcessesHoldingFileHandle(dev_paths).Take(10).Select(FormatProcessName).ToArray();

                    if (in_use_apps.Length > 1)
                    {
                        throw new IOException($@"Failed to safely dismount volume '{volume}'.

Currently, the following applications have files open on this volume:
{string.Join(", ", in_use_apps)}", ex);
                    }
                    else if (in_use_apps.Length == 1)
                    {
                        throw new IOException($@"Failed to safely dismount volume '{volume}'.

Currently, the following application has files open on this volume:
{in_use_apps[0]}", ex);
                    }
                    else
                    {
                        throw new IOException($"Failed to safely dismount volume '{volume}'", ex);
                    }
                }
            }

            cancel.ThrowIfCancellationRequested();

            try
            {
                using var device = new DiskDevice(volume.TrimEnd('\\'), FileAccess.ReadWrite);
                device.FlushBuffers();
                await device.DismountVolumeFilesystemAsync(true, cancel).ConfigureAwait(false);
                device.SetVolumeOffline(true);

                refresh = true;
                continue;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to forcefully dismount volume '{volume}': {ex.JoinMessages()}");

            }

            return false;
        }

        return refresh;

    }

    public static async Task WaitForDiskIoIdleAsync(string device_path, int iterations, TimeSpan waitTime, CancellationToken cancel)
    {
        var volumes = EnumerateDiskVolumes(device_path).ToArray();

        var dev_paths = volumes.SelectMany(volume => QueryDosDevice(volume.Substring(4, 44))).ToArray();

        string[] in_use_apps;

        for (int i = 1, loopTo = iterations; i <= loopTo; i++)
        {
            cancel.ThrowIfCancellationRequested();

            in_use_apps = EnumerateProcessesHoldingFileHandle(dev_paths).Take(10).Select(FormatProcessName).ToArray();
            if (in_use_apps.Length == 0)
            {
                break;
            }

            Trace.WriteLine($"File systems still in use by process {string.Join(", ", in_use_apps)}");

            await Task.Delay(waitTime, cancel).ConfigureAwait(false);
        }
    }

    public static void EnableFileSecurityBypassPrivileges()
    {

        var privileges_enabled = EnablePrivileges(NativeConstants.SE_BACKUP_NAME, NativeConstants.SE_RESTORE_NAME, NativeConstants.SE_DEBUG_NAME, NativeConstants.SE_MANAGE_VOLUME_NAME, NativeConstants.SE_SECURITY_NAME, NativeConstants.SE_TCB_NAME);

        if (privileges_enabled is not null)
        {
            Trace.WriteLine($"Enabled privileges: {string.Join(", ", privileges_enabled)}");
        }
        else
        {
            Trace.WriteLine("Error enabling privileges.");
        }
    }

    public static void ShutdownSystem(ShutdownFlags Flags, ShutdownReasons Reason)
    {

        EnablePrivileges(NativeConstants.SE_SHUTDOWN_NAME);

        Win32Try(UnsafeNativeMethods.ExitWindowsEx(Flags, Reason));

    }

    public static string[]? EnablePrivileges(params string[] privileges)
    {
        if (!UnsafeNativeMethods.OpenThreadToken(UnsafeNativeMethods.GetCurrentThread(), (uint)((long)NativeConstants.TOKEN_ADJUST_PRIVILEGES | NativeConstants.TOKEN_QUERY), openAsSelf: true, out var token))
        {
            Win32Try(UnsafeNativeMethods.OpenProcessToken(UnsafeNativeMethods.GetCurrentProcess(), (uint)((long)NativeConstants.TOKEN_ADJUST_PRIVILEGES | NativeConstants.TOKEN_QUERY), out token));
        }

        using (token)
        {
            var intsize = PinnedBuffer<int>.TypeSize;
            var structsize = PinnedBuffer<LUID_AND_ATTRIBUTES>.TypeSize;

            var luid_and_attribs_list = new Dictionary<string, LUID_AND_ATTRIBUTES>(privileges.Length);

            foreach (var privilege in privileges)
            {
                if (UnsafeNativeMethods.LookupPrivilegeValueW('\0', privilege.AsRef(), out var luid))
                {
                    var luid_and_attribs = new LUID_AND_ATTRIBUTES(LUID: luid, attributes: NativeConstants.SE_PRIVILEGE_ENABLED);

                    luid_and_attribs_list.Add(privilege, luid_and_attribs);
                }
            }

            if (luid_and_attribs_list.Count == 0)
            {
                return null;
            }

            var bufferSize = intsize + privileges.Length * structsize;

            Span<byte> buffer = stackalloc byte[bufferSize];

            var argvalue = luid_and_attribs_list.Count;
            MemoryMarshal.Write(buffer, ref argvalue);

            for (int i = 0, loopTo = luid_and_attribs_list.Count - 1; i <= loopTo; i++)
            {
                var argvalue1 = luid_and_attribs_list.Values.ElementAtOrDefault(i);
                MemoryMarshal.Write(buffer.Slice(intsize + i * structsize), ref argvalue1);
            }

            var rc = UnsafeNativeMethods.AdjustTokenPrivileges(token, false, buffer[0], bufferSize, out buffer[0], out _);

            var err = Marshal.GetLastWin32Error();

            if (!rc)
            {
                throw new Win32Exception();
            }

            if (err == NativeConstants.ERROR_NOT_ALL_ASSIGNED)
            {

                var count = MemoryMarshal.Read<int>(buffer);
                var enabled_luids = new LUID_AND_ATTRIBUTES[count];
                MemoryMarshal.Cast<byte, LUID_AND_ATTRIBUTES>(buffer.Slice(intsize)).Slice(0, count).CopyTo(enabled_luids);

                var enabled_privileges = (from enabled_luid in enabled_luids
                                          join privilege_name in luid_and_attribs_list on enabled_luid.LUID equals privilege_name.Value.LUID
                                          select privilege_name.Key).ToArray();

                return enabled_privileges;
            }

            return privileges;
        }
    }

    private sealed class NativeWaitHandle : WaitHandle
    {
        public NativeWaitHandle(SafeWaitHandle handle)
        {
            SafeWaitHandle = handle;
        }
    }

    public static WaitHandle CreateWaitHandle(IntPtr Handle, bool inheritable)
    {
        var current_process = UnsafeNativeMethods.GetCurrentProcess();

        if (!UnsafeNativeMethods.DuplicateHandle(current_process, Handle, current_process, out var new_handle, 0U, inheritable, 0x2U))
        {
            throw new Win32Exception();
        }

        return new NativeWaitHandle(new_handle);
    }

    public static WaitHandle CreateWaitHandle(SafeHandle Handle, bool inheritable)
    {
        var current_process = UnsafeNativeMethods.GetCurrentProcess();

        if (!UnsafeNativeMethods.DuplicateHandle(current_process, Handle, current_process, out var new_handle, 0U, inheritable, 0x2U))
        {
            throw new Win32Exception();
        }

        return new NativeWaitHandle(new_handle);
    }

    public static void SetEvent(SafeWaitHandle handle)
        => Win32Try(UnsafeNativeMethods.SetEvent(handle));

    public static void SetInheritable(SafeHandle handle, bool inheritable)
        => Win32Try(UnsafeNativeMethods.SetHandleInformation(handle, 1U, inheritable ? 1U : 0U));

    public static void SetProtectFromClose(SafeHandle handle, bool protect_from_close)
        => Win32Try(UnsafeNativeMethods.SetHandleInformation(handle, 2U, protect_from_close ? 2U : 0U));

    /// <summary>
    /// Returns current system handle table.
    /// </summary>
    public static SystemHandleTableEntryInformation[] GetSystemHandleTable()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(65536);

        try
        {
            for (; ; )
            {
                var status = UnsafeNativeMethods.NtQuerySystemInformation(SystemInformationClass.SystemHandleInformation,
                                                                          out buffer[0],
                                                                          buffer.Length,
                                                                          out var argpuReturnLength);

                if (status == NativeConstants.STATUS_INFO_LENGTH_MISMATCH)
                {
                    var oldSize = buffer.Length;
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = null;
                    buffer = ArrayPool<byte>.Shared.Rent(oldSize << 1);
                    continue;
                }

                NtDllTry(status);

                break;
            }

            var handlecount = MemoryMarshal.Read<int>(buffer);
            var arrayBytes = buffer.AsSpan(IntPtr.Size);
            var array = MemoryMarshal.Cast<byte, SystemHandleTableEntryInformation>(arrayBytes).Slice(0, handlecount);

            return array.ToArray();
        }
        finally
        {
            if (buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public sealed class HandleTableEntryInformation
    {
        public SystemHandleTableEntryInformation HandleTableEntry { get; }

        public string? ObjectType { get; }

        public string? ObjectName { get; }
        public string ProcessName { get; }
        public DateTime ProcessStartTime { get; }
        public int SessionId { get; }

        internal HandleTableEntryInformation(in SystemHandleTableEntryInformation HandleTableEntry, string? ObjectType, string? ObjectName, Process Process)
        {

            this.HandleTableEntry = HandleTableEntry;
            this.ObjectType = ObjectType;
            this.ObjectName = ObjectName;
            ProcessName = Process.ProcessName;
            ProcessStartTime = Process.StartTime;
            SessionId = Process.SessionId;
        }
    }

    /// <summary>
    /// System uptime
    /// </summary>
    /// <returns>Time elapsed since system startup</returns>
    public static TimeSpan SystemUptime => TimeSpan.FromMilliseconds(SafeNativeMethods.GetTickCount64());

    public static long LastObjectNameQuueryTime { get; private set; }

    public static uint LastObjectNameQueryGrantedAccess { get; private set; }

    /// <summary>
    /// Enumerates open handles in the system.
    /// </summary>
    /// <param name="filterObjectType">Name of object types to return in the enumeration. Normally set to for example "File" to return file handles or "Key" to return registry key handles</param>
    /// <returns>Enumeration with information about each handle table entry</returns>
    public static IEnumerable<HandleTableEntryInformation>? EnumerateHandleTableHandleInformation(string filterObjectType)
        => EnumerateHandleTableHandleInformation(GetSystemHandleTable(), filterObjectType);

    private static readonly ConcurrentDictionary<byte, string?> ObjectTypes = new();

    private static IEnumerable<HandleTableEntryInformation>? EnumerateHandleTableHandleInformation(IEnumerable<SystemHandleTableEntryInformation> handleTable,
                                                                                                   string filterObjectType)
    {

        handleTable.NullCheck(nameof(handleTable));

        if (filterObjectType is not null)
        {
            filterObjectType = string.Intern(filterObjectType);
        }

        using var buffer = new HGlobalBuffer(65536);        
        using var processHandleList = new DisposableDictionary<int, SafeFileHandle?>();
        using var processInfoList = new DisposableDictionary<int, Process>();

        Array.ForEach(Process.GetProcesses(), p => processInfoList.Add(p.Id, p));

        foreach (var handle in handleTable)
        {
            string? object_type = null;
            string? object_name = null;

            if (handle.ProcessId == 0
                || (filterObjectType is not null
                && ObjectTypes.TryGetValue(handle.ObjectType, out object_type)
                && !ReferenceEquals(object_type, filterObjectType))
                || !processInfoList.TryGetValue(handle.ProcessId, out var processInfo))
            {
                continue;
            }

            if (!processHandleList.TryGetValue(handle.ProcessId, out var processHandle))
            {
                processHandle = UnsafeNativeMethods.OpenProcess(NativeConstants.PROCESS_DUP_HANDLE | NativeConstants.PROCESS_QUERY_LIMITED_INFORMATION, false, handle.ProcessId);
                if (processHandle.IsInvalid)
                {
                    processHandle = null;
                }

                processHandleList.Add(handle.ProcessId, processHandle);
            }

            if (processHandle is null)
            {
                continue;
            }

            var duphandle = new SafeFileHandle(default, true);
            var status = UnsafeNativeMethods.NtDuplicateObject(processHandle,
                                                               new IntPtr(handle.Handle),
                                                               UnsafeNativeMethods.GetCurrentProcess(),
                                                               out duphandle,
                                                               0U,
                                                               0U,
                                                               0U);

            if (status < 0)
            {
                continue;
            }

            try
            {
                int newbuffersize;

                object_type ??= ObjectTypes.GetOrAdd(handle.ObjectType, b =>
                {
                    for (; ; )
                    {
                        var rc = UnsafeNativeMethods.NtQueryObject(duphandle, ObjectInformationClass.ObjectTypeInformation, buffer, (int)buffer.ByteLength, out newbuffersize);
                        if (rc is NativeConstants.STATUS_BUFFER_TOO_SMALL or NativeConstants.STATUS_BUFFER_OVERFLOW)
                        {
                            buffer.Resize(newbuffersize);
                            continue;
                        }
                        else if (rc < 0)
                        {
                            return null;
                        }

                        break;
                    }

                    return string.Intern(buffer.Read<UNICODE_STRING>(0UL).ToString());
                });

                if (object_type is null || filterObjectType is not null && !ReferenceEquals(filterObjectType, object_type))
                {
                    continue;
                }

                if (handle.GrantedAccess is not (uint)0x12019FL and not (uint)0x12008DL and not (uint)0x120189L and not (uint)0x16019FL and not (uint)0x1A0089L and not (uint)0x1A019FL and not (uint)0x120089L and not (uint)0x100000L)
                {

                    for(; ;)
                    {
                        LastObjectNameQueryGrantedAccess = handle.GrantedAccess;
                        LastObjectNameQuueryTime = SafeNativeMethods.GetTickCount64();

                        status = UnsafeNativeMethods.NtQueryObject(duphandle, ObjectInformationClass.ObjectNameInformation, buffer, (int)buffer.ByteLength, out newbuffersize);

                        LastObjectNameQuueryTime = 0L;

                        if (status < 0 && (ulong)newbuffersize > buffer.ByteLength)
                        {
                            buffer.Resize(newbuffersize);
                            continue;
                        }
                        else if (status < 0)
                        {
                            continue;
                        }

                        break;
                    }

                    var name = buffer.Read<UNICODE_STRING>(0UL);

                    if (name.Length == 0)
                    {
                        continue;
                    }

                    object_name = name.ToString();
                }
            }
            catch
            {
            }
            finally
            {
                duphandle.Dispose();
            }

            yield return new(handle, object_type, object_name, processInfo);
        }
    }

    public static IEnumerable<int> EnumerateProcessesHoldingFileHandle(params string[] nativeFullPaths)
    {

        var paths = Array.ConvertAll(nativeFullPaths, path => new { path, dir_path = string.Concat(path, @"\") });

        return (from handle in EnumerateHandleTableHandleInformation("File")
                where handle.ObjectName is not null && !string.IsNullOrWhiteSpace(handle.ObjectName)
                    && paths.Any(path => handle.ObjectName.Equals(path.path, StringComparison.OrdinalIgnoreCase)
                        || handle.ObjectName.StartsWith(path.dir_path, StringComparison.OrdinalIgnoreCase))
                select handle.HandleTableEntry.ProcessId).Distinct();

    }

    public static string FormatProcessName(int processId)
    {
        try
        {
            using var ps = Process.GetProcessById(processId);
            return ps.SessionId == 0 || string.IsNullOrWhiteSpace(ps.MainWindowTitle)
                ? $"'{ps.ProcessName}' (id={processId})"
                : $"'{ps.MainWindowTitle}' (id={processId})";
        }

        catch
        {
            return $"id={processId}";

        }
    }

    public static bool GetDiskFreeSpace(string lpRootPathName,
                                        out uint lpSectorsPerCluster,
                                        out uint lpBytesPerSector,
                                        out uint lpNumberOfFreeClusters,
                                        out uint lpTotalNumberOfClusters)
        => UnsafeNativeMethods.GetDiskFreeSpaceW(lpRootPathName.AsRef(),
                                                 out lpSectorsPerCluster,
                                                 out lpBytesPerSector,
                                                 out lpNumberOfFreeClusters,
                                                 out lpTotalNumberOfClusters);

    public static bool DeviceIoControl(SafeFileHandle hDevice,
                                       uint dwIoControlCode,
                                       IntPtr lpInBuffer,
                                       uint nInBufferSize,
                                       IntPtr lpOutBuffer,
                                       uint nOutBufferSize,
                                       out uint lpBytesReturned,
                                       IntPtr lpOverlapped)
        => UnsafeNativeMethods.DeviceIoControl(hDevice,
                                               dwIoControlCode,
                                               lpInBuffer,
                                               nInBufferSize,
                                               lpOutBuffer,
                                               nOutBufferSize,
                                               out lpBytesReturned,
                                               lpOverlapped);

    public static bool DeviceIoControl(SafeFileHandle hDevice,
                                       uint dwIoControlCode,
                                       SafeBuffer lpInBuffer,
                                       uint nInBufferSize,
                                       SafeBuffer lpOutBuffer,
                                       uint nOutBufferSize,
                                       out uint lpBytesReturned,
                                       IntPtr lpOverlapped)
    {

        if (nInBufferSize > lpInBuffer?.ByteLength)
        {
            throw new ArgumentException("Buffer size to use in call must be within size of SafeBuffer", nameof(nInBufferSize));
        }

        return nOutBufferSize > lpOutBuffer?.ByteLength
            ? throw new ArgumentException("Buffer size to use in call must be within size of SafeBuffer", nameof(nOutBufferSize))
            : UnsafeNativeMethods.DeviceIoControl(hDevice,
                                                  dwIoControlCode,
                                                  lpInBuffer,
                                                  nInBufferSize,
                                                  lpOutBuffer,
                                                  nOutBufferSize,
                                                  out lpBytesReturned,
                                                  lpOverlapped);
    }

    /// <summary>
    /// Sends an IOCTL control request to a device driver, or an FSCTL control request to a filesystem driver.
    /// </summary>
    /// <param name="device">Open handle to filer or device.</param>
    /// <param name="ctrlcode">IOCTL or FSCTL control code.</param>
    /// <param name="data">Optional function to create input data for the control function.</param>
    /// <param name="outdatasize">Number of bytes to return.</param>
    /// <returns>This method returns a byte array that can be used to read and parse data returned by
    /// driver in the output buffer.</returns>
    public static Span<byte> DeviceIoControl(SafeFileHandle device, uint ctrlcode, Span<byte> data, int outdatasize)
    {
        var indata = (ReadOnlySpan<byte>)data;

        var indatasize = indata.Length;

        if (outdatasize > indatasize)
        {
            data = new byte[outdatasize];
        }

        var rc = UnsafeNativeMethods.DeviceIoControl(device,
                                                     ctrlcode,
                                                     indata.AsRef(),
                                                     indatasize,
                                                     out data.AsRef(),
                                                     data.Length,
                                                     out outdatasize,
                                                     IntPtr.Zero);

        if (!rc)
        {
            throw new Win32Exception();
        }

        return data.Slice(0, outdatasize);
    }

    public static FileSystemRights ConvertManagedFileAccess(FileAccess DesiredAccess)
    {

        var NativeDesiredAccess = FileSystemRights.ReadAttributes;

        if (DesiredAccess.HasFlag(FileAccess.Read))
        {
            NativeDesiredAccess |= FileSystemRights.Read;
        }

        if (DesiredAccess.HasFlag(FileAccess.Write))
        {
            NativeDesiredAccess |= FileSystemRights.Write;
        }

        return NativeDesiredAccess;

    }

    /// <summary>
    /// Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
    /// </summary>
    /// <param name="FileName">Name of file to open.</param>
    /// <param name="DesiredAccess">File access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    /// <param name="SecurityAttributes"></param>
    /// <param name="FlagsAndAttributes"></param>
    /// <param name="TemplateFile"></param>
    public static SafeFileHandle CreateFile(string FileName, FileSystemRights DesiredAccess, FileShare ShareMode, IntPtr SecurityAttributes, uint CreationDisposition, int FlagsAndAttributes, IntPtr TemplateFile)
    {

        var handle = UnsafeNativeMethods.CreateFileW(FileName.AsRef(),
                                                     DesiredAccess,
                                                     ShareMode,
                                                     SecurityAttributes,
                                                     CreationDisposition,
                                                     FlagsAndAttributes,
                                                     TemplateFile);

        return handle.IsInvalid
            ? throw new IOException($"Cannot open '{FileName}'", new Win32Exception())
            : handle;
    }

    /// <summary>
    /// Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
    /// </summary>
    /// <param name="FileName">Name of file to open.</param>
    /// <param name="DesiredAccess">File access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    /// <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
    public static SafeFileHandle OpenFileHandle(string FileName, FileAccess DesiredAccess, FileShare ShareMode, FileMode CreationDisposition, bool Overlapped)
    {
        if (string.IsNullOrWhiteSpace(FileName))
        {
            throw new ArgumentNullException(nameof(FileName));
        }

        var NativeDesiredAccess = ConvertManagedFileAccess(DesiredAccess);
        
        var NativeCreationDisposition = CreationDisposition switch
        {
            FileMode.Create => NativeConstants.CREATE_ALWAYS,
            FileMode.CreateNew => NativeConstants.CREATE_NEW,
            FileMode.Open => NativeConstants.OPEN_EXISTING,
            FileMode.OpenOrCreate => NativeConstants.OPEN_ALWAYS,
            FileMode.Truncate => NativeConstants.TRUNCATE_EXISTING,
            _ => throw new NotImplementedException(),
        };
        
        var NativeFlagsAndAttributes = FileAttributes.Normal;
        
        if (Overlapped)
        {
            NativeFlagsAndAttributes |= (FileAttributes)NativeConstants.FILE_FLAG_OVERLAPPED;
        }

        var Handle = UnsafeNativeMethods.CreateFileW(FileName.AsRef(),
                                                     NativeDesiredAccess,
                                                     ShareMode,
                                                     IntPtr.Zero,
                                                     NativeCreationDisposition,
                                                     (int)NativeFlagsAndAttributes,
                                                     IntPtr.Zero);

        return Handle.IsInvalid
            ? throw new IOException($"Cannot open {FileName}", new Win32Exception())
            : Handle;
    }

    /// <summary>
    /// Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
    /// </summary>
    /// <param name="FileName">Name of file to open.</param>
    /// <param name="DesiredAccess">File access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    /// <param name="Options">Specifies whether to request overlapped I/O.</param>
    public static SafeFileHandle OpenFileHandle(string FileName, FileAccess DesiredAccess, FileShare ShareMode, FileMode CreationDisposition, FileOptions Options)
        => OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, (uint)Options);

    /// <summary>
    /// Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
    /// </summary>
    /// <param name="FileName">Name of file to open.</param>
    /// <param name="DesiredAccess">File access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    /// <param name="Options">Specifies whether to request overlapped I/O.</param>
    public static SafeFileHandle OpenFileHandle(string FileName, FileAccess DesiredAccess, FileShare ShareMode, FileMode CreationDisposition, uint Options)
    {

        if (string.IsNullOrWhiteSpace(FileName))
        {
            throw new ArgumentNullException(nameof(FileName));
        }

        var NativeDesiredAccess = ConvertManagedFileAccess(DesiredAccess);

        uint NativeCreationDisposition;
        switch (CreationDisposition)
        {
            case FileMode.Create:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS;
                    break;
                }
            case FileMode.CreateNew:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_NEW;
                    break;
                }
            case FileMode.Open:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING;
                    break;
                }
            case FileMode.OpenOrCreate:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS;
                    break;
                }
            case FileMode.Truncate:
                {
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING;
                    break;
                }

            default:
                {
                    throw new NotImplementedException();
                }
        }

        var NativeFlagsAndAttributes = FileAttributes.Normal;

        NativeFlagsAndAttributes |= (FileAttributes)Options;

        var Handle = UnsafeNativeMethods.CreateFileW(FileName.AsRef(),
                                                     NativeDesiredAccess,
                                                     ShareMode,
                                                     IntPtr.Zero,
                                                     NativeCreationDisposition,
                                                     (int)NativeFlagsAndAttributes,
                                                     IntPtr.Zero);

        return Handle.IsInvalid
            ? throw new IOException($"Cannot open {FileName}", new Win32Exception())
            : Handle;
    }

    /// <summary>
    /// Calls NT API NtCreateFile() function and encapsulates returned handle in a SafeFileHandle object.
    /// </summary>
    /// <param name="FileName">Name of file to open.</param>
    /// <param name="DesiredAccess">File access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationOption">Specifies whether to request overlapped I/O.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    /// <param name="FileAttributes">Attributes for created file.</param>
    /// <param name="ObjectAttributes">Object attributes.</param>
    /// <param name="RootDirectory">Root directory to start path parsing from, or null for rooted path.</param>
    /// <param name="WasCreated">Return information about whether a file was created, existing file opened etc.</param>
    /// <returns>NTSTATUS value indicating result of the operation.</returns>
    public static SafeFileHandle NtCreateFile(string FileName,
                                              NtObjectAttributes ObjectAttributes,
                                              FileAccess DesiredAccess,
                                              FileShare ShareMode,
                                              NtCreateDisposition CreationDisposition,
                                              NtCreateOptions CreationOption,
                                              FileAttributes FileAttributes,
                                              SafeFileHandle? RootDirectory,
                                              out NtFileCreated WasCreated)
    {

        if (string.IsNullOrEmpty(FileName))
        {
            throw new ArgumentNullException(nameof(FileName));
        }

        var native_desired_access = ConvertManagedFileAccess(DesiredAccess) | FileSystemRights.Synchronize;

        using var filename_native = UnicodeString.Pin(FileName);

        var object_attributes = new ObjectAttributes(RootDirectory, filename_native, ObjectAttributes, null, null);

        var status = UnsafeNativeMethods.NtCreateFile(out var handle_value,
                                                      native_desired_access,
                                                      object_attributes,
                                                      out var io_status_block,
                                                      0,
                                                      FileAttributes,
                                                      ShareMode,
                                                      CreationDisposition,
                                                      CreationOption,
                                                      IntPtr.Zero,
                                                      0);

        WasCreated = (NtFileCreated)io_status_block.Information;

        return status < 0
            ? throw GetExceptionForNtStatus(status)
            : handle_value;
    }

    /// <summary>
    /// Calls NT API NtOpenEvent() function to open an event object using NT path and encapsulates returned handle in a SafeWaitHandle object.
    /// </summary>
    /// <param name="EventName">Name of event to open.</param>
    /// <param name="DesiredAccess">Access to request.</param>
    /// <param name="ObjectAttributes">Object attributes.</param>
    /// <param name="RootDirectory">Root directory to start path parsing from, or null for rooted path.</param>
    /// <returns>NTSTATUS value indicating result of the operation.</returns>
    public static SafeWaitHandle NtOpenEvent(string EventName, NtObjectAttributes ObjectAttributes, uint DesiredAccess, SafeFileHandle? RootDirectory)
    {
        if (string.IsNullOrEmpty(EventName))
        {
            throw new ArgumentNullException(nameof(EventName));
        }

        using var eventname_native = UnicodeString.Pin(EventName);

        var object_attributes = new ObjectAttributes(RootDirectory, eventname_native, ObjectAttributes, null, null);

        var status = UnsafeNativeMethods.NtOpenEvent(out var handle_value,
                                                     DesiredAccess,
                                                     object_attributes);

        return status < 0
            ? throw GetExceptionForNtStatus(status)
            : handle_value;
    }

    /// <summary>
    /// Calls Win32 API CreateFile() function to create a backup handle for a file or
    /// directory and encapsulates returned handle in a SafeFileHandle object. This
    /// handle can later be used in calls to Win32 Backup API functions or similar.
    /// </summary>
    /// <param name="FilePath">Name of file or directory to open.</param>
    /// <param name="DesiredAccess">Access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    public static SafeFileHandle OpenBackupHandle(string FilePath, FileAccess DesiredAccess, FileShare ShareMode, FileMode CreationDisposition)
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentNullException(nameof(FilePath));
        }

        var NativeDesiredAccess = FileSystemRights.ReadAttributes;
        if (DesiredAccess.HasFlag(FileAccess.Read))
        {
            NativeDesiredAccess |= FileSystemRights.Read;
        }

        if (DesiredAccess.HasFlag(FileAccess.Write))
        {
            NativeDesiredAccess |= FileSystemRights.Write;
        }

        uint NativeCreationDisposition;
        switch (CreationDisposition)
        {
            case FileMode.Create:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS;
                    break;
                }
            case FileMode.CreateNew:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_NEW;
                    break;
                }
            case FileMode.Open:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING;
                    break;
                }
            case FileMode.OpenOrCreate:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS;
                    break;
                }
            case FileMode.Truncate:
                {
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING;
                    break;
                }

            default:
                {
                    throw new NotImplementedException();
                }
        }

        var NativeFlagsAndAttributes = (FileAttributes)NativeConstants.FILE_FLAG_BACKUP_SEMANTICS;

        var Handle = UnsafeNativeMethods.CreateFileW(FilePath.AsRef(),
                                                     NativeDesiredAccess,
                                                     ShareMode,
                                                     IntPtr.Zero,
                                                     NativeCreationDisposition,
                                                     (int)NativeFlagsAndAttributes,
                                                     IntPtr.Zero);

        return Handle.IsInvalid
            ? throw new IOException($"Cannot open {FilePath}", new Win32Exception())
            : Handle;
    }

    /// <summary>
    /// Calls Win32 API CreateFile() function to create a backup handle for a file or
    /// directory and encapsulates returned handle in a SafeFileHandle object. This
    /// handle can later be used in calls to Win32 Backup API functions or similar.
    /// </summary>
    /// <param name="FilePath">Name of file or directory to open.</param>
    /// <param name="DesiredAccess">Access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    public static SafeFileHandle TryOpenBackupHandle(string FilePath, FileAccess DesiredAccess, FileShare ShareMode, FileMode CreationDisposition)
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentNullException(nameof(FilePath));
        }

        var NativeDesiredAccess = FileSystemRights.ReadAttributes;
        if (DesiredAccess.HasFlag(FileAccess.Read))
        {
            NativeDesiredAccess |= FileSystemRights.Read;
        }

        if (DesiredAccess.HasFlag(FileAccess.Write))
        {
            NativeDesiredAccess |= FileSystemRights.Write;
        }

        uint NativeCreationDisposition;
        switch (CreationDisposition)
        {
            case FileMode.Create:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS;
                    break;
                }
            case FileMode.CreateNew:
                {
                    NativeCreationDisposition = NativeConstants.CREATE_NEW;
                    break;
                }
            case FileMode.Open:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING;
                    break;
                }
            case FileMode.OpenOrCreate:
                {
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS;
                    break;
                }
            case FileMode.Truncate:
                {
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING;
                    break;
                }

            default:
                {
                    throw new NotImplementedException();
                }
        }

        var NativeFlagsAndAttributes = (FileAttributes)NativeConstants.FILE_FLAG_BACKUP_SEMANTICS;

        var Handle = UnsafeNativeMethods.CreateFileW(FilePath.AsRef(),
                                                     NativeDesiredAccess,
                                                     ShareMode,
                                                     IntPtr.Zero,
                                                     NativeCreationDisposition,
                                                     (int)NativeFlagsAndAttributes,
                                                     IntPtr.Zero);

        if (Handle.IsInvalid)
        {
            Trace.WriteLine($"Cannot open {FilePath} ({Marshal.GetLastWin32Error()})");
        }

        return Handle;
    }

    /// <summary>
    /// Converts FileAccess flags to values legal in constructor call to FileStream class.
    /// </summary>
    /// <param name="Value">FileAccess values.</param>
    private static FileAccess GetFileStreamLegalAccessValue(FileAccess Value) => Value == 0 ? FileAccess.Read : Value;

    /// <summary>
    /// Calls Win32 API CreateFile() function and encapsulates returned handle.
    /// </summary>
    /// <param name="FileName">Name of file to open.</param>
    /// <param name="DesiredAccess">File access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    /// <param name="BufferSize">Buffer size to specify in constructor call to FileStream class.</param>
    /// <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
    public static FileStream OpenFileStream(string FileName, FileMode CreationDisposition, FileAccess DesiredAccess, FileShare ShareMode, int BufferSize, bool Overlapped)
        => new(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped), GetFileStreamLegalAccessValue(DesiredAccess), BufferSize, Overlapped);

    /// <summary>
    /// Calls Win32 API CreateFile() function and encapsulates returned handle.
    /// </summary>
    /// <param name="FileName">Name of file to open.</param>
    /// <param name="DesiredAccess">File access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    /// <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
    public static FileStream OpenFileStream(string FileName, FileMode CreationDisposition, FileAccess DesiredAccess, FileShare ShareMode, bool Overlapped)
        => new(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped), GetFileStreamLegalAccessValue(DesiredAccess), 1, Overlapped);

    /// <summary>
    /// Calls Win32 API CreateFile() function and encapsulates returned handle.
    /// </summary>
    /// <param name="FileName">Name of file to open.</param>
    /// <param name="DesiredAccess">File access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="bufferSize"></param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    /// <param name="Options">Specifies whether to request overlapped I/O.</param>
    public static FileStream OpenFileStream(string FileName, FileMode CreationDisposition, FileAccess DesiredAccess, FileShare ShareMode, int bufferSize, FileOptions Options)
        => new(OpenFileHandle(FileName,
                              DesiredAccess,
                              ShareMode,
                              CreationDisposition,
                              Options),
            GetFileStreamLegalAccessValue(DesiredAccess),
            bufferSize,
            Options.HasFlag(FileOptions.Asynchronous));

    private static unsafe void SetFileCompressionState(SafeFileHandle SafeFileHandle, ushort State)
        => Win32Try(UnsafeNativeMethods.DeviceIoControl(SafeFileHandle,
                                                        NativeConstants.FSCTL_SET_COMPRESSION,
                                                        new IntPtr(&State),
                                                        2U,
                                                        IntPtr.Zero,
                                                        0U,
                                                        out _,
                                                        IntPtr.Zero));

    public static long GetFileSize(string Filename)
    {
        using var safefilehandle = TryOpenBackupHandle(Filename, 0, FileShare.ReadWrite | FileShare.Delete, FileMode.Open);

        return safefilehandle.IsInvalid ? -1 : GetFileSize(safefilehandle);
    }

    public static bool TryGetFileAttributes(string Filename, out FileAttributes attributes)
    {
        attributes = UnsafeNativeMethods.GetFileAttributesW(Filename.AsRef());

        return (int)attributes != -1;
    }

    public static long GetFileSize(SafeFileHandle SafeFileHandle)
    {
        Win32Try(UnsafeNativeMethods.GetFileSizeEx(SafeFileHandle, out var FileSize));

        return FileSize;
    }

    public static long? GetDiskSize(SafeFileHandle SafeFileHandle)
        => UnsafeNativeMethods.DeviceIoControl(SafeFileHandle,
                                               NativeConstants.IOCTL_DISK_GET_LENGTH_INFO,
                                               IntPtr.Zero,
                                               0U,
                                               out long FileSize,
                                               8U,
                                               out _,
                                               IntPtr.Zero)
            ? FileSize
            : (GetPartitionInformationEx(SafeFileHandle)?.PartitionLength);

    public static bool IsDiskWritable(SafeFileHandle SafeFileHandle)
    {

        var rc = UnsafeNativeMethods.DeviceIoControl(SafeFileHandle, NativeConstants.IOCTL_DISK_IS_WRITABLE, IntPtr.Zero, 0U, IntPtr.Zero, 0U, out _, IntPtr.Zero);
        if (rc)
        {
            return true;
        }
        else
        {
            var err = Marshal.GetLastWin32Error();

            switch (err)
            {

                case NativeConstants.ERROR_WRITE_PROTECT:
                case NativeConstants.ERROR_NOT_READY:
                case NativeConstants.FVE_E_LOCKED_VOLUME:
                    {

                        return false;
                    }

                default:
                    {
                        throw new Win32Exception(err);
                    }
            }
        }
    }

    public static void GrowPartition(SafeFileHandle DiskHandle, int PartitionNumber, long BytesToGrow)
    {

        var DiskGrowPartition = new DISK_GROW_PARTITION(PartitionNumber, BytesToGrow);

        Win32Try(UnsafeNativeMethods.DeviceIoControl(DiskHandle, NativeConstants.IOCTL_DISK_GROW_PARTITION, DiskGrowPartition, (uint)PinnedBuffer<DISK_GROW_PARTITION>.TypeSize, IntPtr.Zero, 0U, out _, IntPtr.Zero));

    }

    public static void CompressFile(SafeFileHandle SafeFileHandle)
        => SetFileCompressionState(SafeFileHandle, NativeConstants.COMPRESSION_FORMAT_DEFAULT);

    public static void UncompressFile(SafeFileHandle SafeFileHandle)
        => SetFileCompressionState(SafeFileHandle, NativeConstants.COMPRESSION_FORMAT_NONE);

    public static void AllowExtendedDASDIO(SafeFileHandle SafeFileHandle)
        => Win32Try(UnsafeNativeMethods.DeviceIoControl(SafeFileHandle,
                                                        NativeConstants.FSCTL_ALLOW_EXTENDED_DASD_IO,
                                                        IntPtr.Zero,
                                                        0U,
                                                        IntPtr.Zero,
                                                        0U,
                                                        out _,
                                                        IntPtr.Zero));

    public static string GetLongFullPath(string path)
    {

        var newpath = GetNtPath(path);

        if (newpath.StartsWith(@"\??\", StringComparison.Ordinal))
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            newpath = string.Concat(@"\\?\", newpath.AsSpan(4));
#else
            newpath = string.Concat(@"\\?\", newpath.Substring(4));
#endif
        }

        return newpath;

    }

    /// <summary>
    /// Adds a semicolon separated list of paths to the PATH environment variable of
    /// current process. Any paths already in present PATH variable are not added again.
    /// </summary>
    /// <param name="AddPaths">Semicolon separated list of directory paths</param>
    /// <param name="BeforeExisting">Indicates whether to insert new paths before existing path list or move
    /// existing of specified paths first if True, or add new paths after existing path list if False.</param>
    public static void AddProcessPaths(bool BeforeExisting, string AddPaths)
    {

        if (string.IsNullOrEmpty(AddPaths))
        {
            return;
        }

        var AddPathsArray = AddPaths.Split(';', StringSplitOptions.RemoveEmptyEntries);

        AddProcessPaths(BeforeExisting, AddPathsArray);

    }

    /// <summary>
    /// Adds a list of paths to the PATH environment variable of current process. Any
    /// paths already in present PATH variable are not added again.
    /// </summary>
    /// <param name="AddPathsArray">Array of directory paths</param>
    /// <param name="BeforeExisting">Indicates whether to insert new paths before existing path list or move
    /// existing of specified paths first if True, or add new paths after existing path list if False.</param>
    public static void AddProcessPaths(bool BeforeExisting, params string[] AddPathsArray)
    {

        if (AddPathsArray is null || AddPathsArray.Length == 0)
        {
            return;
        }

        var Paths = new List<string>(Environment.GetEnvironmentVariable("PATH")?.Split(';', StringSplitOptions.RemoveEmptyEntries)
            ?? Enumerable.Empty<string>());

        if (BeforeExisting)
        {
            foreach (var AddPath in AddPathsArray)
            {
                if (Paths.BinarySearch(AddPath, StringComparer.CurrentCultureIgnoreCase) >= 0)
                {
                    Paths.Remove(AddPath);
                }
            }

            Paths.InsertRange(0, AddPathsArray);
        }
        else
        {
            foreach (var AddPath in AddPathsArray)
            {
                if (Paths.BinarySearch(AddPath, StringComparer.CurrentCultureIgnoreCase) < 0)
                {
                    Paths.Add(AddPath);
                }
            }
        }

        Environment.SetEnvironmentVariable("PATH", Paths.Join(';'));
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    public static unsafe ReadOnlySpan<char> ProcessCommandLine
        => MemoryMarshal.CreateReadOnlySpanFromNullTerminated(UnsafeNativeMethods.GetCommandLineW());
#endif

    public static unsafe string[] GetProcessCommandLineAsArgumentArray()
    {
        var argsPtr = UnsafeNativeMethods.CommandLineToArgvW(UnsafeNativeMethods.GetCommandLineW(), out var numArgs);

        if (argsPtr == 0)
        {
            throw new Win32Exception();
        }

        using var args = new Win32LocalBuffer(address: argsPtr, numBytes: (ulong)(IntPtr.Size * numArgs), ownsHandle: true);

        var ArgsArray = new string[numArgs];
        for (int i = 0, loopTo = numArgs - 1; i <= loopTo; i++)
        {
            var ParamPtr = args.Read<nint>((ulong)(IntPtr.Size * i));
            ArgsArray[i] = Marshal.PtrToStringUni(ParamPtr)!;
        }

        return ArgsArray;
    }

    /// <summary>
    /// Locks and dismounts filesystem on a volume. Upon successful return, further access to the device
    /// can only be done through the handle passed to this function until handle is closed or lock is
    /// released.
    /// </summary>
    /// <param name="Device">Handle to device to lock and dismount.</param>
    /// <param name="Force">Indicates if True that volume should be immediately dismounted even if it
    /// cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
    /// successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
    public static bool DismountVolumeFilesystem(SafeFileHandle Device, bool Force)
    {
        var lock_result = false;

        for (var i = 0; i <= 10; i++)
        {
            if (i > 0)
            {
                Trace.WriteLine("Error locking volume, retrying...");
            }

            UnsafeNativeMethods.FlushFileBuffers(Device);

            Thread.Sleep(300);
            lock_result = UnsafeNativeMethods.DeviceIoControl(Device, NativeConstants.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0U, IntPtr.Zero, 0U, out _, default);
            if (lock_result || Marshal.GetLastWin32Error() != NativeConstants.ERROR_ACCESS_DENIED)
            {
                break;
            }
        }

        return (lock_result || Force)
            && UnsafeNativeMethods.DeviceIoControl(Device,
                                                   NativeConstants.FSCTL_DISMOUNT_VOLUME,
                                                   IntPtr.Zero,
                                                   0U,
                                                   IntPtr.Zero,
                                                   0U,
                                                   out _,
                                                   default);
    }

    /// <summary>
    /// Locks and dismounts filesystem on a volume. Upon successful return, further access to the device
    /// can only be done through the handle passed to this function until handle is closed or lock is
    /// released.
    /// </summary>
    /// <param name="Device">Handle to device to lock and dismount.</param>
    /// <param name="Force">Indicates if True that volume should be immediately dismounted even if it
    /// cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
    /// successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
    /// <param name="cancel"></param>
    public static async Task<bool> DismountVolumeFilesystemAsync(SafeFileHandle Device, bool Force, CancellationToken cancel)
    {
        var lock_result = false;

        for (var i = 0; i <= 10; i++)
        {
            if (i > 0)
            {
                Trace.WriteLine("Error locking volume, retrying...");
            }

            UnsafeNativeMethods.FlushFileBuffers(Device);

            await Task.Delay(300, cancel).ConfigureAwait(false);
            lock_result = UnsafeNativeMethods.DeviceIoControl(Device,
                                                              NativeConstants.FSCTL_LOCK_VOLUME,
                                                              IntPtr.Zero,
                                                              0U,
                                                              IntPtr.Zero,
                                                              0U,
                                                              out _,
                                                              default);
            if (lock_result || Marshal.GetLastWin32Error() != NativeConstants.ERROR_ACCESS_DENIED)
            {
                break;
            }
        }

        return (lock_result || Force)
            && UnsafeNativeMethods.DeviceIoControl(Device,
                                                   NativeConstants.FSCTL_DISMOUNT_VOLUME,
                                                   IntPtr.Zero,
                                                   0U,
                                                   IntPtr.Zero,
                                                   0U,
                                                   out _,
                                                   default);
    }

    /// <summary>
    /// Retrieves disk geometry.
    /// </summary>
    /// <param name="hDevice">Handle to device.</param>
    public static DISK_GEOMETRY? GetDiskGeometry(SafeFileHandle hDevice)
        => UnsafeNativeMethods.DeviceIoControl(hDevice,
                                               NativeConstants.IOCTL_DISK_GET_DRIVE_GEOMETRY,
                                               IntPtr.Zero,
                                               0U,
                                               out DISK_GEOMETRY DiskGeometry,
                                               (uint)PinnedBuffer<DISK_GEOMETRY>.TypeSize,
                                               out _,
                                               default)
            ? DiskGeometry
            : (DISK_GEOMETRY?)default;

    /// <summary>
    /// Retrieves SCSI address.
    /// </summary>
    /// <param name="hDevice">Handle to device.</param>
    public static SCSI_ADDRESS? GetScsiAddress(SafeFileHandle hDevice)
        => UnsafeNativeMethods.DeviceIoControl(hDevice,
                                               NativeConstants.IOCTL_SCSI_GET_ADDRESS,
                                               IntPtr.Zero,
                                               0U,
                                               out SCSI_ADDRESS ScsiAddress,
                                               (uint)PinnedBuffer<SCSI_ADDRESS>.TypeSize,
                                               out _,
                                               default)
            ? ScsiAddress
            : (SCSI_ADDRESS?)default;

    /// <summary>
    /// Retrieves SCSI address.
    /// </summary>
    /// <param name="Device">Path to device.</param>
    public static SCSI_ADDRESS? GetScsiAddress(string Device)
    {
        using var hDevice = OpenFileHandle(Device, 0, FileShare.ReadWrite, FileMode.Open, false);

        return GetScsiAddress(hDevice);
    }

    /// <summary>
    /// Retrieves status of write overlay for mounted device.
    /// </summary>
    /// <param name="NtDevicePath">Path to device.</param>
    public static SCSI_ADDRESS? GetScsiAddressForNtDevice(string NtDevicePath)
    {
        try
        {
            using var hDevice = NtCreateFile(NtDevicePath,
                                             0,
                                             0,
                                             FileShare.ReadWrite,
                                             NtCreateDisposition.Open,
                                             NtCreateOptions.NonDirectoryFile,
                                             0,
                                             null,
                                             out var argWasCreated);

            return GetScsiAddress(hDevice);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error getting SCSI address for device '{NtDevicePath}': {ex.JoinMessages()}");
            return default;
        }
    }

    /// <summary>
    /// Retrieves storage standard properties.
    /// </summary>
    /// <param name="hDevice">Handle to device.</param>
    public static StorageStandardProperties? GetStorageStandardProperties(SafeFileHandle hDevice)
    {

        var StoragePropertyQuery = new STORAGE_PROPERTY_QUERY(STORAGE_PROPERTY_ID.StorageDeviceProperty, STORAGE_QUERY_TYPE.PropertyStandardQuery);

        if (!UnsafeNativeMethods.DeviceIoControl(hDevice,
                                                 NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY,
                                                 StoragePropertyQuery,
                                                 (uint)PinnedBuffer<STORAGE_PROPERTY_QUERY>.TypeSize,
                                                 out STORAGE_DESCRIPTOR_HEADER StorageDescriptorHeader,
                                                 (uint)PinnedBuffer<STORAGE_DESCRIPTOR_HEADER>.TypeSize,
                                                 out _,
                                                 IntPtr.Zero))
        {
            return default;
        }

        byte[]? allocated = null;

        var buffer = StorageDescriptorHeader.Size <= 512
            ? stackalloc byte[StorageDescriptorHeader.Size]
            : (allocated = ArrayPool<byte>.Shared.Rent(StorageDescriptorHeader.Size)).AsSpan(0, StorageDescriptorHeader.Size);
        try
        {
            return !UnsafeNativeMethods.DeviceIoControl(hDevice,
                                                     NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY,
                                                     StoragePropertyQuery,
                                                     (uint)PinnedBuffer<STORAGE_PROPERTY_QUERY>.TypeSize,
                                                     out buffer[0],
                                                     (uint)buffer.Length,
                                                     out _,
                                                     IntPtr.Zero)
                ? default
                : (StorageStandardProperties?)new StorageStandardProperties(buffer);
        }
        finally
        {
            if (allocated is not null)
            {
                ArrayPool<byte>.Shared.Return(allocated);
            }
        }
    }

    /// <summary>
    /// Retrieves storage TRIM properties.
    /// </summary>
    /// <param name="hDevice">Handle to device.</param>
    public static bool? GetStorageTrimProperties(SafeFileHandle hDevice)
    {
        var StoragePropertyQuery = new STORAGE_PROPERTY_QUERY(STORAGE_PROPERTY_ID.StorageDeviceTrimProperty, STORAGE_QUERY_TYPE.PropertyStandardQuery);

        return !UnsafeNativeMethods.DeviceIoControl(hDevice,
                                                    NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY,
                                                    StoragePropertyQuery,
                                                    (uint)PinnedBuffer<STORAGE_PROPERTY_QUERY>.TypeSize,
                                                    out DEVICE_TRIM_DESCRIPTOR DeviceTrimDescriptor,
                                                    (uint)PinnedBuffer<DEVICE_TRIM_DESCRIPTOR>.TypeSize,
                                                    out _,
                                                    IntPtr.Zero)
            ? default
            : (bool?)(DeviceTrimDescriptor.TrimEnabled != 0);
    }

    /// <summary>
    /// Retrieves storage device number.
    /// </summary>
    /// <param name="hDevice">Handle to device.</param>
    public static STORAGE_DEVICE_NUMBER? GetStorageDeviceNumber(SafeFileHandle hDevice)
        => UnsafeNativeMethods.DeviceIoControl(hDevice,
                                               NativeConstants.IOCTL_STORAGE_GET_DEVICE_NUMBER,
                                               IntPtr.Zero,
                                               0U,
                                               out STORAGE_DEVICE_NUMBER StorageDeviceNumber,
                                               (uint)PinnedBuffer<STORAGE_DEVICE_NUMBER>.TypeSize,
                                               out _,
                                               default)
            ? StorageDeviceNumber
            : (STORAGE_DEVICE_NUMBER?)default;

    /// <summary>
    /// Retrieves PhysicalDrive or CdRom path for NT raw device path
    /// </summary>
    /// <param name="ntdevice">NT device path, such as \Device\00000001.</param>
    public static string GetPhysicalDriveNameForNtDevice(string ntdevice)
    {
        using var hDevice = NtCreateFile(ntdevice, 0, 0, FileShare.ReadWrite, NtCreateDisposition.Open, 0, 0, null, out _);

        var devnr = GetStorageDeviceNumber(hDevice);

        if (!devnr.HasValue || devnr.Value.PartitionNumber > 0)
        {
            throw new InvalidOperationException($"Device '{ntdevice}' is not a physical disk device object");
        }

        switch (devnr.Value.DeviceType)
        {
            case DeviceType.CdRom:
                {
                    return $"CdRom{devnr.Value.DeviceNumber}";
                }
            case DeviceType.Disk:
                {
                    return $"PhysicalDrive{devnr.Value.DeviceNumber}";
                }

            default:
                {
                    throw new InvalidOperationException($"Device '{ntdevice}' has unknown device type 0x{(int)devnr.Value.DeviceType:X}");
                }
        }
    }

    /// <summary>
    /// Returns directory junction target path
    /// </summary>
    /// <param name="source">Location of directory that is a junction.</param>
    public static string? QueryDirectoryJunction(string source)
    {
        using var hdir = OpenFileHandle(source,
                                        FileAccess.Write,
                                        FileShare.Read,
                                        FileMode.Open,
                                        NativeConstants.FILE_FLAG_BACKUP_SEMANTICS | NativeConstants.FILE_FLAG_OPEN_REPARSE_POINT);

        return QueryDirectoryJunction(hdir);
    }

    /// <summary>
    /// Creates a directory junction
    /// </summary>
    /// <param name="source">Location of directory to convert to a junction.</param>
    /// <param name="target">Target path for the junction.</param>
    public static void CreateDirectoryJunction(string source, string target)
        => CreateDirectoryJunction(source, target.AsSpan());

    /// <summary>
    /// Creates a directory junction
    /// </summary>
    /// <param name="source">Location of directory to convert to a junction.</param>
    /// <param name="target">Target path for the junction.</param>
    public static void CreateDirectoryJunction(string source, ReadOnlySpan<char> target)
    {
        Directory.CreateDirectory(source);

        using var hdir = OpenFileHandle(source,
                                        FileAccess.Write,
                                        FileShare.Read,
                                        FileMode.Open,
                                        NativeConstants.FILE_FLAG_BACKUP_SEMANTICS | NativeConstants.FILE_FLAG_OPEN_REPARSE_POINT);

        CreateDirectoryJunction(hdir, target);
    }

    public static void SetFileSparseFlag(SafeFileHandle file, bool flag)
        => Win32Try(UnsafeNativeMethods.DeviceIoControl(file, NativeConstants.FSCTL_SET_SPARSE, flag, 1, IntPtr.Zero, 0, out _, IntPtr.Zero));

    /// <summary>
    /// Get directory junction target path
    /// </summary>
    /// <param name="source">Handle to directory.</param>
    public static string? QueryDirectoryJunction(SafeFileHandle source)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(65533);

        try
        {
            if (!UnsafeNativeMethods.DeviceIoControl(source,
                                                     NativeConstants.FSCTL_GET_REPARSE_POINT,
                                                     IntPtr.Zero,
                                                     0U,
                                                     out buffer[0],
                                                     (uint)buffer.Length,
                                                     out var size,
                                                     IntPtr.Zero))
            {
                return null;
            }

            if (BitConverter.ToUInt32(buffer, 0) != NativeConstants.IO_REPARSE_TAG_MOUNT_POINT)
            {
                throw new InvalidDataException("Not a mount point or junction");
            }
            // DWORD ReparseTag
            // WORD ReparseDataLength
            // WORD Reserved
            // WORD NameOffset
            var name_length = BitConverter.ToUInt16(buffer, 10) - 2; // WORD NameLength
            // WORD DisplayNameOffset
            // WORD DisplayNameLength
            return Encoding.Unicode.GetString(buffer, 16, name_length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Creates a directory junction
    /// </summary>
    /// <param name="source">Handle to directory.</param>
    /// <param name="target">Target path for the junction.</param>
    public static void CreateDirectoryJunction(SafeFileHandle source, ReadOnlySpan<char> target)
    {
        var namebytes = MemoryMarshal.AsBytes(target);
        var namelength = (ushort)namebytes.Length;

        var data = new REPARSE_DATA_BUFFER(reparseDataLength: (ushort)(8 + namelength + 2 + namelength + 2),
                                           substituteNameOffset: 0,
                                           substituteNameLength: namelength,
                                           printNameOffset: (ushort)(namelength + 2),
                                           printNameLength: namelength);

        var bufferSize = PinnedBuffer<REPARSE_DATA_BUFFER>.TypeSize + namelength + 2 + namelength + 2;

        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            MemoryMarshal.Write(buffer, ref data);
            namebytes.CopyTo(buffer.AsSpan(PinnedBuffer<REPARSE_DATA_BUFFER>.TypeSize));
            namebytes.CopyTo(buffer.AsSpan(PinnedBuffer<REPARSE_DATA_BUFFER>.TypeSize + namelength + 2));

            if (!UnsafeNativeMethods.DeviceIoControl(source,
                                                     NativeConstants.FSCTL_SET_REPARSE_POINT,
                                                     buffer[0],
                                                     (uint)bufferSize,
                                                     IntPtr.Zero,
                                                     0U,
                                                     out _,
                                                     IntPtr.Zero))
            {
                throw new Win32Exception();
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static IEnumerable<string> QueryDosDevice()
    {
        const int UcchMax = 65536;

        var TargetPath = ArrayPool<char>.Shared.Rent(UcchMax);

        try
        {
            var length = UnsafeNativeMethods.QueryDosDeviceW(IntPtr.Zero, out TargetPath[0], UcchMax);

            if (length < 2)
            {
                yield break;
            }

            foreach (var name in TargetPath.AsMemory(0, length).ParseDoubleTerminatedString())
            {
                yield return name.ToString();
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(TargetPath);
        }
    }

    public static IEnumerable<string> QueryDosDevice(string DosDevice)
    {
        const int UcchMax = 65536;

        var TargetPath = ArrayPool<char>.Shared.Rent(UcchMax);

        try
        {
            var length = UnsafeNativeMethods.QueryDosDeviceW(DosDevice.AsRef(), out TargetPath[0], UcchMax);

            if (length < 2)
            {
                yield break;
            }

            foreach (var name in TargetPath.AsMemory(0, length).ParseDoubleTerminatedString())
            {
                yield return name.ToString();
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(TargetPath);
        }
    }

    public static string GetNtPath(string Win32Path)
    {
        var RC = UnsafeNativeMethods.RtlDosPathNameToNtPathName_U(Win32Path.AsRef(), out var unicode_string, IntPtr.Zero, IntPtr.Zero);

        if (!RC)
        {
            throw new IOException($"Invalid path: '{Win32Path}'");
        }

        try
        {
            return unicode_string.ToString();
        }
        finally
        {
            UnsafeNativeMethods.RtlFreeUnicodeString(ref unicode_string);
        }
    }

    public static void DeleteVolumeMountPoint(string VolumeMountPoint)
        => Win32Try(UnsafeNativeMethods.DeleteVolumeMountPointW(VolumeMountPoint.AsRef()));

    public static void SetVolumeMountPoint(string VolumeMountPoint, string VolumeName)
        => Win32Try(UnsafeNativeMethods.SetVolumeMountPointW(VolumeMountPoint.AsRef(),
                                                             VolumeName.AsRef()));

    public static char FindFirstFreeDriveLetter() => FindFirstFreeDriveLetter('D');

    public static char FindFirstFreeDriveLetter(char start)
    {
        start = char.ToUpperInvariant(start);
        if (start is < 'A' or > 'Z')
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        var logical_drives = SafeNativeMethods.GetLogicalDrives();

        for (ushort search = start, loopTo = 'Z'; search <= loopTo; search++)
        {
            if ((logical_drives & (1 << search - 'A')) == 0L)
            {
                using var key = Registry.CurrentUser.OpenSubKey($@"Network\{search}");
                if (key is null)
                {
                    return (char)search;
                }
            }
        }

        return default;
    }

    public static DiskExtent[] GetVolumeDiskExtents(SafeFileHandle volume)
    {
        // 776 is enough to hold 32 disk extent items
        const int outdatasize = 776;

        var buffer = DeviceIoControl(volume, NativeConstants.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, default, outdatasize);

        return MemoryMarshal.Cast<byte, DiskExtent>(buffer.Slice(8)).ToArray();
    }

    public static PARTITION_INFORMATION? GetPartitionInformation(string DevicePath)
    {
        using var devicehandle = OpenFileHandle(DevicePath, FileAccess.Read, FileShare.ReadWrite, FileMode.Open, (FileOptions)0);

        return GetPartitionInformation(devicehandle);
    }

    public static unsafe PARTITION_INFORMATION? GetPartitionInformation(SafeFileHandle disk)
        => UnsafeNativeMethods.DeviceIoControl(disk,
                                               NativeConstants.IOCTL_DISK_GET_PARTITION_INFO_EX,
                                               IntPtr.Zero,
                                               0U,
                                               out PARTITION_INFORMATION partition_info,
                                               (uint)sizeof(PARTITION_INFORMATION),
                                               out _,
                                               IntPtr.Zero)
            ? partition_info
            : default;

    public static PARTITION_INFORMATION_EX? GetPartitionInformationEx(string DevicePath)
    {
        using var devicehandle = OpenFileHandle(DevicePath, 0, FileShare.ReadWrite, FileMode.Open, (FileOptions)0);

        return GetPartitionInformationEx(devicehandle);
    }

    public static unsafe PARTITION_INFORMATION_EX? GetPartitionInformationEx(SafeFileHandle disk)
        => UnsafeNativeMethods.DeviceIoControl(disk,
                                               NativeConstants.IOCTL_DISK_GET_PARTITION_INFO_EX,
                                               IntPtr.Zero,
                                               0U,
                                               out PARTITION_INFORMATION_EX partition_info,
                                               (uint)sizeof(PARTITION_INFORMATION_EX),
                                               out _,
                                               IntPtr.Zero)
            ? partition_info
            : default;

    public class DriveLayoutInformationType
    {
        public DRIVE_LAYOUT_INFORMATION_EX DriveLayoutInformation { get; }

        public ReadOnlyCollection<PARTITION_INFORMATION_EX> Partitions { get; }

        public DriveLayoutInformationType(DRIVE_LAYOUT_INFORMATION_EX DriveLayoutInformation, IList<PARTITION_INFORMATION_EX> Partitions)
        {

            this.DriveLayoutInformation = DriveLayoutInformation;
            this.Partitions = new ReadOnlyCollection<PARTITION_INFORMATION_EX>(Partitions);
        }

        public override int GetHashCode() => 0;

        public override string ToString() => "N/A";

    }

    public class DriveLayoutInformationMBR : DriveLayoutInformationType
    {

        public DRIVE_LAYOUT_INFORMATION_MBR MBR { get; }

        public DriveLayoutInformationMBR(DRIVE_LAYOUT_INFORMATION_EX DriveLayoutInformation, PARTITION_INFORMATION_EX[] Partitions, DRIVE_LAYOUT_INFORMATION_MBR DriveLayoutInformationMBR)
            : base(DriveLayoutInformation, Partitions)
        {

            MBR = DriveLayoutInformationMBR;
        }

        public override int GetHashCode() => MBR.GetHashCode();

        public override string ToString() => MBR.ToString();

    }

    public class DriveLayoutInformationGPT : DriveLayoutInformationType
    {

        public DRIVE_LAYOUT_INFORMATION_GPT GPT { get; }

        public DriveLayoutInformationGPT(DRIVE_LAYOUT_INFORMATION_EX DriveLayoutInformation, PARTITION_INFORMATION_EX[] Partitions, DRIVE_LAYOUT_INFORMATION_GPT DriveLayoutInformationGPT)
            : base(DriveLayoutInformation, Partitions)
        {
            GPT = DriveLayoutInformationGPT;
        }

        public override int GetHashCode() => GPT.GetHashCode();

        public override string ToString() => GPT.ToString();
    }

    public static DriveLayoutInformationType? GetDriveLayoutEx(string DevicePath)
    {
        using var devicehandle = OpenFileHandle(DevicePath, FileAccess.Read, FileShare.ReadWrite, FileMode.Open, (FileOptions)0);

        return GetDriveLayoutEx(devicehandle);
    }

    public static DriveLayoutInformationType? GetDriveLayoutEx(SafeFileHandle disk)
    {
        var max_partitions = 4;

        for(; ;)
        {
            var buffer_size = PinnedBuffer<DRIVE_LAYOUT_INFORMATION_EX>.TypeSize
                + PinnedBuffer<DRIVE_LAYOUT_INFORMATION_GPT>.TypeSize
                + max_partitions * PinnedBuffer<PARTITION_INFORMATION_EX>.TypeSize;

            var buffer = ArrayPool<byte>.Shared.Rent(buffer_size);

            try
            {
                if (!UnsafeNativeMethods.DeviceIoControl(disk,
                                                         NativeConstants.IOCTL_DISK_GET_DRIVE_LAYOUT_EX,
                                                         IntPtr.Zero,
                                                         0U,
                                                         out buffer[0],
                                                         (uint)buffer.Length,
                                                         out var arglpBytesReturned,
                                                         IntPtr.Zero))
                {
                    if (Marshal.GetLastWin32Error() == NativeConstants.ERROR_INSUFFICIENT_BUFFER)
                    {
                        max_partitions *= 2;
                        continue;
                    }

                    return null;
                }

                var layout = MemoryMarshal.Read<DRIVE_LAYOUT_INFORMATION_EX>(buffer);

                if (layout.PartitionCount > max_partitions)
                {
                    max_partitions *= 2;
                    continue;
                }

                var partitions = new PARTITION_INFORMATION_EX[layout.PartitionCount];

                for (int i = 0, loopTo = layout.PartitionCount - 1; i <= loopTo; i++)
                {
                    var source = buffer
                        .AsSpan(PinnedBuffer<DRIVE_LAYOUT_INFORMATION_EX>.TypeSize
                        + PinnedBuffer<DRIVE_LAYOUT_INFORMATION_GPT>.TypeSize
                        + i * PinnedBuffer<PARTITION_INFORMATION_EX>.TypeSize);

                    partitions[i] = MemoryMarshal.Read<PARTITION_INFORMATION_EX>(source);
                }

                if (layout.PartitionStyle == PARTITION_STYLE.MBR)
                {
                    var mbr = MemoryMarshal.Read<DRIVE_LAYOUT_INFORMATION_MBR>(buffer.AsSpan(PinnedBuffer<DRIVE_LAYOUT_INFORMATION_EX>.TypeSize));
                    return new DriveLayoutInformationMBR(layout, partitions, mbr);
                }
                else if (layout.PartitionStyle == PARTITION_STYLE.GPT)
                {
                    var gpt = MemoryMarshal.Read<DRIVE_LAYOUT_INFORMATION_GPT>(buffer.AsSpan(PinnedBuffer<DRIVE_LAYOUT_INFORMATION_EX>.TypeSize));
                    return new DriveLayoutInformationGPT(layout, partitions, gpt);
                }
                else
                {
                    return new DriveLayoutInformationType(layout, partitions);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public static void SetDriveLayoutEx(SafeFileHandle disk, DriveLayoutInformationType layout)
    {
        layout.NullCheck(nameof(layout));

        var partition_count = Math.Min(layout.Partitions.Count, layout.DriveLayoutInformation.PartitionCount);

        var size_needed = PinnedBuffer<DRIVE_LAYOUT_INFORMATION_EX>.TypeSize
            + PinnedBuffer<DRIVE_LAYOUT_INFORMATION_GPT>.TypeSize
            + partition_count * PinnedBuffer<PARTITION_INFORMATION_EX>.TypeSize;

        var pos = 0;

        var buffer = ArrayPool<byte>.Shared.Rent(size_needed);

        try
        {
            var argvalue = layout.DriveLayoutInformation;
            MemoryMarshal.Write(buffer.AsSpan(pos), ref argvalue);

            pos += PinnedBuffer<DRIVE_LAYOUT_INFORMATION_EX>.TypeSize;

            switch (layout.DriveLayoutInformation.PartitionStyle)
            {
                case PARTITION_STYLE.MBR:
                    {
                        var argvalue1 = ((DriveLayoutInformationMBR)layout).MBR;
                        MemoryMarshal.Write(buffer.AsSpan(pos), ref argvalue1);
                        break;
                    }

                case PARTITION_STYLE.GPT:
                    {
                        var argvalue2 = ((DriveLayoutInformationGPT)layout).GPT;
                        MemoryMarshal.Write(buffer.AsSpan(pos), ref argvalue2);
                        break;
                    }
            }

            pos += PinnedBuffer<DRIVE_LAYOUT_INFORMATION_GPT>.TypeSize;

            for (int i = 0, loopTo = partition_count - 1; i <= loopTo; i++)
            {
                var tmp = layout.Partitions;
                var argvalue3 = tmp[i];
                MemoryMarshal.Write(buffer.AsSpan(pos + i * PinnedBuffer<PARTITION_INFORMATION_EX>.TypeSize), ref argvalue3);
            }

            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk,
                                                         NativeConstants.IOCTL_DISK_SET_DRIVE_LAYOUT_EX,
                                                         buffer[0],
                                                         (uint)size_needed,
                                                         IntPtr.Zero,
                                                         0u,
                                                         out _,
                                                         IntPtr.Zero));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static void FlushBuffers(SafeFileHandle handle)
        => Win32Try(UnsafeNativeMethods.FlushFileBuffers(handle));

    public static bool? GetDiskOffline(SafeFileHandle disk)
        => UnsafeNativeMethods.DeviceIoControl(disk,
                                               NativeConstants.IOCTL_DISK_GET_DISK_ATTRIBUTES,
                                               IntPtr.Zero,
                                               0,
                                               out var attribs,
                                               PinnedBuffer<GET_DISK_ATTRIBUTES>.TypeSize,
                                               out _,
                                               IntPtr.Zero)
            ? attribs.Attributes.HasFlag(DiskAttributes.Offline)
            : (bool?)default;

    private static readonly long TryParseFileTimeUtc_MaxFileTime = DateTime.MaxValue.ToFileTimeUtc();

    public static DateTime? TryParseFileTimeUtc(long filetime)
        => filetime > 0L && filetime <= TryParseFileTimeUtc_MaxFileTime
        ? DateTime.FromFileTimeUtc(filetime)
        : (DateTime?)default;

    public static bool SetFilePointer(SafeFileHandle file, long distance_to_move, out long new_file_pointer, uint move_method)
        => UnsafeNativeMethods.SetFilePointerEx(file, distance_to_move, out new_file_pointer, move_method);

    public static void SetDiskOffline(SafeFileHandle disk, bool offline)
    {
        var attribs = new SET_DISK_ATTRIBUTES(flags: DiskAttributesFlags.None, attributesMask: DiskAttributes.Offline, attributes: offline ? DiskAttributes.Offline : DiskAttributes.None);

        Win32Try(UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_SET_DISK_ATTRIBUTES,
                                      attribs, attribs.Version, IntPtr.Zero, 0,
                                      out _, IntPtr.Zero));
    }

    public static bool? GetDiskReadOnly(SafeFileHandle disk) => UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_DISK_ATTRIBUTES, IntPtr.Zero, 0, out var attribs, PinnedBuffer<GET_DISK_ATTRIBUTES>.TypeSize, out _, IntPtr.Zero)
            ? attribs.Attributes.HasFlag(DiskAttributes.ReadOnly)
            : (bool?)default;

    public static void SetDiskReadOnly(SafeFileHandle disk, bool read_only)
    {
        var attribs = new SET_DISK_ATTRIBUTES(flags: DiskAttributesFlags.None,
                                              attributesMask: DiskAttributes.ReadOnly,
                                              attributes: read_only ? DiskAttributes.ReadOnly : DiskAttributes.None);

        Win32Try(UnsafeNativeMethods.DeviceIoControl(disk,
                                                     NativeConstants.IOCTL_DISK_SET_DISK_ATTRIBUTES,
                                                     attribs,
                                                     attribs.Version,
                                                     IntPtr.Zero,
                                                     0,
                                                     out _,
                                                     IntPtr.Zero));
    }

    public static void SetVolumeOffline(SafeFileHandle disk, bool offline)
        => Win32Try(UnsafeNativeMethods.DeviceIoControl(disk, offline ? NativeConstants.IOCTL_VOLUME_OFFLINE : NativeConstants.IOCTL_VOLUME_ONLINE, IntPtr.Zero, 0U, IntPtr.Zero, 0U, out _, IntPtr.Zero));

    public static Exception GetExceptionForNtStatus(int NtStatus)
        => new Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(NtStatus));

    public static Exception GetExceptionForNtStatus(uint NtStatus)
        => new Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(NtStatus));

    public static string GetModuleFullPath(IntPtr hModule)
    {
        var str = ArrayPool<char>.Shared.Rent(32768);
        try
        {
            var PathLength = UnsafeNativeMethods.GetModuleFileNameW(hModule, out str[0], str.Length);
            return PathLength == 0
                ? throw new Win32Exception()
                : str.AsSpan(0, PathLength).ToString();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(str);
        }
    }

    public static IEnumerable<string> EnumerateDiskVolumesMountPoints(string DiskDevice)
        => EnumerateDiskVolumes(DiskDevice).SelectMany(EnumerateVolumeMountPoints);

    public static IEnumerable<string> EnumerateDiskVolumesMountPoints(uint DiskNumber)
        => EnumerateDiskVolumes(DiskNumber).SelectMany(EnumerateVolumeMountPoints);

    public static string? GetVolumeNameForVolumeMountPoint(string MountPoint)
    {
        Span<char> str = stackalloc char[50];

        if (UnsafeNativeMethods.GetVolumeNameForVolumeMountPointW(MountPoint.AsRef(),
                                                                  out str[0],
                                                                  str.Length)
            && str[0] != '\0')
        {
            return str.ReadNullTerminatedUnicodeString();
        }

        var ptr = MountPoint.AsMemory();

        if (ptr.Span.StartsWith(@"\\?\".AsSpan(), StringComparison.Ordinal))
        {
            ptr = ptr.Slice(4);
        }

        ptr = ptr.TrimEnd('\\');

        var nt_device_path = QueryDosDevice(ptr.ToString()).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(nt_device_path))
        {
            return null;
        }

        var found = (from dosdevice in QueryDosDevice()
                     where dosdevice.Length == 44 && dosdevice.StartsWith("Volume{", StringComparison.OrdinalIgnoreCase)
                     let targets = QueryDosDevice(dosdevice)
                     where targets.Any(target => target.Equals(nt_device_path, StringComparison.OrdinalIgnoreCase))
                     select dosdevice).FirstOrDefault();

        return found is null ? null : $@"\\?\{found}\";
    }

    public static string GetVolumePathName(string path)
    {
        const int CchBufferLength = 32768;

        var result = ArrayPool<char>.Shared.Rent(CchBufferLength);
        try
        {
            return UnsafeNativeMethods.GetVolumePathNameW(path.AsRef(),
                                                          out result[0],
                                                          CchBufferLength)
                ? result.AsSpan().ReadNullTerminatedUnicodeString()
                : throw new IOException($"Failed to get volume name for path '{path}'", new Win32Exception());
        }
        finally
        {
            ArrayPool<char>.Shared.Return(result);
        }
    }

    public static bool TryGetVolumePathName(string path, out string volume)
    {
        const int CchBufferLength = 32768;

        var result = ArrayPool<char>.Shared.Rent(CchBufferLength);
        try
        {
            if (UnsafeNativeMethods.GetVolumePathNameW(path.AsRef(),
                                                       out result[0],
                                                       CchBufferLength))
            {
                volume = result.AsSpan().ReadNullTerminatedUnicodeString();
                return true;
            }

            volume = null!;
            return false;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(result);
        }
    }

    private static readonly uint GetScsiAddressAndLength_SizeOfLong = (uint)PinnedBuffer<long>.TypeSize;
    private static readonly uint GetScsiAddressAndLength_SizeOfScsiAddress = (uint)PinnedBuffer<SCSI_ADDRESS>.TypeSize;

    public static ScsiAddressAndLength? GetScsiAddressAndLength(string drv)
    {
        try
        {
            using var disk = new DiskDevice(drv, FileAccess.Read);
            
            var rc = UnsafeNativeMethods.DeviceIoControl(disk.SafeFileHandle,
                                                         NativeConstants.IOCTL_SCSI_GET_ADDRESS,
                                                         IntPtr.Zero,
                                                         0U,
                                                         out SCSI_ADDRESS ScsiAddress,
                                                         GetScsiAddressAndLength_SizeOfScsiAddress,
                                                         out var arglpBytesReturned,
                                                         default);

            if (!rc)
            {
                Trace.WriteLine($"IOCTL_SCSI_GET_ADDRESS failed for device {drv}: Error 0x{Marshal.GetLastWin32Error():X}");
                return default;
            }

            rc = UnsafeNativeMethods.DeviceIoControl(disk.SafeFileHandle, NativeConstants.IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0U, out
            long Length, GetScsiAddressAndLength_SizeOfLong, out var arglpBytesReturned1, default);

            if (!rc)
            {
                Trace.WriteLine($"IOCTL_DISK_GET_LENGTH_INFO failed for device {drv}: Error 0x{Marshal.GetLastWin32Error():X}");
                return default;
            }

            return new ScsiAddressAndLength(ScsiAddress, Length);
        }

        catch (Exception ex)
        {
            Trace.WriteLine($"Exception attempting to find SCSI address for device {drv}: {ex.JoinMessages()}");
            return default;

        }
    }

    private static readonly ReadOnlyDictionary<uint, string> emptyDeviceNumberLookup
        = new(new Dictionary<uint, string>());

    public static ReadOnlyDictionary<uint, string> GetDevicesScsiAddresses(ScsiAdapter adapter)
    {
        var deviceList = adapter.GetDeviceList();
        
        if (deviceList.Length == 0)
        {
            return emptyDeviceNumberLookup;
        }

        var q = from device_number in deviceList
                let drv = adapter.GetDeviceName(device_number)
                where drv is not null
                select (device_number, drv);

        return new(q.ToDictionary(o => o.device_number, o => o.drv));
    }

    public static string GetMountPointBasedPath(string path)
    {
        const string volume_path_prefix = @"\\?\Volume{00000000-0000-0000-0000-000000000000}\";

        if (path.Length > volume_path_prefix.Length && path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase))
        {
            var vol = path.Substring(0, volume_path_prefix.Length);
            var mountpoint = EnumerateVolumeMountPoints(vol)?.FirstOrDefault();

            if (mountpoint is not null)
            {
                return $"{mountpoint}{path.AsMemory(volume_path_prefix.Length)}";
            }
        }

        return path.ToString();

    }

    public static IEnumerable<string> EnumerateVolumeMountPoints(string VolumeName)
    {
        const int CchBufferLength = 65536;

        var TargetPath = ArrayPool<char>.Shared.Rent(CchBufferLength);

        try
        {
            if (UnsafeNativeMethods.GetVolumePathNamesForVolumeNameW(VolumeName.AsRef(),
                                                                     out TargetPath[0],
                                                                     CchBufferLength,
                                                                     out var length) && length > 2)
            {
                foreach (var s in TargetPath.AsMemory(0, length).ParseDoubleTerminatedString())
                {
                    yield return s.ToString();
                }

                yield break;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(TargetPath);
        }

        var volumeNamePtr = VolumeName.AsMemory();

        if (volumeNamePtr.Span.StartsWith(@"\\?\Volume{".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            volumeNamePtr = volumeNamePtr.Slice(@"\\?\".Length, 44);
        }
        else if (volumeNamePtr.Span.StartsWith("Volume{".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            volumeNamePtr = volumeNamePtr.Slice(0, 44);
        }
        else
        {
            yield break;
        }

        VolumeName = volumeNamePtr.ToString();

        var targetdev = QueryDosDevice(VolumeName).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(targetdev))
        {
            yield break;
        }

        var namelinks = from link in QueryDosDevice()
                        where link.Length == 2 && link[1] == ':'
                        from target in QueryDosDevice(link)
                        where targetdev.Equals(target, StringComparison.OrdinalIgnoreCase)
                        select link;

        foreach (var namelink in namelinks)
        {
            yield return $@"{namelink}\";
        }
    }

    public static IEnumerable<string> EnumerateDiskVolumes(string? DevicePath)
    {
        if (DevicePath is null)
        {
            return Enumerable.Empty<string>();
        }
        else if (DevicePath.StartsWith(@"\\?\PhysicalDrive", StringComparison.OrdinalIgnoreCase)
            || DevicePath.StartsWith(@"\\.\PhysicalDrive", StringComparison.OrdinalIgnoreCase))          // \\?\PhysicalDrive paths to partitioned disks
        {

#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
            return EnumerateDiskVolumes(uint.Parse(DevicePath.AsSpan(@"\\?\PhysicalDrive".Length)));
#else
            return EnumerateDiskVolumes(uint.Parse(DevicePath.Substring(@"\\?\PhysicalDrive".Length)));
#endif
        }
        else if (DevicePath.StartsWith(@"\\?\", StringComparison.Ordinal)
            || DevicePath.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return EnumerateVolumeNamesForDeviceObject(QueryDosDevice(DevicePath.Substring(@"\\?\".Length)).First());     // \\?\C: or similar paths to mounted volumes
        }
        else
        {
            return Enumerable.Empty<string>();
        }
    }

    public static IEnumerable<string> EnumerateDiskVolumes(uint DiskNumber) =>
        VolumeEnumerator.Volumes
        .Where(volumeGuid =>
        {
            try
            {
                return VolumeUsesDisk(volumeGuid, DiskNumber);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{volumeGuid}: {ex.JoinMessages()}");
                return false;
            }
        });

#if NET6_0_OR_GREATER
    public static IEnumerable<string> EnumerateVolumeNamesForDeviceObject(string DeviceObject)
        => DeviceObject.EndsWith('}')
        && DeviceObject.StartsWith(@"\Device\Volume{", StringComparison.Ordinal)
        ? SingleValueEnumerable.Get($@"\\?\{DeviceObject.AsSpan(@"\Device\".Length)}\")
        : VolumeEnumerator.Volumes.Where(volumeGuidStr =>
        {
            var volumeGuid = volumeGuidStr.AsMemory();

            try
            {
                if (volumeGuid.Span.StartsWith(@"\\?\".AsSpan(), StringComparison.Ordinal))
                {
                    volumeGuid = volumeGuid.Slice(4);
                }

                volumeGuid = volumeGuid.TrimEnd('\\');

                return QueryDosDevice(volumeGuid.ToString())
                    .Any(target => target.Equals(DeviceObject, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{volumeGuidStr}: {ex.JoinMessages()}");
                return false;
            }
        });
#else
    public static IEnumerable<string> EnumerateVolumeNamesForDeviceObject(string DeviceObject)
        => DeviceObject.EndsWith('}')
        && DeviceObject.StartsWith(@"\Device\Volume{", StringComparison.Ordinal)
        ? SingleValueEnumerable.Get($@"\\?\{DeviceObject.Substring(@"\Device\".Length)}\")
        : VolumeEnumerator.Volumes.Where(volumeGuidStr =>
        {
            var volumeGuid = volumeGuidStr.AsMemory();

            try
            {
                if (volumeGuid.Span.StartsWith(@"\\?\".AsSpan(), StringComparison.Ordinal))
                {
                    volumeGuid = volumeGuid.Slice(4);
                }

                volumeGuid = volumeGuid.TrimEnd('\\');

                return QueryDosDevice(volumeGuid.ToString())
                    .Any(target => target.Equals(DeviceObject, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{volumeGuidStr}: {ex.JoinMessages()}");
                return false;
            }
        });
#endif

    public static bool VolumeUsesDisk(string VolumeGuid, uint DiskNumber)
    {
        using var volume = new DiskDevice(VolumeGuid.TrimEnd('\\'), 0);

        try
        {
            var extents = GetVolumeDiskExtents(volume.SafeFileHandle);

            return extents.Any(extent => extent.DiskNumber == DiskNumber);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == NativeConstants.ERROR_INVALID_FUNCTION)
        {
            return false;
        }
    }

    public static void ScanForHardwareChanges() => ScanForHardwareChanges(null);

    public static uint ScanForHardwareChanges(string? rootid)
    {
        var devInst = 0u;

        var status = UnsafeNativeMethods.CM_Locate_DevNodeW(devInst, rootid.AsRef(), 0);

        return status != 0L ? status : UnsafeNativeMethods.CM_Reenumerate_DevNode(devInst, 0U);
    }

    public static uint? GetDevInst(string devinstName)
    {
        var devInst = 0u;

        var status = UnsafeNativeMethods.CM_Locate_DevNodeW(devInst, devinstName.AsRef(), 0);

        if (status != 0L)
        {
            Trace.WriteLine($"Device '{devinstName}' error 0x{status:X}");
            return default;
        }

#if DEBUG
        Trace.WriteLine($"{devinstName} = devInst {devInst}");
#endif

        return devInst;
    }

    public static uint EnumerateDeviceInstancesForService(string service, out IEnumerable<ReadOnlyMemory<char>>? instances)
    {
        instances = null;
        var status = UnsafeNativeMethods.CM_Get_Device_ID_List_SizeW(out var length, service.AsRef(), NativeConstants.CM_GETIDLIST_FILTER_SERVICE);

        if (status != 0L)
        {
            return status;
        }

        var Buffer = new char[length];
        status = UnsafeNativeMethods.CM_Get_Device_ID_ListW(service.AsRef(),
                                                            out Buffer[0],
                                                            (uint)Buffer.Length,
                                                            NativeConstants.CM_GETIDLIST_FILTER_SERVICE);

        if (status != 0L)
        {
            return status;
        }

        instances = Buffer.AsMemory(0, length).ParseDoubleTerminatedString();

        return status;
    }

    public static uint EnumerateDeviceInstancesForSetupClass(string service, out IEnumerable<ReadOnlyMemory<char>>? instances)
    {
        instances = null;

        var status = UnsafeNativeMethods.CM_Get_Device_ID_List_SizeW(out var length, service.AsRef(), NativeConstants.CM_GETIDLIST_FILTER_SERVICE);

        if (status != 0L)
        {
            return status;
        }

        var Buffer = new char[length];
        status = UnsafeNativeMethods.CM_Get_Device_ID_ListW(service.AsRef(),
                                                            out Buffer[0],
                                                            (uint)Buffer.Length,
                                                            NativeConstants.CM_GETIDLIST_FILTER_SERVICE);

        if (status != 0L)
        {
            return status;
        }

        instances = Buffer.AsMemory(0, length).ParseDoubleTerminatedString();

        return status;
    }

    public static void RestartDevice(Guid devclass, uint devinst)
    {
        // ' get a list of devices which support the given interface
        using var devinfo = UnsafeNativeMethods.SetupDiGetClassDevsW(devclass,
                           default,
                           default,
                           NativeConstants.DIGCF_PROFILE |
                           NativeConstants.DIGCF_DEVICEINTERFACE |
                           NativeConstants.DIGCF_PRESENT);

        if (devinfo.IsInvalid)
        {
            throw new DriveNotFoundException("Device not found");
        }

        var devInfoData = new SP_DEVINFO_DATA();

        // ' step through the list of devices for this handle
        // ' get device info at index deviceIndex, the function returns FALSE
        // ' when there is no device at the given index.
        var deviceIndex = 0U;

        while (UnsafeNativeMethods.SetupDiEnumDeviceInfo(devinfo, deviceIndex, ref devInfoData))
        {
            if (devInfoData.DevInst == devinst)
            {
                var pcp = new SP_PROPCHANGE_PARAMS(classInstallHeader: new SP_CLASSINSTALL_HEADER(installFunction: NativeConstants.DIF_PROPERTYCHANGE),
                                                   hwProfile: 0U,
                                                   scope: NativeConstants.DICS_FLAG_CONFIGSPECIFIC,
                                                   stateChange: NativeConstants.DICS_PROPCHANGE);

                if (UnsafeNativeMethods.SetupDiSetClassInstallParamsW(devinfo, devInfoData, pcp, PinnedBuffer<SP_PROPCHANGE_PARAMS>.TypeSize) &&
                    UnsafeNativeMethods.SetupDiCallClassInstaller(NativeConstants.DIF_PROPERTYCHANGE, devinfo, devInfoData))
                {
                    return;
                }

                if (Marshal.GetLastWin32Error() == NativeConstants.ERROR_NO_SUCH_DEVINST)
                {
                    throw new DriveNotFoundException("Device not found");
                }

                throw new IOException("Device restart failed", new Win32Exception());
            }

            deviceIndex += 1U;
        }

        throw new DriveNotFoundException("Device not found");
    }

    public static void RunDLLInstallHinfSection(IntPtr OwnerWindow, string InfPath, ReadOnlySpan<char> InfSection)
    {
#if NETCOREAPP
        var cmdLine = $"{InfSection} 132 {InfPath}";
#else
        var cmdLine = $"{InfSection.ToString()} 132 {InfPath}";
#endif
        Trace.WriteLine($"RunDLLInstallFromInfSection: {cmdLine}");

        if (InfPath.NullCheck(nameof(InfPath)).Contains(' ')
            || InfSection.Contains(' '))
        {
            throw new ArgumentException("Arguments to this method cannot contain spaces.", nameof(InfSection));
        }

        InfPath = Path.GetFullPath(InfPath);
        if (!File.Exists(InfPath))
        {
            throw new FileNotFoundException("File not found", InfPath);
        }

        UnsafeNativeMethods.InstallHinfSectionW(OwnerWindow,
                                                default,
                                                cmdLine.AsRef(),
                                                0);

    }

    public static void InstallFromInfSection(IntPtr OwnerWindow, string InfPath, string InfSection)
    {

        Trace.WriteLine($"InstallFromInfSection: InfPath=\"{InfPath}\", InfSection=\"{InfSection}\"");

        // '
        // ' Inf must be a full pathname
        // '
        InfPath = Path.GetFullPath(InfPath);
        if (!File.Exists(InfPath))
        {
            throw new FileNotFoundException("File not found", InfPath);
        }

        using var hInf = UnsafeNativeMethods.SetupOpenInfFileW(InfPath.AsRef(),
                                                               default,
                                                               0x2U,
                                                               out var ErrorLine);

        if (hInf.IsInvalid)
        {
            throw new Win32Exception($"Line number: {ErrorLine}");
        }

        Win32Try(UnsafeNativeMethods.SetupInstallFromInfSectionW(OwnerWindow,
                                                                 hInf,
                                                                 InfSection.AsRef(),
                                                                 0x1FFU,
                                                                 IntPtr.Zero,
                                                                 default,
                                                                 0x4U,
                                                                 (_, _, _, _) => 1,
                                                                 default,
                                                                 default,
                                                                 default));
    }

    public const uint DIF_REGISTERDEVICE = 0x19U;
    public const uint DIF_REMOVE = 0x5U;

    public static void CreateRootPnPDevice(IntPtr OwnerWindow, string infPath, string hwid, bool ForceReplaceExistingDrivers, out bool RebootRequired)
    {

        Trace.WriteLine($"CreateOrUpdateRootPnPDevice: InfPath=\"{infPath}\", hwid=\"{hwid}\"");

        // '
        // ' Inf must be a full pathname
        // '
        infPath = Path.GetFullPath(infPath);
        if (!File.Exists(infPath))
        {
            throw new FileNotFoundException($"File {infPath} not found", infPath);
        }

        // '
        // ' List of hardware ID's must be double zero-terminated
        // '
        var hwIdList = MemoryMarshal.AsBytes($"{hwid}\0\0".AsSpan());

        // '
        // ' Use the INF File to extract the Class GUID.
        // '
        Span<char> ClassName = stackalloc char[32];
        Win32Try(UnsafeNativeMethods.SetupDiGetINFClassW(infPath,
                                                         out var ClassGUID,
                                                         out ClassName[0],
                                                         32U,
                                                         out _));

#if NET6_0_OR_GREATER
        Trace.WriteLine($"CreateOrUpdateRootPnPDevice: ClassGUID=\"{ClassGUID}\", ClassName=\"{ClassName}\"");
#else
        Trace.WriteLine($"CreateOrUpdateRootPnPDevice: ClassGUID=\"{ClassGUID}\", ClassName=\"{ClassName.ToString()}\"");
#endif

        // '
        // ' Create the container for the to-be-created Device Information Element.
        // '
        var DeviceInfoSet = UnsafeNativeMethods.SetupDiCreateDeviceInfoList(ClassGUID, OwnerWindow);

        if (DeviceInfoSet.IsInvalid)
        {
            throw new Win32Exception();
        }

        using (DeviceInfoSet)
        {
            var DeviceInfoData = new SP_DEVINFO_DATA();

            // '
            // ' Now create the element.
            // ' Use the Class GUID and Name from the INF file.
            // '
            Win32Try(UnsafeNativeMethods.SetupDiCreateDeviceInfoW(DeviceInfoSet,
                                                                  ClassName[0],
                                                                  ClassGUID,
                                                                  default,
                                                                  OwnerWindow,
                                                                  0x1U,
                                                                  ref DeviceInfoData));

            // '
            // ' Add the HardwareID to the Device's HardwareID property.
            // '
            Win32Try(UnsafeNativeMethods.SetupDiSetDeviceRegistryPropertyW(DeviceInfoSet,
                                                                           ref DeviceInfoData,
                                                                           0x1U,
                                                                           hwIdList[0],
                                                                           (uint)hwIdList.Length));

            // '
            // ' Transform the registry element into an actual devnode
            // ' in the PnP HW tree.
            // '
            Win32Try(UnsafeNativeMethods.SetupDiCallClassInstaller(DIF_REGISTERDEVICE,
                                                                    DeviceInfoSet,
                                                                    DeviceInfoData));
        }

        // '
        // ' update the driver for the device we just created
        // '
        UpdateDriverForPnPDevices(OwnerWindow, infPath, hwid, ForceReplaceExistingDrivers, out RebootRequired);
    }

    public static IEnumerable<uint> EnumerateChildDevices(uint devInst)
    {

        var rc = UnsafeNativeMethods.CM_Get_Child(out var child, devInst, 0U);

        while (rc == 0L)
        {
#if DEBUG
            Trace.WriteLine($"Found child devinst: {child}");
#endif

            yield return child;

            rc = UnsafeNativeMethods.CM_Get_Sibling(out child, child, 0U);
        }
    }

    public static string? GetPhysicalDeviceObjectNtPath(string devInstName)
    {
        var devinst = GetDevInst(devInstName);

        return devinst.HasValue ? GetPhysicalDeviceObjectNtPath(devinst.Value) : null;
    }

    public static string? GetPhysicalDeviceObjectNtPath(uint devInst)
    {
        var buffersize = 518;
        Span<byte> buffer = stackalloc byte[buffersize];

        var rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_PropertyW(devInst,
                                                                       CmDevNodeRegistryProperty.CM_DRP_PHYSICAL_DEVICE_OBJECT_NAME,
                                                                       out var regtype,
                                                                       out buffer[0],
                                                                       buffersize,
                                                                       0);

        if (rc != 0L)
        {
            Trace.WriteLine($"Error getting registry property for device {devInst}. Status=0x{rc:X}");
            return null;
        }

        var name = MemoryMarshal.Cast<byte, char>(buffer.Slice(0, buffersize - 2)).ToString();

#if DEBUG
        Trace.WriteLine($"Found physical device object name: '{name}'");
#endif

        return name;
    }

    public static IEnumerable<string>? GetDeviceRegistryProperty(uint devInst, CmDevNodeRegistryProperty prop)
    {
        var buffersize = 518;
        Span<byte> buffer = stackalloc byte[buffersize];

        var rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_PropertyW(devInst,
                                                                       prop,
                                                                       out var regtype,
                                                                       out buffer[0],
                                                                       buffersize,
                                                                       0);

        if (rc != 0L)
        {
            Trace.WriteLine($"Error getting registry property for device {devInst}. Status=0x{rc:X}");
            return null;
        }

        var name = buffer
            .Slice(0, buffersize)
            .ToArray()
            .AsMemory()
            .ParseDoubleTerminatedString();

        return name;
    }

    public static IEnumerable<string> EnumerateWin32DevicePaths(string nt_device_path)
    {
        var query = from dosdevice in QueryDosDevice()
                    where QueryDosDevice(dosdevice).Any(target => target.Equals(nt_device_path, StringComparison.OrdinalIgnoreCase))
                    select dosdevice;

        return from dosdevice in query
               select $@"\\?\{dosdevice}";
    }

    public static IEnumerable<string> EnumerateRegisteredFilters(uint devInst)
    {
        var buffersize = 65536;

        var buffer = ArrayPool<byte>.Shared.Rent(buffersize);

        try
        {
            var rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_PropertyW(devInst,
                                                                           CmDevNodeRegistryProperty.CM_DRP_UPPERFILTERS,
                                                                           out var regtype,
                                                                           out buffer[0],
                                                                           buffersize,
                                                                           0);

            if (rc == NativeConstants.CR_NO_SUCH_VALUE)
            {
                yield break;
            }
            else if (rc != 0L)
            {
                var msg = $"Error getting registry property for device. Status=0x{rc:X}";
                throw new IOException(msg);
            }

            foreach (var filter in buffer.AsMemory(0, buffersize).ParseDoubleTerminatedString())
            {
                yield return filter;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ' Switched to querying registry directly instead. CM_Get_Class_Registry_PropertyW seems to
    // ' return 0x13 CR_FAILURE on Win7.
#if USE_CM_API

            Public Shared Function GetRegisteredFilters(devClass As Guid) As String()

                Dim regtype As RegistryValueKind = Nothing

                Dim buffer(0 To 65535) As Byte
                Dim buffersize = buffer.Length

                Dim rc = Win32API.CM_Get_Class_Registry_PropertyW(devClass, Win32API.CmClassRegistryProperty.CM_CRP_UPPERFILTERS, regtype, buffer, buffersize, 0)

                If rc <> 0 Then
                    Dim msg = $"Error getting registry property for device class {devClass}. Status=0x{rc:X}"
                    Trace.WriteLine(msg)
                    Throw New IOException(msg)
                End If

                Return ParseDoubleTerminatedString(Buffer)

            End Function

#else

    public static string[]? GetRegisteredFilters(Guid devClass)
    {

        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\Class\{devClass:B}");

        return key?.GetValue("UpperFilters") as string[];

    }

#endif

    public static void SetRegisteredFilters(uint devInst, IEnumerable<string> filters)
    {
        var str = $"{filters.Join('\0')}\0\0";
        var buffer = MemoryMarshal.AsBytes(str.AsSpan());
        var buffersize = buffer.Length;

        var rc = UnsafeNativeMethods.CM_Set_DevNode_Registry_PropertyW(devInst,
                                                                       CmDevNodeRegistryProperty.CM_DRP_UPPERFILTERS,
                                                                       buffer[0], buffersize, 0);

        if (rc != 0L)
        {
            throw new Exception($"Error setting registry property for device. Status=0x{rc:X}");
        }
    }

    public static void SetRegisteredFilters(Guid devClass, IEnumerable<string> filters)
    {
        var str = $"{filters.Join('\0')}\0\0";
        var buffer = MemoryMarshal.AsBytes(str.AsSpan());
        var buffersize = buffer.Length;

        var rc = UnsafeNativeMethods.CM_Set_Class_Registry_PropertyW(devClass,
                                                                     CmClassRegistryProperty.CM_CRP_UPPERFILTERS,
                                                                     buffer[0],
                                                                     buffersize,
                                                                     0);

        if (rc != 0L)
        {
            throw new Exception($"Error setting registry property for class {devClass}. Status=0x{rc:X}");
        }
    }

    public static bool AddFilter(uint devInst, string driver)
    {
        var filters = EnumerateRegisteredFilters(devInst).ToList();

        if (filters.Any(f => f.Equals(driver, StringComparison.OrdinalIgnoreCase)))
        {

            Trace.WriteLine($"Filter '{driver}' already registered for devinst {devInst}");

            return false;

        }

        Trace.WriteLine($"Registering filter '{driver}' for devinst {devInst}");

        filters.Add(driver);

        SetRegisteredFilters(devInst, filters);

        return true;

    }

    public static bool AddFilter(Guid devClass, string driver, bool addfirst)
    {

        driver.NullCheck(nameof(driver));

        var filters = GetRegisteredFilters(devClass);

        if (filters is null)
        {

            filters = Array.Empty<string>();
        }

        else if (addfirst && driver.Equals(filters.FirstOrDefault(), StringComparison.OrdinalIgnoreCase))
        {

            Trace.WriteLine($"Filter '{driver}' already registered first for class {devClass}");
            return false;
        }

        else if (!addfirst && driver.Equals(filters.LastOrDefault(), StringComparison.OrdinalIgnoreCase))
        {

            Trace.WriteLine($"Filter '{driver}' already registered last for class {devClass}");
            return false;

        }

        var filter_list = new List<string>(filters);

        filter_list.RemoveAll(f => f.Equals(driver, StringComparison.OrdinalIgnoreCase));

        if (addfirst)
        {

            filter_list.Insert(0, driver);
        }

        else
        {

            filter_list.Add(driver);

        }

        Trace.WriteLine($"Registering filters '{string.Join(",", filter_list)}' for class {devClass}");

        SetRegisteredFilters(devClass, filter_list);

        return true;

    }

    public static bool RemoveFilter(uint devInst, string driver)
    {

        var filters = EnumerateRegisteredFilters(devInst).ToArray();

        if (filters is null || filters.Length == 0)
        {
            Trace.WriteLine($"No filters registered for devinst {devInst}");
            return false;
        }

        var newfilters = filters.Where(f => !f.Equals(driver, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (newfilters.Length == filters.Length)
        {
            Trace.WriteLine($"Filter '{driver}' not registered for devinst {devInst}");
            return false;
        }

        Trace.WriteLine($"Removing filter '{driver}' from devinst {devInst}");

        SetRegisteredFilters(devInst, newfilters);

        return true;

    }

    public static bool RemoveFilter(Guid devClass, string driver)
    {

        var filters = GetRegisteredFilters(devClass);

        if (filters is null)
        {
            Trace.WriteLine($"No filters registered for class {devClass}");
            return false;
        }

        var newfilters = filters.Where(f => !f.Equals(driver, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (newfilters.Length == filters.Length)
        {
            Trace.WriteLine($"Filter '{driver}' not registered for class {devClass}");
            return false;
        }

        Trace.WriteLine($"Removing filter '{driver}' from class {devClass}");

        SetRegisteredFilters(devClass, newfilters);

        return true;

    }

    public static int RemovePnPDevice(IntPtr OwnerWindow, string hwid)
    {
        Trace.WriteLine($"RemovePnPDevice: hwid='{hwid}'");

        // '
        // ' Create the container for the to-be-created Device Information Element.
        // '
        using var DeviceInfoSet = UnsafeNativeMethods.SetupDiCreateDeviceInfoList(IntPtr.Zero, OwnerWindow);

        if (DeviceInfoSet.IsInvalid)
        {
            throw new Win32Exception("SetupDiCreateDeviceInfoList");
        }

        if (!UnsafeNativeMethods.SetupDiOpenDeviceInfoW(DeviceInfoSet,
                                                        hwid.AsRef(),
                                                        OwnerWindow,
                                                        0,
                                                        IntPtr.Zero))
        {
            return 0;
        }

        var DeviceInfoData = new SP_DEVINFO_DATA();

        var i = 0u;
        var done = 0;

        for (; ; )
        {
            if (!UnsafeNativeMethods.SetupDiEnumDeviceInfo(DeviceInfoSet, i, ref DeviceInfoData))
            {
                return i == 0L
                    ? throw new Win32Exception("SetupDiEnumDeviceInfo")
                    : done;
            }

            if (UnsafeNativeMethods.SetupDiCallClassInstaller(DIF_REMOVE, DeviceInfoSet, DeviceInfoData))
            {
                done += 1;
            }

            i += 1U;
        }
    }

    public static void UpdateDriverForPnPDevices(IntPtr OwnerWindow, string InfPath, string hwid, bool forceReplaceExisting, out bool RebootRequired)
    {
        Trace.WriteLine($"UpdateDriverForPnPDevices: InfPath=\"{InfPath}\", hwid=\"{hwid}\", forceReplaceExisting={forceReplaceExisting}");

        // '
        // ' Inf must be a full pathname
        // '
        InfPath = Path.GetFullPath(InfPath);
        if (!File.Exists(InfPath))
        {
            throw new FileNotFoundException("File not found", InfPath);
        }

        // '
        // ' make use of UpdateDriverForPlugAndPlayDevices
        // '
        Win32Try(UnsafeNativeMethods.UpdateDriverForPlugAndPlayDevicesW(OwnerWindow,
                                                                        hwid.AsRef(),
                                                                        InfPath.AsRef(),
                                                                        forceReplaceExisting ? 0x1U : 0x0U,
                                                                        out RebootRequired));

    }

    public static string SetupCopyOEMInf(string InfPath, bool NoOverwrite)
    {

        // '
        // ' Inf must be a full pathname
        // '
        InfPath = Path.GetFullPath(InfPath);
        if (!File.Exists(InfPath))
        {
            throw new FileNotFoundException("File not found", InfPath);
        }

        Span<char> destName = stackalloc char[260];

        Win32Try(UnsafeNativeMethods.SetupCopyOEMInfW(InfPath.AsRef(),
                                                      default,
                                                      0,
                                                      NoOverwrite ? 0x8U : 0x0U,
                                                      out destName[0],
                                                      destName.Length,
                                                      out _,
                                                      default));

        return destName.ReadNullTerminatedUnicodeString();
    }

    public static void DriverPackagePreinstall(string InfPath)
    {

        // '
        // ' Inf must be a full pathname
        // '
        InfPath = Path.GetFullPath(InfPath);
        if (!File.Exists(InfPath))
        {
            throw new FileNotFoundException("File not found", InfPath);
        }

        var errcode = UnsafeNativeMethods.DriverPackagePreinstallW(InfPath.AsRef(), 1);

        if (errcode != 0)
        {
            throw new Win32Exception(errcode);
        }
    }

    public static void DriverPackageInstall(string InfPath, out bool NeedReboot)
    {

        // '
        // ' Inf must be a full pathname
        // '
        InfPath = Path.GetFullPath(InfPath);
        if (!File.Exists(InfPath))
        {
            throw new FileNotFoundException("File not found", InfPath);
        }

        var errcode = UnsafeNativeMethods.DriverPackageInstallW(InfPath.AsRef(), 1, default, out NeedReboot);

        if (errcode != 0)
        {
            throw new Win32Exception(errcode);
        }
    }

    public static void DriverPackageUninstall(string InfPath, DriverPackageUninstallFlags Flags, out bool NeedReboot)
    {

        // '
        // ' Inf must be a full pathname
        // '
        InfPath = Path.GetFullPath(InfPath);
        if (!File.Exists(InfPath))
        {
            throw new FileNotFoundException("File not found", InfPath);
        }

        var errcode = UnsafeNativeMethods.DriverPackageUninstallW(InfPath.AsRef(), Flags, default, out NeedReboot);

        if (errcode != 0)
        {
            throw new Win32Exception(errcode);
        }
    }

    public static bool MapFileAndCheckSum(string file, out int headerSum, out int checkSum)
        => UnsafeNativeMethods.MapFileAndCheckSumW(file.AsRef(), out headerSum, out checkSum) == 0;

    /// <summary>
    /// Re-enumerates partitions on all disk drives currently connected to the system. No exceptions are
    /// thrown on error, but any exceptions from underlying API calls are logged to trace log.
    /// </summary>
    public static void UpdateDiskProperties()
    {
        foreach (var diskdevice in from device in QueryDosDevice()
                                   where device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) || device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)
                                   select device)
        {
            try
            {
                using var device = OpenFileHandle($@"\\?\{diskdevice}", 0, FileShare.ReadWrite, FileMode.Open, Overlapped: false);

                if (!UpdateDiskProperties(device, throwOnFailure: false))
                {
                    Trace.WriteLine($"Error updating disk properties for {diskdevice}: {new Win32Exception().Message}");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error updating disk properties for {diskdevice}: {ex.JoinMessages()}");
            }
        }
    }

    /// <summary>
    /// Re-enumerates partitions on a disk device with a specified SCSI address. No
    /// exceptions are thrown on error, but any exceptions from underlying API calls are
    /// logged to trace log.
    /// </summary>
    /// <returns>Returns a value indicating whether operation was successful or not.</returns>
    public static bool UpdateDiskProperties(SCSI_ADDRESS ScsiAddress)
    {

        try
        {
            using var devicehandle = OpenDiskByScsiAddress(ScsiAddress, default).Value;

            var rc = UpdateDiskProperties(devicehandle, throwOnFailure: false);

            if (!rc)
            {

                Trace.WriteLine($"Updating disk properties failed for {ScsiAddress}: {new Win32Exception().Message}");

            }

            return rc;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error updating disk properties for {ScsiAddress}: {ex.JoinMessages()}");
        }

        return false;

    }

    public static bool UpdateDiskProperties(SafeFileHandle devicehandle, bool throwOnFailure)
    {
        var rc = UnsafeNativeMethods.DeviceIoControl(devicehandle, NativeConstants.IOCTL_DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0U, IntPtr.Zero, 0U, out _, IntPtr.Zero);

        return !rc && throwOnFailure
            ? throw new Win32Exception()
            : rc;
    }

    /// <summary>
    /// Re-enumerates partitions on a disk device with a specified device path. No
    /// exceptions are thrown on error, but any exceptions from underlying API calls are
    /// logged to trace log.
    /// </summary>
    /// <returns>Returns a value indicating whether operation was successful or not.</returns>
    public static bool UpdateDiskProperties(string DevicePath)
    {
        try
        {
            using var devicehandle = OpenFileHandle(DevicePath, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open, (FileOptions)0);

            var rc = UnsafeNativeMethods.DeviceIoControl(devicehandle,
                                                         NativeConstants.IOCTL_DISK_UPDATE_PROPERTIES,
                                                         IntPtr.Zero,
                                                         0U,
                                                         IntPtr.Zero,
                                                         0U,
                                                         out var arglpBytesReturned,
                                                         IntPtr.Zero);

            if (!rc)
            {
                Trace.WriteLine($"Updating disk properties failed for {DevicePath}: {new Win32Exception().Message}");
            }

            return rc;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error updating disk properties for {DevicePath}: {ex.JoinMessages()}");
        }

        return false;
    }

    /// <summary>
    /// Opens a disk device with a specified SCSI address and returns both name and an open handle.
    /// </summary>
    public static KeyValuePair<string, SafeFileHandle> OpenDiskByScsiAddress(SCSI_ADDRESS ScsiAddress, FileAccess AccessMode)
    {

        var dosdevs = QueryDosDevice();

        var rawdevices = from device in dosdevs
                         where device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) || device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)
                         select device;

        var volumedevices = from device in dosdevs
                            where device.Length == 2 && device[1] == ':'
                            select device;

        KeyValuePair<string, SafeFileHandle> filter(string diskdevice)
        {

            diskdevice = $@"\\?\{diskdevice}";

            try
            {
                var devicehandle = OpenFileHandle(diskdevice, AccessMode, FileShare.ReadWrite, FileMode.Open, Overlapped: false);

                try
                {
                    var Address = GetScsiAddress(devicehandle);

                    if (!Address.HasValue || Address.Value != ScsiAddress)
                    {

                        devicehandle.Dispose();

                        return default;

                    }

                    Trace.WriteLine($"Found {diskdevice} with SCSI address {Address}");

                    return new(diskdevice, devicehandle);
                }

                catch (Exception ex)
                {
                    Trace.WriteLine($"Exception while querying SCSI address for {diskdevice}: {ex.JoinMessages()}");

                    devicehandle.Dispose();

                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception while opening {diskdevice}: {ex.JoinMessages()}");
            }

            return default;

        }

        var dev = (from anydevice in rawdevices.Concat(volumedevices)
                   select filter(anydevice)).FirstOrDefault(anydevice => anydevice.Key is not null);

        return dev.Key is not null
            ? dev
            : throw new DriveNotFoundException($"No physical drive found with SCSI address: {ScsiAddress}");
    }

    /// <summary>
    /// Returns a disk device object name for a specified SCSI address.
    /// </summary>
    [Obsolete("Use PnP features instead to find device names. This method is not guaranteed to return the correct intended device.")]
    public static string? GetDeviceNameByScsiAddressAndSize(SCSI_ADDRESS scsi_address, long disk_size)
    {

        var dosdevs = QueryDosDevice();

        var rawdevices = from device in dosdevs
                         where device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) || device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)
                         select device;

        var volumedevices = from device in dosdevs
                            where device.Length == 2 && device[1] == ':'
                            select device;

        bool filter(string diskdevicestr)
        {
            var diskdevice = $@"\\?\{diskdevicestr}";

            try
            {
                var devicehandle = OpenFileHandle(diskdevice, 0, FileShare.ReadWrite, FileMode.Open, Overlapped: false);

                try
                {
                    var got_address = GetScsiAddress(devicehandle);

                    if (!got_address.HasValue || got_address.Value != scsi_address)
                    {

                        return false;

                    }

                    Trace.WriteLine($"Found {diskdevice} with SCSI address {got_address}");

                    devicehandle.Close();

                    devicehandle = null;

                    devicehandle = OpenFileHandle(diskdevice, FileAccess.Read, FileShare.ReadWrite, FileMode.Open, Overlapped: false);

                    var got_size = GetDiskSize(devicehandle);

                    if (got_size == disk_size)
                    {
                        return true;
                    }

                    Trace.WriteLine($"Found {diskdevice} has wrong size. Expected: {disk_size}, got: {got_size}");

                    return false;
                }

                catch (Exception ex)
                {
                    Trace.WriteLine($"Exception while querying SCSI address for {diskdevice}: {ex.JoinMessages()}");
                }

                finally
                {
                    devicehandle?.Dispose();

                }
            }

            catch (Exception ex)
            {
                Trace.WriteLine($"Exception while opening {diskdevice}: {ex.JoinMessages()}");

            }

            return false;

        }

        return rawdevices.Concat(volumedevices).FirstOrDefault(filter);
    }

    public static bool TestFileOpen(string path)
    {
        using var handle = UnsafeNativeMethods.CreateFileW(path.AsRef(),
                                                           FileSystemRights.ReadAttributes,
                                                           0,
                                                           IntPtr.Zero,
                                                           NativeConstants.OPEN_EXISTING,
                                                           0,
                                                           IntPtr.Zero);

        return !handle.IsInvalid;
    }

    public static void CreateHardLink(string existing, string newlink)
        => Win32Try(UnsafeNativeMethods.CreateHardLinkW(newlink.AsRef(), existing.AsRef(), IntPtr.Zero));

    public static void MoveFile(string existing, string newname)
        => Win32Try(UnsafeNativeMethods.MoveFileW(existing.AsRef(), newname.AsRef()));

    public static OperatingSystem GetOSVersion()
    {
        var os_version = new OSVERSIONINFOEX();

        var status = UnsafeNativeMethods.RtlGetVersion(ref os_version);

        return status < 0
            ? throw new Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(status))
            : new OperatingSystem(os_version.PlatformId,
                                  new Version(os_version.MajorVersion,
                                              os_version.MinorVersion,
                                              os_version.BuildNumber,
                                              os_version.ServicePackMajor << 16 | os_version.ServicePackMinor));
    }

    /// <summary>
    /// Encapsulates a Service Control Management object handle that is closed by calling CloseServiceHandle() Win32 API.
    /// </summary>
    public sealed partial class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
#if NET7_0_OR_GREATER
        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseServiceHandle(IntPtr hSCObject);
#else
        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);
#endif
        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="open_handle">Existing open handle.</param>
        /// <param name="owns_handle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        public SafeServiceHandle(IntPtr open_handle, bool owns_handle)
            : base(owns_handle)
        {
            SetHandle(open_handle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        public SafeServiceHandle()
            : base(ownsHandle: true)
        {

        }

        /// <summary>
        /// Closes contained handle by calling CloseServiceHandle() Win32 API.
        /// </summary>
        /// <returns>Return value from CloseServiceHandle() Win32 API.</returns>
        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }

    /// <summary>
    /// Encapsulates a FindVolume handle that is closed by calling FindVolumeClose() Win32 API.
    /// </summary>
    public sealed partial class SafeFindVolumeHandle : SafeHandleMinusOneIsInvalid
    {
#if NET7_0_OR_GREATER
        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool FindVolumeClose(IntPtr h);
#else
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool FindVolumeClose(IntPtr h);
#endif

        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="open_handle">Existing open handle.</param>
        /// <param name="owns_handle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        public SafeFindVolumeHandle(IntPtr open_handle, bool owns_handle)
            : base(owns_handle)
        {

            SetHandle(open_handle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        public SafeFindVolumeHandle()
            : base(ownsHandle: true)
        {

        }

        /// <summary>
        /// Closes contained handle by calling CloseServiceHandle() Win32 API.
        /// </summary>
        /// <returns>Return value from CloseServiceHandle() Win32 API.</returns>
        protected override bool ReleaseHandle() => FindVolumeClose(handle);
    }

    /// <summary>
    /// Encapsulates a FindVolumeMountPoint handle that is closed by calling FindVolumeMountPointClose () Win32 API.
    /// </summary>
    public sealed partial class SafeFindVolumeMountPointHandle : SafeHandleMinusOneIsInvalid
    {
#if NET7_0_OR_GREATER
        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool FindVolumeMountPointClose(IntPtr h);
#else
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool FindVolumeMountPointClose(IntPtr h);
#endif

        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="open_handle">Existing open handle.</param>
        /// <param name="owns_handle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        public SafeFindVolumeMountPointHandle(IntPtr open_handle, bool owns_handle)
            : base(owns_handle)
        {

            SetHandle(open_handle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        public SafeFindVolumeMountPointHandle()
            : base(ownsHandle: true)
        {

        }

        /// <summary>
        /// Closes contained handle by calling CloseServiceHandle() Win32 API.
        /// </summary>
        /// <returns>Return value from CloseServiceHandle() Win32 API.</returns>
        protected override bool ReleaseHandle() => FindVolumeMountPointClose(handle);
    }

    /// <summary>
    /// Encapsulates a SetupAPI hInf handle that is closed by calling SetupCloseInf() API.
    /// </summary>
    public sealed partial class SafeInfHandle : SafeHandleMinusOneIsInvalid
    {
#if NET7_0_OR_GREATER
        [LibraryImport("setupapi", SetLastError = true)]
        private static partial void SetupCloseInfFile(IntPtr hInf);
#else
        [DllImport("setupapi", SetLastError = true)]
        private static extern void SetupCloseInfFile(IntPtr hInf);
#endif

        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="open_handle">Existing open handle.</param>
        /// <param name="owns_handle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        public SafeInfHandle(IntPtr open_handle, bool owns_handle)
            : base(owns_handle)
        {

            SetHandle(open_handle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        public SafeInfHandle()
            : base(ownsHandle: true)
        {

        }

        /// <summary>
        /// Closes contained handle by calling CloseServiceHandle() Win32 API.
        /// </summary>
        /// <returns>Return value from CloseServiceHandle() Win32 API.</returns>
        protected override bool ReleaseHandle()
        {
            SetupCloseInfFile(handle);
            return true;
        }
    }

    /// <summary>
    /// Encapsulates a SetupAPI hInf handle that is closed by calling SetupCloseInf() API.
    /// </summary>
    public sealed partial class SafeDeviceInfoSetHandle : SafeHandleMinusOneIsInvalid
    {
#if NET7_0_OR_GREATER
        [LibraryImport("setupapi", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetupDiDestroyDeviceInfoList(IntPtr handle);
#else
        [DllImport("setupapi", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr handle);
#endif

        /// <summary>
        /// Initiates a new instance with an existing open handle.
        /// </summary>
        /// <param name="open_handle">Existing open handle.</param>
        /// <param name="owns_handle">Indicates whether handle should be closed when this
        /// instance is released.</param>
        public SafeDeviceInfoSetHandle(IntPtr open_handle, bool owns_handle)
            : base(owns_handle)
        {

            SetHandle(open_handle);
        }

        /// <summary>
        /// Creates a new empty instance. This constructor is used by native to managed
        /// handle marshaller.
        /// </summary>
        public SafeDeviceInfoSetHandle()
            : base(ownsHandle: true)
        {

        }

        /// <summary>
        /// Closes contained handle by calling CloseServiceHandle() Win32 API.
        /// </summary>
        /// <returns>Return value from CloseServiceHandle() Win32 API.</returns>
        protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ByHandleFileInformation
    {
        public FileAttributes FileAttributes { get; }
        private readonly long ftCreationTime;
        private readonly long ftLastAccessTime;
        private readonly long ftLastWriteTime;
        public uint VolumeSerialNumber { get; }
        private readonly int nFileSizeHigh;
        private readonly uint nFileSizeLow;
        public int NumberOfLinks { get; }
        private readonly uint nFileIndexHigh;
        private readonly uint nFileIndexLow;

        public DateTime CreationTime => DateTime.FromFileTime(ftCreationTime);

        public DateTime LastAccessTime => DateTime.FromFileTime(ftLastAccessTime);

        public DateTime LastWriteTime => DateTime.FromFileTime(ftLastWriteTime);

        public long FileSize => (long)nFileSizeHigh << 32 | nFileSizeLow;

        public ulong FileIndexAndSequence => (ulong)nFileIndexHigh << 32 | nFileIndexLow;

        public long FileIndex => (nFileIndexHigh & 0xFFFFL) << 32 | nFileIndexLow;

        public ushort Sequence => (ushort)(nFileIndexHigh >> 16);

        public static ByHandleFileInformation FromHandle(SafeFileHandle handle)
        {
            Win32Try(UnsafeNativeMethods.GetFileInformationByHandle(handle, out var obj));

            return obj;
        }
    }

    [Flags]
    public enum DriverPackageUninstallFlags : uint
    {
        Normal = 0x0U,
        DeleteFiles = NativeConstants.DRIVER_PACKAGE_DELETE_FILES,
        Force = NativeConstants.DRIVER_PACKAGE_FORCE,
        Silent = NativeConstants.DRIVER_PACKAGE_SILENT
    }

    public class Win32LocalBuffer : SafeBuffer
    {
        public Win32LocalBuffer(nint numBytes)
            : base(ownsHandle: true)
        {
            var ptr = UnsafeNativeMethods.LocalAlloc(0x0040, numBytes);
            if (ptr == default)
            {
                throw new Win32Exception();
            }

            SetHandle(ptr);
            Initialize((ulong)numBytes);
        }

        public Win32LocalBuffer(int numBytes)
            : this(new IntPtr(numBytes))
        {
        }

        public Win32LocalBuffer(nint address, ulong numBytes, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(address);
            Initialize(numBytes);
        }

        protected Win32LocalBuffer()
            : base(ownsHandle: true)
        {
        }

        public void Resize(int newSize) => Resize(new IntPtr(newSize));

        public void Resize(nint newSize)
        {
            nint newptr;
            if (handle != default)
            {
                newptr = UnsafeNativeMethods.LocalReAlloc(handle, 0x0040, newSize);
            }
            else
            {
                newptr = UnsafeNativeMethods.LocalAlloc(0x0040, newSize);
            }

            if (newptr == default)
            {
                throw new Win32Exception();
            }

            handle = newptr;
            Initialize((ulong)newSize);
        }

        protected override bool ReleaseHandle()
        {
            return UnsafeNativeMethods.LocalFree(handle) == default;
        }
    }
}