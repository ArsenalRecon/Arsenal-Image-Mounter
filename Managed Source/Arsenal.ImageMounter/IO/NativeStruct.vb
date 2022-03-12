Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Globalization
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Runtime.Versioning
Imports System.Security
Imports System.Security.AccessControl
Imports System.Threading
Imports Arsenal.ImageMounter.Extensions
Imports Microsoft.Win32.SafeHandles

#Disable Warning CA1069 ' Enums values should not be duplicated

Namespace IO

    Public Module NativeStruct

        Public Function GetFileSize(path As String) As Long

#If NET461_OR_GREATER OrElse NETSTANDARD OrElse NETCOREAPP Then
            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Return New FileInfo(path).Length
            End If
#End If

            Return NativeFileIO.GetFileSize(path)

        End Function

        Public Function ReadAllBytes(path As String) As Byte()

#If NET461_OR_GREATER OrElse NETSTANDARD OrElse NETCOREAPP Then
            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Return File.ReadAllBytes(path)
            End If
#End If

            Using stream = NativeFileIO.OpenFileStream(path,
                                                       FileMode.Open,
                                                       FileAccess.Read,
                                                       FileShare.Read Or FileShare.Delete,
                                                       CType(NativeConstants.FILE_FLAG_BACKUP_SEMANTICS, FileOptions))

                Dim buffer(0 To CInt(stream.Length - 1)) As Byte

                If stream.Read(buffer, 0, buffer.Length) <> stream.Length Then

                    Throw New IOException($"Incomplete read from '{path}'")

                End If

                Return buffer

            End Using

        End Function

#If NETSTANDARD2_1_OR_GREATER OrElse NETCOREAPP Then
        Public Async Function ReadAllBytesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Byte())

            If Not RuntimeInformation.IsOSPlatform(OSPlatform.Windows) Then
                Return Await File.ReadAllBytesAsync(path, cancellationToken)
            End If

            Using stream = NativeFileIO.OpenFileStream(path,
                                                       FileMode.Open,
                                                       FileAccess.Read,
                                                       FileShare.Read Or FileShare.Delete,
                                                       CType(NativeConstants.FILE_FLAG_BACKUP_SEMANTICS, FileOptions) Or FileOptions.Asynchronous)

                Dim buffer(0 To CInt(stream.Length - 1)) As Byte

                If Await stream.ReadAsync(buffer, cancellationToken) <> stream.Length Then

                    Throw New IOException($"Incomplete read from '{path}'")

                End If

                Return buffer

            End Using

        End Function
