''''' API.vb
''''' API for manipulating flag values, issuing SCSI bus rescans and similar
''''' tasks.
''''' 
''''' Copyright (c) 2012-2019, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code and API are available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''

Imports Arsenal.ImageMounter.IO
Imports Microsoft.Win32

''' <summary>
''' API for manipulating flag values, issuing SCSI bus rescans and similar tasks.
''' </summary>
<ComVisible(False)>
Public Class API

    Private Sub New()

    End Sub

    ''' <summary>
    ''' Builds a list of device paths for active Arsenal Image Mounter
    ''' objects.
    ''' </summary>
    Public Shared Function GetAdapterDevicePaths(hwndParent As IntPtr) As List(Of String)

        Dim devinstances As String() = Nothing
        Dim status = NativeFileIO.GetDeviceInstancesForService("phdskmnt", devinstances)
        If status <> 0 OrElse
          devinstances Is Nothing OrElse
          devinstances.Length = 0 OrElse
          Array.TrueForAll(devinstances, AddressOf String.IsNullOrWhiteSpace) Then

            Trace.WriteLine("No devices found serviced by 'phdskmnt'. status=0x" & status.ToString("X"))
            Return Nothing
        End If

        Dim devInstList As New List(Of String)

        For Each devinstname In devinstances

            Using DevInfoSet = NativeFileIO.Win32API.SetupDiGetClassDevs(NativeFileIO.Win32API.SerenumBusEnumeratorGuid,
                                                                         devinstname,
                                                                         hwndParent,
                                                                         NativeFileIO.Win32API.DIGCF_DEVICEINTERFACE Or NativeFileIO.Win32API.DIGCF_PRESENT)

                If DevInfoSet.IsInvalid Then
                    Throw New Win32Exception
                End If

                Dim i = 0UI
                Do
                    Dim DeviceInterfaceData As New NativeFileIO.Win32API.SP_DEVICE_INTERFACE_DATA
                    DeviceInterfaceData.Initialize()
                    If NativeFileIO.Win32API.SetupDiEnumDeviceInterfaces(DevInfoSet, IntPtr.Zero, NativeFileIO.Win32API.SerenumBusEnumeratorGuid, i, DeviceInterfaceData) = False Then
                        Exit Do
                    End If

                    Dim DeviceInterfaceDetailData As New NativeFileIO.Win32API.SP_DEVICE_INTERFACE_DETAIL_DATA
                    DeviceInterfaceDetailData.Initialize()
                    If NativeFileIO.Win32API.SetupDiGetDeviceInterfaceDetail(DevInfoSet, DeviceInterfaceData, DeviceInterfaceDetailData, CUInt(Marshal.SizeOf(DeviceInterfaceData)), 0, IntPtr.Zero) = True Then
                        devInstList.Add(DeviceInterfaceDetailData.DevicePath)
                    End If

                    i += 1UI
                Loop

            End Using
        Next

        Return devInstList

    End Function

    ''' <summary>
    ''' Returns a value indicating whether Arsenal Image Mounter driver is
    ''' installed and running.
    ''' </summary>
    Public Shared ReadOnly Property AdapterDevicePresent As Boolean
        Get
            Dim devInsts = GetAdapterDeviceInstances()
            If devInsts Is Nothing OrElse devInsts.Count = 0 Then
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
    Public Shared Function GetAdapterDeviceInstances() As List(Of UInt32)

        Dim devinstances As String() = Nothing
        Dim status = NativeFileIO.GetDeviceInstancesForService("phdskmnt", devinstances)
        If status <> 0 OrElse
          devinstances Is Nothing OrElse
          devinstances.Length = 0 OrElse
          Array.TrueForAll(devinstances, AddressOf String.IsNullOrWhiteSpace) Then

            Trace.WriteLine("No devices found serviced by 'phdskmnt'. status=0x" & status.ToString("X"))
            Return Nothing
        End If

        Dim devInstList As New List(Of UInt32)(devinstances.Length)
        For Each devinstname In devinstances
            Trace.WriteLine($"Found adapter instance '{devinstname}'")

            Dim devInst As UInt32
            status = NativeFileIO.Win32API.CM_Locate_DevNode(devInst, devinstname, 0)
            If status <> 0 Then
                Trace.WriteLine($"Device '{devinstname}' error 0x{status:X}")
                Continue For
            End If

            Trace.WriteLine($"Found adapter devinst '{devInst}'")

            devInstList.Add(devInst)
        Next

        Return devInstList

    End Function

    ''' <summary>
    ''' Issues a SCSI bus rescan on found Arsenal Image Mounter adapters. This causes Disk Management
    ''' in Windows to find newly created virtual disks and remove newly deleted ones.
    ''' </summary>
    Public Shared Function RescanScsiAdapter() As Boolean

        Dim devInsts = GetAdapterDeviceInstances()
        If devInsts Is Nothing OrElse devInsts.Count = 0 Then
            Return False
        End If

        Dim rc As Boolean
        For Each devInst In devInsts
            Dim status = NativeFileIO.Win32API.CM_Reenumerate_DevNode(devInst, 0)
            If status <> 0 Then
                Trace.WriteLine("Re-enumeration of '" & devInst & "' failed: 0x" & status.ToString("X"))
            Else
                Trace.WriteLine("Re-enumeration of '" & devInst & "' successful.")
                rc = True
            End If
        Next

        Return rc

    End Function

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

    Public Shared Function FormatFileSize(size As ULong) As String

        For Each m In multipliers
            If size >= m.Key Then
                Return (size / m.Key).ToString("0.000") & m.Value
            End If
        Next

        Return size.ToString() & " byte"

    End Function

    Public Shared Function FormatFileSize(size As Long) As String

        For Each m In multipliers
            If Math.Abs(size) >= m.Key Then
                Return (size / m.Key).ToString("0.000") & m.Value
            End If
        Next

        Return size.ToString() & " byte"

    End Function

    ''' <summary>
    ''' Checks if Flags specifies a read only virtual disk.
    ''' </summary>
    ''' <param name="Flags">Flag field to check.</param>
    Public Shared Function IsReadOnly(Flags As DeviceFlags) As Boolean

        Return (Flags And DeviceFlags.ReadOnly) = DeviceFlags.ReadOnly

    End Function

    ''' <summary>
    ''' Checks if Flags specifies a removable virtual disk.
    ''' </summary>
    ''' <param name="Flags">Flag field to check.</param>
    Public Shared Function IsRemovable(Flags As DeviceFlags) As Boolean

        Return (Flags And DeviceFlags.Removable) = DeviceFlags.Removable

    End Function

    ''' <summary>
    ''' Checks if Flags specifies a modified virtual disk.
    ''' </summary>
    ''' <param name="Flags">Flag field to check.</param>
    Public Shared Function IsModified(Flags As DeviceFlags) As Boolean

        Return (Flags And DeviceFlags.Modified) = DeviceFlags.Modified

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

    Public Shared Function GetPhysicalDeviceObjectPath(DeviceNumber As UInt32) As IEnumerable(Of String)

        Dim adapters = GetAdapterDeviceInstances()

        If adapters Is Nothing OrElse
            adapters.Count = 0 Then

            Throw New IOException("SCSI adapter not installed")

        End If

        Return _
            From devinstAdapter In adapters
            From devinstChild In NativeFileIO.EnumerateDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectName(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let win32path = $"\\?\GLOBALROOT{path}"
            Let address = NativeFileIO.GetScsiAddress(win32path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)
            Select path

    End Function

    Public Shared Sub UnregisterWriteOverlayImage(devInst As UInteger)
        RegisterWriteOverlayImage(devInst, Nothing)
    End Sub

    Public Shared Sub RegisterWriteOverlayImage(devInst As UInteger, OverlayImagePath As String)

        Dim nativepath As String = Nothing

        If Not String.IsNullOrWhiteSpace(OverlayImagePath) Then
            nativepath = NativeFileIO.GetNtPath(OverlayImagePath)
        End If

        Dim dev_path = NativeFileIO.GetPhysicalDeviceObjectName(devInst)
        Dim win32_path = $"\\?\GLOBALROOT{dev_path}"

        Trace.WriteLine($"Device {dev_path} devinst {devInst}. Registering write overlay '{nativepath}'")

        Using regkey = Registry.LocalMachine.CreateSubKey("SYSTEM\CurrentControlSet\Services\aimwrfltr\Parameters")
            If nativepath Is Nothing Then
                regkey.DeleteValue(dev_path, throwOnMissingValue:=False)
            Else
                regkey.SetValue(dev_path, nativepath, RegistryValueKind.String)
            End If
        End Using

        If nativepath Is Nothing Then
            NativeFileIO.RemoveFilter(devInst, "aimwrfltr")
        Else
            NativeFileIO.AddFilter(devInst, "aimwrfltr")
        End If

        Dim last_error = 0

        For r = 1 To 2

            NativeFileIO.RestartDevice(NativeFileIO.Win32API.DiskClassGuid, devInst)

            If nativepath Is Nothing Then
                Return
            End If

            Dim statistics As New WriteFilterStatistics

            If Not GetWriteOverlayStatus(win32_path, statistics) Then
                last_error = Marshal.GetLastWin32Error()
                If last_error = NativeFileIO.Win32API.ERROR_INVALID_FUNCTION Then
                    Trace.WriteLine("Filter driver not yet loaded, retrying...")
                    Thread.Sleep(200)
                    Continue For
                Else
                    Throw New NotSupportedException("Error checking write filter driver status", New Win32Exception)
                End If
            End If

            If statistics.Initialized = 1 Then
                Return
            End If

            Throw New IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode))

        Next

        Dim in_use_apps = NativeFileIO.FindProcessesHoldingFileHandle(dev_path).ToArray()

        If in_use_apps.Length = 0 AndAlso last_error > 0 Then
            Throw New NotSupportedException("Write filter driver not attached to device", New Win32Exception(last_error))
        ElseIf in_use_apps.Length = 0 Then
            Throw New NotSupportedException("Write filter driver not attached to device")
        Else
            Dim apps = String.Join(", ", From app In in_use_apps Select $"{app.ProcessName} (id={app.HandleTableEntry.ProcessId})")
            Throw New UnauthorizedAccessException($"Write filter driver cannot be attached while applications hold the virtual disk device open. Currently, the following application{If(in_use_apps.Length <> 1, "s", "")} hold{If(in_use_apps.Length = 1, "s", "")} the disk device open: {apps}")
        End If

        Throw New FileNotFoundException("Error adding write overlay: Device not found.")

    End Sub

    Public Enum RegisterWriteFilterOperation
        Register
        Unregister
    End Enum

    Public Shared Sub RegisterWriteFilter(DeviceNumber As UInt32, operation As RegisterWriteFilterOperation)

        Dim adapters = GetAdapterDeviceInstances()

        If adapters Is Nothing OrElse
            adapters.Count = 0 Then

            Throw New IOException("SCSI adapter not installed")

        End If

        For Each dev In
            From devinstAdapter In adapters
            From devinstChild In NativeFileIO.EnumerateDevices(devinstAdapter)
            Let path = NativeFileIO.GetPhysicalDeviceObjectName(devinstChild)
            Where Not String.IsNullOrWhiteSpace(path)
            Let win32path = $"\\?\GLOBALROOT{path}"
            Let address = NativeFileIO.GetScsiAddress(win32path)
            Where address.HasValue AndAlso address.Value.DWordDeviceNumber.Equals(DeviceNumber)

            Trace.WriteLine($"Device number {DeviceNumber:X6}  found at {dev.path} devinst {dev.devinstChild}. Registering write filter driver.")

            If operation = RegisterWriteFilterOperation.Unregister Then

                If NativeFileIO.RemoveFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeFileIO.Win32API.DiskClassGuid, dev.devinstChild)
                End If

                Return

            End If

            Dim last_error = 0

            For r = 1 To 2

                If NativeFileIO.AddFilter(dev.devinstChild, "aimwrfltr") Then
                    NativeFileIO.RestartDevice(NativeFileIO.Win32API.DiskClassGuid, dev.devinstChild)
                End If

                Dim statistics As New WriteFilterStatistics

                If Not GetWriteOverlayStatus(dev.win32path, statistics) Then
                    last_error = Marshal.GetLastWin32Error()
                    If last_error = NativeFileIO.Win32API.ERROR_INVALID_FUNCTION Then
                        Trace.WriteLine("Filter driver not loaded, retrying...")
                        Thread.Sleep(200)
                        Continue For
                    Else
                        Throw New NotSupportedException("Error checking write filter driver status", New Win32Exception)
                    End If
                End If

                If statistics.Initialized = 1 Then
                    Return
                End If

                Throw New IOException("Error adding write overlay to device", NativeFileIO.GetExceptionForNtStatus(statistics.LastErrorCode))

            Next

            Dim in_use_apps = NativeFileIO.FindProcessesHoldingFileHandle(dev.path).ToArray()

            If in_use_apps.Length = 0 AndAlso last_error > 0 Then
                Throw New NotSupportedException("Write filter driver not attached to device", New Win32Exception(last_error))
            ElseIf in_use_apps.Length = 0 Then
                Throw New NotSupportedException("Write filter driver not attached to device")
            Else
                Dim apps = String.Join(", ", From app In in_use_apps Select $"{app.ProcessName} (id={app.HandleTableEntry.ProcessId})")
                Throw New UnauthorizedAccessException($"Write filter driver cannot be attached while applications hold the virtual disk device open. Currently, the following application{If(in_use_apps.Length <> 1, "s", "")} hold{If(in_use_apps.Length = 1, "s", "")} the disk device open: {apps}")
            End If
        Next

        Throw New FileNotFoundException("Error adding write overlay: Device not found.")

    End Sub

    ''' <summary>
    ''' Retrieves status of write overlay for mounted device.
    ''' </summary>
    ''' <param name="Device">Path to device.</param>
    Public Shared Function GetWriteOverlayStatus(Device As String, <Out> ByRef Statistics As WriteFilterStatistics) As Boolean

        Using hDevice = NativeFileIO.OpenFileHandle(Device, 0, FileShare.ReadWrite, FileMode.Open, False)

            Return GetWriteOverlayStatus(hDevice, Statistics)

        End Using

    End Function

    ''' <summary>
    ''' Retrieves status of write overlay for mounted device.
    ''' </summary>
    ''' <param name="hDevice">Handle to device.</param>
    Public Shared Function GetWriteOverlayStatus(hDevice As SafeFileHandle, <Out> ByRef Statistics As WriteFilterStatistics) As Boolean

        Statistics.Initialize()

        Return DeviceIoControl(hDevice, IOCTL_AIMWRFLTR_GET_DEVICE_DATA, IntPtr.Zero, 0, Statistics, CUInt(Marshal.SizeOf(GetType(WriteFilterStatistics))), Nothing, Nothing)

    End Function

    Private Const IOCTL_AIMWRFLTR_GET_DEVICE_DATA = &H88443404UI

    Private Declare Function DeviceIoControl Lib "kernel32" (
              hDevice As SafeFileHandle,
              dwIoControlCode As UInt32,
              lpInBuffer As IntPtr,
              nInBufferSize As UInt32,
              <Out> ByRef lpOutBuffer As WriteFilterStatistics,
              nOutBufferSize As UInt32,
              <Out> ByRef lpBytesReturned As UInt32,
              lpOverlapped As IntPtr) As Boolean

End Class


