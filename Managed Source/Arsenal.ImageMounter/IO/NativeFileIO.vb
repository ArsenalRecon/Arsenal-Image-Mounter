Imports System.Collections.Concurrent
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Globalization
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Security
Imports System.Security.AccessControl
Imports System.Security.Permissions
Imports System.Text
Imports System.Threading
Imports Arsenal.ImageMounter.Extensions
Imports Microsoft.Win32
Imports Microsoft.Win32.SafeHandles

''''' NativeFileIO.vb
''''' Routines for accessing some useful Win32 API functions to access features not
''''' directly accessible through .NET Framework.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
'''''

#Disable Warning IDE0079 ' Remove unnecessary suppression
#Disable Warning CA1308 ' Normalize strings to uppercase
#Disable Warning CA1060 ' Move pinvokes to native methods class
#Disable Warning SYSLIB0003 ' Type or member is obsolete


Namespace IO

    ''' <summary>
    ''' Provides wrappers for Win32 file API. This makes it possible to open everything that
    ''' CreateFile() can open and get a FileStream based .NET wrapper around the file handle.
    ''' </summary>
    Public NotInheritable Class NativeFileIO

#Region "Win32 API"

        Public NotInheritable Class NativeConstants

            Public Const STANDARD_RIGHTS_REQUIRED As UInt32 = &HF0000UI

            Public Const FILE_ATTRIBUTE_NORMAL As UInt32 = &H80UI
            Public Const FILE_FLAG_OVERLAPPED As UInt32 = &H40000000UI
            Public Const FILE_FLAG_BACKUP_SEMANTICS As UInt32 = &H2000000UI
            Public Const FILE_FLAG_OPEN_REPARSE_POINT As UInt32 = &H200000UI
            Public Const OPEN_ALWAYS As UInt32 = 4UI
            Public Const OPEN_EXISTING As UInt32 = 3UI
            Public Const CREATE_ALWAYS As UInt32 = 2UI
            Public Const CREATE_NEW As UInt32 = 1UI
            Public Const TRUNCATE_EXISTING As UInt32 = 5UI
            Public Const EVENT_QUERY_STATE As UInt32 = 1UI
            Public Const EVENT_MODIFY_STATE As UInt32 = 2UI
            Public Const EVENT_ALL_ACCESS As UInt32 = STANDARD_RIGHTS_REQUIRED Or FileSystemRights.Synchronize Or FileSystemRights.ReadData Or FileSystemRights.WriteData

            Public Const NO_ERROR As UInt32 = 0UI
            Public Const ERROR_INVALID_FUNCTION As UInt32 = 1UI
            Public Const ERROR_IO_DEVICE As UInt32 = &H45DUI
            Public Const ERROR_FILE_NOT_FOUND As UInt32 = 2UI
            Public Const ERROR_PATH_NOT_FOUND As UInt32 = 3UI
            Public Const ERROR_ACCESS_DENIED As UInt32 = 5UI
            Public Const ERROR_NO_MORE_FILES As UInt32 = 18UI
            Public Const ERROR_HANDLE_EOF As UInt32 = 38UI
            Public Const ERROR_NOT_SUPPORTED As UInt32 = 50UI
            Public Const ERROR_DEV_NOT_EXIST As UInt32 = 55UI
            Public Const ERROR_INVALID_PARAMETER As UInt32 = 87UI
            Public Const ERROR_MORE_DATA As UInt32 = &H234UI
            Public Const ERROR_NOT_ALL_ASSIGNED As UInt32 = 1300UI
            Public Const ERROR_INSUFFICIENT_BUFFER As UInt32 = 122UI
            Public Const ERROR_IN_WOW64 As Int32 = &HE0000235I

            Public Const FSCTL_GET_COMPRESSION As UInt32 = &H9003C
            Public Const FSCTL_SET_COMPRESSION As UInt32 = &H9C040
            Public Const COMPRESSION_FORMAT_NONE As UInt16 = 0US
            Public Const COMPRESSION_FORMAT_DEFAULT As UInt16 = 1US
            Public Const FSCTL_SET_SPARSE As UInt32 = &H900C4

            Public Const FSCTL_ALLOW_EXTENDED_DASD_IO As UInt32 = &H90083

            Public Const FSCTL_LOCK_VOLUME As UInt32 = &H90018
            Public Const FSCTL_DISMOUNT_VOLUME As UInt32 = &H90020

            Public Const FSCTL_SET_REPARSE_POINT As UInt32 = &H900A4
            Public Const FSCTL_GET_REPARSE_POINT As UInt32 = &H900A8
            Public Const FSCTL_DELETE_REPARSE_POINT As UInt32 = &H900AC
            Public Const IO_REPARSE_TAG_MOUNT_POINT As UInt32 = &HA0000003UI

            Public Const IOCTL_SCSI_MINIPORT As UInt32 = &H4D008
            Public Const IOCTL_SCSI_GET_ADDRESS As UInt32 = &H41018
            Public Const IOCTL_STORAGE_QUERY_PROPERTY As UInt32 = &H2D1400
            Public Const IOCTL_STORAGE_GET_DEVICE_NUMBER As UInt32 = &H2D1080
            Public Const IOCTL_DISK_GET_DRIVE_GEOMETRY As UInt32 = &H70000
            Public Const IOCTL_DISK_GET_LENGTH_INFO As UInt32 = &H7405C
            Public Const IOCTL_DISK_GET_PARTITION_INFO As UInt32 = &H74004
            Public Const IOCTL_DISK_GET_PARTITION_INFO_EX As UInt32 = &H70048
            Public Const IOCTL_DISK_GET_DRIVE_LAYOUT As UInt32 = &H7400C
            Public Const IOCTL_DISK_GET_DRIVE_LAYOUT_EX As UInt32 = &H70050
            Public Const IOCTL_DISK_SET_DRIVE_LAYOUT_EX As UInt32 = &H7C054
            Public Const IOCTL_DISK_CREATE_DISK As UInt32 = &H7C058
            Public Const IOCTL_DISK_GROW_PARTITION As UInt32 = &H7C0D0
            Public Const IOCTL_DISK_UPDATE_PROPERTIES As UInt32 = &H70140
            Public Const IOCTL_DISK_IS_WRITABLE As UInt32 = &H70024
            Public Const IOCTL_SCSI_RESCAN_BUS As UInt32 = &H4101C

            Public Const IOCTL_DISK_GET_DISK_ATTRIBUTES As UInt32 = &H700F0
            Public Const IOCTL_DISK_SET_DISK_ATTRIBUTES As UInt32 = &H7C0F4
            Public Const IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS As UInt32 = &H560000
            Public Const IOCTL_VOLUME_OFFLINE As UInt32 = &H56C00C
            Public Const IOCTL_VOLUME_ONLINE As UInt32 = &H56C008

            Public Const FILE_DEVICE_DISK As UInt32 = &H7

            Public Const ERROR_WRITE_PROTECT As Int32 = 19I
            Public Const ERROR_NOT_READY As Int32 = 21I
            Public Const FVE_E_LOCKED_VOLUME As Int32 = &H80310000I

            Public Const SC_MANAGER_CREATE_SERVICE As UInt32 = &H2
            Public Const SC_MANAGER_ALL_ACCESS As UInt32 = &HF003F
            Public Const SERVICE_KERNEL_DRIVER As UInt32 = &H1
            Public Const SERVICE_FILE_SYSTEM_DRIVER As UInt32 = &H2
            Public Const SERVICE_WIN32_OWN_PROCESS As UInt32 = &H10 'Service that runs in its own process. 
            Public Const SERVICE_WIN32_INTERACTIVE As UInt32 = &H100 'Service that runs in its own process. 
            Public Const SERVICE_WIN32_SHARE_PROCESS As UInt32 = &H20

            Public Const SERVICE_BOOT_START As UInt32 = &H0
            Public Const SERVICE_SYSTEM_START As UInt32 = &H1
            Public Const SERVICE_AUTO_START As UInt32 = &H2
            Public Const SERVICE_DEMAND_START As UInt32 = &H3
            Public Const SERVICE_ERROR_IGNORE As UInt32 = &H0
            Public Const SERVICE_CONTROL_STOP As UInt32 = &H1
            Public Const ERROR_SERVICE_DOES_NOT_EXIST As UInt32 = 1060
            Public Const ERROR_SERVICE_ALREADY_RUNNING As UInt32 = 1056

            Public Const CR_SUCCESS As UInt32 = &H0
            Public Const CR_FAILURE As UInt32 = &H13
            Public Const CR_NO_SUCH_VALUE As UInt32 = &H25
            Public Const CR_NO_SUCH_REGISTRY_KEY As UInt32 = &H2E

            Public Shared ReadOnly Property SerenumBusEnumeratorGuid As New Guid("{4D36E97B-E325-11CE-BFC1-08002BE10318}")
            Public Shared ReadOnly Property DiskDriveGuid As New Guid("{4D36E967-E325-11CE-BFC1-08002BE10318}")

            Public Shared ReadOnly Property DiskClassGuid As New Guid("{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}")
            Public Shared ReadOnly Property CdRomClassGuid As New Guid("{53F56308-B6BF-11D0-94F2-00A0C91EFB8B}")
            Public Shared ReadOnly Property StoragePortClassGuid As New Guid("{2ACCFE60-C130-11D2-B082-00A0C91EFB8B}")
            Public Shared ReadOnly Property ComPortClassGuid As New Guid("{86E0D1E0-8089-11D0-9CE4-08003E301F73}")

            Public Const SE_BACKUP_NAME = "SeBackupPrivilege"
            Public Const SE_RESTORE_NAME = "SeRestorePrivilege"
            Public Const SE_SECURITY_NAME = "SeSecurityPrivilege"
            Public Const SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege"
            Public Const SE_DEBUG_NAME = "SeDebugPrivilege"
            Public Const SE_TCB_NAME = "SeTcbPrivilege"
            Public Const SE_SHUTDOWN_NAME = "SeShutdownPrivilege"

            Public Const PROCESS_DUP_HANDLE As UInteger = &H40
            Public Const PROCESS_QUERY_LIMITED_INFORMATION As UInteger = &H1000

            Public Const TOKEN_QUERY As UInteger = &H8
            Public Const TOKEN_ADJUST_PRIVILEGES = &H20

            Public Const KEY_READ As Integer = &H20019
            Public Const REG_OPTION_BACKUP_RESTORE As Integer = &H4

            Public Const SE_PRIVILEGE_ENABLED As Integer = &H2

            Public Const STATUS_INFO_LENGTH_MISMATCH As Integer = &HC0000004
            Public Const STATUS_BUFFER_TOO_SMALL As Integer = &HC0000023
            Public Const STATUS_BUFFER_OVERFLOW As Integer = &H80000005
            Public Const STATUS_OBJECT_NAME_NOT_FOUND As Integer = &HC0000034
            Public Const STATUS_BAD_COMPRESSION_BUFFER As Integer = &HC0000242

            Public Const FILE_BEGIN As Integer = 1
            Public Const FILE_CURRENT As Integer = 1
            Public Const FILE_END As Integer = 2

            Private Sub New()
            End Sub

        End Class

        Public NotInheritable Class SafeNativeMethods

#Disable Warning CA1401 ' P/Invokes should not be visible
            Public Declare Auto Function AllocConsole Lib "kernel32.dll" (
              ) As Boolean

            Public Declare Auto Function FreeConsole Lib "kernel32.dll" (
              ) As Boolean

            Public Declare Auto Function GetConsoleWindow Lib "kernel32.dll" () As IntPtr

            Public Declare Auto Function GetLogicalDrives Lib "kernel32.dll" (
              ) As UInteger

            Public Declare Auto Function GetFileAttributes Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpFileName As String
              ) As FileAttributes

            Public Declare Auto Function SetFileAttributes Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpFileName As String,
              dwFileAttributes As FileAttributes
              ) As Boolean

            Public Declare Auto Function GetTickCount64 Lib "kernel32.dll" () As Long