#End If

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
        Public NotInheritable Class SafeFindHandle
            Inherits SafeHandleMinusOneIsInvalid

            Private Declare Auto Function FindClose Lib "kernel32" (
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
            Public Sub New()
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

        Public Structure DiskExtent
            Public ReadOnly Property DiskNumber As UInteger
            Public ReadOnly Property StartingOffset As Long
            Public ReadOnly Property ExtentLength As Long
        End Structure

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

        Public Enum NtFileCreated

            Superseded
            Opened
            Created
            Overwritten
            Exists
            DoesNotExist

        End Enum

        Private ReadOnly KnownFormats As New Dictionary(Of String, Long)(StringComparer.OrdinalIgnoreCase) From
          {
            {"nrg", 600 << 9},
            {"sdi", 8 << 9}
          }

        ''' <summary>
        ''' Checks if filename contains a known extension for which PhDskMnt knows of a constant offset value. That value can be
        ''' later passed as Offset parameter to CreateDevice method.
        ''' </summary>
        ''' <param name="ImageFile">Name of disk image file.</param>
        Public Function GetOffsetByFileExt(ImageFile As String) As Long

            Dim Offset As Long
            If KnownFormats.TryGetValue(Path.GetExtension(ImageFile), Offset) Then
                Return Offset
            Else
                Return 0
            End If

        End Function

        ''' <summary>
        ''' Returns sector size typically used for image file name extensions. Returns 2048 for
        ''' .iso, .nrg and .bin. Returns 512 for all other file name extensions.
        ''' </summary>
        ''' <param name="ImageFile">Name of disk image file.</param>
        Public Function GetSectorSizeFromFileName(imagefile As String) As UInteger

            imagefile.NullCheck(NameOf(imagefile))

            If imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) OrElse
                    imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) OrElse
                    imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) Then

                Return 2048
            Else
                Return 512
            End If

        End Function

        Private ReadOnly multipliers As New Dictionary(Of ULong, String) From
            {{1UL << 60, " EB"},
             {1UL << 50, " PB"},
             {1UL << 40, " TB"},
             {1UL << 30, " GB"},
             {1UL << 20, " MB"},
             {1UL << 10, " KB"}}

        Public Function FormatBytes(size As ULong) As String

            For Each m In multipliers
                If size >= m.Key Then
                    Return $"{size / m.Key:0.0}{m.Value}"
                End If
            Next

            Return $"{size} byte"

        End Function

        Public Function FormatBytes(size As ULong, precision As Integer) As String

            For Each m In multipliers
                If size >= m.Key Then
                    Return $"{(size / m.Key).ToString($"0.{New String("0"c, precision - 1)}")}{m.Value}"
                End If
            Next

            Return $"{size} byte"

        End Function

        Public Function FormatBytes(size As Long) As String

            For Each m In multipliers
                If Math.Abs(size) >= m.Key Then
                    Return $"{size / m.Key:0.000}{m.Value}"
                End If
            Next

            If size = 1 Then
                Return $"{size} byte"
            Else
                Return $"{size} bytes"
            End If

        End Function

        Public Function FormatBytes(size As Long, precision As Integer) As String

            For Each m In multipliers
                If size >= m.Key Then
                    Return $"{(size / m.Key).ToString("0." & New String("0"c, precision - 1))}{m.Value}"
                End If
            Next

            Return $"{size} byte"

        End Function

        ''' <summary>
        ''' Checks if Flags specifies a read only virtual disk.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function IsReadOnly(Flags As DeviceFlags) As Boolean

            Return Flags.HasFlag(DeviceFlags.ReadOnly)

        End Function

        ''' <summary>
        ''' Checks if Flags specifies a removable virtual disk.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function IsRemovable(Flags As DeviceFlags) As Boolean

            Return Flags.HasFlag(DeviceFlags.Removable)

        End Function

        ''' <summary>
        ''' Checks if Flags specifies a modified virtual disk.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function IsModified(Flags As DeviceFlags) As Boolean

            Return Flags.HasFlag(DeviceFlags.Modified)

        End Function

        ''' <summary>
        ''' Gets device type bits from a Flag field.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function GetDeviceType(Flags As DeviceFlags) As DeviceFlags

            Return CType(Flags And &HF0UI, DeviceFlags)

        End Function

        ''' <summary>
        ''' Gets disk type bits from a Flag field.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function GetDiskType(Flags As DeviceFlags) As DeviceFlags

            Return CType(Flags And &HF00UI, DeviceFlags)

        End Function

        ''' <summary>
        ''' Gets proxy type bits from a Flag field.
        ''' </summary>
        ''' <param name="Flags">Flag field to check.</param>
        <Extension>
        Public Function GetProxyType(Flags As DeviceFlags) As DeviceFlags

            Return CType(Flags And &HF000UI, DeviceFlags)

        End Function

        Public MustInherit Class NativeConstants

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

            <SupportedOSPlatform(API.SUPPORTED_WINDOWS_PLATFORM)>
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
            Public Const FSCTL_GET_RETRIEVAL_POINTERS As UInt32 = &H90073
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

            Public Const DIGCF_DEFAULT As UInt32 = &H1
            Public Const DIGCF_PRESENT As UInt32 = &H2
            Public Const DIGCF_ALLCLASSES As UInt32 = &H4
            Public Const DIGCF_PROFILE As UInt32 = &H8
            Public Const DIGCF_DEVICEINTERFACE As UInt32 = &H10

            Public Const DRIVER_PACKAGE_DELETE_FILES = &H20UI
            Public Const DRIVER_PACKAGE_FORCE = &H4UI
            Public Const DRIVER_PACKAGE_SILENT = &H2UI

            Public Const CM_GETIDLIST_FILTER_SERVICE As UInt32 = &H2UI

            Public Const DIF_PROPERTYCHANGE As UInt32 = &H12
            Public Const DICS_FLAG_CONFIGSPECIFIC As UInt32 = &H2  '' make change in specified profile only
            Public Const DICS_PROPCHANGE As UInt32 = &H3

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

            Public Const FILE_BEGIN As Integer = 0
            Public Const FILE_CURRENT As Integer = 1
            Public Const FILE_END As Integer = 2

            Private Sub New()
            End Sub

        End Class

    End Module

End Namespace
