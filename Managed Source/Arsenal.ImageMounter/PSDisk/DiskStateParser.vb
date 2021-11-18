Imports System.Threading.Tasks
Imports Arsenal.ImageMounter.ScsiAdapter
Imports System.Runtime.InteropServices
Imports Arsenal.ImageMounter.PSDisk
Imports Arsenal.ImageMounter.IO
Imports Arsenal.ImageMounter.Extensions
Imports System.IO

Namespace PSDisk

    Public NotInheritable Class DiskStateParser

        Private Sub New()

        End Sub

        Public Shared Function GetSimpleView(portnumber As ScsiAdapter, deviceProperties As IEnumerable(Of DeviceProperties)) As IEnumerable(Of DiskStateView)

            Return GetSimpleViewSpecial(Of DiskStateView)(portnumber, deviceProperties)

        End Function

        Public Shared Function GetSimpleViewSpecial(Of T As {New, DiskStateView})(portnumber As ScsiAdapter, deviceProperties As IEnumerable(Of DeviceProperties)) As IEnumerable(Of T)

            Try
                Dim ids = NativeFileIO.GetDevicesScsiAddresses(portnumber)

                Dim getid =
                    Function(dev As DeviceProperties) As String
                        Dim result As String = Nothing
                        If ids.TryGetValue(dev.DeviceNumber, result) Then
                            Return result
                        Else
                            Trace.WriteLine($"No PhysicalDrive object found for device number {dev.DeviceNumber:X6}")
                            Return Nothing
                        End If
                    End Function

                Return _
                    deviceProperties.
                    Select(
                        Function(dev)

                            Dim view As New T With {
                                .DeviceProperties = dev,
                                .DeviceName = getid(dev)
                            }

                            view.FakeDiskSignature = dev.Flags.HasFlag(DeviceFlags.FakeDiskSignatureIfZero)

                            If view.DeviceName IsNot Nothing Then
                                Try
                                    view.DevicePath = $"\\?\{view.DeviceName}"
                                    Using device As New DiskDevice(view.DevicePath, FileAccess.Read)
                                        view.RawDiskSignature = device.DiskSignature
                                        view.NativePropertyDiskOffline = device.DiskPolicyOffline
                                        view.NativePropertyDiskReadOnly = device.DiskPolicyReadOnly
                                        view.StorageDeviceNumber = device.StorageDeviceNumber
                                        Dim drive_layout = device.DriveLayoutEx
                                        view.DiskId = TryCast(drive_layout, NativeFileIO.DriveLayoutInformationGPT)?.GPT.DiskId
                                        If device.HasValidPartitionTable Then
                                            view.NativePartitionLayout = drive_layout?.DriveLayoutInformation.PartitionStyle
                                        Else
                                            view.NativePartitionLayout = Nothing
                                        End If
                                    End Using

                                Catch ex As Exception
                                    Trace.WriteLine($"Error reading signature from MBR for drive {view.DevicePath}: {ex.JoinMessages()}")

                                End Try

                                Try
                                    view.Volumes = NativeFileIO.EnumerateDiskVolumes(view.DevicePath).ToArray()
                                    view.MountPoints = view.Volumes?.SelectMany(AddressOf NativeFileIO.EnumerateVolumeMountPoints).ToArray()

                                Catch ex As Exception
                                    Trace.WriteLine($"Error enumerating volumes for drive {view.DevicePath}: {ex.JoinMessages()}")

                                End Try
                            End If

                            Return view

                        End Function)

            Catch ex As Exception
                Trace.WriteLine($"Exception in GetSimpleView: {ex}")

                Throw New Exception("Exception generating view", ex)

            End Try

        End Function

    End Class

End Namespace
