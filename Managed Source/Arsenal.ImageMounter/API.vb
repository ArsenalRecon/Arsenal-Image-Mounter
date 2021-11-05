''''' API.vb
''''' API for manipulating flag values, issuing SCSI bus rescans and similar
''''' tasks.
''''' 
''''' Copyright (c) 2012-2021, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
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
Imports System.Threading
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32
Imports Microsoft.Win32.SafeHandles

''' <summary>
''' API for manipulating flag values, issuing SCSI bus rescans, manage write filter driver and similar tasks.
''' </summary>
<ComVisible(False)>
Public NotInheritable Class API

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Builds a list of device paths for active Arsenal Image Mounter
    ''' objects.
    ''' </summary>
    Public Shared Iterator Function EnumerateAdapterDevicePaths(HwndParent As IntPtr) As IEnumerable(Of String)

        Dim devinstances As IEnumerable(Of String) = Nothing
        Dim status = NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt", devinstances)

        If status <> 0 OrElse devinstances Is Nothing Then

            Trace.WriteLine($"No devices found serviced by 'phdskmnt'. status=0x{status:X}")
            Return
        End If

        For Each devinstname In devinstances

            Using DevInfoSet = NativeFileIO.UnsafeNativeMethods.SetupDiGetClassDevs(NativeFileIO.NativeConstants.SerenumBusEnumeratorGuid,
                                                                         devinstname,
                                                                         HwndParent,
                                                                         NativeFileIO.UnsafeNativeMethods.DIGCF_DEVICEINTERFACE Or NativeFileIO.UnsafeNativeMethods.DIGCF_PRESENT)

                If DevInfoSet.IsInvalid Then
                    Throw New Win32Exception
                End If

                Dim i = 0UI
                Do
                    Dim DeviceInterfaceData As New NativeFileIO.UnsafeNativeMethods.SP_DEVICE_INTERFACE_DATA
                    DeviceInterfaceData.Initialize()
                    If NativeFileIO.UnsafeNativeMethods.SetupDiEnumDeviceInterfaces(DevInfoSet, IntPtr.Zero, NativeFileIO.NativeConstants.SerenumBusEnumeratorGuid, i, DeviceInterfaceData) = False Then
                        Exit Do
                    End If

                    Dim DeviceInterfaceDetailData As New NativeFileIO.UnsafeNativeMethods.SP_DEVICE_INTERFACE_DETAIL_DATA
                    DeviceInterfaceDetailData.Initialize()
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
    Public Shared Iterator Function EnumerateAdapterDeviceInstances() As IEnumerable(Of UInt32)

        Dim devinstances = EnumerateAdapterDeviceInstanceNames()

        If devinstances Is Nothing Then
            Return
        End If

        For Each devinstname In devinstances
            Trace.WriteLine($"Found adapter instance '{devinstname}'")

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
    Public Shared Function EnumerateAdapterDeviceInstanceNames() As IEnumerable(Of String)

        Dim devinstances As IEnumerable(Of String) = Nothing

        Dim status = NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt", devinstances)

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

    Private Shared ReadOnly KnownFormats As New Dictionary(Of String, Long)(StringComparer.OrdinalIgnoreCase) From
      {
        {"nrg", 600 << 9},
        {"sdi", 8 << 9}
      }

    ''' <summary>
    ''' Checks if filename contains a known extension for which PhDskMnt knows of a constant offset value. That value can be
    ''' later passed as Offset parameter to CreateDevice method.
    ''' </summary>
    ''' <param name="ImageFile">Name of disk image file.</param>
    Public Shared Function GetOffsetByFileExt(ImageFile As String) As Long

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
    Public Shared Function GetSectorSizeFromFileName(imagefile As String) As UInteger

        imagefile.NullCheck(NameOf(imagefile))

        If imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) OrElse
                    imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) OrElse
                    imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) Then

            Return 2048
        Else
            Return 512
        End If

    End Function

    Private Shared ReadOnly multipliers As New Dictionary(Of ULong, String) From
            {{1UL << 60, " EB"},
             {1UL << 50, " PB"},
             {1UL << 40, " TB"},
             {1UL << 30, " GB"},
             {1UL << 20, " MB"},
             {1UL << 10, " KB"},
             {2UL, " bytes"}}

    Public Shared Function FormatBytes(size As ULong) As String

        For Each m In multipliers
            If size >= m.Key Then
                Return $"{size / m.Key:0.0}{m.Value}"
            End If
        Next

        Return $"{size} byte"

    End Function

    Public Shared Function FormatBytes(size As ULong, precision As Integer) As String

        For Each m In multipliers
            If size >= m.Key Then
                Return $"{(size / m.Key).ToString("0." & New String("0"c, precision - 1))}{m.Value}"
            End If
        Next

        Return $"{size} byte"

    End Function

    Public Shared Function FormatBytes(size As Long) As String

        For Each m In multipliers
            If Math.Abs(size) >= m.Key Then
                Return $"{size / m.Key:0.000}{m.Value}"
            End If
        Next

        Return $"{size} byte"

    End Function

    Public Shared Function FormatBytes(size As Long, precision As Integer) As String

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
    Public Shared Function IsReadOnly(Flags As DeviceFlags) As Boolean

        Return Flags.HasFlag(DeviceFlags.ReadOnly)

    End Function

    ''' <summary>
    ''' Checks if Flags specifies a removable virtual disk.
    ''' </summary>
    ''' <param name="Flags">Flag field to check.</param>
    Public Shared Function IsRemovable(Flags As DeviceFlags) As Boolean

        Return Flags.HasFlag(DeviceFlags.Removable)

    End Function

    ''' <summary>
    ''' Checks if Flags specifies a modified virtual disk.
    ''' </summary>
    ''' <param name="Flags">Flag field to check.</param>
    Public Shared Function IsModified(Flags As DeviceFlags) As Boolean

        Return Flags.HasFlag(DeviceFlags.Modified)

    End Function

    ''' <summary>
    ''' Gets device type bits from a Flag field.
    ''' </summary>
    ''' <param name="Flags">Flag field to check.</param>
    Public Shared Function GetDeviceType(Flags As DeviceFlags) As DeviceFlags

        Return CType(Flags And &HF0UI, DeviceFlags)

    End Function

    ''' <summary>
    ''' Gets disk type bits from a Flag field.
    ''' </summary>
    ''' <param name="Flags">Flag field to check.</param>
    Public Shared Function GetDiskType(Flags As DeviceFlags) As DeviceFlags

        Return CType(Flags And &HF00UI, DeviceFlags)

    End Function

    ''' <summary>
    ''' Gets proxy type bits from a Flag field.
    ''' </summary>
    ''' <param name="Flags">Flag field to check.</param>
    Public Shared Function GetProxyType(Flags As DeviceFlags) As DeviceFlags

        Return CType(Flags And &HF000UI, DeviceFlags)

    End Function

    Public Shared Function EnumeratePhysicalDeviceObjectPaths(devinstAdapter As UInt32, DeviceNumber As UInt32) As IEnumerable(Of String)

        Return _
            From devinstChild In NativeFileIO.EnumerateChildDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let address = NativeFileIO.GetScsiAddressForNtDevice(path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)
            Select path

    End Function

    Public Shared Function EnumerateDeviceProperty(devinstAdapter As UInt32, DeviceNumber As UInt32, prop As NativeFileIO.CmDevNodeRegistryProperty) As IEnumerable(Of String)

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

    Public Shared Sub RegisterWriteOverlayImage(devInst As UInteger, OverlayImagePath As String)
        RegisterWriteOverlayImage(devInst, OverlayImagePath, FakeNonRemovable:=False)
    End Sub

    Public Shared Sub RegisterWriteOverlayImage(devInst As UInteger, OverlayImagePath As String, FakeNonRemovable As Boolean)

        Dim nativepath As String

        If Not String.IsNullOrWhiteSpace(OverlayImagePath) Then
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

            NativeFileIO.RestartDevice(NativeFileIO.NativeConstants.DiskClassGuid, devInst)

            Dim statistics As New WriteFilterStatistics

            last_error = GetWriteOverlayStatus(pdo_path, statistics)

            Trace.WriteLine($"Overlay path '{nativepath}', I/O error code: {last_error}, aimwrfltr error code: 0x{statistics.LastErrorCode:X}, protection: {statistics.IsProtected}, initialized: {statistics.Initialized}")

            If nativepath Is Nothing AndAlso last_error = NativeFileIO.NativeConstants.NO_ERROR Then

                Trace.WriteLine("Filter driver not yet unloaded, retrying...")
                Thread.Sleep(300)
                Continue For

            ElseIf nativepath IsNot Nothing AndAlso (last_error = NativeFileIO.NativeConstants.ERROR_INVALID_FUNCTION OrElse
                last_error = NativeFileIO.NativeConstants.ERROR_INVALID_PARAMETER OrElse
                last_error = NativeFileIO.NativeConstants.ERROR_NOT_SUPPORTED) Then

                Trace.WriteLine("Filter driver not yet loaded, retrying...")
                Thread.Sleep(300)
                Continue For

            ElseIf (nativepath IsNot Nothing AndAlso last_error <> NativeFileIO.NativeConstants.NO_ERROR) OrElse
                (nativepath Is Nothing AndAlso last_error <> NativeFileIO.NativeConstants.ERROR_INVALID_FUNCTION AndAlso
                last_error <> NativeFileIO.NativeConstants.ERROR_INVALID_PARAMETER AndAlso
                last_error <> NativeFileIO.NativeConstants.ERROR_NOT_SUPPORTED) Then

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

    End Sub

    Public Shared Sub RegisterWriteFilter(devinstAdapter As UInt32, DeviceNumber As UInt32, operation As RegisterWriteFilterOperation)

        For Each dev In
            From devinstChild In NativeFileIO.EnumerateChildDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let address = NativeFileIO.GetScsiAddressForNtDevice(path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)

            Trace.WriteLine($"Device number {DeviceNumber:X6}  found at {dev.path} devinst {dev.devinstChild}. Registering write filter driver.")

            If operation = RegisterWriteFilterOperation.Unregister Then

                If NativeFileIO.RemoveFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeFileIO.NativeConstants.DiskClassGuid, dev.devinstChild)
                End If

                Return

            End If

            Dim last_error = 0

            For r = 1 To 2

                If NativeFileIO.AddFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeFileIO.NativeConstants.DiskClassGuid, dev.devinstChild)
                End If

                Dim statistics As New WriteFilterStatistics

                last_error = GetWriteOverlayStatus(dev.path, statistics)

                If last_error = NativeFileIO.NativeConstants.ERROR_INVALID_FUNCTION Then
                    Trace.WriteLine("Filter driver not loaded, retrying...")
                    Thread.Sleep(200)
                    Continue For
                ElseIf last_error <> NativeFileIO.NativeConstants.NO_ERROR Then
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

#If NET45_OR_GREATER OrElse NETCOREAPP OrElse NETSTANDARD Then
    Public Shared Async Function RegisterWriteOverlayImageAsync(devInst As UInteger, OverlayImagePath As String, FakeNonRemovable As Boolean, cancel As CancellationToken) As Task

        Dim nativepath As String

        If Not String.IsNullOrWhiteSpace(OverlayImagePath) Then
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

            NativeFileIO.RestartDevice(NativeFileIO.NativeConstants.DiskClassGuid, devInst)

            Dim statistics As New WriteFilterStatistics

            last_error = GetWriteOverlayStatus(pdo_path, statistics)

            Trace.WriteLine($"Overlay path '{nativepath}', I/O error code: {last_error}, aimwrfltr error code: 0x{statistics.LastErrorCode:X}, protection: {statistics.IsProtected}, initialized: {statistics.Initialized}")

            If nativepath Is Nothing AndAlso last_error = NativeFileIO.NativeConstants.NO_ERROR Then

                Trace.WriteLine("Filter driver not yet unloaded, retrying...")
                Await Task.Delay(300, cancel).ConfigureAwait(continueOnCapturedContext:=False)
                Continue For

            ElseIf nativepath IsNot Nothing AndAlso (last_error = NativeFileIO.NativeConstants.ERROR_INVALID_FUNCTION OrElse
                last_error = NativeFileIO.NativeConstants.ERROR_INVALID_PARAMETER OrElse
                last_error = NativeFileIO.NativeConstants.ERROR_NOT_SUPPORTED) Then

                Trace.WriteLine("Filter driver not yet loaded, retrying...")
                Await Task.Delay(300, cancel).ConfigureAwait(continueOnCapturedContext:=False)
                Continue For

            ElseIf (nativepath IsNot Nothing AndAlso last_error <> NativeFileIO.NativeConstants.NO_ERROR) OrElse
                (nativepath Is Nothing AndAlso last_error <> NativeFileIO.NativeConstants.ERROR_INVALID_FUNCTION AndAlso
                last_error <> NativeFileIO.NativeConstants.ERROR_INVALID_PARAMETER AndAlso
                last_error <> NativeFileIO.NativeConstants.ERROR_NOT_SUPPORTED) Then

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

    Public Shared Async Function RegisterWriteFilterAsync(devinstAdapter As UInt32, DeviceNumber As UInt32, operation As RegisterWriteFilterOperation, cancel As CancellationToken) As Task

        For Each dev In
            From devinstChild In NativeFileIO.EnumerateChildDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let address = NativeFileIO.GetScsiAddressForNtDevice(path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)

            Trace.WriteLine($"Device number {DeviceNumber:X6}  found at {dev.path} devinst {dev.devinstChild}. Registering write filter driver.")

            If operation = RegisterWriteFilterOperation.Unregister Then

                If NativeFileIO.RemoveFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeFileIO.NativeConstants.DiskClassGuid, dev.devinstChild)
                End If

                Return

            End If

            Dim last_error = 0

            For r = 1 To 2

                If NativeFileIO.AddFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeFileIO.NativeConstants.DiskClassGuid, dev.devinstChild)
                End If

                Dim statistics As New WriteFilterStatistics

                last_error = GetWriteOverlayStatus(dev.path, statistics)

                If last_error = NativeFileIO.NativeConstants.ERROR_INVALID_FUNCTION Then
                    Trace.WriteLine("Filter driver not loaded, retrying...")
                    Await Task.Delay(200, cancel).ConfigureAwait(continueOnCapturedContext:=False)
                    Continue For
                ElseIf last_error <> NativeFileIO.NativeConstants.NO_ERROR Then
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

#End If

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

        Using hDevice = NativeFileIO.NtCreateFile(NtDevicePath, 0, 0, FileShare.ReadWrite, NativeFileIO.NtCreateDisposition.Open, NativeFileIO.NtCreateOptions.NonDirectoryFile, 0, Nothing, Nothing)

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

        Statistics = WriteFilterStatistics.Initialize()

        If UnsafeNativeMethods.DeviceIoControl(hDevice, UnsafeNativeMethods.IOCTL_AIMWRFLTR_GET_DEVICE_DATA, IntPtr.Zero, 0, Statistics, Statistics.Version, Nothing, Nothing) Then
            Return NativeFileIO.NativeConstants.NO_ERROR
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

        Using hDevice = NativeFileIO.NtCreateFile(NtDevicePath, 0, FileAccess.ReadWrite, FileShare.ReadWrite, NativeFileIO.NtCreateDisposition.Open, NativeFileIO.NtCreateOptions.NonDirectoryFile, 0, Nothing, Nothing)

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
            Return NativeFileIO.NativeConstants.NO_ERROR
        Else
            Return Marshal.GetLastWin32Error()
        End If

    End Function

    Private NotInheritable Class UnsafeNativeMethods

        Private Sub New()
        End Sub

        Public Const IOCTL_AIMWRFLTR_GET_DEVICE_DATA = &H88443404UI

        Public Const IOCTL_AIMWRFLTR_DELETE_ON_CLOSE = &H8844F407UI

        Public Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As WriteFilterStatistics,
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

    End Class

End Class


