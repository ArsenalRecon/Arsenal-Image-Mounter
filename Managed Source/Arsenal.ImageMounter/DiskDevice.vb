''''' DiskDevice.vb
''''' Class for controlling Arsenal Image Mounter Disk Devices.
''''' 
''''' Copyright (c) 2012-2015, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.IO


''' <summary>
''' Represents disk objects, attached to a virtual or physical SCSI adapter.
''' </summary>
Public Class DiskDevice
    Inherits DeviceObject

    Private _RawDiskStream As DiskStream

    Private _CachedDeviceNumber As UInteger?
    Private _CachedPortNumber As Byte?

    ''' <summary>
    ''' Returns the device path used to open this device object, if opened by name.
    ''' If the object was opened in any other way, such as by supplying an already
    ''' open handle, this property returns null/Nothing.
    ''' </summary>
    Public ReadOnly Property DevicePath As String

    Private Sub AllowExtendedDasdIo()
        NativeFileIO.Win32API.DeviceIoControl(SafeFileHandle, NativeFileIO.Win32API.FSCTL_ALLOW_EXTENDED_DASD_IO, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, 0UI, IntPtr.Zero)
    End Sub

    Protected Friend Sub New(DeviceNameAndHandle As KeyValuePair(Of String, SafeFileHandle), AccessMode As FileAccess)
        MyBase.New(DeviceNameAndHandle.Value, AccessMode)

        _DevicePath = DeviceNameAndHandle.Key

        AllowExtendedDasdIo()
    End Sub

    ''' <summary>
    ''' Opens an disk device object without requesting read or write permissions. The
    ''' resulting object can only be used to query properties like SCSI address, disk
    ''' size and similar, but not for reading or writing raw disk data.
    ''' </summary>
    ''' <param name="DevicePath"></param>
    Public Sub New(DevicePath As String)
        MyBase.New(DevicePath)

        _DevicePath = DevicePath

        AllowExtendedDasdIo()
    End Sub

    ''' <summary>
    ''' Opens an disk device object, requesting read, write or both permissions.
    ''' </summary>
    ''' <param name="DevicePath"></param>
    ''' <param name="AccessMode"></param>
    Public Sub New(DevicePath As String, AccessMode As FileAccess)
        MyBase.New(DevicePath, AccessMode)

        _DevicePath = DevicePath

        AllowExtendedDasdIo()
    End Sub

    ''' <summary>
    ''' Opens an disk device object.
    ''' </summary>
    ''' <param name="ScsiAddress"></param>
    ''' <param name="AccessMode"></param>
    Public Sub New(ScsiAddress As NativeFileIO.Win32API.SCSI_ADDRESS, AccessMode As FileAccess)
        Me.New(NativeFileIO.OpenDiskByScsiAddress(ScsiAddress, AccessMode), AccessMode)

    End Sub

    ''' <summary>
    ''' Retrieves device number for this disk.
    ''' </summary>
    Public Function GetDeviceNumber() As UInt32

        If _CachedDeviceNumber Is Nothing Then
            Dim scsi_address = ScsiAddress.Value

            Using driver As New ScsiAdapter(scsi_address.PortNumber)
            End Using

            _CachedDeviceNumber = scsi_address.DWordDeviceNumber
            _CachedPortNumber = scsi_address.PortNumber
        End If

        Return _CachedDeviceNumber.Value

    End Function

    ''' <summary>
    ''' Retrieves SCSI address for this disk.
    ''' </summary>
    Public ReadOnly Property ScsiAddress As NativeFileIO.Win32API.SCSI_ADDRESS?
        Get
            Return NativeFileIO.GetScsiAddress(SafeFileHandle)
        End Get
    End Property

    ''' <summary>
    ''' Retrieves storage device type and physical disk number information.
    ''' </summary>
    Public ReadOnly Property StorageDeviceNumber As NativeFileIO.Win32API.STORAGE_DEVICE_NUMBER?
        Get
            Return NativeFileIO.GetStorageDeviceNumber(SafeFileHandle)
        End Get
    End Property

    ''' <summary>
    ''' Opens SCSI adapter that created this virtual disk.
    ''' </summary>
    Public Function OpenAdapter() As ScsiAdapter

        Return New ScsiAdapter(NativeFileIO.GetScsiAddress(SafeFileHandle).Value.PortNumber)

    End Function

    ''' <summary>
    ''' Updates disk properties by re-enumerating partition table.
    ''' </summary>
    Public Sub UpdateProperties()

        NativeFileIO.UpdateDiskProperties(SafeFileHandle)

    End Sub

    ''' <summary>
    ''' Retrieves the physical location of a specified volume on one or more disks. 
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property VolumeDiskExtents As NativeFileIO.DiskExtent()
        Get
            Return NativeFileIO.GetVolumeDiskExtents(SafeFileHandle)
        End Get
    End Property

    ''' <summary>
    ''' Gets or sets disk signature stored in boot record.
    ''' </summary>
    Public Property DiskSignature As UInt32
        Get
            Dim rawsig(0 To Convert.ToInt32(Geometry.BytesPerSector - 1UI)) As Byte
            With GetRawDiskStream()
                .Position = 0
                .Read(rawsig, 0, rawsig.Length)
            End With
            Return BitConverter.ToUInt32(rawsig, &H1B8)
        End Get
        Set
            Dim newvalue = BitConverter.GetBytes(Value)
            Dim rawsig(0 To Convert.ToInt32(Geometry.BytesPerSector - 1UI)) As Byte
            With GetRawDiskStream()
                .Position = 0
                .Read(rawsig, 0, rawsig.Length)
                Array.Copy(newvalue, 0, rawsig, &H1B8, newvalue.Length)
                .Position = 0
                .Write(rawsig, 0, rawsig.Length)
            End With
        End Set
    End Property

    ''' <summary>
    ''' Flush buffers for a disk or volume.
    ''' </summary>
    Public Sub FlushBuffers()
        NativeFileIO.FlushBuffers(SafeFileHandle)
    End Sub

    ''' <summary>
    ''' Gets or sets physical disk offline attribute. Only valid for
    ''' physical disk objects, not volumes or partitions.
    ''' </summary>
    Public Property DiskOffline As Boolean?
        Get
            Return NativeFileIO.GetDiskOffline(SafeFileHandle)
        End Get
        Set
            If Value.HasValue Then
                NativeFileIO.SetDiskOffline(SafeFileHandle, Value.Value)
            End If
        End Set
    End Property

    ''' <summary>
    ''' Gets or sets physical disk read only attribute. Only valid for
    ''' physical disk objects, not volumes or partitions.
    ''' </summary>
    Public Property DiskReadOnly As Boolean?
        Get
            Return NativeFileIO.GetDiskReadOnly(SafeFileHandle)
        End Get
        Set
            If Value.HasValue Then
                NativeFileIO.SetDiskReadOnly(SafeFileHandle, Value.Value)
            End If
        End Set
    End Property

    ''' <summary>
    ''' Sets disk volume offline attribute. Only valid for logical
    ''' disk volumes, not physical disk drives.
    ''' </summary>
    Public WriteOnly Property VolumeOffline As Boolean
        Set
            NativeFileIO.SetVolumeOffline(SafeFileHandle, Value)
        End Set
    End Property

    ''' <summary>
    ''' Gets information about a partition stored on a disk with MBR
    ''' partition layout. This property is not available for physical
    ''' disks, only disk partitions are supported.
    ''' </summary>
    Public ReadOnly Property PartitionInformation As NativeFileIO.Win32API.PARTITION_INFORMATION?
        Get
            Return NativeFileIO.GetPartitionInformation(SafeFileHandle)
        End Get
    End Property

    ''' <summary>
    ''' Gets information about a disk partition. This property is not
    ''' available for physical disks, only disk partitions are supported.
    ''' </summary>
    Public ReadOnly Property PartitionInformationEx As NativeFileIO.Win32API.PARTITION_INFORMATION_EX?
        Get
            Return NativeFileIO.GetPartitionInformationEx(SafeFileHandle)
        End Get
    End Property

    ''' <summary>
    ''' Retrieves properties for an existing virtual disk.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number of virtual disk.</param>
    ''' <param name="DiskSize">Size of virtual disk.</param>
    ''' <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
    ''' <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    ''' Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter or Windows
    ''' filesystem drivers.</param>
    ''' <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    ''' <param name="Filename">Name of disk image file holding storage for file type virtual disk or used to create a
    ''' virtual memory type virtual disk.</param>
    Public Sub QueryDevice(ByRef DeviceNumber As UInt32,
                           ByRef DiskSize As Int64,
                           ByRef BytesPerSector As UInt32,
                           ByRef ImageOffset As Int64,
                           ByRef Flags As DeviceFlags,
                           ByRef Filename As String)

        Dim scsi_address = ScsiAddress.Value

        Using adapter As New ScsiAdapter(scsi_address.PortNumber)

            Dim FillRequestData =
              Sub(Request As BinaryWriter)
                  Request.Write(scsi_address.DWordDeviceNumber)
                  Request.Write(0L)
                  Request.Write(0UI)
                  Request.Write(0L)
                  Request.Write(0UI)
                  Request.Write(65535US)
                  Request.Write(New Byte(0 To 65534) {})
              End Sub

            Dim ReturnCode As Int32

            Dim Response =
                NativeFileIO.PhDiskMntCtl.SendSrbIoControl(
                    SafeFileHandle,
                    NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_DEVICE,
                    0,
                    FillRequestData,
                    ReturnCode)

            If ReturnCode <> 0 Then
                Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
            End If

            DeviceNumber = Response.ReadUInt32()
            DiskSize = Response.ReadInt64()
            BytesPerSector = Response.ReadUInt32()
            Response.ReadUInt32()
            ImageOffset = Response.ReadInt64()
            Flags = CType(Response.ReadUInt32(), DeviceFlags)
            Dim FilenameLength = Response.ReadUInt16()
            If FilenameLength = 0 Then
                Filename = Nothing
            Else
                Filename = Encoding.Unicode.GetString(Response.ReadBytes(FilenameLength))
            End If

        End Using

    End Sub

    ''' <summary>
    ''' Retrieves properties for an existing virtual disk.
    ''' </summary>
    Public Function QueryDevice() As ScsiAdapter.DeviceProperties

        Dim DeviceProperties As New ScsiAdapter.DeviceProperties
        QueryDevice(DeviceProperties.DeviceNumber,
                    DeviceProperties.DiskSize,
                    DeviceProperties.BytesPerSector,
                    DeviceProperties.ImageOffset,
                    DeviceProperties.Flags,
                    DeviceProperties.Filename)
        Return DeviceProperties

    End Function

    ''' <summary>
    ''' Removes this virtual disk from adapter.
    ''' </summary>
    Public Sub RemoveDevice()

        Dim scsi_address = ScsiAddress.Value

        Using adapter As New ScsiAdapter(scsi_address.PortNumber)

            adapter.RemoveDevice(scsi_address.DWordDeviceNumber)

        End Using

    End Sub

    ''' <summary>
    ''' Retrieves volume size of disk device.
    ''' </summary>
    Public ReadOnly Property DiskSize As Long
        Get
            Return NativeFileIO.GetDiskSize(SafeFileHandle)
        End Get
    End Property

    ''' <summary>
    ''' Determines whether disk is writable or read-only.
    ''' </summary>
    Public ReadOnly Property IsDiskWritable As Boolean
        Get
            Return NativeFileIO.IsDiskWritable(SafeFileHandle)
        End Get
    End Property

    ''' <summary>
    ''' Returns logical disk geometry. Normally, only the BytesPerSector member
    ''' contains data of interest.
    ''' </summary>
    Public ReadOnly Property Geometry As NativeFileIO.Win32API.DISK_GEOMETRY
        Get
            Return NativeFileIO.GetDiskGeometry(SafeFileHandle)
        End Get
    End Property

    ''' <summary>
    ''' Locks and dismounts filesystem on a volume. Upon successful return, further access to the device
    ''' can only be done through this device object instance until it is either closed (disposed) or lock is
    ''' released on the underlying handle.
    ''' </summary>
    ''' <param name="Force">Indicates if True that volume should be immediately dismounted even if it
    ''' cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
    ''' successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
    Public Sub DismountVolumeFilesystem(Force As Boolean)

        NativeFileIO.Win32Try(NativeFileIO.DismountVolumeFilesystem(SafeFileHandle, Force))

    End Sub

    ''' <summary>
    ''' Returns an DiskStream object that can be used to directly access disk data.
    ''' </summary>
    Public Function GetRawDiskStream() As DiskStream
        If _RawDiskStream Is Nothing Then
            _RawDiskStream = New DiskStream(SafeFileHandle,
                                            If(AccessMode = 0, FileAccess.Read, AccessMode))
        End If
        Return _RawDiskStream
    End Function

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _RawDiskStream?.Dispose()
        End If
        _RawDiskStream = Nothing

        MyBase.Dispose(disposing)
    End Sub

End Class


