Imports System.Security
Imports Microsoft.VisualBasic
Imports Arsenal.ImageMounter.Extensions

''''' NativeFileIO.vb
''''' Routines for accessing some useful Win32 API functions to access features not
''''' directly accessible through .NET Framework.
''''' 
''''' Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Namespace IO

    ''' <summary>
    ''' Provides wrappers for Win32 file API. This makes it possible to open everyting that
    ''' CreateFile() can open and get a FileStream based .NET wrapper around the file handle.
    ''' </summary>
    Public Class NativeFileIO

#Region "Win32 API"
        Public Class Win32API

            Public Const GENERIC_READ As UInt32 = &H80000000UI
            Public Const GENERIC_WRITE As UInt32 = &H40000000UI
            Public Const GENERIC_ALL As UInt32 = &H10000000

            Public Const FILE_SHARE_READ As UInt32 = &H1UI
            Public Const FILE_SHARE_WRITE As UInt32 = &H2UI
            Public Const FILE_SHARE_DELETE As UInt32 = &H4UI
            Public Const FILE_READ_ATTRIBUTES As UInt32 = &H80UI
            Public Const FILE_ATTRIBUTE_NORMAL As UInt32 = &H80UI
            Public Const FILE_FLAG_OVERLAPPED As UInt32 = &H40000000UI
            Public Const FILE_FLAG_BACKUP_SEMANTICS As UInt32 = &H2000000UI
            Public Const OPEN_ALWAYS As UInt32 = 4UI
            Public Const OPEN_EXISTING As UInt32 = 3UI
            Public Const CREATE_ALWAYS As UInt32 = 2UI
            Public Const CREATE_NEW As UInt32 = 1UI
            Public Const TRUNCATE_EXISTING As UInt32 = 5UI
            Public Const SYNCHRONIZE As UInt32 = &H100000UI

            Public Const ERROR_INVALID_FUNCTION As UInt32 = 1UI
            Public Const ERROR_IO_DEVICE As UInt32 = &H45DUI
            Public Const ERROR_FILE_NOT_FOUND As UInt32 = 2UI
            Public Const ERROR_PATH_NOT_FOUND As UInt32 = 3UI
            Public Const ERROR_ACCESS_DENIED As UInt32 = 5UI
            Public Const ERROR_NO_MORE_FILES As UInt32 = 18UI

            Public Const FSCTL_GET_COMPRESSION As UInt32 = &H9003C
            Public Const FSCTL_SET_COMPRESSION As UInt32 = &H9C040
            Public Const COMPRESSION_FORMAT_NONE As UInt16 = 0US
            Public Const COMPRESSION_FORMAT_DEFAULT As UInt16 = 1US

            Public Const FSCTL_ALLOW_EXTENDED_DASD_IO As UInt32 = &H90083

            Public Const FSCTL_LOCK_VOLUME As UInt32 = &H90018
            Public Const FSCTL_DISMOUNT_VOLUME As UInt32 = &H90020

            Public Const IOCTL_SCSI_MINIPORT As UInt32 = &H4D008
            Public Const IOCTL_SCSI_GET_ADDRESS As UInt32 = &H41018
            Public Const IOCTL_STORAGE_GET_DEVICE_NUMBER As UInt32 = &H2D1080
            Public Const IOCTL_DISK_GET_DRIVE_GEOMETRY As UInt32 = &H70000
            Public Const IOCTL_DISK_GET_LENGTH_INFO As UInt32 = &H7405C
            Public Const IOCTL_DISK_GET_PARTITION_INFO As UInt32 = &H74004
            Public Const IOCTL_DISK_GET_PARTITION_INFO_EX As UInt32 = &H70048
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

            Public Const ERROR_WRITE_PROTECT As UInt32 = 19UI

            Public Const SC_MANAGER_CREATE_SERVICE As UInt32 = &H2
            Public Const SC_MANAGER_ALL_ACCESS As UInt32 = &HF003F
            Public Const SERVICE_KERNEL_DRIVER As UInt32 = &H1
            Public Const SERVICE_FILE_SYSTEM_DRIVER As UInt32 = &H2
            Public Const SERVICE_WIN32_OWN_PROCESS As UInt32 = &H10 'Service that runs in its own process. 
            Public Const SERVICE_WIN32_SHARE_PROCESS As UInt32 = &H20

            Public Const SERVICE_DEMAND_START As UInt32 = &H3
            Public Const SERVICE_ERROR_IGNORE As UInt32 = &H0
            Public Const SERVICE_CONTROL_STOP As UInt32 = &H1
            Public Const ERROR_SERVICE_DOES_NOT_EXIST As UInt32 = 1060
            Public Const ERROR_SERVICE_ALREADY_RUNNING As UInt32 = 1056

            <StructLayout(LayoutKind.Sequential)>
            Public Structure STORAGE_DEVICE_NUMBER

                Public DeviceType As UInt32

                Public DeviceNumber As UInt32

                Public PartitionNumber As Int32

            End Structure

            <StructLayout(LayoutKind.Sequential)>
            Public Structure SCSI_ADDRESS
                Implements IEquatable(Of SCSI_ADDRESS)

                Public Length As UInt32
                Public PortNumber As Byte
                Public PathId As Byte
                Public TargetId As Byte
                Public Lun As Byte

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
                        Return CUInt(PathId) Or (CUInt(TargetId) << 8) Or (CUInt(Lun) << 16)
                    End Get
                    Set(value As UInt32)
                        PathId = CByte(value And &HFF)
                        TargetId = CByte((value >> 8) And &HFF)
                        Lun = CByte((value >> 16) And &HFF)
                    End Set
                End Property

                Public Overrides Function ToString() As String
                    Return "Port = " & PortNumber & ", Path = " & PathId & ", Target = " & TargetId & ", Lun = " & Lun
                End Function

                Public Overloads Function Equals(other As SCSI_ADDRESS) As Boolean Implements IEquatable(Of SCSI_ADDRESS).Equals
                    Return _
                        PortNumber.Equals(other.PortNumber) AndAlso
                        PathId.Equals(other.PathId) AndAlso
                        TargetId.Equals(other.TargetId) AndAlso
                        Lun.Equals(other.Lun)
                End Function

                Public Overrides Function Equals(obj As Object) As Boolean
                    If Not TypeOf obj Is SCSI_ADDRESS Then
                        Return False
                    End If

                    Return Equals(DirectCast(obj, SCSI_ADDRESS))
                End Function

                Public Overrides Function GetHashCode() As Integer
                    Return CInt(PathId) Or (CInt(TargetId) << 8) Or (CInt(Lun) << 16)
                End Function

                Public Shared Operator =(first As SCSI_ADDRESS, second As SCSI_ADDRESS) As Boolean
                    Return first.Equals(second)
                End Operator

                Public Shared Operator <>(first As SCSI_ADDRESS, second As SCSI_ADDRESS) As Boolean
                    Return Not first.Equals(second)
                End Operator

            End Structure

            <StructLayout(LayoutKind.Sequential)>
            Public Structure UNICODE_STRING
                Public Length As UInt16
                Public MaximumLength As UInt16
                Public Buffer As IntPtr

                Public Overrides Function ToString() As String
                    Try
                        Return Marshal.PtrToStringUni(Buffer, Length >> 1)

                    Catch ex As Exception
                        Return "{" & ex.Message & "}"

                    End Try
                End Function
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

                Public Cylinders As Int64
                Public MediaType As MEDIA_TYPE
                Public TracksPerCylinder As Int32
                Public SectorsPerTrack As Int32
                Public BytesPerSector As Int32
            End Structure

            <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
            Public Structure OSVERSIONINFO
                Public dwOSVersionInfoSize As Int32
                Public dwMajorVersion As Int32
                Public dwMinorVersion As Int32
                Public dwBuildNumber As Int32
                Public dwPlatformId As UInt32

                <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=128)>
                Public szCSDVersion As String
            End Structure

            <StructLayout(LayoutKind.Sequential, Pack:=8)>
            Public Structure PARTITION_INFORMATION
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

                Public StartingOffset As Int64
                Public PartitionLength As Int64
                Public HiddenSectors As UInt32
                Public PartitionNumber As UInt32
                Public PartitionType As PARTITION_TYPE
                Public BootIndicator As Byte
                Public RecognizedPartition As Byte
                Public RewritePartition As Byte

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
                        Return (PartitionType And PARTITION_TYPE.PARTITION_NTFT) = PARTITION_TYPE.PARTITION_NTFT
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
                        Return PartitionType And Not PARTITION_TYPE.PARTITION_NTFT
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
                        Return (PartitionType = PARTITION_TYPE.PARTITION_EXTENDED) OrElse (PartitionType = PARTITION_TYPE.PARTITION_XINT13_EXTENDED)
                    End Get
                End Property
            End Structure

            <StructLayout(LayoutKind.Sequential, Pack:=8)>
            Public Structure PARTITION_INFORMATION_EX
                Public Enum PARTITION_STYLE As Byte
                    PARTITION_STYLE_MBR
                    PARTITION_STYLE_GPT
                    PARTITION_STYLE_RAW
                End Enum

                <MarshalAs(UnmanagedType.I1)>
                Public PartitionStyle As PARTITION_STYLE
                Public StartingOffset As Int64
                Public PartitionLength As Int64
                Public PartitionNumber As UInt32
                <MarshalAs(UnmanagedType.I1)>
                Public RewritePartition As Boolean

                <MarshalAs(UnmanagedType.ByValArray, SizeConst:=108)>
                Private fields As Byte()

            End Structure

            <StructLayout(LayoutKind.Sequential)>
            Public Structure DISK_GROW_PARTITION
                Public PartitionNumber As Int32
                Public BytesToGrow As Int64
            End Structure

            <StructLayout(LayoutKind.Sequential)>
            Public Structure COMMTIMEOUTS
                Public ReadIntervalTimeout As UInt32
                Public ReadTotalTimeoutMultiplier As UInt32
                Public ReadTotalTimeoutConstant As UInt32
                Public WriteTotalTimeoutMultiplier As UInt32
                Public WriteTotalTimeoutConstant As UInt32
            End Structure

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
                Protected Sub New()
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
                Protected Sub New()
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

            Public Declare Auto Function FindFirstVolumeMountPoint Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpszRootPathName As String,
              <MarshalAs(UnmanagedType.LPTStr), [Out]> lpszVolumeMountPoint As StringBuilder,
              cchBufferLength As Integer) As SafeFindVolumeMountPointHandle

            Public Declare Auto Function FindNextVolumeMountPoint Lib "kernel32.dll" (
              hFindVolumeMountPoint As SafeFindVolumeMountPointHandle,
              <MarshalAs(UnmanagedType.LPTStr), [Out]> lpszVolumeMountPoint As StringBuilder,
              cchBufferLength As Integer) As Boolean

            Public Declare Auto Function FindFirstVolume Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [Out]> lpszVolumeName As StringBuilder,
              cchBufferLength As Integer) As SafeFindVolumeHandle

            Public Declare Auto Function FindNextVolume Lib "kernel32.dll" (
              hFindVolumeMountPoint As SafeFindVolumeHandle,
              <MarshalAs(UnmanagedType.LPTStr), [Out]> lpszVolumeName As StringBuilder,
              cchBufferLength As Integer) As Boolean

            Public Declare Auto Function SetVolumeMountPoint Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpszVolumeMountPoint As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpszVolumeName As String) As Boolean

            Public Declare Auto Function GetLogicalDrives Lib "kernel32.dll" (
              ) As UInteger

            <StructLayout(LayoutKind.Sequential)>
            Public Structure SERVICE_STATUS
                Public dwServiceType As Integer
                Public dwCurrentState As Integer
                Public dwControlsAccepted As Integer
                Public dwWin32ExitCode As Integer
                Public dwServiceSpecificExitCode As Integer
                Public dwCheckPoint As Integer
                Public dwWaitHint As Integer
            End Structure

            <Flags()>
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
                Protected Sub New()
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

            Public Declare Auto Function OpenSCManager Lib "advapi32.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpMachineName As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpDatabaseName As String,
              dwDesiredAccess As Integer) As SafeServiceHandle

            Public Declare Auto Function CreateService Lib "advapi32.dll" (
              hSCManager As SafeServiceHandle,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpServiceName As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpDisplayName As String,
              dwDesiredAccess As Integer,
              dwServiceType As Integer,
              dwStartType As Integer,
              dwErrorControl As Integer,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpBinaryPathName As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpLoadOrderGroup As String,
              lpdwTagId As IntPtr,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpDependencies As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lp As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpPassword As String) As SafeServiceHandle

            Public Declare Auto Function OpenService Lib "advapi32.dll" (
              hSCManager As SafeServiceHandle,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpServiceName As String,
              dwDesiredAccess As Integer) As SafeServiceHandle

            Public Declare Auto Function ControlService Lib "advapi32.dll" (
              hSCManager As SafeServiceHandle,
              dwControl As Integer,
              ByRef lpServiceStatus As SERVICE_STATUS) As Boolean

            Public Declare Auto Function DeleteService Lib "advapi32.dll" (
              hSCObject As SafeServiceHandle) As Boolean

            Public Declare Auto Function StartService Lib "advapi32.dll" (
              hService As SafeServiceHandle,
              dwNumServiceArgs As Integer,
              lpServiceArgVectors As IntPtr) As Boolean

            Public Declare Auto Function GetModuleHandle Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]> ModuleName As String) As IntPtr

            Public Declare Auto Function LoadLibrary Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpFileName As String) As IntPtr

            Public Declare Auto Function FreeLibrary Lib "kernel32.dll" (
              hModule As IntPtr) As Boolean

            Public Declare Auto Function AllocConsole Lib "kernel32.dll" (
              ) As Boolean

            Public Declare Auto Function FreeConsole Lib "kernel32.dll" (
              ) As Boolean

            Public Declare Auto Function DefineDosDevice Lib "kernel32.dll" (
              dwFlags As DEFINE_DOS_DEVICE_FLAGS,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpDeviceName As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpTargetPath As String) As Boolean

            Public Declare Auto Function QueryDosDevice Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpDeviceName As String,
              <Out, MarshalAs(UnmanagedType.LPArray)> lpTargetPath As Char(),
              ucchMax As UInt32) As UInt32

            Public Declare Auto Function GetVolumePathNamesForVolumeName Lib "kernel32.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpszVolumeName As String,
              <Out, MarshalAs(UnmanagedType.LPArray)> lpszVolumePathNames As Char(),
              cchBufferLength As UInt32,
              <Out> ByRef lpcchReturnLength As UInt32) As UInt32

            Public Declare Auto Function GetCommTimeouts Lib "kernel32" (
              hFile As SafeFileHandle,
              <Out> ByRef lpCommTimeouts As COMMTIMEOUTS) As Boolean

            Public Declare Auto Function SetCommTimeouts Lib "kernel32" (
              hFile As SafeFileHandle,
              <[In]()> ByRef lpCommTimeouts As COMMTIMEOUTS) As Boolean

            Public Declare Auto Function CreateFile Lib "kernel32" (
              <MarshalAs(UnmanagedType.LPTStr), [In]> lpFileName As String,
              dwDesiredAccess As UInt32,
              dwShareMode As UInt32,
              lpSecurityAttributes As IntPtr,
              dwCreationDisposition As UInt32,
              dwFlagsAndAttributes As UInt32,
              hTemplateFile As IntPtr) As SafeFileHandle

            Public Declare Function GetFileSize Lib "kernel32" Alias "GetFileSizeEx" (
              hFile As SafeFileHandle,
              <Out> ByRef liFileSize As Int64) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=6), Out> lpOutBuffer As Byte(),
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <MarshalAs(UnmanagedType.LPArray), [In]()> lpInBuffer As Byte(),
              nInBufferSize As UInt32,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <MarshalAs(UnmanagedType.LPArray), [In]()> lpInBuffer As Byte(),
              nInBufferSize As UInt32,
              <MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=6), Out> lpOutBuffer As Byte(),
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As Int64,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              <[In]()> ByRef lpInBuffer As Win32API.DISK_GROW_PARTITION,
              nInBufferSize As UInt32,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As Win32API.DISK_GEOMETRY,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As Win32API.PARTITION_INFORMATION,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As Win32API.PARTITION_INFORMATION_EX,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As Win32API.SCSI_ADDRESS,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As Win32API.STORAGE_DEVICE_NUMBER,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

            Public Declare Auto Function GetModuleFileName Lib "kernel32" (
              hModule As IntPtr,
              <Out, MarshalAs(UnmanagedType.LPTStr)> lpFilename As String,
              nSize As Int32) As Int32

            Public Declare Ansi Function GetProcAddress Lib "kernel32" (
              hModule As IntPtr,
              <[In](), MarshalAs(UnmanagedType.LPStr)> lpEntryName As String) As IntPtr

            Public Declare Ansi Function GetProcAddress Lib "kernel32" (
              hModule As IntPtr,
              ordinal As IntPtr) As IntPtr

            Public Declare Unicode Function RtlDosPathNameToNtPathName_U Lib "ntdll.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]()> DosName As String,
              ByRef NtName As UNICODE_STRING,
              DosFilePath As IntPtr,
              NtFilePath As IntPtr) As Boolean

            Public Declare Sub RtlFreeUnicodeString Lib "ntdll.dll" (
              ByRef UnicodeString As UNICODE_STRING)

            Public Declare Function RtlNtStatusToDosError Lib "ntdll.dll" (
              NtStatus As Int32) As Int32

            Public Declare Auto Function WritePrivateProfileString Lib "kernel32" (
              <[In](), MarshalAs(UnmanagedType.LPTStr)> SectionName As String,
              <[In](), MarshalAs(UnmanagedType.LPTStr)> SettingName As String,
              <[In](), MarshalAs(UnmanagedType.LPTStr)> Value As String,
              <[In](), MarshalAs(UnmanagedType.LPTStr)> FileName As String) As Boolean

            Public Declare Auto Sub InstallHinfSection Lib "setupapi.dll" (
              hwndOwner As IntPtr,
              hModule As IntPtr,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> lpCmdLine As String,
              nCmdShow As Int32)

            Public Declare Auto Function SetupCopyOEMInf Lib "setupapi.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]()> SourceInfFileName As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> OEMSourceMediaLocation As String,
              OEMSourceMediaType As UInt32,
              CopyStyle As UInt32,
              <MarshalAs(UnmanagedType.LPTStr), [In](), Out> DestinationInfFileName As StringBuilder,
              DestinationInfFileNameSize As Int32,
              ByRef RequiredSize As UInt32,
              DestinationInfFileNameComponent As IntPtr) As Boolean

            Public Const DRIVER_PACKAGE_DELETE_FILES = &H20UI
            Public Const DRIVER_PACKAGE_FORCE = &H4UI
            Public Const DRIVER_PACKAGE_SILENT = &H2UI

            Public Declare Auto Function DriverPackagePreinstall Lib "difxapi.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]()> SourceInfFileName As String,
              Options As UInt32) As Integer

            Public Declare Auto Function DriverPackageInstall Lib "difxapi.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]()> SourceInfFileName As String,
              Options As UInt32,
              pInstallerInfo As IntPtr,
              ByRef pNeedReboot As Boolean) As Integer

            Public Declare Auto Function DriverPackageUninstall Lib "difxapi.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]()> SourceInfFileName As String,
              Options As UInt32,
              pInstallerInfo As IntPtr,
              ByRef pNeedReboot As Boolean) As Integer

            Public Declare Auto Function CM_Locate_DevNode Lib "setupapi.dll" (
              ByRef devInst As UInt32,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> rootid As String,
              Flags As UInt32) As UInt32

            Public Declare Function CM_Reenumerate_DevNode Lib "setupapi.dll" (
              devInst As UInt32,
              Flags As UInt32) As UInt32

            Public Const CM_GETIDLIST_FILTER_SERVICE As UInt32 = &H2UI

            Public Declare Auto Function CM_Get_Device_ID_List_Size Lib "setupapi.dll" (
              ByRef Length As UInt32,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> filter As String,
              Flags As UInt32) As UInt32

            Public Declare Auto Function CM_Get_Device_ID_List Lib "setupapi.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]()> filter As String,
              <Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=2)> Buffer As Char(),
              BufferLength As UInt32,
              Flags As UInt32) As UInt32

            Public Declare Auto Function SetupSetNonInteractiveMode Lib "setupapi.dll" (
              state As Boolean) As Boolean

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
                Protected Sub New()
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
                Protected Sub New()
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

            Public Declare Auto Function SetupOpenInfFile Lib "setupapi.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]()> FileName As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> InfClass As String,
              InfStyle As UInt32,
              ByRef ErrorLine As UInt32) As SafeInfHandle

            Public Delegate Function SetupFileCallback(Context As IntPtr,
                                                       Notification As UInt32,
                                                       Param1 As UIntPtr,
                                                       Param2 As UIntPtr) As UInt32

            Public Declare Auto Function SetupInstallFromInfSection Lib "setupapi.dll" (
              hWnd As IntPtr,
              InfHandle As SafeInfHandle,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> SectionName As String,
              Flags As UInt32,
              RelativeKeyRoot As IntPtr,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> SourceRootPath As String,
              CopyFlags As UInt32,
              MsgHandler As SetupFileCallback,
              Context As IntPtr,
              DeviceInfoSet As IntPtr,
              DeviceInfoData As IntPtr) As Boolean

            Public Declare Auto Function SetupInstallFromInfSection Lib "setupapi.dll" (
              hWnd As IntPtr,
              InfHandle As SafeInfHandle,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> SectionName As String,
              Flags As UInt32,
              RelativeKeyRoot As SafeRegistryHandle,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> SourceRootPath As String,
              CopyFlags As UInt32,
              MsgHandler As SetupFileCallback,
              Context As IntPtr,
              DeviceInfoSet As IntPtr,
              DeviceInfoData As IntPtr) As Boolean

            Public Const DIGCF_PRESENT As UInt32 = &H2
            Public Const DIGCF_DEVICEINTERFACE As UInt32 = &H10

            Public Declare Auto Function SetupDiGetINFClass Lib "setupapi.dll" (
              <MarshalAs(UnmanagedType.LPTStr), [In]()> InfPath As String,
              <Out> ByRef ClassGuid As Guid,
              <Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex:=3)> ClassName As Char(),
              ClassNameSize As UInt32,
              <Out> ByRef RequiredSize As UInt32) As Boolean

            Public Declare Auto Function SetupDiOpenDeviceInfo Lib "setupapi.dll" (
              DevInfoSet As SafeDeviceInfoSetHandle,
              <[In](), MarshalAs(UnmanagedType.LPTStr)> Enumerator As String,
              hWndParent As IntPtr,
              Flags As UInt32,
              DeviceInfoData As IntPtr) As Boolean

            Public Declare Auto Function SetupDiOpenDeviceInfo Lib "setupapi.dll" (
              DevInfoSet As SafeDeviceInfoSetHandle,
              Enumerator As Byte(),
              hWndParent As IntPtr,
              Flags As UInt32,
              DeviceInfoData As IntPtr) As Boolean

            Public Declare Auto Function SetupDiGetClassDevs Lib "setupapi.dll" (
              <[In]()> ByRef ClassGuid As Guid,
              <[In](), MarshalAs(UnmanagedType.LPTStr)> Enumerator As String,
              hWndParent As IntPtr,
              Flags As UInt32) As SafeDeviceInfoSetHandle

            Public Declare Auto Function SetupDiGetClassDevs Lib "setupapi.dll" (
              ClassGuid As IntPtr,
              <[In](), MarshalAs(UnmanagedType.LPTStr)> Enumerator As String,
              hWndParent As IntPtr,
              Flags As UInt32) As SafeDeviceInfoSetHandle

            <StructLayout(LayoutKind.Sequential)>
            Public Structure SP_DEVICE_INTERFACE_DATA
                Public cbSize As UInt32
                Public InterfaceClassGuid As Guid
                Public Flags As UInt32
                Public Reserved As UIntPtr

                Public Sub Initialize()
                    cbSize = CUInt(Marshal.SizeOf(Me))
                End Sub
            End Structure

            <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
            Public Structure SP_DEVICE_INTERFACE_DETAIL_DATA
                Public cbSize As UInt32
                <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=32768)>
                Public DevicePath As String

                Public Sub Initialize()
                    cbSize = CUInt(Marshal.SizeOf(Me))
                End Sub
            End Structure

            <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
            Public Structure SP_DEVINFO_LIST_DETAIL_DATA

                Public Const SP_MAX_MACHINENAME_LENGTH = 263

                Public cbSize As UInt32

                Public ClassGUID As Guid

                Public RemoteMachineHandle As IntPtr

                <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=SP_MAX_MACHINENAME_LENGTH)>
                Public RemoteMachineName As String

                Public Sub Initialize()
                    cbSize = CUInt(Marshal.SizeOf(Me))
                End Sub
            End Structure

            Public Declare Auto Function SetupDiEnumDeviceInfo Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              MemberIndex As UInt32,
              <[Out]()> ByRef DeviceInterfaceData As SP_DEVINFO_DATA) As Boolean

            Public Declare Auto Function SetupDiEnumDeviceInterfaces Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              DeviceInfoData As IntPtr,
              <[In]()> ByRef ClassGuid As Guid,
              MemberIndex As UInt32,
              <[Out]()> ByRef DeviceInterfaceData As SP_DEVICE_INTERFACE_DATA) As Boolean

            Public Declare Auto Function SetupDiEnumDeviceInterfaces Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              DeviceInfoData As IntPtr,
              ClassGuid As IntPtr,
              MemberIndex As UInt32,
              <[Out]()> ByRef DeviceInterfaceData As SP_DEVICE_INTERFACE_DATA) As Boolean

            Public Declare Auto Function SetupDiCreateDeviceInfoListEx Lib "setupapi.dll" (
              <[In]()> ByRef ClassGuid As Guid,
              hwndParent As IntPtr,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> MachineName As String,
              Reserved As IntPtr) As SafeDeviceInfoSetHandle

            Public Declare Auto Function SetupDiCreateDeviceInfoListEx Lib "setupapi.dll" (
              ClassGuid As IntPtr,
              hwndParent As IntPtr,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> MachineName As String,
              Reserved As IntPtr) As SafeDeviceInfoSetHandle

            Public Declare Auto Function SetupDiCreateDeviceInfoList Lib "setupapi.dll" (
              <[In]()> ByRef ClassGuid As Guid,
              hwndParent As IntPtr) As SafeDeviceInfoSetHandle

            Public Declare Auto Function SetupDiCreateDeviceInfoList Lib "setupapi.dll" (
              ClassGuid As IntPtr,
              hwndParent As IntPtr) As SafeDeviceInfoSetHandle

            Public Declare Auto Function SetupDiGetDeviceInterfaceDetail Lib "setupapi.dll" (
              DeviceInfoSet As SafeDeviceInfoSetHandle,
              <[In]()> ByRef DeviceInterfaceData As SP_DEVICE_INTERFACE_DATA,
              <[Out](), MarshalAs(UnmanagedType.LPStruct, SizeParamIndex:=3)> ByRef DeviceInterfaceDetailData As SP_DEVICE_INTERFACE_DETAIL_DATA,
              DeviceInterfaceDetailDataSize As UInt32,
              <Out> ByRef RequiredSize As UInt32,
              DeviceInfoData As IntPtr) As Boolean

            Public Declare Auto Function SetupDiGetDeviceInfoListDetail Lib "setupapi.dll" (
              devinfo As SafeDeviceInfoSetHandle,
              <Out> ByRef DeviceInfoDetailData As SP_DEVINFO_LIST_DETAIL_DATA) As Boolean

            Public Declare Auto Function SetupDiCreateDeviceInfo Lib "setupapi.dll" (
              hDevInfo As SafeDeviceInfoSetHandle,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> DeviceName As String,
              <[In]()> ByRef ClassGuid As Guid,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> DeviceDescription As String,
              owner As IntPtr,
              CreationFlags As UInt32,
              <Out> ByRef DeviceInfoData As SP_DEVINFO_DATA) As Boolean

            <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
            Public Structure SP_DEVINFO_DATA
                Public cbSize As UInt32
                Public ClassGuid As Guid
                Public DevInst As UInt32
                Public Reserved As UIntPtr

                Public Sub Initialize()
                    cbSize = CUInt(Marshal.SizeOf(Me))
                End Sub
            End Structure

            Public Declare Auto Function SetupDiSetDeviceRegistryProperty Lib "setupapi.dll" (
              hDevInfo As SafeDeviceInfoSetHandle,
              ByRef DeviceInfoData As SP_DEVINFO_DATA,
              [Property] As UInt32,
              <[In](), MarshalAs(UnmanagedType.LPArray)> PropertyBuffer As Byte(),
              PropertyBufferSize As UInt32) As Boolean

            Public Declare Auto Function SetupDiCallClassInstaller Lib "setupapi.dll" (
              InstallFunction As UInt32,
              hDevInfo As SafeDeviceInfoSetHandle,
              <[In]()> ByRef DeviceInfoData As SP_DEVINFO_DATA) As Boolean

            Public Declare Auto Function UpdateDriverForPlugAndPlayDevices Lib "newdev.dll" (
              owner As IntPtr,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> HardwareId As String,
              <MarshalAs(UnmanagedType.LPTStr), [In]()> InfPath As String,
              InstallFlags As UInt32,
              RebootRequired As IntPtr) As Boolean

            Public Declare Auto Function GetConsoleWindow Lib "kernel32.dll" () As IntPtr

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
            Public Enum ShutdownReason As UInt32
                ReasonFlagPlanned = &H80000000UI
            End Enum

            Public Declare Auto Function ExitWindowsEx Lib "kernel32.dll" (
              flags As ShutdownFlags,
              reason As ShutdownReason) As Boolean

            Public Declare Unicode Function RtlGetVersion Lib "ntdll.dll" (
              <[In], Out> ByRef os_version As OSVERSIONINFO) As Integer

        End Class
#End Region

#Region "Miniport Control"

        ''' <summary>
        ''' Control methods for direct communication with SCSI miniport.
        ''' </summary>
        Public Class PhDiskMntCtl

            Public Const SMP_IMSCSI = &H83730000UI
            Public Const SMP_IMSCSI_QUERY_VERSION = SMP_IMSCSI Or &H800UI
            Public Const SMP_IMSCSI_CREATE_DEVICE = SMP_IMSCSI Or &H801UI
            Public Const SMP_IMSCSI_QUERY_DEVICE = SMP_IMSCSI Or &H802UI
            Public Const SMP_IMSCSI_QUERY_ADAPTER = SMP_IMSCSI Or &H803UI
            Public Const SMP_IMSCSI_CHECK = SMP_IMSCSI Or &H804UI
            Public Const SMP_IMSCSI_SET_DEVICE_FLAGS = SMP_IMSCSI Or &H805UI
            Public Const SMP_IMSCSI_REMOVE_DEVICE = SMP_IMSCSI Or &H806UI

            ''' <summary>
            ''' Signature to set in SRB_IO_CONTROL header. This identifies that sender and receiver of
            ''' IOCTL_SCSI_MINIPORT requests talk to intended components only.
            ''' </summary>
            Public Shared ReadOnly SrbIoCtlSignature As Byte() = Encoding.ASCII.GetBytes("PhDskMnt".PadRight(8, New Char))

            ''' <summary>
            ''' SRB_IO_CONTROL header, as defined in NTDDDISK.
            ''' </summary>
            <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Ansi)>
            <ComVisible(False)>
            Public Structure SRB_IO_CONTROL
                Public HeaderLength As UInt32
                <MarshalAs(UnmanagedType.ByValArray, SizeConst:=8)>
                Public Signature As Byte()
                Public Timeout As UInt32
                Public ControlCode As UInt32
                Public ReturnCode As UInt32
                Public Length As UInt32

            End Structure

            ''' <summary>
            ''' Sends an IOCTL_SCSI_MINIPORT control request to a SCSI miniport.
            ''' </summary>
            ''' <param name="adapter">Open handle to SCSI adapter.</param>
            ''' <param name="ctrlcode">Control code to set in SRB_IO_CONTROL header.</param>
            ''' <param name="timeout">Timeout to set in SRB_IO_CONTROL header.</param>
            ''' <param name="filldata">Optional function to fill request data after SRB_IO_CONTROL header. The Length field in
            ''' SRB_IO_CONTROL header will be automatically adjusted to reflect the amount of data passed by this function.</param>
            ''' <param name="returncode">ReturnCode value from SRB_IO_CONTROL header upon return.</param>
            ''' <returns>This method returns a BinaryReader object that can be used to read and parse data returned after the
            ''' SRB_IO_CONTROL header.</returns>
            Public Shared Function SendSrbIoControl(adapter As SafeFileHandle,
                                                    ctrlcode As UInt32,
                                                    timeout As UInt32,
                                                    filldata As Action(Of BinaryWriter),
                                                    ByRef returncode As Int32) As BinaryReader

                Dim indata =
                  Sub(Request As BinaryWriter)
                      Request.Write(Marshal.SizeOf(GetType(NativeFileIO.PhDiskMntCtl.SRB_IO_CONTROL)))
                      Request.Write(NativeFileIO.PhDiskMntCtl.SrbIoCtlSignature)
                      Request.Write(timeout)
                      Request.Write(ctrlcode)
                      Request.Write(0UI)
                      If filldata Is Nothing Then
                          Request.Write(0UI)
                      Else
                          Dim data As New BinaryWriter(New MemoryStream, Encoding.Unicode)
                          filldata(data)
                          Dim databytes = DirectCast(data.BaseStream, MemoryStream).ToArray()
                          Request.Write(databytes.Length)
                          Request.Write(databytes)
                      End If
                  End Sub

                Dim Response =
                  NativeFileIO.DeviceIoControl(adapter,
                                               NativeFileIO.Win32API.IOCTL_SCSI_MINIPORT,
                                               indata,
                                               0)

                Response.ReadUInt32()
                Response.ReadBytes(8)
                Response.ReadUInt32()
                Response.ReadUInt32()
                returncode = Response.ReadInt32()
                Dim ResponseLength = Response.ReadInt32()

                Return New BinaryReader(New MemoryStream(Response.ReadBytes(ResponseLength)))

            End Function

        End Class

