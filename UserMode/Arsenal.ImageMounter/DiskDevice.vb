''''' DiskDevice.vb
''''' Class for controlling Arsenal Image Mounter Disk Devices.
''''' 
''''' Copyright (c) 2012-2014, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
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

    Private Sub AllowExtendedDasdIo()
        NativeFileIO.Win32API.DeviceIoControl(SafeFileHandle, NativeFileIO.Win32API.FSCTL_ALLOW_EXTENDED_DASD_IO, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, 0UI, IntPtr.Zero)
    End Sub

    Protected Friend Sub New(DeviceHandle As SafeFileHandle, AccessMode As FileAccess)
        MyBase.New(DeviceHandle, AccessMode)

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

        AllowExtendedDasdIo()
    End Sub

    ''' <summary>
    ''' Opens an disk device object, requesting read, write or both permissions.
    ''' </summary>
    ''' <param name="DevicePath"></param>
    ''' <param name="AccessMode"></param>
    Public Sub New(DevicePath As String, AccessMode As FileAccess)
        MyBase.New(DevicePath, AccessMode)

        AllowExtendedDasdIo()
    End Sub

    ''' <summary>
    ''' Opens an disk device object.
    ''' </summary>
    ''' <param name="ScsiAddress"></param>
    ''' <param name="AccessMode"></param>
    Public Sub New(ScsiAddress As NativeFileIO.Win32API.SCSI_ADDRESS, AccessMode As FileAccess)
        Me.New(NativeFileIO.OpenDiskByScsiAddress(ScsiAddress, AccessMode).Value, AccessMode)

    End Sub

    ''' <summary>
    ''' Retrieves device number for this disk.
    ''' </summary>
    Public Function GetDeviceNumber() As UInt32

        Return GetScsiAddress().DWordDeviceNumber

    End Function

    ''' <summary>
    ''' Retrieves device number for a disk, given a Win32 path such
    ''' as \\?\PhysicalDrive0
    ''' </summary>
    Public Shared Function GetDeviceNumber(path As String) As UInt32

        Using disk As New DiskDevice(path)
            Return disk.GetDeviceNumber()
        End Using

    End Function

    ''' <summary>
    ''' Retrieves SCSI address for this disk.
    ''' </summary>
    Public Function GetScsiAddress() As NativeFileIO.Win32API.SCSI_ADDRESS

        Return NativeFileIO.GetScsiAddress(SafeFileHandle)

    End Function

    ''' <summary>
    ''' Retrieves SCSI address for a disk, given a Win32 path such
    ''' as \\?\PhysicalDrive0
    ''' </summary>
    Public Shared Function GetScsiAddress(path As String) As NativeFileIO.Win32API.SCSI_ADDRESS

        Using disk As New DiskDevice(path)
            Return disk.GetScsiAddress()
        End Using

    End Function

    ''' <summary>
    ''' Opens SCSI adapter that created this virtual disk.
    ''' </summary>
    Public Function OpenAdapter() As ScsiAdapter

        Return New ScsiAdapter(NativeFileIO.GetScsiAddress(SafeFileHandle).PortNumber)

    End Function

    ''' <summary>
    ''' Updates disk properties by re-enumerating partition table.
    ''' </summary>
    Public Sub UpdateProperties()

        NativeFileIO.UpdateDiskProperties(SafeFileHandle)

    End Sub

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

        Dim FillRequestData =
          Sub(Request As BinaryWriter)
              Request.Write(GetDeviceNumber())
              Request.Write(0L)
              Request.Write(0UI)
              Request.Write(0L)
              Request.Write(0UI)
              Request.Write(65535US)
              Request.Write(New Byte(0 To 65534) {})
          End Sub

        Dim ReturnCode As Int32

        Dim Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
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

        Dim FillRequestData =
          Sub(Request As BinaryWriter)
              Request.Write(GetDeviceNumber())
          End Sub

        Dim ReturnCode As Int32

        Dim Response =
          NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                      NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_REMOVE_DEVICE,
                                                      0,
                                                      FillRequestData,
                                                      ReturnCode)

        If ReturnCode <> 0 Then
            Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
        End If

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
        If _RawDiskStream IsNot Nothing Then
            _RawDiskStream.Dispose()
            _RawDiskStream = Nothing
        End If

        MyBase.Dispose(disposing)
    End Sub

End Class


