Imports System.Buffers
Imports System.Collections.Concurrent
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Runtime.Versioning
Imports System.Security.AccessControl
Imports System.Text
Imports System.Threading
Imports Arsenal.ImageMounter.Extensions
Imports Microsoft.Win32
Imports Microsoft.Win32.SafeHandles

''''' NativeFileIO.vb
''''' Routines for accessing some useful Win32 API functions to access features not
''''' directly accessible through .NET Framework.
''''' 
''''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
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
#Disable Warning CA1069 ' Enums values should not be duplicated


Namespace IO

    ''' <summary>
    ''' Provides wrappers for Win32 file API. This makes it possible to open everything that
    ''' CreateFile() can open and get a FileStream based .NET wrapper around the file handle.
    ''' </summary>
    <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
    Public NotInheritable Class NativeFileIO

#Region "Win32 API"

        Public NotInheritable Class SafeNativeMethods

#Disable Warning CA1401 ' P/Invokes should not be visible
            Public Declare Unicode Function AllocConsole Lib "kernel32" (
              ) As Boolean

            Public Declare Unicode Function FreeConsole Lib "kernel32" (
              ) As Boolean

            Public Declare Unicode Function GetConsoleWindow Lib "kernel32" (
              ) As IntPtr

            Public Declare Unicode Function GetLogicalDrives Lib "kernel32" (
              ) As UInteger

            Public Declare Unicode Function GetFileAttributesW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpFileName As Char
              ) As FileAttributes

            Public Declare Unicode Function SetFileAttributesW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpFileName As Char,
              dwFileAttributes As FileAttributes
              ) As Boolean

            Public Declare Unicode Function GetTickCount64 Lib "kernel32" () As Long
#Enable Warning CA1401 ' P/Invokes should not be visible

        End Class

        Public NotInheritable Class UnsafeNativeMethods

            Friend Declare Unicode Function DuplicateHandle Lib "kernel32" (
                hSourceProcessHandle As IntPtr,
                hSourceHandle As IntPtr,
                hTargetProcessHandle As IntPtr,
                <Out> ByRef lpTargetHandle As SafeWaitHandle,
                dwDesiredAccess As UInteger,
                bInheritHandle As Boolean,
                dwOptions As UInteger) As Boolean

            Friend Declare Unicode Function SetEvent Lib "kernel32" (
                hEvent As SafeWaitHandle) As Boolean

            Friend Declare Unicode Function SetHandleInformation Lib "kernel32" (
                h As SafeHandle,
                mask As UInteger,
                flags As UInteger) As Boolean

            Friend Declare Unicode Function GetHandleInformation Lib "kernel32" (
                h As SafeHandle,
                <Out> ByRef flags As UInteger) As Boolean

            Friend Declare Unicode Function NtCreateFile Lib "ntdll" (
                <Out> ByRef hFile As SafeFileHandle,
                AccessMask As FileSystemRights,
                <[In], IsReadOnly> ByRef ObjectAttributes As ObjectAttributes,
                <Out> ByRef IoStatusBlock As IoStatusBlock,
                <[In], IsReadOnly> ByRef AllocationSize As Long,
                FileAttributes As FileAttributes,
                ShareAccess As FileShare,
                CreateDisposition As NtCreateDisposition,
                CreateOptions As NtCreateOptions,
                EaBuffer As IntPtr,
                EaLength As UInteger) As Integer

            Friend Declare Unicode Function NtOpenEvent Lib "ntdll" (
                <Out> ByRef hEvent As SafeWaitHandle,
                AccessMask As UInteger,
                <[In], IsReadOnly> ByRef ObjectAttributes As ObjectAttributes) As Integer

            Friend Declare Unicode Function GetFileInformationByHandle Lib "kernel32" (
                hFile As SafeFileHandle,
                <Out> ByRef lpFileInformation As ByHandleFileInformation) As Boolean

            Friend Declare Unicode Function GetFileTime Lib "kernel32" (
                hFile As SafeFileHandle,
                <Out, [Optional]> ByRef lpCreationTime As Long,
                <Out, [Optional]> ByRef lpLastAccessTime As Long,
                <Out, [Optional]> ByRef lpLastWriteTime As Long) As Boolean

            Friend Declare Unicode Function GetFileTime Lib "kernel32" (
                hFile As SafeFileHandle,
                <Out, [Optional]> ByRef lpCreationTime As Long,
                lpLastAccessTime As IntPtr,
                <Out, [Optional]> ByRef lpLastWriteTime As Long) As Boolean

            Friend Declare Unicode Function GetFileTime Lib "kernel32" (
                hFile As SafeFileHandle,
                lpCreationTime As IntPtr,
                lpLastAccessTime As IntPtr,
                <Out, [Optional]> ByRef lpLastWriteTime As Long) As Boolean

            Friend Declare Unicode Function GetFileTime Lib "kernel32" (
                hFile As SafeFileHandle,
                <Out, [Optional]> ByRef lpCreationTime As Long,
                lpLastAccessTime As IntPtr,
                lpLastWriteTime As IntPtr) As Boolean

            Friend Declare Unicode Function FindFirstStreamW Lib "kernel32" Alias "FindFirstStreamW" (
              <[In], IsReadOnly> ByRef lpFileName As Char,
              InfoLevel As UInteger,
              <[Out]> ByRef lpszVolumeMountPoint As FindStreamData,
              dwFlags As UInteger) As SafeFindHandle

            Friend Declare Unicode Function FindNextStream Lib "kernel32" Alias "FindNextStreamW" (
              hFindStream As SafeFindHandle,
              <[Out]> ByRef lpszVolumeMountPoint As FindStreamData) As Boolean

            Friend Declare Unicode Function FindFirstVolumeMountPointW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpszRootPathName As Char,
              <Out> ByRef lpszVolumeMountPoint As Char,
              cchBufferLength As Integer) As SafeFindVolumeMountPointHandle

            Friend Declare Unicode Function FindNextVolumeMountPointW Lib "kernel32" (
              hFindVolumeMountPoint As SafeFindVolumeMountPointHandle,
              <Out> ByRef lpszVolumeMountPoint As Char,
              cchBufferLength As Integer) As Boolean

            Friend Declare Unicode Function FindFirstVolumeW Lib "kernel32" (
              <Out> ByRef lpszVolumeName As Char,
              cchBufferLength As Integer) As SafeFindVolumeHandle

            Friend Declare Unicode Function FindNextVolumeW Lib "kernel32" (
              hFindVolumeMountPoint As SafeFindVolumeHandle,
              <Out> ByRef lpszVolumeName As Char,
              cchBufferLength As Integer) As Boolean

            Friend Declare Unicode Function DeleteVolumeMountPointW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpszVolumeMountPoint As Char) As Boolean

            Friend Declare Unicode Function SetVolumeMountPointW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpszVolumeMountPoint As Char,
              <[In], IsReadOnly> ByRef lpszVolumeName As Char) As Boolean

            Friend Declare Function SetFilePointerEx Lib "kernel32" (
              hFile As SafeFileHandle,
              distance_to_move As Long,
              <Out> ByRef new_file_pointer As Long,
              move_method As UInteger) As Boolean

            Friend Declare Function SetFilePointerEx Lib "kernel32" (
              hFile As SafeFileHandle,
              distance_to_move As Long,
              ptr_new_file_pointer As IntPtr,
              move_method As UInteger) As Boolean

            Friend Declare Unicode Function OpenSCManagerW Lib "advapi32" (
              lpMachineName As IntPtr,
              lpDatabaseName As IntPtr,
              dwDesiredAccess As Integer) As SafeServiceHandle

            Friend Declare Unicode Function OpenSCManagerW Lib "advapi32" (
              <[In], IsReadOnly> ByRef lpMachineName As Char,
              lpDatabaseName As IntPtr,
              dwDesiredAccess As Integer) As SafeServiceHandle

            Friend Declare Unicode Function CreateServiceW Lib "advapi32" (
              hSCManager As SafeServiceHandle,
              <[In], IsReadOnly> ByRef lpServiceName As Char,
              <[In], IsReadOnly> ByRef lpDisplayName As Char,
              dwDesiredAccess As Integer,
              dwServiceType As Integer,
              dwStartType As Integer,
              dwErrorControl As Integer,
              <[In], IsReadOnly> ByRef lpBinaryPathName As Char,
              <[In], IsReadOnly> ByRef lpLoadOrderGroup As Char,
              lpdwTagId As IntPtr,
              <[In], IsReadOnly> ByRef lpDependencies As Char,
              <[In], IsReadOnly> ByRef lp As Char,
              <[In], IsReadOnly> ByRef lpPassword As Char) As SafeServiceHandle

            Friend Declare Unicode Function OpenServiceW Lib "advapi32" (
              hSCManager As SafeServiceHandle,
              <[In], IsReadOnly> ByRef lpServiceName As Char,
              dwDesiredAccess As Integer) As SafeServiceHandle

            Friend Declare Unicode Function ControlService Lib "advapi32" (
              hSCManager As SafeServiceHandle,
              dwControl As Integer,
              ByRef lpServiceStatus As SERVICE_STATUS) As Boolean

            Friend Declare Unicode Function DeleteService Lib "advapi32" (
              hSCObject As SafeServiceHandle) As Boolean

            Friend Declare Unicode Function StartServiceW Lib "advapi32" (
              hService As SafeServiceHandle,
              dwNumServiceArgs As Integer,
              lpServiceArgVectors As IntPtr) As Boolean

            Friend Declare Unicode Function GetModuleHandleW Lib "kernel32" (
              <[In], IsReadOnly> ByRef ModuleName As Char) As IntPtr

            Friend Declare Unicode Function LoadLibraryW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpFileName As Char) As IntPtr

            Friend Declare Unicode Function FreeLibrary Lib "kernel32" (
              hModule As IntPtr) As Boolean

            Friend Declare Function GetFileType Lib "kernel32" (handle As IntPtr) As Win32FileType

            Friend Declare Function GetFileType Lib "kernel32" (handle As SafeFileHandle) As Win32FileType

            Friend Declare Function GetStdHandle Lib "kernel32" (nStdHandle As StdHandle) As IntPtr

            Friend Declare Function GetConsoleScreenBufferInfo Lib "kernel32" (hConsoleOutput As IntPtr, <Out> ByRef lpConsoleScreenBufferInfo As CONSOLE_SCREEN_BUFFER_INFO) As Boolean

            Friend Declare Function GetConsoleScreenBufferInfo Lib "kernel32" (hConsoleOutput As SafeFileHandle, <Out> ByRef lpConsoleScreenBufferInfo As CONSOLE_SCREEN_BUFFER_INFO) As Boolean

            Friend Declare Unicode Function DefineDosDeviceW Lib "kernel32" (
              dwFlags As DEFINE_DOS_DEVICE_FLAGS,
              <[In], IsReadOnly> ByRef lpDeviceName As Char,
              <[In], IsReadOnly> ByRef lpTargetPath As Char) As Boolean

            Friend Declare Unicode Function QueryDosDeviceW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpDeviceName As Char,
              <Out> ByRef lpTargetPath As Char,
              ucchMax As Integer) As Integer

            Friend Declare Unicode Function QueryDosDeviceW Lib "kernel32" (
              lpDeviceName As IntPtr,
              <Out> ByRef lpTargetPath As Char,
              ucchMax As Integer) As Integer

            Friend Declare Unicode Function GetVolumePathNamesForVolumeNameW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpszVolumeName As Char,
              <Out> ByRef lpszVolumePathNames As Char,
              cchBufferLength As Integer,
              <Out> ByRef lpcchReturnLength As Integer) As Boolean

            Friend Declare Unicode Function GetVolumeNameForVolumeMountPointW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpszVolumeName As Char,
              <Out> ByRef DestinationInfFileName As Char,
              DestinationInfFileNameSize As Integer) As Boolean

            Friend Declare Unicode Function GetCommTimeouts Lib "kernel32" (
              hFile As SafeFileHandle,
              <Out> ByRef lpCommTimeouts As COMMTIMEOUTS) As Boolean

            Friend Declare Unicode Function SetCommTimeouts Lib "kernel32" (
              hFile As SafeFileHandle,
              <[In], IsReadOnly> ByRef lpCommTimeouts As COMMTIMEOUTS) As Boolean

            Friend Declare Unicode Function GetVolumePathNameW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpszFileName As Char,
              <Out> ByRef lpszVolumePathName As Char,
              cchBufferLength As Integer) As Boolean

            Friend Declare Unicode Function CreateFileW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpFileName As Char,
              dwDesiredAccess As FileSystemRights,
              dwShareMode As FileShare,
              lpSecurityAttributes As IntPtr,
              dwCreationDisposition As UInteger,
              dwFlagsAndAttributes As Integer,
              hTemplateFile As IntPtr) As SafeFileHandle

            Friend Declare Function FlushFileBuffers Lib "kernel32" (
              handle As SafeFileHandle) As Boolean

            Friend Declare Function GetFileSizeEx Lib "kernel32" (
              hFile As SafeFileHandle,
              <Out> ByRef liFileSize As Long) As Boolean

            Friend Declare Function GetFileAttributesW Lib "kernel32" (
              <[In], IsReadOnly> ByRef path As Char) As FileAttributes

            Friend Declare Unicode Function GetDiskFreeSpaceW Lib "kernel32" (
              <[In], IsReadOnly> ByRef lpRootPathName As Char,
              <Out> ByRef lpSectorsPerCluster As UInteger,
              <Out> ByRef lpBytesPerSector As UInteger,
              <Out> ByRef lpNumberOfFreeClusters As UInteger,
              <Out> ByRef lpTotalNumberOfClusters As UInteger) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" Alias "DeviceIoControl" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              <[In], IsReadOnly, MarshalAs(UnmanagedType.I1)> ByRef lpInBuffer As Boolean,
              nInBufferSize As UInteger,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As Byte,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              <[In], IsReadOnly> ByRef lpInBuffer As Byte,
              nInBufferSize As UInteger,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              <[In], IsReadOnly> ByRef lpInBuffer As Byte,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As Byte,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              lpOutBuffer As SafeBuffer,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As SafeBuffer,
              nInBufferSize As UInteger,
              lpOutBuffer As SafeBuffer,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As SafeBuffer,
              nInBufferSize As UInteger,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As Long,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As Integer,
              <Out> ByRef lpInBuffer As GET_DISK_ATTRIBUTES,
              nOutBufferSize As Integer,
              <Out> ByRef lpBytesReturned As Integer,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              <[In], IsReadOnly> ByRef lpInBuffer As DISK_GROW_PARTITION,
              nInBufferSize As UInteger,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              <[In], IsReadOnly> ByRef lpInBuffer As SET_DISK_ATTRIBUTES,
              nInBufferSize As Integer,
              lpOutBuffer As IntPtr,
              nOutBufferSize As Integer,
              <Out> ByRef lpBytesReturned As Integer,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As DISK_GEOMETRY,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As PARTITION_INFORMATION,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As PARTITION_INFORMATION_EX,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As SCSI_ADDRESS,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As STORAGE_DEVICE_NUMBER,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              <[In], IsReadOnly> ByRef lpInBuffer As STORAGE_PROPERTY_QUERY,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As STORAGE_DESCRIPTOR_HEADER,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              <[In], IsReadOnly> ByRef lpInBuffer As STORAGE_PROPERTY_QUERY,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As DEVICE_TRIM_DESCRIPTOR,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              <[In], IsReadOnly> ByRef lpInBuffer As STORAGE_PROPERTY_QUERY,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As Byte,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Friend Declare Unicode Function GetModuleFileNameW Lib "kernel32" (
              hModule As IntPtr,
              <Out> ByRef lpFilename As Char,
              nSize As Integer) As Integer

            <CodeAnalysis.SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments", Justification:="Special Ansi only function")>
            Friend Declare Ansi Function GetProcAddress Lib "kernel32" (
              hModule As IntPtr,
              <[In], IsReadOnly, MarshalAs(UnmanagedType.LPStr)> lpEntryName As String) As IntPtr

            Friend Declare Ansi Function GetProcAddress Lib "kernel32" (
              hModule As IntPtr,
              ordinal As IntPtr) As IntPtr

            Friend Declare Unicode Function RtlDosPathNameToNtPathName_U Lib "ntdll" (
              <[In], IsReadOnly> ByRef DosName As Char,
              <Out> ByRef NtName As UNICODE_STRING,
              DosFilePath As IntPtr,
              NtFilePath As IntPtr) As Boolean

            Friend Declare Sub RtlFreeUnicodeString Lib "ntdll" (
              ByRef UnicodeString As UNICODE_STRING)

            Friend Declare Function RtlNtStatusToDosError Lib "ntdll" (
              NtStatus As Integer) As Integer

            Friend Declare Unicode Function GetPrivateProfileSectionNamesW Lib "kernel32" (
              <Out> ByRef Names As Char,
              NamesSize As Integer,
              <[In], IsReadOnly> ByRef FileName As Char) As Integer

            Friend Declare Unicode Function GetPrivateProfileSectionW Lib "kernel32" (
              <[In], IsReadOnly> ByRef SectionName As Char,
              <[In], IsReadOnly> ByRef Values As Char,
              ValuesSize As Integer,
              <[In], IsReadOnly> ByRef FileName As Char) As Integer

            Friend Declare Unicode Function WritePrivateProfileStringW Lib "kernel32" (
              <[In], IsReadOnly> ByRef SectionName As Char,
              <[In], IsReadOnly> ByRef SettingName As Char,
              <[In], IsReadOnly> ByRef Value As Char,
              <[In], IsReadOnly> ByRef FileName As Char) As Boolean

            Friend Declare Unicode Sub InstallHinfSectionW Lib "setupapi" (
              hwndOwner As IntPtr,
              hModule As IntPtr,
              <[In], IsReadOnly> ByRef lpCmdLine As Char,
              nCmdShow As Integer)

            Friend Declare Unicode Function SetupCopyOEMInfW Lib "setupapi" (
              <[In], IsReadOnly> ByRef SourceInfFileName As Char,
              <[In], IsReadOnly> ByRef OEMSourceMediaLocation As Char,
              OEMSourceMediaType As UInteger,
              CopyStyle As UInteger,
              <Out> ByRef DestinationInfFileName As Char,
              DestinationInfFileNameSize As Integer,
              <Out> ByRef RequiredSize As UInteger,
              DestinationInfFileNameComponent As IntPtr) As Boolean

            Friend Declare Unicode Function DriverPackagePreinstallW Lib "difxapi" (
              <[In], IsReadOnly> ByRef SourceInfFileName As Char,
              Options As UInteger) As Integer

            Friend Declare Unicode Function DriverPackageInstallW Lib "difxapi" (
              <[In], IsReadOnly> ByRef SourceInfFileName As Char,
              Options As UInteger,
              pInstallerInfo As IntPtr,
              <Out> ByRef pNeedReboot As Boolean) As Integer

            Friend Declare Unicode Function DriverPackageUninstallW Lib "difxapi" (
              <[In], IsReadOnly> ByRef SourceInfFileName As Char,
              Options As UInteger,
              pInstallerInfo As IntPtr,
              <Out> ByRef pNeedReboot As Boolean) As Integer

            Friend Declare Unicode Function MapFileAndCheckSumW Lib "imagehlp" (
              <[In], IsReadOnly> ByRef file As Char,
              <Out> ByRef headerSum As Integer,
              <Out> ByRef checkSum As Integer) As Integer

            Friend Declare Unicode Function CM_Locate_DevNodeW Lib "setupapi" (
              ByRef devInst As UInteger,
              <[In], IsReadOnly> ByRef rootid As Char,
              Flags As UInteger) As UInteger

            Friend Declare Unicode Function CM_Get_DevNode_Registry_PropertyW Lib "setupapi" (
              DevInst As UInteger,
              Prop As CmDevNodeRegistryProperty,
              <Out> ByRef RegDataType As RegistryValueKind,
              <Out> ByRef Buffer As Byte,
              <[In], IsReadOnly, Out> ByRef BufferLength As Integer,
              Flags As UInteger) As UInteger

            Friend Declare Unicode Function CM_Set_DevNode_Registry_PropertyW Lib "setupapi" (
              DevInst As UInteger,
              Prop As CmDevNodeRegistryProperty,
              <[In], IsReadOnly> ByRef Buffer As Byte,
              length As Integer,
              Flags As UInteger) As UInteger

            Friend Declare Unicode Function CM_Get_Class_Registry_PropertyW Lib "setupapi" (
              <[In], IsReadOnly> ByRef ClassGuid As Guid,
              Prop As CmClassRegistryProperty,
              <Out> ByRef RegDataType As RegistryValueKind,
              <Out> ByRef Buffer As Byte,
              <[In], IsReadOnly, Out> ByRef BufferLength As Integer,
              Flags As UInteger,
              Optional hMachine As IntPtr = Nothing) As UInteger

            Friend Declare Unicode Function CM_Set_Class_Registry_PropertyW Lib "setupapi" (
              <[In], IsReadOnly> ByRef ClassGuid As Guid,
              Prop As CmClassRegistryProperty,
              <[In], IsReadOnly> ByRef Buffer As Byte,
              length As Integer,
              Flags As UInteger,
              Optional hMachine As IntPtr = Nothing) As UInteger

            Friend Declare Unicode Function CM_Get_Child Lib "setupapi" (
              <Out> ByRef dnDevInst As UInteger,
              DevInst As UInteger,
              Flags As UInteger) As UInteger

            Friend Declare Unicode Function CM_Get_Sibling Lib "setupapi" (
              <Out> ByRef dnDevInst As UInteger,
              DevInst As UInteger,
              Flags As UInteger) As UInteger

            Friend Declare Function CM_Reenumerate_DevNode Lib "setupapi" (
              devInst As UInteger,
              Flags As UInteger) As UInteger

            Friend Declare Unicode Function CM_Get_Device_ID_List_SizeW Lib "setupapi" (
              ByRef Length As Integer,
              <[In], IsReadOnly> ByRef filter As Char,
              Flags As UInteger) As UInteger

            Friend Declare Unicode Function CM_Get_Device_ID_ListW Lib "setupapi" (
              <[In], IsReadOnly> ByRef filter As Char,
              <Out> ByRef Buffer As Char,
              BufferLength As UInteger,
              Flags As UInteger) As UInteger

            Friend Declare Unicode Function SetupSetNonInteractiveMode Lib "setupapi" (
              state As Boolean) As Boolean

            Friend Declare Unicode Function SetupOpenInfFileW Lib "setupapi" (
              <[In], IsReadOnly> ByRef FileName As Char,
              <[In], IsReadOnly> ByRef InfClass As Char,
              InfStyle As UInteger,
              ByRef ErrorLine As UInteger) As SafeInfHandle

            Public Delegate Function SetupFileCallback(Context As IntPtr,
                                                       Notification As UInteger,
                                                       Param1 As UIntPtr,
                                                       Param2 As UIntPtr) As UInteger

            Friend Declare Unicode Function SetupInstallFromInfSectionW Lib "setupapi" (
              hWnd As IntPtr,
              InfHandle As SafeInfHandle,
              <[In], IsReadOnly> ByRef SectionName As Char,
              Flags As UInteger,
              RelativeKeyRoot As IntPtr,
              <[In], IsReadOnly> ByRef SourceRootPath As Char,
              CopyFlags As UInteger,
              MsgHandler As SetupFileCallback,
              Context As IntPtr,
              DeviceInfoSet As IntPtr,
              DeviceInfoData As IntPtr) As Boolean

            Friend Declare Unicode Function SetupInstallFromInfSectionW Lib "setupapi" (
              hWnd As IntPtr,
              InfHandle As SafeInfHandle,
              <[In], IsReadOnly> ByRef SectionName As Char,
              Flags As UInteger,
              RelativeKeyRoot As SafeRegistryHandle,
              <[In], IsReadOnly> ByRef SourceRootPath As Char,
              CopyFlags As UInteger,
              MsgHandler As SetupFileCallback,
              Context As IntPtr,
              DeviceInfoSet As IntPtr,
              DeviceInfoData As IntPtr) As Boolean

            Friend Declare Unicode Function SetupDiGetINFClassW Lib "setupapi" (
              <[In], IsReadOnly> ByRef InfPath As Char,
              <Out> ByRef ClassGuid As Guid,
              <Out> ByRef ClassName As Char,
              ClassNameSize As UInteger,
              <Out> ByRef RequiredSize As UInteger) As Boolean

            Friend Declare Unicode Function SetupDiOpenDeviceInfoW Lib "setupapi" (
              DevInfoSet As SafeDeviceInfoSetHandle,
              <[In], IsReadOnly> ByRef Enumerator As Char,
              hWndParent As IntPtr,
              Flags As UInteger,
              DeviceInfoData As IntPtr) As Boolean

            Friend Declare Unicode Function SetupDiOpenDeviceInfoW Lib "setupapi" (
              DevInfoSet As SafeDeviceInfoSetHandle,
              ByRef Enumerator As Byte,
              hWndParent As IntPtr,
              Flags As UInteger,
              DeviceInfoData As IntPtr) As Boolean

            Friend Declare Unicode Function SetupDiGetClassDevsW Lib "setupapi" (
              <[In], IsReadOnly> ByRef ClassGuid As Guid,
              <[In], IsReadOnly> ByRef Enumerator As Char,
              hWndParent As IntPtr,
              Flags As UInteger) As SafeDeviceInfoSetHandle

            Friend Declare Unicode Function SetupDiGetClassDevsW Lib "setupapi" (
              ClassGuid As IntPtr,
              <[In], IsReadOnly> ByRef Enumerator As Char,
              hWndParent As IntPtr,
              Flags As UInteger) As SafeDeviceInfoSetHandle

            Friend Declare Unicode Function SetupDiEnumDeviceInfo Lib "setupapi" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              MemberIndex As UInteger,
              <[Out]> ByRef DeviceInterfaceData As SP_DEVINFO_DATA) As Boolean

            Friend Declare Unicode Function SetupDiRestartDevices Lib "setupapi" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              <[In], IsReadOnly, [Out]> ByRef DeviceInterfaceData As SP_DEVINFO_DATA) As Boolean

            Friend Declare Unicode Function SetupDiSetClassInstallParamsW Lib "setupapi" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              <[In], IsReadOnly, Out> ByRef DeviceInfoData As SP_DEVINFO_DATA,
              <[In], IsReadOnly> ByRef ClassInstallParams As SP_PROPCHANGE_PARAMS,
              ClassInstallParamsSize As Integer) As Boolean

            Friend Declare Unicode Function SetupDiEnumDeviceInterfaces Lib "setupapi" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              DeviceInfoData As IntPtr,
              <[In], IsReadOnly> ByRef ClassGuid As Guid,
              MemberIndex As UInteger,
              <[Out]> ByRef DeviceInterfaceData As SP_DEVICE_INTERFACE_DATA) As Boolean

            Friend Declare Unicode Function SetupDiEnumDeviceInterfaces Lib "setupapi" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              DeviceInfoData As IntPtr,
              ClassGuid As IntPtr,
              MemberIndex As UInteger,
              <[Out]> ByRef DeviceInterfaceData As SP_DEVICE_INTERFACE_DATA) As Boolean

            Friend Declare Unicode Function SetupDiCreateDeviceInfoListExW Lib "setupapi" (
              <[In], IsReadOnly> ByRef ClassGuid As Guid,
              hwndParent As IntPtr,
              <[In], IsReadOnly> ByRef MachineName As Char,
              Reserved As IntPtr) As SafeDeviceInfoSetHandle

            Friend Declare Unicode Function SetupDiCreateDeviceInfoListExW Lib "setupapi" (
              ClassGuid As IntPtr,
              hwndParent As IntPtr,
              <[In], IsReadOnly> ByRef MachineName As Char,
              Reserved As IntPtr) As SafeDeviceInfoSetHandle

            Friend Declare Unicode Function SetupDiCreateDeviceInfoList Lib "setupapi" (
              <[In], IsReadOnly> ByRef ClassGuid As Guid,
              hwndParent As IntPtr) As SafeDeviceInfoSetHandle

            Friend Declare Unicode Function SetupDiCreateDeviceInfoList Lib "setupapi" (
              ClassGuid As IntPtr,
              hwndParent As IntPtr) As SafeDeviceInfoSetHandle

            Friend Declare Unicode Function SetupDiGetDeviceInterfaceDetailW Lib "setupapi" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              <[In], IsReadOnly> ByRef DeviceInterfaceData As SP_DEVICE_INTERFACE_DATA,
              <[Out](), MarshalAs(UnmanagedType.LPStruct, SizeParamIndex:=3)> DeviceInterfaceDetailData As SP_DEVICE_INTERFACE_DETAIL_DATA,
              DeviceInterfaceDetailDataSize As UInteger,
              <Out> ByRef RequiredSize As UInteger,
              DeviceInfoData As IntPtr) As Boolean

            Friend Declare Unicode Function SetupDiGetDeviceInfoListDetailW Lib "setupapi" (
              devinfo As SafeDeviceInfoSetHandle,
              <Out> DeviceInfoDetailData As SP_DEVINFO_LIST_DETAIL_DATA) As Boolean

            Friend Declare Unicode Function SetupDiCreateDeviceInfoW Lib "setupapi" (
              hDevInfo As SafeDeviceInfoSetHandle,
              <[In], IsReadOnly> ByRef DeviceName As Char,
              <[In], IsReadOnly> ByRef ClassGuid As Guid,
              <[In], IsReadOnly> ByRef DeviceDescription As Char,
              owner As IntPtr,
              CreationFlags As UInteger,
              <Out> ByRef DeviceInfoData As SP_DEVINFO_DATA) As Boolean

            Friend Declare Unicode Function SetupDiSetDeviceRegistryPropertyW Lib "setupapi" (
              hDevInfo As SafeDeviceInfoSetHandle,
              ByRef DeviceInfoData As SP_DEVINFO_DATA,
              [Property] As UInteger,
              <[In], IsReadOnly> ByRef PropertyBuffer As Byte,
              PropertyBufferSize As UInteger) As Boolean

            Friend Declare Unicode Function SetupDiCallClassInstaller Lib "setupapi" (
              InstallFunction As UInteger,
              hDevInfo As SafeDeviceInfoSetHandle,
              <[In], IsReadOnly> ByRef DeviceInfoData As SP_DEVINFO_DATA) As Boolean

            Friend Declare Unicode Function UpdateDriverForPlugAndPlayDevicesW Lib "newdev" (
              owner As IntPtr,
              <[In], IsReadOnly> ByRef HardwareId As Char,
              <[In], IsReadOnly> ByRef InfPath As Char,
              InstallFlags As UInteger,
              <MarshalAs(UnmanagedType.Bool), Out> ByRef RebootRequired As Boolean) As Boolean

            Friend Declare Unicode Function ExitWindowsEx Lib "user32" (
              flags As ShutdownFlags,
              reason As ShutdownReasons) As Boolean

            Friend Declare Unicode Function RtlGetVersion Lib "ntdll" (
              <[In], IsReadOnly, Out> os_version As OSVERSIONINFO) As Integer

            Friend Declare Unicode Function RtlGetVersion Lib "ntdll" (
              <[In], IsReadOnly, Out> os_version As OSVERSIONINFOEX) As Integer

            Friend Declare Unicode Function LookupPrivilegeValueW Lib "advapi32" (
              <[In], IsReadOnly> ByRef lpSystemName As Char,
              <[In], IsReadOnly> ByRef lpName As Char,
              <Out> ByRef lpLuid As Long) As Boolean

            Friend Declare Unicode Function OpenThreadToken Lib "advapi32" (
              <[In], IsReadOnly> hThread As IntPtr,
              <[In], IsReadOnly> dwAccess As UInteger,
              <[In], IsReadOnly> openAsSelf As Boolean,
              <Out> ByRef lpTokenHandle As SafeFileHandle) As Boolean

            Friend Declare Unicode Function OpenProcessToken Lib "advapi32" (
              <[In], IsReadOnly> hProcess As IntPtr,
              <[In], IsReadOnly> dwAccess As UInteger,
              <Out> ByRef lpTokenHandle As SafeFileHandle) As Boolean

            Friend Declare Unicode Function AdjustTokenPrivileges Lib "advapi32" (
              <[In], IsReadOnly> TokenHandle As SafeFileHandle,
              <[In], IsReadOnly> DisableAllPrivileges As Boolean,
              <[In], IsReadOnly> ByRef NewStates As Byte,
              <[In], IsReadOnly> BufferLength As Integer,
              <Out> ByRef PreviousState As Byte,
              <Out> ByRef ReturnLength As Integer) As Boolean

            Friend Declare Unicode Function NtQuerySystemInformation Lib "ntdll" (
              <[In], IsReadOnly> SystemInformationClass As SystemInformationClass,
              <[In], IsReadOnly> pSystemInformation As SafeBuffer,
              <[In], IsReadOnly> uSystemInformationLength As Integer,
              <Out> ByRef puReturnLength As Integer) As Integer

            Friend Declare Unicode Function NtQueryObject Lib "ntdll" (
              <[In], IsReadOnly> ObjectHandle As SafeFileHandle,
              <[In], IsReadOnly> ObjectInformationClass As ObjectInformationClass,
              <[In], IsReadOnly> ObjectInformation As SafeBuffer,
              <[In], IsReadOnly> ObjectInformationLength As Integer,
              <Out> ByRef puReturnLength As Integer) As Integer

            Friend Declare Unicode Function NtDuplicateObject Lib "ntdll" (
              <[In], IsReadOnly> SourceProcessHandle As SafeHandle,
              <[In], IsReadOnly> SourceHandle As IntPtr,
              <[In], IsReadOnly> TargetProcessHandle As IntPtr,
              <Out> ByRef TargetHandle As SafeFileHandle,
              <[In], IsReadOnly> DesiredAccess As UInteger,
              <[In], IsReadOnly> HandleAttributes As UInteger,
              <[In], IsReadOnly> Options As UInteger) As Integer

            Friend Declare Unicode Function GetCurrentProcess Lib "kernel32" () As IntPtr

            Friend Declare Unicode Function GetCurrentThread Lib "kernel32" () As IntPtr

            Friend Declare Unicode Function OpenProcess Lib "kernel32" (
              <[In], IsReadOnly> DesiredAccess As UInteger,
              <[In], IsReadOnly> InheritHandle As Boolean,
              <[In], IsReadOnly> ProcessId As Integer) As SafeFileHandle

            Friend Declare Unicode Function CreateHardLinkW Lib "kernel32" (<[In], IsReadOnly> ByRef newlink As Char, <[In], IsReadOnly> ByRef existing As Char, security As IntPtr) As Boolean

            Friend Declare Unicode Function MoveFileW Lib "kernel32" (<[In], IsReadOnly> ByRef existing As Char, <[In], IsReadOnly> ByRef newname As Char) As Boolean

            Friend Declare Unicode Function RtlCompareMemoryUlong Lib "ntdll" (<[In], IsReadOnly> ByRef buffer As Byte, length As IntPtr, v As Integer) As IntPtr
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
                                                    ctrlcode As UInteger,
                                                    timeout As UInteger,
                                                    databytes As Byte(),
                                                    <Out> ByRef returncode As Integer) As Byte()

                Dim indata = ArrayPool(Of Byte).Shared.Rent(28 + If(databytes?.Length, 0))
                Try
                    MemoryMarshal.Write(indata.AsSpan(0), PinnedBuffer(Of SRB_IO_CONTROL).TypeSize)
                    SrbIoCtlSignature.AsSpan().CopyTo(indata.AsSpan(4))
                    MemoryMarshal.Write(indata.AsSpan(12), timeout)
                    MemoryMarshal.Write(indata.AsSpan(16), ctrlcode)

                    If databytes Is Nothing Then
                        MemoryMarshal.Write(indata.AsSpan(24), 0UI)
                    Else
                        MemoryMarshal.Write(indata.AsSpan(24), databytes.Length)
                        Buffer.BlockCopy(databytes, 0, indata, 28, databytes.Length)
                    End If

                    Dim Response = DeviceIoControl(adapter,
                                                   NativeConstants.IOCTL_SCSI_MINIPORT,
                                                   indata,
                                                   0)

                    returncode = MemoryMarshal.Read(Of Integer)(Response.Span.Slice(20))

                    If databytes IsNot Nothing Then
                        Dim ResponseLength = Math.Min(Math.Min(MemoryMarshal.Read(Of Integer)(Response.Span.Slice(24)), Response.Length - 28), databytes.Length)
                        Response.Slice(28, ResponseLength).CopyTo(databytes)
                        Array.Resize(databytes, ResponseLength)
                    End If

                    Return databytes

                Finally
                    ArrayPool(Of Byte).Shared.Return(indata)

                End Try

            End Function

        End Class

#End Region

        Public Shared ReadOnly Property SystemArchitecture As String = GetSystemArchitecture()

        Public Shared ReadOnly Property ProcessArchitecture As String = GetProcessArchitecture()

        Private Shared Function GetSystemArchitecture() As String

            Dim SystemArchitecture = RuntimeInformation.OSArchitecture.ToString()
            If SystemArchitecture.Equals("X64", StringComparison.OrdinalIgnoreCase) Then
                SystemArchitecture = "amd64"
            End If

            If SystemArchitecture Is Nothing Then
                SystemArchitecture = "x86"
            End If
            SystemArchitecture = String.Intern(SystemArchitecture.ToLowerInvariant())

            Trace.WriteLine($"System architecture is: {SystemArchitecture}")

            Return SystemArchitecture

        End Function

        Private Shared Function GetProcessArchitecture() As String

            Dim ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString()
            If ProcessArchitecture.Equals("X64", StringComparison.OrdinalIgnoreCase) Then
                ProcessArchitecture = "amd64"
            End If

            ProcessArchitecture = String.Intern(ProcessArchitecture.ToLowerInvariant())

            Trace.WriteLine($"Process architecture is: {ProcessArchitecture}")

            Return ProcessArchitecture

        End Function

        Public Shared Sub BrowseTo(target As String)

            Process.Start(New ProcessStartInfo With {.FileName = target, .UseShellExecute = True})?.Dispose()

        End Sub

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

            Return OfflineDiskVolumes(device_path.AsMemory(), force, CancellationToken.None)

        End Function

        Public Shared Function OfflineDiskVolumes(device_path As ReadOnlyMemory(Of Char), force As Boolean) As Boolean

            Return OfflineDiskVolumes(device_path, force, CancellationToken.None)

        End Function

        Public Shared Function OfflineDiskVolumes(device_path As String, force As Boolean, cancel As CancellationToken) As Boolean

            Return OfflineDiskVolumes(device_path.AsMemory(), force, cancel)

        End Function

        Public Shared Function OfflineDiskVolumes(device_path As ReadOnlyMemory(Of Char), force As Boolean, cancel As CancellationToken) As Boolean

            Dim refresh = False

            For Each volume In EnumerateDiskVolumes(device_path)

                cancel.ThrowIfCancellationRequested()

                Try
                    Using device As New DiskDevice(volume.AsMemory().TrimEnd("\"c), FileAccess.ReadWrite)

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
                        Dim dev_paths = QueryDosDevice(volume.AsMemory(4, 44)).ToArray()
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
                    Using device As New DiskDevice(volume.AsMemory().TrimEnd("\"c), FileAccess.ReadWrite)
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
                    Using device As New DiskDevice(volume.AsMemory().TrimEnd("\"c), FileAccess.ReadWrite)
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

        Public Shared Async Function OfflineDiskVolumesAsync(device_path As ReadOnlyMemory(Of Char), force As Boolean, cancel As CancellationToken) As Task(Of Boolean)

            Dim refresh = False

            For Each volume In EnumerateDiskVolumes(device_path)

                cancel.ThrowIfCancellationRequested()

                Try
                    Using device As New DiskDevice(volume.AsMemory().TrimEnd("\"c), FileAccess.ReadWrite)

                        If device.IsDiskWritable AndAlso Not device.DiskPolicyReadOnly.GetValueOrDefault() Then

                            Dim t As Task = Nothing

                            Try
                                device.FlushBuffers()
                                Await device.DismountVolumeFilesystemAsync(Force:=False, cancel).ConfigureAwait(False)

                            Catch ex As Win32Exception When (
                                ex.NativeErrorCode = NativeConstants.ERROR_WRITE_PROTECT OrElse
                                ex.NativeErrorCode = NativeConstants.ERROR_NOT_READY OrElse
                                ex.NativeErrorCode = NativeConstants.ERROR_DEV_NOT_EXIST)

                                t = device.DismountVolumeFilesystemAsync(Force:=True, cancel)

                            End Try

                            If t IsNot Nothing Then
                                Await t.ConfigureAwait(False)
                            End If

                        Else

                            Await device.DismountVolumeFilesystemAsync(Force:=True, cancel).ConfigureAwait(False)

                        End If

                        device.SetVolumeOffline(True)

                    End Using

                    refresh = True

                    Continue For

                Catch ex As Exception
                    Trace.WriteLine($"Failed to safely dismount volume '{volume}': {ex.JoinMessages()}")

                    If Not force Then
                        Dim dev_paths = QueryDosDevice(volume.AsMemory(4, 44)).ToArray()
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
                    Using device As New DiskDevice(volume.AsMemory().TrimEnd("\"c), FileAccess.ReadWrite)
                        device.FlushBuffers()
                        Await device.DismountVolumeFilesystemAsync(True, cancel).ConfigureAwait(False)
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
                    Using device As New DiskDevice(volume.AsMemory().TrimEnd("\"c), FileAccess.ReadWrite)
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

        Public Shared Async Function WaitForDiskIoIdleAsync(device_path As ReadOnlyMemory(Of Char), iterations As Integer, waitTime As TimeSpan, cancel As CancellationToken) As Task

            Dim volumes = EnumerateDiskVolumes(device_path).ToArray()

            Dim dev_paths = volumes.SelectMany(Function(volume) QueryDosDevice(volume.AsMemory(4, 44))).ToArray()

            Dim in_use_apps As String()

            For i = 1 To iterations
                cancel.ThrowIfCancellationRequested()

                in_use_apps = EnumerateProcessesHoldingFileHandle(dev_paths).Take(10).Select(AddressOf FormatProcessName).ToArray()
                If in_use_apps.Length = 0 Then
                    Exit For
                End If

                Trace.WriteLine($"File systems still in use by process {String.Join(", ", in_use_apps)}")

                Await Task.Delay(waitTime, cancel).ConfigureAwait(False)
            Next

        End Function

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

                Dim intsize = PinnedBuffer(Of Integer).TypeSize
                Dim structsize = PinnedBuffer(Of LUID_AND_ATTRIBUTES).TypeSize

                Dim luid_and_attribs_list As New Dictionary(Of String, LUID_AND_ATTRIBUTES)(privileges.Length)

                For Each privilege In privileges

                    Dim luid As Long

                    If UnsafeNativeMethods.LookupPrivilegeValueW(Nothing, MemoryMarshal.GetReference(privilege.AsSpan()), luid) Then

                        Dim luid_and_attribs As New LUID_AND_ATTRIBUTES(
                            LUID:=luid,
                            attributes:=NativeConstants.SE_PRIVILEGE_ENABLED)

                        luid_and_attribs_list.Add(privilege, luid_and_attribs)

                    End If

                Next

                If luid_and_attribs_list.Count = 0 Then

                    Return Nothing

                End If

                Dim bufferSize = intsize + privileges.Length * structsize

                Dim buffer = ArrayPool(Of Byte).Shared.Rent(bufferSize)

                Try
                    MemoryMarshal.Write(buffer, luid_and_attribs_list.Count)

                    For i = 0 To luid_and_attribs_list.Count - 1
                        MemoryMarshal.Write(buffer.AsSpan(intsize + i * structsize), luid_and_attribs_list.Values(i))
                    Next

                    Dim rc = UnsafeNativeMethods.AdjustTokenPrivileges(token, False, buffer(0), bufferSize, buffer(0), Nothing)

                    Dim err = Marshal.GetLastWin32Error()

                    If Not rc Then
                        Throw New Win32Exception
                    End If

                    If err = NativeConstants.ERROR_NOT_ALL_ASSIGNED Then

                        Dim count = MemoryMarshal.Read(Of Integer)(buffer)
                        Dim enabled_luids(0 To count - 1) As LUID_AND_ATTRIBUTES
                        MemoryMarshal.Cast(Of Byte, LUID_AND_ATTRIBUTES)(buffer.AsSpan(intsize)).Slice(0, count).CopyTo(enabled_luids)

                        Dim enabled_privileges = Aggregate enabled_luid In enabled_luids
                                                     Join privilege_name In luid_and_attribs_list
                                                         On enabled_luid.LUID Equals privilege_name.Value.LUID
                                                         Select privilege_name.Key
                                                         Into ToArray()

                        Return enabled_privileges
                    End If

                    Return privileges

                Finally
                    ArrayPool(Of Byte).Shared.Return(buffer)

                End Try

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

            Friend Sub New(<[In], IsReadOnly> ByRef HandleTableEntry As SystemHandleTableEntryInformation,
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

        Public Shared Function GetDiskFreeSpace(
              lpRootPathName As ReadOnlyMemory(Of Char),
              <Out> ByRef lpSectorsPerCluster As UInteger,
              <Out> ByRef lpBytesPerSector As UInteger,
              <Out> ByRef lpNumberOfFreeClusters As UInteger,
              <Out> ByRef lpTotalNumberOfClusters As UInteger) As Boolean

            Return UnsafeNativeMethods.GetDiskFreeSpaceW(
                lpRootPathName.MakeNullTerminated(),
                lpSectorsPerCluster,
                lpBytesPerSector,
                lpNumberOfFreeClusters,
                lpTotalNumberOfClusters)

        End Function

        Public Shared Function DeviceIoControl(
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            Return UnsafeNativeMethods.DeviceIoControl(
                hDevice,
                dwIoControlCode,
                lpInBuffer,
                nInBufferSize,
                lpOutBuffer,
                nOutBufferSize,
                lpBytesReturned,
                lpOverlapped)

        End Function

        Public Shared Function DeviceIoControl(
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As SafeBuffer,
              nInBufferSize As UInteger,
              lpOutBuffer As SafeBuffer,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

            If nInBufferSize > lpInBuffer?.ByteLength Then
                Throw New ArgumentException("Buffer size to use in call must be within size of SafeBuffer", NameOf(nInBufferSize))
            End If

            If nOutBufferSize > lpOutBuffer?.ByteLength Then
                Throw New ArgumentException("Buffer size to use in call must be within size of SafeBuffer", NameOf(nOutBufferSize))
            End If

            Return UnsafeNativeMethods.DeviceIoControl(
                hDevice,
                dwIoControlCode,
                lpInBuffer,
                nInBufferSize,
                lpOutBuffer,
                nOutBufferSize,
                lpBytesReturned,
                lpOverlapped)

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
                                               ctrlcode As UInteger,
                                               data As Memory(Of Byte),
                                               <Out> ByRef outdatasize As UInteger) As Memory(Of Byte)

            Dim indata = CType(data, ReadOnlyMemory(Of Byte))

            Dim indatasize = CUInt(indata.Length)

            If outdatasize > indatasize Then
                data = New Byte(0 To CInt(outdatasize - 1UI)) {}
            End If

            Dim rc =
              UnsafeNativeMethods.DeviceIoControl(device,
                                                  ctrlcode,
                                                  MemoryMarshal.GetReference(indata.Span),
                                                  indatasize,
                                                  data.Span(0),
                                                  CUInt(data.Length),
                                                  outdatasize,
                                                  IntPtr.Zero)

            If Not rc Then
                Throw New Win32Exception
            End If

            Return data.Slice(0, CInt(outdatasize))

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
              FileName As ReadOnlyMemory(Of Char),
              DesiredAccess As FileSystemRights,
              ShareMode As FileShare,
              SecurityAttributes As IntPtr,
              CreationDisposition As UInteger,
              FlagsAndAttributes As Integer,
              TemplateFile As IntPtr) As SafeFileHandle

            Dim handle = UnsafeNativeMethods.CreateFileW(FileName.MakeNullTerminated(), DesiredAccess, ShareMode, SecurityAttributes, CreationDisposition, FlagsAndAttributes, TemplateFile)

            If handle.IsInvalid Then
                Throw New IOException($"Cannot open '{FileName}'", New Win32Exception)
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
        Public Shared Function OpenFileHandle(FileName As ReadOnlyMemory(Of Char),
                                              DesiredAccess As FileAccess,
                                              ShareMode As FileShare,
                                              CreationDisposition As FileMode,
                                              Overlapped As Boolean) As SafeFileHandle

            If FileName.Span.IsWhiteSpace() Then
                Throw New ArgumentNullException(NameOf(FileName))
            End If

            Dim NativeDesiredAccess = ConvertManagedFileAccess(DesiredAccess)

            Dim NativeCreationDisposition As UInteger
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

            Dim Handle = UnsafeNativeMethods.CreateFileW(FileName.MakeNullTerminated(),
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
          FileName As ReadOnlyMemory(Of Char),
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
          FileName As ReadOnlyMemory(Of Char),
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          CreationDisposition As FileMode,
          Options As UInteger) As SafeFileHandle

            If FileName.Span.IsWhiteSpace() Then
                Throw New ArgumentNullException(NameOf(FileName))
            End If

            Dim NativeDesiredAccess = ConvertManagedFileAccess(DesiredAccess)

            Dim NativeCreationDisposition As UInteger
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

            Dim Handle = UnsafeNativeMethods.CreateFileW(FileName.MakeNullTerminated(),
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

            Using filename_native = UnicodeString.Pin(FileName)

                Dim object_attributes As New ObjectAttributes(RootDirectory, filename_native, ObjectAttributes, Nothing, Nothing)

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
                                           DesiredAccess As UInteger,
                                           RootDirectory As SafeFileHandle) As SafeWaitHandle

            If String.IsNullOrEmpty(EventName) Then
                Throw New ArgumentNullException(NameOf(EventName))
            End If

            Dim handle_value As SafeWaitHandle = Nothing

            Using eventname_native = UnicodeString.Pin(EventName)

                Dim object_attributes As New ObjectAttributes(RootDirectory, eventname_native, ObjectAttributes, Nothing, Nothing)

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
          FilePath As ReadOnlyMemory(Of Char),
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          CreationDisposition As FileMode) As SafeFileHandle

            If FilePath.Span.IsWhiteSpace() Then
                Throw New ArgumentNullException(NameOf(FilePath))
            End If

            Dim NativeDesiredAccess = FileSystemRights.ReadAttributes
            If DesiredAccess.HasFlag(FileAccess.Read) Then
                NativeDesiredAccess = NativeDesiredAccess Or FileSystemRights.Read
            End If
            If DesiredAccess.HasFlag(FileAccess.Write) Then
                NativeDesiredAccess = NativeDesiredAccess Or FileSystemRights.Write
            End If

            Dim NativeCreationDisposition As UInteger
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
                UnsafeNativeMethods.CreateFileW(FilePath.MakeNullTerminated(),
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

        ''' <summary>
        ''' Calls Win32 API CreateFile() function to create a backup handle for a file or
        ''' directory and encapsulates returned handle in a SafeFileHandle object. This
        ''' handle can later be used in calls to Win32 Backup API functions or similar.
        ''' </summary>
        ''' <param name="FilePath">Name of file or directory to open.</param>
        ''' <param name="DesiredAccess">Access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        Public Shared Function TryOpenBackupHandle(
          FilePath As ReadOnlyMemory(Of Char),
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          CreationDisposition As FileMode) As SafeFileHandle

            If FilePath.Span.IsWhiteSpace() Then
                Throw New ArgumentNullException(NameOf(FilePath))
            End If

            Dim NativeDesiredAccess = FileSystemRights.ReadAttributes
            If DesiredAccess.HasFlag(FileAccess.Read) Then
                NativeDesiredAccess = NativeDesiredAccess Or FileSystemRights.Read
            End If
            If DesiredAccess.HasFlag(FileAccess.Write) Then
                NativeDesiredAccess = NativeDesiredAccess Or FileSystemRights.Write
            End If

            Dim NativeCreationDisposition As UInteger
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
                UnsafeNativeMethods.CreateFileW(FilePath.MakeNullTerminated(),
                                                NativeDesiredAccess,
                                                ShareMode,
                                                IntPtr.Zero,
                                                NativeCreationDisposition,
                                                NativeFlagsAndAttributes,
                                                IntPtr.Zero)

            If Handle.IsInvalid Then
                Trace.WriteLine($"Cannot open {FilePath} ({Marshal.GetLastWin32Error()})")
            End If

            Return Handle
        End Function

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
          FileName As ReadOnlyMemory(Of Char),
          CreationDisposition As FileMode,
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          BufferSize As Integer,
          Overlapped As Boolean) As FileStream

            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped), GetFileStreamLegalAccessValue(DesiredAccess), BufferSize, Overlapped)

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
        Public Shared Function OpenFileStream(FileName As ReadOnlyMemory(Of Char),
                                              CreationDisposition As FileMode,
                                              DesiredAccess As FileAccess,
                                              ShareMode As FileShare,
                                              Overlapped As Boolean) As FileStream

            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped), GetFileStreamLegalAccessValue(DesiredAccess), 1, Overlapped)

        End Function

        ''' <summary>
        ''' Calls Win32 API CreateFile() function and encapsulates returned handle.
        ''' </summary>
        ''' <param name="FileName">Name of file to open.</param>
        ''' <param name="DesiredAccess">File access to request.</param>
        ''' <param name="ShareMode">Share mode to request.</param>
        ''' <param name="CreationDisposition">Open/creation mode.</param>
        ''' <param name="Options">Specifies whether to request overlapped I/O.</param>
        Public Shared Function OpenFileStream(FileName As ReadOnlyMemory(Of Char),
                                              CreationDisposition As FileMode,
                                              DesiredAccess As FileAccess,
                                              ShareMode As FileShare,
                                              Options As FileOptions) As FileStream

            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Options), GetFileStreamLegalAccessValue(DesiredAccess), 1, Options.HasFlag(FileOptions.Asynchronous))

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

        Public Shared Function GetFileSize(Filename As String) As Long

            Return GetFileSize(Filename.AsMemory())

        End Function

        Public Shared Function GetFileSize(Filename As ReadOnlyMemory(Of Char)) As Long

            Using safefilehandle = TryOpenBackupHandle(Filename, 0, FileShare.ReadWrite Or FileShare.Delete, FileMode.Open)

                If safefilehandle.IsInvalid Then
                    Return -1
                End If

                Return GetFileSize(safefilehandle)

            End Using

        End Function

        Public Shared Function TryGetFileAttributes(Filename As ReadOnlyMemory(Of Char), <Out> ByRef attributes As FileAttributes) As Boolean

            attributes = UnsafeNativeMethods.GetFileAttributesW(Filename.MakeNullTerminated())

            Return attributes <> -1

        End Function

        Public Shared Function TryGetFileAttributes(Filename As String, <Out> ByRef attributes As FileAttributes) As Boolean

            attributes = UnsafeNativeMethods.GetFileAttributesW(MemoryMarshal.GetReference(Filename.AsSpan()))

            Return attributes <> -1

        End Function

        Public Shared Function GetFileSize(SafeFileHandle As SafeFileHandle) As Long

            Dim FileSize As Long

            Win32Try(UnsafeNativeMethods.GetFileSizeEx(SafeFileHandle, FileSize))

            Return FileSize

        End Function

        Public Shared Function GetDiskSize(SafeFileHandle As SafeFileHandle) As Long?

            Dim FileSize As Long

            If UnsafeNativeMethods.DeviceIoControl(SafeFileHandle, NativeConstants.IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0UI, FileSize, 8UI, 0UI, IntPtr.Zero) Then

                Return FileSize

            Else

                Return GetPartitionInformationEx(SafeFileHandle)?.PartitionLength

            End If

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

        Public Shared Sub GrowPartition(DiskHandle As SafeFileHandle, PartitionNumber As Integer, BytesToGrow As Long)

            Dim DiskGrowPartition As New DISK_GROW_PARTITION(PartitionNumber, BytesToGrow)

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

        Public Shared Function GetLongFullPath(path As ReadOnlyMemory(Of Char)) As String

            Dim newpath = GetNtPath(path)

            If newpath.StartsWith("\??\", StringComparison.Ordinal) Then
#If NETSTANDARD2_1_OR_GREATER OrElse NETCOREAPP Then
                newpath = String.Concat("\\?\", newpath.AsSpan(4))
#Else
                newpath = String.Concat("\\?\", newpath.Substring(4))
#End If
            End If

            Return newpath

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

            Dim AddPathsArray = AddPaths.Split(";"c, StringSplitOptions.RemoveEmptyEntries)

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

            Dim Paths As New List(Of String)(Environment.GetEnvironmentVariable("PATH").Split(";"c, StringSplitOptions.RemoveEmptyEntries))

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

            Environment.SetEnvironmentVariable("PATH", String.Join(";"c, Paths))
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

                Await Task.Delay(300, cancel).ConfigureAwait(False)

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
        Public Shared Function GetScsiAddress(Device As ReadOnlyMemory(Of Char)) As SCSI_ADDRESS?

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

            Dim buffer = ArrayPool(Of Byte).Shared.Rent(StorageDescriptorHeader.Size)
            Try
                If Not UnsafeNativeMethods.DeviceIoControl(hDevice,
                                                           NativeConstants.IOCTL_STORAGE_QUERY_PROPERTY,
                                                           StoragePropertyQuery,
                                                           CUInt(PinnedBuffer(Of STORAGE_PROPERTY_QUERY).TypeSize),
                                                           buffer(0),
                                                           CUInt(buffer.Length),
                                                           Nothing,
                                                           Nothing) Then
                    Return Nothing
                End If

                Return New StorageStandardProperties(buffer)

            Finally
                ArrayPool(Of Byte).Shared.Return(buffer)

            End Try

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
        Public Shared Function QueryDirectoryJunction(source As ReadOnlyMemory(Of Char)) As String

            Using hdir = OpenFileHandle(source, FileAccess.Write, FileShare.Read, FileMode.Open, NativeConstants.FILE_FLAG_BACKUP_SEMANTICS Or NativeConstants.FILE_FLAG_OPEN_REPARSE_POINT)
                Return QueryDirectoryJunction(hdir)
            End Using

        End Function

        ''' <summary>
        ''' Creates a directory junction
        ''' </summary>
        ''' <param name="source">Location of directory to convert to a junction.</param>
        ''' <param name="target">Target path for the junction.</param>
        Public Shared Sub CreateDirectoryJunction(source As String, target As ReadOnlyMemory(Of Char))

            Directory.CreateDirectory(source)

            Using hdir = OpenFileHandle(source.AsMemory(),
                                        FileAccess.Write,
                                        FileShare.Read,
                                        FileMode.Open,
                                        NativeConstants.FILE_FLAG_BACKUP_SEMANTICS Or NativeConstants.FILE_FLAG_OPEN_REPARSE_POINT)

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

            Dim buffer = ArrayPool(Of Byte).Shared.Rent(65533)

            Try
                Dim size As UInteger

                If Not UnsafeNativeMethods.DeviceIoControl(source,
                                                           NativeConstants.FSCTL_GET_REPARSE_POINT,
                                                           IntPtr.Zero,
                                                           0UI,
                                                           buffer(0),
                                                           CUInt(buffer.Length),
                                                           size,
                                                           IntPtr.Zero) Then
                    Return Nothing
                End If

                If BitConverter.ToUInt32(buffer, 0) <> NativeConstants.IO_REPARSE_TAG_MOUNT_POINT Then
                    Throw New InvalidDataException("Not a mount point or junction")
                End If ' DWORD ReparseTag
                ' WORD ReparseDataLength
                ' WORD Reserved
                ' WORD NameOffset
                Dim name_length = BitConverter.ToUInt16(buffer, 10) - 2 ' WORD NameLength
                ' WORD DisplayNameOffset
                ' WORD DisplayNameLength
                Return Encoding.Unicode.GetString(buffer, 16, name_length)

            Finally
                ArrayPool(Of Byte).Shared.Return(buffer)

            End Try

        End Function

        ''' <summary>
        ''' Creates a directory junction
        ''' </summary>
        ''' <param name="source">Handle to directory.</param>
        ''' <param name="target">Target path for the junction.</param>
        Public Shared Sub CreateDirectoryJunction(source As SafeFileHandle, target As ReadOnlyMemory(Of Char))

            Dim namebytes = MemoryMarshal.AsBytes(target.Span)
            Dim namelength = CUShort(namebytes.Length)

            Dim data As New REPARSE_DATA_BUFFER(reparseDataLength:=8US + namelength + 2US + namelength + 2US,
                                                substituteNameOffset:=0,
                                                substituteNameLength:=namelength,
                                                printNameOffset:=namelength + 2US,
                                                printNameLength:=namelength)

            Dim bufferSize = PinnedBuffer(Of REPARSE_DATA_BUFFER).TypeSize + namelength + 2 + namelength + 2

            Dim buffer = ArrayPool(Of Byte).Shared.Rent(bufferSize)
            Try
                MemoryMarshal.Write(buffer, data)
                namebytes.CopyTo(buffer.AsSpan(PinnedBuffer(Of REPARSE_DATA_BUFFER).TypeSize))
                namebytes.CopyTo(buffer.AsSpan(PinnedBuffer(Of REPARSE_DATA_BUFFER).TypeSize + namelength + 2))

                If Not UnsafeNativeMethods.DeviceIoControl(source,
                                                           NativeConstants.FSCTL_SET_REPARSE_POINT,
                                                           buffer(0),
                                                           CUInt(bufferSize),
                                                           IntPtr.Zero,
                                                           0UI,
                                                           0UI,
                                                           IntPtr.Zero) Then
                    Throw New Win32Exception
                End If

            Finally
                ArrayPool(Of Byte).Shared.Return(buffer)

            End Try

        End Sub

        Public Shared Iterator Function QueryDosDevice() As IEnumerable(Of String)

            Const UcchMax = 65536

            Dim TargetPath = ArrayPool(Of Char).Shared.Rent(UcchMax)
            Try
                Dim length = UnsafeNativeMethods.QueryDosDeviceW(IntPtr.Zero, TargetPath(0), UcchMax)

                If length < 2 Then
                    Return
                End If

                For Each name In TargetPath.AsMemory(0, length).ParseDoubleTerminatedString()
                    Yield name.ToString()
                Next

            Finally
                ArrayPool(Of Char).Shared.Return(TargetPath)

            End Try

        End Function

        Public Shared Function QueryDosDevice(DosDevice As String) As IEnumerable(Of String)

            Return QueryDosDevice(DosDevice.AsMemory())

        End Function

        Public Shared Iterator Function QueryDosDevice(DosDevice As ReadOnlyMemory(Of Char)) As IEnumerable(Of String)

            Const UcchMax = 65536

            Dim TargetPath = ArrayPool(Of Char).Shared.Rent(UcchMax)
            Try
                Dim length = UnsafeNativeMethods.QueryDosDeviceW(DosDevice.MakeNullTerminated(), TargetPath(0), UcchMax)

                If length < 2 Then
                    Return
                End If

                For Each name In TargetPath.AsMemory(0, length).ParseDoubleTerminatedString()
                    Yield name.ToString()
                Next

            Finally
                ArrayPool(Of Char).Shared.Return(TargetPath)

            End Try

        End Function

        Public Shared Function GetNtPath(Win32Path As String) As String

            Return GetNtPath(Win32Path.AsMemory())

        End Function

        Public Shared Function GetNtPath(Win32Path As ReadOnlyMemory(Of Char)) As String

            Dim unicode_string As UNICODE_STRING

            Dim RC = UnsafeNativeMethods.RtlDosPathNameToNtPathName_U(Win32Path.MakeNullTerminated(), unicode_string, Nothing, Nothing)
            If Not RC Then
                Throw New IOException($"Invalid path: '{Win32Path}'")
            End If

            Try
                Return unicode_string.ToString()

            Finally
                UnsafeNativeMethods.RtlFreeUnicodeString(unicode_string)

            End Try

        End Function

        Public Shared Sub DeleteVolumeMountPoint(VolumeMountPoint As ReadOnlyMemory(Of Char))
            Win32Try(UnsafeNativeMethods.DeleteVolumeMountPointW(VolumeMountPoint.MakeNullTerminated()))
        End Sub

        Public Shared Sub SetVolumeMountPoint(VolumeMountPoint As ReadOnlyMemory(Of Char), VolumeName As ReadOnlyMemory(Of Char))
            Win32Try(UnsafeNativeMethods.SetVolumeMountPointW(VolumeMountPoint.MakeNullTerminated(),
                                                             VolumeName.MakeNullTerminated()))
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

        Public Shared Function GetVolumeDiskExtents(volume As SafeFileHandle) As DiskExtent()

            ' 776 is enough to hold 32 disk extent items
            Dim buffer = DeviceIoControl(volume, NativeConstants.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, Nothing, 776)

            Dim number = MemoryMarshal.Read(Of Integer)(buffer.Span)

            Return MemoryMarshal.Cast(Of Byte, DiskExtent)(buffer.Span.Slice(8)).ToArray()

        End Function

        Public Shared Function GetPartitionInformation(DevicePath As ReadOnlyMemory(Of Char)) As PARTITION_INFORMATION?

            Using devicehandle = OpenFileHandle(DevicePath, FileAccess.Read, FileShare.ReadWrite, FileMode.Open, 0)

                Return GetPartitionInformation(devicehandle)

            End Using

        End Function

        Public Shared Function GetPartitionInformation(disk As SafeFileHandle) As PARTITION_INFORMATION?

            Dim partition_info As PARTITION_INFORMATION = Nothing

            If UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_PARTITION_INFO_EX,
                                          IntPtr.Zero, 0, partition_info, CUInt(Marshal.SizeOf(Of PARTITION_INFORMATION)()),
                                          0, IntPtr.Zero) Then
                Return partition_info
            Else
                Return Nothing
            End If

        End Function

        Public Shared Function GetPartitionInformationEx(DevicePath As ReadOnlyMemory(Of Char)) As PARTITION_INFORMATION_EX?

            Using devicehandle = OpenFileHandle(DevicePath, 0, FileShare.ReadWrite, FileMode.Open, 0)

                Return GetPartitionInformationEx(devicehandle)

            End Using

        End Function

        Public Shared Function GetPartitionInformationEx(disk As SafeFileHandle) As PARTITION_INFORMATION_EX?

            Dim partition_info As PARTITION_INFORMATION_EX = Nothing

            If UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_PARTITION_INFO_EX,
                                          IntPtr.Zero, 0, partition_info, CUInt(Marshal.SizeOf(Of PARTITION_INFORMATION_EX)()),
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

        Public Shared Function GetDriveLayoutEx(DevicePath As ReadOnlyMemory(Of Char)) As DriveLayoutInformation

            Using devicehandle = OpenFileHandle(DevicePath, FileAccess.Read, FileShare.ReadWrite, FileMode.Open, 0)

                Return GetDriveLayoutEx(devicehandle)

            End Using

        End Function

        Public Shared Function GetDriveLayoutEx(disk As SafeFileHandle) As DriveLayoutInformation

            Dim max_partitions = 4

            Do

                Dim buffer_size = PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_EX).TypeSize +
                    PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_GPT).TypeSize +
                    max_partitions * PinnedBuffer(Of PARTITION_INFORMATION_EX).TypeSize

                Dim buffer = ArrayPool(Of Byte).Shared.Rent(buffer_size)

                Try
                    If Not UnsafeNativeMethods.DeviceIoControl(disk,
                                                               NativeConstants.IOCTL_DISK_GET_DRIVE_LAYOUT_EX,
                                                               IntPtr.Zero,
                                                               0,
                                                               buffer(0),
                                                               CUInt(buffer.Length),
                                                               0,
                                                               IntPtr.Zero) Then

                        If Marshal.GetLastWin32Error() = NativeConstants.ERROR_INSUFFICIENT_BUFFER Then
                            max_partitions *= 2
                            Continue Do
                        End If

                        Return Nothing

                    End If

                    Dim layout = MemoryMarshal.Read(Of DRIVE_LAYOUT_INFORMATION_EX)(buffer)

                    If layout.PartitionCount > max_partitions Then
                        max_partitions *= 2
                        Continue Do
                    End If

                    Dim partitions(0 To layout.PartitionCount - 1) As PARTITION_INFORMATION_EX

                    For i = 0 To layout.PartitionCount - 1

                        Dim prt = buffer.AsSpan(PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_EX).TypeSize +
                                                PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_GPT).TypeSize +
                                                i * PinnedBuffer(Of PARTITION_INFORMATION_EX).TypeSize)

                        partitions(i) = MemoryMarshal.Read(Of PARTITION_INFORMATION_EX)(buffer.AsSpan(PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_EX).TypeSize +
                                                                                                      PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_GPT).TypeSize +
                                                                                                      i * PinnedBuffer(Of PARTITION_INFORMATION_EX).TypeSize))

                    Next

                    If layout.PartitionStyle = PARTITION_STYLE.MBR Then
                        Dim mbr = MemoryMarshal.Read(Of DRIVE_LAYOUT_INFORMATION_MBR)(buffer.AsSpan(PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_EX).TypeSize))
                        Return New DriveLayoutInformationMBR(layout, partitions, mbr)
                    ElseIf layout.PartitionStyle = PARTITION_STYLE.GPT Then
                        Dim gpt = MemoryMarshal.Read(Of DRIVE_LAYOUT_INFORMATION_GPT)(buffer.AsSpan(PinnedBuffer(Of DRIVE_LAYOUT_INFORMATION_EX).TypeSize))
                        Return New DriveLayoutInformationGPT(layout, partitions, gpt)
                    Else
                        Return New DriveLayoutInformation(layout, partitions)
                    End If

                Finally
                    ArrayPool(Of Byte).Shared.Return(buffer)

                End Try

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

            Dim buffer = ArrayPool(Of Byte).Shared.Rent(size_needed)

            Try
                MemoryMarshal.Write(buffer.AsSpan(pos), layout.DriveLayoutInformation)

                pos += drive_layout_information_ex_size

                Select Case layout.DriveLayoutInformation.PartitionStyle

                    Case PARTITION_STYLE.MBR
                        MemoryMarshal.Write(buffer.AsSpan(pos), DirectCast(layout, DriveLayoutInformationMBR).MBR)

                    Case PARTITION_STYLE.GPT
                        MemoryMarshal.Write(buffer.AsSpan(pos), DirectCast(layout, DriveLayoutInformationGPT).GPT)

                End Select

                pos += drive_layout_information_record_size

                For i = 0 To partition_count - 1
                    MemoryMarshal.Write(buffer.AsSpan(pos + i * partition_struct_size), layout.Partitions(i))
                Next

                Win32Try(UnsafeNativeMethods.DeviceIoControl(disk,
                                                             NativeConstants.IOCTL_DISK_SET_DRIVE_LAYOUT_EX,
                                                             buffer(0),
                                                             CUInt(size_needed),
                                                             IntPtr.Zero,
                                                             0,
                                                             0,
                                                             IntPtr.Zero))

            Finally
                ArrayPool(Of Byte).Shared.Return(buffer)

            End Try

        End Sub

        Public Shared Sub FlushBuffers(handle As SafeFileHandle)
            Win32Try(UnsafeNativeMethods.FlushFileBuffers(handle))
        End Sub

        Public Shared Function GetDiskOffline(disk As SafeFileHandle) As Boolean?

            Dim attribs As GET_DISK_ATTRIBUTES

            If UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_DISK_ATTRIBUTES,
                                          IntPtr.Zero, 0, attribs, PinnedBuffer(Of GET_DISK_ATTRIBUTES).TypeSize,
                                          0, IntPtr.Zero) Then
                Return attribs.Attributes.HasFlag(DiskAttributes.Offline)
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

            Dim attribs As New SET_DISK_ATTRIBUTES(
                flags:=DiskAttributesFlags.None,
                attributesMask:=DiskAttributes.Offline,
                attributes:=If(offline, DiskAttributes.Offline, DiskAttributes.None)
            )

            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_SET_DISK_ATTRIBUTES,
                                          attribs, attribs.Version, IntPtr.Zero, 0,
                                          0, IntPtr.Zero))

        End Sub

        Public Shared Function GetDiskReadOnly(disk As SafeFileHandle) As Boolean?

            Dim attribs As GET_DISK_ATTRIBUTES

            If UnsafeNativeMethods.DeviceIoControl(disk, NativeConstants.IOCTL_DISK_GET_DISK_ATTRIBUTES,
                                          IntPtr.Zero, 0, attribs, PinnedBuffer(Of GET_DISK_ATTRIBUTES).TypeSize,
                                          0, IntPtr.Zero) Then
                Return attribs.Attributes.HasFlag(DiskAttributes.ReadOnly)
            Else
                Return Nothing
            End If

        End Function

        Public Shared Sub SetDiskReadOnly(disk As SafeFileHandle, read_only As Boolean)

            Dim attribs As New SET_DISK_ATTRIBUTES(
                flags:=DiskAttributesFlags.None,
                attributesMask:=DiskAttributes.ReadOnly,
                attributes:=If(read_only, DiskAttributes.ReadOnly, DiskAttributes.None)
            )

            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk,
                                                         NativeConstants.IOCTL_DISK_SET_DISK_ATTRIBUTES,
                                                         attribs,
                                                         attribs.Version,
                                                         IntPtr.Zero,
                                                         0,
                                                         0,
                                                         IntPtr.Zero))

        End Sub

        Public Shared Sub SetVolumeOffline(disk As SafeFileHandle, offline As Boolean)

            Win32Try(UnsafeNativeMethods.DeviceIoControl(disk,
                                                         If(offline, NativeConstants.IOCTL_VOLUME_OFFLINE, NativeConstants.IOCTL_VOLUME_ONLINE),
                                                         IntPtr.Zero,
                                                         0,
                                                         IntPtr.Zero,
                                                         0,
                                                         0,
                                                         IntPtr.Zero))

        End Sub

        Public Shared Function GetExceptionForNtStatus(NtStatus As Integer) As Exception

            Return New Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(NtStatus))

        End Function

        Public Shared Function GetModuleFullPath(hModule As IntPtr) As String

            Dim str = ArrayPool(Of Char).Shared.Rent(32768)
            Try
                Dim PathLength = UnsafeNativeMethods.GetModuleFileNameW(hModule, str(0), str.Length)
                If PathLength = 0 Then
                    Throw New Win32Exception
                End If

                Return str.AsSpan(0, PathLength).ToString()

            Finally
                ArrayPool(Of Char).Shared.Return(str)

            End Try

        End Function

        Public Shared Function EnumerateDiskVolumesMountPoints(DiskDevice As ReadOnlyMemory(Of Char)) As IEnumerable(Of String)

            Return EnumerateDiskVolumes(DiskDevice).SelectMany(AddressOf EnumerateVolumeMountPoints)

        End Function

        Public Shared Function EnumerateDiskVolumesMountPoints(DiskNumber As UInteger) As IEnumerable(Of String)

            Return EnumerateDiskVolumes(DiskNumber).SelectMany(AddressOf EnumerateVolumeMountPoints)

        End Function

        Public Shared Function GetVolumeNameForVolumeMountPoint(MountPoint As String) As String

            Return GetVolumeNameForVolumeMountPoint(MountPoint.AsMemory())

        End Function

        Public Shared Function GetVolumeNameForVolumeMountPoint(MountPoint As ReadOnlyMemory(Of Char)) As String

            Dim str = ArrayPool(Of Char).Shared.Rent(50)
            Try
                If UnsafeNativeMethods.GetVolumeNameForVolumeMountPointW(MountPoint.MakeNullTerminated(), str(0), str.Length) AndAlso
                    str(0) <> Nothing Then

                    Return str.AsSpan().ReadNullTerminatedUnicodeString()

                End If

            Finally
                ArrayPool(Of Char).Shared.Return(str)

            End Try

            Dim ptr = MountPoint

            If ptr.Span.StartsWith("\\?\".AsSpan(), StringComparison.Ordinal) Then
                ptr = ptr.Slice(4)
            End If

            ptr = ptr.TrimEnd("\"c)

            Dim nt_device_path = QueryDosDevice(ptr).FirstOrDefault()

            If String.IsNullOrWhiteSpace(nt_device_path) Then

                Return Nothing

            End If

            Dim found =
                Aggregate dosdevice In QueryDosDevice()
                Where dosdevice.Length = 44 AndAlso dosdevice.StartsWith("Volume{", StringComparison.OrdinalIgnoreCase)
                Let targets = QueryDosDevice(dosdevice)
                Where targets.
                    Any(Function(target) target.Equals(nt_device_path, StringComparison.OrdinalIgnoreCase))
                Into FirstOrDefault()

            If found Is Nothing Then
                Return Nothing
            End If

            Return $"\\?\{found.dosdevice}\"

        End Function

        Public Shared Function GetVolumePathName(path As String) As String

            Return GetVolumePathName(path.AsMemory())

        End Function

        Public Shared Function GetVolumePathName(path As ReadOnlyMemory(Of Char)) As String

            Const CchBufferLength = 32768

            Dim result = ArrayPool(Of Char).Shared.Rent(CchBufferLength)
            Try
                If Not UnsafeNativeMethods.GetVolumePathNameW(path.MakeNullTerminated(),
                                                             result(0),
                                                             CchBufferLength) Then

                    Throw New IOException($"Failed to get volume name for path '{path}'", New Win32Exception)
                End If

                Return result.AsSpan().ReadNullTerminatedUnicodeString()

            Finally
                ArrayPool(Of Char).Shared.Return(result)

            End Try

        End Function

        Public Shared Function TryGetVolumePathName(path As ReadOnlyMemory(Of Char), <Out> ByRef volume As String) As Boolean

            Const CchBufferLength = 32768

            Dim result = ArrayPool(Of Char).Shared.Rent(CchBufferLength)
            Try
                If Not UnsafeNativeMethods.GetVolumePathNameW(path.MakeNullTerminated(),
                                                             result(0),
                                                             CchBufferLength) Then

                    Return False
                End If

                volume = result.AsSpan().ReadNullTerminatedUnicodeString()
                Return True

            Finally
                ArrayPool(Of Char).Shared.Return(result)

            End Try

        End Function

        Public Shared Function GetScsiAddressAndLength(drv As ReadOnlyMemory(Of Char)) As ScsiAddressAndLength?

            Static SizeOfLong As UInteger = CUInt(PinnedBuffer(Of Long).TypeSize)
            Static SizeOfScsiAddress As UInteger = CUInt(PinnedBuffer(Of SCSI_ADDRESS).TypeSize)

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

            Return GetMountPointBasedPath(path.AsMemory())

        End Function

        Public Shared Function GetMountPointBasedPath(path As ReadOnlyMemory(Of Char)) As String

            Const volume_path_prefix = "\\?\Volume{00000000-0000-0000-0000-000000000000}\"

            If path.Length > volume_path_prefix.Length AndAlso
                path.Span.StartsWith("\\?\Volume{".AsSpan(), StringComparison.OrdinalIgnoreCase) Then

                Dim vol = path.Slice(0, volume_path_prefix.Length)
                Dim mountpoint = EnumerateVolumeMountPoints(vol)?.FirstOrDefault()

                If mountpoint IsNot Nothing Then
#If NET6_0_OR_GREATER Then
                    Return String.Concat(mountpoint.AsSpan(), path.Span.Slice(volume_path_prefix.Length))
#Else
                    Return $"{mountpoint}{path.Slice(volume_path_prefix.Length)}"
#End If
                End If

            End If

            Return path.ToString()

        End Function

        Public Shared Function EnumerateVolumeMountPoints(VolumeName As String) As IEnumerable(Of String)

            Return EnumerateVolumeMountPoints(VolumeName.AsMemory())

        End Function

        Public Shared Iterator Function EnumerateVolumeMountPoints(VolumeName As ReadOnlyMemory(Of Char)) As IEnumerable(Of String)

            Const CchBufferLength = 65536

            Dim TargetPath = ArrayPool(Of Char).Shared.Rent(CchBufferLength)
            Try
                Dim length As Integer

                If UnsafeNativeMethods.GetVolumePathNamesForVolumeNameW(VolumeName.MakeNullTerminated(),
                                                                        TargetPath(0),
                                                                        CchBufferLength,
                                                                        length) AndAlso
                    length > 2 Then

                    For Each s In TargetPath.AsMemory(0, length).ParseDoubleTerminatedString()
                        Yield s.ToString()
                    Next

                    Return

                End If

            Finally
                ArrayPool(Of Char).Shared.Return(TargetPath)

            End Try

            If VolumeName.Span.StartsWith("\\?\Volume{".AsSpan(), StringComparison.OrdinalIgnoreCase) Then
                VolumeName = VolumeName.Slice("\\?\".Length, 44)
            ElseIf VolumeName.Span.StartsWith("Volume{".AsSpan(), StringComparison.OrdinalIgnoreCase) Then
                VolumeName = VolumeName.Slice(0, 44)
            Else
                Return
            End If

            Dim targetdev = QueryDosDevice(VolumeName).FirstOrDefault()

            If String.IsNullOrWhiteSpace(targetdev) Then
                Return
            End If

            Dim namelinks = From link In QueryDosDevice()
                            Where link.Length = 2 AndAlso link(1) = ":"c
                            From target In QueryDosDevice(link)
                            Where targetdev.Equals(target, StringComparison.OrdinalIgnoreCase)

            For Each namelink In namelinks
                Yield $"{namelink.link}\"
            Next

        End Function

        Public Shared Function EnumerateDiskVolumes(DevicePath As String) As IEnumerable(Of String)

            Return EnumerateDiskVolumes(DevicePath.AsMemory())

        End Function

        Public Shared Function EnumerateDiskVolumes(DevicePath As ReadOnlyMemory(Of Char)) As IEnumerable(Of String)

            If DevicePath.Span.StartsWith("\\?\PhysicalDrive".AsSpan(), StringComparison.OrdinalIgnoreCase) OrElse
                DevicePath.Span.StartsWith("\\.\PhysicalDrive".AsSpan(), StringComparison.OrdinalIgnoreCase) Then          ' \\?\PhysicalDrive paths to partitioned disks

#If NETCOREAPP OrElse NETSTANDARD2_1_OR_GREATER Then
                Return EnumerateDiskVolumes(UInteger.Parse(DevicePath.Slice("\\?\PhysicalDrive".Length).Span))
#Else
                Return EnumerateDiskVolumes(UInteger.Parse(DevicePath.Slice("\\?\PhysicalDrive".Length).ToString()))
#End If

            ElseIf DevicePath.Span.StartsWith("\\?\".AsSpan(), StringComparison.Ordinal) OrElse
                DevicePath.Span.StartsWith("\\.\".AsSpan(), StringComparison.Ordinal) Then

                Return EnumerateVolumeNamesForDeviceObject(QueryDosDevice(DevicePath.Slice("\\?\".Length)).First())     ' \\?\C: or similar paths to mounted volumes

            Else

                Return Enumerable.Empty(Of String)()

            End If

        End Function

        Public Shared Function EnumerateDiskVolumes(DiskNumber As UInteger) As IEnumerable(Of String)

            Return New VolumeEnumerator().Where(
                Function(volumeGuid)
                    Try
                        Return VolumeUsesDisk(volumeGuid.AsMemory(), DiskNumber)

                    Catch ex As Exception
                        Trace.WriteLine($"{volumeGuid}: {ex.JoinMessages()}")
                        Return False

                    End Try
                End Function)

        End Function

        Public Shared Function EnumerateVolumeNamesForDeviceObject(DeviceObject As String) As IEnumerable(Of String)

            If DeviceObject.EndsWith("}"c) AndAlso
                DeviceObject.StartsWith("\Device\Volume{", StringComparison.Ordinal) Then

                Return {$"\\?\{DeviceObject.Substring("\Device\".Length)}\"}

            End If

            Return New VolumeEnumerator().Where(
                Function(volumeGuidStr)

                    Dim volumeGuid = volumeGuidStr.AsMemory()

                    Try
                        If volumeGuid.Span.StartsWith("\\?\".AsSpan(), StringComparison.Ordinal) Then
                            volumeGuid = volumeGuid.Slice(4)
                        End If

                        volumeGuid = volumeGuid.TrimEnd("\"c)

                        Return _
                            Aggregate target In QueryDosDevice(volumeGuid)
                                Into Any(target.Equals(DeviceObject, StringComparison.OrdinalIgnoreCase))

                    Catch ex As Exception
                        Trace.WriteLine($"{volumeGuidStr}: {ex.JoinMessages()}")
                        Return False

                    End Try
                End Function)

        End Function

        Public Shared Function VolumeUsesDisk(VolumeGuid As ReadOnlyMemory(Of Char), DiskNumber As UInteger) As Boolean

            Using volume As New DiskDevice(VolumeGuid.TrimEnd("\"c), 0)

                Try
                    Dim extents = GetVolumeDiskExtents(volume.SafeFileHandle)

                    Return extents.Any(Function(extent) extent.DiskNumber.Equals(DiskNumber))

                Catch ex As Win32Exception When ex.NativeErrorCode = NativeConstants.ERROR_INVALID_FUNCTION

                    Return False

                End Try

            End Using

        End Function

        Public Shared Sub ScanForHardwareChanges()

            ScanForHardwareChanges(Nothing)

        End Sub

        Public Shared Function ScanForHardwareChanges(rootid As ReadOnlyMemory(Of Char)) As UInteger

            Dim devInst As UInteger

            Dim status = UnsafeNativeMethods.CM_Locate_DevNodeW(devInst, rootid.MakeNullTerminated(), 0)

            If status <> 0 Then
                Return status
            End If

            Return UnsafeNativeMethods.CM_Reenumerate_DevNode(devInst, 0)

        End Function

        Public Shared Function GetDevInst(devinstName As ReadOnlyMemory(Of Char)) As UInteger?

            Dim devInst As UInteger

            Dim status = UnsafeNativeMethods.CM_Locate_DevNodeW(devInst, devinstName.MakeNullTerminated(), 0)

            If status <> 0 Then
                Trace.WriteLine($"Device '{devinstName}' error 0x{status:X}")
                Return Nothing
            End If

#If DEBUG Then
            Trace.WriteLine($"{devinstName} = devInst {devInst}")
#End If

            Return devInst

        End Function

        Public Shared Function EnumerateDeviceInstancesForService(service As ReadOnlyMemory(Of Char), <Out> ByRef instances As IEnumerable(Of ReadOnlyMemory(Of Char))) As UInteger

            Dim length As Integer
            Dim status = UnsafeNativeMethods.CM_Get_Device_ID_List_SizeW(length, service.MakeNullTerminated(), NativeConstants.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            Dim Buffer(0 To length - 1) As Char
            status = UnsafeNativeMethods.CM_Get_Device_ID_ListW(service.MakeNullTerminated(),
                                                                Buffer(0),
                                                                CUInt(Buffer.Length),
                                                                NativeConstants.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            instances = Buffer.AsMemory(0, length).ParseDoubleTerminatedString()

            Return status

        End Function

        Public Shared Function EnumerateDeviceInstancesForSetupClass(service As ReadOnlyMemory(Of Char), <Out> ByRef instances As IEnumerable(Of ReadOnlyMemory(Of Char))) As UInteger

            Dim length As Integer
            Dim status = UnsafeNativeMethods.CM_Get_Device_ID_List_SizeW(length, service.MakeNullTerminated(), NativeConstants.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            Dim Buffer(0 To length - 1) As Char
            status = UnsafeNativeMethods.CM_Get_Device_ID_ListW(service.MakeNullTerminated(),
                                                                Buffer(0),
                                                                CUInt(Buffer.Length),
                                                                NativeConstants.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            instances = Buffer.AsMemory(0, length).ParseDoubleTerminatedString()

            Return status

        End Function

        Public Shared Sub RestartDevice(devclass As Guid, devinst As UInteger)

            '' get a list of devices which support the given interface
            Using devinfo = UnsafeNativeMethods.SetupDiGetClassDevsW(devclass,
                Nothing,
                Nothing,
                NativeConstants.DIGCF_PROFILE Or
                NativeConstants.DIGCF_DEVICEINTERFACE Or
                NativeConstants.DIGCF_PRESENT)

                If devinfo.IsInvalid Then
                    Throw New Exception("Device not found")
                End If

                Dim devInfoData As New SP_DEVINFO_DATA

                '' step through the list of devices for this handle
                '' get device info at index deviceIndex, the function returns FALSE
                '' when there is no device at the given index.
                Dim deviceIndex = 0UI

                While UnsafeNativeMethods.SetupDiEnumDeviceInfo(devinfo, deviceIndex, devInfoData)

                    If devInfoData.DevInst.Equals(devinst) Then
                        Dim pcp As New SP_PROPCHANGE_PARAMS(
                            classInstallHeader:=New SP_CLASSINSTALL_HEADER(
                                installFunction:=NativeConstants.DIF_PROPERTYCHANGE),
                            hwProfile:=0,
                            scope:=NativeConstants.DICS_FLAG_CONFIGSPECIFIC,
                            stateChange:=NativeConstants.DICS_PROPCHANGE)

                        If UnsafeNativeMethods.SetupDiSetClassInstallParamsW(devinfo, devInfoData, pcp, PinnedBuffer(Of SP_PROPCHANGE_PARAMS).TypeSize) AndAlso
                            UnsafeNativeMethods.SetupDiCallClassInstaller(NativeConstants.DIF_PROPERTYCHANGE, devinfo, devInfoData) Then

                            Return
                        End If

                        Throw New Exception($"Device restart failed", New Win32Exception)
                    End If

                    deviceIndex += 1UI

                End While

            End Using

            Throw New Exception("Device not found")

        End Sub

        Public Shared Sub RunDLLInstallHinfSection(OwnerWindow As IntPtr, InfPath As String, InfSection As ReadOnlyMemory(Of Char))

            Dim cmdLine = $"{InfSection} 132 {InfPath}"
            Trace.WriteLine($"RunDLLInstallFromInfSection: {cmdLine}")

            If InfPath.NullCheck(NameOf(InfPath)).Contains(" "c) Then
                Throw New ArgumentException("Arguments to this method cannot contain spaces.", NameOf(InfPath))
            End If

            If InfSection.Span.Contains(" "c) Then
                Throw New ArgumentException("Arguments to this method cannot contain spaces.", NameOf(InfSection))
            End If

            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            UnsafeNativeMethods.InstallHinfSectionW(OwnerWindow,
                                                    Nothing,
                                                    MemoryMarshal.GetReference(cmdLine.AsSpan()),
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

            Dim ErrorLine As UInteger
            Dim hInf = UnsafeNativeMethods.SetupOpenInfFileW(MemoryMarshal.GetReference(InfPath.AsSpan()),
                                                             Nothing,
                                                             &H2UI,
                                                             ErrorLine)
            If hInf.IsInvalid Then
                Throw New Win32Exception($"Line number: {ErrorLine}")
            End If

            Using hInf

                Win32Try(UnsafeNativeMethods.SetupInstallFromInfSectionW(OwnerWindow,
                                                                         hInf,
                                                                         MemoryMarshal.GetReference(InfSection.AsSpan()),
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

        Public Shared Sub CreateRootPnPDevice(OwnerWindow As IntPtr, InfPath As String, hwid As ReadOnlyMemory(Of Char), ForceReplaceExistingDrivers As Boolean, <Out> ByRef RebootRequired As Boolean)

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
            Dim hwIdList = MemoryMarshal.AsBytes($"{hwid}{New Char}{New Char}".AsSpan())

            ''
            '' Use the INF File to extract the Class GUID.
            ''
            Dim ClassGUID As Guid
            Dim ClassName(0 To 31) As Char
            Win32Try(UnsafeNativeMethods.SetupDiGetINFClassW(MemoryMarshal.GetReference(InfPath.AsSpan()),
                                                             ClassGUID,
                                                             ClassName(0),
                                                             32UI,
                                                             0))

            Trace.WriteLine($"CreateOrUpdateRootPnPDevice: ClassGUID=""{ClassGUID}"", ClassName=""{ClassName.AsMemory()}""")

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
                Dim DeviceInfoData As New SP_DEVINFO_DATA

                Win32Try(UnsafeNativeMethods.SetupDiCreateDeviceInfoW(DeviceInfoSet,
                                                                     ClassName(0),
                                                                     ClassGUID,
                                                                     Nothing,
                                                                     OwnerWindow,
                                                                     &H1UI,
                                                                     DeviceInfoData))

                ''
                '' Add the HardwareID to the Device's HardwareID property.
                ''
                Win32Try(UnsafeNativeMethods.SetupDiSetDeviceRegistryPropertyW(DeviceInfoSet,
                                                                               DeviceInfoData,
                                                                               &H1UI,
                                                                               MemoryMarshal.GetReference(hwIdList),
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

        Public Shared Iterator Function EnumerateChildDevices(devInst As UInteger) As IEnumerable(Of UInteger)

            Dim child As UInteger

            Dim rc = UnsafeNativeMethods.CM_Get_Child(child, devInst, 0)

            While rc = 0

                Trace.WriteLine($"Found child devinst: {child}")

                Yield child

                rc = UnsafeNativeMethods.CM_Get_Sibling(child, child, 0)

            End While

        End Function

        Public Shared Function GetPhysicalDeviceObjectNtPath(devInstName As ReadOnlyMemory(Of Char)) As String

            Dim devinst = GetDevInst(devInstName)

            If Not devinst.HasValue Then
                Return Nothing
            End If

            Return GetPhysicalDeviceObjectNtPath(devinst.Value)

        End Function

        Public Shared Function GetPhysicalDeviceObjectNtPath(devInst As UInteger) As String

            Dim regtype As RegistryValueKind = Nothing

            Dim buffersize = 518
            Dim buffer = ArrayPool(Of Byte).Shared.Rent(buffersize)
            Try
                Dim rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_PropertyW(devInst, CmDevNodeRegistryProperty.CM_DRP_PHYSICAL_DEVICE_OBJECT_NAME, regtype, buffer(0), buffersize, 0)

                If rc <> 0 Then
                    Trace.WriteLine($"Error getting registry property for device {devInst}. Status=0x{rc:X}")
                    Return Nothing
                End If

                Dim name = MemoryMarshal.Cast(Of Byte, Char)(buffer.AsSpan(0, buffersize - 2)).ToString()

#If DEBUG Then
                Trace.WriteLine($"Found physical device object name: '{name}'")
#End If

                Return name

            Finally
                ArrayPool(Of Byte).Shared.Return(buffer)

            End Try

        End Function

        Public Shared Function GetDeviceRegistryProperty(devInst As UInteger, prop As CmDevNodeRegistryProperty) As IEnumerable(Of String)

            Dim regtype As RegistryValueKind = Nothing

            Dim buffersize = 518
            Dim buffer = ArrayPool(Of Byte).Shared.Rent(buffersize)
            Try
                Dim rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_PropertyW(devInst, prop, regtype, buffer(0), buffersize, 0)

                If rc <> 0 Then
                    Trace.WriteLine($"Error getting registry property for device {devInst}. Status=0x{rc:X}")
                    Return Nothing
                End If

                Dim name = buffer.AsMemory(0, buffersize).ParseDoubleTerminatedString()

                Return name

            Finally
                ArrayPool(Of Byte).Shared.Return(buffer)

            End Try

        End Function

        Public Shared Function EnumerateWin32DevicePaths(nt_device_path As ReadOnlyMemory(Of Char)) As IEnumerable(Of String)

            Dim query =
                From dosdevice In QueryDosDevice()
                Where QueryDosDevice(dosdevice).
                    Any(Function(target) MemoryExtensions.Equals(target.AsSpan(), nt_device_path.Span, StringComparison.OrdinalIgnoreCase))

            Return From dosdevice In query Select $"\\?\{dosdevice}"

        End Function

        Public Shared Function EnumerateRegisteredFilters(devInst As UInteger) As IEnumerable(Of String)

            Dim regtype As RegistryValueKind = Nothing

            Dim buffersize = 65536
            Dim buffer = ArrayPool(Of Byte).Shared.Rent(buffersize)
            Try
                Dim rc = UnsafeNativeMethods.CM_Get_DevNode_Registry_PropertyW(devInst, CmDevNodeRegistryProperty.CM_DRP_UPPERFILTERS, regtype, buffer(0), buffersize, 0)

                If rc = NativeConstants.CR_NO_SUCH_VALUE Then
                    Return Enumerable.Empty(Of String)()
                ElseIf rc <> 0 Then
                    Dim msg = $"Error getting registry property for device. Status=0x{rc:X}"
                    Throw New IOException(msg)
                End If

                Return buffer.AsMemory(0, buffersize).ParseDoubleTerminatedString()

            Finally
                ArrayPool(Of Byte).Shared.Return(buffer)

            End Try

        End Function

        '' Switched to querying registry directly instead. CM_Get_Class_Registry_PropertyW seems to
        '' return 0x13 CR_FAILURE on Win7.
#If USE_CM_API Then

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

#Else

        Public Shared Function GetRegisteredFilters(devClass As Guid) As String()

            Using key = Registry.LocalMachine.OpenSubKey($"SYSTEM\CurrentControlSet\Control\Class\{devClass:B}")

                Return TryCast(key?.GetValue("UpperFilters"), String())

            End Using

        End Function

#End If

        Public Shared Sub SetRegisteredFilters(devInst As UInteger, filters As IEnumerable(Of String))

            Dim str = $"{String.Join(New Char, filters)}{New Char}{New Char}"
            Dim buffer = MemoryMarshal.AsBytes(str.AsSpan())
            Dim buffersize = buffer.Length

            Dim rc = UnsafeNativeMethods.CM_Set_DevNode_Registry_PropertyW(devInst,
                                                                           CmDevNodeRegistryProperty.CM_DRP_UPPERFILTERS,
                                                                           MemoryMarshal.GetReference(buffer),
                                                                           buffersize,
                                                                           0)

            If rc <> 0 Then
                Throw New Exception($"Error setting registry property for device. Status=0x{rc:X}")
            End If

        End Sub

        Public Shared Sub SetRegisteredFilters(devClass As Guid, filters As IEnumerable(Of String))

            Dim str = $"{String.Join(New Char, filters)}{New Char}{New Char}"
            Dim buffer = MemoryMarshal.AsBytes(str.AsSpan())
            Dim buffersize = buffer.Length

            Dim rc = UnsafeNativeMethods.CM_Set_Class_Registry_PropertyW(devClass,
                                                                         CmClassRegistryProperty.CM_CRP_UPPERFILTERS,
                                                                         MemoryMarshal.GetReference(buffer),
                                                                         buffersize,
                                                                         0)

            If rc <> 0 Then
                Throw New Exception($"Error setting registry property for class {devClass}. Status=0x{rc:X}")
            End If

        End Sub

        Public Shared Function AddFilter(devInst As UInteger, driver As String) As Boolean

            Dim filters = EnumerateRegisteredFilters(devInst).ToList()

            If filters.Any(Function(f) f.Equals(driver, StringComparison.OrdinalIgnoreCase)) Then

                Trace.WriteLine($"Filter '{driver}' already registered for devinst {devInst}")

                Return False

            End If

            Trace.WriteLine($"Registering filter '{driver}' for devinst {devInst}")

            filters.Add(driver)

            SetRegisteredFilters(devInst, filters)

            Return True

        End Function

        Public Shared Function AddFilter(devClass As Guid, driver As String, addfirst As Boolean) As Boolean

            driver.NullCheck(NameOf(driver))

            Dim filters = GetRegisteredFilters(devClass)

            If filters Is Nothing Then

                filters = Array.Empty(Of String)()

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

            Trace.WriteLine($"Registering filters '{String.Join(",", filter_list)}' for class {devClass}")

            SetRegisteredFilters(devClass, filter_list)

            Return True

        End Function

        Public Shared Function RemoveFilter(devInst As UInteger, driver As String) As Boolean

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

        Public Shared Function RemovePnPDevice(OwnerWindow As IntPtr, hwid As ReadOnlyMemory(Of Char)) As Integer

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

                If Not UnsafeNativeMethods.SetupDiOpenDeviceInfoW(DeviceInfoSet,
                                                                  hwid.MakeNullTerminated(),
                                                                  OwnerWindow,
                                                                  0,
                                                                  IntPtr.Zero) Then
                    Return 0
                End If

                Dim DeviceInfoData As New SP_DEVINFO_DATA

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

        Public Shared Sub UpdateDriverForPnPDevices(OwnerWindow As IntPtr, InfPath As String, hwid As ReadOnlyMemory(Of Char), forceReplaceExisting As Boolean, <Out> ByRef RebootRequired As Boolean)

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
            Win32Try(UnsafeNativeMethods.UpdateDriverForPlugAndPlayDevicesW(OwnerWindow,
                                                                            hwid.MakeNullTerminated(),
                                                                            MemoryMarshal.GetReference(InfPath.AsSpan()),
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

            Dim destName = ArrayPool(Of Char).Shared.Rent(260)
            Try

                Win32Try(UnsafeNativeMethods.SetupCopyOEMInfW(MemoryMarshal.GetReference(InfPath.AsSpan()),
                                                              Nothing,
                                                              0,
                                                              If(NoOverwrite, &H8UI, &H0UI),
                                                              destName(0),
                                                              destName.Length,
                                                              Nothing,
                                                              Nothing))

                Return destName.AsSpan().ReadNullTerminatedUnicodeString()

            Finally
                ArrayPool(Of Char).Shared.Return(destName)

            End Try

        End Function

        Public Shared Sub DriverPackagePreinstall(InfPath As String)

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim errcode = UnsafeNativeMethods.DriverPackagePreinstallW(MemoryMarshal.GetReference(InfPath.AsSpan()), 1)
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

            Dim errcode = UnsafeNativeMethods.DriverPackageInstallW(MemoryMarshal.GetReference(InfPath.AsSpan()), 1, Nothing, NeedReboot)
            If errcode <> 0 Then
                Throw New Win32Exception(errcode)
            End If

        End Sub

        Public Shared Sub DriverPackageUninstall(InfPath As String, Flags As DriverPackageUninstallFlags, <Out> ByRef NeedReboot As Boolean)

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim errcode = UnsafeNativeMethods.DriverPackageUninstallW(MemoryMarshal.GetReference(InfPath.AsSpan()), Flags, Nothing, NeedReboot)
            If errcode <> 0 Then
                Throw New Win32Exception(errcode)
            End If

        End Sub

        Public Shared Function MapFileAndCheckSum(file As ReadOnlyMemory(Of Char), <Out> ByRef headerSum As Integer, <Out> ByRef checkSum As Integer) As Boolean

            Return UnsafeNativeMethods.MapFileAndCheckSumW(file.MakeNullTerminated(), headerSum, checkSum) = 0

        End Function

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
                    Using device = OpenFileHandle($"\\?\{diskdevice}".AsMemory(), 0, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

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
        Public Shared Function UpdateDiskProperties(DevicePath As ReadOnlyMemory(Of Char)) As Boolean

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
        Public Shared Function OpenDiskByScsiAddress(ScsiAddress As SCSI_ADDRESS, AccessMode As FileAccess) As KeyValuePair(Of ReadOnlyMemory(Of Char), SafeFileHandle)

            Dim dosdevs = QueryDosDevice()

            Dim rawdevices =
                From device In dosdevs
                Where
                    device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) OrElse
                    device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)

            Dim volumedevices =
                From device In dosdevs
                Where device.Length = 2 AndAlso device(1) = ":"c

            Dim filter =
                Function(diskdevice As String) As KeyValuePair(Of ReadOnlyMemory(Of Char), SafeFileHandle)

                    diskdevice = $"\\?\{diskdevice}"

                    Try
                        Dim devicehandle = OpenFileHandle(diskdevice.AsMemory(), AccessMode, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

                        Try
                            Dim Address = GetScsiAddress(devicehandle)

                            If Not Address.HasValue OrElse Not Address.Value.Equals(ScsiAddress) Then

                                devicehandle.Dispose()

                                Return Nothing

                            End If

                            Trace.WriteLine($"Found {diskdevice} with SCSI address {Address}")

                            Return New KeyValuePair(Of ReadOnlyMemory(Of Char), SafeFileHandle)(diskdevice.AsMemory(), devicehandle)

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
                    Select seldevice = filter(anydevice)
                        Into FirstOrDefault(Not seldevice.Key.IsEmpty)

            If dev.Key.IsEmpty Then
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
                Where device.Length = 2 AndAlso device(1) = ":"c

            Dim filter =
                Function(diskdevicestr As String) As Boolean

                    Dim diskdevice = $"\\?\{diskdevicestr}".AsMemory()

                    Try
                        Dim devicehandle = OpenFileHandle(diskdevice, 0, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

                        Try
                            Dim got_address = GetScsiAddress(devicehandle)

                            If Not got_address.HasValue OrElse Not got_address.Value.Equals(scsi_address) Then

                                Return False

                            End If

                            Trace.WriteLine($"Found {diskdevice} with SCSI address {got_address}")

                            devicehandle.Close()

                            devicehandle = Nothing

                            devicehandle = OpenFileHandle(diskdevice, FileAccess.Read, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

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

        Public Shared Function TestFileOpen(path As ReadOnlyMemory(Of Char)) As Boolean

            Using handle = UnsafeNativeMethods.CreateFileW(path.MakeNullTerminated(),
                       FileSystemRights.ReadAttributes,
                       0,
                       IntPtr.Zero,
                       NativeConstants.OPEN_EXISTING,
                       0,
                       IntPtr.Zero)

                Return Not handle.IsInvalid

            End Using

        End Function

        Public Shared Function TestFileOpen(path As String) As Boolean

            Return TestFileOpen(path.AsMemory())

        End Function

        Public Shared Sub CreateHardLink(existing As ReadOnlyMemory(Of Char), newlink As ReadOnlyMemory(Of Char))

            Win32Try(UnsafeNativeMethods.CreateHardLinkW(newlink.MakeNullTerminated(), existing.MakeNullTerminated(), Nothing))

        End Sub

        Public Shared Sub MoveFile(existing As ReadOnlyMemory(Of Char), newname As ReadOnlyMemory(Of Char))

            Win32Try(UnsafeNativeMethods.MoveFileW(existing.MakeNullTerminated(), newname.MakeNullTerminated()))

        End Sub

        Public Shared Function GetOSVersion() As OperatingSystem

            Dim os_version As New OSVERSIONINFOEX()

            Dim status = UnsafeNativeMethods.RtlGetVersion(os_version)

            If status < 0 Then
                Throw New Win32Exception(UnsafeNativeMethods.RtlNtStatusToDosError(status))
            End If

            Return New OperatingSystem(os_version.PlatformId,
                                   New Version(os_version.MajorVersion,
                                               os_version.MinorVersion,
                                               os_version.BuildNumber,
                                               CInt(os_version.ServicePackMajor) << 16 Or os_version.ServicePackMinor))

        End Function

        ''' <summary>
        ''' Encapsulates a Service Control Management object handle that is closed by calling CloseServiceHandle() Win32 API.
        ''' </summary>
        Public NotInheritable Class SafeServiceHandle
            Inherits SafeHandleZeroOrMinusOneIsInvalid

            Private Declare Unicode Function CloseServiceHandle Lib "advapi32" (
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
            Public Sub New()
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

            Private Declare Unicode Function FindVolumeClose Lib "kernel32" (
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
            Public Sub New()
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

            Private Declare Unicode Function FindVolumeMountPointClose Lib "kernel32" (
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
            Public Sub New()
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

            Private Declare Unicode Sub SetupCloseInfFile Lib "setupapi" (
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
            Public Sub New()
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

            Private Declare Unicode Function SetupDiDestroyDeviceInfoList Lib "setupapi" (
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
            Public Sub New()
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

        <Flags>
        Public Enum DriverPackageUninstallFlags As UInteger
            Normal = &H0UI
            DeleteFiles = NativeConstants.DRIVER_PACKAGE_DELETE_FILES
            Force = NativeConstants.DRIVER_PACKAGE_FORCE
            Silent = NativeConstants.DRIVER_PACKAGE_SILENT
        End Enum

    End Class

End Namespace
