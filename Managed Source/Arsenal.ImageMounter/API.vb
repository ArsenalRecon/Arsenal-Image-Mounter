''''' API.vb
''''' API for manipulating flag values, issuing SCSI bus rescans and similar
''''' tasks.
''''' 
''''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
'''''

Imports System.ComponentModel
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Runtime.Versioning
Imports System.Threading
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32
Imports Microsoft.Win32.SafeHandles

''' <summary>
''' API for manipulating flag values, issuing SCSI bus rescans, manage write filter driver and similar tasks.
''' </summary>
<ComVisible(False)>
<SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
Public NotInheritable Class API

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property OSVersion As Version = NativeFileIO.GetOSVersion().Version

    Public Shared ReadOnly Property Kernel As String = GetKernelName()

    Public Shared ReadOnly Property HasStorPort As Boolean

    Private Shared Function GetKernelName() As String

        If _OSVersion >= New Version(10, 0) Then
            _Kernel = "Win10"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(6, 3) Then
            _Kernel = "Win8.1"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(6, 2) Then
            _Kernel = "Win8"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(6, 1) Then
            _Kernel = "Win7"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(6, 0) Then
            _Kernel = "WinLH"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(5, 2) Then
            _Kernel = "WinNET"
            _HasStorPort = True
        ElseIf _OSVersion >= New Version(5, 1) Then
            _Kernel = "WinXP"
            _HasStorPort = False
        ElseIf _OSVersion >= New Version(5, 0) Then
            _Kernel = "Win2K"
            _HasStorPort = False
        Else
            Throw New NotSupportedException($"Unsupported Windows version ('{_OSVersion}')")
        End If

        Return _Kernel

    End Function

    ''' <summary>
    ''' Builds a list of device paths for active Arsenal Image Mounter
    ''' objects.
    ''' </summary>
    Public Shared Iterator Function EnumerateAdapterDevicePaths(HwndParent As IntPtr) As IEnumerable(Of String)

        Dim devinstances As IEnumerable(Of ReadOnlyMemory(Of Char)) = Nothing
        Dim status = NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt".AsMemory(), devinstances)

        If status <> 0 OrElse devinstances Is Nothing Then

            Trace.WriteLine($"No devices found serviced by 'phdskmnt'. status=0x{status:X}")
            Return
        End If

        For Each devinstname In devinstances

            Using DevInfoSet = NativeFileIO.UnsafeNativeMethods.SetupDiGetClassDevs(NativeConstants.SerenumBusEnumeratorGuid,
                                                                                     devinstname.MakeNullTerminated(),
                                                                                     HwndParent,
                                                                                     NativeConstants.DIGCF_DEVICEINTERFACE Or NativeConstants.DIGCF_PRESENT)

                If DevInfoSet.IsInvalid Then
                    Throw New Win32Exception
                End If

                Dim i = 0UI
                Do
                    Dim DeviceInterfaceData = SP_DEVICE_INTERFACE_DATA.GetNew()

                    If NativeFileIO.UnsafeNativeMethods.SetupDiEnumDeviceInterfaces(DevInfoSet, IntPtr.Zero, NativeConstants.SerenumBusEnumeratorGuid, i, DeviceInterfaceData) = False Then
                        Exit Do
                    End If

                    Dim DeviceInterfaceDetailData As New SP_DEVICE_INTERFACE_DETAIL_DATA

                    If NativeFileIO.UnsafeNativeMethods.SetupDiGetDeviceInterfaceDetail(DevInfoSet, DeviceInterfaceData, DeviceInterfaceDetailData, CUInt(Marshal.SizeOf(DeviceInterfaceData)), 0, IntPtr.Zero) = True Then
                        Yield DeviceInterfaceDetailData.DevicePath
                    End If

                    i += 1UI
                Loop

            End Using
        Next

    End Function

    ''' <summary>
    ''' Returns a value indicating whether Arsenal Image Mounter driver is
    ''' installed and running.
    ''' </summary>
    Public Shared ReadOnly Property AdapterDevicePresent As Boolean
        Get
            Dim devInsts = EnumerateAdapterDeviceInstanceNames()
            If devInsts Is Nothing Then
                Return False
            Else
                Return True
            End If
        End Get
    End Property

    ''' <summary>
    ''' Builds a list of setup device ids for active Arsenal Image Mounter
    ''' objects. Device ids are used in calls to plug-and-play setup functions.
    ''' </summary>
    Public Shared Iterator Function EnumerateAdapterDeviceInstances() As IEnumerable(Of UInteger)

        Dim devinstances = EnumerateAdapterDeviceInstanceNames()

        If devinstances Is Nothing Then
            Return
        End If

        For Each devinstname In devinstances
#If DEBUG Then
            Trace.WriteLine($"Found adapter instance '{devinstname}'")
#End If

            Dim devInst = NativeFileIO.GetDevInst(devinstname)

            If Not devInst.HasValue Then
                Continue For
            End If

            Yield devInst.Value
        Next

    End Function

    ''' <summary>
    ''' Builds a list of setup device ids for active Arsenal Image Mounter
    ''' objects. Device ids are used in calls to plug-and-play setup functions.
    ''' </summary>
    Public Shared Function EnumerateAdapterDeviceInstanceNames() As IEnumerable(Of ReadOnlyMemory(Of Char))

        Dim devinstances As IEnumerable(Of ReadOnlyMemory(Of Char)) = Nothing

        Dim status = NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt".AsMemory(), devinstances)

        If status <> 0 OrElse devinstances Is Nothing Then

            Trace.WriteLine($"No devices found serviced by 'phdskmnt'. status=0x{status:X}")
            Return Nothing
        End If

        Return devinstances

    End Function

    ''' <summary>
    ''' Issues a SCSI bus rescan on found Arsenal Image Mounter adapters. This causes Disk Management
    ''' in Windows to find newly created virtual disks and remove newly deleted ones.
    ''' </summary>
    Public Shared Function RescanScsiAdapter(devInst As UInteger) As Boolean

        Dim rc As Boolean

        Dim status = NativeFileIO.UnsafeNativeMethods.CM_Reenumerate_DevNode(devInst, 0)
        If status <> 0 Then
            Trace.WriteLine($"Re-enumeration of '{devInst}' failed: 0x{status:X}")
        Else
            Trace.WriteLine($"Re-enumeration of '{devInst}' successful.")
            rc = True
        End If

        Return rc

    End Function

    Private Const NonRemovableSuffix As String = ":$NonRemovable"

    Public Shared Function EnumeratePhysicalDeviceObjectPaths(devinstAdapter As UInteger, DeviceNumber As UInteger) As IEnumerable(Of String)

        Return _
            From devinstChild In NativeFileIO.EnumerateChildDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let address = NativeFileIO.GetScsiAddressForNtDevice(path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)
            Select path

    End Function

    Public Shared Function EnumerateDeviceProperty(devinstAdapter As UInteger, DeviceNumber As UInteger, prop As CmDevNodeRegistryProperty) As IEnumerable(Of String)

        Return _
            From devinstChild In NativeFileIO.EnumerateChildDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let address = NativeFileIO.GetScsiAddressForNtDevice(path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)
            From value In NativeFileIO.GetDeviceRegistryProperty(devinstChild, prop)
            Select value

    End Function

    Public Shared Sub UnregisterWriteOverlayImage(devInst As UInteger)
        RegisterWriteOverlayImage(devInst, OverlayImagePath:=Nothing, FakeNonRemovable:=False)
    End Sub

    Public Shared Sub RegisterWriteOverlayImage(devInst As UInteger, OverlayImagePath As ReadOnlyMemory(Of Char))
        RegisterWriteOverlayImage(devInst, OverlayImagePath, FakeNonRemovable:=False)
    End Sub

    Public Shared Sub RegisterWriteOverlayImage(devInst As UInteger, OverlayImagePath As ReadOnlyMemory(Of Char), FakeNonRemovable As Boolean)

        Dim nativepath As String

        If Not OverlayImagePath.Span.IsWhiteSpace() Then
            nativepath = NativeFileIO.GetNtPath(OverlayImagePath)
        Else
            OverlayImagePath = Nothing
            nativepath = Nothing
        End If

        Dim pdo_path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devInst)
        Dim dev_path = NativeFileIO.QueryDosDevice(NativeFileIO.GetPhysicalDriveNameForNtDevice(pdo_path)).FirstOrDefault()

        Trace.WriteLine($"Device {pdo_path} devinst {devInst}. Registering write overlay '{nativepath}', FakeNonRemovable={FakeNonRemovable}")

        Using regkey = Registry.LocalMachine.CreateSubKey("SYSTEM\CurrentControlSet\Services\aimwrfltr\Parameters")
            If nativepath Is Nothing Then
                regkey.DeleteValue(pdo_path, throwOnMissingValue:=False)
            ElseIf FakeNonRemovable Then
                regkey.SetValue(pdo_path, $"{nativepath}{NonRemovableSuffix}", RegistryValueKind.String)
            Else
                regkey.SetValue(pdo_path, nativepath, RegistryValueKind.String)
            End If
        End Using

        If nativepath Is Nothing Then
            NativeFileIO.RemoveFilter(devInst, "aimwrfltr")
        Else
            NativeFileIO.AddFilter(devInst, "aimwrfltr")
        End If

        Dim last_error = 0

        For r = 1 To 4

            NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, devInst)

            Dim statistics As New WriteFilterStatistics

            last_error = GetWriteOverlayStatus(pdo_path, statistics)

            Trace.WriteLine($"Overlay path '{nativepath}', I/O error code: {last_error}, aimwrfltr error code: 0x{statistics.LastErrorCode:X}, protection: {statistics.IsProtected}, initialized: {statistics.Initialized}")

            If nativepath Is Nothing AndAlso last_error = NativeConstants.NO_ERROR Then

                Trace.WriteLine("Filter driver not yet unloaded, retrying...")
                Thread.Sleep(300)
                Continue For

            ElseIf nativepath IsNot Nothing AndAlso (last_error = NativeConstants.ERROR_INVALID_FUNCTION OrElse
                last_error = NativeConstants.ERROR_INVALID_PARAMETER OrElse
                last_error = NativeConstants.ERROR_NOT_SUPPORTED) Then

                Trace.WriteLine("Filter driver not yet loaded, retrying...")
                Thread.Sleep(300)
                Continue For

            ElseIf (nativepath IsNot Nothing AndAlso last_error <> NativeConstants.NO_ERROR) OrElse
                (nativepath Is Nothing AndAlso last_error <> NativeConstants.ERROR_INVALID_FUNCTION AndAlso
                last_error <> NativeConstants.ERROR_INVALID_PARAMETER AndAlso
                last_error <> NativeConstants.ERROR_NOT_SUPPORTED) Then

                Throw New NotSupportedException("Error checking write filter driver status", New Win32Exception(last_error))

            ElseIf (nativepath IsNot Nothing AndAlso statistics.Initialized) OrElse
                nativepath Is Nothing Then

                Return

            End If

            Throw New IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode))

        Next

        Dim in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(pdo_path, dev_path).
            Take(10).
            Select(AddressOf NativeFileIO.FormatProcessName).
            ToArray()

        If in_use_apps.Length = 0 AndAlso last_error <> 0 Then
            Throw New NotSupportedException("Write filter driver not attached to device", New Win32Exception(last_error))
        ElseIf in_use_apps.Length = 0 Then
            Throw New NotSupportedException("Write filter driver not attached to device")
        Else
            Dim apps = String.Join(", ", in_use_apps)

            Throw New UnauthorizedAccessException($"Write filter driver cannot be attached while applications hold the disk device open.

Currently, the following application{If(in_use_apps.Length <> 1, "s", "")} hold{If(in_use_apps.Length = 1, "s", "")} the disk device open:
{apps}")
        End If

        Throw New FileNotFoundException("Error adding write overlay: Device not found.")

    End Sub

    Public Shared Sub RegisterWriteFilter(devinstAdapter As UInteger, DeviceNumber As UInteger, operation As RegisterWriteFilterOperation)

        For Each dev In
            From devinstChild In NativeFileIO.EnumerateChildDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let address = NativeFileIO.GetScsiAddressForNtDevice(path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)

            Trace.WriteLine($"Device number {DeviceNumber:X6}  found at {dev.path} devinst {dev.devinstChild}. Registering write filter driver.")

            If operation = RegisterWriteFilterOperation.Unregister Then

                If NativeFileIO.RemoveFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild)
                End If

                Return

            End If

            Dim last_error = 0

            For r = 1 To 2

                If NativeFileIO.AddFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild)
                End If

                Dim statistics As New WriteFilterStatistics

                last_error = GetWriteOverlayStatus(dev.path, statistics)

                If last_error = NativeConstants.ERROR_INVALID_FUNCTION Then
                    Trace.WriteLine("Filter driver not loaded, retrying...")
                    Thread.Sleep(200)
                    Continue For
                ElseIf last_error <> NativeConstants.NO_ERROR Then
                    Throw New NotSupportedException("Error checking write filter driver status", New Win32Exception)
                End If

                If statistics.Initialized Then
                    Return
                End If

                Throw New IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode))

            Next

            Dim in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(dev.path).Take(10).Select(AddressOf NativeFileIO.FormatProcessName).ToArray()

            If in_use_apps.Length = 0 AndAlso last_error > 0 Then
                Throw New NotSupportedException("Write filter driver not attached to device", New Win32Exception(last_error))
            ElseIf in_use_apps.Length = 0 Then
                Throw New NotSupportedException("Write filter driver not attached to device")
            Else
                Dim apps = String.Join(", ", in_use_apps)
                Throw New UnauthorizedAccessException($"Write filter driver cannot be attached while applications hold the virtual disk device open.

Currently, the following application{If(in_use_apps.Length <> 1, "s", "")} hold{If(in_use_apps.Length = 1, "s", "")} the disk device open:
{apps}")
            End If
        Next

        Throw New FileNotFoundException("Error adding write overlay: Device not found.")

    End Sub

    Public Shared Async Function RegisterWriteOverlayImageAsync(devInst As UInteger, OverlayImagePath As ReadOnlyMemory(Of Char), FakeNonRemovable As Boolean, cancel As CancellationToken) As Task

        Dim nativepath As String

        If Not OverlayImagePath.Span.IsWhiteSpace() Then
            nativepath = NativeFileIO.GetNtPath(OverlayImagePath)
        Else
            OverlayImagePath = Nothing
            nativepath = Nothing
        End If

        Dim pdo_path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devInst)
        Dim dev_path = NativeFileIO.QueryDosDevice(NativeFileIO.GetPhysicalDriveNameForNtDevice(pdo_path)).FirstOrDefault()

        Trace.WriteLine($"Device {pdo_path} devinst {devInst}. Registering write overlay '{nativepath}', FakeNonRemovable={FakeNonRemovable}")

        Using regkey = Registry.LocalMachine.CreateSubKey("SYSTEM\CurrentControlSet\Services\aimwrfltr\Parameters")
            If nativepath Is Nothing Then
                regkey.DeleteValue(pdo_path, throwOnMissingValue:=False)
            ElseIf FakeNonRemovable Then
                regkey.SetValue(pdo_path, $"{nativepath}{NonRemovableSuffix}", RegistryValueKind.String)
            Else
                regkey.SetValue(pdo_path, nativepath, RegistryValueKind.String)
            End If
        End Using

        If nativepath Is Nothing Then
            NativeFileIO.RemoveFilter(devInst, "aimwrfltr")
        Else
            NativeFileIO.AddFilter(devInst, "aimwrfltr")
        End If

        Dim last_error = 0

        For r = 1 To 4

            NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, devInst)

            Dim statistics As New WriteFilterStatistics

            last_error = GetWriteOverlayStatus(pdo_path, statistics)

            Trace.WriteLine($"Overlay path '{nativepath}', I/O error code: {last_error}, aimwrfltr error code: 0x{statistics.LastErrorCode:X}, protection: {statistics.IsProtected}, initialized: {statistics.Initialized}")

            If nativepath Is Nothing AndAlso last_error = NativeConstants.NO_ERROR Then

                Trace.WriteLine("Filter driver not yet unloaded, retrying...")
                Await Task.Delay(300, cancel).ConfigureAwait(False)
                Continue For

            ElseIf nativepath IsNot Nothing AndAlso (last_error = NativeConstants.ERROR_INVALID_FUNCTION OrElse
                last_error = NativeConstants.ERROR_INVALID_PARAMETER OrElse
                last_error = NativeConstants.ERROR_NOT_SUPPORTED) Then

                Trace.WriteLine("Filter driver not yet loaded, retrying...")
                Await Task.Delay(300, cancel).ConfigureAwait(False)
                Continue For

            ElseIf (nativepath IsNot Nothing AndAlso last_error <> NativeConstants.NO_ERROR) OrElse
                (nativepath Is Nothing AndAlso last_error <> NativeConstants.ERROR_INVALID_FUNCTION AndAlso
                last_error <> NativeConstants.ERROR_INVALID_PARAMETER AndAlso
                last_error <> NativeConstants.ERROR_NOT_SUPPORTED) Then

                Throw New NotSupportedException("Error checking write filter driver status", New Win32Exception(last_error))

            ElseIf (nativepath IsNot Nothing AndAlso statistics.Initialized) OrElse
                nativepath Is Nothing Then

                Return

            End If

            Throw New IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode))

        Next

        Dim in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(pdo_path, dev_path).Take(10).Select(AddressOf NativeFileIO.FormatProcessName).ToArray()

        If in_use_apps.Length = 0 AndAlso last_error <> 0 Then
            Throw New NotSupportedException("Write filter driver not attached to device", New Win32Exception(last_error))
        ElseIf in_use_apps.Length = 0 Then
            Throw New NotSupportedException("Write filter driver not attached to device")
        Else
            Dim apps = String.Join(", ", in_use_apps)

            Throw New UnauthorizedAccessException($"Write filter driver cannot be attached while applications hold the disk device open.

Currently, the following application{If(in_use_apps.Length <> 1, "s", "")} hold{If(in_use_apps.Length = 1, "s", "")} the disk device open:
{apps}")
        End If

        Throw New FileNotFoundException("Error adding write overlay: Device not found.")

    End Function

    Public Shared Async Function RegisterWriteFilterAsync(devinstAdapter As UInteger, DeviceNumber As UInteger, operation As RegisterWriteFilterOperation, cancel As CancellationToken) As Task

        For Each dev In
            From devinstChild In NativeFileIO.EnumerateChildDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let address = NativeFileIO.GetScsiAddressForNtDevice(path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)

            Trace.WriteLine($"Device number {DeviceNumber:X6}  found at {dev.path} devinst {dev.devinstChild}. Registering write filter driver.")

            If operation = RegisterWriteFilterOperation.Unregister Then

                If NativeFileIO.RemoveFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild)
                End If

                Return

            End If

            Dim last_error = 0

            For r = 1 To 2

                If NativeFileIO.AddFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeConstants.DiskClassGuid, dev.devinstChild)
                End If

                Dim statistics As New WriteFilterStatistics

                last_error = GetWriteOverlayStatus(dev.path, statistics)

                If last_error = NativeConstants.ERROR_INVALID_FUNCTION Then
                    Trace.WriteLine("Filter driver not loaded, retrying...")
                    Await Task.Delay(200, cancel).ConfigureAwait(False)
                    Continue For
                ElseIf last_error <> NativeConstants.NO_ERROR Then
                    Throw New NotSupportedException("Error checking write filter driver status", New Win32Exception)
                End If

                If statistics.Initialized Then
                    Return
                End If

                Throw New IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode))

            Next

            Dim in_use_apps = NativeFileIO.EnumerateProcessesHoldingFileHandle(dev.path).Take(10).Select(AddressOf NativeFileIO.FormatProcessName).ToArray()

            If in_use_apps.Length = 0 AndAlso last_error > 0 Then
                Throw New NotSupportedException("Write filter driver not attached to device", New Win32Exception(last_error))
            ElseIf in_use_apps.Length = 0 Then
                Throw New NotSupportedException("Write filter driver not attached to device")
            Else
                Dim apps = String.Join(", ", in_use_apps)
                Throw New UnauthorizedAccessException($"Write filter driver cannot be attached while applications hold the virtual disk device open.

Currently, the following application{If(in_use_apps.Length <> 1, "s", "")} hold{If(in_use_apps.Length = 1, "s", "")} the disk device open:
{apps}")
            End If
        Next

        Throw New FileNotFoundException("Error adding write overlay: Device not found.")

    End Function

    Public Enum RegisterWriteFilterOperation
        Register
        Unregister
    End Enum

    ''' <summary>
    ''' Retrieves status of write overlay for mounted device.
    ''' </summary>
    ''' <param name="NtDevicePath">NT path to device.</param>
    ''' <param name="Statistics">Data structure that receives current statistics and settings for filter</param>
    ''' <returns>Returns 0 on success or Win32 error code on failure</returns>
    Public Shared Function GetWriteOverlayStatus(NtDevicePath As String, <Out> ByRef Statistics As WriteFilterStatistics) As Integer

        Using hDevice = NativeFileIO.NtCreateFile(NtDevicePath, 0, 0, FileShare.ReadWrite, NtCreateDisposition.Open, NtCreateOptions.NonDirectoryFile, 0, Nothing, Nothing)

            Return GetWriteOverlayStatus(hDevice, Statistics)

        End Using

    End Function

    ''' <summary>
    ''' Retrieves status of write overlay for mounted device.
    ''' </summary>
    ''' <param name="hDevice">Handle to device.</param>
    ''' <param name="Statistics">Data structure that receives current statistics and settings for filter</param>
    ''' <returns>Returns 0 on success or Win32 error code on failure</returns>
    Public Shared Function GetWriteOverlayStatus(hDevice As SafeFileHandle, <Out> ByRef Statistics As WriteFilterStatistics) As Integer

        Statistics = WriteFilterStatistics.GetNew()

        If UnsafeNativeMethods.DeviceIoControl(hDevice, UnsafeNativeMethods.IOCTL_AIMWRFLTR_GET_DEVICE_DATA, IntPtr.Zero, 0, Statistics, Statistics.Version, Nothing, Nothing) Then
            Return NativeConstants.NO_ERROR
        Else
            Return Marshal.GetLastWin32Error()
        End If

    End Function

    ''' <summary>
    ''' Deletes the write overlay image file after use. Also sets this filter driver to
    ''' silently ignore flush requests to improve performance when integrity of the write
    ''' overlay image is not needed for future sessions.
    ''' </summary>
    ''' <param name="NtDevicePath">NT path to device.</param>
    ''' <returns>Returns 0 on success or Win32 error code on failure</returns>
    Public Shared Function SetWriteOverlayDeleteOnClose(NtDevicePath As String) As Integer

        Using hDevice = NativeFileIO.NtCreateFile(NtDevicePath, 0, FileAccess.ReadWrite, FileShare.ReadWrite, NtCreateDisposition.Open, NtCreateOptions.NonDirectoryFile, 0, Nothing, Nothing)

            Return SetWriteOverlayDeleteOnClose(hDevice)

        End Using

    End Function

    ''' <summary>
    ''' Deletes the write overlay image file after use. Also sets this filter driver to
    ''' silently ignore flush requests to improve performance when integrity of the write
    ''' overlay image is not needed for future sessions.
    ''' </summary>
    ''' <param name="hDevice">Handle to device.</param>
    ''' <returns>Returns 0 on success or Win32 error code on failure</returns>
    Public Shared Function SetWriteOverlayDeleteOnClose(hDevice As SafeFileHandle) As Integer

        If UnsafeNativeMethods.DeviceIoControl(hDevice, UnsafeNativeMethods.IOCTL_AIMWRFLTR_DELETE_ON_CLOSE, IntPtr.Zero, 0, IntPtr.Zero, 0, Nothing, Nothing) Then
            Return NativeConstants.NO_ERROR
        Else
            Return Marshal.GetLastWin32Error()
        End If

    End Function

    <SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)>
    Private NotInheritable Class UnsafeNativeMethods

        Private Sub New()
        End Sub

        Public Const IOCTL_AIMWRFLTR_GET_DEVICE_DATA = &H88443404UI

        Public Const IOCTL_AIMWRFLTR_DELETE_ON_CLOSE = &H8844F407UI

        Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              <Out> ByRef lpOutBuffer As WriteFilterStatistics,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

        Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInteger,
              lpInBuffer As IntPtr,
              nInBufferSize As UInteger,
              lpOutBuffer As IntPtr,
              nOutBufferSize As UInteger,
              <Out> ByRef lpBytesReturned As UInteger,
              lpOverlapped As IntPtr) As Boolean

    End Class

End Class


