''''' ScsiAdapter.vb
''''' Class for controlling Arsenal Image Mounter Devices.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.ComponentModel
Imports System.Diagnostics.CodeAnalysis
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32.SafeHandles

''' <summary>
''' Represents Arsenal Image Mounter objects.
''' </summary>
Public Class ScsiAdapter
    Inherits DeviceObject

    Public Const CompatibleDriverVersion As UInteger = &H101

    Public Const AutoDeviceNumber As UInt32 = &HFFFFFF

    Public ReadOnly Property DeviceInstanceName As String

    Public ReadOnly Property DeviceInstance As UInteger

    ''' <summary>
    ''' Object storing properties for a virtual disk device. Returned by
    ''' QueryDevice() method.
    ''' </summary>
    Public NotInheritable Class DeviceProperties

        Public Sub New(adapter As ScsiAdapter, device_number As UInt32)

            _DeviceNumber = device_number

            adapter.QueryDevice(_DeviceNumber,
                                _DiskSize,
                                _BytesPerSector,
                                _ImageOffset,
                                _Flags,
                                _Filename,
                                _WriteOverlayImageFile)

        End Sub

        ''' <summary>Device number of virtual disk.</summary>
        Public ReadOnly Property DeviceNumber As UInt32

        ''' <summary>Size of virtual disk.</summary>
        Public ReadOnly Property DiskSize As Int64

        ''' <summary>Number of bytes per sector for virtual disk geometry.</summary>
        Public ReadOnly Property BytesPerSector As UInt32

        ''' <summary>A skip offset if virtual disk data does not begin immediately at start of disk image file.
        ''' Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
        ''' or Windows filesystem drivers.</summary>
        Public ReadOnly Property ImageOffset As Int64

        ''' <summary>Flags specifying properties for virtual disk. See comments for each flag value.</summary>
        Public ReadOnly Property Flags As DeviceFlags

        ''' <summary>Name of disk image file holding storage for file type virtual disk or used to create a
        ''' virtual memory type virtual disk.</summary>
        Public Property Filename As String

        ''' <summary>Path to differencing file used in write-temporary mode.</summary>
        Public ReadOnly Property WriteOverlayImageFile As String

    End Class

    Private Shared Function OpenAdapterHandle(ntdevice As String, devInst As UInteger) As SafeFileHandle

        Dim handle As SafeFileHandle
        Try
            handle = NativeFileIO.NtCreateFile(ntdevice, 0,
                                                 FileAccess.ReadWrite,
                                                 FileShare.ReadWrite,
                                                 NativeFileIO.NtCreateDisposition.Open,
                                                 0, 0, Nothing, Nothing)

        Catch ex As Exception
            Trace.WriteLine($"PhDskMnt::OpenAdapterHandle: Error opening device '{ntdevice}': {ex.JoinMessages()}")

            Return Nothing

        End Try

        Dim acceptedversion As Boolean
        For i = 1 To 3
            Try
                acceptedversion = CheckDriverVersion(handle)
                If acceptedversion Then
                    Return handle
                Else
                    handle.Dispose()
                    Throw New Exception("Incompatible version of Arsenal Image Mounter Miniport driver.")
                End If

            Catch ex As Win32Exception When _
                (ex.NativeErrorCode = NativeFileIO.NativeConstants.ERROR_INVALID_FUNCTION) OrElse
                (ex.NativeErrorCode = NativeFileIO.NativeConstants.ERROR_IO_DEVICE)

                '' In case of SCSIPORT (Win XP) miniport, there is always a risk
                '' that we lose contact with IOCTL_SCSI_MINIPORT after device adds
                '' and removes. Therefore, in case we know that we have a handle to
                '' the SCSI adapter and it fails IOCTL_SCSI_MINIPORT requests, just
                '' issue a bus re-enumeration to find the dummy IOCTL device, which
                '' will make SCSIPORT let control requests through again.
                If Not DriverSetup.HasStorPort Then
                    Trace.WriteLine("PhDskMnt::OpenAdapterHandle: Lost contact with miniport, rescanning...")
                    Try
                        API.RescanScsiAdapter(devInst)
                        Thread.Sleep(100)
                        Continue For

                    Catch ex2 As Exception
                        Trace.WriteLine($"PhDskMnt::RescanScsiAdapter: {ex2}")

                    End Try
                End If
                handle.Dispose()
                Return Nothing

            Catch ex As Exception
                If TypeOf ex Is Win32Exception Then
                    Trace.WriteLine($"Error code 0x{DirectCast(ex, Win32Exception).NativeErrorCode:X8}")
                End If
                Trace.WriteLine($"PhDskMnt::OpenAdapterHandle: Error checking driver version: {ex.JoinMessages()}")
                handle.Dispose()
                Return Nothing

            End Try
        Next

        Return Nothing

    End Function

    Private NotInheritable Class AdapterDeviceInstance

        Public ReadOnly Property DevInstName As String

        Public ReadOnly Property DevInst As UInteger

        Public ReadOnly Property SafeHandle As SafeFileHandle

        Public Sub New(devInstName As String, devInst As UInteger, safeHhandle As SafeFileHandle)
            _DevInstName = devInstName
            _DevInst = devInst
            _SafeHandle = safeHhandle
        End Sub

    End Class

    ''' <summary>
    ''' Retrieves a handle to first found adapter, or null if error occurs.
    ''' </summary>
    ''' <remarks>Arsenal Image Mounter does not currently support more than one adapter.</remarks>
    ''' <returns>An object containing devinst value and an open handle to first found
    ''' compatible adapter.</returns>
    Private Shared Function OpenAdapter() As AdapterDeviceInstance

        Dim devinstNames = API.EnumerateAdapterDeviceInstanceNames()

        If devinstNames Is Nothing Then

            Throw New FileNotFoundException("No Arsenal Image Mounter adapter found.")

        End If

        Dim found = Aggregate devInstName In devinstNames
                    Let devinst = NativeFileIO.GetDevInst(devInstName)
                    Where devinst.HasValue
                    Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinst.Value)
                    Where path IsNot Nothing
                    Let handle = OpenAdapterHandle(path, devinst.Value)
                    Where handle IsNot Nothing
                    Select New AdapterDeviceInstance(devInstName, devinst.Value, handle)
                    Into FirstOrDefault()

        If found Is Nothing Then

            Throw New FileNotFoundException("No Arsenal Image Mounter adapter found.")

        End If

        Return found

    End Function

    ''' <summary>
    ''' Opens first found Arsenal Image Mounter.
    ''' </summary>
    Public Sub New()
        Me.New(OpenAdapter())

    End Sub

    Private Sub New(OpenAdapterHandle As AdapterDeviceInstance)
        MyBase.New(OpenAdapterHandle.SafeHandle, FileAccess.ReadWrite)

        _DeviceInstance = OpenAdapterHandle.DevInst
        _DeviceInstanceName = OpenAdapterHandle.DevInstName

        Trace.WriteLine($"Successfully opened SCSI adapter '{OpenAdapterHandle.DevInstName}'.")
    End Sub

    ''' <summary>
    ''' Opens a specific Arsenal Image Mounter adapter specified by SCSI port number.
    ''' </summary>
    ''' <param name="ScsiPortNumber">Scsi adapter port number as assigned by SCSI class driver.</param>
    Public Sub New(ScsiPortNumber As Byte)
        MyBase.New($"\\?\Scsi{ScsiPortNumber}:", FileAccess.ReadWrite)

        Trace.WriteLine($"Successfully opened adapter with SCSI portnumber = {ScsiPortNumber}.")

        If Not CheckDriverVersion() Then
            Throw New Exception("Incompatible version of Arsenal Image Mounter Miniport driver.")
        End If

    End Sub

    ''' <summary>
    ''' Retrieves a list of virtual disks on this adapter. Each element in returned list holds device number of an existing
    ''' virtual disk.
    ''' </summary>
    Public Function GetDeviceList() As UInteger()

        Dim ReturnCode As Int32

        Dim Response =
            NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                      NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_ADAPTER,
                                                      0,
                                                      New Byte(0 To 65535) {},
                                                      ReturnCode)

        If ReturnCode <> 0 Then
            Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
        End If

        Dim NumberOfDevices = BitConverter.ToInt32(Response, 0)

        Dim array(0 To NumberOfDevices - 1) As UInteger

        Buffer.BlockCopy(Response, 4, array, 0, NumberOfDevices * 4)

        Return array

    End Function

    ''' <summary>
    ''' Retrieves a list of DeviceProperties objects for each virtual disk on this adapter.
    ''' </summary>
    Public Function EnumerateDevicesProperties() As IEnumerable(Of DeviceProperties)

        Return GetDeviceList().Select(AddressOf QueryDevice)

    End Function

    ''' <summary>
    ''' Creates a new virtual disk.
    ''' </summary>
    ''' <param name="DiskSize">Size of virtual disk. If this parameter is zero, current size of disk image file will
    ''' automatically be used as virtual disk size.</param>
    ''' <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry. This parameter can be zero
    '''  in which case most reasonable value will be automatically used by the driver.</param>
    ''' <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    ''' Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    ''' or Windows filesystem drivers.</param>
    ''' <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    ''' <param name="Filename">Name of disk image file to use or create. If disk image file already exists, the DiskSize
    ''' parameter can be zero in which case current disk image file size will be used as virtual disk size. If Filename
    ''' parameter is Nothing/null disk will be created in virtual memory and not backed by a physical disk image file.</param>
    ''' <param name="NativePath">Specifies whether Filename parameter specifies a path in Windows native path format, the
    ''' path format used by drivers in Windows NT kernels, for example \Device\Harddisk0\Partition1\imagefile.img. If this
    ''' parameter is False path in FIlename parameter will be interpreted as an ordinary user application path.</param>
    ''' <param name="DeviceNumber">In: Device number for device to create. Device number must not be in use by an existing
    ''' virtual disk. For automatic allocation of device number, pass ScsiAdapter.AutoDeviceNumber.
    '''
    ''' Out: Device number for created device.</param>
    Public Sub CreateDevice(DiskSize As Int64,
                            BytesPerSector As UInt32,
                            ImageOffset As Int64,
                            Flags As DeviceFlags,
                            Filename As String,
                            NativePath As Boolean,
                            ByRef DeviceNumber As UInt32)

        CreateDevice(
            DiskSize,
            BytesPerSector,
            ImageOffset,
            Flags,
            Filename,
            NativePath,
            WriteOverlayFilename:=Nothing,
            WriteOverlayNativePath:=Nothing,
            DeviceNumber:=DeviceNumber)

    End Sub

    ''' <summary>
    ''' Creates a new virtual disk.
    ''' </summary>
    ''' <param name="DiskSize">Size of virtual disk. If this parameter is zero, current size of disk image file will
    ''' automatically be used as virtual disk size.</param>
    ''' <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry. This parameter can be zero
    '''  in which case most reasonable value will be automatically used by the driver.</param>
    ''' <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    ''' Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    ''' or Windows filesystem drivers.</param>
    ''' <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    ''' <param name="Filename">Name of disk image file to use or create. If disk image file already exists, the DiskSize
    ''' parameter can be zero in which case current disk image file size will be used as virtual disk size. If Filename
    ''' parameter is Nothing/null disk will be created in virtual memory and not backed by a physical disk image file.</param>
    ''' <param name="NativePath">Specifies whether Filename parameter specifies a path in Windows native path format, the
    ''' path format used by drivers in Windows NT kernels, for example \Device\Harddisk0\Partition1\imagefile.img. If this
    ''' parameter is False path in Filename parameter will be interpreted as an ordinary user application path.</param>
    ''' <param name="WriteOverlayFilename">Name of differencing image file to use for write overlay operation. Flags fields
    ''' must also specify read-only device and write overlay operation for this field to be used.</param>
    ''' <param name="WriteOverlayNativePath">Specifies whether WriteOverlayFilename parameter specifies a path in Windows
    ''' native path format, the path format used by drivers in Windows NT kernels, for example
    ''' \Device\Harddisk0\Partition1\imagefile.img. If this parameter is False path in Filename parameter will be interpreted
    ''' as an ordinary user application path.</param>
    ''' <param name="DeviceNumber">In: Device number for device to create. Device number must not be in use by an existing
    ''' virtual disk. For automatic allocation of device number, pass ScsiAdapter.AutoDeviceNumber.
    '''
    ''' Out: Device number for created device.</param>
    Public Sub CreateDevice(DiskSize As Int64,
                            BytesPerSector As UInt32,
                            ImageOffset As Int64,
                            Flags As DeviceFlags,
                            Filename As String,
                            NativePath As Boolean,
                            WriteOverlayFilename As String,
                            WriteOverlayNativePath As Boolean,
                            ByRef DeviceNumber As UInt32)

        '' Temporary variable for passing through lambda function
        Dim devnr = DeviceNumber

        '' Both UInt32.MaxValue and AutoDeviceNumber can be used
        '' for auto-selecting device number, but only AutoDeviceNumber
        '' is accepted by driver.
        If devnr = UInteger.MaxValue Then
            devnr = AutoDeviceNumber
        End If

        '' Translate Win32 path to native NT path that kernel understands
        If (Not String.IsNullOrEmpty(Filename)) AndAlso (Not NativePath) Then
            Select Case API.GetDiskType(Flags)
                Case DeviceFlags.TypeProxy
                    Select Case API.GetProxyType(Flags)

                        Case DeviceFlags.ProxyTypeSharedMemory
                            Filename = $"\BaseNamedObjects\Global\{Filename}"

                        Case DeviceFlags.ProxyTypeComm, DeviceFlags.ProxyTypeTCP

                        Case Else
                            Filename = NativeFileIO.GetNtPath(Filename)

                    End Select

                Case Else
                    Filename = NativeFileIO.GetNtPath(Filename)

            End Select
        End If

        '' Show what we got
        Trace.WriteLine($"ScsiAdapter.CreateDevice: Native filename='{Filename}'")

        Dim write_filter_added As GlobalCriticalMutex = Nothing

        Try

            If Not String.IsNullOrWhiteSpace(WriteOverlayFilename) Then

                If (Not WriteOverlayNativePath) Then
                    WriteOverlayFilename = NativeFileIO.GetNtPath(WriteOverlayFilename)
                End If

                Trace.WriteLine($"ScsiAdapter.CreateDevice: Thread {Thread.CurrentThread.ManagedThreadId} entering global critical section")

                write_filter_added = New GlobalCriticalMutex()

                NativeFileIO.AddFilter(NativeFileIO.NativeConstants.DiskDriveGuid, "aimwrfltr", addfirst:=True)

            End If

            '' Show what we got
            Trace.WriteLine($"ScsiAdapter.CreateDevice: Native write overlay filename='{WriteOverlayFilename}'")

            Dim ReservedField(0 To 3) As Byte

            If Not String.IsNullOrWhiteSpace(WriteOverlayFilename) Then
                Dim bytes = BitConverter.GetBytes(CUShort(WriteOverlayFilename.Length * 2))
                Buffer.BlockCopy(bytes, 0, ReservedField, 0, bytes.Length)
            End If

            Dim Request As New BufferedBinaryWriter
            Request.Write(devnr)
            Request.Write(DiskSize)
            Request.Write(BytesPerSector)
            Request.Write(ReservedField)
            Request.Write(ImageOffset)
            Request.Write(CUInt(Flags))
            If String.IsNullOrEmpty(Filename) Then
                Request.Write(0US)
            Else
                Dim bytes = Encoding.Unicode.GetBytes(Filename)
                Request.Write(CUShort(bytes.Length))
                Request.Write(bytes)
            End If
            If Not String.IsNullOrWhiteSpace(WriteOverlayFilename) Then
                Dim bytes = Encoding.Unicode.GetBytes(WriteOverlayFilename)
                Request.Write(bytes)
            End If

            Dim ReturnCode As Int32

            Dim outbuffer =
                NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                           NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_CREATE_DEVICE,
                                                           0,
                                                           Request.ToArray(),
                                                           ReturnCode)

            If ReturnCode <> 0 Then
                Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
            End If

            Using Response As New BinaryReader(New MemoryStream(outbuffer))

                DeviceNumber = Response.ReadUInt32()
                DiskSize = Response.ReadInt64()
                BytesPerSector = Response.ReadUInt32()
                ReservedField = Response.ReadBytes(4)
                ImageOffset = Response.ReadInt64()
                Flags = CType(Response.ReadUInt32(), DeviceFlags)

            End Using

            While Not GetDeviceList().Contains(DeviceNumber)
                Trace.WriteLine($"Waiting for new device {DeviceNumber:X6} to be registered by driver...")
                Thread.Sleep(2500)
            End While

            Dim DiskDevice As DiskDevice

            Dim waittime = TimeSpan.FromMilliseconds(500)
            Do

                Thread.Sleep(waittime)

                Try
                    DiskDevice = OpenDevice(DeviceNumber, FileAccess.Read)

                Catch ex As DriveNotFoundException
                    Trace.WriteLine($"Error opening device: {ex.JoinMessages()}")
                    waittime += TimeSpan.FromMilliseconds(500)

                    Trace.WriteLine("Not ready, rescanning SCSI adapter...")

                    RescanBus()

                    Continue Do

                End Try

                Using DiskDevice

                    If DiskDevice.DiskSize = 0 Then

                        '' Wait at most 20 x 500 msec for device to get initialized by driver
                        For i = 1 To 20

                            Thread.Sleep(500 * i)

                            If DiskDevice.DiskSize <> 0 Then
                                Exit For
                            End If

                            Trace.WriteLine("Updating disk properties...")
                            DiskDevice.UpdateProperties()

                        Next

                    End If

                    If Flags.HasFlag(DeviceFlags.WriteOverlay) AndAlso
                        Not String.IsNullOrWhiteSpace(WriteOverlayFilename) Then

                        Dim status = DiskDevice.WriteOverlayStatus

                        If status.HasValue Then

                            Trace.WriteLine($"Write filter attached, {status.Value.UsedDiffSize} differencing bytes used.")

                            Exit Do

                        End If

                        Trace.WriteLine("Write filter not registered. Registering and restarting device...")

                    Else

                        Exit Do

                    End If

                End Using

                Try
                    API.RegisterWriteFilter(_DeviceInstance, DeviceNumber, API.RegisterWriteFilterOperation.Register)

                Catch ex As Exception
                    RemoveDevice(DeviceNumber)
                    Throw New Exception("Failed to register write filter driver", ex)

                End Try

            Loop

        Finally

            If write_filter_added IsNot Nothing Then

                NativeFileIO.RemoveFilter(NativeFileIO.NativeConstants.DiskDriveGuid, "aimwrfltr")

                Trace.WriteLine($"ScsiAdapter.CreateDevice: Thread {Thread.CurrentThread.ManagedThreadId} leaving global critical section")

                write_filter_added.Dispose()

            End If

        End Try

        Trace.WriteLine("CreateDevice done.")

    End Sub


    ''' <summary>
    ''' Removes an existing virtual disk from adapter by first taking the disk offline so that any
    ''' mounted file systems are safely dismounted.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number to remove. Note that AutoDeviceNumber constant passed
    ''' in this parameter causes all present virtual disks to be removed from this adapter.</param>
    Public Sub RemoveDeviceSafe(DeviceNumber As UInt32)

        If DeviceNumber = AutoDeviceNumber Then

            RemoveAllDevicesSafe()

            Return

        End If

        Dim volumes As IEnumerable(Of String) = Nothing

        Using disk = OpenDevice(DeviceNumber, FileAccess.ReadWrite)

            If disk.IsDiskWritable Then

                volumes = disk.EnumerateDiskVolumes()

            End If

        End Using

        If volumes IsNot Nothing Then

            For Each volname In volumes.Select(Function(v) v.TrimEnd("\"c))
                Trace.WriteLine($"Dismounting volume: {volname}")

                Using vol = NativeFileIO.OpenFileHandle(volname, FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open, FileOptions.None)
                    If NativeFileIO.IsDiskWritable(vol) Then

                        Try
                            NativeFileIO.FlushBuffers(vol)

                        Catch ex As Exception
                            Trace.WriteLine($"Failed flushing buffers for volume {volname}: {ex.JoinMessages()}")

                        End Try

                        'NativeFileIO.Win32Try(NativeFileIO.DismountVolumeFilesystem(vol, Force:=False))

                        NativeFileIO.SetVolumeOffline(vol, offline:=True)
                    End If
                End Using
            Next

        End If

        RemoveDevice(DeviceNumber)

    End Sub

    ''' <summary>
    ''' Removes all virtual disks on current adapter by first taking the disks offline so that any
    ''' mounted file systems are safely dismounted.
    ''' </summary>
    Public Sub RemoveAllDevicesSafe()

        Parallel.ForEach(GetDeviceList(), AddressOf RemoveDeviceSafe)

    End Sub

    ''' <summary>
    ''' Removes an existing virtual disk from adapter.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number to remove. Note that AutoDeviceNumber constant passed
    ''' in this parameter causes all present virtual disks to be removed from this adapter.</param>
    Public Sub RemoveDevice(DeviceNumber As UInt32)

        Dim ReturnCode As Int32

        NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                      NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_REMOVE_DEVICE,
                                                      0,
                                                      BitConverter.GetBytes(DeviceNumber),
                                                      ReturnCode)

        If ReturnCode = NativeFileIO.NativeConstants.STATUS_OBJECT_NAME_NOT_FOUND Then ' Device already removed
            Return
        ElseIf ReturnCode <> 0 Then
            Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
        End If

    End Sub

    ''' <summary>
    ''' Removes all virtual disks on current adapter.
    ''' </summary>
    Public Sub RemoveAllDevices()

        RemoveDevice(AutoDeviceNumber)

    End Sub

    ''' <summary>
    ''' Retrieves properties for an existing virtual disk.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
    ''' <param name="DiskSize">Size of virtual disk.</param>
    ''' <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
    ''' <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    ''' Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    ''' or Windows filesystem drivers.</param>
    ''' <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    ''' <param name="Filename">Name of disk image file holding storage for file type virtual disk or used to create a
    ''' virtual memory type virtual disk.</param>
    Public Sub QueryDevice(DeviceNumber As UInt32,
                           <Out> ByRef DiskSize As Int64,
                           <Out> ByRef BytesPerSector As UInt32,
                           <Out> ByRef ImageOffset As Int64,
                           <Out> ByRef Flags As DeviceFlags,
                           <Out> ByRef Filename As String)

        QueryDevice(DeviceNumber, DiskSize, BytesPerSector, ImageOffset, Flags, Filename, WriteOverlayImagefile:=Nothing)

    End Sub

    ''' <summary>
    ''' Retrieves properties for an existing virtual disk.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
    ''' <param name="DiskSize">Size of virtual disk.</param>
    ''' <param name="BytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
    ''' <param name="ImageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    ''' Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    ''' or Windows filesystem drivers.</param>
    ''' <param name="Flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    ''' <param name="Filename">Name of disk image file holding storage for file type virtual disk or used to create a
    ''' virtual memory type virtual disk.</param>
    ''' <param name="WriteOverlayImagefile">Path to differencing file used in write-temporary mode.</param>
    Public Sub QueryDevice(DeviceNumber As UInt32,
                           <Out> ByRef DiskSize As Int64,
                           <Out> ByRef BytesPerSector As UInt32,
                           <Out> ByRef ImageOffset As Int64,
                           <Out> ByRef Flags As DeviceFlags,
                           <Out> ByRef Filename As String,
                           <Out> ByRef WriteOverlayImagefile As String)

        Dim Request As New BufferedBinaryWriter
        Request.Write(DeviceNumber)
        Request.Write(0L)
        Request.Write(0UI)
        Request.Write(0L)
        Request.Write(0UI)
        Request.Write(65535US)
        Request.Write(New Byte(0 To 65534) {})

        Dim ReturnCode As Int32

        Dim buffer = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                                   NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_DEVICE,
                                                                   0,
                                                                   Request.ToArray(),
                                                                   ReturnCode)

        '' STATUS_OBJECT_NAME_NOT_FOUND. Possible "zombie" device, just return empty data.
        If ReturnCode = &HC0000034I Then
            Return
        ElseIf ReturnCode <> 0 Then
            Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
        End If

        Using Response As New BinaryReader(New MemoryStream(buffer))

            DeviceNumber = Response.ReadUInt32()
            DiskSize = Response.ReadInt64()
            BytesPerSector = Response.ReadUInt32
            Dim ReservedField = Response.ReadBytes(4)
            ImageOffset = Response.ReadInt64()
            Flags = CType(Response.ReadUInt32(), DeviceFlags)
            Dim FilenameLength = Response.ReadUInt16()
            If FilenameLength = 0 Then
                Filename = Nothing
            Else
                Filename = Encoding.Unicode.GetString(Response.ReadBytes(FilenameLength))
            End If
            If Flags.HasFlag(DeviceFlags.WriteOverlay) Then
                Dim WriteOverlayImagefileLength = BitConverter.ToUInt16(ReservedField, 0)
                WriteOverlayImagefile = Encoding.Unicode.GetString(Response.ReadBytes(WriteOverlayImagefileLength))
            End If

        End Using

    End Sub

    ''' <summary>
    ''' Retrieves properties for an existing virtual disk.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
    Public Function QueryDevice(DeviceNumber As UInt32) As DeviceProperties

        Return New DeviceProperties(Me, DeviceNumber)

    End Function

    ''' <summary>
    ''' Modifies properties for an existing virtual disk.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number of virtual disk to modify properties for.</param>
    ''' <param name="FlagsToChange">Flags for which to change values for.</param>
    ''' <param name="FlagValues">New flag values.</param>
    Public Sub ChangeFlags(DeviceNumber As UInt32,
                           FlagsToChange As DeviceFlags,
                           FlagValues As DeviceFlags)

        Dim Request As New BufferedBinaryWriter
        Request.Write(DeviceNumber)
        Request.Write(CUInt(FlagsToChange))
        Request.Write(CUInt(FlagValues))

        Dim ReturnCode As Int32

        NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                   NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_SET_DEVICE_FLAGS,
                                                   0,
                                                   Request.ToArray(),
                                                   ReturnCode)

        If ReturnCode <> 0 Then
            Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
        End If

    End Sub

    ''' <summary>
    ''' Extends size of an existing virtual disk.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number of virtual disk to modify.</param>
    ''' <param name="ExtendSize">Number of bytes to extend.</param>
    Public Sub ExtendSize(DeviceNumber As UInt32,
                          ExtendSize As Int64)

        Dim Request As New BufferedBinaryWriter
        Request.Write(DeviceNumber)
        Request.Write(ExtendSize)

        Dim ReturnCode As Int32

        NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                    NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_EXTEND_DEVICE,
                                                    0,
                                                    Request.ToArray(),
                                                    ReturnCode)

        If ReturnCode <> 0 Then
            Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
        End If

    End Sub

    ''' <summary>
    ''' Checks if version of running Arsenal Image Mounter SCSI miniport servicing this device object is compatible with this API
    ''' library. If this device object is not created by Arsenal Image Mounter SCSI miniport, an exception is thrown.
    ''' </summary>
    Public Function CheckDriverVersion() As Boolean

        Return CheckDriverVersion(SafeFileHandle)

    End Function

    ''' <summary>
    ''' Checks if version of running Arsenal Image Mounter SCSI miniport servicing this device object is compatible with this API
    ''' library. If this device object is not created by Arsenal Image Mounter SCSI miniport, an exception is thrown.
    ''' </summary>
    Public Shared Function CheckDriverVersion(SafeFileHandle As SafeFileHandle) As Boolean

        Dim ReturnCode As Int32
        NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                    NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_VERSION,
                                                    0,
                                                    Nothing,
                                                    ReturnCode)

        If ReturnCode = CompatibleDriverVersion Then
            Return True
        End If

        Trace.WriteLine($"Library version: {CompatibleDriverVersion:X4}")
        Trace.WriteLine($"Driver version: {ReturnCode:X4}")

        Return False

    End Function

    ''' <summary>
    ''' Retrieves the sub version of the driver. This is not the same as the API compatibility version checked for by
    ''' CheckDriverVersion method. The version record returned by this GetDriverSubVersion method can be used to find
    ''' out whether the latest version of the driver is loaded, for example to show a dialog box asking user whether to
    ''' upgrade the driver. If driver does not support this version query, this method returns Nothing/null.
    ''' </summary>
    Public Function GetDriverSubVersion() As Version

        Dim ReturnCode As Int32
        Dim Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                                  NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_VERSION,
                                                                  0,
                                                                  New Byte(0 To 3) {},
                                                                  ReturnCode)

        Trace.WriteLine($"Library version: {CompatibleDriverVersion:X4}")
        Trace.WriteLine($"Driver version: {ReturnCode:X4}")

        If ReturnCode <> CompatibleDriverVersion Then
            Return Nothing
        End If

        Try
            Dim build = Response(0)
            Dim low = Response(1)
            Dim minor = Response(2)
            Dim major = Response(3)

            Return New Version(major, minor, low, build)

        Catch ex As IOException
            Return Nothing

        End Try

    End Function

    Public Function RescanScsiAdapter() As Boolean

        Return API.RescanScsiAdapter(_DeviceInstance)

    End Function

    ''' <summary>
    ''' Issues a SCSI bus rescan to find newly attached devices and remove missing ones.
    ''' </summary>
    Public Sub RescanBus()

        Try
            NativeFileIO.DeviceIoControl(SafeFileHandle, NativeFileIO.NativeConstants.IOCTL_SCSI_RESCAN_BUS, Nothing, 0)

        Catch ex As Exception
            Trace.WriteLine($"IOCTL_SCSI_RESCAN_BUS failed: {ex.JoinMessages()}")
            API.RescanScsiAdapter(_DeviceInstance)

        End Try

    End Sub

    ''' <summary>
    ''' Re-enumerates partitions on all disk drives currently connected to this adapter. No
    ''' exceptions are thrown on error, but any exceptions from underlying API calls are logged
    ''' to trace log.
    ''' </summary>
    Public Sub UpdateDiskProperties()

        For Each device In GetDeviceList()

            UpdateDiskProperties(device)

        Next

    End Sub

    ''' <summary>
    ''' Re-enumerates partitions on specified disk currently connected to this adapter. No
    ''' exceptions are thrown on error, but any exceptions from underlying API calls are logged
    ''' to trace log.
    ''' </summary>
    Public Function UpdateDiskProperties(DeviceNumber As UInteger) As Boolean

        Try
            Using disk = OpenDevice(DeviceNumber, 0)

                If Not NativeFileIO.UpdateDiskProperties(disk.SafeFileHandle, throwOnFailure:=False) Then

                    Trace.WriteLine($"Error updating disk properties for device {DeviceNumber:X6}: {New Win32Exception().Message}")

                    Return False

                End If

            End Using

            Return True

        Catch ex As Exception
            Trace.WriteLine($"Error updating disk properties for device {DeviceNumber:X6}: {ex.JoinMessages()}")

            Return False

        End Try

    End Function

    ''' <summary>
    ''' Opens a DiskDevice object for specified device number. Device numbers are created when
    ''' a new virtual disk is created and returned in a reference parameter to CreateDevice
    ''' method.
    ''' </summary>
    Public Function OpenDevice(DeviceNumber As UInteger, AccessMode As FileAccess) As DiskDevice

        Try
            Dim device_name = GetDeviceName(DeviceNumber)

            If device_name Is Nothing Then
                Throw New DriveNotFoundException($"No drive found for device number {DeviceNumber:X6}")
            End If

            Return New DiskDevice($"\\?\{device_name}", AccessMode)

        Catch ex As Exception
            Throw New DriveNotFoundException($"Device {DeviceNumber:X6} is not ready", ex)

        End Try

    End Function

    ''' <summary>
    ''' Opens a DiskDevice object for specified device number. Device numbers are created when
    ''' a new virtual disk is created and returned in a reference parameter to CreateDevice
    ''' method. This overload requests a DiskDevice object without read or write access, that
    ''' can only be used to query metadata such as size, geometry, SCSI address etc.
    ''' </summary>
    Public Function OpenDevice(DeviceNumber As UInteger) As DiskDevice

        Try
            Dim device_name = GetDeviceName(DeviceNumber)

            If device_name Is Nothing Then
                Throw New DriveNotFoundException($"No drive found for device number {DeviceNumber:X6}")
            End If

            Return New DiskDevice($"\\?\{device_name}")

        Catch ex As Exception
            Throw New DriveNotFoundException($"Device {DeviceNumber:X6} is not ready", ex)

        End Try

    End Function

    ''' <summary>
    ''' Returns a PhysicalDrive or CdRom device name for specified device number. Device numbers
    ''' are created when a new virtual disk is created and returned in a reference parameter to
    ''' CreateDevice method.
    ''' </summary>
    Public Function GetDeviceName(DeviceNumber As UInteger) As String

        Try
            Dim raw_device = GetRawDeviceName(DeviceNumber)

            If raw_device Is Nothing Then
                Return Nothing
            End If

            Return NativeFileIO.GetPhysicalDriveNameForNtDevice(raw_device)

        Catch ex As Exception
            Trace.WriteLine($"Error getting device name for device number {DeviceNumber}: {ex.JoinMessages()}")
            Return Nothing

        End Try

    End Function

    ''' <summary>
    ''' Returns an NT device path to the physical device object that SCSI port driver has created for a mounted device.
    ''' This device path can be used even if there is no functional driver attached to the device stack.
    ''' </summary>
    Public Function GetRawDeviceName(DeviceNumber As UInteger) As String

        Return API.EnumeratePhysicalDeviceObjectPaths(_DeviceInstance, DeviceNumber).FirstOrDefault()

    End Function

    ''' <summary>
    ''' Returns a PnP registry property for the device object that SCSI port driver has created for a mounted device.
    ''' </summary>
    Public Function GetPnPDeviceName(DeviceNumber As UInteger, prop As NativeFileIO.CmDevNodeRegistryProperty) As IEnumerable(Of String)

        Return API.EnumerateDeviceProperty(_DeviceInstance, DeviceNumber, prop)

    End Function

End Class

