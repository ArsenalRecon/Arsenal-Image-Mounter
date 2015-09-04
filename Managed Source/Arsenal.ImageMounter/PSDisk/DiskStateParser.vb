Imports System.Threading.Tasks
Imports Arsenal.ImageMounter.ScsiAdapter
Imports System.Runtime.InteropServices
Imports Arsenal.ImageMounter.PSDisk
Imports Arsenal.ImageMounter.IO

Namespace PSDisk

    Public Class DiskStateParser
        Implements IDisposable

        Private PSPhysicalDiskParser As PSPhysicalDiskParser

        Private PSDiskParser As PSDiskParser

        Private Shared ReadOnly SizeOfScsiAddress As UInt32 = CUInt(Marshal.SizeOf(GetType(NativeFileIO.Win32API.SCSI_ADDRESS)))

        Private Shared Function GetDriveScsiIds(portnumber As Byte) As Dictionary(Of UInteger, UInteger)

            Dim GetScsiAddress =
                Function(drv As String) As NativeFileIO.Win32API.SCSI_ADDRESS?
                Try
                    Using disk As New DiskDevice(drv)
                        Dim ScsiAddress As NativeFileIO.Win32API.SCSI_ADDRESS
                        Dim rc = NativeFileIO.Win32API.
                            DeviceIoControl(disk.SafeFileHandle,
                                            NativeFileIO.Win32API.IOCTL_SCSI_GET_ADDRESS,
                                            IntPtr.Zero,
                                            0,
                                            ScsiAddress,
                                            SizeOfScsiAddress,
                                            Nothing,
                                            Nothing)

                        If rc Then
                            Return ScsiAddress
                        Else
                            Trace.WriteLine("IOCTL_SCSI_GET_ADDRESS failed for device " & drv & ": Error 0x" & Marshal.GetLastWin32Error().ToString("X"))
                            Return Nothing
                        End If
                    End Using

                Catch ex As Exception
                    Trace.WriteLine("Exception attempting to find SCSI address for device " & drv & ": " & ex.ToString())
                    Return Nothing

                End Try
            End Function

            Dim q =
                From drv In NativeFileIO.QueryDosDevice()
                Where drv.StartsWith("PhysicalDrive")
                Let address = GetScsiAddress(String.Concat("\\?\", drv))
                Where address.HasValue
                Where address.Value.PortNumber = portnumber
                Let drivenumber = UInteger.Parse(drv.Substring("PhysicalDrive".Length))

            Return q.ToDictionary(Function(o) o.address.Value.DWordDeviceNumber,
                                  Function(o) o.drivenumber)

        End Function

        Public Sub New()

            Try
                Me.PSPhysicalDiskParser = New PSPhysicalDiskParser("Arsenal ", "Virtual ")

            Catch ex As Exception
                Trace.WriteLine("Cannot use PowerShell Get-PhysicalDisk command: " & ex.ToString())

            End Try

            Try
                Me.PSDiskParser = New PSDiskParser("Arsenal Virtual *")

            Catch ex As Exception
                Trace.WriteLine("Cannot use PowerShell Get-Disk command: " & ex.ToString())

            End Try

        End Sub

        Public Function GetSimpleView(portnumber As Byte, deviceProperties As List(Of DeviceProperties)) As List(Of DiskStateView)

            Try
                Dim ids = GetDriveScsiIds(portnumber)

                Dim getid =
                    Function(dev As DeviceProperties) As UInt32?
                    Dim result As UInt32
                    If ids.TryGetValue(dev.DeviceNumber, result) Then
                        Return result
                    Else
                        Return Nothing
                    End If
                End Function

                Return _
                    deviceProperties.
                    ConvertAll(
                        Function(dev)

                            Dim view As New DiskStateView

                            view.DeviceProperties = dev
                            view.DriveNumber = getid(dev)

                            If view.DriveNumber.HasValue Then
                                Try
                                    Dim device_path = "\\?\PhysicalDrive" & view.DriveNumber.Value.ToString()
                                    Using device As New DiskDevice(device_path, FileAccess.Read)
                                        view.DevicePath = device_path
                                        view.RawDiskSignature = device.DiskSignature
                                        view.NativePropertyDiskOffline = device.DiskOffline
                                        view.NativePropertyDiskOReadOnly = device.DiskReadOnly
                                        Dim part = device.PartitionInformationEx
                                        If part.HasValue Then
                                            Select Case part.Value.PartitionStyle
                                                Case NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE.PARTITION_STYLE_MBR
                                                    view.NativePartitionLayout = PSDisk.PSDiskParser.PartitionStyle.MBR
                                                Case NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE.PARTITION_STYLE_GPT
                                                    view.NativePartitionLayout = PSDisk.PSDiskParser.PartitionStyle.GPT
                                                Case NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE.PARTITION_STYLE_RAW
                                                    view.NativePartitionLayout = PSDisk.PSDiskParser.PartitionStyle.RAW
                                            End Select
                                        End If
                                    End Using

                                Catch ex As Exception
                                    Trace.WriteLine("Error reading signature from MBR for drive " & view.DriveNumber.Value.ToString() & ": " & ex.ToString())

                                End Try

                                Try
                                    view.Volumes = NativeFileIO.GetDiskVolumes(view.DriveNumber.Value).ToArray()
                                    view.MountPoints = view.Volumes.SelectMany(AddressOf NativeFileIO.GetVolumeMountPoints).ToArray()

                                Catch ex As Exception
                                    Trace.WriteLine("Error enumerating volumes for drive " & view.DriveNumber.Value.ToString() & ": " & ex.ToString())

                                End Try
                            End If

                            Return view
                        End Function)

            Catch ex As Exception When _
                Function()
                    Trace.WriteLine("Exception in GetSimpleView: " & ex.ToString())

                    Return False
                End Function()

                Return Nothing
            End Try

        End Function

        Public Function GetFullView(portnumber As Byte, deviceProperties As List(Of DeviceProperties)) As List(Of DiskStateView)

            Dim GetScsiAddress =
                Function(disknumber As String)
                    Using disk As New DiskDevice("\\?\PhysicalDrive" & disknumber)
                        Return disk.ScsiAddress
                    End Using
                End Function

            Try
                If PSDiskParser Is Nothing OrElse PSPhysicalDiskParser Is Nothing Then
                    Throw New Exception("Full disk view not supported on this platform. Windows 8 or later required.")
                End If

                Dim diskstates = PSDiskParser.GetDiskStates().ToArray()

                Dim phdiskstates = Aggregate disk In PSPhysicalDiskParser.GetDiskStates()
                                   Let scsi_address = GetScsiAddress(disk.DeviceId)
                                   Where scsi_address.HasValue
                                   Where scsi_address.Value.PortNumber = portnumber
                                   Select New KeyValuePair(Of UInteger, PSPhysicalDiskParser.PhysicalDiskState)(
                                        scsi_address.Value.DWordDeviceNumber, disk)
                                    Into ToArray()

                Return _
                    deviceProperties.ConvertAll(
                        Function(dev)

                            Dim view As New DiskStateView

                            view.DeviceProperties = dev
                            view.PhysicalDiskState = (Aggregate DiskState In phdiskstates Into FirstOrDefault(DiskState.Key = dev.DeviceNumber)).Value
                            view.DiskState = If(view.PhysicalDiskState IsNot Nothing,
                                            (Aggregate DiskState In diskstates
                                             Into FirstOrDefault(DiskState.Number.HasValue AndAlso DiskState.Number.Value = UInteger.Parse(view.PhysicalDiskState.DeviceId))),
                                            Nothing)
                            view.DriveNumber = If(view.DiskState IsNot Nothing,
                                              view.DiskState.Number,
                                              If(view.PhysicalDiskState IsNot Nothing,
                                                 UInteger.Parse(view.PhysicalDiskState.DeviceId),
                                                 Nothing))

                            If view.DriveNumber.HasValue Then
                                Try
                                    Dim device_path = "\\?\PhysicalDrive" & view.DriveNumber.Value.ToString()
                                    Using device As New DiskDevice(device_path, FileAccess.Read)
                                        view.DevicePath = device_path
                                        view.RawDiskSignature = device.DiskSignature
                                        view.NativePropertyDiskOffline = device.DiskOffline
                                        view.NativePropertyDiskOReadOnly = device.DiskReadOnly
                                        Dim part = device.PartitionInformationEx
                                        If part.HasValue Then
                                            Select Case part.Value.PartitionStyle
                                                Case NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE.PARTITION_STYLE_MBR
                                                    view.NativePartitionLayout = PSDisk.PSDiskParser.PartitionStyle.MBR
                                                Case NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE.PARTITION_STYLE_GPT
                                                    view.NativePartitionLayout = PSDisk.PSDiskParser.PartitionStyle.GPT
                                                Case NativeFileIO.Win32API.PARTITION_INFORMATION_EX.PARTITION_STYLE.PARTITION_STYLE_RAW
                                                    view.NativePartitionLayout = PSDisk.PSDiskParser.PartitionStyle.RAW
                                            End Select
                                        End If
                                    End Using

                                Catch ex As Exception
                                    Trace.WriteLine("Error reading signature from MBR for drive " & view.DriveNumber.Value.ToString() & ": " & ex.ToString())

                                End Try

                                Try
                                    view.Volumes = NativeFileIO.GetDiskVolumes(view.DriveNumber.Value).ToArray()
                                    view.MountPoints = view.Volumes.SelectMany(AddressOf NativeFileIO.GetVolumeMountPoints).ToArray()

                                Catch ex As Exception
                                    Trace.WriteLine("Error enumerating volumes for drive " & view.DriveNumber.Value.ToString() & ": " & ex.ToString())

                                End Try
                            End If

                            Return view
                        End Function)

            Catch ex As Exception When _
                Function()
                    Trace.WriteLine("Exception in GetFullView: " & ex.ToString())

                    Return False
                End Function()

                Return Nothing
            End Try

        End Function


#Region "IDisposable Support"
        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not Me.disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                For Each obj In New IDisposable() {PSPhysicalDiskParser, PSDiskParser}
                    If obj IsNot Nothing Then
                        obj.Dispose()
                    End If
                Next

                ' TODO: set large fields to null.
            End If
            Me.disposedValue = True
        End Sub

        ' TODO: override Finalize() only if Dispose(ByVal disposing As Boolean) above has code to free unmanaged resources.
        Protected Overrides Sub Finalize()
            ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
            Dispose(False)
            MyBase.Finalize()
        End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
#End Region

    End Class

End Namespace
