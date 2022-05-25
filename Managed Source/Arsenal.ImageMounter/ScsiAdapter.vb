''''' ScsiAdapter.vb
''''' Class for controlling Arsenal Image Mounter Devices.
''''' 
''''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports System.Buffers
Imports System.ComponentModel
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Runtime.Versioning
Imports System.Text
Imports System.Threading
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32.SafeHandles

#Disable Warning IDE0079 ' Remove unnecessary suppression
#Disable Warning CA1840 ' Use 'Environment.CurrentManagedThreadId'

''' <summary>
''' Represents Arsenal Image Mounter objects.
''' </summary>
<SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
Public Class ScsiAdapter
    Inherits DeviceObject

    Public Const CompatibleDriverVersion As UInteger = &H101

    Public Const AutoDeviceNumber As UInteger = &HFFFFFF

    Public ReadOnly Property DeviceInstanceName As ReadOnlyMemory(Of Char)

    Public ReadOnly Property DeviceInstance As UInteger

    Private Shared Function OpenAdapterHandle(ntdevice As String, devInst As UInteger) As SafeFileHandle

        Dim handle As SafeFileHandle
        Try
            handle = NativeFileIO.NtCreateFile(ntdevice, 0,
                                                 FileAccess.ReadWrite,
                                                 FileShare.ReadWrite,
                                                 NtCreateDisposition.Open,
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
                (ex.NativeErrorCode = NativeConstants.ERROR_INVALID_FUNCTION) OrElse
                (ex.NativeErrorCode = NativeConstants.ERROR_IO_DEVICE)

                '' In case of SCSIPORT (Win XP) miniport, there is always a risk
                '' that we lose contact with IOCTL_SCSI_MINIPORT after device adds
                '' and removes. Therefore, in case we know that we have a handle to
                '' the SCSI adapter and it fails IOCTL_SCSI_MINIPORT requests, just
                '' issue a bus re-enumeration to find the dummy IOCTL device, which
                '' will make SCSIPORT let control requests through again.
                If Not API.HasStorPort Then
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

        Public ReadOnly Property DevInstName As ReadOnlyMemory(Of Char)

        Public ReadOnly Property DevInst As UInteger

        Public ReadOnly Property SafeHandle As SafeFileHandle

        Public Sub New(devInstName As ReadOnlyMemory(Of Char), devInst As UInteger, safeHhandle As SafeFileHandle)
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
    ''' Opens first found Arsenal Image Mounter adapter.
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
        MyBase.New($"\\?\Scsi{ScsiPortNumber}:".AsMemory(), FileAccess.ReadWrite)

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

        Dim ReturnCode As Integer

        Dim buffer = ArrayPool(Of Byte).Shared.Rent(65536)
        Try
            Dim Response =
            NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                       NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_ADAPTER,
                                                       0,
                                                       buffer,
                                                       ReturnCode)

            If ReturnCode <> 0 Then
                Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
            End If

            Dim NumberOfDevices = BitConverter.ToInt32(Response, 0)

            Dim array(0 To NumberOfDevices - 1) As UInteger

            System.Buffer.BlockCopy(Response, 4, array, 0, NumberOfDevices * 4)

            Return array

        Finally
            ArrayPool(Of Byte).Shared.Return(buffer)

        End Try

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
    Public Sub CreateDevice(DiskSize As Long,
                            BytesPerSector As UInteger,
                            ImageOffset As Long,
                            Flags As DeviceFlags,
                            Filename As ReadOnlyMemory(Of Char),
                            NativePath As Boolean,
                            ByRef DeviceNumber As UInteger)

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
    Public Sub CreateDevice(DiskSize As Long,
                            BytesPerSector As UInteger,
                            ImageOffset As Long,
                            Flags As DeviceFlags,
                            Filename As ReadOnlyMemory(Of Char),
                            NativePath As Boolean,
                            WriteOverlayFilename As ReadOnlyMemory(Of Char),
                            WriteOverlayNativePath As Boolean,
                            ByRef DeviceNumber As UInteger)

        '' Temporary variable for passing through lambda function
        Dim devnr = DeviceNumber

        '' Both UInt32.MaxValue and AutoDeviceNumber can be used
        '' for auto-selecting device number, but only AutoDeviceNumber
        '' is accepted by driver.
        If devnr = UInteger.MaxValue Then
            devnr = AutoDeviceNumber
        End If

        '' Translate Win32 path to native NT path that kernel understands
        If (Not Filename.Span.IsWhiteSpace()) AndAlso (Not NativePath) Then
            Select Case Flags.GetDiskType()
                Case DeviceFlags.TypeProxy
                    Select Case Flags.GetProxyType()

                        Case DeviceFlags.ProxyTypeSharedMemory
                            Filename = $"\BaseNamedObjects\Global\{Filename}".AsMemory()

                        Case DeviceFlags.ProxyTypeComm, DeviceFlags.ProxyTypeTCP

                        Case Else
                            Filename = NativeFileIO.GetNtPath(Filename).AsMemory()

                    End Select

                Case Else
                    Filename = NativeFileIO.GetNtPath(Filename).AsMemory()

            End Select
        End If

        '' Show what we got
        Trace.WriteLine($"ScsiAdapter.CreateDevice: Native filename='{Filename}'")

        Dim write_filter_added As GlobalCriticalMutex = Nothing

        Try

            If Not WriteOverlayFilename.Span.IsWhiteSpace() Then

                If (Not WriteOverlayNativePath) Then
                    WriteOverlayFilename = NativeFileIO.GetNtPath(WriteOverlayFilename).AsMemory()
                End If

                Trace.WriteLine($"ScsiAdapter.CreateDevice: Thread {Thread.CurrentThread.ManagedThreadId} entering global critical section")

                write_filter_added = New GlobalCriticalMutex()

                NativeFileIO.AddFilter(NativeConstants.DiskDriveGuid, "aimwrfltr", addfirst:=True)

            End If

            '' Show what we got
            Trace.WriteLine($"ScsiAdapter.CreateDevice: Native write overlay filename='{WriteOverlayFilename}'")

            Dim deviceConfig As New IMSCSI_DEVICE_CONFIGURATION(
                DeviceNumber:=devnr,
                DiskSize:=DiskSize,
                BytesPerSector:=BytesPerSector,
                ImageOffset:=ImageOffset,
                Flags:=CInt(Flags),
                FileNameLength:=CUShort(If(Filename.Span.IsWhiteSpace(), 0, MemoryMarshal.AsBytes(Filename.Span).Length)),
                WriteOverlayFileNameLength:=CUShort(If(WriteOverlayFilename.Span.IsWhiteSpace(), 0, MemoryMarshal.AsBytes(WriteOverlayFilename.Span).Length)))

            Dim Request = ArrayPool(Of Byte).Shared.Rent(PinnedBuffer(Of IMSCSI_DEVICE_CONFIGURATION).TypeSize + deviceConfig.FileNameLength + deviceConfig.WriteOverlayFileNameLength)

            MemoryMarshal.Write(Request, deviceConfig)

            If Not Filename.Span.IsWhiteSpace() Then
                MemoryMarshal.AsBytes(Filename.Span).CopyTo(Request.AsSpan(PinnedBuffer(Of IMSCSI_DEVICE_CONFIGURATION).TypeSize))
            End If

            If Not WriteOverlayFilename.Span.IsWhiteSpace() Then
                MemoryMarshal.AsBytes(WriteOverlayFilename.Span).CopyTo(Request.AsSpan(PinnedBuffer(Of IMSCSI_DEVICE_CONFIGURATION).TypeSize + deviceConfig.FileNameLength))
            End If

            Dim ReturnCode As Integer

            Dim Response =
                NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                           NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_CREATE_DEVICE,
                                                           0,
                                                           Request,
                                                           ReturnCode)

            If ReturnCode <> 0 Then
                Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
            End If

            deviceConfig = MemoryMarshal.Read(Of IMSCSI_DEVICE_CONFIGURATION)(Response)

            DeviceNumber = deviceConfig.DeviceNumber
            DiskSize = deviceConfig.DiskSize
            BytesPerSector = deviceConfig.BytesPerSector
            ImageOffset = deviceConfig.ImageOffset
            Flags = CType(deviceConfig.Flags, DeviceFlags)

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
                        Not WriteOverlayFilename.Span.IsWhiteSpace() Then

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

                NativeFileIO.RemoveFilter(NativeConstants.DiskDriveGuid, "aimwrfltr")

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
    Public Sub RemoveDeviceSafe(DeviceNumber As UInteger)

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

            For Each volname In volumes.Select(Function(v) v.AsMemory().TrimEnd("\"c))
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
    Public Sub RemoveDevice(DeviceNumber As UInteger)

        Dim ReturnCode As Integer

        NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                      NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_REMOVE_DEVICE,
                                                      0,
                                                      BitConverter.GetBytes(DeviceNumber),
                                                      ReturnCode)

        If ReturnCode = NativeConstants.STATUS_OBJECT_NAME_NOT_FOUND Then ' Device already removed
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
    Public Sub QueryDevice(DeviceNumber As UInteger,
                           <Out> ByRef DiskSize As Long,
                           <Out> ByRef BytesPerSector As UInteger,
                           <Out> ByRef ImageOffset As Long,
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
    Public Sub QueryDevice(DeviceNumber As UInteger,
                           <Out> ByRef DiskSize As Long,
                           <Out> ByRef BytesPerSector As UInteger,
                           <Out> ByRef ImageOffset As Long,
                           <Out> ByRef Flags As DeviceFlags,
                           <Out> ByRef Filename As String,
                           <Out> ByRef WriteOverlayImagefile As String)

        Dim Request = ArrayPool(Of Byte).Shared.Rent(PinnedBuffer(Of IMSCSI_DEVICE_CONFIGURATION).TypeSize + 65535)
        Try
            Dim deviceConfig As New IMSCSI_DEVICE_CONFIGURATION(
                deviceNumber:=DeviceNumber,
                fileNameLength:=65535)

            MemoryMarshal.Write(Request, deviceConfig)

            Dim ReturnCode As Integer

            Dim Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                                      NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_DEVICE,
                                                                      0,
                                                                      Request,
                                                                      ReturnCode)

            '' STATUS_OBJECT_NAME_NOT_FOUND. Possible "zombie" device, just return empty data.
            If ReturnCode = &HC0000034I Then
                Return
            ElseIf ReturnCode <> 0 Then
                Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
            End If

            deviceConfig = MemoryMarshal.Read(Of IMSCSI_DEVICE_CONFIGURATION)(Response)
            DeviceNumber = deviceConfig.DeviceNumber
            DiskSize = deviceConfig.DiskSize
            BytesPerSector = deviceConfig.BytesPerSector
            ImageOffset = deviceConfig.ImageOffset
            Flags = CType(deviceConfig.Flags, DeviceFlags)
            If deviceConfig.FileNameLength = 0 Then
                Filename = Nothing
            Else
                Filename = Encoding.Unicode.GetString(Response,
                                                      PinnedBuffer(Of IMSCSI_DEVICE_CONFIGURATION).TypeSize,
                                                      deviceConfig.FileNameLength)
            End If

            If Flags.HasFlag(DeviceFlags.WriteOverlay) Then
                Dim WriteOverlayImagefileLength = deviceConfig.WriteOverlayFileNameLength
                WriteOverlayImagefile = Encoding.Unicode.GetString(Response,
                                                                   PinnedBuffer(Of IMSCSI_DEVICE_CONFIGURATION).TypeSize + deviceConfig.FileNameLength,
                                                                   WriteOverlayImagefileLength)
            End If

        Finally
            ArrayPool(Of Byte).Shared.Return(Request)

        End Try

    End Sub

    ''' <summary>
    ''' Retrieves properties for an existing virtual disk.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number of virtual disk to retrieve properties for.</param>
    Public Function QueryDevice(DeviceNumber As UInteger) As DeviceProperties

        Return New DeviceProperties(Me, DeviceNumber)

    End Function

    ''' <summary>
    ''' Modifies properties for an existing virtual disk.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number of virtual disk to modify properties for.</param>
    ''' <param name="FlagsToChange">Flags for which to change values for.</param>
    ''' <param name="FlagValues">New flag values.</param>
    Public Sub ChangeFlags(DeviceNumber As UInteger,
                           FlagsToChange As DeviceFlags,
                           FlagValues As DeviceFlags)

        Dim Request = ArrayPool(Of Byte).Shared.Rent(PinnedBuffer(Of IMSCSI_SET_DEVICE_FLAGS).TypeSize)
        Try
            Dim changeFlags As New IMSCSI_SET_DEVICE_FLAGS(DeviceNumber, FlagsToChange, FlagValues)

            MemoryMarshal.Write(Request, changeFlags)

            Dim ReturnCode As Integer

            NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                       NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_SET_DEVICE_FLAGS,
                                                       0,
                                                       Request,
                                                       ReturnCode)

            If ReturnCode <> 0 Then
                Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
            End If

        Finally
            ArrayPool(Of Byte).Shared.Return(Request)

        End Try

    End Sub

    ''' <summary>
    ''' Extends size of an existing virtual disk.
    ''' </summary>
    ''' <param name="DeviceNumber">Device number of virtual disk to modify.</param>
    ''' <param name="ExtendSize">Number of bytes to extend.</param>
    Public Sub ExtendSize(DeviceNumber As UInteger,
                          ExtendSize As Long)

        Dim Request = ArrayPool(Of Byte).Shared.Rent(PinnedBuffer(Of IMSCSI_EXTEND_SIZE).TypeSize)
        Try
            Dim changeFlags As New IMSCSI_EXTEND_SIZE(DeviceNumber, ExtendSize)

            MemoryMarshal.Write(Request, changeFlags)

            Dim ReturnCode As Integer

            NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                       NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_SET_DEVICE_FLAGS,
                                                       0,
                                                       Request,
                                                       ReturnCode)

            If ReturnCode <> 0 Then
                Throw NativeFileIO.GetExceptionForNtStatus(ReturnCode)
            End If

        Finally
            ArrayPool(Of Byte).Shared.Return(Request)

        End Try

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

        Dim ReturnCode As Integer
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

        Dim buffer = ArrayPool(Of Byte).Shared.Rent(4)
        Try
            Dim ReturnCode As Integer
            Dim Response = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle,
                                                                      NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_VERSION,
                                                                      0,
                                                                      buffer,
                                                                      ReturnCode)

            Trace.WriteLine($"Library version: {CompatibleDriverVersion:X4}")
            Trace.WriteLine($"Driver version: {ReturnCode:X4}")

            If ReturnCode <> CompatibleDriverVersion Then
                Return Nothing
            End If

            Dim build = Response(0)
            Dim low = Response(1)
            Dim minor = Response(2)
            Dim major = Response(3)

            Return New Version(major, minor, low, build)

        Catch ex As IOException
            Return Nothing

        Finally
            ArrayPool(Of Byte).Shared.Return(buffer)

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
            NativeFileIO.DeviceIoControl(SafeFileHandle, NativeConstants.IOCTL_SCSI_RESCAN_BUS, Nothing, 0)

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

            Return New DiskDevice($"\\?\{device_name}".AsMemory(), AccessMode)

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
    Public Function GetPnPDeviceName(DeviceNumber As UInteger, prop As CmDevNodeRegistryProperty) As IEnumerable(Of String)

        Return API.EnumerateDeviceProperty(_DeviceInstance, DeviceNumber, prop)

    End Function

End Class

''' <summary>
''' Object storing properties for a virtual disk device. Returned by
''' QueryDevice() method.
''' </summary>
Public NotInheritable Class DeviceProperties

    <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
    Public Sub New(adapter As ScsiAdapter, device_number As UInteger)

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
    Public ReadOnly Property DeviceNumber As UInteger

    ''' <summary>Size of virtual disk.</summary>
    Public ReadOnly Property DiskSize As Long

    ''' <summary>Number of bytes per sector for virtual disk geometry.</summary>
    Public ReadOnly Property BytesPerSector As UInteger

    ''' <summary>A skip offset if virtual disk data does not begin immediately at start of disk image file.
    ''' Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter
    ''' or Windows filesystem drivers.</summary>
    Public ReadOnly Property ImageOffset As Long

    ''' <summary>Flags specifying properties for virtual disk. See comments for each flag value.</summary>
    Public ReadOnly Property Flags As DeviceFlags

    ''' <summary>Name of disk image file holding storage for file type virtual disk or used to create a
    ''' virtual memory type virtual disk.</summary>
    Public Property Filename As String

    ''' <summary>Path to differencing file used in write-temporary mode.</summary>
    Public ReadOnly Property WriteOverlayImageFile As String

End Class

