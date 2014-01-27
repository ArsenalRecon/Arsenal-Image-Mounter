
''''' API.vb
''''' API for manipulating flag values, issuing SCSI bus rescans and similar
''''' tasks.
''''' 
''''' Copyright (c) 2012-2013, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
''''' This source code is available under the terms of the Affero General Public
''''' License v3.
'''''
''''' Please see LICENSE.txt for full license terms, including the availability of
''''' proprietary exceptions.
''''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
'''''



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

        Dim SCSIGUID As New Guid("{4D36E97B-E325-11CE-BFC1-08002BE10318}")

        Dim devinstances As String() = Nothing
        Dim status = NativeFileIO.GetDeviceInstancesForService("phdskmnt", devinstances)
        If status <> 0 OrElse
          devinstances Is Nothing OrElse
          devinstances.Length = 0 OrElse
          Not devinstances.Any(Function(s) Not String.IsNullOrEmpty(s)) Then

            Trace.WriteLine("No devices found serviced by 'phdskmnt'. status=0x" & status.ToString("X"))
            Return Nothing
        End If

        Dim devInstList As New List(Of String)
        For Each devinstname In From devinst In devinstances Where Not String.IsNullOrEmpty(devinst)
            Using DevInfoSet = NativeFileIO.Win32API.SetupDiGetClassDevs(SCSIGUID,
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
                    If NativeFileIO.Win32API.SetupDiEnumDeviceInterfaces(DevInfoSet, IntPtr.Zero, SCSIGUID, i, DeviceInterfaceData) = False Then
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
            If status <> 0 Then
                Trace.WriteLine("Device '" & devinstname & "' error 0x" & status.ToString("X"))
                Continue For
            End If
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
          Not devinstances.Any(Function(s) Not String.IsNullOrEmpty(s)) Then

            Trace.WriteLine("No devices found serviced by 'phdskmnt'. status=0x" & status.ToString("X"))
            Return Nothing
        End If

        Dim devInstList As New List(Of UInt32)
        For Each devinstname In From devinst In devinstances Where Not String.IsNullOrEmpty(devinst)
            Dim devInst As UInt32
            status = NativeFileIO.Win32API.CM_Locate_DevNode(devInst, devinstname, 0)
            If status <> 0 Then
                Trace.WriteLine("Device '" & devinstname & "' error 0x" & status.ToString("X"))
                Continue For
            End If

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
                Trace.WriteLine("Re-enumeration failed: 0x" & status.ToString("X"))
            Else
                Trace.WriteLine("Re-enumeration successful.")
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

End Class