#End Region

        Public Shared ReadOnly SystemArchitecture As String
        Public Shared ReadOnly ProcessArchitecture As String

        Shared Sub New()

            SystemArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432")
            If SystemArchitecture Is Nothing Then
                SystemArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
            End If
            If SystemArchitecture Is Nothing Then
                SystemArchitecture = "x86"
            End If
            SystemArchitecture = SystemArchitecture.ToLowerInvariant()

            Trace.WriteLine("System architecture is: " & SystemArchitecture)

            ProcessArchitecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")
            If ProcessArchitecture Is Nothing Then
                ProcessArchitecture = "x86"
            End If
            ProcessArchitecture = ProcessArchitecture.ToLowerInvariant()

            Trace.WriteLine("Process architecture is: " & ProcessArchitecture)

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
        <DebuggerHidden()>
        Public Shared Function Win32Try(Of T)(result As T) As T

            If result Is Nothing Then
                Throw New Win32Exception
            End If
            Return result

        End Function

        ''' <summary>
        ''' Sends an IOCTL control request to a device driver, or an FSCTL control request to a filesystem driver.
        ''' </summary>
        ''' <param name="device">Open handle to filer or device.</param>
        ''' <param name="ctrlcode">IOCTL or FSCTL control code.</param>
        ''' <param name="indata">Optional function to create input data for the control function.</param>
        ''' <param name="outdatasize">Number of bytes returned in output buffer by driver.</param>
        ''' <returns>This method returns a BinaryReader object that can be used to read and parse data returned by
        ''' driver in the output buffer.</returns>
        Public Shared Function DeviceIoControl(device As SafeFileHandle,
                                               ctrlcode As UInt32,
                                               indata As Action(Of BinaryWriter),
                                               ByRef outdatasize As UInt32) As BinaryReader

            Dim bytes As Byte() = Nothing

            If indata IsNot Nothing Then
                Using Request As New BinaryWriter(New MemoryStream, Encoding.Unicode)
                    indata(Request)
                    bytes = DirectCast(Request.BaseStream, MemoryStream).ToArray()
                End Using
            End If

            Dim indatasize = If(bytes Is Nothing, 0UI, CUInt(bytes.Length))

            If outdatasize > indatasize Then
                Array.Resize(bytes, CInt(outdatasize))
            End If

            Dim rc =
              NativeFileIO.Win32API.DeviceIoControl(device,
                                                    ctrlcode,
                                                    bytes,
                                                    indatasize,
                                                    bytes,
                                                    If(bytes Is Nothing, 0UI, CUInt(bytes.Length)),
                                                    outdatasize,
                                                    IntPtr.Zero)

            If Not rc Then
                Throw New Win32Exception
            End If

            Array.Resize(bytes, CInt(outdatasize))

            Return New BinaryReader(New MemoryStream(bytes), Encoding.Unicode)

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
                Throw New ArgumentNullException("FileName")
            End If

            Dim NativeDesiredAccess As UInt32 = Win32API.FILE_READ_ATTRIBUTES Or Win32API.SYNCHRONIZE
            If (DesiredAccess And FileAccess.Read) = FileAccess.Read Then
                NativeDesiredAccess = NativeDesiredAccess Or Win32API.GENERIC_READ
            End If
            If (DesiredAccess And FileAccess.Write) = FileAccess.Write Then
                NativeDesiredAccess = NativeDesiredAccess Or Win32API.GENERIC_WRITE
            End If

            Dim NativeShareMode As UInt32 = 0
            If (ShareMode And FileShare.Read) = FileShare.Read Then
                NativeShareMode = NativeShareMode Or Win32API.FILE_SHARE_READ
            End If
            If (ShareMode And FileShare.Write) = FileShare.Write Then
                NativeShareMode = NativeShareMode Or Win32API.FILE_SHARE_WRITE
            End If
            If (ShareMode And FileShare.Delete) = FileShare.Delete Then
                NativeShareMode = NativeShareMode Or Win32API.FILE_SHARE_DELETE
            End If

            Dim NativeCreationDisposition As UInt32 = 0
            Select Case CreationDisposition
                Case FileMode.Create
                    NativeCreationDisposition = Win32API.CREATE_ALWAYS
                Case FileMode.CreateNew
                    NativeCreationDisposition = Win32API.CREATE_NEW
                Case FileMode.Open
                    NativeCreationDisposition = Win32API.OPEN_EXISTING
                Case FileMode.OpenOrCreate
                    NativeCreationDisposition = Win32API.OPEN_ALWAYS
                Case FileMode.Truncate
                    NativeCreationDisposition = Win32API.TRUNCATE_EXISTING
                Case Else
                    Throw New NotImplementedException
            End Select

            Dim NativeFlagsAndAttributes As UInt32 = Win32API.FILE_ATTRIBUTE_NORMAL
            If Overlapped Then
                NativeFlagsAndAttributes += Win32API.FILE_FLAG_OVERLAPPED
            End If

            Dim Handle = Win32API.CreateFile(FileName,
                                             NativeDesiredAccess,
                                             NativeShareMode,
                                             IntPtr.Zero,
                                             NativeCreationDisposition,
                                             NativeFlagsAndAttributes,
                                             IntPtr.Zero)
            If Handle.IsInvalid Then
                Throw New Win32Exception
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
        Public Shared Function OpenBackupHandle(
          FilePath As String,
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          CreationDisposition As FileMode) As SafeFileHandle

            If String.IsNullOrEmpty(FilePath) Then
                Throw New ArgumentNullException("FilePath")
            End If

            Dim NativeDesiredAccess As UInt32 = Win32API.FILE_READ_ATTRIBUTES
            If (DesiredAccess And FileAccess.Read) = FileAccess.Read Then
                NativeDesiredAccess = NativeDesiredAccess Or Win32API.GENERIC_READ
            End If
            If (DesiredAccess And FileAccess.Write) = FileAccess.Write Then
                NativeDesiredAccess = NativeDesiredAccess Or Win32API.GENERIC_WRITE
            End If

            Dim NativeShareMode As UInt32 = 0
            If (ShareMode And FileShare.Read) = FileShare.Read Then
                NativeShareMode = NativeShareMode Or Win32API.FILE_SHARE_READ
            End If
            If (ShareMode And FileShare.Write) = FileShare.Write Then
                NativeShareMode = NativeShareMode Or Win32API.FILE_SHARE_WRITE
            End If
            If (ShareMode And FileShare.Delete) = FileShare.Delete Then
                NativeShareMode = NativeShareMode Or Win32API.FILE_SHARE_DELETE
            End If

            Dim NativeCreationDisposition As UInt32 = 0
            Select Case CreationDisposition
                Case FileMode.Create
                    NativeCreationDisposition = Win32API.CREATE_ALWAYS
                Case FileMode.CreateNew
                    NativeCreationDisposition = Win32API.CREATE_NEW
                Case FileMode.Open
                    NativeCreationDisposition = Win32API.OPEN_EXISTING
                Case FileMode.OpenOrCreate
                    NativeCreationDisposition = Win32API.OPEN_ALWAYS
                Case FileMode.Truncate
                    NativeCreationDisposition = Win32API.TRUNCATE_EXISTING
                Case Else
                    Throw New NotImplementedException
            End Select

            Dim NativeFlagsAndAttributes As UInt32 = Win32API.FILE_FLAG_BACKUP_SEMANTICS

            Dim Handle = Win32API.CreateFile(FilePath,
                                             NativeDesiredAccess,
                                             NativeShareMode,
                                             IntPtr.Zero,
                                             NativeCreationDisposition,
                                             NativeFlagsAndAttributes,
                                             IntPtr.Zero)
            If Handle.IsInvalid Then
                Throw New Win32Exception
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
        Public Shared Function OpenFileStream(
          FileName As String,
          CreationDisposition As FileMode,
          DesiredAccess As FileAccess,
          ShareMode As FileShare) As FileStream

            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped:=False), GetFileStreamLegalAccessValue(DesiredAccess))

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

            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped:=False), GetFileStreamLegalAccessValue(DesiredAccess), BufferSize)

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
        Public Shared Function OpenFileStream(
          FileName As String,
          CreationDisposition As FileMode,
          DesiredAccess As FileAccess,
          ShareMode As FileShare,
          Overlapped As Boolean) As FileStream

            Return New FileStream(OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped), GetFileStreamLegalAccessValue(DesiredAccess), 1, Overlapped)

        End Function

        Private Shared Sub SetFileCompressionState(SafeFileHandle As SafeFileHandle, State As UShort)

            Dim pinptr = GCHandle.Alloc(State, GCHandleType.Pinned)
            Try
                Win32Try(Win32API.DeviceIoControl(SafeFileHandle, _
                                                  Win32API.FSCTL_SET_COMPRESSION, _
                                                  pinptr.AddrOfPinnedObject(), _
                                                  2UI, _
                                                  IntPtr.Zero, _
                                                  0UI, _
                                                  Nothing, _
                                                  IntPtr.Zero))

            Finally
                pinptr.Free()

            End Try

        End Sub

        Public Shared Function GetFileSize(SafeFileHandle As SafeFileHandle) As Int64

            Dim FileSize As Int64

            Win32Try(Win32API.GetFileSize(SafeFileHandle, FileSize))

            Return FileSize

        End Function

        Public Shared Function GetDiskSize(SafeFileHandle As SafeFileHandle) As Int64

            Dim FileSize As Int64

            Win32Try(Win32API.DeviceIoControl(SafeFileHandle, Win32API.IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0UI, FileSize, CUInt(Marshal.SizeOf(FileSize)), 0UI, IntPtr.Zero))

            Return FileSize

        End Function

        Public Shared Function IsDiskWritable(SafeFileHandle As SafeFileHandle) As Boolean

            Dim rc = Win32API.DeviceIoControl(SafeFileHandle, Win32API.IOCTL_DISK_IS_WRITABLE, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, 0UI, IntPtr.Zero)
            If rc Then
                Return True
            Else
                Dim err = Marshal.GetLastWin32Error()
                If err = Win32API.ERROR_WRITE_PROTECT Then
                    Return False
                Else
                    Throw New Win32Exception(err)
                End If
            End If

        End Function

        Public Shared Sub UpdateDiskProperties(SafeFileHandle As SafeFileHandle)

            Win32Try(Win32API.DeviceIoControl(SafeFileHandle, Win32API.IOCTL_DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, 0UI, IntPtr.Zero))

        End Sub

        Public Shared Sub GrowPartition(DiskHandle As SafeFileHandle, PartitionNumber As Integer, BytesToGrow As Int64)

            Dim DiskGrowPartition As Win32API.DISK_GROW_PARTITION
            DiskGrowPartition.PartitionNumber = PartitionNumber
            DiskGrowPartition.BytesToGrow = BytesToGrow
            Win32Try(Win32API.DeviceIoControl(DiskHandle, Win32API.IOCTL_DISK_GROW_PARTITION, DiskGrowPartition, CUInt(Marshal.SizeOf(DiskGrowPartition.GetType())), IntPtr.Zero, 0UI, 0UI, IntPtr.Zero))

        End Sub

        Public Shared Sub CompressFile(SafeFileHandle As SafeFileHandle)

            SetFileCompressionState(SafeFileHandle, Win32API.COMPRESSION_FORMAT_DEFAULT)

        End Sub

        Public Shared Sub UncompressFile(SafeFileHandle As SafeFileHandle)

            SetFileCompressionState(SafeFileHandle, Win32API.COMPRESSION_FORMAT_NONE)

        End Sub

        Public Shared Sub AllowExtendedDASDIO(SafeFileHandle As SafeFileHandle)

            Win32Try(Win32API.DeviceIoControl(SafeFileHandle, Win32API.FSCTL_ALLOW_EXTENDED_DASD_IO, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, 0UI, IntPtr.Zero))

        End Sub

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
        ''' <param name="hDevice">Handle to device to lock and dismount.</param>
        ''' <param name="bForce">Indicates if True that volume should be immediately dismounted even if it
        ''' cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
        ''' successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
        Public Shared Function DismountVolumeFilesystem(hDevice As SafeFileHandle, bForce As Boolean) As Boolean

            If Not Win32API.DeviceIoControl(hDevice, Win32API.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, Nothing, Nothing) Then
                If Not bForce Then
                    Return False
                End If
            End If

            If Not Win32API.DeviceIoControl(hDevice, Win32API.FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, Nothing, Nothing) Then
                Return False
            End If

            Return Win32API.DeviceIoControl(hDevice, Win32API.FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, Nothing, Nothing)

        End Function

        ''' <summary>
        ''' Retrieves disk geometry.
        ''' </summary>
        ''' <param name="hDevice">Handle to device.</param>
        Public Shared Function GetDiskGeometry(hDevice As SafeFileHandle) As Win32API.DISK_GEOMETRY

            Dim DiskGeometry As Win32API.DISK_GEOMETRY

            Win32Try(Win32API.DeviceIoControl(hDevice, Win32API.IOCTL_DISK_GET_DRIVE_GEOMETRY, IntPtr.Zero, 0, DiskGeometry, CUInt(Marshal.SizeOf(GetType(Win32API.DISK_GEOMETRY))), Nothing, Nothing))

            Return DiskGeometry

        End Function

        ''' <summary>
        ''' Retrieves SCSI address.
        ''' </summary>
        ''' <param name="hDevice">Handle to device.</param>
        Public Shared Function GetScsiAddress(hDevice As SafeFileHandle) As Win32API.SCSI_ADDRESS?

            Dim ScsiAddress As Win32API.SCSI_ADDRESS

            If Win32API.DeviceIoControl(hDevice, Win32API.IOCTL_SCSI_GET_ADDRESS, IntPtr.Zero, 0, ScsiAddress, CUInt(Marshal.SizeOf(GetType(Win32API.SCSI_ADDRESS))), Nothing, Nothing) Then
                Return ScsiAddress
            Else
                Return Nothing
            End If

        End Function

        ''' <summary>
        ''' Retrieves storage device number.
        ''' </summary>
        ''' <param name="hDevice">Handle to device.</param>
        Public Shared Function GetStorageDeviceNumber(hDevice As SafeFileHandle) As Win32API.STORAGE_DEVICE_NUMBER?

            Dim StorageDeviceNumber As Win32API.STORAGE_DEVICE_NUMBER

            If Win32API.DeviceIoControl(hDevice, Win32API.IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, StorageDeviceNumber, CUInt(Marshal.SizeOf(GetType(Win32API.STORAGE_DEVICE_NUMBER))), Nothing, Nothing) Then
                Return StorageDeviceNumber
            Else
                Return Nothing
            End If

        End Function

        Public Shared Function GetProcAddress(hModule As IntPtr, procedureName As String, delegateType As Type) As [Delegate]

            Return Marshal.GetDelegateForFunctionPointer(Win32Try(Win32API.GetProcAddress(hModule, procedureName)), delegateType)

        End Function

        Public Shared Function GetProcAddress(moduleName As String, procedureName As String, delegateType As Type) As [Delegate]

            Dim hModule = Win32Try(Win32API.LoadLibrary(moduleName))
            Return Marshal.GetDelegateForFunctionPointer(Win32Try(Win32API.GetProcAddress(hModule, procedureName)), delegateType)

        End Function

        Public Shared Function QueryDosDevice() As String()

            Return QueryDosDevice(Nothing)

        End Function

        Public Shared Function QueryDosDevice(DosDevice As String) As String()

            Dim TargetPath(0 To 65536) As Char

            Dim length = Win32API.QueryDosDevice(DosDevice, TargetPath, CUInt(TargetPath.Length))

            If length < 2 Then
                Return Nothing
            End If

            Dim Target As New String(TargetPath, 0, CInt(length - 2))

            Return Target.Split({New Char}, StringSplitOptions.RemoveEmptyEntries)

        End Function

        Public Shared Function GetNtPath(Win32Path As String) As String

            Dim UnicodeString As Win32API.UNICODE_STRING

            Dim RC = Win32API.RtlDosPathNameToNtPathName_U(Win32Path, UnicodeString, Nothing, Nothing)
            If Not RC Then
                Throw New IOException("Invalid path: '" & Win32Path & "'")
            End If

            Try
                Return Marshal.PtrToStringUni(UnicodeString.Buffer, UnicodeString.Length \ 2)

            Finally
                Win32API.RtlFreeUnicodeString(UnicodeString)

            End Try

        End Function

        Public Shared Sub SetVolumeMountPoint(VolumeMountPoint As String, VolumeName As String)
            Win32Try(Win32API.SetVolumeMountPoint(VolumeMountPoint, VolumeName))
        End Sub

        Public Shared Function FindFirstFreeDriveLetter() As Char
            Return FindFirstFreeDriveLetter("D"c)
        End Function

        Public Shared Function FindFirstFreeDriveLetter(start As Char) As Char
            start = Char.ToUpperInvariant(start)
            If start < "A"c OrElse start > "Z"c Then
                Throw New ArgumentOutOfRangeException("start")
            End If

            Dim logical_drives = Win32API.GetLogicalDrives()

            For search = Convert.ToUInt16(start) To Convert.ToUInt16("Z"c)
                If (logical_drives And (1 << (search - Convert.ToUInt16("A"c)))) = 0 Then
                    Return Convert.ToChar(search)
                End If
            Next

            Return Nothing

        End Function

        Structure DiskExtent
            Public DiskNumber As UInteger
            Public StartingOffset As Long
            Public ExtentLength As Long
        End Structure

        Public Shared Function GetVolumeDiskExtents(volume As SafeFileHandle) As DiskExtent()

            ' 776 is enough to hold 32 disk extent items
            Dim reader = DeviceIoControl(volume, Win32API.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, Nothing, 776)
            Dim number = reader.ReadInt32()
            reader.ReadInt32()
            Dim array(0 To number - 1) As DiskExtent
            For i = 0 To number - 1
                array(i).DiskNumber = reader.ReadUInt32()
                reader.ReadInt32()
                array(i).StartingOffset = reader.ReadInt64()
                array(i).ExtentLength = reader.ReadInt64()
            Next

            Return array

        End Function

        Public Shared Function GetPartitionInformation(disk As SafeFileHandle) As Win32API.PARTITION_INFORMATION?

            Dim partition_info As Win32API.PARTITION_INFORMATION = Nothing

            If Win32API.DeviceIoControl(disk, Win32API.IOCTL_DISK_GET_PARTITION_INFO_EX,
                                              IntPtr.Zero, 0, partition_info, CUInt(Marshal.SizeOf(partition_info)),
                                              0, IntPtr.Zero) Then
                Return partition_info
            Else
                Return Nothing
            End If

        End Function

        Public Shared Function GetPartitionInformationEx(disk As SafeFileHandle) As Win32API.PARTITION_INFORMATION_EX?

            Dim partition_info As Win32API.PARTITION_INFORMATION_EX = Nothing

            If Win32API.DeviceIoControl(disk, Win32API.IOCTL_DISK_GET_PARTITION_INFO_EX,
                                              IntPtr.Zero, 0, partition_info, CUInt(Marshal.SizeOf(partition_info)),
                                              0, IntPtr.Zero) Then
                Return partition_info
            Else
                Return Nothing
            End If

        End Function

        Public Shared Function GetDiskOffline(disk As SafeFileHandle) As Boolean?

            Dim attribs_size As Byte = 16
            Dim attribs(0 To attribs_size - 1) As Byte

            If Win32API.DeviceIoControl(disk, Win32API.IOCTL_DISK_GET_DISK_ATTRIBUTES,
                                              IntPtr.Zero, 0, attribs, attribs_size,
                                              0, IntPtr.Zero) Then
                Return (attribs(8) And 1) <> 0
            Else
                Return Nothing
            End If

        End Function

        Public Shared Sub SetDiskOffline(disk As SafeFileHandle, offline As Boolean)

            Dim attribs_size As Byte = 40
            Dim attribs(0 To attribs_size - 1) As Byte
            attribs(0) = attribs_size
            attribs(16) = 1
            If offline Then
                attribs(8) = 1
            End If

            Win32Try(Win32API.DeviceIoControl(disk, Win32API.IOCTL_DISK_SET_DISK_ATTRIBUTES,
                                              attribs, attribs_size, IntPtr.Zero, 0,
                                              0, IntPtr.Zero))

        End Sub

        Public Shared Function GetDiskReadOnly(disk As SafeFileHandle) As Boolean?

            Dim attribs_size As Byte = 16
            Dim attribs(0 To attribs_size - 1) As Byte

            If Win32API.DeviceIoControl(disk, Win32API.IOCTL_DISK_GET_DISK_ATTRIBUTES,
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

            Win32Try(Win32API.DeviceIoControl(disk, Win32API.IOCTL_DISK_SET_DISK_ATTRIBUTES,
                                              attribs, attribs_size, IntPtr.Zero, 0,
                                              0, IntPtr.Zero))

        End Sub

        Public Shared Sub SetVolumeOffline(disk As SafeFileHandle, offline As Boolean)

            Win32Try(Win32API.DeviceIoControl(disk, If(offline,
                                                       Win32API.IOCTL_VOLUME_OFFLINE,
                                                       Win32API.IOCTL_VOLUME_ONLINE),
                                              IntPtr.Zero, 0, IntPtr.Zero, 0,
                                              0, IntPtr.Zero))

        End Sub

        Public Shared Function GetExceptionForNtStatus(NtStatus As Int32) As Exception

            Return New Win32Exception(Win32API.RtlNtStatusToDosError(NtStatus))

        End Function

        Public Shared Function GetModuleFullPath(hModule As IntPtr) As String

            Dim str As New String(Nothing, 32768)

            Dim PathLength = Win32API.GetModuleFileName(hModule, str, str.Length)
            If PathLength = 0 Then
                Throw New Win32Exception
            End If

            Return str.Substring(0, PathLength)

        End Function

        Public Shared Function GetDiskVolumesMountPoints(DiskNumber As UInteger) As IEnumerable(Of String)

            Return GetDiskVolumes(DiskNumber).SelectMany(AddressOf GetVolumeMountPoints)

        End Function

        Public Shared Function GetVolumeMountPoints(VolumeName As String) As String()

            Dim TargetPath(0 To 65536) As Char

            Dim length As UInt32

            Win32Try(Win32API.GetVolumePathNamesForVolumeName(VolumeName, TargetPath, CUInt(TargetPath.Length), length))

            If length <= 2 Then
                Return {}
            End If

            Dim Target As New String(TargetPath, 0, CInt(length - 2))

            Return Target.Split({New Char}, StringSplitOptions.RemoveEmptyEntries)

        End Function

        Public Shared Function GetDiskVolumes(DiskNumber As UInteger) As IEnumerable(Of String)

            Return (New VolumeEnumerator).Where(
                Function(volumeGuid)
                    Try
                        Return VolumeUsesDisk(volumeGuid, DiskNumber)

                    Catch ex As Exception
                        Trace.WriteLine(volumeGuid & ": " & ex.JoinMessages())
                        Return False

                    End Try
                End Function)

        End Function

        Public Shared Function VolumeUsesDisk(VolumeGuid As String, DiskNumber As UInteger) As Boolean

            Using volume As New DiskDevice(VolumeGuid.TrimEnd("\"c), 0)

                Try
                    Dim extents = GetVolumeDiskExtents(volume.SafeFileHandle)

                    Return extents.Any(Function(extent) extent.DiskNumber.Equals(DiskNumber))

                Catch ex As Win32Exception When _
                    ex.NativeErrorCode = Win32API.ERROR_INVALID_FUNCTION

                    Return False

                End Try

            End Using

        End Function

        Public Shared Sub ScanForHardwareChanges()

            ScanForHardwareChanges(Nothing)

        End Sub

        Public Shared Function ScanForHardwareChanges(rootid As String) As UInt32

            Dim devInst As UInt32
            Dim status = Win32API.CM_Locate_DevNode(devInst, rootid, 0)
            If status <> 0 Then
                Return status
            End If

            Return Win32API.CM_Reenumerate_DevNode(devInst, 0)

        End Function

        Public Shared Function GetDeviceInstancesForService(service As String, <Out> ByRef instances As String()) As UInt32

            Dim length As UInt32
            Dim status = Win32API.CM_Get_Device_ID_List_Size(length, service, Win32API.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            Dim Buffer(0 To CInt(length) - 1) As Char
            status = Win32API.CM_Get_Device_ID_List(service,
                                                    Buffer,
                                                    CUInt(Buffer.Length),
                                                    Win32API.CM_GETIDLIST_FILTER_SERVICE)
            If status <> 0 Then
                Return status
            End If

            instances = New String(Buffer).Split({New Char})

            Return status

        End Function

        Public Shared Sub RunDLLInstallHinfSection(OwnerWindow As IntPtr, InfPath As String, InfSection As String)

            Dim cmdLine = InfSection & " 132 " & InfPath
            Trace.WriteLine("RunDLLInstallFromInfSection: " & cmdLine)

            If InfPath.Contains(" ") Then
                Throw New ArgumentException("Arguments to this method cannot contain spaces.", "InfPath")
            End If

            If InfSection.Contains(" ") Then
                Throw New ArgumentException("Arguments to this method cannot contain spaces.", "InfSection")
            End If

            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Win32API.InstallHinfSection(OwnerWindow,
                                        Nothing,
                                        cmdLine,
                                        0)

        End Sub

        Public Shared Sub InstallFromInfSection(OwnerWindow As IntPtr, InfPath As String, InfSection As String)

            Trace.WriteLine("InstallFromInfSection: InfPath=""" & InfPath & """, InfSection=""" & InfSection & """")

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim ErrorLine As UInt32
            Dim hInf = Win32API.SetupOpenInfFile(InfPath,
                                                 Nothing,
                                                 &H2UI,
                                                 ErrorLine)
            If hInf.IsInvalid Then
                Throw New Win32Exception("Line number: " & ErrorLine)
            End If

            Using hInf

                Win32Try(Win32API.SetupInstallFromInfSection(OwnerWindow,
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

        Public Shared Sub CreateRootPnPDevice(OwnerWindow As IntPtr, InfPath As String, hwid As String)

            Trace.WriteLine("CreateOrUpdateRootPnPDevice: InfPath=""" & InfPath & """, hwid=""" & hwid & """")

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
            Win32Try(Win32API.SetupDiGetINFClass(InfPath,
                                                 ClassGUID,
                                                 ClassName,
                                                 CUInt(ClassName.Length),
                                                 0))

            Trace.WriteLine("CreateOrUpdateRootPnPDevice: ClassGUID=""" & ClassGUID.ToString() & """, ClassName=""" & New String(ClassName) & """")

            ''
            '' Create the container for the to-be-created Device Information Element.
            ''
            Dim DeviceInfoSet = Win32API.SetupDiCreateDeviceInfoList(ClassGUID,
                                                                     OwnerWindow)
            If DeviceInfoSet.IsInvalid Then
                Throw New Win32Exception
            End If

            Using DeviceInfoSet

                ''
                '' Now create the element.
                '' Use the Class GUID and Name from the INF file.
                ''
                Dim DeviceInfoData As Win32API.SP_DEVINFO_DATA
                DeviceInfoData.Initialize()
                Win32Try(Win32API.SetupDiCreateDeviceInfo(DeviceInfoSet,
                                                          ClassName,
                                                          ClassGUID,
                                                          Nothing,
                                                          OwnerWindow,
                                                          &H1UI,
                                                          DeviceInfoData))

                ''
                '' Add the HardwareID to the Device's HardwareID property.
                ''
                Win32Try(Win32API.SetupDiSetDeviceRegistryProperty(DeviceInfoSet,
                                                                   DeviceInfoData,
                                                                   &H1UI,
                                                                   hwIdList,
                                                                   CUInt(hwIdList.Length)))

                ''
                '' Transform the registry element into an actual devnode
                '' in the PnP HW tree.
                ''
                Win32Try(Win32API.SetupDiCallClassInstaller(DIF_REGISTERDEVICE,
                                                            DeviceInfoSet,
                                                            DeviceInfoData))

            End Using

            ''
            '' update the driver for the device we just created
            ''
            UpdateDriverForPnPDevices(OwnerWindow,
                                      InfPath,
                                      hwid,
                                      forceReplaceExisting:=True)

        End Sub

        '    Public Shared Function EnumerateDevices(DeviceInstanceName As String, Flags As UInt32, hwndParent As IntPtr) As Object()

        '        '    HDEVINFO devs = INVALID_HANDLE_VALUE
        '        'IdEntry * templ = NULL
        '        '    DWORD err
        '        '    int failcode = EXIT_FAIL
        '        '    int retcode
        '        '    int argIndex
        '        '    DWORD devIndex
        '        '    SP_DEVINFO_DATA devInfo
        '        '    SP_DEVINFO_LIST_DETAIL_DATA devInfoListDetail
        '        '    BOOL doSearch = False
        '        '    BOOL match
        '        '    BOOL all = False
        '        '    Guid cls
        '        '    DWORD numClass = 0
        '        '    int skip = 0

        '        If String.IsNullOrEmpty(DeviceInstanceName) Then
        '            Throw New ArgumentNullException("DeviceInstanceName")
        '        End If

        '        Dim devs = Win32API.SetupDiCreateDeviceInfoList(IntPtr.Zero, hwndParent)

        '        If devs.IsInvalid Then
        '            Throw New Win32Exception("SetupDiCreateDeviceInfoListEx")
        '        End If

        '        Using devs

        '            ''
        '            '' add explicit instances to list (even if enumerated all,
        '            '' this gets around DIGCF_PRESENT)
        '            '' do this even if wildcards appear to be detected since they
        '            '' might actually be part of the instance ID of a non-present device
        '            ''
        '            If Not Win32API.SetupDiOpenDeviceInfo(devs, DeviceInstanceName, hwndParent, 0, IntPtr.Zero) Then
        '                Throw New Win32Exception("SetupDiOpenDeviceInfo")
        '            End If


        '            Dim devInfoListDetail As Win32API.SP_DEVINFO_LIST_DETAIL_DATA
        '            devInfoListDetail.Initialize()

        '            If Not Win32API.SetupDiGetDeviceInfoListDetail(devs, devInfoListDetail) Then
        '                Throw New Win32Exception("SetupDiGetDeviceInfoListDetail")
        '            End If

        '            ''
        '            '' now enumerate them
        '            ''
        '            Dim devIndex As UInteger
        '            Do
        '                Dim devInfo As Win32API.SP_DEVINFO_DATA
        '                devInfo.Initialize()

        '                Win32API.SetupDiEnumDeviceInfo(devs, devIndex, devInfo)

        '        if(doSearch) {
        '            for(argIndex=skip,match=FALSE(argIndex<argc) && !matchargIndex++) {
        '                TCHAR devID[MAX_DEVICE_ID_LEN]
        '                LPTSTR *hwIds = NULL
        '                LPTSTR *compatIds = NULL
        '                        ''
        '                        '' determine instance ID
        '                        ''
        '                        If Win32API.CM_Get_Device_ID_Ex(devInfo.DevInst, devID, MAX_DEVICE_ID_LEN, 0, devInfoListDetail.RemoteMachineHandle) <> CR_SUCCESS Then
        '                    devID[0] = TEXT('\0')
        '                        End If

        '                if(templ[argIndex].InstanceId) {
        '                            ''
        '                            '' match on the instance ID
        '                            ''
        '                    if(WildCardMatch(devID,templ[argIndex])) {
        '                                match = True
        '                    }
        '                } else {
        '                                ''
        '                                '' determine hardware ID's
        '                                '' and search for matches
        '                                ''
        '                    hwIds = GetDevMultiSz(devs,&devInfo,SPDRP_HARDWAREID)
        '                    compatIds = GetDevMultiSz(devs,&devInfo,SPDRP_COMPATIBLEIDS)

        '                    if(WildCompareHwIds(hwIds,templ[argIndex]) ||
        '                        WildCompareHwIds(compatIds,templ[argIndex])) {
        '                                    match = True
        '                    }
        '                }
        '                                    DelMultiSz(hwIds)
        '                                    DelMultiSz(compatIds)
        '            }
        '        } else {
        '                                    match = True
        '        }
        '        if(match) {
        '            retcode = Callback(devs,&devInfo,devIndex,Context)
        '            if(retcode) {
        '                                            failcode = retcode
        '                                            GoTo final
        '            }
        '        }
        '    }

        '                                            failcode = EXIT_OK

        'final:
        '    if(templ) {
        '        delete [] templ
        '    }
        '    if(devs != INVALID_HANDLE_VALUE) {
        '                                                    SetupDiDestroyDeviceInfoList(devs)
        '    }
        '                                                    Return failcode
        '    End Function

        Public Shared Function RemovePnPDevice(OwnerWindow As IntPtr, hwid As String) As Integer

            Trace.WriteLine("RemovePnPDevice: hwid=""" & hwid & """")

            ''
            '' Create the container for the to-be-created Device Information Element.
            ''
            Dim DeviceInfoSet = Win32API.SetupDiCreateDeviceInfoList(IntPtr.Zero,
                                                                     OwnerWindow)
            If DeviceInfoSet.IsInvalid Then
                Throw New Win32Exception("SetupDiCreateDeviceInfoList")
            End If

            Using DeviceInfoSet

                If Not Win32API.SetupDiOpenDeviceInfo(DeviceInfoSet, hwid, OwnerWindow, 0, IntPtr.Zero) Then
                    Return 0
                End If

                Dim DeviceInfoData As Win32API.SP_DEVINFO_DATA
                DeviceInfoData.Initialize()

                Dim i As UInteger
                Dim done As Integer
                Do
                    If Not Win32API.SetupDiEnumDeviceInfo(DeviceInfoSet, i, DeviceInfoData) Then
                        If i = 0 Then
                            Throw New Win32Exception("SetupDiEnumDeviceInfo")
                        Else
                            Return done
                        End If
                    End If

                    If Win32API.SetupDiCallClassInstaller(DIF_REMOVE, DeviceInfoSet, DeviceInfoData) Then
                        done += 1
                    End If

                    i += 1UI
                Loop

            End Using

        End Function

        Public Shared Sub UpdateDriverForPnPDevices(OwnerWindow As IntPtr, InfPath As String, hwid As String, forceReplaceExisting As Boolean)

            Trace.WriteLine("UpdateDriverForPnPDevices: InfPath=""" & InfPath & """, hwid=""" & hwid & """, forceReplaceExisting=" & forceReplaceExisting)

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
            Win32Try(Win32API.UpdateDriverForPlugAndPlayDevices(OwnerWindow,
                                                                hwid,
                                                                InfPath,
                                                                If(forceReplaceExisting, &H1UI, &H0UI),
                                                                Nothing))

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

            Win32Try(Win32API.SetupCopyOEMInf(InfPath,
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

            Dim errcode = Win32API.DriverPackagePreinstall(InfPath, 1)
            If errcode <> 0 Then
                Throw New Win32Exception(errcode)
            End If

        End Sub

        Public Shared Sub DriverPackageInstall(InfPath As String, ByRef NeedReboot As Boolean)

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim errcode = Win32API.DriverPackageInstall(InfPath, 1, Nothing, NeedReboot)
            If errcode <> 0 Then
                Throw New Win32Exception(errcode)
            End If

        End Sub

        <Flags>
        Public Enum DriverPackageUninstallFlags As UInteger
            Normal = &H0UI
            DeleteFiles = Win32API.DRIVER_PACKAGE_DELETE_FILES
            Force = Win32API.DRIVER_PACKAGE_FORCE
            Silent = Win32API.DRIVER_PACKAGE_SILENT
        End Enum

        Public Shared Sub DriverPackageUninstall(InfPath As String, Flags As DriverPackageUninstallFlags, ByRef NeedReboot As Boolean)

            ''
            '' Inf must be a full pathname
            ''
            InfPath = Path.GetFullPath(InfPath)
            If Not File.Exists(InfPath) Then
                Throw New FileNotFoundException("File not found", InfPath)
            End If

            Dim errcode = Win32API.DriverPackageUninstall(InfPath, Flags, Nothing, NeedReboot)
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
                Where device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase)

                Try
                    Using device = OpenFileHandle("\\?\" & diskdevice, 0, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

                        UpdateDiskProperties(device)

                    End Using

                Catch ex As Exception
                    Trace.WriteLine("Error updating disk properties for " & diskdevice & ": " & ex.ToString())

                End Try

            Next

        End Sub

        ''' <summary>
        ''' Re-enumerates partitions on a disk device with a specified SCSI address. No
        ''' exceptions are thrown on error, but any exceptions from underlying API calls are
        ''' logged to trace log.
        ''' </summary>
        ''' <returns>Returns number of disk devices found with specified device number.</returns>
        Public Shared Function UpdateDiskProperties(ScsiAddress As Win32API.SCSI_ADDRESS) As Boolean

            Try
                Using devicehandle = OpenDiskByScsiAddress(ScsiAddress, Nothing).Value

                    UpdateDiskProperties(devicehandle)

                    Return True

                End Using

            Catch ex As Exception
                Trace.WriteLine("Error updating disk properties: " & ex.ToString())

            End Try

            Return False

        End Function

        ''' <summary>
        ''' Opens a disk device with a specified SCSI address and returns both name and an open handle.
        ''' </summary>
        Public Shared Function OpenDiskByScsiAddress(ScsiAddress As Win32API.SCSI_ADDRESS, AccessMode As FileAccess) As KeyValuePair(Of String, SafeFileHandle)

            For Each diskdevice In
                From device In QueryDosDevice()
                Where
                    device.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) OrElse
                    device.StartsWith("CdRom", StringComparison.OrdinalIgnoreCase)
                Select
                    String.Concat("\\?\", device)

                Try
                    Dim devicehandle = OpenFileHandle(diskdevice, AccessMode, FileShare.ReadWrite, FileMode.Open, Overlapped:=False)

                    Try
                        Dim Address = GetScsiAddress(devicehandle)

                        If Address <> ScsiAddress Then

                            devicehandle.Dispose()

                            Continue For

                        End If

                        Trace.WriteLine("Found " & diskdevice & " with SCSI address " & Address.ToString())

                        Return New KeyValuePair(Of String, SafeFileHandle)(diskdevice, devicehandle)

                    Catch ex As Exception
                        Trace.WriteLine("Exception while querying SCSI address for " & diskdevice & ": " & ex.ToString())

                        devicehandle.Dispose()

                    End Try

                Catch ex As Exception
                    Trace.WriteLine("Exception while opening " & diskdevice & ": " & ex.ToString())

                End Try

            Next

            Throw New DriveNotFoundException("No physical drive found with SCSI address: " & ScsiAddress.ToString())

        End Function

        Public Shared Function GetOSVersion() As Version

            Dim os_version As New Win32API.OSVERSIONINFO With {
                .dwOSVersionInfoSize = Marshal.SizeOf(os_version)
            }

            Dim status = Win32API.RtlGetVersion(os_version)

            If status < 0 Then
                Throw New Win32Exception(Win32API.RtlNtStatusToDosError(status))
            End If

            Return New Version(os_version.dwMajorVersion,
                               os_version.dwMinorVersion,
                               os_version.dwBuildNumber)

        End Function

    End Class

End Namespace