#Enable Warning CA1401 ' P/Invokes should not be visible

        End Class

        Public NotInheritable Class UnsafeNativeMethods

            Friend Declare Auto Function DuplicateHandle Lib "kernel32.dll" (
                hSourceProcessHandle As IntPtr,
                hSourceHandle As IntPtr,
                hTargetProcessHandle As IntPtr,
                <Out> ByRef lpTargetHandle As SafeWaitHandle,
                dwDesiredAccess As UInteger,
                bInheritHandle As Boolean,
                dwOptions As UInteger) As Boolean

            Friend Declare Auto Function SetEvent Lib "kernel32.dll" (
                hEvent As SafeWaitHandle) As Boolean

            Friend Declare Auto Function SetHandleInformation Lib "kernel32.dll" (
                h As SafeHandle,
                mask As UInteger,
                flags As UInteger) As Boolean

            Friend Declare Auto Function GetHandleInformation Lib "kernel32.dll" (
                h As SafeHandle,
                <Out> ByRef flags As UInteger) As Boolean

            Friend Declare Auto Function NtCreateFile Lib "ntdll.dll" (
                <Out> ByRef hFile As SafeFileHandle,
                AccessMask As FileSystemRights,
                <[In]> ByRef ObjectAttributes As ObjectAttributes,
                <Out> ByRef IoStatusBlock As IoStatusBlock,
                <[In]> ByRef AllocationSize As Long,
                FileAttributes As FileAttributes,
                ShareAccess As FileShare,
                CreateDisposition As NtCreateDisposition,
                CreateOptions As NtCreateOptions,
                EaBuffer As IntPtr,
                EaLength As UInt32) As Int32

            Friend Declare Auto Function NtOpenEvent Lib "ntdll.dll" (
                <Out> ByRef hEvent As SafeWaitHandle,
                AccessMask As UInt32,
                <[In]> ByRef ObjectAttributes As ObjectAttributes) As Int32

            Friend Declare Auto Function GetFileInformationByHandle Lib "kernel32.dll" (
                hFile As SafeFileHandle,
                <Out> ByRef lpFileInformation As ByHandleFileInformation) As Boolean

            Friend Declare Auto Function GetFileTime Lib "kernel32.dll" (
                hFile As SafeFileHandle,
                <Out, [Optional]> ByRef lpCreationTime As Int64,
                <Out, [Optional]> ByRef lpLastAccessTime As Int64,
                <Out, [Optional]> ByRef lpLastWriteTime As Int64) As Boolean

            Friend Declare Auto Function GetFileTime Lib "kernel32.dll" (
                hFile As SafeFileHandle,
                <Out, [Optional]> ByRef lpCreationTime As Int64,
                lpLastAccessTime As IntPtr,
                <Out, [Optional]> ByRef lpLastWriteTime As Int64) As Boolean

            Friend Declare Auto Function GetFileTime Lib "kernel32.dll" (
                hFile As SafeFileHandle,
                lpCreationTime As IntPtr,
                lpLastAccessTime As IntPtr,
                <Out, [Optional]> ByRef lpLastWriteTime As Int64) As Boolean

            Friend Declare Auto Function GetFileTime Lib "kernel32.dll" (
                hFile As SafeFileHandle,
                <Out, [Optional]> ByRef lpCreationTime As Int64,
                lpLastAccessTime As IntPtr,
                lpLastWriteTime As IntPtr) As Boolean

            Friend Declare Auto Function FindFirstStream Lib "kernel32.dll" Alias "FindFirstStreamW" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpFileName As String,
              InfoLevel As UInt32,
              <[Out]> ByRef lpszVolumeMountPoint As FindStreamData,
              dwFlags As UInt32) As SafeFindHandle

            Friend Declare Auto Function FindNextStream Lib "kernel32.dll" Alias "FindNextStreamW" (
              hFindStream As SafeFindHandle,
              <[Out]> ByRef lpszVolumeMountPoint As FindStreamData) As Boolean

            Friend Declare Auto Function FindFirstVolumeMountPoint Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpszRootPathName As String,
              <MarshalAs(UnmanagedType.LPWStr), [Out]> lpszVolumeMountPoint As StringBuilder,
              cchBufferLength As Integer) As SafeFindVolumeMountPointHandle

            Friend Declare Auto Function FindNextVolumeMountPoint Lib "kernel32.dll" (
              hFindVolumeMountPoint As SafeFindVolumeMountPointHandle,
              <MarshalAs(UnmanagedType.LPWStr), [Out]> lpszVolumeMountPoint As StringBuilder,
              cchBufferLength As Integer) As Boolean

            Friend Declare Auto Function FindFirstVolume Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [Out]> lpszVolumeName As StringBuilder,
              cchBufferLength As Integer) As SafeFindVolumeHandle

            Friend Declare Auto Function FindNextVolume Lib "kernel32.dll" (
              hFindVolumeMountPoint As SafeFindVolumeHandle,
              <MarshalAs(UnmanagedType.LPWStr), [Out]> lpszVolumeName As StringBuilder,
              cchBufferLength As Integer) As Boolean

            Friend Declare Auto Function DeleteVolumeMountPoint Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpszVolumeMountPoint As String) As Boolean

            Friend Declare Auto Function SetVolumeMountPoint Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpszVolumeMountPoint As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpszVolumeName As String) As Boolean

            Friend Declare Function SetFilePointerEx Lib "kernel32" (
              hFile As SafeFileHandle,
              distance_to_move As Long,
              <Out> ByRef new_file_pointer As Long,
              move_method As UInt32) As Boolean

            Friend Declare Function SetFilePointerEx Lib "kernel32" (
              hFile As SafeFileHandle,
              distance_to_move As Long,
              ptr_new_file_pointer As IntPtr,
              move_method As UInt32) As Boolean

            Friend Declare Auto Function OpenSCManager Lib "advapi32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpMachineName As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpDatabaseName As String,
              dwDesiredAccess As Integer) As SafeServiceHandle

            Friend Declare Auto Function CreateService Lib "advapi32.dll" (
              hSCManager As SafeServiceHandle,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpServiceName As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpDisplayName As String,
              dwDesiredAccess As Integer,
              dwServiceType As Integer,
              dwStartType As Integer,
              dwErrorControl As Integer,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpBinaryPathName As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpLoadOrderGroup As String,
              lpdwTagId As IntPtr,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpDependencies As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lp As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpPassword As String) As SafeServiceHandle

            Friend Declare Auto Function OpenService Lib "advapi32.dll" (
              hSCManager As SafeServiceHandle,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpServiceName As String,
              dwDesiredAccess As Integer) As SafeServiceHandle

            Friend Declare Auto Function ControlService Lib "advapi32.dll" (
              hSCManager As SafeServiceHandle,
              dwControl As Integer,
              ByRef lpServiceStatus As SERVICE_STATUS) As Boolean

            Friend Declare Auto Function DeleteService Lib "advapi32.dll" (
              hSCObject As SafeServiceHandle) As Boolean

            Friend Declare Auto Function StartService Lib "advapi32.dll" (
              hService As SafeServiceHandle,
              dwNumServiceArgs As Integer,
              lpServiceArgVectors As IntPtr) As Boolean

            Friend Declare Auto Function GetModuleHandle Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> ModuleName As String) As IntPtr

            Friend Declare Auto Function LoadLibrary Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpFileName As String) As IntPtr

            Friend Declare Auto Function FreeLibrary Lib "kernel32.dll" (
              hModule As IntPtr) As Boolean

            Friend Declare Function GetFileType Lib "kernel32.dll" (handle As IntPtr) As Win32FileType

            Friend Declare Function GetFileType Lib "kernel32.dll" (handle As SafeFileHandle) As Win32FileType

            Friend Declare Function GetStdHandle Lib "kernel32.dll" (nStdHandle As StdHandle) As IntPtr

            Friend Declare Function GetConsoleScreenBufferInfo Lib "kernel32.dll" (hConsoleOutput As IntPtr, <Out> ByRef lpConsoleScreenBufferInfo As CONSOLE_SCREEN_BUFFER_INFO) As Boolean

            Friend Declare Function GetConsoleScreenBufferInfo Lib "kernel32.dll" (hConsoleOutput As SafeFileHandle, <Out> ByRef lpConsoleScreenBufferInfo As CONSOLE_SCREEN_BUFFER_INFO) As Boolean

            Friend Declare Auto Function DefineDosDevice Lib "kernel32.dll" (
              dwFlags As DEFINE_DOS_DEVICE_FLAGS,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpDeviceName As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpTargetPath As String) As Boolean

            Friend Declare Auto Function QueryDosDevice Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpDeviceName As String,
              <Out, MarshalAs(UnmanagedType.LPArray)> lpTargetPath As Char(),
              ucchMax As Int32) As Int32

            Friend Declare Auto Function GetVolumePathNamesForVolumeName Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpszVolumeName As String,
              <Out, MarshalAs(UnmanagedType.LPArray)> lpszVolumePathNames As Char(),
              cchBufferLength As Int32,
              <Out> ByRef lpcchReturnLength As Int32) As Boolean

            Friend Declare Auto Function GetVolumeNameForVolumeMountPoint Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpszVolumeName As String,
              <MarshalAs(UnmanagedType.LPWStr), [In], Out> DestinationInfFileName As StringBuilder,
              DestinationInfFileNameSize As Int32) As Boolean

            Friend Declare Auto Function GetCommTimeouts Lib "kernel32" (
              hFile As SafeFileHandle,
              <Out> ByRef lpCommTimeouts As COMMTIMEOUTS) As Boolean

            Friend Declare Auto Function SetCommTimeouts Lib "kernel32" (
              hFile As SafeFileHandle,
              <[In]> ByRef lpCommTimeouts As COMMTIMEOUTS) As Boolean

            Friend Declare Auto Function CreateFile Lib "kernel32" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpFileName As String,
              dwDesiredAccess As FileSystemRights,
              dwShareMode As FileShare,
              lpSecurityAttributes As IntPtr,
              dwCreationDisposition As UInt32,
              dwFlagsAndAttributes As Int32,
              hTemplateFile As IntPtr) As SafeFileHandle

            Friend Declare Function FlushFileBuffers Lib "kernel32" (
              handle As SafeFileHandle) As Boolean

            Friend Declare Function GetFileSize Lib "kernel32" Alias "GetFileSizeEx" (
              hFile As SafeFileHandle,
              <Out> ByRef liFileSize As Int64) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" Alias "DeviceIoControl" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <[In], MarshalAs(UnmanagedType.I1)> ByRef lpInBuffer As Boolean,
              nInBufferSize As UInt32,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=6), Out> lpOutBuffer As Byte(),
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <MarshalAs(UnmanagedType.LPArray), [In]> lpInBuffer As Byte(),
              nInBufferSize As UInt32,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <MarshalAs(UnmanagedType.LPArray), [In]> lpInBuffer As Byte(),
              nInBufferSize As UInt32,
              <MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=6), Out> lpOutBuffer As Byte(),
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              lpOutBuffer As SafeBuffer,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As SafeBuffer,
              nInBufferSize As UInt32,
              lpOutBuffer As SafeBuffer,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As SafeBuffer,
              nInBufferSize As UInt32,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As Int64,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <[In]> ByRef lpInBuffer As DISK_GROW_PARTITION,
              nInBufferSize As UInt32,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As DISK_GEOMETRY,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As PARTITION_INFORMATION,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As PARTITION_INFORMATION_EX,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As SCSI_ADDRESS,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As STORAGE_DEVICE_NUMBER,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <[In]> ByRef lpInBuffer As STORAGE_PROPERTY_QUERY,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As STORAGE_DESCRIPTOR_HEADER,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <[In]> ByRef lpInBuffer As STORAGE_PROPERTY_QUERY,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As DEVICE_TRIM_DESCRIPTOR,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <[In]> ByRef lpInBuffer As STORAGE_PROPERTY_QUERY,
              nInBufferSize As UInt32,
              lpOutBuffer As SafeBuffer,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Auto Function GetModuleFileName Lib "kernel32" (
              hModule As IntPtr,
              <MarshalAs(UnmanagedType.LPWStr)> lpFilename As StringBuilder,
              nSize As Int32) As Int32

            <CodeAnalysis.SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification:="Special Ansi only function")>
            Friend Declare Ansi Function GetProcAddress Lib "kernel32" (
              hModule As IntPtr,
              <[In], MarshalAs(UnmanagedType.LPStr)> lpEntryName As String) As IntPtr

            Friend Declare Ansi Function GetProcAddress Lib "kernel32" (
              hModule As IntPtr,
              ordinal As IntPtr) As IntPtr

            Friend Declare Auto Function RtlDosPathNameToNtPathName_U Lib "ntdll.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> DosName As String,
              <Out> ByRef NtName As UNICODE_STRING,
              DosFilePath As IntPtr,
              NtFilePath As IntPtr) As Boolean

            Friend Declare Sub RtlFreeUnicodeString Lib "ntdll.dll" (
              ByRef UnicodeString As UNICODE_STRING)

            Friend Declare Function RtlNtStatusToDosError Lib "ntdll.dll" (
              NtStatus As Int32) As Int32

            Friend Declare Auto Function GetPrivateProfileSectionNames Lib "kernel32.dll" (
              <Out, MarshalAs(UnmanagedType.LPArray)> Names As Char(),
              NamesSize As Integer,
              <[In], MarshalAs(UnmanagedType.LPWStr)> FileName As String) As Integer

            Friend Declare Auto Function GetPrivateProfileSection Lib "kernel32.dll" (
              <[In], MarshalAs(UnmanagedType.LPWStr)> SectionName As String,
              <[In], MarshalAs(UnmanagedType.LPArray)> Values As Char(),
              ValuesSize As Integer,
              <[In], MarshalAs(UnmanagedType.LPWStr)> FileName As String) As Integer

            Friend Declare Auto Function WritePrivateProfileString Lib "kernel32.dll" (
              <[In], MarshalAs(UnmanagedType.LPWStr)> SectionName As String,
              <[In], MarshalAs(UnmanagedType.LPWStr)> SettingName As String,
              <[In], MarshalAs(UnmanagedType.LPWStr)> Value As String,
              <[In], MarshalAs(UnmanagedType.LPWStr)> FileName As String) As Boolean

            Friend Declare Auto Sub InstallHinfSection Lib "setupapi.dll" (
              hwndOwner As IntPtr,
              hModule As IntPtr,
              <MarshalAs(UnmanagedType.LPWStr), [In]> lpCmdLine As String,
              nCmdShow As Int32)

            Friend Declare Auto Function SetupCopyOEMInf Lib "setupapi.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> SourceInfFileName As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> OEMSourceMediaLocation As String,
              OEMSourceMediaType As UInt32,
              CopyStyle As UInt32,
              <MarshalAs(UnmanagedType.LPWStr), [In], Out> DestinationInfFileName As StringBuilder,
              DestinationInfFileNameSize As Int32,
              <Out> ByRef RequiredSize As UInt32,
              DestinationInfFileNameComponent As IntPtr) As Boolean

            Public Const DRIVER_PACKAGE_DELETE_FILES = &H20UI
            Public Const DRIVER_PACKAGE_FORCE = &H4UI
            Public Const DRIVER_PACKAGE_SILENT = &H2UI

            Friend Declare Auto Function DriverPackagePreinstall Lib "difxapi.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> SourceInfFileName As String,
              Options As UInt32) As Integer

            Friend Declare Auto Function DriverPackageInstall Lib "difxapi.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> SourceInfFileName As String,
              Options As UInt32,
              pInstallerInfo As IntPtr,
              <Out> ByRef pNeedReboot As Boolean) As Integer

            Friend Declare Auto Function DriverPackageUninstall Lib "difxapi.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> SourceInfFileName As String,
              Options As UInt32,
              pInstallerInfo As IntPtr,
              <Out> ByRef pNeedReboot As Boolean) As Integer

            Friend Declare Auto Function CM_Locate_DevNode Lib "setupapi.dll" (
              ByRef devInst As UInt32,
              <MarshalAs(UnmanagedType.LPWStr), [In]> rootid As String,
              Flags As UInt32) As UInt32

            Friend Declare Auto Function CM_Get_DevNode_Registry_Property Lib "setupapi.dll" (
              DevInst As UInt32,
              Prop As CmDevNodeRegistryProperty,
              <Out> ByRef RegDataType As RegistryValueKind,
              <Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=4)> Buffer As Byte(),
              <[In], Out> ByRef BufferLength As Int32,
              Flags As UInt32) As UInt32

            Friend Declare Auto Function CM_Set_DevNode_Registry_Property Lib "setupapi.dll" (
              DevInst As UInt32,
              Prop As CmDevNodeRegistryProperty,
              <[In], MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=3)> Buffer As Byte(),
              length As Int32,
              Flags As UInt32) As UInt32

            Friend Declare Auto Function CM_Get_Class_Registry_Property Lib "setupapi.dll" (
              <[In]> ByRef ClassGuid As Guid,
              Prop As CmClassRegistryProperty,
              <Out> ByRef RegDataType As RegistryValueKind,
              <Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=4)> Buffer As Byte(),
              <[In], Out> ByRef BufferLength As Int32,
              Flags As UInt32,
              Optional hMachine As IntPtr = Nothing) As UInt32

            Friend Declare Auto Function CM_Set_Class_Registry_Property Lib "setupapi.dll" (
              <[In]> ByRef ClassGuid As Guid,
              Prop As CmClassRegistryProperty,
              <[In], MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=3)> Buffer As Byte(),
              length As Int32,
              Flags As UInt32,
              Optional hMachine As IntPtr = Nothing) As UInt32

            Friend Declare Auto Function CM_Get_Child Lib "setupapi.dll" (
              <Out> ByRef dnDevInst As UInt32,
              DevInst As UInt32,
              Flags As UInt32) As UInt32

            Friend Declare Auto Function CM_Get_Sibling Lib "setupapi.dll" (
              <Out> ByRef dnDevInst As UInt32,
              DevInst As UInt32,
              Flags As UInt32) As UInt32

            Friend Declare Function CM_Reenumerate_DevNode Lib "setupapi.dll" (
              devInst As UInt32,
              Flags As UInt32) As UInt32

            Public Const CM_GETIDLIST_FILTER_SERVICE As UInt32 = &H2UI

            Friend Declare Auto Function CM_Get_Device_ID_List_Size Lib "setupapi.dll" (
              ByRef Length As Int32,
              <MarshalAs(UnmanagedType.LPWStr), [In]> filter As String,
              Flags As UInt32) As UInt32

            Friend Declare Auto Function CM_Get_Device_ID_List Lib "setupapi.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> filter As String,
              <Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=2)> Buffer As Char(),
              BufferLength As UInt32,
              Flags As UInt32) As UInt32

            Friend Declare Auto Function SetupSetNonInteractiveMode Lib "setupapi.dll" (
              state As Boolean) As Boolean

            Friend Declare Auto Function SetupOpenInfFile Lib "setupapi.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> FileName As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> InfClass As String,
              InfStyle As UInt32,
              ByRef ErrorLine As UInt32) As SafeInfHandle

            Public Delegate Function SetupFileCallback(Context As IntPtr,
                                                       Notification As UInt32,
                                                       Param1 As UIntPtr,
                                                       Param2 As UIntPtr) As UInt32

            Friend Declare Auto Function SetupInstallFromInfSection Lib "setupapi.dll" (
              hWnd As IntPtr,
              InfHandle As SafeInfHandle,
              <MarshalAs(UnmanagedType.LPWStr), [In]> SectionName As String,
              Flags As UInt32,
              RelativeKeyRoot As IntPtr,
              <MarshalAs(UnmanagedType.LPWStr), [In]> SourceRootPath As String,
              CopyFlags As UInt32,
              MsgHandler As SetupFileCallback,
              Context As IntPtr,
              DeviceInfoSet As IntPtr,
              DeviceInfoData As IntPtr) As Boolean

            Friend Declare Auto Function SetupInstallFromInfSection Lib "setupapi.dll" (
              hWnd As IntPtr,
              InfHandle As SafeInfHandle,
              <MarshalAs(UnmanagedType.LPWStr), [In]> SectionName As String,
              Flags As UInt32,
              RelativeKeyRoot As SafeRegistryHandle,
              <MarshalAs(UnmanagedType.LPWStr), [In]> SourceRootPath As String,
              CopyFlags As UInt32,
              MsgHandler As SetupFileCallback,
              Context As IntPtr,
              DeviceInfoSet As IntPtr,
              DeviceInfoData As IntPtr) As Boolean

            Public Const DIGCF_DEFAULT As UInt32 = &H1
            Public Const DIGCF_PRESENT As UInt32 = &H2
            Public Const DIGCF_ALLCLASSES As UInt32 = &H4
            Public Const DIGCF_PROFILE As UInt32 = &H8
            Public Const DIGCF_DEVICEINTERFACE As UInt32 = &H10

            Friend Declare Auto Function SetupDiGetINFClass Lib "setupapi.dll" (
              <MarshalAs(UnmanagedType.LPWStr), [In]> InfPath As String,
              <Out> ByRef ClassGuid As Guid,
              <Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=3)> ClassName As Char(),
              ClassNameSize As UInt32,
              <Out> ByRef RequiredSize As UInt32) As Boolean

            Friend Declare Auto Function SetupDiOpenDeviceInfo Lib "setupapi.dll" (
              DevInfoSet As SafeDeviceInfoSetHandle,
              <[In], MarshalAs(UnmanagedType.LPWStr)> Enumerator As String,
              hWndParent As IntPtr,
              Flags As UInt32,
              DeviceInfoData As IntPtr) As Boolean

            Friend Declare Auto Function SetupDiOpenDeviceInfo Lib "setupapi.dll" (
              DevInfoSet As SafeDeviceInfoSetHandle,
              Enumerator As Byte(),
              hWndParent As IntPtr,
              Flags As UInt32,
              DeviceInfoData As IntPtr) As Boolean

            Friend Declare Auto Function SetupDiGetClassDevs Lib "setupapi.dll" (
              <[In]> ByRef ClassGuid As Guid,
              <[In], MarshalAs(UnmanagedType.LPWStr)> Enumerator As String,
              hWndParent As IntPtr,
              Flags As UInt32) As SafeDeviceInfoSetHandle

            Friend Declare Auto Function SetupDiGetClassDevs Lib "setupapi.dll" (
              ClassGuid As IntPtr,
              <[In], MarshalAs(UnmanagedType.LPWStr)> Enumerator As String,
              hWndParent As IntPtr,
              Flags As UInt32) As SafeDeviceInfoSetHandle

            <StructLayout(LayoutKind.Sequential)>
            Public Structure SP_DEVICE_INTERFACE_DATA
                Public ReadOnly Property Size As UInt32
                Public ReadOnly Property InterfaceClassGuid As Guid
                Public ReadOnly Property Flags As UInt32
                Public ReadOnly Property Reserved As IntPtr

                Public Sub Initialize()
                    _Size = CUInt(Marshal.SizeOf(Me))
                End Sub
            End Structure

            <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
            Public Structure SP_DEVICE_INTERFACE_DETAIL_DATA

                Public ReadOnly Property Size As UInt32

                Public ReadOnly Property DevicePath As String
                    Get
                        Return _devicePath
                    End Get
                End Property

                <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=32768)>
                Private ReadOnly _devicePath As String

                Public Sub Initialize()
                    _Size = CUInt(Marshal.SizeOf(Me))
                End Sub
            End Structure

            <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
            Public Structure SP_DEVINFO_LIST_DETAIL_DATA

                Public Const SP_MAX_MACHINENAME_LENGTH = 263

                Public ReadOnly Property Size As UInt32

                Public ReadOnly Property ClassGUID As Guid

                Public ReadOnly Property RemoteMachineHandle As IntPtr

                Public Property RemoteMachineName As String
                    Get
                        Return _remoteMachineName
                    End Get
                    Set(value As String)
                        _remoteMachineName = value
                    End Set
                End Property

                <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=SP_MAX_MACHINENAME_LENGTH)>
                Private _remoteMachineName As String

                Public Sub Initialize()
                    _Size = CUInt(Marshal.SizeOf(Me))
                End Sub
            End Structure

            Friend Declare Auto Function SetupDiEnumDeviceInfo Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              MemberIndex As UInt32,
              <[Out]> ByRef DeviceInterfaceData As SP_DEVINFO_DATA) As Boolean

            Friend Declare Auto Function SetupDiRestartDevices Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              <[In], [Out]> ByRef DeviceInterfaceData As SP_DEVINFO_DATA) As Boolean

            Friend Declare Auto Function SetupDiSetClassInstallParams Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              <[In], Out> ByRef DeviceInfoData As SP_DEVINFO_DATA,
              <[In]> ByRef ClassInstallParams As SP_PROPCHANGE_PARAMS,
              ClassInstallParamsSize As Integer) As Boolean

            Public Const DIF_PROPERTYCHANGE As UInt32 = &H12
            Public Const DICS_FLAG_CONFIGSPECIFIC As UInt32 = &H2  '' make change in specified profile only
            Public Const DICS_PROPCHANGE As UInt32 = &H3

            Friend Declare Auto Function SetupDiEnumDeviceInterfaces Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              DeviceInfoData As IntPtr,
              <[In]> ByRef ClassGuid As Guid,
              MemberIndex As UInt32,
              <[Out]> ByRef DeviceInterfaceData As SP_DEVICE_INTERFACE_DATA) As Boolean

            Friend Declare Auto Function SetupDiEnumDeviceInterfaces Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              DeviceInfoData As IntPtr,
              ClassGuid As IntPtr,
              MemberIndex As UInt32,
              <[Out]> ByRef DeviceInterfaceData As SP_DEVICE_INTERFACE_DATA) As Boolean

            Friend Declare Auto Function SetupDiCreateDeviceInfoListEx Lib "setupapi.dll" (
              <[In]> ByRef ClassGuid As Guid,
              hwndParent As IntPtr,
              <MarshalAs(UnmanagedType.LPWStr), [In]> MachineName As String,
              Reserved As IntPtr) As SafeDeviceInfoSetHandle

            Friend Declare Auto Function SetupDiCreateDeviceInfoListEx Lib "setupapi.dll" (
              ClassGuid As IntPtr,
              hwndParent As IntPtr,
              <MarshalAs(UnmanagedType.LPWStr), [In]> MachineName As String,
              Reserved As IntPtr) As SafeDeviceInfoSetHandle

            Friend Declare Auto Function SetupDiCreateDeviceInfoList Lib "setupapi.dll" (
              <[In]> ByRef ClassGuid As Guid,
              hwndParent As IntPtr) As SafeDeviceInfoSetHandle

            Friend Declare Auto Function SetupDiCreateDeviceInfoList Lib "setupapi.dll" (
              ClassGuid As IntPtr,
              hwndParent As IntPtr) As SafeDeviceInfoSetHandle

            Friend Declare Auto Function SetupDiGetDeviceInterfaceDetail Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              <[In]> ByRef DeviceInterfaceData As SP_DEVICE_INTERFACE_DATA,
              <[Out](), MarshalAs(UnmanagedType.LPStruct, SizeParamIndex:=3)> ByRef DeviceInterfaceDetailData As SP_DEVICE_INTERFACE_DETAIL_DATA,
              DeviceInterfaceDetailDataSize As UInt32,
              <Out> ByRef RequiredSize As UInt32,
              DeviceInfoData As IntPtr) As Boolean

            Friend Declare Auto Function SetupDiGetDeviceInfoListDetail Lib "setupapi.dll" (
              devinfo As SafeDeviceInfoSetHandle,
              <Out> ByRef DeviceInfoDetailData As SP_DEVINFO_LIST_DETAIL_DATA) As Boolean

            Friend Declare Auto Function SetupDiCreateDeviceInfo Lib "setupapi.dll" (
              hDevInfo As SafeDeviceInfoSetHandle,
              <MarshalAs(UnmanagedType.LPWStr), [In]> DeviceName As String,
              <[In]> ByRef ClassGuid As Guid,
              <MarshalAs(UnmanagedType.LPWStr), [In]> DeviceDescription As String,
              owner As IntPtr,
              CreationFlags As UInt32,
              <Out> ByRef DeviceInfoData As SP_DEVINFO_DATA) As Boolean

            Friend Declare Auto Function SetupDiSetDeviceRegistryProperty Lib "setupapi.dll" (
              hDevInfo As SafeDeviceInfoSetHandle,
              ByRef DeviceInfoData As SP_DEVINFO_DATA,
              [Property] As UInt32,
              <[In], MarshalAs(UnmanagedType.LPArray)> PropertyBuffer As Byte(),
              PropertyBufferSize As UInt32) As Boolean

            Public Structure SP_CLASSINSTALL_HEADER
                Public Property Size As UInt32
                Public Property InstallFunction As UInt32
            End Structure

            Public Structure SP_PROPCHANGE_PARAMS
                Public Property ClassInstallHeader As SP_CLASSINSTALL_HEADER
                Public Property StateChange As UInt32
                Public Property Scope As UInt32
                Public Property HwProfile As UInt32
            End Structure

            Friend Declare Auto Function SetupDiCallClassInstaller Lib "setupapi.dll" (
              InstallFunction As UInt32,
              hDevInfo As SafeDeviceInfoSetHandle,
              <[In]> ByRef DeviceInfoData As SP_DEVINFO_DATA) As Boolean

            Friend Declare Auto Function UpdateDriverForPlugAndPlayDevices Lib "newdev.dll" (
              owner As IntPtr,
              <MarshalAs(UnmanagedType.LPWStr), [In]> HardwareId As String,
              <MarshalAs(UnmanagedType.LPWStr), [In]> InfPath As String,
              InstallFlags As UInt32,
              <MarshalAs(UnmanagedType.Bool), Out> ByRef RebootRequired As Boolean) As Boolean

            Friend Declare Auto Function ExitWindowsEx Lib "user32.dll" (
              flags As ShutdownFlags,
              reason As ShutdownReasons) As Boolean

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <MarshalAs(UnmanagedType.LPArray), Out> buffer As Byte(),
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              buffer As IntPtr,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <Out> ByRef buffer As SByte,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <Out> ByRef buffer As Int16,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <Out> ByRef buffer As Int32,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <Out> ByRef buffer As Int64,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <Out> ByRef buffer As Byte,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <Out> ByRef buffer As UInt16,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <Out> ByRef buffer As UInt32,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <Out> ByRef buffer As UInt64,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGenRandom Lib "advapi32" Alias "SystemFunction036" (
              <Out> ByRef buffer As Guid,
              length As Int32) As Byte

            Friend Declare Auto Function RtlGetVersion Lib "ntdll.dll" (
              <[In], Out> ByRef os_version As OSVERSIONINFO) As Integer

            Friend Declare Auto Function RtlGetVersion Lib "ntdll.dll" (
              <[In], Out> ByRef os_version As OSVERSIONINFOEX) As Integer

            Friend Declare Auto Function LookupPrivilegeValue Lib "advapi32.dll" (
              <[In], MarshalAs(UnmanagedType.LPWStr)> lpSystemName As String,
              <[In], MarshalAs(UnmanagedType.LPWStr)> lpName As String,
              <Out> ByRef lpLuid As Int64) As Boolean

            Friend Declare Auto Function OpenThreadToken Lib "advapi32.dll" (
              <[In]> hThread As IntPtr,
              <[In]> dwAccess As UInteger,
              <[In]> openAsSelf As Boolean,
              <Out> ByRef lpTokenHandle As SafeFileHandle) As Boolean

            Friend Declare Auto Function OpenProcessToken Lib "advapi32.dll" (
              <[In]> hProcess As IntPtr,
              <[In]> dwAccess As UInteger,
              <Out> ByRef lpTokenHandle As SafeFileHandle) As Boolean

            Friend Declare Auto Function AdjustTokenPrivileges Lib "advapi32.dll" (
              <[In]> TokenHandle As SafeFileHandle,
              <[In]> DisableAllPrivileges As Boolean,
              <[In]> NewStates As SafeBuffer,
              <[In]> BufferLength As Integer,
              <[In]> PreviousState As SafeBuffer,
              <Out> ByRef ReturnLength As Integer) As Boolean

            Friend Declare Auto Function NtQuerySystemInformation Lib "ntdll.dll" (
              <[In]> SystemInformationClass As SystemInformationClass,
              <[In]> pSystemInformation As SafeBuffer,
              <[In]> uSystemInformationLength As Integer,
              <Out> ByRef puReturnLength As Integer) As Integer

            Friend Declare Auto Function NtQueryObject Lib "ntdll.dll" (
              <[In]> ObjectHandle As SafeFileHandle,
              <[In]> ObjectInformationClass As ObjectInformationClass,
              <[In]> ObjectInformation As SafeBuffer,
              <[In]> ObjectInformationLength As Integer,
              <Out> ByRef puReturnLength As Integer) As Integer

            Friend Declare Auto Function NtQueryVolumeInformationFile Lib "ntdll.dll" (
              <[In]> FileHandle As SafeFileHandle,
              <[In], Out> IoStatusBlock As IoStatusBlock,
              <[In]> FsInformation As SafeBuffer,
              <[In]> FsInformationLength As Integer,
              <[In]> FsInformationClass As FsInformationClass) As Integer

            Friend Declare Auto Function NtDuplicateObject Lib "ntdll.dll" (
              <[In]> SourceProcessHandle As SafeHandle,
              <[In]> SourceHandle As IntPtr,
              <[In]> TargetProcessHandle As IntPtr,
              <Out> ByRef TargetHandle As SafeFileHandle,
              <[In]> DesiredAccess As UInteger,
              <[In]> HandleAttributes As UInteger,
              <[In]> Options As UInteger) As Integer

            Friend Declare Auto Function GetCurrentProcess Lib "kernel32.dll" () As IntPtr

            Friend Declare Auto Function GetCurrentThread Lib "kernel32.dll" () As IntPtr

            Friend Declare Auto Function OpenProcess Lib "kernel32.dll" (
              <[In]> DesiredAccess As UInteger,
              <[In]> InheritHandle As Boolean,
              <[In]> ProcessId As Integer) As SafeFileHandle

            Friend Declare Auto Function CreateHardLink Lib "kernel32.dll" (<MarshalAs(UnmanagedType.LPWStr), [In]> newlink As String, <MarshalAs(UnmanagedType.LPWStr), [In]> existing As String, security As IntPtr) As Boolean

            Friend Declare Auto Function MoveFile Lib "kernel32.dll" (<MarshalAs(UnmanagedType.LPWStr), [In]> existing As String, <MarshalAs(UnmanagedType.LPWStr), [In]> newname As String) As Boolean

            Friend Declare Auto Function RtlCompareMemoryUlong Lib "ntdll.dll" (<MarshalAs(UnmanagedType.LPArray), [In]> buffer As Byte(), length As IntPtr, v As Integer) As IntPtr
        End Class
#End Region

#Region "Miniport Control"

        ''' <summary>
        ''' Control methods for direct communication with SCSI miniport.
        ''' </summary>
        Public NotInheritable Class PhDiskMntCtl

            Public Const SMP_IMSCSI = &H83730000UI
            Public Const SMP_IMSCSI_QUERY_VERSION = SMP_IMSCSI Or &H800UI
            Public Const SMP_IMSCSI_CREATE_DEVICE = SMP_IMSCSI Or &H801UI
            Public Const SMP_IMSCSI_QUERY_DEVICE = SMP_IMSCSI Or &H802UI
            Public Const SMP_IMSCSI_QUERY_ADAPTER = SMP_IMSCSI Or &H803UI
            Public Const SMP_IMSCSI_CHECK = SMP_IMSCSI Or &H804UI
            Public Const SMP_IMSCSI_SET_DEVICE_FLAGS = SMP_IMSCSI Or &H805UI
            Public Const SMP_IMSCSI_REMOVE_DEVICE = SMP_IMSCSI Or &H806UI
            Public Const SMP_IMSCSI_EXTEND_DEVICE = SMP_IMSCSI Or &H807UI

            ''' <summary>
            ''' Signature to set in SRB_IO_CONTROL header. This identifies that sender and receiver of
            ''' IOCTL_SCSI_MINIPORT requests talk to intended components only.
            ''' </summary>
            Private Shared ReadOnly SrbIoCtlSignature As Byte() = Encoding.ASCII.GetBytes("PhDskMnt".PadRight(8, New Char))

            ''' <summary>
            ''' SRB_IO_CONTROL header, as defined in NTDDDISK.
            ''' </summary>
            <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Ansi)>
            <ComVisible(False)>
            Public Structure SRB_IO_CONTROL
                Public Property HeaderLength As UInt32
                <MarshalAs(UnmanagedType.ByValArray, SizeConst:=8)>
                Private _signature As Byte()
                Public Property Timeout As UInt32
                Public Property ControlCode As UInt32
                Public Property ReturnCode As UInt32
                Public Property Length As UInt32

                Friend Property Signature As Byte()
                    Get
                        Return _signature
                    End Get
                    Set
                        _signature = Value
                    End Set
                End Property
            End Structure

            ''' <summary>
            ''' Sends an IOCTL_SCSI_MINIPORT control request to a SCSI miniport.
            ''' </summary>
            ''' <param name="adapter">Open handle to SCSI adapter.</param>
            ''' <param name="ctrlcode">Control code to set in SRB_IO_CONTROL header.</param>
            ''' <param name="timeout">Timeout to set in SRB_IO_CONTROL header.</param>
            ''' <param name="databytes">Optional request data after SRB_IO_CONTROL header. The Length field in
            ''' SRB_IO_CONTROL header will be automatically adjusted to reflect the amount of data passed by this function.</param>
            ''' <param name="returncode">ReturnCode value from SRB_IO_CONTROL header upon return.</param>
            ''' <returns>This method returns a BinaryReader object that can be used to read and parse data returned after the
            ''' SRB_IO_CONTROL header.</returns>
            Public Shared Function SendSrbIoControl(adapter As SafeFileHandle,
                                                    ctrlcode As UInt32,
                                                    timeout As UInt32,
                                                    databytes As Byte(),
                                                    <Out> ByRef returncode As Int32) As Byte()

                Dim indata(0 To 28 - 1 + If(databytes?.Length, 0)) As Byte

                Using Request = PinnedBuffer.Create(indata)

                    Request.Write(0, PinnedBuffer(Of SRB_IO_CONTROL).TypeSize)
                    Request.WriteArray(4, SrbIoCtlSignature, 0, 8)
                    Request.Write(12, timeout)
                    Request.Write(16, ctrlcode)

                    If databytes Is Nothing Then
                        Request.Write(24, 0UI)
                    Else
                        Request.Write(24, databytes.Length)
                        Buffer.BlockCopy(databytes, 0, indata, 28, databytes.Length)
                    End If

                End Using

                Dim Response = DeviceIoControl(adapter,
                                               NativeConstants.IOCTL_SCSI_MINIPORT,
                                               indata,
                                               0)

                returncode = BitConverter.ToInt32(Response, 20)

                If databytes IsNot Nothing Then
                    Dim ResponseLength = Math.Min(Math.Min(BitConverter.ToInt32(Response, 24), Response.Length - 28), databytes.Length)
                    Buffer.BlockCopy(Response, 28, databytes, 0, ResponseLength)
                    Array.Resize(databytes, ResponseLength)
                End If

                Return databytes

            End Function

        End Class

#End Region

        Public Shared ReadOnly Property SystemArchitecture As String = GetSystemArchitecture()

        Public Shared ReadOnly Property ProcessArchitecture As String = GetProcessArchitecture()

        Private Shared Function GetSystemArchitecture() As String

            Dim SystemArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432")
            If SystemArchitecture Is Nothing Then
                SystemArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
            End If
            If SystemArchitecture Is Nothing Then
                SystemArchitecture = "x86"
            End If
            SystemArchitecture = String.Intern(SystemArchitecture.ToLowerInvariant())

            Trace.WriteLine($"System architecture is: {SystemArchitecture}")

            Return SystemArchitecture

        End Function

        Private Shared Function GetProcessArchitecture() As String

            Dim ProcessArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
            If ProcessArchitecture Is Nothing Then
                ProcessArchitecture = "x86"
            End If
            ProcessArchitecture = String.Intern(ProcessArchitecture.ToLowerInvariant())

            Trace.WriteLine($"Process architecture is: {ProcessArchitecture}")

            Return ProcessArchitecture

        End Function

        Public Shared Sub BrowseTo(target As String)

            Process.Start(New ProcessStartInfo With {.FileName = target, .UseShellExecute = True})?.Dispose()

        End Sub

        Public Structure NativeWindowHandle
            Implements Windows.Forms.IWin32Window

            Public ReadOnly Property Handle As IntPtr Implements Windows.Forms.IWin32Window.Handle

            Public Sub New(handle As IntPtr)
                _Handle = handle
            End Sub

            Public Overrides Function ToString() As String
                Return _Handle.ToString()
            End Function

        End Structure

        Private Sub New()

        End Sub

        ''' <summary>
        ''' Encapsulates call to a Win32 API function that returns a BOOL value indicating success
        ''' or failure and where an error value is available through a call to GetLastError() in case
        ''' of failure. If value True is passed to this method it does nothing. If False is passed,
        ''' it calls GetLastError(), converts error code to a HRESULT value and throws a managed
        ''' exception for that HRESULT.
        ''' </summary>
        ''' <param name="result">Return code from a Win32 API function call.</param>
        Public Shared Sub Win32Try(result As Boolean)

            If result = False Then
                Throw New Win32Exception
            End If

        End Sub

        ''' <summary>
        ''' Encapsulates call to a Win32 API function that returns a value where failure
        ''' is indicated as a NULL return and GetLastError() returns an error code. If
        ''' non-zero value is passed to this method it just returns that value. If zero
        ''' value is passed, it calls GetLastError() and throws a managed exception for
        ''' that error code.
        ''' </summary>
        ''' <param name="result">Return code from a Win32 API function call.</param>
        Public Shared Function Win32Try(Of T)(result As T) As T

            If result Is Nothing Then
                Throw New Win32Exception
            End If
            Return result

        End Function

        ''' <summary>
        ''' Encapsulates call to an ntdll.dll API function that returns an NTSTATUS value indicating
        ''' success or error status. If result is zero or positive, this function just passes through
        ''' that value as return value. If result is negative indicating an error, it converts error
        ''' code to a Win32 error code and throws a managed exception for that error code.
        ''' </summary>
        ''' <param name="result">Return code from a ntdll.dll API function call.</param>
        Public Shared Function NtDllTry(result As Integer) As Integer

            If result < 0 Then
                Throw New Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(result))
            End If

            Return result

        End Function

        Public Shared Function OfflineDiskVolumes(device_path As String, force As Boolean) As Boolean

            Return OfflineDiskVolumes(device_path, force, CancellationToken.None)

        End Function

        Public Shared Function OfflineDiskVolumes(device_path As String, force As Boolean, cancel As CancellationToken) As Boolean

            Dim refresh = False

            For Each volume In EnumerateDiskVolumes(device_path)

                cancel.ThrowIfCancellationRequested()

                Try
                    Using device As New DiskDevice(volume.TrimEnd("\"c), FileAccess.ReadWrite)

                        If device.IsDiskWritable AndAlso Not device.DiskPolicyReadOnly.GetValueOrDefault() Then

                            Try
                                device.FlushBuffers()
                                device.DismountVolumeFilesystem(Force:=False)

                            Catch ex As Win32Exception When (
                                ex.NativeErrorCode = NativeConstants.ERROR_WRITE_PROTECT OrElse
                                ex.NativeErrorCode = NativeConstants.ERROR_NOT_READY OrElse
                                ex.NativeErrorCode = NativeConstants.ERROR_DEV_NOT_EXIST)

                                device.DismountVolumeFilesystem(Force:=True)
                            End Try

                        Else

                            device.DismountVolumeFilesystem(Force:=True)

                        End If

                        device.SetVolumeOffline(True)

                    End Using

                    refresh = True

                    Continue For

                Catch ex As Exception
                    Trace.WriteLine($"Failed to safely dismount volume '{volume}': {ex.JoinMessages()}")

                    If Not force Then
                        Dim dev_paths = QueryDosDevice(volume.Substring(4, 44)).ToArray()
                        Dim in_use_apps = EnumerateProcessesHoldingFileHandle(dev_paths).Take(10).Select(AddressOf FormatProcessName).ToArray()

                        If in_use_apps.Length > 1 Then
                            Throw New IOException($"Failed to safely dismount volume '{volume}'.

Currently, the following applications have files open on this volume:
{String.Join(", ", in_use_apps)}", ex)
                        ElseIf in_use_apps.Length = 1 Then
                            Throw New IOException($"Failed to safely dismount volume '{volume}'.

Currently, the following application has files open on this volume:
{in_use_apps(0)}", ex)
                        Else
                            Throw New IOException($"Failed to safely dismount volume '{volume}'", ex)
                        End If

                    End If

                End Try

                cancel.ThrowIfCancellationRequested()

                Try
                    Using device As New DiskDevice(volume.TrimEnd("\"c), FileAccess.ReadWrite)
                        device.FlushBuffers()
                        device.DismountVolumeFilesystem(True)
                        device.SetVolumeOffline(True)
                    End Using

                    refresh = True
                    Continue For

                Catch ex As Exception
                    Trace.WriteLine($"Failed to forcefully dismount volume '{volume}': {ex.JoinMessages()}")

                End Try

                Return False

                cancel.ThrowIfCancellationRequested()

                Try
                    Using device As New DiskDevice(volume.TrimEnd("\"c), FileAccess.ReadWrite)
                        device.SetVolumeOffline(True)
                    End Using

                    refresh = True
                    Continue For

                Catch ex As Exception
                    Trace.WriteLine($"Failed to offline volume '{volume}': {ex.JoinMessages()}")

                End Try

            Next

            Return refresh

        End Function

#If NET45_OR_GREATER OrElse NETCOREAPP OrElse NETSTANDARD Then

        Public Shared Async Function OfflineDiskVolumesAsync(device_path As String, force As Boolean, cancel As CancellationToken) As Task(Of Boolean)

            Dim refresh = False

            For Each volume In EnumerateDiskVolumes(device_path)

                cancel.ThrowIfCancellationRequested()

                Try
                    Using device As New DiskDevice(volume.TrimEnd("\"c), FileAccess.ReadWrite)

                        If device.IsDiskWritable AndAlso Not device.DiskPolicyReadOnly.GetValueOrDefault() Then

                            Dim t As Task = Nothing

                            Try
                                device.FlushBuffers()
                                Await device.DismountVolumeFilesystemAsync(Force:=False, cancel).ConfigureAwait(continueOnCapturedContext:=False)

                            Catch ex As Win32Exception When (
                                ex.NativeErrorCode = NativeConstants.ERROR_WRITE_PROTECT OrElse
                                ex.NativeErrorCode = NativeConstants.ERROR_NOT_READY OrElse
                                ex.NativeErrorCode = NativeConstants.ERROR_DEV_NOT_EXIST)

                                t = device.DismountVolumeFilesystemAsync(Force:=True, cancel)

                            End Try

                            If t IsNot Nothing Then
                                Await t.ConfigureAwait(continueOnCapturedContext:=False)
                            End If

                        Else

                            Await device.DismountVolumeFilesystemAsync(Force:=True, cancel).ConfigureAwait(continueOnCapturedContext:=False)

                        End If

                        device.SetVolumeOffline(True)

                    End Using

                    refresh = True

                    Continue For

                Catch ex As Exception
                    Trace.WriteLine($"Failed to safely dismount volume '{volume}': {ex.JoinMessages()}")

                    If Not force Then
                        Dim dev_paths = QueryDosDevice(volume.Substring(4, 44)).ToArray()
                        Dim in_use_apps = EnumerateProcessesHoldingFileHandle(dev_paths).Take(10).Select(AddressOf FormatProcessName).ToArray()

                        If in_use_apps.Length > 1 Then
                            Throw New IOException($"Failed to safely dismount volume '{volume}'.

Currently, the following applications have files open on this volume:
{String.Join(", ", in_use_apps)}", ex)
                        ElseIf in_use_apps.Length = 1 Then
                            Throw New IOException($"Failed to safely dismount volume '{volume}'.

Currently, the following application has files open on this volume:
{in_use_apps(0)}", ex)
                        Else
                            Throw New IOException($"Failed to safely dismount volume '{volume}'", ex)
                        End If

                    End If

                End Try

                cancel.ThrowIfCancellationRequested()

                Try
                    Using device As New DiskDevice(volume.TrimEnd("\"c), FileAccess.ReadWrite)
                        device.FlushBuffers()
                        Await device.DismountVolumeFilesystemAsync(True, cancel).ConfigureAwait(continueOnCapturedContext:=False)
                        device.SetVolumeOffline(True)
                    End Using

                    refresh = True
                    Continue For

                Catch ex As Exception
                    Trace.WriteLine($"Failed to forcefully dismount volume '{volume}': {ex.JoinMessages()}")

                End Try

                Return False

                cancel.ThrowIfCancellationRequested()

                Try
                    Using device As New DiskDevice(volume.TrimEnd("\"c), FileAccess.ReadWrite)
                        device.SetVolumeOffline(True)
                    End Using

                    refresh = True
                    Continue For

                Catch ex As Exception
                    Trace.WriteLine($"Failed to offline volume '{volume}': {ex.JoinMessages()}")

                End Try

            Next

            Return refresh

        End Function

        Public Shared Async Function WaitForDiskIoIdleAsync(device_path As String, iterations As Integer, waitTime As TimeSpan, cancel As CancellationToken) As Task

            Dim volumes = EnumerateDiskVolumes(device_path).ToArray()

            Dim dev_paths = volumes.SelectMany(Function(volume) QueryDosDevice(volume.Substring(4, 44))).ToArray()

            Dim in_use_apps As String()

            For i = 1 To iterations
                cancel.ThrowIfCancellationRequested()

                in_use_apps = EnumerateProcessesHoldingFileHandle(dev_paths).Take(10).Select(AddressOf FormatProcessName).ToArray()
                If in_use_apps.Length = 0 Then
                    Exit For
                End If

                Trace.WriteLine($"File systems still in use by process {String.Join(", ", in_use_apps)}")

                Await Task.Delay(waitTime, cancel).ConfigureAwait(continueOnCapturedContext:=False)
            Next

        End Function
#End If

        Public Shared Sub EnableFileSecurityBypassPrivileges()

            Dim privileges_enabled = EnablePrivileges(
                NativeConstants.SE_BACKUP_NAME,
                NativeConstants.SE_RESTORE_NAME,
                NativeConstants.SE_DEBUG_NAME,
                NativeConstants.SE_MANAGE_VOLUME_NAME,
                NativeConstants.SE_SECURITY_NAME,
                NativeConstants.SE_TCB_NAME)

            If privileges_enabled IsNot Nothing Then
                Trace.WriteLine($"Enabled privileges: {String.Join(", ", privileges_enabled)}")
            Else
                Trace.WriteLine("Error enabling privileges.")
            End If

        End Sub

        Public Shared Sub ShutdownSystem(Flags As ShutdownFlags, Reason As ShutdownReasons)

            EnablePrivileges(NativeConstants.SE_SHUTDOWN_NAME)

            Win32Try(UnsafeNativeMethods.ExitWindowsEx(Flags, Reason))

        End Sub

        Public Shared Function EnablePrivileges(ParamArray privileges As String()) As String()

            Dim token As SafeFileHandle = Nothing
            If Not UnsafeNativeMethods.OpenThreadToken(UnsafeNativeMethods.GetCurrentThread(), NativeConstants.TOKEN_ADJUST_PRIVILEGES Or NativeConstants.TOKEN_QUERY, openAsSelf:=True, token) Then
                Win32Try(UnsafeNativeMethods.OpenProcessToken(UnsafeNativeMethods.GetCurrentProcess(), NativeConstants.TOKEN_ADJUST_PRIVILEGES Or NativeConstants.TOKEN_QUERY, token))
            End If

            Using token

                Dim intsize = CLng(PinnedBuffer(Of Integer).TypeSize)
                Dim structsize = PinnedBuffer(Of LUID_AND_ATTRIBUTES).TypeSize

                Dim luid_and_attribs_list As New Dictionary(Of String, LUID_AND_ATTRIBUTES)(privileges.Length)

                For Each privilege In privileges

                    Dim luid_and_attribs As New LUID_AND_ATTRIBUTES With {
                        .Attributes = NativeConstants.SE_PRIVILEGE_ENABLED
                    }

                    If UnsafeNativeMethods.LookupPrivilegeValue(Nothing, privilege, luid_and_attribs.LUID) Then

                        luid_and_attribs_list.Add(privilege, luid_and_attribs)

                    End If

                Next

                If luid_and_attribs_list.Count = 0 Then

                    Return Nothing

                End If

                Using buffer As New PinnedBuffer(Of Byte)(CInt(intsize + privileges.LongLength * structsize))

                    buffer.Write(0, luid_and_attribs_list.Count)

                    buffer.WriteArray(CULng(intsize), luid_and_attribs_list.Values.ToArray(), 0, luid_and_attribs_list.Count)

                    Dim rc = UnsafeNativeMethods.AdjustTokenPrivileges(token, False, buffer, CInt(buffer.ByteLength), buffer, Nothing)

                    Dim err = Marshal.GetLastWin32Error()

                    If Not rc Then
                        Throw New Win32Exception
                    End If

                    If err = NativeConstants.ERROR_NOT_ALL_ASSIGNED Then
                        Dim count = buffer.Read(Of Integer)(0)
                        Dim enabled_luids(0 To count - 1) As LUID_AND_ATTRIBUTES
                        buffer.ReadArray(CULng(intsize), enabled_luids, 0, count)
                        Dim enabled_privileges = Aggregate enabled_luid In enabled_luids
                                                     Join privilege_name In luid_and_attribs_list
                                                         On enabled_luid.LUID Equals privilege_name.Value.LUID
                                                         Select privilege_name.Key
                                                         Into ToArray()

                        Return enabled_privileges
                    End If

                    Return privileges

                End Using

            End Using

        End Function

        Private NotInheritable Class NativeWaitHandle
            Inherits WaitHandle

            Public Sub New(handle As SafeWaitHandle)
                SafeWaitHandle = handle
            End Sub

        End Class

        Public Shared Function CreateWaitHandle(Handle As IntPtr, inheritable As Boolean) As WaitHandle

            Dim new_handle As SafeWaitHandle = Nothing

            Dim current_process = UnsafeNativeMethods.GetCurrentProcess()

            If Not UnsafeNativeMethods.DuplicateHandle(current_process, Handle, current_process, new_handle, 0, inheritable, &H2) Then
                Throw New Win32Exception
            End If

            Return New NativeWaitHandle(new_handle)

        End Function

        Public Shared Sub SetEvent(handle As SafeWaitHandle)

            Win32Try(UnsafeNativeMethods.SetEvent(handle))

        End Sub

        Public Shared Sub SetInheritable(handle As SafeHandle, inheritable As Boolean)

            Win32Try(UnsafeNativeMethods.SetHandleInformation(handle, 1UI, If(inheritable, 1UI, 0UI)))

        End Sub

        Public Shared Sub SetProtectFromClose(handle As SafeHandle, protect_from_close As Boolean)

            Win32Try(UnsafeNativeMethods.SetHandleInformation(handle, 2UI, If(protect_from_close, 2UI, 0UI)))

        End Sub

        Public Shared Function GenRandomInt32() As Integer

            Dim value As Integer

            UnsafeNativeMethods.RtlGenRandom(value, 4)

            Return value

        End Function

        Public Shared Function GenRandomInt64() As Long

            Dim value As Long

            UnsafeNativeMethods.RtlGenRandom(value, 8)

            Return value

        End Function

        Public Shared Function GenRandomUInt32() As UInteger

            Dim value As UInteger

            UnsafeNativeMethods.RtlGenRandom(value, 4)

            Return value

        End Function

        Public Shared Function GenRandomUInt64() As ULong

            Dim value As ULong

            UnsafeNativeMethods.RtlGenRandom(value, 8)

            Return value

        End Function

        Public Shared Function GenRandomGuid() As Guid

            Dim value As Guid

            UnsafeNativeMethods.RtlGenRandom(value, 16)

            Return value

        End Function

        ''' <summary>
        ''' Returns current system handle table.
        ''' </summary>
        Public Shared Function GetSystemHandleTable() As SystemHandleTableEntryInformation()

            Using buffer As New HGlobalBuffer(65536)

                Do
                    Dim status = UnsafeNativeMethods.NtQuerySystemInformation(
                        SystemInformationClass.SystemHandleInformation,
                        buffer,
                        CInt(buffer.ByteLength),
                        Nothing)

                    If status = NativeConstants.STATUS_INFO_LENGTH_MISMATCH Then
                        buffer.Resize(CType(buffer.ByteLength << 1, IntPtr))
                        Continue Do
                    End If

                    NtDllTry(status)

                    Exit Do
                Loop

                Dim handlecount = buffer.Read(Of Integer)(0)
                Dim arrayoffset = IntPtr.Size
                Dim array(0 To handlecount - 1) As SystemHandleTableEntryInformation
                buffer.ReadArray(CULng(arrayoffset), array, 0, handlecount)

                Return array

            End Using

        End Function

        Public NotInheritable Class HandleTableEntryInformation

            Public ReadOnly Property HandleTableEntry As SystemHandleTableEntryInformation

            Public ReadOnly Property ObjectType As String

            Public ReadOnly Property ObjectName As String
            Public ReadOnly Property ProcessName As String
            Public ReadOnly Property ProcessStartTime As Date
            Public ReadOnly Property SessionId As Integer

            Friend Sub New(<[In]> ByRef HandleTableEntry As SystemHandleTableEntryInformation,
                                     ObjectType As String,
                                     ObjectName As String,
                                     Process As Process)

                _HandleTableEntry = HandleTableEntry
                _ObjectType = ObjectType
                _ObjectName = ObjectName
                _ProcessName = Process.ProcessName
                _ProcessStartTime = Process.StartTime
                _SessionId = Process.SessionId
            End Sub

        End Class

        ''' <summary>
        ''' System uptime
        ''' </summary>
        ''' <returns>Time elapsed since system startup</returns>
        Public Shared ReadOnly Property SystemUptime As TimeSpan
            Get
                Return TimeSpan.FromMilliseconds(SafeNativeMethods.GetTickCount64())
            End Get
        End Property

        Public Shared ReadOnly Property LastObjectNameQuueryTime As Long

        Public Shared ReadOnly Property LastObjectNameQueryGrantedAccess As UInteger

        ''' <summary>
        ''' Enumerates open handles in the system.
        ''' </summary>
        ''' <param name="filterObjectType">Name of object types to return in the enumeration. Normally set to for example "File" to return file handles or "Key" to return registry key handles</param>
        ''' <returns>Enumeration with information about each handle table entry</returns>
        Public Shared Function EnumerateHandleTableHandleInformation(filterObjectType As String) As IEnumerable(Of HandleTableEntryInformation)

            Return EnumerateHandleTableHandleInformation(GetSystemHandleTable(), filterObjectType)

        End Function

        Private Shared ReadOnly _objectTypes As New ConcurrentDictionary(Of Byte, String)

        Private Shared Iterator Function EnumerateHandleTableHandleInformation(handleTable As IEnumerable(Of SystemHandleTableEntryInformation), filterObjectType As String) As IEnumerable(Of HandleTableEntryInformation)

            handleTable.NullCheck(NameOf(handleTable))

            If filterObjectType IsNot Nothing Then
                filterObjectType = String.Intern(filterObjectType)
            End If

            Using buffer As New HGlobalBuffer(65536),
                processHandleList = New DisposableDictionary(Of Integer, SafeFileHandle),
                processInfoList = New DisposableDictionary(Of Integer, Process)

                Array.ForEach(Process.GetProcesses(),
                              Sub(p) processInfoList.Add(p.Id, p))

                For Each handle In handleTable

                    Dim object_type As String = Nothing
                    Dim object_name As String = Nothing
                    Dim processInfo As Process = Nothing

                    If handle.ProcessId = 0 OrElse
                        (filterObjectType IsNot Nothing AndAlso
                        _objectTypes.TryGetValue(handle.ObjectType, object_type) AndAlso
                        Not ReferenceEquals(object_type, filterObjectType)) OrElse
                        Not processInfoList.TryGetValue(handle.ProcessId, processInfo) Then

                        Continue For
                    End If

                    Dim processHandle As SafeFileHandle = Nothing
                    If Not processHandleList.TryGetValue(handle.ProcessId, processHandle) Then
                        processHandle = UnsafeNativeMethods.OpenProcess(NativeConstants.PROCESS_DUP_HANDLE Or NativeConstants.PROCESS_QUERY_LIMITED_INFORMATION, False, handle.ProcessId)
                        If processHandle.IsInvalid Then
                            processHandle = Nothing
                        End If
                        processHandleList.Add(handle.ProcessId, processHandle)
                    End If
                    If processHandle Is Nothing Then
                        Continue For
                    End If

                    Dim duphandle As New SafeFileHandle(Nothing, True)
                    Dim status = UnsafeNativeMethods.NtDuplicateObject(processHandle, New IntPtr(handle.Handle), UnsafeNativeMethods.GetCurrentProcess(), duphandle, 0, 0, 0)
                    If status < 0 Then
                        Continue For
                    End If

                    Try
                        Dim newbuffersize As Integer

                        If object_type Is Nothing Then

                            object_type = _objectTypes.GetOrAdd(
                                handle.ObjectType,
                                Function()
                                    Do
                                        Dim rc = UnsafeNativeMethods.NtQueryObject(duphandle, ObjectInformationClass.ObjectTypeInformation, buffer, CInt(buffer.ByteLength), newbuffersize)
                                        If rc = NativeConstants.STATUS_BUFFER_TOO_SMALL OrElse rc = NativeConstants.STATUS_BUFFER_OVERFLOW Then
                                            buffer.Resize(newbuffersize)
                                            Continue Do
                                        ElseIf rc < 0 Then
                                            Return Nothing
                                        End If
                                        Exit Do
                                    Loop

                                    Return String.Intern(buffer.Read(Of UNICODE_STRING)(0).ToString())
                                End Function)

                        End If

                        If object_type Is Nothing OrElse (filterObjectType IsNot Nothing AndAlso
                            Not ReferenceEquals(filterObjectType, object_type)) Then

                            Continue For
                        End If

                        If handle.GrantedAccess <> &H12019F AndAlso
                            handle.GrantedAccess <> &H12008D AndAlso
                            handle.GrantedAccess <> &H120189 AndAlso
                            handle.GrantedAccess <> &H16019F AndAlso
                            handle.GrantedAccess <> &H1A0089 AndAlso
                            handle.GrantedAccess <> &H1A019F AndAlso
                            handle.GrantedAccess <> &H120089 AndAlso
                            handle.GrantedAccess <> &H100000 Then

                            Do
                                _LastObjectNameQueryGrantedAccess = handle.GrantedAccess
                                _LastObjectNameQuueryTime = SafeNativeMethods.GetTickCount64()

                                status = UnsafeNativeMethods.NtQueryObject(duphandle, ObjectInformationClass.ObjectNameInformation, buffer, CInt(buffer.ByteLength), newbuffersize)

                                _LastObjectNameQuueryTime = 0

                                If status < 0 AndAlso newbuffersize > buffer.ByteLength Then
                                    buffer.Resize(newbuffersize)
                                    Continue Do
                                ElseIf status < 0 Then
                                    Continue For
                                End If
                                Exit Do
                            Loop

                            Dim name = buffer.Read(Of UNICODE_STRING)(0)

                            If name.Length = 0 Then
                                Continue For
                            End If

                            object_name = name.ToString()
                        End If

                    Catch

                    Finally
                        duphandle.Dispose()

                    End Try

                    Yield New HandleTableEntryInformation(handle, object_type, object_name, processInfo)
                Next
            End Using

        End Function

        Public Shared Function EnumerateProcessesHoldingFileHandle(ParamArray nativeFullPaths As String()) As IEnumerable(Of Integer)

            Dim paths = Array.ConvertAll(nativeFullPaths, Function(path) New With {path, .dir_path = String.Concat(path, "\")})

            Return _
                From handle In EnumerateHandleTableHandleInformation("File")
                Where
                    Not String.IsNullOrWhiteSpace(handle.ObjectName) AndAlso
                    paths.Any(Function(path) handle.ObjectName.Equals(path.path, StringComparison.OrdinalIgnoreCase) OrElse
                        handle.ObjectName.StartsWith(path.dir_path, StringComparison.OrdinalIgnoreCase))
                Select handle.HandleTableEntry.ProcessId
                Distinct

        End Function

        Public Shared Function FormatProcessName(processId As Integer) As String
            Try
                Using ps = Process.GetProcessById(processId)
                    If ps.SessionId = 0 OrElse String.IsNullOrWhiteSpace(ps.MainWindowTitle) Then
                        Return $"'{ps.ProcessName}' (id={processId})"
                    Else
                        Return $"'{ps.MainWindowTitle}' (id={processId})"
                    End If
                End Using

            Catch
                Return $"id={processId}"

            End Try
        End Function

        ''' <summary>
        ''' Sends an IOCTL control request to a device driver, or an FSCTL control request to a filesystem driver.
        ''' </summary>
        ''' <param name="device">Open handle to filer or device.</param>
        ''' <param name="ctrlcode">IOCTL or FSCTL control code.</param>
        ''' <param name="data">Optional function to create input data for the control function.</param>
        ''' <param name="outdatasize">Number of bytes returned in output buffer by driver.</param>
        ''' <returns>This method returns a byte array that can be used to read and parse data returned by
        ''' driver in the output buffer.</returns>
        Public Shared Function DeviceIoControl(device As SafeFileHandle,
                                               ctrlcode As UInt32,
                                               data As Byte(),
                                               <Out> ByRef outdatasize As UInt32) As Byte()

            Dim indatasize = If(data Is Nothing, 0UI, CUInt(data.Length))

            If outdatasize > indatasize Then
                Array.Resize(data, CInt(outdatasize))
            End If

            Dim rc =
              UnsafeNativeMethods.DeviceIoControl(device,
                                        ctrlcode,
                                        data,
                                        indatasize,
                                        data,
                                        If(data Is Nothing, 0UI, CUInt(data.Length)),
                                        outdatasize,
                                        IntPtr.Zero)

            If Not rc Then
                Throw New Win32Exception
            End If

            Array.Resize(data, CInt(outdatasize))

            Return data

        End Function

        Public Shared Function ConvertManagedFileAccess(DesiredAccess As FileAccess) As FileSystemRights

            Dim NativeDesiredAccess = FileSystemRights.ReadAttributes

            If DesiredAccess.HasFlag(FileAccess.Read) Then
                NativeDesiredAccess = NativeDesiredAccess Or FileSystemRights.Read
            End If
            If DesiredAccess.HasFlag(FileAccess.Write) Then
                NativeDesiredAccess = NativeDesiredAccess Or FileSystemRights.Write
            End If

            Return NativeDesiredAccess

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="SecurityAttributes"></param>
        ''' <param name="FlagsAndAttributes"></param>
        ''' <param name="TemplateFile"></param>
        Public Shared Function CreateFile(
              FileName As String,
              DesiredAccess As FileSystemRights,
              ShareMode As FileShare,
              SecurityAttributes As IntPtr,
              CreationDisposition As UInt32,
              FlagsAndAttributes As Int32,
              TemplateFile As IntPtr) As SafeFileHandle

            Dim handle = UnsafeNativeMethods.CreateFile(FileName, DesiredAccess, ShareMode, SecurityAttributes, CreationDisposition, FlagsAndAttributes, TemplateFile)

            If handle.IsInvalid Then
                Throw New IOException($"Cannot open {FileName}", New Win32Exception)
            End If

            Return handle

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
        Public Shared Function OpenFileHandle(
          FileName As String,
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          CreationDisposition As FileMode,
          Overlapped As Boolean) As SafeFileHandle

            If String.IsNullOrEmpty(FileName) Then
                Throw New ArgumentNullException(NameOf(FileName))
            End If

            Dim NativeDesiredAccess = ConvertManagedFileAccess(DesiredAccess)

            Dim NativeCreationDisposition As UInt32
            Select Case CreationDisposition
                Case FileMode.Create
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS
                Case FileMode.CreateNew
                    NativeCreationDisposition = NativeConstants.CREATE_NEW
                Case FileMode.Open
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING
                Case FileMode.OpenOrCreate
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS
                Case FileMode.Truncate
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING
                Case Else
                    Throw New NotImplementedException
            End Select

            Dim NativeFlagsAndAttributes = FileAttributes.Normal
            If Overlapped Then
                NativeFlagsAndAttributes = NativeFlagsAndAttributes Or CType(NativeConstants.FILE_FLAG_OVERLAPPED, FileAttributes)
            End If

            Dim Handle = UnsafeNativeMethods.CreateFile(FileName,
                                             NativeDesiredAccess,
                                             ShareMode,
                                             IntPtr.Zero,
                                             NativeCreationDisposition,
                                             NativeFlagsAndAttributes,
                                             IntPtr.Zero)

            If Handle.IsInvalid Then
                Throw New IOException($"Cannot open {FileName}", New Win32Exception)
            End If

            Return Handle

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="Options">Specifies whether to request overlapped I/O.</param>
        Public Shared Function OpenFileHandle(
          FileName As String,
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          CreationDisposition As FileMode,
          Options As FileOptions) As SafeFileHandle

            Return OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, CUInt(Options))

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="Options">Specifies whether to request overlapped I/O.</param>
        Public Shared Function OpenFileHandle(
          FileName As String,
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          CreationDisposition As FileMode,
          Options As UInt32) As SafeFileHandle

            If String.IsNullOrEmpty(FileName) Then
                Throw New ArgumentNullException(NameOf(FileName))
            End If

            Dim NativeDesiredAccess = ConvertManagedFileAccess(DesiredAccess)

            Dim NativeCreationDisposition As UInt32
            Select Case CreationDisposition
                Case FileMode.Create
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS
                Case FileMode.CreateNew
                    NativeCreationDisposition = NativeConstants.CREATE_NEW
                Case FileMode.Open
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING
                Case FileMode.OpenOrCreate
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS
                Case FileMode.Truncate
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING
                Case Else
                    Throw New NotImplementedException
            End Select

            Dim NativeFlagsAndAttributes = FileAttributes.Normal

            NativeFlagsAndAttributes = NativeFlagsAndAttributes Or CType(Options, FileAttributes)

            Dim Handle = UnsafeNativeMethods.CreateFile(FileName,
                                             NativeDesiredAccess,
                                             ShareMode,
                                             IntPtr.Zero,
                                             NativeCreationDisposition,
                                             NativeFlagsAndAttributes,
                                             IntPtr.Zero)
            If Handle.IsInvalid Then
                Throw New IOException($"Cannot open {FileName}", New Win32Exception)
            End If

            Return Handle
        End Function

        ''' <summary>
        ''' Calls NT API NtCreateFile() function and encapsulates returned handle in a SafeFileHandle object.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationOption">Specifies whether to request overlapped I/O.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="FileAttributes">Attributes for created file.</param>
        ''' <param name="ObjectAttributes">Object attributes.</param>
        ''' <param name="RootDirectory">Root directory to start path parsing from, or null for rooted path.</param>
        ''' <param name="WasCreated">Return information about whether a file was created, existing file opened etc.</param>
        ''' <returns>NTSTATUS value indicating result of the operation.</returns>
        Public Shared Function NtCreateFile(FileName As String,
                                            ObjectAttributes As NtObjectAttributes,
                                            DesiredAccess As FileAccess,
                                            ShareMode As FileShare,
                                            CreationDisposition As NtCreateDisposition,
                                            CreationOption As NtCreateOptions,
                                            FileAttributes As FileAttributes,
                                            RootDirectory As SafeFileHandle,
                                            <Out> ByRef WasCreated As NtFileCreated) As SafeFileHandle

            If String.IsNullOrEmpty(FileName) Then
                Throw New ArgumentNullException(NameOf(FileName))
            End If

            Dim native_desired_access = ConvertManagedFileAccess(DesiredAccess) Or FileSystemRights.Synchronize

            Dim handle_value As SafeFileHandle = Nothing

            Using pinned_name_string As New PinnedString(FileName), unicode_string_name = PinnedBuffer.Serialize(pinned_name_string.UnicodeString)

                Dim object_attributes As New ObjectAttributes(If(RootDirectory?.DangerousGetHandle(), IntPtr.Zero), unicode_string_name.DangerousGetHandle(), ObjectAttributes, Nothing, Nothing)

                Dim io_status_block As IoStatusBlock

                Dim status = UnsafeNativeMethods.NtCreateFile(handle_value,
                                                              native_desired_access,
                                                              object_attributes,
                                                              io_status_block,
                                                              0,
                                                              FileAttributes,
                                                              ShareMode,
                                                              CreationDisposition,
                                                              CreationOption,
                                                              Nothing,
                                                              0)

                WasCreated = CType(io_status_block.Information, NtFileCreated)

                If status < 0 Then
                    Throw GetExceptionForNtStatus(status)
                End If

            End Using

            Return handle_value

        End Function

        Public Enum NtFileCreated

            Superseded
            Opened
            Created
            Overwritten
            Exists
            DoesNotExist

        End Enum

        ''' <summary>
        ''' Calls NT API NtOpenEvent() function to open an event object using NT path and encapsulates returned handle in a SafeWaitHandle object.
        ''' </summary>
        ''' <param name="EventName">Name of event to open.</param>
        ''' <param name="DesiredAccess">Access to request.</param>
        ''' <param name="ObjectAttributes">Object attributes.</param>
        ''' <param name="RootDirectory">Root directory to start path parsing from, or null for rooted path.</param>
        ''' <returns>NTSTATUS value indicating result of the operation.</returns>
        Public Shared Function NtOpenEvent(EventName As String,
                                           ObjectAttributes As NtObjectAttributes,
                                           DesiredAccess As UInt32,
                                           RootDirectory As SafeFileHandle) As SafeWaitHandle

            If String.IsNullOrEmpty(EventName) Then
                Throw New ArgumentNullException(NameOf(EventName))
            End If

            Dim handle_value As SafeWaitHandle = Nothing

            Using pinned_name_string As New PinnedString(EventName), unicode_string_name = PinnedBuffer.Serialize(pinned_name_string.UnicodeString)

                Dim object_attributes As New ObjectAttributes(If(RootDirectory?.DangerousGetHandle(), IntPtr.Zero), unicode_string_name.DangerousGetHandle(), ObjectAttributes, Nothing, Nothing)

                Dim status = UnsafeNativeMethods.NtOpenEvent(handle_value,
                                                             DesiredAccess,
                                                             object_attributes)

                If status < 0 Then
                    Throw GetExceptionForNtStatus(status)
                End If

            End Using

            Return handle_value

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function to create a backup handle for a file or
        ''' directory and encapsulates returned handle in a SafeFileHandle object. This
        ''' handle can later be used in calls to Win32 Backup API functions or similar.
        ''' </summary>
        ''' <param name="FilePath">Name of file or directory to open.</param>
        ''' <param name="DesiredAccess">Access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        Public Shared Function OpenBackupHandle(
          FilePath As String,
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          CreationDisposition As FileMode) As SafeFileHandle

            If String.IsNullOrEmpty(FilePath) Then
                Throw New ArgumentNullException(NameOf(FilePath))
            End If

            Dim NativeDesiredAccess = FileSystemRights.ReadAttributes
            If DesiredAccess.HasFlag(FileAccess.Read) Then
                NativeDesiredAccess = NativeDesiredAccess Or FileSystemRights.Read
            End If
            If DesiredAccess.HasFlag(FileAccess.Write) Then
                NativeDesiredAccess = NativeDesiredAccess Or FileSystemRights.Write
            End If

            Dim NativeCreationDisposition As UInt32
            Select Case CreationDisposition
                Case FileMode.Create
                    NativeCreationDisposition = NativeConstants.CREATE_ALWAYS
                Case FileMode.CreateNew
                    NativeCreationDisposition = NativeConstants.CREATE_NEW
                Case FileMode.Open
                    NativeCreationDisposition = NativeConstants.OPEN_EXISTING
                Case FileMode.OpenOrCreate
                    NativeCreationDisposition = NativeConstants.OPEN_ALWAYS
                Case FileMode.Truncate
                    NativeCreationDisposition = NativeConstants.TRUNCATE_EXISTING
                Case Else
                    Throw New NotImplementedException
            End Select

            Dim NativeFlagsAndAttributes = CType(NativeConstants.FILE_FLAG_BACKUP_SEMANTICS, FileAttributes)

            Dim Handle =
                UnsafeNativeMethods.CreateFile(FilePath,
                                               NativeDesiredAccess,
                                               ShareMode,
                                               IntPtr.Zero,
                                               NativeCreationDisposition,
                                               NativeFlagsAndAttributes,
                                               IntPtr.Zero)

            If Handle.IsInvalid Then
                Throw New IOException($"Cannot open {FilePath}", New Win32Exception)
            End If

            Return Handle
        End Function

#If NETFRAMEWORK AndAlso Not NET462_OR_GREATER Then
        ''' <summary>
        ''' Converts FileAccess flags to values legal in constructor call to FileStream class.
        ''' </summary>
        ''' <param name="Value">FileAccess values.</param>
        Private Shared Function GetFileStreamLegalAccessValue(Value As FileAccess) As FileAccess
            If Value = 0 Then
                Return FileAccess.Read
            Else
                Return Value
            End If
        End Function
#End If

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        Public Shared Function OpenFileStream(
          FileName As String,
          CreationDisposition As FileMode,
          DesiredAccess As FileAccess,
          ShareMode As FileShare) As FileStream

#If NETFRAMEWORK AndAlso Not NET462_OR_GREATER Then
            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped:=False), GetFileStreamLegalAccessValue(DesiredAccess))
#Else
            Return New FileStream(FileName, CreationDisposition, DesiredAccess, ShareMode)
#End If

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="BufferSize">Buffer size to specify in constructor call to FileStream class.</param>
        Public Shared Function OpenFileStream(
          FileName As String,
          CreationDisposition As FileMode,
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          BufferSize As Integer) As FileStream

#If NETFRAMEWORK AndAlso Not NET462_OR_GREATER Then
            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped:=False), GetFileStreamLegalAccessValue(DesiredAccess), BufferSize)
#Else
            Return New FileStream(FileName, CreationDisposition, DesiredAccess, ShareMode, BufferSize)
#End If

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="BufferSize">Buffer size to specify in constructor call to FileStream class.</param>
        ''' <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
        Public Shared Function OpenFileStream(
      FileName As String,
      CreationDisposition As FileMode,
      DesiredAccess As FileAccess,
      ShareMode As FileShare,
      BufferSize As Integer,
      Overlapped As Boolean) As FileStream

#If NETFRAMEWORK AndAlso Not NET462_OR_GREATER Then
            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped), GetFileStreamLegalAccessValue(DesiredAccess), BufferSize, Overlapped)
#Else
            Return New FileStream(FileName, CreationDisposition, DesiredAccess, ShareMode, BufferSize, Overlapped)
#End If

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
        Public Shared Function OpenFileStream(FileName As String,
                                              CreationDisposition As FileMode,
                                              DesiredAccess As FileAccess,
                                              ShareMode As FileShare,
                                              Overlapped As Boolean) As FileStream

#If NETFRAMEWORK AndAlso Not NET462_OR_GREATER Then
            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped), GetFileStreamLegalAccessValue(DesiredAccess), 1, Overlapped)
#Else
            Return New FileStream(FileName, CreationDisposition, DesiredAccess, ShareMode, 1, Overlapped)
#End If

        End Function

        Private Shared Sub SetFileCompressionState(SafeFileHandle As SafeFileHandle, State As UShort)

            Dim pinptr = GCHandle.Alloc(State, GCHandleType.Pinned)
            Try
                Win32Try(UnsafeNativeMethods.DeviceIoControl(SafeFileHandle,
                                              NativeConstants.FSCTL_SET_COMPRESSION,
                                              pinptr.AddrOfPinnedObject(),
                                              2UI,
                                              IntPtr.Zero,
                                              0UI,
                                              Nothing,
                                              IntPtr.Zero))

            Finally
                pinptr.Free()

            End Try

        End Sub

        Public Shared Function GetFileSize(Filename As String) As Int64

            Using safefilehandle = OpenFileHandle(Filename, 0, FileShare.ReadWrite Or FileShare.Delete, FileMode.Open, FileOptions.None)

                Return GetFileSize(safefilehandle)

            End Using

        End Function

        Public Shared Function GetFileSize(SafeFileHandle As SafeFileHandle) As Int64

            Dim FileSize As Int64

            Win32Try(UnsafeNativeMethods.GetFileSize(SafeFileHandle, FileSize))

            Return FileSize

        End Function

        Public Shared Function GetDiskSize(SafeFileHandle As SafeFileHandle) As Int64?

            Dim FileSize As Int64

            If UnsafeNativeMethods.DeviceIoControl(SafeFileHandle, NativeConstants.IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0UI, FileSize, CUInt(Marshal.SizeOf(FileSize)), 0UI, IntPtr.Zero) Then

                Return FileSize

            Else

                Return Nothing

            End If

        End Function

        Public Shared Function GetVolumeSizeInformation(SafeFileHandle As SafeFileHandle) As FILE_FS_FULL_SIZE_INFORMATION?

            Using buffer As New PinnedBuffer(Of FILE_FS_FULL_SIZE_INFORMATION)(1)

                Dim io_status_block As New IoStatusBlock

                Dim status = UnsafeNativeMethods.NtQueryVolumeInformationFile(SafeFileHandle, io_status_block, buffer, CInt(buffer.ByteLength), FsInformationClass.FileFsFullSizeInformation)

                If status < 0 Then
                    Return Nothing
                End If

                Return buffer.Read(Of FILE_FS_FULL_SIZE_INFORMATION)(0)

            End Using

        End Function

        Public Shared Function IsDiskWritable(SafeFileHandle As SafeFileHandle) As Boolean

            Dim rc = UnsafeNativeMethods.DeviceIoControl(SafeFileHandle, NativeConstants.IOCTL_DISK_IS_WRITABLE, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, 0UI, IntPtr.Zero)
            If rc Then
                Return True
            Else
                Dim err = Marshal.GetLastWin32Error()

                Select Case err

                    Case NativeConstants.ERROR_WRITE_PROTECT,
                         NativeConstants.ERROR_NOT_READY,
                         NativeConstants.FVE_E_LOCKED_VOLUME

                        Return False

                    Case Else
                        Throw New Win32Exception(err)

                End Select

            End If

        End Function

        Public Shared Sub GrowPartition(DiskHandle As SafeFileHandle, PartitionNumber As Integer, BytesToGrow As Int64)

            Dim DiskGrowPartition As DISK_GROW_PARTITION
            DiskGrowPartition.PartitionNumber = PartitionNumber
            DiskGrowPartition.BytesToGrow = BytesToGrow
            Win32Try(UnsafeNativeMethods.DeviceIoControl(DiskHandle, NativeConstants.IOCTL_DISK_GROW_PARTITION, DiskGrowPartition, CUInt(PinnedBuffer(Of DISK_GROW_PARTITION).TypeSize), IntPtr.Zero, 0UI, 0UI, IntPtr.Zero))

        End Sub

        Public Shared Sub CompressFile(SafeFileHandle As SafeFileHandle)

            SetFileCompressionState(SafeFileHandle, NativeConstants.COMPRESSION_FORMAT_DEFAULT)

        End Sub

        Public Shared Sub UncompressFile(SafeFileHandle As SafeFileHandle)

            SetFileCompressionState(SafeFileHandle, NativeConstants.COMPRESSION_FORMAT_NONE)

        End Sub

        Public Shared Sub AllowExtendedDASDIO(SafeFileHandle As SafeFileHandle)

            Win32Try(UnsafeNativeMethods.DeviceIoControl(SafeFileHandle, NativeConstants.FSCTL_ALLOW_EXTENDED_DASD_IO, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, 0UI, IntPtr.Zero))

        End Sub

        Public Shared Function GetLongFullPath(path As String) As String

            path = GetNtPath(path)

            If path.StartsWith("\??\", StringComparison.Ordinal) Then
                path = $"\\?\{path.Substring(4)}"
            End If

            Return path

        End Function

        ''' <summary>
        ''' Adds a semicolon separated list of paths to the PATH environment variable of
        ''' current process. Any paths already in present PATH variable are not added again.
        ''' </summary>
        ''' <param name="AddPaths">Semicolon separated list of directory paths</param>
        ''' <param name="BeforeExisting">Indicates whether to insert new paths before existing path list or move
        ''' existing of specified paths first if True, or add new paths after existing path list if False.</param>
        Public Shared Sub AddProcessPaths(BeforeExisting As Boolean, AddPaths As String)

            If String.IsNullOrEmpty(AddPaths) Then
                Return
            End If

            Dim AddPathsArray = AddPaths.Split({";"c}, StringSplitOptions.RemoveEmptyEntries)

            AddProcessPaths(BeforeExisting, AddPathsArray)

        End Sub

        ''' <summary>
        ''' Adds a list of paths to the PATH environment variable of current process. Any
        ''' paths already in present PATH variable are not added again.
        ''' </summary>
        ''' <param name="AddPathsArray">Array of directory paths</param>
        ''' <param name="BeforeExisting">Indicates whether to insert new paths before existing path list or move
        ''' existing of specified paths first if True, or add new paths after existing path list if False.</param>
        Public Shared Sub AddProcessPaths(BeforeExisting As Boolean, ParamArray AddPathsArray As String())

            If AddPathsArray Is Nothing OrElse AddPathsArray.Length = 0 Then
                Return
            End If

            Dim Paths As New List(Of String)(Environment.GetEnvironmentVariable("PATH").Split({";"c}, StringSplitOptions.RemoveEmptyEntries))

            If BeforeExisting Then
                For Each AddPath In AddPathsArray
                    If Paths.BinarySearch(AddPath, StringComparer.CurrentCultureIgnoreCase) >= 0 Then
                        Paths.Remove(AddPath)
                    End If
                Next
                Paths.InsertRange(0, AddPathsArray)
            Else
                For Each AddPath In AddPathsArray
                    If Paths.BinarySearch(AddPath, StringComparer.CurrentCultureIgnoreCase) < 0 Then
                        Paths.Add(AddPath)
                    End If
                Next
            End If

            Environment.SetEnvironmentVariable("PATH", String.Join(";", Paths.ToArray()))
        End Sub

        ''' <summary>
        ''' Locks and dismounts filesystem on a volume. Upon successful return, further access to the device
        ''' can only be done through the handle passed to this function until handle is closed or lock is
        ''' released.
        ''' </summary>
        ''' <param name="Device">Handle to device to lock and dismount.</param>
        ''' <param name="Force">Indicates if True that volume should be immediately dismounted even if it
        ''' cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
        ''' successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
        Public Shared Function DismountVolumeFilesystem(Device As SafeFileHandle, Force As Boolean) As Boolean

            Dim lock_result As Boolean

            For i = 0 To 10

                If i > 0 Then

                    Trace.WriteLine("Error locking volume, retrying...")

                End If

                UnsafeNativeMethods.FlushFileBuffers(Device)

                Thread.Sleep(300)

                lock_result = UnsafeNativeMethods.DeviceIoControl(Device, NativeConstants.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, Nothing, Nothing)
                If lock_result OrElse Marshal.GetLastWin32Error() <> NativeConstants.ERROR_ACCESS_DENIED Then
                    Exit For
                End If

            Next

            If Not lock_result AndAlso Not Force Then
                Return False
            End If

            Return UnsafeNativeMethods.DeviceIoControl(Device, NativeConstants.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, Nothing, Nothing)

        End Function

#If NET45_OR_GREATER OrElse NETCOREAPP OrElse NETSTANDARD Then
        ''' <summary>
        ''' Locks and dismounts filesystem on a volume. Upon successful return, further access to the device
        ''' can only be done through the handle passed to this function until handle is closed or lock is
        ''' released.
        ''' </summary>
        ''' <param name="Device">Handle to device to lock and dismount.</param>
        ''' <param name="Force">Indicates if True that volume should be immediately dismounted even if it
        ''' cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
        ''' successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
        Public Shared Async Function DismountVolumeFilesystemAsync(Device As SafeFileHandle, Force As Boolean, cancel As CancellationToken) As Task(Of Boolean)

            Dim lock_result As Boolean

            For i = 0 To 10

                If i > 0 Then

                    Trace.WriteLine("Error locking volume, retrying...")

                End If

                UnsafeNativeMethods.FlushFileBuffers(Device)

                Await Task.Delay(300, cancel).ConfigureAwait(continueOnCapturedContext:=False)

                lock_result = UnsafeNativeMethods.DeviceIoControl(Device, NativeConstants.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, Nothing, Nothing)
                If lock_result OrElse Marshal.GetLastWin32Error() <> NativeConstants.ERROR_ACCESS_DENIED Then
                    Exit For
                End If

            Next

            If Not lock_result AndAlso Not Force Then
                Return False
            End If

            Return UnsafeNativeMethods.DeviceIoControl(Device, NativeConstants.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, Nothing, Nothing)

        End Function
#End If

        ''' <summary>
        ''' Retrieves disk geometry.
        ''' </summary>
        ''' <param name="hDevice">Handle to device.</param>
        Public Shared Function GetDiskGeometry(hDevice As SafeFileHandle) As DISK_GEOMETRY?

            Dim DiskGeometry As DISK_GEOMETRY

            If UnsafeNativeMethods.DeviceIoControl(hDevice, NativeConstants.IOCTL_DISK_GET_DRIVE_GEOMETRY, IntPtr.Zero, 0, DiskGeometry, CUInt(PinnedBuffer(Of DISK_GEOMETRY).TypeSize), Nothing, Nothing) Then

                Return DiskGeometry

            Else

                Return Nothing

            End If

        End Function

        ''' <summary>
        ''' Retrieves SCSI address.
        ''' </summary>
        ''' <param name="hDevice">Handle to device.</param>
        Public Shared Function GetScsiAddress(hDevice As SafeFileHandle) As SCSI_ADDRESS?

            Dim ScsiAddress As SCSI_ADDRESS

            If UnsafeNativeMethods.DeviceIoControl(hDevice, NativeConstants.IOCTL_SCSI_GET_ADDRESS, IntPtr.Zero, 0, ScsiAddress, CUInt(PinnedBuffer(Of SCSI_ADDRESS).TypeSize), Nothing, Nothing) Then
                Return ScsiAddress
            Else
                Return Nothing
            End If

        End Function

        ''' <summary>
        ''' Retrieves SCSI address.
        ''' </summary>
        ''' <param name="Device">Path to device.</param>
        Public Shared Function GetScsiAddress(Device As String) As SCSI_ADDRESS?

            Using hDevice = OpenFileHandle(Device, 0, FileShare.ReadWrite, FileMode.Open, False)

                Return GetScsiAddress(hDevice)

            End Using

        End Function

        ''' <summary>
        ''' Retrieves status of write overlay for mounted device.
        ''' </summary>
        ''' <param name="NtDevicePath">Path to device.</param>
        Public Shared Function GetScsiAddressForNtDevice(NtDevicePath As String) As SCSI_ADDRESS?

            Try
                Using hDevice = NtCreateFile(NtDevicePath, 0, 0, FileShare.ReadWrite, NtCreateDisposition.Open, NtCreateOptions.NonDirectoryFile, 0, Nothing, Nothing)

                    Return GetScsiAddress(hDevice)

                End Using

            Catch ex As Exception
                Trace.WriteLine($"Error getting SCSI address for device '{NtDevicePath}': {ex.JoinMessages()}")
                Return Nothing

            End Try

        End Function

        ''' <summary>
        ''' Retrieves storage standard properties.
        ''' </summary>
        ''' <param name="hDevice">Handle to device.</param>
        Public Shared Function GetStorageStandardProperties(hDevice As SafeFileHandle) As StorageStandardProperties?

            Dim StoragePropertyQuery As New STORAGE_PROPERTY_QUERY(STORAGE_PROPERTY_ID.StorageDeviceProperty, STORAGE_QUERY_TYPE.PropertyStandardQuery)
            Dim StorageDescriptorHeader As New STORAGE_DESCRIPTOR_HEADER

            If Not UnsafeNativeMethods.DeviceIoControl(hDevice, NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY,
                                                   StoragePropertyQuery, CUInt(PinnedBuffer(Of STORAGE_PROPERTY_QUERY).TypeSize),
                                                   StorageDescriptorHeader, CUInt(PinnedBuffer(Of STORAGE_DESCRIPTOR_HEADER).TypeSize),
                                                   Nothing, Nothing) Then
                Return Nothing
            End If

            Using buffer As New PinnedBuffer(Of Byte)(New Byte(0 To CInt(StorageDescriptorHeader.Size - 1)) {})

                If Not UnsafeNativeMethods.DeviceIoControl(hDevice, NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY,
                                                   StoragePropertyQuery, CUInt(PinnedBuffer(Of STORAGE_PROPERTY_QUERY).TypeSize),
                                                   buffer, CUInt(buffer.ByteLength),
                                                   Nothing, Nothing) Then
                    Return Nothing
                End If

                Return New StorageStandardProperties(buffer)

            End Using

        End Function

        ''' <summary>
        ''' Retrieves storage TRIM properties.
        ''' </summary>
        ''' <param name="hDevice">Handle to device.</param>
        Public Shared Function GetStorageTrimProperties(hDevice As SafeFileHandle) As Boolean?

            Dim StoragePropertyQuery As New STORAGE_PROPERTY_QUERY(STORAGE_PROPERTY_ID.StorageDeviceTrimProperty, STORAGE_QUERY_TYPE.PropertyStandardQuery)
            Dim DeviceTrimDescriptor As New DEVICE_TRIM_DESCRIPTOR

            If Not UnsafeNativeMethods.DeviceIoControl(hDevice, NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY,
                                               StoragePropertyQuery, CUInt(PinnedBuffer(Of STORAGE_PROPERTY_QUERY).TypeSize),
                                               DeviceTrimDescriptor, CUInt(PinnedBuffer(Of DEVICE_TRIM_DESCRIPTOR).TypeSize),
                                               Nothing, Nothing) Then
                Return Nothing
            End If

            Return DeviceTrimDescriptor.TrimEnabled <> 0

        End Function

        ''' <summary>
        ''' Retrieves storage device number.
        ''' </summary>
        ''' <param name="hDevice">Handle to device.</param>
        Public Shared Function GetStorageDeviceNumber(hDevice As SafeFileHandle) As STORAGE_DEVICE_NUMBER?

            Dim StorageDeviceNumber As STORAGE_DEVICE_NUMBER

            If UnsafeNativeMethods.DeviceIoControl(hDevice, NativeConstants.IOCTL_STORAGE_GET_DEVICE_NUMBER,
                                                   IntPtr.Zero, 0,
                                                   StorageDeviceNumber, CUInt(PinnedBuffer(Of STORAGE_DEVICE_NUMBER).TypeSize), Nothing, Nothing) Then

                Return StorageDeviceNumber
            Else
                Return Nothing
            End If

        End Function

        ''' <summary>
        ''' Retrieves PhysicalDrive or CdRom path for NT raw device path
        ''' </summary>
        ''' <param name="ntdevice">NT device path, such as \Device\00000001.</param>
        Public Shared Function GetPhysicalDriveNameForNtDevice(ntdevice As String) As String

            Using hDevice = NtCreateFile(ntdevice, 0, 0, FileShare.ReadWrite, NtCreateDisposition.Open, 0, 0, Nothing, Nothing)

                Dim devnr = GetStorageDeviceNumber(hDevice)

                If Not devnr.HasValue OrElse devnr.Value.PartitionNumber > 0 Then

                    Throw New InvalidOperationException($"Device '{ntdevice}' is not a physical disk device object")

                End If

                Select Case devnr.Value.DeviceType
                    Case DeviceType.CdRom
                        Return $"CdRom{devnr.Value.DeviceNumber}"
                    Case DeviceType.Disk
                        Return $"PhysicalDrive{devnr.Value.DeviceNumber}"
                    Case Else
                        Throw New InvalidOperationException($"Device '{ntdevice}' has unknown device type 0x{CInt(devnr.Value.DeviceType):X}")
                End Select

            End Using

        End Function

        ''' <summary>
        ''' Returns directory junction target path
        ''' </summary>
        ''' <param name="source">Location of directory that is a junction.</param>
        Public Shared Function QueryDirectoryJunction(source As String) As String

            Using hdir = OpenFileHandle(source, FileAccess.Write, FileShare.Read, FileMode.Open, NativeConstants.FILE_FLAG_BACKUP_SEMANTICS Or NativeConstants.FILE_FLAG_OPEN_REPARSE_POINT)
                Return QueryDirectoryJunction(hdir)
            End Using

        End Function

        ''' <summary>
        ''' Creates a directory junction
        ''' </summary>
        ''' <param name="source">Location of directory to convert to a junction.</param>
        ''' <param name="target">Target path for the junction.</param>
        Public Shared Sub CreateDirectoryJunction(source As String, target As String)

            Directory.CreateDirectory(source)

            Using hdir = OpenFileHandle(source, FileAccess.Write, FileShare.Read, FileMode.Open, NativeConstants.FILE_FLAG_BACKUP_SEMANTICS Or NativeConstants.FILE_FLAG_OPEN_REPARSE_POINT)
                CreateDirectoryJunction(hdir, target)
            End Using

        End Sub

        Public Shared Sub SetFileSparseFlag(file As SafeFileHandle, flag As Boolean)

            Win32Try(UnsafeNativeMethods.DeviceIoControl(file, NativeConstants.FSCTL_SET_SPARSE, flag, 1, Nothing, 0, Nothing, Nothing))

        End Sub

        ''' <summary>
        ''' Get directory junction target path
        ''' </summary>
        ''' <param name="source">Handle to directory.</param>
        Public Shared Function QueryDirectoryJunction(source As SafeFileHandle) As String

            Dim buffer(0 To 65533) As Byte

            Dim size As UInteger

            If Not UnsafeNativeMethods.DeviceIoControl(source, NativeConstants.FSCTL_GET_REPARSE_POINT, IntPtr.Zero, 0UI, buffer, CUInt(buffer.Length), size, IntPtr.Zero) Then
                Return Nothing
            End If

            Using wr As New BinaryReader(New MemoryStream(buffer, 0, CInt(size)))
                If wr.ReadUInt32() <> NativeConstants.IO_REPARSE_TAG_MOUNT_POINT Then
                    Throw New InvalidDataException("Not a mount point or junction")
                End If ' DWORD ReparseTag
                wr.ReadUInt16() ' WORD ReparseDataLength
                wr.ReadUInt16() ' WORD Reserved
                wr.ReadUInt16() ' WORD NameOffset
                Dim name_length = wr.ReadUInt16() - 2 ' WORD NameLength
                wr.ReadUInt16() ' WORD DisplayNameOffset
                wr.ReadUInt16() ' WORD DisplayNameLength
                Return Encoding.Unicode.GetString(wr.ReadBytes(name_length))

            End Using

        End Function

        ''' <summary>
        ''' Creates a directory junction
        ''' </summary>
        ''' <param name="source">Handle to directory.</param>
        ''' <param name="target">Target path for the junction.</param>
        Public Shared Sub CreateDirectoryJunction(source As SafeFileHandle, target As String)

            Dim name = Encoding.Unicode.GetBytes(target)

            Using wr As New BufferedBinaryWriter
                wr.Write(NativeConstants.IO_REPARSE_TAG_MOUNT_POINT) ' DWORD ReparseTag
                wr.Write(8S + CShort(name.Length) + 2S + CShort(name.Length) + 2S) ' WORD ReparseDataLength
                wr.Write(0S) ' WORD Reserved
                wr.Write(0S) ' WORD NameOffset
                wr.Write(CShort(name.Length)) ' WORD NameLength
                wr.Write(CShort(name.Length) + 2S) ' WORD DisplayNameOffset
                wr.Write(CShort(name.Length)) ' WORD DisplayNameLength
                wr.Write(name)
                wr.Write(New Char)
                wr.Write(name)
                wr.Write(New Char)

                Dim buffer = wr.ToArray()

                If Not UnsafeNativeMethods.DeviceIoControl(source, NativeConstants.FSCTL_SET_REPARSE_POINT, buffer, CUInt(buffer.Length), IntPtr.Zero, 0UI, 0UI, IntPtr.Zero) Then
                    Throw New Win32Exception
                End If

            End Using

        End Sub

        Public Shared Function GetProcAddress(hModule As IntPtr, procedureName As String, delegateType As Type) As [Delegate]

            Return Marshal.GetDelegateForFunctionPointer(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)), delegateType)

        End Function

        Public Shared Function GetProcAddressNoThrow(hModule As IntPtr, procedureName As String, delegateType As Type) As [Delegate]

            Dim fptr = UnsafeNativeMethods.GetProcAddress(hModule, procedureName)

            If fptr = Nothing Then
                Return Nothing
            End If

            Return Marshal.GetDelegateForFunctionPointer(fptr, delegateType)

        End Function

        Public Shared Function GetProcAddress(moduleName As String, procedureName As String, delegateType As Type) As [Delegate]

            Dim hModule = Win32Try(UnsafeNativeMethods.LoadLibrary(moduleName))

            Return Marshal.GetDelegateForFunctionPointer(Win32Try(UnsafeNativeMethods.GetProcAddress(hModule, procedureName)), delegateType)

        End Function

        Public Shared Function GetProcAddressNoThrow(moduleName As String, procedureName As String) As IntPtr

            Dim hModule = UnsafeNativeMethods.LoadLibrary(moduleName)

            If hModule = Nothing Then
                Return Nothing
            End If

            Return UnsafeNativeMethods.GetProcAddress(hModule, procedureName)

        End Function

        Public Shared Function GetProcAddressNoThrow(moduleName As String, procedureName As String, delegateType As Type) As [Delegate]

            Dim fptr = GetProcAddressNoThrow(moduleName, procedureName)

            If fptr = Nothing Then
                Return Nothing
            End If

            Return Marshal.GetDelegateForFunctionPointer(fptr, delegateType)

        End Function

        Public Shared Function QueryDosDevice() As IEnumerable(Of String)

            Return QueryDosDevice(Nothing)

        End Function

        Public Shared Function QueryDosDevice(DosDevice As String) As IEnumerable(Of String)

            Dim TargetPath(0 To 65536) As Char

            Dim length = UnsafeNativeMethods.QueryDosDevice(DosDevice, TargetPath, TargetPath.Length)

            If length < 2 Then
                Return Nothing
            End If

            Return ParseDoubleTerminatedString(TargetPath, length)

        End Function

        Public Shared Function GetNtPath(Win32Path As String) As String

            Dim unicode_string As UNICODE_STRING

            Dim RC = UnsafeNativeMethods.RtlDosPathNameToNtPathName_U(Win32Path, unicode_string, Nothing, Nothing)
            If Not RC Then
                Throw New IOException($"Invalid path: '{Win32Path}'")
            End If

            Try
                Return unicode_string.ToString()

            Finally
                UnsafeNativeMethods.RtlFreeUnicodeString(unicode_string)

            End Try

        End Function

        Public Shared Sub DeleteVolumeMountPoint(VolumeMountPoint As String)
            Win32Try(UnsafeNativeMethods.DeleteVolumeMountPoint(VolumeMountPoint))
        End Sub

        Public Shared Sub SetVolumeMountPoint(VolumeMountPoint As String, VolumeName As String)
            Win32Try(UnsafeNativeMethods.SetVolumeMountPoint(VolumeMountPoint, VolumeName))
        End Sub

        Public Shared Function FindFirstFreeDriveLetter() As Char
            Return FindFirstFreeDriveLetter("D"c)
        End Function

        Public Shared Function FindFirstFreeDriveLetter(start As Char) As Char
            start = Char.ToUpperInvariant(start)
            If start < "A"c OrElse start > "Z"c Then
                Throw New ArgumentOutOfRangeException(NameOf(start))
            End If

            Dim logical_drives = SafeNativeMethods.GetLogicalDrives()

            For search = Convert.ToUInt16(start) To Convert.ToUInt16("Z"c)
                If (logical_drives And (1 << (search - Convert.ToUInt16("A"c)))) = 0 Then
                    Using key = Registry.CurrentUser.OpenSubKey($"Network\{search}")
                        If key Is Nothing Then
                            Return Convert.ToChar(search)
                        End If
                    End Using
                End If
            Next

            Return Nothing

        End Function

        Public Shared Function GetFileVersion(exe As Stream) As Version

            Dim buffer As New MemoryStream
            exe.CopyTo(buffer)

            Return GetFileVersion(buffer.GetBuffer())

        End Function

        Public Shared Function GetFileVersion(exepath As String) As Version

            Dim buffer As Byte()

            Using exe = OpenFileStream(exepath, FileMode.Open, FileAccess.Read, FileShare.Read Or FileShare.Delete)
                buffer = New Byte(0 To CInt(exe.Length - 1)) {}
                exe.Read(buffer, 0, buffer.Length)
            End Using

            Return GetFileVersion(buffer)

        End Function

        Public Shared Function GetFileVersion(exe As Byte()) As Version

            Dim filever = NativeFileVersion.GetNativeFileVersion(exe)

            Return filever.FileVersion

        End Function

        Public Shared Function ReadNullTerminatedString(buffer As Byte(), ByRef offset As Integer) As String

            Dim sb As New StringBuilder

            Do
                Dim c = BitConverter.ToChar(buffer, offset)
                offset += 2
                If c = Nothing Then
                    Exit Do
                End If
                sb.Append(c)
            Loop

            Return sb.ToString()

        End Function

        Public Shared Function GetExeFileHeader(exe As Stream) As IMAGE_NT_HEADERS

            Dim buffer = New Byte(0 To CInt(exe.Length - 1)) {}
            exe.Read(buffer, 0, buffer.Length)

            Return GetExeFileHeader(buffer)

        End Function

        Public Shared Function GetExeFileHeader(exepath As String) As IMAGE_NT_HEADERS

            Using exe = OpenFileStream(exepath, FileMode.Open, FileAccess.Read, FileShare.Read Or FileShare.Delete)
                Return GetExeFileHeader(exe)
            End Using

        End Function

        Public Shared Function GetExeFileHeader(exe As Byte()) As IMAGE_NT_HEADERS

            Return NativePE.GetImageNtHeaders(exe)

        End Function

        Public Shared Function PadValue(value As Integer, align As Integer) As Integer
            Return (value + align - 1) And -align
        End Function

        <StructLayout(LayoutKind.Sequential)>
        Public Structure DiskExtent
            Public ReadOnly Property DiskNumber As UInteger
            Public ReadOnly Property StartingOffset As Long
            Public ReadOnly Property ExtentLength As Long
        End Structure

        Public Shared Function GetVolumeDiskExtents(volume As SafeFileHandle) As DiskExtent()

            ' 776 is enough to hold 32 disk extent items
            Using buffer = PinnedBuffer.Create(DeviceIoControl(volume, NativeConstants.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, Nothing, 776))

                Dim number = buffer.Read(Of Integer)(0)

                Dim array(0 To number - 1) As DiskExtent

                buffer.ReadArray(8, array, 0, number)

                Return array

            End Using

        End Function

        Public Shared Function GetPartitionInformation(disk As SafeFileHandle) As PARTITION_INFORMATION?

            Dim partition_info As PARTITION_INFORMATION = Nothing

            If UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_PARTITION_INFO_EX,
                                          IntPtr.Zero, 0, partition_info, CUInt(Marshal.SizeOf(partition_info)),
                                          0, IntPtr.Zero) Then
                Return partition_info
            Else
                Return Nothing
            End If

        End Function

        Public Shared Function GetPartitionInformationEx(disk As SafeFileHandle) As PARTITION_INFORMATION_EX?

            Dim partition_info As PARTITION_INFORMATION_EX = Nothing

            If UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_PARTITION_INFO_EX,
                                          IntPtr.Zero, 0, partition_info, CUInt(Marshal.SizeOf(partition_info)),
                                          0, IntPtr.Zero) Then
                Return partition_info
            Else
                Return Nothing
            End If

        End Function

        Public Class DriveLayoutInformation

            Public ReadOnly Property DriveLayoutInformation As DRIVE_LAYOUT_INFORMATION_EX

            Public ReadOnly Property Partitions As ReadOnlyCollection(Of PARTITION_INFORMATION_EX)

            Public Sub New(DriveLayoutInformation As DRIVE_LAYOUT_INFORMATION_EX,
                       Partitions As IList(Of PARTITION_INFORMATION_EX))

                _DriveLayoutInformation = DriveLayoutInformation
                _Partitions = New ReadOnlyCollection(Of PARTITION_INFORMATION_EX)(Partitions)
            End Sub

            Public Overrides Function GetHashCode() As Integer
                Return 0
            End Function

            Public Overrides Function ToString() As String
                Return "N/A"
            End Function

        End Class

        Public Class DriveLayoutInformationMBR
            Inherits DriveLayoutInformation

            Public ReadOnly Property MBR As DRIVE_LAYOUT_INFORMATION_MBR

            Public Sub New(DriveLayoutInformation As DRIVE_LAYOUT_INFORMATION_EX,
                       Partitions As PARTITION_INFORMATION_EX(),
                       DriveLayoutInformationMBR As DRIVE_LAYOUT_INFORMATION_MBR)
                MyBase.New(DriveLayoutInformation, Partitions)

                _MBR = DriveLayoutInformationMBR
            End Sub

            Public Overrides Function GetHashCode() As Integer
                Return _MBR.GetHashCode()
            End Function

            Public Overrides Function ToString() As String
                Return _MBR.ToString()
            End Function

        End Class

        Public Class DriveLayoutInformationGPT
            Inherits DriveLayoutInformation

            Public ReadOnly Property GPT As DRIVE_LAYOUT_INFORMATION_GPT

            Public Sub New(DriveLayoutInformation As DRIVE_LAYOUT_INFORMATION_EX,
                       Partitions As PARTITION_INFORMATION_EX(),
                       DriveLayoutInformationGPT As DRIVE_LAYOUT_INFORMATION_GPT)
                MyBase.New(DriveLayoutInformation, Partitions)

                _GPT = DriveLayoutInformationGPT
            End Sub

            Public Overrides Function GetHashCode() As Integer
                Return _GPT.GetHashCode()
            End Function

            Public Overrides Function ToString() As String
                Return _GPT.ToString()
            End Function

        End Class

        Public Shared Function GetDriveLayoutEx(disk As SafeFileHandle) As DriveLayoutInformation

            Dim max_partitions = 1

            Do

                Dim size_needed = PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_EX).TypeSize +
                PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_GPT).TypeSize +
                max_partitions * PinnedBuffer(Of PARTITION_INFORMATION_EX).TypeSize

                Using buffer As New PinnedBuffer(Of Byte)(size_needed)

                    If Not UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_DRIVE_LAYOUT_EX,
                                              IntPtr.Zero, 0, buffer, CUInt(buffer.ByteLength),
                                              0, IntPtr.Zero) Then

                        If Marshal.GetLastWin32Error() = NativeConstants.ERROR_INSUFFICIENT_BUFFER Then
                            max_partitions *= 2
                            Continue Do
                        End If

                        Return Nothing

                    End If

                    Dim layout = buffer.Read(Of DRIVE_LAYOUT_INFORMATION_EX)(0)

                    If layout.PartitionCount > max_partitions Then
                        max_partitions *= 2
                        Continue Do
                    End If

                    Dim partitions(0 To layout.PartitionCount - 1) As PARTITION_INFORMATION_EX
                    For i = 0 To layout.PartitionCount - 1
                        partitions(i) = CType(Marshal.PtrToStructure(buffer.DangerousGetHandle() + 48 + i * PinnedBuffer(Of PARTITION_INFORMATION_EX).TypeSize,
                                                                       GetType(PARTITION_INFORMATION_EX)),
                                                                       PARTITION_INFORMATION_EX)
                    Next

                    If layout.PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_MBR Then
                        Dim mbr = buffer.Read(Of DRIVE_LAYOUT_INFORMATION_MBR)(8)
                        Return New DriveLayoutInformationMBR(layout, partitions, mbr)
                    ElseIf layout.PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT Then
                        Dim gpt = buffer.Read(Of DRIVE_LAYOUT_INFORMATION_GPT)(8)
                        Return New DriveLayoutInformationGPT(layout, partitions, gpt)
                    Else
                        Return New DriveLayoutInformation(layout, partitions)
                    End If

                End Using

            Loop

        End Function

        Public Shared Sub SetDriveLayoutEx(disk As SafeFileHandle, layout As DriveLayoutInformation)

            Static partition_struct_size As Integer = PinnedBuffer(Of PARTITION_INFORMATION_EX).TypeSize

            Static drive_layout_information_ex_size As Integer = PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_EX).TypeSize

            Static drive_layout_information_record_size As Integer = PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_GPT).TypeSize

            layout.NullCheck(NameOf(layout))

            Dim partition_count = Math.Min(layout.Partitions.Count, layout.DriveLayoutInformation.PartitionCount)

            Dim size_needed = drive_layout_information_ex_size +
            drive_layout_information_record_size +
            partition_count * partition_struct_size

            Dim pos = 0

            Using buffer As New PinnedBuffer(Of Byte)(size_needed)

                buffer.Write(CULng(pos), layout.DriveLayoutInformation)

                pos += drive_layout_information_ex_size

                Select Case layout.DriveLayoutInformation.PartitionStyle

                    Case PARTITION_STYLE.PARTITION_STYLE_MBR
                        buffer.Write(CULng(pos), DirectCast(layout, DriveLayoutInformationMBR).MBR)

                    Case PARTITION_STYLE.PARTITION_STYLE_GPT
                        buffer.Write(CULng(pos), DirectCast(layout, DriveLayoutInformationGPT).GPT)

                End Select

                pos += drive_layout_information_record_size

                For i = 0 To partition_count - 1
                    Marshal.StructureToPtr(layout.Partitions(i),
                                       buffer.DangerousGetHandle() + pos + i * partition_struct_size,
                                       False)
                Next

                Dim rc = UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_SET_DRIVE_LAYOUT_EX,
                                              buffer, CUInt(buffer.ByteLength), IntPtr.Zero, 0,
                                              0, IntPtr.Zero)

                For i = 0 To partition_count - 1
                    Marshal.DestroyStructure(buffer.DangerousGetHandle() + pos + i * partition_struct_size,
                                         GetType(PARTITION_INFORMATION_EX))
                Next

                Win32Try(rc)

            End Using

        End Sub

        Public Shared Sub InitializeDisk(disk As SafeFileHandle, PartitionStyle As PARTITION_STYLE)

            Using buffer As New PinnedBuffer(Of CREATE_DISK_GPT)(1)

                Select Case PartitionStyle

                    Case PARTITION_STYLE.PARTITION_STYLE_MBR
                        Dim mbr As New CREATE_DISK_MBR With {
                            .PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_MBR
                        }

                        UnsafeNativeMethods.RtlGenRandom(mbr.DiskSignature, 4)

                        mbr.DiskSignature = mbr.DiskSignature Or &H80808081UI
                        mbr.DiskSignature = mbr.DiskSignature And &HFEFEFEFFUI

                        buffer.Write(0, mbr)

                    Case PARTITION_STYLE.PARTITION_STYLE_GPT
                        Dim gpt As New CREATE_DISK_GPT With {
                            .PartitionStyle = PARTITION_STYLE.PARTITION_STYLE_GPT,
                            .DiskId = Guid.NewGuid(),
                            .MaxPartitionCount = 128
                        }

                        buffer.Write(0, gpt)

                    Case Else
                        Throw New ArgumentOutOfRangeException(NameOf(PartitionStyle))

                End Select

                Dim rc = UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_CREATE_DISK,
                                                  buffer, CUInt(buffer.ByteLength), IntPtr.Zero, 0,
                                                  0, IntPtr.Zero)

                Win32Try(rc)

            End Using

        End Sub

        Public Shared Sub FlushBuffers(handle As SafeFileHandle)
            Win32Try(UnsafeNativeMethods.FlushFileBuffers(handle))
        End Sub

        Public Shared Function GetDiskOffline(disk As SafeFileHandle) As Boolean?

            Dim attribs_size As Byte = 16
            Dim attribs(0 To attribs_size - 1) As Byte

            If UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_DISK_ATTRIBUTES,
                                          IntPtr.Zero, 0, attribs, attribs_size,
                                          0, IntPtr.Zero) Then
                Return (attribs(8) And 1) <> 0
            Else
                Return Nothing
            End If

        End Function

        Public Shared Function TryParseFileTimeUtc(filetime As Long) As Date?

            Static MaxFileTime As Long = Date.MaxValue.ToFileTimeUtc()

            If filetime > 0 AndAlso filetime <= MaxFileTime Then
                Return Date.FromFileTimeUtc(filetime)
            Else
                Return Nothing
            End If

        End Function

        Public Shared Function SetFilePointer(file As SafeFileHandle, distance_to_move As Long, <Out> ByRef new_file_pointer As Long, move_method As UInteger) As Boolean

            Return UnsafeNativeMethods.SetFilePointerEx(file, distance_to_move, new_file_pointer, move_method)

        End Function

        Public Shared Sub SetDiskOffline(disk As SafeFileHandle, offline As Boolean)

            Dim attribs_size As Byte = 40
            Dim attribs(0 To attribs_size - 1) As Byte
            attribs(0) = attribs_size
            attribs(16) = 1
            If offline Then
                attribs(8) = 1
            End If

            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_SET_DISK_ATTRIBUTES,
                                          attribs, attribs_size, IntPtr.Zero, 0,
                                          0, IntPtr.Zero))

        End Sub

        Public Shared Function GetDiskReadOnly(disk As SafeFileHandle) As Boolean?

            Dim attribs_size As Byte = 16
            Dim attribs(0 To attribs_size - 1) As Byte

            If UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_DISK_ATTRIBUTES,
                                          IntPtr.Zero, 0, attribs, attribs_size,
                                          0, IntPtr.Zero) Then

                Return (attribs(8) And 2) <> 0
            Else
                Return Nothing
            End If

        End Function

        Public Shared Sub SetDiskReadOnly(disk As SafeFileHandle, read_only As Boolean)

            Dim attribs_size As Byte = 40
            Dim attribs(0 To attribs_size - 1) As Byte
            attribs(0) = attribs_size
            attribs(16) = 2
            If read_only Then
                attribs(8) = 2
            End If

            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_SET_DISK_ATTRIBUTES,
                                          attribs, attribs_size, IntPtr.Zero, 0,
                                          0, IntPtr.Zero))

        End Sub

        Public Shared Sub SetVolumeOffline(disk As SafeFileHandle, offline As Boolean)

            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk, If(offline,
                                                   NativeConstants.IOCTL_VOLUME_OFFLINE,
                                                   NativeConstants.IOCTL_VOLUME_ONLINE),
                                          IntPtr.Zero, 0, IntPtr.Zero, 0,
                                          0, IntPtr.Zero))

        End Sub

        Public Shared Function GetExceptionForNtStatus(NtStatus As Int32) As Exception

            Return New Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(NtStatus))

        End Function

        Public Shared Function GetModuleFullPath(hModule As IntPtr) As String

            Dim str As New StringBuilder(32768)

            Dim PathLength = UnsafeNativeMethods.GetModuleFileName(hModule, str, str.Capacity)
            If PathLength = 0 Then
                Throw New Win32Exception
            End If

            Return str.ToString(0, PathLength)

        End Function

        Public Shared Function EnumerateDiskVolumesMountPoints(DiskDevice As String) As IEnumerable(Of String)

            Return EnumerateDiskVolumes(DiskDevice).SelectMany(AddressOf EnumerateVolumeMountPoints)

        End Function

        Public Shared Function EnumerateDiskVolumesMountPoints(DiskNumber As UInteger) As IEnumerable(Of String)

            Return EnumerateDiskVolumes(DiskNumber).SelectMany(AddressOf EnumerateVolumeMountPoints)

        End Function

        Public Shared Function GetVolumeNameForVolumeMountPoint(MountPoint As String) As String

            Dim str As New StringBuilder(65536)

            If UnsafeNativeMethods.GetVolumeNameForVolumeMountPoint(MountPoint, str, str.Capacity) AndAlso
                str.Length > 0 Then

                Return str.ToString()

            End If

            If MountPoint.StartsWith("\\?\", StringComparison.Ordinal) Then
                MountPoint = MountPoint.Substring(4)
            End If

            MountPoint = MountPoint.TrimEnd("\"c)

            Dim nt_device_path = QueryDosDevice(MountPoint)?.FirstOrDefault()

            If String.IsNullOrWhiteSpace(nt_device_path) Then

                Return Nothing

            End If

            Return _
                Aggregate dosdevice In QueryDosDevice()
                Where dosdevice.Length = 44 AndAlso dosdevice.StartsWith("Volume{", StringComparison.OrdinalIgnoreCase)
                Let target = QueryDosDevice(dosdevice)
                Where target IsNot Nothing AndAlso target.Contains(nt_device_path, StringComparer.OrdinalIgnoreCase)
                Select $"\\?\{dosdevice}\"
                Into FirstOrDefault()

        End Function

        Public Structure ScsiAddressAndLength
            Implements IEquatable(Of ScsiAddressAndLength)

            Public ReadOnly Property ScsiAddress As SCSI_ADDRESS

            Public ReadOnly Property Length As Long

            Public Sub New(ScsiAddress As SCSI_ADDRESS, Length As Long)
                _ScsiAddress = ScsiAddress
                _Length = Length
            End Sub

            Public Overrides Function Equals(obj As Object) As Boolean
                If TypeOf obj IsNot ScsiAddressAndLength Then
                    Return False
                End If

                Return Equals(DirectCast(obj, ScsiAddressAndLength))
            End Function

            Public Overrides Function GetHashCode() As Integer
                Return _ScsiAddress.GetHashCode() Xor _Length.GetHashCode
            End Function

            Public Overrides Function ToString() As String
                Return $"{_ScsiAddress}, Length = {_Length}"
            End Function

            Public Overloads Function Equals(other As ScsiAddressAndLength) As Boolean Implements IEquatable(Of ScsiAddressAndLength).Equals
                Return _Length.Equals(other._Length) AndAlso _ScsiAddress.Equals(other._ScsiAddress)
            End Function

            Shared Operator =(a As ScsiAddressAndLength, b As ScsiAddressAndLength) As Boolean
                Return a.Equals(b)
            End Operator

            Shared Operator <>(a As ScsiAddressAndLength, b As ScsiAddressAndLength) As Boolean
                Return a.Equals(b)
            End Operator
        End Structure

        Public Shared Function GetScsiAddressAndLength(drv As String) As ScsiAddressAndLength?

            Static SizeOfLong As UInt32 = CUInt(PinnedBuffer(Of Long).TypeSize)
            Static SizeOfScsiAddress As UInt32 = CUInt(PinnedBuffer(Of SCSI_ADDRESS).TypeSize)

            Try
                Using disk As New DiskDevice(drv, FileAccess.Read)
                    Dim ScsiAddress As SCSI_ADDRESS
                    Dim rc = UnsafeNativeMethods.
                            DeviceIoControl(disk.SafeFileHandle,
                                            NativeConstants.IOCTL_SCSI_GET_ADDRESS,
                                            IntPtr.Zero,
                                            0,
                                            ScsiAddress,
                                            SizeOfScsiAddress,
                                            Nothing,
                                            Nothing)

                    If Not rc Then
                        Trace.WriteLine($"IOCTL_SCSI_GET_ADDRESS failed for device {drv}: Error 0x{Marshal.GetLastWin32Error():X}")
                        Return Nothing
                    End If

                    Dim Length As Long
                    rc = UnsafeNativeMethods.
                            DeviceIoControl(disk.SafeFileHandle,
                                            NativeConstants.IOCTL_DISK_GET_LENGTH_INFO,
                                            IntPtr.Zero,
                                            0,
                                            Length,
                                            SizeOfLong,
                                            Nothing,
                                            Nothing)

                    If Not rc Then
                        Trace.WriteLine($"IOCTL_DISK_GET_LENGTH_INFO failed for device {drv}: Error 0x{Marshal.GetLastWin32Error():X}")
                        Return Nothing
                    End If

                    Return New ScsiAddressAndLength(ScsiAddress, Length)
                End Using

            Catch ex As Exception
                Trace.WriteLine($"Exception attempting to find SCSI address for device {drv}: {ex.JoinMessages()}")
                Return Nothing

            End Try
        End Function

        Public Shared Function GetDevicesScsiAddresses(adapter As ScsiAdapter) As Dictionary(Of UInteger, String)

            Dim q =
            From device_number In adapter.GetDeviceList()
            Let drv = adapter.GetDeviceName(device_number)
            Where drv IsNot Nothing

            Return q.ToDictionary(Function(o) o.device_number,
                              Function(o) o.drv)

        End Function

        Public Shared Function GetMountPointBasedPath(path As String) As String

            path.NullCheck(NameOf(path))

            Const volume_path_prefix = "\\?\Volume{00000000-0000-0000-0000-000000000000}\"

            If path.Length > volume_path_prefix.Length AndAlso
                    path.StartsWith("\\?\Volume{", StringComparison.OrdinalIgnoreCase) Then

                Dim vol = path.Substring(0, volume_path_prefix.Length)
                Dim mountpoint = EnumerateVolumeMountPoints(vol)?.FirstOrDefault()

                If mountpoint IsNot Nothing Then
                    path = $"{mountpoint}{path.Substring(volume_path_prefix.Length)}"
                End If

            End If

            Return path

        End Function

        Public Shared Function EnumerateVolumeMountPoints(VolumeName As String) As IEnumerable(Of String)

            VolumeName.NullCheck(NameOf(VolumeName))

            Dim TargetPath(0 To 65536) As Char

            Dim length As Int32

            If UnsafeNativeMethods.GetVolumePathNamesForVolumeName(VolumeName, TargetPath, TargetPath.Length, length) AndAlso
                length > 2 Then

                Return ParseDoubleTerminatedString(TargetPath, length)

            End If

            If VolumeName.StartsWith("\\?\Volume{", StringComparison.OrdinalIgnoreCase) Then
                VolumeName = VolumeName.Substring("\\?\".Length, 44)
            ElseIf VolumeName.StartsWith("Volume{", StringComparison.OrdinalIgnoreCase) Then
                VolumeName = VolumeName.Substring(0, 44)
            Else
                Return Enumerable.Empty(Of String)()
            End If

            VolumeName = QueryDosDevice(VolumeName)?.FirstOrDefault()

            If String.IsNullOrWhiteSpace(VolumeName) Then
                Return Enumerable.Empty(Of String)()
            End If

            Dim names = From link In QueryDosDevice()
                        Where link.Length = 2 AndAlso link(1) = ":"c
                        From target In QueryDosDevice(link)
                        Where VolumeName.Equals(target, StringComparison.OrdinalIgnoreCase)
                        Select $"{link}\"

            Return names

        End Function

        Public Shared Function EnumerateDiskVolumes(DevicePath As String) As IEnumerable(Of String)

            DevicePath.NullCheck(NameOf(DevicePath))

            If DevicePath.StartsWith("\\?\PhysicalDrive", StringComparison.OrdinalIgnoreCase) OrElse
                DevicePath.StartsWith("\\.\PhysicalDrive", StringComparison.OrdinalIgnoreCase) Then          ' \\?\PhysicalDrive paths to partitioned disks

                Return EnumerateDiskVolumes(UInteger.Parse(DevicePath.Substring("\\?\PhysicalDrive".Length)))

            ElseIf DevicePath.StartsWith("\\?\", StringComparison.Ordinal) OrElse
                DevicePath.StartsWith("\\.\", StringComparison.Ordinal) Then

                Return EnumerateVolumeNamesForDeviceObject(QueryDosDevice(DevicePath.Substring("\\?\".Length)).First())     ' \\?\C: or similar paths to mounted volumes

            Else

                Return Enumerable.Empty(Of String)()

            End If

        End Function

        Public Shared Function EnumerateDiskVolumes(DiskNumber As UInteger) As IEnumerable(Of String)

            Return (New VolumeEnumerator).Where(
                Function(volumeGuid)
                    Try
                        Return VolumeUsesDisk(volumeGuid, DiskNumber)

                    Catch ex As Exception
                        Trace.WriteLine($"{volumeGuid}: {ex.JoinMessages()}")
                        Return False

                    End Try
                End Function)

        End Function

        Public Shared Function EnumerateVolumeNamesForDeviceObject(DeviceObject As String) As IEnumerable(Of String)

            If DeviceObject.EndsWith("}", StringComparison.Ordinal) AndAlso
                DeviceObject.StartsWith("\Device\Volume{", StringComparison.Ordinal) Then

                Return {$"\\?\{DeviceObject.Substring("\Device\".Length)}\"}

            End If

            Return (New VolumeEnumerator).Where(
            Function(volumeGuid)
                Try
                    If volumeGuid.StartsWith("\\?\", StringComparison.Ordinal) Then
                        volumeGuid = volumeGuid.Substring(4)
                    End If

                    volumeGuid = volumeGuid.TrimEnd("\"c)

                    Return _
                        Aggregate target In QueryDosDevice(volumeGuid)
                            Into Any(target.Equals(DeviceObject, StringComparison.OrdinalIgnoreCase))

                Catch ex As Exception
                    Trace.WriteLine($"{volumeGuid}: {ex.JoinMessages()}")
                    Return False

                End Try
            End Function)

        End Function

        Public Shared Function VolumeUsesDisk(VolumeGuid As String, DiskNumber As UInteger) As Boolean

            Using volume As New DiskDevice(VolumeGuid.NullCheck(NameOf(VolumeGuid)).TrimEnd("\"c), 0)

                Try
                    Dim extents = GetVolumeDiskExtents(volume.SafeFileHandle)

                    Return extents.Any(Function(extent) extent.DiskNumber.Equals(DiskNumber))

                Catch ex As Win32Exception When _
                ex.NativeErrorCode = NativeConstants.ERROR_INVALID_FUNCTION

                    Return False

                End Try

            End Using

        End Function

        Public Shared Sub ScanForHardwareChanges()

            ScanForHardwareChanges(Nothing)

        End Sub

        Public Shared Function ScanForHardwareChanges(rootid As String) As UInt32

            Dim devInst As UInt32
            Dim status = UnsafeNativeMethods.CM_Locate_DevNode(devInst, rootid, 0)
            If status <> 0 Then
                Return status
            End If

            Return UnsafeNativeMethods.CM_Reenumerate_DevNode(devInst, 0)

        End Function

        Public Shared Function GetDevInst(devinstName As String) As UInt32?

            Dim devInst As UInt32

            Dim status = UnsafeNativeMethods.CM_Locate_DevNode(devInst, devinstName, 0)

            If status <> 0 Then
                Trace.WriteLine($"Device '{devinstName}' error 0x{status:X}")
                Return Nothing
            End If

#If DEBUG Then
            Trace.WriteLine($"{devinstName} = devInst {devInst}")
#End If

            Return devInst

        End Function

        Public Shared Function EnumerateDeviceInstancesForService(service As String, <Out> ByRef instances As IEnumerable(Of String)) As UInt32

            Dim length As Int32
            Dim status = UnsafeNativeMethods.CM_Get_Device_ID_List_Size(length, service, UnsafeNativeMethods.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            Dim Buffer(0 To length - 1) As Char
            status = UnsafeNativeMethods.CM_Get_Device_ID_List(service,
                                                Buffer,
                                                CUInt(Buffer.Length),
                                                UnsafeNativeMethods.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            instances = ParseDoubleTerminatedString(Buffer, length)

            Return status

        End Function

        Public Shared Function EnumerateDeviceInstancesForSetupClass(service As String, <Out> ByRef instances As IEnumerable(Of String)) As UInt32

            Dim length As Int32
            Dim status = UnsafeNativeMethods.CM_Get_Device_ID_List_Size(length, service, UnsafeNativeMethods.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            Dim Buffer(0 To length - 1) As Char
            status = UnsafeNativeMethods.CM_Get_Device_ID_List(service,
                                                Buffer,
                                                CUInt(Buffer.Length),
                                                UnsafeNativeMethods.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            instances = ParseDoubleTerminatedString(Buffer, length)

            Return status

        End Function

        Public Shared Sub RestartDevice(devclass As Guid, devinst As UInt32)

            '' get a list of devices which support the given interface
            Using devinfo = UnsafeNativeMethods.SetupDiGetClassDevs(devclass,
                Nothing,
                Nothing,
                UnsafeNativeMethods.DIGCF_PROFILE Or
                UnsafeNativeMethods.DIGCF_DEVICEINTERFACE Or
                UnsafeNativeMethods.DIGCF_PRESENT)

                If devinfo.IsInvalid Then
                    Throw New Exception("Device not found")
                End If

                Dim devInfoData As SP_DEVINFO_DATA
                '' as per DDK docs on SetupDiEnumDeviceInfo
                devInfoData.Initialize()

                '' step through the list of devices for this handle
                '' get device info at index deviceIndex, the function returns FALSE
                '' when there Is no device at the given index.
                Dim deviceIndex = 0UI

                While UnsafeNativeMethods.SetupDiEnumDeviceInfo(devinfo, deviceIndex, devInfoData)

                    If devInfoData.DevInst.Equals(devinst) Then
                        Dim pcp As New UnsafeNativeMethods.SP_PROPCHANGE_PARAMS With {
                            .ClassInstallHeader = New UnsafeNativeMethods.SP_CLASSINSTALL_HEADER With {
                                .Size = CUInt(PinnedBuffer(Of UnsafeNativeMethods.SP_CLASSINSTALL_HEADER).TypeSize),
                                .InstallFunction = UnsafeNativeMethods.DIF_PROPERTYCHANGE
                            },
                            .HwProfile = 0,
                            .Scope = UnsafeNativeMethods.DICS_FLAG_CONFIGSPECIFIC,
                            .StateChange = UnsafeNativeMethods.DICS_PROPCHANGE
                        }

                        If UnsafeNativeMethods.SetupDiSetClassInstallParams(devinfo, devInfoData, pcp, PinnedBuffer(Of UnsafeNativeMethods.SP_PROPCHANGE_PARAMS).TypeSize) AndAlso
                            UnsafeNativeMethods.SetupDiCallClassInstaller(UnsafeNativeMethods.DIF_PROPERTYCHANGE, devinfo, devInfoData) Then

                            Return
                        End If

                        Throw New Exception($"Device restart failed", New Win32Exception)
                    End If

                    deviceIndex += 1UI

                End While

            End Using

            Throw New Exception("Device not found")

        End Sub

        Public Shared Sub RunDLLInstallHinfSection(OwnerWindow As IntPtr, InfPath As String, InfSection As String)

            Dim cmdLine = $"{InfSection} 132 {InfPath}"
            Trace.WriteLine($"RunDLLInstallFromInfSection: {cmdLine}")

            If InfPath.NullCheck(NameOf(InfPath)).Contains(" ") Then
                Throw New ArgumentException("Arguments to this method cannot contain spaces.", NameOf(InfPath))
            End If

            If InfSection.NullCheck(NameOf(InfSection)).Contains(" ") Then
                Throw New ArgumentException("Arguments to this method cannot contain spaces.", NameOf(InfSection))
            End If

            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            UnsafeNativeMethods.InstallHinfSection(OwnerWindow,
                                    Nothing,
                                    cmdLine,
                                    0)

        End Sub

        Public Shared Sub InstallFromInfSection(OwnerWindow As IntPtr, InfPath As String, InfSection As String)

            Trace.WriteLine($"InstallFromInfSection: InfPath=""{InfPath}"", InfSection=""{InfSection}""")

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim ErrorLine As UInt32
            Dim hInf = UnsafeNativeMethods.SetupOpenInfFile(InfPath,
                                             Nothing,
                                             &H2UI,
                                             ErrorLine)
            If hInf.IsInvalid Then
                Throw New Win32Exception($"Line number: {ErrorLine}")
            End If

            Using hInf

                Win32Try(UnsafeNativeMethods.SetupInstallFromInfSection(OwnerWindow,
                                                         hInf,
                                                         InfSection,
                                                         &H1FFUI,
                                                         IntPtr.Zero,
                                                         Nothing,
                                                         &H4UI,
                                                         Function() 1,
                                                         Nothing,
                                                         Nothing,
                                                         Nothing))

            End Using

        End Sub

        Public Const DIF_REGISTERDEVICE = &H19UI
        Public Const DIF_REMOVE = &H5UI

        Public Shared Sub CreateRootPnPDevice(OwnerWindow As IntPtr, InfPath As String, hwid As String, ForceReplaceExistingDrivers As Boolean, <Out> ByRef RebootRequired As Boolean)

            Trace.WriteLine($"CreateOrUpdateRootPnPDevice: InfPath=""{InfPath}"", hwid=""{hwid}""")

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            ''
            '' List of hardware ID's must be double zero-terminated
            ''
            Dim hwIdList = Encoding.Unicode.GetBytes(hwid & {New Char, New Char})

            ''
            '' Use the INF File to extract the Class GUID.
            ''
            Dim ClassGUID As Guid
            Dim ClassName(0 To 31) As Char
            Win32Try(UnsafeNativeMethods.SetupDiGetINFClass(InfPath,
                                             ClassGUID,
                                             ClassName,
                                             CUInt(ClassName.Length),
                                             0))

            Trace.WriteLine($"CreateOrUpdateRootPnPDevice: ClassGUID=""{ClassGUID}"", ClassName=""{New String(ClassName)}""")

            ''
            '' Create the container for the to-be-created Device Information Element.
            ''
            Dim DeviceInfoSet = UnsafeNativeMethods.SetupDiCreateDeviceInfoList(ClassGUID, OwnerWindow)
            If DeviceInfoSet.IsInvalid Then
                Throw New Win32Exception
            End If

            Using DeviceInfoSet

                ''
                '' Now create the element.
                '' Use the Class GUID and Name from the INF file.
                ''
                Dim DeviceInfoData As SP_DEVINFO_DATA
                DeviceInfoData.Initialize()
                Win32Try(UnsafeNativeMethods.SetupDiCreateDeviceInfo(DeviceInfoSet,
                                                      ClassName,
                                                      ClassGUID,
                                                      Nothing,
                                                      OwnerWindow,
                                                      &H1UI,
                                                      DeviceInfoData))

                ''
                '' Add the HardwareID to the Device's HardwareID property.
                ''
                Win32Try(UnsafeNativeMethods.SetupDiSetDeviceRegistryProperty(DeviceInfoSet,
                                                               DeviceInfoData,
                                                               &H1UI,
                                                               hwIdList,
                                                               CUInt(hwIdList.Length)))

                ''
                '' Transform the registry element into an actual devnode
                '' in the PnP HW tree.
                ''
                Win32Try(UnsafeNativeMethods.SetupDiCallClassInstaller(DIF_REGISTERDEVICE,
                                                        DeviceInfoSet,
                                                        DeviceInfoData))

            End Using

            ''
            '' update the driver for the device we just created
            ''
            UpdateDriverForPnPDevices(OwnerWindow,
                                  InfPath,
                                  hwid,
                                  ForceReplaceExistingDrivers,
                                  RebootRequired)

        End Sub

        Public Shared Iterator Function EnumerateChildDevices(devInst As UInt32) As IEnumerable(Of UInt32)

            Dim child As UInteger

            Dim rc = UnsafeNativeMethods.CM_Get_Child(child, devInst, 0)

            While rc = 0

                Trace.WriteLine($"Found child devinst: {child}")

                Yield child

                rc = UnsafeNativeMethods.CM_Get_Sibling(child, child, 0)

            End While

        End Function

        Public Shared Function GetPhysicalDeviceObjectNtPath(devInstName As String) As String

            Dim devinst = GetDevInst(devInstName)

            If Not devinst.HasValue Then
                Return Nothing
            End If

            Return GetPhysicalDeviceObjectNtPath(devinst.Value)

        End Function

        Public Shared Function GetPhysicalDeviceObjectNtPath(devInst As UInt32) As String

            Dim regtype As RegistryValueKind = Nothing

            Dim buffer(0 To 518) As Byte
            Dim buffersize = buffer.Length

            Dim rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_Property(devInst, CmDevNodeRegistryProperty.CM_DRP_PHYSICAL_DEVICE_OBJECT_NAME, regtype, buffer, buffersize, 0)

            If rc <> 0 Then
                Trace.WriteLine($"Error getting registry property for device {devInst}. Status=0x{rc:X}")
                Return Nothing
            End If

            Dim name = Encoding.Unicode.GetString(buffer, 0, buffersize - 2)

#If DEBUG Then
            Trace.WriteLine($"Found physical device object name: '{name}'")
#End If

            Return name

        End Function

        Public Shared Function GetDeviceRegistryProperty(devInst As UInt32, prop As CmDevNodeRegistryProperty) As IEnumerable(Of String)

            Dim regtype As RegistryValueKind = Nothing

            Dim buffer(0 To 518) As Byte
            Dim buffersize = buffer.Length

            Dim rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_Property(devInst, prop, regtype, buffer, buffersize, 0)

            If rc <> 0 Then
                Trace.WriteLine($"Error getting registry property for device {devInst}. Status=0x{rc:X}")
                Return Nothing
            End If

            Dim name = ParseDoubleTerminatedString(buffer, buffersize)

            Return name

        End Function

        Public Shared Function EnumerateWin32DevicePaths(nt_device_path As String) As IEnumerable(Of String)

            Return _
                From dosdevice In QueryDosDevice()
                Where QueryDosDevice(dosdevice).Contains(nt_device_path, StringComparer.OrdinalIgnoreCase)
                Select $"\\?\{dosdevice}"

        End Function

        Public Shared Function EnumerateRegisteredFilters(devInst As UInt32) As IEnumerable(Of String)

            Dim regtype As RegistryValueKind = Nothing

            Dim buffer(0 To 65535) As Byte
            Dim buffersize = buffer.Length

            Dim rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_Property(devInst, CmDevNodeRegistryProperty.CM_DRP_UPPERFILTERS, regtype, buffer, buffersize, 0)

            If rc = NativeConstants.CR_NO_SUCH_VALUE Then
                Return Enumerable.Empty(Of String)()
            ElseIf rc <> 0 Then
                Dim msg = $"Error getting registry property for device. Status=0x{rc:X}"
                Throw New IOException(msg)
            End If

            Return ParseDoubleTerminatedString(buffer, buffersize)

        End Function

        '' Switched to querying registry directly instead. CM_Get_Class_Registry_Property seems to
        '' return 0x13 CR_FAILURE on Win7.
#If USE_CM_API Then

        Public Shared Function GetRegisteredFilters(devClass As Guid) As String()

            Dim regtype As RegistryValueKind = Nothing

            Dim buffer(0 To 65535) As Byte
            Dim buffersize = buffer.Length

            Dim rc = Win32API.CM_Get_Class_Registry_Property(devClass, Win32API.CmClassRegistryProperty.CM_CRP_UPPERFILTERS, regtype, buffer, buffersize, 0)

            If rc <> 0 Then
                Dim msg = $"Error getting registry property for device class {devClass}. Status=0x{rc:X}"
                Trace.WriteLine(msg)
                Throw New IOException(msg)
            End If

            Return ParseDoubleTerminatedString(Buffer)

        End Function

#Else

        Public Shared Function GetRegisteredFilters(devClass As Guid) As String()

            Using key = Registry.LocalMachine.OpenSubKey($"SYSTEM\CurrentControlSet\Control\Class\{devClass:B}")

                Return TryCast(key?.GetValue("UpperFilters"), String())

            End Using

        End Function

#End If

        Public Shared Sub SetRegisteredFilters(devInst As UInt32, filters As String())

            Dim str = String.Join(New Char, filters) & New Char & New Char
            Dim buffer = Encoding.Unicode.GetBytes(str)
            Dim buffersize = buffer.Length

            Dim rc = UnsafeNativeMethods.CM_Set_DevNode_Registry_Property(devInst, CmDevNodeRegistryProperty.CM_DRP_UPPERFILTERS, buffer, buffersize, 0)

            If rc <> 0 Then
                Throw New Exception($"Error setting registry property for device. Status=0x{rc:X}")
            End If

        End Sub

        Public Shared Sub SetRegisteredFilters(devClass As Guid, filters As String())

            Dim str = String.Join(New Char, filters) & New Char & New Char
            Dim buffer = Encoding.Unicode.GetBytes(str)
            Dim buffersize = buffer.Length

            Dim rc = UnsafeNativeMethods.CM_Set_Class_Registry_Property(devClass, CmClassRegistryProperty.CM_CRP_UPPERFILTERS, buffer, buffersize, 0)

            If rc <> 0 Then
                Throw New Exception($"Error setting registry property for class {devClass}. Status=0x{rc:X}")
            End If

        End Sub

        Public Shared Function AddFilter(devInst As UInt32, driver As String) As Boolean

            Dim filters = EnumerateRegisteredFilters(devInst).ToArray()

            If filters.Any(Function(f) f.Equals(driver, StringComparison.OrdinalIgnoreCase)) Then

                Trace.WriteLine($"Filter '{driver}' already registered for devinst {devInst}")

                Return False

            End If

            Trace.WriteLine($"Registering filter '{driver}' for devinst {devInst}")

            Array.Resize(filters, filters.Length + 1)

            filters(filters.Length - 1) = driver

            SetRegisteredFilters(devInst, filters)

            Return True

        End Function

        Public Shared Function AddFilter(devClass As Guid, driver As String, addfirst As Boolean) As Boolean

            driver.NullCheck(NameOf(driver))

            Dim filters = GetRegisteredFilters(devClass)

            If filters Is Nothing Then

#If NETFRAMEWORK AndAlso Not NET46_OR_GREATER Then
                filters = {}
#Else
                filters = Array.Empty(Of String)()
#End If

            ElseIf addfirst AndAlso
            driver.Equals(filters.FirstOrDefault(), StringComparison.OrdinalIgnoreCase) Then

                Trace.WriteLine($"Filter '{driver}' already registered first for class {devClass}")
                Return False

            ElseIf (Not addfirst) AndAlso
            driver.Equals(filters.LastOrDefault(), StringComparison.OrdinalIgnoreCase) Then

                Trace.WriteLine($"Filter '{driver}' already registered last for class {devClass}")
                Return False

            End If

            Dim filter_list As New List(Of String)(filters)

            filter_list.RemoveAll(Function(f) f.Equals(driver, StringComparison.OrdinalIgnoreCase))

            If addfirst Then

                filter_list.Insert(0, driver)

            Else

                filter_list.Add(driver)

            End If

            filters = filter_list.ToArray()

            Trace.WriteLine($"Registering filters '{String.Join(",", filters)}' for class {devClass}")

            SetRegisteredFilters(devClass, filters)

            Return True

        End Function

        Public Shared Function RemoveFilter(devInst As UInt32, driver As String) As Boolean

            Dim filters = EnumerateRegisteredFilters(devInst).ToArray()

            If filters Is Nothing OrElse filters.Length = 0 Then
                Trace.WriteLine($"No filters registered for devinst {devInst}")
                Return False
            End If

            Dim newfilters =
            filters.
            Where(Function(f) Not f.Equals(driver, StringComparison.OrdinalIgnoreCase)).
            ToArray()

            If newfilters.Length = filters.Length Then
                Trace.WriteLine($"Filter '{driver}' not registered for devinst {devInst}")
                Return False
            End If

            Trace.WriteLine($"Removing filter '{driver}' from devinst {devInst}")

            SetRegisteredFilters(devInst, newfilters)

            Return True

        End Function

        Public Shared Function RemoveFilter(devClass As Guid, driver As String) As Boolean

            Dim filters = GetRegisteredFilters(devClass)

            If filters Is Nothing Then
                Trace.WriteLine($"No filters registered for class {devClass}")
                Return False
            End If

            Dim newfilters =
            filters.
            Where(Function(f) Not f.Equals(driver, StringComparison.OrdinalIgnoreCase)).
            ToArray()

            If newfilters.Length = filters.Length Then
                Trace.WriteLine($"Filter '{driver}' not registered for class {devClass}")
                Return False
            End If

            Trace.WriteLine($"Removing filter '{driver}' from class {devClass}")

            SetRegisteredFilters(devClass, newfilters)

            Return True

        End Function

        Public Shared Function RemovePnPDevice(OwnerWindow As IntPtr, hwid As String) As Integer

            Trace.WriteLine($"RemovePnPDevice: hwid='{hwid}'")

            ''
            '' Create the container for the to-be-created Device Information Element.
            ''
            Dim DeviceInfoSet = UnsafeNativeMethods.SetupDiCreateDeviceInfoList(IntPtr.Zero,
                                                                 OwnerWindow)
            If DeviceInfoSet.IsInvalid Then
                Throw New Win32Exception("SetupDiCreateDeviceInfoList")
            End If

            Using DeviceInfoSet

                If Not UnsafeNativeMethods.SetupDiOpenDeviceInfo(DeviceInfoSet, hwid, OwnerWindow, 0, IntPtr.Zero) Then
                    Return 0
                End If

                Dim DeviceInfoData As SP_DEVINFO_DATA
                DeviceInfoData.Initialize()

                Dim i As UInteger
                Dim done As Integer
                Do
                    If Not UnsafeNativeMethods.SetupDiEnumDeviceInfo(DeviceInfoSet, i, DeviceInfoData) Then
                        If i = 0 Then
                            Throw New Win32Exception("SetupDiEnumDeviceInfo")
                        Else
                            Return done
                        End If
                    End If

                    If UnsafeNativeMethods.SetupDiCallClassInstaller(DIF_REMOVE, DeviceInfoSet, DeviceInfoData) Then
                        done += 1
                    End If

                    i += 1UI
                Loop

            End Using

        End Function

        Public Shared Sub UpdateDriverForPnPDevices(OwnerWindow As IntPtr, InfPath As String, hwid As String, forceReplaceExisting As Boolean, <Out> ByRef RebootRequired As Boolean)

            Trace.WriteLine($"UpdateDriverForPnPDevices: InfPath=""{InfPath}"", hwid=""{hwid}"", forceReplaceExisting={forceReplaceExisting}")

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            ''
            '' make use of UpdateDriverForPlugAndPlayDevices
            ''
            Win32Try(UnsafeNativeMethods.UpdateDriverForPlugAndPlayDevices(OwnerWindow,
                                                            hwid,
                                                            InfPath,
                                                            If(forceReplaceExisting, &H1UI, &H0UI),
                                                            RebootRequired))

        End Sub

        Public Shared Function SetupCopyOEMInf(InfPath As String, NoOverwrite As Boolean) As String

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim destName As New StringBuilder(260)

            Win32Try(UnsafeNativeMethods.SetupCopyOEMInf(InfPath,
                                          Nothing,
                                          0,
                                          If(NoOverwrite, &H8UI, &H0UI),
                                          destName,
                                          destName.Capacity,
                                          Nothing,
                                          Nothing))

            Return destName.ToString()

        End Function

        Public Shared Sub DriverPackagePreinstall(InfPath As String)

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim errcode = UnsafeNativeMethods.DriverPackagePreinstall(InfPath, 1)
            If errcode <> 0 Then
                Throw New Win32Exception(errcode)
            End If

        End Sub

        Public Shared Sub DriverPackageInstall(InfPath As String, <Out> ByRef NeedReboot As Boolean)

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim errcode = UnsafeNativeMethods.DriverPackageInstall(InfPath, 1, Nothing, NeedReboot)
            If errcode <> 0 Then
                Throw New Win32Exception(errcode)
            End If

        End Sub

        <Flags>
        Public Enum DriverPackageUninstallFlags As UInteger
            Normal = &H0UI
            DeleteFiles = UnsafeNativeMethods.DRIVER_PACKAGE_DELETE_FILES
            Force = UnsafeNativeMethods.DRIVER_PACKAGE_FORCE
            Silent = UnsafeNativeMethods.DRIVER_PACKAGE_SILENT
        End Enum

        Public Shared Sub DriverPackageUninstall(InfPath As String, Flags As DriverPackageUninstallFlags, <Out> ByRef NeedReboot As Boolean)

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim errcode = UnsafeNativeMethods.DriverPackageUninstall(InfPath, Flags, Nothing, NeedReboot)
            If errcode <> 0 Then
                Throw New Win32Exception(errcode)
            End If

        End Sub

        ''' <summary>
        ''' Re-enumerates partitions on all disk drives currently connected to the system. No exceptions are
        ''' thrown on error, but any exceptions from underlying API calls are logged to trace log.
        ''' </summary>
        Public Shared Sub UpdateDiskProperties()

            For Each diskdevice In
            From device In QueryDosDevice()
            Where device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) OrElse
                device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)

                Try
                    Using device = OpenFileHandle($"\\?\{diskdevice}", 0, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

                        If Not UpdateDiskProperties(device, throwOnFailure:=False) Then
                            Trace.WriteLine($"Error updating disk properties for {diskdevice}: {New Win32Exception().Message}")
                        End If

                    End Using

                Catch ex As Exception
                    Trace.WriteLine($"Error updating disk properties for {diskdevice}: {ex.JoinMessages()}")

                End Try

            Next

        End Sub

        ''' <summary>
        ''' Re-enumerates partitions on a disk device with a specified SCSI address. No
        ''' exceptions are thrown on error, but any exceptions from underlying API calls are
        ''' logged to trace log.
        ''' </summary>
        ''' <returns>Returns a value indicating whether operation was successful or not.</returns>
        Public Shared Function UpdateDiskProperties(ScsiAddress As SCSI_ADDRESS) As Boolean

            Try
                Using devicehandle = OpenDiskByScsiAddress(ScsiAddress, Nothing).Value

                    Dim rc = UpdateDiskProperties(devicehandle, throwOnFailure:=False)

                    If Not rc Then

                        Trace.WriteLine($"Updating disk properties failed for {ScsiAddress}: {New Win32Exception().Message}")

                    End If

                    Return rc

                End Using

            Catch ex As Exception
                Trace.WriteLine($"Error updating disk properties for {ScsiAddress}: {ex.JoinMessages()}")

            End Try

            Return False

        End Function

        Public Shared Function UpdateDiskProperties(devicehandle As SafeFileHandle, throwOnFailure As Boolean) As Boolean

            Dim rc = UnsafeNativeMethods.DeviceIoControl(devicehandle, NativeConstants.IOCTL_DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, 0UI, IntPtr.Zero)

            If Not rc AndAlso throwOnFailure Then
                Throw New Win32Exception
            End If

            Return rc

        End Function

        ''' <summary>
        ''' Re-enumerates partitions on a disk device with a specified device path. No
        ''' exceptions are thrown on error, but any exceptions from underlying API calls are
        ''' logged to trace log.
        ''' </summary>
        ''' <returns>Returns a value indicating whether operation was successful or not.</returns>
        Public Shared Function UpdateDiskProperties(DevicePath As String) As Boolean

            Try
                Using devicehandle = OpenFileHandle(DevicePath, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open, 0)

                    Dim rc = UnsafeNativeMethods.DeviceIoControl(devicehandle, NativeConstants.IOCTL_DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, 0UI, IntPtr.Zero)

                    If Not rc Then

                        Trace.WriteLine($"Updating disk properties failed for {DevicePath}: {New Win32Exception().Message}")

                    End If

                    Return rc

                End Using

            Catch ex As Exception
                Trace.WriteLine($"Error updating disk properties for {DevicePath}: {ex.JoinMessages()}")

            End Try

            Return False

        End Function

        ''' <summary>
        ''' Opens a disk device with a specified SCSI address and returns both name and an open handle.
        ''' </summary>
        Public Shared Function OpenDiskByScsiAddress(ScsiAddress As SCSI_ADDRESS, AccessMode As FileAccess) As KeyValuePair(Of String, SafeFileHandle)

            Dim dosdevs = QueryDosDevice()

            Dim rawdevices =
            From device In dosdevs
            Where
                device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) OrElse
                device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)

            Dim volumedevices =
                From device In dosdevs
                Where
                    device.Length = 2 AndAlso device(1).Equals(":"c)

            Dim filter =
                Function(diskdevice As String) As KeyValuePair(Of String, SafeFileHandle)

                    Try
                        Dim devicehandle = OpenFileHandle(diskdevice, AccessMode, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

                        Try
                            Dim Address = GetScsiAddress(devicehandle)

                            If Not Address.HasValue OrElse Not Address.Value.Equals(ScsiAddress) Then

                                devicehandle.Dispose()

                                Return Nothing

                            End If

                            Trace.WriteLine($"Found {diskdevice} with SCSI address {Address}")

                            Return New KeyValuePair(Of String, SafeFileHandle)(diskdevice, devicehandle)

                        Catch ex As Exception
                            Trace.WriteLine($"Exception while querying SCSI address for {diskdevice}: {ex.JoinMessages()}")

                            devicehandle.Dispose()

                        End Try

                    Catch ex As Exception
                        Trace.WriteLine($"Exception while opening {diskdevice}: {ex.JoinMessages()}")

                    End Try

                    Return Nothing

                End Function

            Dim dev =
                Aggregate anydevice In rawdevices.Concat(volumedevices)
                    Select seldevice = filter($"\\?\{anydevice}")
                        Into FirstOrDefault(seldevice.Key IsNot Nothing)

            If dev.Key Is Nothing Then
                Throw New DriveNotFoundException($"No physical drive found with SCSI address: {ScsiAddress}")
            End If

            Return dev

        End Function

        ''' <summary>
        ''' Returns a disk device object name for a specified SCSI address.
        ''' </summary>
        <Obsolete("Use PnP features instead to find device names. This method is not guaranteed to return the correct intended device.")>
        Public Shared Function GetDeviceNameByScsiAddressAndSize(scsi_address As SCSI_ADDRESS, disk_size As Long) As String

            Dim dosdevs = QueryDosDevice()

            Dim rawdevices =
            From device In dosdevs
            Where
                device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) OrElse
                device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)

            Dim volumedevices =
            From device In dosdevs
            Where
                device.Length = 2 AndAlso device(1).Equals(":"c)

            Dim filter =
            Function(diskdevice As String) As Boolean

                Try
                    Dim devicehandle = OpenFileHandle($"\\?\{diskdevice}", 0, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

                    Try
                        Dim got_address = GetScsiAddress(devicehandle)

                        If Not got_address.HasValue OrElse Not got_address.Value.Equals(scsi_address) Then

                            Return False

                        End If

                        Trace.WriteLine($"Found {diskdevice} with SCSI address {got_address}")

                        devicehandle.Close()

                        devicehandle = Nothing

                        devicehandle = OpenFileHandle($"\\?\{diskdevice}", FileAccess.Read, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

                        Dim got_size = GetDiskSize(devicehandle)

                        If disk_size = got_size Then
                            Return True
                        End If

                        Trace.WriteLine($"Found {diskdevice} has wrong size. Expected: {disk_size}, got: {got_size}")

                        Return False

                    Catch ex As Exception
                        Trace.WriteLine($"Exception while querying SCSI address for {diskdevice}: {ex.JoinMessages()}")

                    Finally
                        devicehandle?.Dispose()

                    End Try

                Catch ex As Exception
                    Trace.WriteLine($"Exception while opening {diskdevice}: {ex.JoinMessages()}")

                End Try

                Return False

            End Function

            Return rawdevices.Concat(volumedevices).FirstOrDefault(filter)

        End Function

        Public Shared Function TestFileOpen(path As String) As Boolean

            Using handle = UnsafeNativeMethods.CreateFile(path,
                       FileSystemRights.ReadAttributes,
                       0,
                       IntPtr.Zero,
                       NativeConstants.OPEN_EXISTING,
                       0,
                       IntPtr.Zero)

                Return Not handle.IsInvalid

            End Using

        End Function

        Public Shared Sub CreateHardLink(existing As String, newlink As String)

            Win32Try(UnsafeNativeMethods.CreateHardLink(newlink, existing, Nothing))

        End Sub

        Public Shared Sub MoveFile(existing As String, newname As String)

            Win32Try(UnsafeNativeMethods.MoveFile(existing, newname))

        End Sub

        Public Shared Function GetOSVersion() As OperatingSystem

            Dim os_version = OSVERSIONINFOEX.Initalize()

            Dim status = UnsafeNativeMethods.RtlGetVersion(os_version)

            If status < 0 Then
                Throw New Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(status))
            End If

            Return New OperatingSystem(os_version.PlatformId,
                                   New Version(os_version.MajorVersion,
                                               os_version.MinorVersion,
                                               os_version.BuildNumber,
                                               CInt(os_version.ServicePackMajor) << 16 Or CInt(os_version.ServicePackMinor)))

        End Function

        Public Shared Function ParseDoubleTerminatedString(bbuffer As Array, byte_count As Integer) As IEnumerable(Of String)

            If bbuffer Is Nothing Then
                Return Enumerable.Empty(Of String)()
            End If

            byte_count = Math.Min(Buffer.ByteLength(bbuffer), byte_count)

            Dim cbuffer = TryCast(bbuffer, Char())

            If cbuffer Is Nothing Then

                cbuffer = New Char(0 To (byte_count >> 1) - 1) {}

                Buffer.BlockCopy(bbuffer, 0, cbuffer, 0, byte_count)

            End If

            Return ParseDoubleTerminatedString(cbuffer, byte_count >> 1)

        End Function

        Public Shared Iterator Function ParseDoubleTerminatedString(buffer As Char(), length As Integer) As IEnumerable(Of String)

            If buffer Is Nothing Then
                Return
            End If

            length = Math.Min(length, buffer.Length)

            Dim i = 0

            While i < length

                Dim pos = Array.IndexOf(buffer, New Char, i, length - i)

                If pos < 0 Then

                    Yield New String(buffer, i, length - i)
                    Return

                ElseIf pos = i Then

                    Return

                ElseIf pos > i Then

                    Yield New String(buffer, i, pos - i)
                    i = pos + 1

                End If

            End While

        End Function

        <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
        Public Structure SP_DEVINFO_DATA
            Public ReadOnly Property Size As UInt32
            Public ReadOnly Property ClassGuid As Guid
            Public ReadOnly Property DevInst As UInt32
            Public ReadOnly Property Reserved As UIntPtr

            Public Sub Initialize()
                _Size = CUInt(Marshal.SizeOf(Me))
            End Sub
        End Structure

        Public Enum CmClassRegistryProperty As UInt32
            CM_CRP_UPPERFILTERS = &H12
        End Enum

        Public Enum CmDevNodeRegistryProperty As UInt32
            CM_DRP_DEVICEDESC = &H1
            CM_DRP_HARDWAREID = &H2
            CM_DRP_COMPATIBLEIDS = &H3
            CM_DRP_SERVICE = &H5
            CM_DRP_CLASS = &H8
            CM_DRP_CLASSGUID = &H9
            CM_DRP_DRIVER = &HA
            CM_DRP_MFG = &HC
            CM_DRP_FRIENDLYNAME = &HD
            CM_DRP_LOCATION_INFORMATION = &HE
            CM_DRP_PHYSICAL_DEVICE_OBJECT_NAME = &HF
            CM_DRP_UPPERFILTERS = &H12
            CM_DRP_LOWERFILTERS = &H13
        End Enum

        <Flags>
        Public Enum ShutdownFlags As UInt32
            HybridShutdown = &H400000UI
            Logoff = &H0UI
            PowerOff = &H8UI
            Reboot = &H2UI
            RestartApps = &H40UI
            Shutdown = &H1UI
            Force = &H4UI
            ForceIfHung = &H10UI
        End Enum

        <Flags>
        Public Enum ShutdownReasons As UInt32
            ReasonFlagPlanned = &H80000000UI
        End Enum

        <StructLayout(LayoutKind.Sequential, Pack:=1)>
        Public Structure LUID_AND_ATTRIBUTES
            Public Property LUID As Long
            Public Property Attributes As Integer

            Public Overrides Function ToString() As String
                Return $"LUID = 0x{LUID:X}, Attributes = 0x{Attributes:X}"
            End Function
        End Structure

        Public Enum FsInformationClass As UInteger
            FileFsVolumeInformation = 1
            FileFsLabelInformation = 2
            FileFsSizeInformation = 3
            FileFsDeviceInformation = 4
            FileFsAttributeInformation = 5
            FileFsControlInformation = 6
            FileFsFullSizeInformation = 7
            FileFsObjectIdInformation = 8
        End Enum

        Public Enum SystemInformationClass As UInteger
            SystemBasicInformation  '' 0x002C 
            SystemProcessorInformation  '' 0x000C 
            SystemPerformanceInformation    '' 0x0138 
            SystemTimeInformation   '' 0x0020 
            SystemPathInformation   '' Not implemented 
            SystemProcessInformation    '' 0x00C8+ per process 
            SystemCallInformation   '' 0x0018 + (n * 0x0004) 
            SystemConfigurationInformation  '' 0x0018 
            SystemProcessorCounters '' 0x0030 per cpu 
            SystemGlobalFlag        '' 0x0004 (fails If size != 4) 
            SystemCallTimeInformation   '' Not implemented 
            SystemModuleInformation '' 0x0004 + (n * 0x011C) 
            SystemLockInformation   '' 0x0004 + (n * 0x0024) 
            SystemStackTraceInformation '' Not implemented 
            SystemPagedPoolInformation  '' checked build only 
            SystemNonPagedPoolInformation   '' checked build only 
            SystemHandleInformation '' 0x0004 + (n * 0x0010) 
            SystemObjectTypeInformation '' 0x0038+ + (n * 0x0030+) 
            SystemPageFileInformation   '' 0x0018+ per page file 
            SystemVdmInstemulInformation    '' 0x0088 
            SystemVdmBopInformation '' invalid info Class 
            SystemCacheInformation  '' 0x0024 
            SystemPoolTagInformation    '' 0x0004 + (n * 0x001C) 
            SystemInterruptInformation  '' 0x0000 Or 0x0018 per cpu 
            SystemDpcInformation    '' 0x0014 
            SystemFullMemoryInformation '' checked build only 
            SystemLoadDriver        '' 0x0018 Set mode only 
            SystemUnloadDriver      '' 0x0004 Set mode only 
            SystemTimeAdjustmentInformation '' 0x000C 0x0008 writeable 
            SystemSummaryMemoryInformation  '' checked build only 
            SystemNextEventIdInformation    '' checked build only 
            SystemEventIdsInformation   '' checked build only 
            SystemCrashDumpInformation  '' 0x0004 
            SystemExceptionInformation  '' 0x0010 
            SystemCrashDumpStateInformation '' 0x0004 
            SystemDebuggerInformation   '' 0x0002 
            SystemContextSwitchInformation  '' 0x0030 
            SystemRegistryQuotaInformation  '' 0x000C 
            SystemAddDriver     '' 0x0008 Set mode only 
            SystemPrioritySeparationInformation '' 0x0004 Set mode only 
            SystemPlugPlayBusInformation    '' Not implemented 
            SystemDockInformation   '' Not implemented 
            SystemPowerInfo     '' 0x0060 (XP only!) 
            SystemProcessorSpeedInformation '' 0x000C (XP only!) 
            SystemTimeZoneInformation   '' 0x00AC 
            SystemLookasideInformation  '' n * 0x0020 
            SystemSetTimeSlipEvent
            SystemCreateSession '' Set mode only 
            SystemDeleteSession '' Set mode only 
            SystemInvalidInfoClass1 '' invalid info Class 
            SystemRangeStartInformation '' 0x0004 (fails If size != 4) 
            SystemVerifierInformation
            SystemAddVerifier
            SystemSessionProcessesInformation   '' checked build only 
            MaxSystemInfoClass
        End Enum

        Public Enum ObjectInformationClass As UInteger
            ObjectBasicInformation  '' 0 Y N 
            ObjectNameInformation   '' 1 Y N 
            ObjectTypeInformation   '' 2 Y N 
            ObjectAllTypesInformation   '' 3 Y N 
            ObjectHandleInformation '' 4 Y Y 
        End Enum

        <StructLayout(LayoutKind.Sequential)>
        Public Structure SystemHandleTableEntryInformation
            Public ReadOnly Property ProcessId As Integer
            Public ReadOnly Property ObjectType As Byte     '' OB_TYPE_* (OB_TYPE_TYPE, etc.) 
            Public ReadOnly Property Flags As Byte      '' HANDLE_FLAG_* (HANDLE_FLAG_INHERIT, etc.) 
            Public ReadOnly Property Handle As UShort
            Public ReadOnly Property ObjectPtr As IntPtr
            Public ReadOnly Property GrantedAccess As UInteger
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure ObjectTypeInformation
            Public ReadOnly Property Name As UNICODE_STRING
            Public ReadOnly Property ObjectCount As UInteger
            Public ReadOnly Property HandleCount As UInteger
            Private ReadOnly Reserved11 As UInteger
            Private ReadOnly Reserved12 As UInteger
            Private ReadOnly Reserved13 As UInteger
            Private ReadOnly Reserved14 As UInteger
            Public ReadOnly Property PeakObjectCount As UInteger
            Public ReadOnly Property PeakHandleCount As UInteger
            Private ReadOnly Reserved21 As UInteger
            Private ReadOnly Reserved22 As UInteger
            Private ReadOnly Reserved23 As UInteger
            Private ReadOnly Reserved24 As UInteger
            Public ReadOnly Property InvalidAttributes As UInteger
            Public ReadOnly Property GenericRead As UInteger
            Public ReadOnly Property GenericWrite As UInteger
            Public ReadOnly Property GenericExecute As UInteger
            Public ReadOnly Property GenericAll As UInteger
            Public ReadOnly Property ValidAccess As UInteger
            Private ReadOnly Unknown As Byte
            <MarshalAs(UnmanagedType.I1)> Private ReadOnly MaintainHandleDatabase As Boolean
            Private ReadOnly Reserved3 As UShort
            Public ReadOnly Property PoolType As Integer
            Public ReadOnly Property PagedPoolUsage As UInteger
            Public ReadOnly Property NonPagedPoolUsage As UInteger
        End Structure

        Public Enum Win32FileType As Int32
            Unknown = &H0
            Disk = &H1
            Character = &H2
            Pipe = &H3
            Remote = &H8000
        End Enum

        Public Enum StdHandle As Int32
            Input = -10
            Output = -11
            [Error] = -12
        End Enum

        <StructLayout(LayoutKind.Sequential)>
        Public Structure COORD
            Public ReadOnly Property X As Short
            Public ReadOnly Property Y As Short
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure SMALL_RECT
            Public ReadOnly Property Left As Short
            Public ReadOnly Property Top As Short
            Public ReadOnly Property Right As Short
            Public ReadOnly Property Bottom As Short
            Public ReadOnly Property Width As Short
                Get
                    Return _Right - _Left + 1S
                End Get
            End Property
            Public ReadOnly Property Height As Short
                Get
                    Return _Bottom - _Top + 1S
                End Get
            End Property
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure CONSOLE_SCREEN_BUFFER_INFO
            Public ReadOnly Property Size As COORD
            Public ReadOnly Property CursorPosition As COORD
            Public ReadOnly Property Attributes As Short
            Public ReadOnly Property Window As SMALL_RECT
            Public ReadOnly Property MaximumWindowSize As COORD
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure SERVICE_STATUS
            Public ReadOnly Property ServiceType As Integer
            Public ReadOnly Property CurrentState As Integer
            Public ReadOnly Property ControlsAccepted As Integer
            Public ReadOnly Property Win32ExitCode As Integer
            Public ReadOnly Property ServiceSpecificExitCode As Integer
            Public ReadOnly Property CheckPoint As Integer
            Public ReadOnly Property WaitHint As Integer
        End Structure

        <Flags>
        Public Enum DEFINE_DOS_DEVICE_FLAGS As UInt32
            DDD_EXACT_MATCH_ON_REMOVE = &H4
            DDD_NO_BROADCAST_SYSTEM = &H8
            DDD_RAW_TARGET_PATH = &H1
            DDD_REMOVE_DEFINITION = &H2
        End Enum

        ''' <summary>
        ''' Encapsulates a Service Control Management object handle that is closed by calling CloseServiceHandle() Win32 API.
        ''' </summary>
        Public NotInheritable Class SafeServiceHandle
            Inherits SafeHandleZeroOrMinusOneIsInvalid

            Private Declare Auto Function CloseServiceHandle Lib "advapi32.dll" (
              hSCObject As IntPtr) As Boolean

            ''' <summary>
            ''' Initiates a new instance with an existing open handle.
            ''' </summary>
            ''' <param name="open_handle">Existing open handle.</param>
            ''' <param name="owns_handle">Indicates whether handle should be closed when this
            ''' instance is released.</param>
            Public Sub New(open_handle As IntPtr, owns_handle As Boolean)
                MyBase.New(owns_handle)

                SetHandle(open_handle)
            End Sub

            ''' <summary>
            ''' Creates a new empty instance. This constructor is used by native to managed
            ''' handle marshaller.
            ''' </summary>
            Private Sub New()
                MyBase.New(ownsHandle:=True)

            End Sub

            ''' <summary>
            ''' Closes contained handle by calling CloseServiceHandle() Win32 API.
            ''' </summary>
            ''' <returns>Return value from CloseServiceHandle() Win32 API.</returns>
            Protected Overrides Function ReleaseHandle() As Boolean
                Return CloseServiceHandle(handle)
            End Function
        End Class

        ''' <summary>
        ''' Encapsulates a FindVolume handle that is closed by calling FindVolumeClose() Win32 API.
        ''' </summary>
        Public NotInheritable Class SafeFindVolumeHandle
            Inherits SafeHandleMinusOneIsInvalid

            Private Declare Auto Function FindVolumeClose Lib "kernel32.dll" (
              h As IntPtr) As Boolean

            ''' <summary>
            ''' Initiates a new instance with an existing open handle.
            ''' </summary>
            ''' <param name="open_handle">Existing open handle.</param>
            ''' <param name="owns_handle">Indicates whether handle should be closed when this
            ''' instance is released.</param>
            Public Sub New(open_handle As IntPtr, owns_handle As Boolean)
                MyBase.New(owns_handle)

                SetHandle(open_handle)
            End Sub

            ''' <summary>
            ''' Creates a new empty instance. This constructor is used by native to managed
            ''' handle marshaller.
            ''' </summary>
            Private Sub New()
                MyBase.New(ownsHandle:=True)

            End Sub

            ''' <summary>
            ''' Closes contained handle by calling CloseServiceHandle() Win32 API.
            ''' </summary>
            ''' <returns>Return value from CloseServiceHandle() Win32 API.</returns>
            Protected Overrides Function ReleaseHandle() As Boolean
                Return FindVolumeClose(handle)
            End Function
        End Class

        ''' <summary>
        ''' Encapsulates a FindVolumeMountPoint handle that is closed by calling FindVolumeMountPointClose () Win32 API.
        ''' </summary>
        Public NotInheritable Class SafeFindVolumeMountPointHandle
            Inherits SafeHandleMinusOneIsInvalid

            Private Declare Auto Function FindVolumeMountPointClose Lib "kernel32.dll" (
              h As IntPtr) As Boolean

            ''' <summary>
            ''' Initiates a new instance with an existing open handle.
            ''' </summary>
            ''' <param name="open_handle">Existing open handle.</param>
            ''' <param name="owns_handle">Indicates whether handle should be closed when this
            ''' instance is released.</param>
            Public Sub New(open_handle As IntPtr, owns_handle As Boolean)
                MyBase.New(owns_handle)

                SetHandle(open_handle)
            End Sub

            ''' <summary>
            ''' Creates a new empty instance. This constructor is used by native to managed
            ''' handle marshaller.
            ''' </summary>
            Private Sub New()
                MyBase.New(ownsHandle:=True)

            End Sub

            ''' <summary>
            ''' Closes contained handle by calling CloseServiceHandle() Win32 API.
            ''' </summary>
            ''' <returns>Return value from CloseServiceHandle() Win32 API.</returns>
            Protected Overrides Function ReleaseHandle() As Boolean
                Return FindVolumeMountPointClose(handle)
            End Function
        End Class

        ''' <summary>
        ''' Encapsulates a SetupAPI hInf handle that is closed by calling SetupCloseInf() API.
        ''' </summary>
        Public NotInheritable Class SafeInfHandle
            Inherits SafeHandleMinusOneIsInvalid

            Private Declare Auto Sub SetupCloseInfFile Lib "setupapi.dll" (
              hInf As IntPtr)

            ''' <summary>
            ''' Initiates a new instance with an existing open handle.
            ''' </summary>
            ''' <param name="open_handle">Existing open handle.</param>
            ''' <param name="owns_handle">Indicates whether handle should be closed when this
            ''' instance is released.</param>
            Public Sub New(open_handle As IntPtr, owns_handle As Boolean)
                MyBase.New(owns_handle)

                SetHandle(open_handle)
            End Sub

            ''' <summary>
            ''' Creates a new empty instance. This constructor is used by native to managed
            ''' handle marshaller.
            ''' </summary>
            Private Sub New()
                MyBase.New(ownsHandle:=True)

            End Sub

            ''' <summary>
            ''' Closes contained handle by calling CloseServiceHandle() Win32 API.
            ''' </summary>
            ''' <returns>Return value from CloseServiceHandle() Win32 API.</returns>
            Protected Overrides Function ReleaseHandle() As Boolean
                SetupCloseInfFile(handle)
                Return True
            End Function
        End Class

        ''' <summary>
        ''' Encapsulates a SetupAPI hInf handle that is closed by calling SetupCloseInf() API.
        ''' </summary>
        Public NotInheritable Class SafeDeviceInfoSetHandle
            Inherits SafeHandleMinusOneIsInvalid

            Private Declare Auto Function SetupDiDestroyDeviceInfoList Lib "setupapi.dll" (
              handle As IntPtr) As Boolean

            ''' <summary>
            ''' Initiates a new instance with an existing open handle.
            ''' </summary>
            ''' <param name="open_handle">Existing open handle.</param>
            ''' <param name="owns_handle">Indicates whether handle should be closed when this
            ''' instance is released.</param>
            Public Sub New(open_handle As IntPtr, owns_handle As Boolean)
                MyBase.New(owns_handle)

                SetHandle(open_handle)
            End Sub

            ''' <summary>
            ''' Creates a new empty instance. This constructor is used by native to managed
            ''' handle marshaller.
            ''' </summary>
            Private Sub New()
                MyBase.New(ownsHandle:=True)

            End Sub

            ''' <summary>
            ''' Closes contained handle by calling CloseServiceHandle() Win32 API.
            ''' </summary>
            ''' <returns>Return value from CloseServiceHandle() Win32 API.</returns>
            Protected Overrides Function ReleaseHandle() As Boolean
                Return SetupDiDestroyDeviceInfoList(handle)
            End Function
        End Class

        Public Class HGlobalBuffer
            Inherits SafeBuffer

            Public Sub New(numBytes As IntPtr)
                MyBase.New(ownsHandle:=True)
                Dim ptr = Marshal.AllocHGlobal(numBytes)
                MyBase.SetHandle(ptr)
                MyBase.Initialize(CULng(numBytes))
            End Sub

            Public Sub New(numBytes As Integer)
                MyBase.New(ownsHandle:=True)
                Dim ptr = Marshal.AllocHGlobal(numBytes)
                MyBase.SetHandle(ptr)
                MyBase.Initialize(CULng(numBytes))
            End Sub

            Public Sub New(address As IntPtr, numBytes As ULong, ownsHandle As Boolean)
                MyBase.New(ownsHandle)
                MyBase.SetHandle(address)
                MyBase.Initialize(numBytes)
            End Sub

            Public Sub Resize(newSize As Integer)
                If handle <> IntPtr.Zero Then
                    Marshal.FreeHGlobal(handle)
                End If
                handle = Marshal.AllocHGlobal(newSize)
                MyBase.Initialize(CULng(newSize))
            End Sub

            Public Sub Resize(newSize As IntPtr)
                If handle <> IntPtr.Zero Then
                    Marshal.FreeHGlobal(handle)
                End If
                handle = Marshal.AllocHGlobal(newSize)
                MyBase.Initialize(CULng(newSize))
            End Sub

            Protected Overrides Function ReleaseHandle() As Boolean
                Try
                    Marshal.FreeHGlobal(handle)
                    Return True

                Catch
                    Return False

                End Try
            End Function

        End Class

        <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
        Public Structure FindStreamData

            Public ReadOnly Property StreamSize As Int64

            <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=296)>
            Private ReadOnly _streamName As String

            Public ReadOnly Property NamePart As String
                Get
                    Return _streamName?.Split(":"c).ElementAtOrDefault(1)
                End Get
            End Property

            Public ReadOnly Property TypePart As String
                Get
                    Return _streamName?.Split(":"c).ElementAtOrDefault(2)
                End Get
            End Property

            Public ReadOnly Property StreamName As String
                Get
                    Return _streamName
                End Get
            End Property
        End Structure

        ''' <summary>
        ''' Encapsulates a FindVolumeMountPoint handle that is closed by calling FindVolumeMountPointClose () Win32 API.
        ''' </summary>
        <SecurityCritical>
        <SecurityPermission(SecurityAction.Demand, Flags:=SecurityPermissionFlag.AllFlags)>
        Public NotInheritable Class SafeFindHandle
            Inherits SafeHandleMinusOneIsInvalid

            Private Declare Auto Function FindClose Lib "kernel32.dll" (
              h As IntPtr) As Boolean

            ''' <summary>
            ''' Initiates a new instance with an existing open handle.
            ''' </summary>
            ''' <param name="open_handle">Existing open handle.</param>
            ''' <param name="owns_handle">Indicates whether handle should be closed when this
            ''' instance is released.</param>
            <SecurityCritical>
            Public Sub New(open_handle As IntPtr, owns_handle As Boolean)
                MyBase.New(owns_handle)

                SetHandle(open_handle)
            End Sub

            ''' <summary>
            ''' Creates a new empty instance. This constructor is used by native to managed
            ''' handle marshaller.
            ''' </summary>
            <SecurityCritical>
            Private Sub New()
                MyBase.New(ownsHandle:=True)

            End Sub

            ''' <summary>
            ''' Closes contained handle by calling FindClose() Win32 API.
            ''' </summary>
            ''' <returns>Return value from FindClose() Win32 API.</returns>
            <SecurityCritical>
            Protected Overrides Function ReleaseHandle() As Boolean
                Return FindClose(handle)
            End Function
        End Class

        <StructLayout(LayoutKind.Sequential, Pack:=1)>
        Public Structure ByHandleFileInformation
            Public ReadOnly Property FileAttributes As FileAttributes
            Private ReadOnly ftCreationTime As Long
            Private ReadOnly ftLastAccessTime As Long
            Private ReadOnly ftLastWriteTime As Long
            Public ReadOnly Property VolumeSerialNumber As UInteger
            Private ReadOnly nFileSizeHigh As Integer
            Private ReadOnly nFileSizeLow As UInteger
            Public ReadOnly Property NumberOfLinks As Integer
            Private ReadOnly nFileIndexHigh As UInteger
            Private ReadOnly nFileIndexLow As UInteger

            Public ReadOnly Property CreationTime As Date
                Get
                    Return Date.FromFileTime(ftCreationTime)
                End Get
            End Property

            Public ReadOnly Property LastAccessTime As Date
                Get
                    Return Date.FromFileTime(ftLastAccessTime)
                End Get
            End Property

            Public ReadOnly Property LastWriteTime As Date
                Get
                    Return Date.FromFileTime(ftLastWriteTime)
                End Get
            End Property

            Public ReadOnly Property FileSize As Long
                Get
                    Return (CLng(nFileSizeHigh) << 32) Or nFileSizeLow
                End Get
            End Property

            Public ReadOnly Property FileIndexAndSequence As ULong
                Get
                    Return (CULng(nFileIndexHigh) << 32) Or nFileIndexLow
                End Get
            End Property

            Public ReadOnly Property FileIndex As Long
                Get
                    Return ((nFileIndexHigh And &HFFFFL) << 32) Or nFileIndexLow
                End Get
            End Property

            Public ReadOnly Property Sequence As UShort
                Get
                    Return CUShort(nFileIndexHigh >> 16)
                End Get
            End Property

            Public Shared Function FromHandle(handle As SafeFileHandle) As ByHandleFileInformation

                Dim obj As New ByHandleFileInformation

                Win32Try(UnsafeNativeMethods.GetFileInformationByHandle(handle, obj))

                Return obj

            End Function

        End Structure

        Public Enum DeviceType

            CdRom = &H2

            Disk = &H7

        End Enum

        <StructLayout(LayoutKind.Sequential)>
        Public Structure STORAGE_DEVICE_NUMBER

            Public ReadOnly Property DeviceType As DeviceType

            Public ReadOnly Property DeviceNumber As UInt32

            Public ReadOnly Property PartitionNumber As Int32

        End Structure

        Public Enum STORAGE_PROPERTY_ID
            StorageDeviceProperty = 0
            StorageAdapterProperty
            StorageDeviceIdProperty
            StorageDeviceUniqueIdProperty
            StorageDeviceWriteCacheProperty
            StorageMiniportProperty
            StorageAccessAlignmentProperty
            StorageDeviceSeekPenaltyProperty
            StorageDeviceTrimProperty
            StorageDeviceWriteAggregationProperty
        End Enum

        Public Enum STORAGE_QUERY_TYPE
            PropertyStandardQuery = 0          '' Retrieves the descriptor
            PropertyExistsQuery                '' Used To test whether the descriptor Is supported
            PropertyMaskQuery                  '' Used To retrieve a mask Of writable fields In the descriptor
            PropertyQueryMaxDefined            '' use To validate the value
        End Enum

        <StructLayout(LayoutKind.Sequential)>
        Public Structure STORAGE_PROPERTY_QUERY

            Public ReadOnly Property PropertyId As STORAGE_PROPERTY_ID

            Public ReadOnly Property QueryType As STORAGE_QUERY_TYPE

            Private ReadOnly _additional As Byte

            Public Sub New(PropertyId As STORAGE_PROPERTY_ID, QueryType As STORAGE_QUERY_TYPE)
                _PropertyId = PropertyId
                _QueryType = QueryType
            End Sub

        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure STORAGE_DESCRIPTOR_HEADER

            Public ReadOnly Property Version As UInteger

            Public ReadOnly Property Size As UInteger

        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure DEVICE_TRIM_DESCRIPTOR

            Public ReadOnly Property Header As STORAGE_DESCRIPTOR_HEADER

            Public ReadOnly Property TrimEnabled As Byte

        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure STORAGE_DEVICE_DESCRIPTOR

            Public ReadOnly Property Header As STORAGE_DESCRIPTOR_HEADER

            Public ReadOnly Property DeviceType As Byte

            Public ReadOnly Property DeviceTypeModifier As Byte

            Public ReadOnly Property RemovableMedia As Byte

            Public ReadOnly Property CommandQueueing As Byte

            Public ReadOnly Property VendorIdOffset As Integer

            Public ReadOnly Property ProductIdOffset As Integer

            Public ReadOnly Property ProductRevisionOffset As Integer

            Public ReadOnly Property SerialNumberOffset As Integer

            Public ReadOnly Property StorageBusType As Byte

            Public ReadOnly Property RawPropertiesLength As Integer

        End Structure

        Public Structure StorageStandardProperties

            Public ReadOnly Property DeviceDescriptor As STORAGE_DEVICE_DESCRIPTOR

            Public ReadOnly Property VendorId As String
            Public ReadOnly Property ProductId As String
            Public ReadOnly Property ProductRevision As String
            Public ReadOnly Property SerialNumber As String

            Public ReadOnly Property RawProperties As ReadOnlyCollection(Of Byte)

            Public Sub New(buffer As SafeBuffer)
                _DeviceDescriptor = buffer.Read(Of STORAGE_DEVICE_DESCRIPTOR)(0)

                If _DeviceDescriptor.ProductIdOffset <> 0 Then
                    _ProductId = Marshal.PtrToStringAnsi(buffer.DangerousGetHandle() + _DeviceDescriptor.ProductIdOffset)
                End If

                If _DeviceDescriptor.VendorIdOffset <> 0 Then
                    _VendorId = Marshal.PtrToStringAnsi(buffer.DangerousGetHandle() + _DeviceDescriptor.VendorIdOffset)
                End If

                If _DeviceDescriptor.SerialNumberOffset <> 0 Then
                    _SerialNumber = Marshal.PtrToStringAnsi(buffer.DangerousGetHandle() + _DeviceDescriptor.SerialNumberOffset)
                End If

                If _DeviceDescriptor.ProductRevisionOffset <> 0 Then
                    _ProductRevision = Marshal.PtrToStringAnsi(buffer.DangerousGetHandle() + _DeviceDescriptor.ProductRevisionOffset)
                End If

                If _DeviceDescriptor.RawPropertiesLength <> 0 Then
                    Dim RawProperties(0 To _DeviceDescriptor.RawPropertiesLength - 1) As Byte
                    Marshal.Copy(buffer.DangerousGetHandle() + PinnedBuffer(Of STORAGE_DEVICE_DESCRIPTOR).TypeSize, RawProperties, 0, _DeviceDescriptor.RawPropertiesLength)
                    _RawProperties = Array.AsReadOnly(RawProperties)
                End If

            End Sub

        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure SCSI_ADDRESS
            Implements IEquatable(Of SCSI_ADDRESS)

            Public ReadOnly Property Length As UInt32
            Public ReadOnly Property PortNumber As Byte
            Public ReadOnly Property PathId As Byte
            Public ReadOnly Property TargetId As Byte
            Public ReadOnly Property Lun As Byte

            Public Sub New(PortNumber As Byte, DWordDeviceNumber As UInt32)
                Me.Length = CUInt(Marshal.SizeOf(Me))
                Me.PortNumber = PortNumber
                Me.DWordDeviceNumber = DWordDeviceNumber
            End Sub

            Public Sub New(DWordDeviceNumber As UInt32)
                Me.Length = CUInt(Marshal.SizeOf(Me))
                Me.DWordDeviceNumber = DWordDeviceNumber
            End Sub

            Public Property DWordDeviceNumber As UInt32
                Get
                    Return CUInt(_PathId) Or (CUInt(_TargetId) << 8) Or (CUInt(_Lun) << 16)
                End Get
                Set
                    _PathId = CByte(Value And &HFF)
                    _TargetId = CByte((Value >> 8) And &HFF)
                    _Lun = CByte((Value >> 16) And &HFF)
                End Set
            End Property

            Public Overrides Function ToString() As String
                Return $"Port = {_PortNumber}, Path = {_PathId}, Target = {_TargetId}, Lun = {_Lun}"
            End Function

            Public Overloads Function Equals(other As SCSI_ADDRESS) As Boolean Implements IEquatable(Of SCSI_ADDRESS).Equals
                Return _
                    _PortNumber.Equals(other._PortNumber) AndAlso
                    _PathId.Equals(other._PathId) AndAlso
                    _TargetId.Equals(other._TargetId) AndAlso
                    _Lun.Equals(other._Lun)
            End Function

            Public Overrides Function Equals(obj As Object) As Boolean
                If TypeOf obj IsNot SCSI_ADDRESS Then
                    Return False
                End If

                Return Equals(DirectCast(obj, SCSI_ADDRESS))
            End Function

            Public Overrides Function GetHashCode() As Integer
                Return CInt(_PathId) Or (CInt(_TargetId) << 8) Or (CInt(_Lun) << 16)
            End Function

            Public Shared Operator =(first As SCSI_ADDRESS, second As SCSI_ADDRESS) As Boolean
                Return first.Equals(second)
            End Operator

            Public Shared Operator <>(first As SCSI_ADDRESS, second As SCSI_ADDRESS) As Boolean
                Return Not first.Equals(second)
            End Operator

        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Structure DISK_GEOMETRY
            Public Enum MEDIA_TYPE As Int32
                Unknown = &H0
                F5_1Pt2_512 = &H1
                F3_1Pt44_512 = &H2
                F3_2Pt88_512 = &H3
                F3_20Pt8_512 = &H4
                F3_720_512 = &H5
                F5_360_512 = &H6
                F5_320_512 = &H7
                F5_320_1024 = &H8
                F5_180_512 = &H9
                F5_160_512 = &HA
                RemovableMedia = &HB
                FixedMedia = &HC
                F3_120M_512 = &HD
                F3_640_512 = &HE
                F5_640_512 = &HF
                F5_720_512 = &H10
                F3_1Pt2_512 = &H11
                F3_1Pt23_1024 = &H12
                F5_1Pt23_1024 = &H13
                F3_128Mb_512 = &H14
                F3_230Mb_512 = &H15
                F8_256_128 = &H16
                F3_200Mb_512 = &H17
                F3_240M_512 = &H18
                F3_32M_512 = &H19
            End Enum

            Public ReadOnly Property Cylinders As Int64
            Public ReadOnly Property MediaType As MEDIA_TYPE
            Public ReadOnly Property TracksPerCylinder As Int32
            Public ReadOnly Property SectorsPerTrack As Int32
            Public ReadOnly Property BytesPerSector As Int32
        End Structure

        <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
        Public Structure OSVERSIONINFO
            Public ReadOnly Property OSVersionInfoSize As Int32
            Public ReadOnly Property MajorVersion As Int32
            Public ReadOnly Property MinorVersion As Int32
            Public ReadOnly Property BuildNumber As Int32
            Public ReadOnly Property PlatformId As PlatformID

            Public ReadOnly Property CSDVersion As String
                Get
                    Return _cSDVersion
                End Get
            End Property

            <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=128)>
            Private ReadOnly _cSDVersion As String

            Public Shared Function Initalize() As OSVERSIONINFO
                Return New OSVERSIONINFO() With {
                ._OSVersionInfoSize = PinnedBuffer(Of OSVERSIONINFO).TypeSize
            }
            End Function
        End Structure

        <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
        Public Structure OSVERSIONINFOEX
            Public ReadOnly Property OSVersionInfoSize As Int32
            Public ReadOnly Property MajorVersion As Int32
            Public ReadOnly Property MinorVersion As Int32
            Public ReadOnly Property BuildNumber As Int32
            Public ReadOnly Property PlatformId As PlatformID

            <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=128)>
            Private ReadOnly _cSDVersion As String

            Public ReadOnly Property ServicePackMajor As UShort

            Public ReadOnly Property ServicePackMinor As UShort

            Public ReadOnly Property SuiteMask As Short

            Public ReadOnly Property ProductType As Byte

            Public ReadOnly Property Reserved As Byte

            Public ReadOnly Property CSDVersion As String
                Get
                    Return _cSDVersion
                End Get
            End Property

            Public Shared Function Initalize() As OSVERSIONINFOEX
                Return New OSVERSIONINFOEX() With {
                ._OSVersionInfoSize = PinnedBuffer(Of OSVERSIONINFOEX).TypeSize
            }
            End Function
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure ObjectAttributes

            Private ReadOnly uLength As Int32

            Public ReadOnly Property RootDirectory As IntPtr

            Public ReadOnly Property ObjectName As IntPtr

            Public ReadOnly Property Attributes As NtObjectAttributes

            Public ReadOnly Property SecurityDescriptor As IntPtr

            Public ReadOnly Property SecurityQualityOfService As IntPtr

            Public Sub New(root_directory As IntPtr, object_name As IntPtr, object_attributes As NtObjectAttributes, security_descriptor As IntPtr, security_quality_of_service As IntPtr)
                uLength = Marshal.SizeOf(Me)
                RootDirectory = root_directory
                ObjectName = object_name
                Attributes = object_attributes
                SecurityDescriptor = security_descriptor
                SecurityQualityOfService = security_quality_of_service

            End Sub

        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure IoStatusBlock

            Public ReadOnly Property Status As IntPtr

            Public ReadOnly Property Information As IntPtr

        End Structure

        <Flags>
        Public Enum NtObjectAttributes

            Inherit = &H2
            Permanent = &H10
            Exclusive = &H20
            CaseInsensitive = &H40
            OpenIf = &H80
            OpenLink = &H100

        End Enum

        Public Enum NtCreateDisposition

            Supersede = &H0
            Open = &H1
            Create = &H2
            OpenIf = &H3
            Overwrite = &H4
            OverwriteIf = &H5

        End Enum

        <Flags>
        Public Enum NtCreateOptions

            DirectoryFile = &H1
            WriteThrough = &H2
            SequentialOnly = &H4
            NoIntermediateBuffering = &H8

            SynchronousIoAlert = &H10
            SynchronousIoNonAlert = &H20
            NonDirectoryFile = &H40
            CreateTreeConnection = &H80

            CompleteIfOpLocked = &H100
            NoEAKnowledge = &H200
            OpenForRecovery = &H400
            RandomAccess = &H800

            DeleteOnClose = &H1000
            OpenByFileId = &H200
            OpenForBackupIntent = &H400
            NoCompression = &H8000

            ReserverNoOpFilter = &H100000
            OpenReparsePoint = &H200000
            OpenNoRecall = &H400000

        End Enum

        <StructLayout(LayoutKind.Sequential)>
        Public Structure MOUNTMGR_MOUNT_POINT

            Public ReadOnly Property SymbolicLinkNameOffset As Integer
            Public ReadOnly Property SymbolicLinkNameLength As UShort
            Public ReadOnly Property Reserved1 As UShort
            Public ReadOnly Property UniqueIdOffset As Integer
            Public ReadOnly Property UniqueIdLength As UShort
            Public ReadOnly Property Reserved2 As UShort
            Public ReadOnly Property DeviceNameOffset As Integer
            Public ReadOnly Property DeviceNameLength As UShort
            Public ReadOnly Property Reserved3 As UShort

            Public Sub New(device_name As String)
                _DeviceNameOffset = Marshal.SizeOf(Me)
                _DeviceNameLength = CUShort(device_name.Length << 1)

            End Sub

        End Structure

        <StructLayout(LayoutKind.Sequential, Pack:=8)>
        Public Structure PARTITION_INFORMATION

            <Flags>
            Public Enum PARTITION_TYPE As Byte
                PARTITION_ENTRY_UNUSED = &H0      ' Entry unused
                PARTITION_FAT_12 = &H1      ' 12-bit FAT entries
                PARTITION_XENIX_1 = &H2      ' Xenix
                PARTITION_XENIX_2 = &H3      ' Xenix
                PARTITION_FAT_16 = &H4      ' 16-bit FAT entries
                PARTITION_EXTENDED = &H5      ' Extended partition entry
                PARTITION_HUGE = &H6      ' Huge partition MS-DOS V4
                PARTITION_IFS = &H7      ' IFS Partition
                PARTITION_OS2BOOTMGR = &HA      ' OS/2 Boot Manager/OPUS/Coherent swap
                PARTITION_FAT32 = &HB      ' FAT32
                PARTITION_FAT32_XINT13 = &HC      ' FAT32 using extended int13 services
                PARTITION_XINT13 = &HE      ' Win95 partition using extended int13 services
                PARTITION_XINT13_EXTENDED = &HF      ' Same as type 5 but uses extended int13 services
                PARTITION_PREP = &H41      ' PowerPC Reference Platform (PReP) Boot Partition
                PARTITION_LDM = &H42      ' Logical Disk Manager partition
                PARTITION_UNIX = &H63      ' Unix
                PARTITION_NTFT = &H80      ' NTFT partition      
            End Enum

            Public ReadOnly Property StartingOffset As Int64
            Public ReadOnly Property PartitionLength As Int64
            Public ReadOnly Property HiddenSectors As UInt32
            Public ReadOnly Property PartitionNumber As UInt32
            Public ReadOnly Property PartitionType As PARTITION_TYPE
            Public ReadOnly Property BootIndicator As Byte
            Public ReadOnly Property RecognizedPartition As Byte
            Public ReadOnly Property RewritePartition As Byte

            ''' <summary>
            ''' Indicates whether this partition entry represents a Windows NT fault tolerant partition,
            ''' such as mirror or stripe set.
            ''' </summary>
            ''' <value>
            ''' Indicates whether this partition entry represents a Windows NT fault tolerant partition,
            ''' such as mirror or stripe set.
            ''' </value>
            ''' <returns>True if this partition entry represents a Windows NT fault tolerant partition,
            ''' such as mirror or stripe set. False otherwise.</returns>
            Public ReadOnly Property IsFTPartition As Boolean
                Get
                    Return _PartitionType.HasFlag(PARTITION_TYPE.PARTITION_NTFT)
                End Get
            End Property

            ''' <summary>
            ''' If this partition entry represents a Windows NT fault tolerant partition, such as mirror or stripe,
            ''' set, then this property returns partition subtype, such as PARTITION_IFS for NTFS or HPFS
            ''' partitions.
            ''' </summary>
            ''' <value>
            ''' If this partition entry represents a Windows NT fault tolerant partition, such as mirror or stripe,
            ''' set, then this property returns partition subtype, such as PARTITION_IFS for NTFS or HPFS
            ''' partitions.
            ''' </value>
            ''' <returns>If this partition entry represents a Windows NT fault tolerant partition, such as mirror or
            ''' stripe, set, then this property returns partition subtype, such as PARTITION_IFS for NTFS or HPFS
            ''' partitions.</returns>
            Public ReadOnly Property FTPartitionSubType As PARTITION_TYPE
                Get
                    Return _PartitionType And Not PARTITION_TYPE.PARTITION_NTFT
                End Get
            End Property

            ''' <summary>
            ''' Indicates whether this partition entry represents a container partition, also known as extended
            ''' partition, where an extended partition table can be found in first sector.
            ''' </summary>
            ''' <value>
            ''' Indicates whether this partition entry represents a container partition.
            ''' </value>
            ''' <returns>True if this partition entry represents a container partition. False otherwise.</returns>
            Public ReadOnly Property IsContainerPartition As Boolean
                Get
                    Return (_PartitionType = PARTITION_TYPE.PARTITION_EXTENDED) OrElse (_PartitionType = PARTITION_TYPE.PARTITION_XINT13_EXTENDED)
                End Get
            End Property
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure DRIVE_LAYOUT_INFORMATION_EX
            Public Sub New(PartitionStyle As PARTITION_STYLE, PartitionCount As Integer)
                _PartitionStyle = PartitionStyle
                _PartitionCount = PartitionCount
            End Sub

            Public ReadOnly Property PartitionStyle As PARTITION_STYLE
            Public ReadOnly Property PartitionCount As Integer
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure DRIVE_LAYOUT_INFORMATION_MBR
            Public Sub New(DiskSignature As UInteger)
                _DiskSignature = DiskSignature
            End Sub

            Public ReadOnly Property DiskSignature As UInteger
            Public ReadOnly Property Checksum As UInteger

            Public Overrides Function GetHashCode() As Integer
                Return _DiskSignature.GetHashCode()
            End Function

            Public Overrides Function ToString() As String
                Return _DiskSignature.ToString("X8")
            End Function
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure DRIVE_LAYOUT_INFORMATION_GPT
            Public ReadOnly Property DiskId As Guid
            Public ReadOnly Property StartingUsableOffset As Long
            Public ReadOnly Property UsableLength As Long
            Public ReadOnly Property MaxPartitionCount As Integer

            Public Overrides Function GetHashCode() As Integer
                Return _DiskId.GetHashCode()
            End Function

            Public Overrides Function ToString() As String
                Return _DiskId.ToString("b")
            End Function
        End Structure

        Public Enum PARTITION_STYLE As Byte
            PARTITION_STYLE_MBR
            PARTITION_STYLE_GPT
            PARTITION_STYLE_RAW
        End Enum

        <StructLayout(LayoutKind.Sequential)>
        Public Structure CREATE_DISK_MBR
            <MarshalAs(UnmanagedType.I1)>
            Private _partitionStyle As PARTITION_STYLE

            Public Property DiskSignature As UInteger

            Public Property PartitionStyle As PARTITION_STYLE
                Get
                    Return _partitionStyle
                End Get
                Set(value As PARTITION_STYLE)
                    _partitionStyle = value
                End Set
            End Property
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure CREATE_DISK_GPT
            <MarshalAs(UnmanagedType.I1)>
            Private _partitionStyle As PARTITION_STYLE

            Public Property DiskId As Guid

            Public Property MaxPartitionCount As Integer

            Public Property PartitionStyle As PARTITION_STYLE
                Get
                    Return _partitionStyle
                End Get
                Set(value As PARTITION_STYLE)
                    _partitionStyle = value
                End Set
            End Property
        End Structure

        <StructLayout(LayoutKind.Sequential, Pack:=8)>
        Public Structure PARTITION_INFORMATION_EX

            <MarshalAs(UnmanagedType.I1)>
            Private ReadOnly _partitionStyle As PARTITION_STYLE
            Public ReadOnly Property StartingOffset As Int64
            Public ReadOnly Property PartitionLength As Int64
            Public ReadOnly Property PartitionNumber As UInt32
            <MarshalAs(UnmanagedType.I1)>
            Private ReadOnly _rewritePartition As Boolean

            Private ReadOnly padding1 As Byte
            Private ReadOnly padding2 As Byte
            Private ReadOnly padding3 As Byte

            <MarshalAs(UnmanagedType.ByValArray, SizeConst:=112)>
            Private fields As Byte()

            Public Property MBR As PARTITION_INFORMATION_MBR
                Get
                    Return PinnedBuffer.Deserialize(Of PARTITION_INFORMATION_MBR)(fields)
                End Get
                Set
                    Using buffer As New PinnedBuffer(Of Byte)(112)
                        buffer.Write(0, Value)
                        fields = buffer.Target
                    End Using
                End Set
            End Property

            Public Property GPT As PARTITION_INFORMATION_GPT
                Get
                    Return PinnedBuffer.Deserialize(Of PARTITION_INFORMATION_GPT)(fields)
                End Get
                Set
                    Using buffer As New PinnedBuffer(Of Byte)(112)
                        buffer.Write(0, Value)
                        fields = buffer.Target
                    End Using
                End Set
            End Property

            Public ReadOnly Property RewritePartition As Boolean
                Get
                    Return _rewritePartition
                End Get
            End Property

            Public ReadOnly Property PartitionStyle As PARTITION_STYLE
                Get
                    Return _partitionStyle
                End Get
            End Property
        End Structure

        <StructLayout(LayoutKind.Sequential, Pack:=4)>
        Public Structure PARTITION_INFORMATION_MBR

            Public ReadOnly Property PartitionType As Byte

            <MarshalAs(UnmanagedType.I1)>
            Private ReadOnly _bootIndicator As Boolean

            <MarshalAs(UnmanagedType.I1)>
            Private ReadOnly _recognizedPartition As Boolean

            Public ReadOnly Property HiddenSectors As Integer

            Public ReadOnly Property RecognizedPartition As Boolean
                Get
                    Return _recognizedPartition
                End Get
            End Property

            Public ReadOnly Property BootIndicator As Boolean
                Get
                    Return _bootIndicator
                End Get
            End Property
        End Structure

        <StructLayout(LayoutKind.Sequential, Pack:=4)>
        Public Structure PARTITION_INFORMATION_GPT

            Public ReadOnly Property PartitionType As Guid

            Public ReadOnly Property PartitionId As Guid

            Public ReadOnly Property Attributes As GptAttributes

            Private ReadOnly _name0 As Long
            Private ReadOnly _name1 As Long
            Private ReadOnly _name2 As Long
            Private ReadOnly _name3 As Long
            Private ReadOnly _name4 As Long
            Private ReadOnly _name5 As Long
            Private ReadOnly _name6 As Long
            Private ReadOnly _name7 As Long
            Private ReadOnly _name8 As Long

            Public ReadOnly Property Name As String
                Get
                    Using buffer As New PinnedBuffer(Of Char)(56)
                        buffer.Write(0, Me)
                        Return New String(buffer.Target, 20, 36)
                    End Using
                End Get
            End Property

        End Structure

        <Flags>
        Public Enum GptAttributes As Long
            GPT_ATTRIBUTE_PLATFORM_REQUIRED = &H1
            GPT_BASIC_DATA_ATTRIBUTE_NO_DRIVE_LETTER = &H8000000000000000
            GPT_BASIC_DATA_ATTRIBUTE_HIDDEN = &H4000000000000000
            GPT_BASIC_DATA_ATTRIBUTE_SHADOW_COPY = &H2000000000000000
            GPT_BASIC_DATA_ATTRIBUTE_READ_ONLY = &H1000000000000000
        End Enum

        <StructLayout(LayoutKind.Sequential)>
        Public Structure DISK_GROW_PARTITION
            Public Property PartitionNumber As Int32
            Public Property BytesToGrow As Int64
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Class OVERLAPPED
            Public ReadOnly Property Status As UIntPtr
            Public ReadOnly Property BytesTransferred As UIntPtr
            Public Property StartOffset As Long
            Public Property EventHandle As SafeWaitHandle
        End Class

        <StructLayout(LayoutKind.Sequential)>
        Public Structure FILE_FS_FULL_SIZE_INFORMATION
            Public ReadOnly Property TotalAllocationUnits As Int64
            Public ReadOnly Property CallerAvailableAllocationUnits As Int64
            Public ReadOnly Property ActualAvailableAllocationUnits As Int64
            Public ReadOnly Property SectorsPerAllocationUnit As UInt32
            Public ReadOnly Property BytesPerSector As UInt32

            Public ReadOnly Property TotalBytes As Int64
                Get
                    Return _TotalAllocationUnits * _SectorsPerAllocationUnit * _BytesPerSector
                End Get
            End Property

            Public ReadOnly Property BytesPerAllocationUnit As UInt32
                Get
                    Return _SectorsPerAllocationUnit * _BytesPerSector
                End Get
            End Property

            Public Overrides Function ToString() As String
                Return TotalBytes.ToString(NumberFormatInfo.InvariantInfo)
            End Function
        End Structure

        <StructLayout(LayoutKind.Sequential)>
        Public Structure COMMTIMEOUTS
            Public Property ReadIntervalTimeout As UInt32
            Public Property ReadTotalTimeoutMultiplier As UInt32
            Public Property ReadTotalTimeoutConstant As UInt32
            Public Property WriteTotalTimeoutMultiplier As UInt32
            Public Property WriteTotalTimeoutConstant As UInt32
        End Structure

    End Class

End Namespace
