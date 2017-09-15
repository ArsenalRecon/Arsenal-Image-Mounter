Imports System.Threading.Tasks
Imports Arsenal.ImageMounter.ScsiAdapter
Imports System.Runtime.InteropServices
Imports Arsenal.ImageMounter.PSDisk
Imports Arsenal.ImageMounter.IO

Namespace PSDisk

    Public Class DiskStateParser

        Public Sub New()

        End Sub

        Public Function GetSimpleView(portnumber As Byte, deviceProperties As List(Of DeviceProperties)) As List(Of DiskStateView)

            Try
                Dim ids = NativeFileIO.GetDevicesScsiAddresses(portnumber)

                Dim getid =
                    Function(dev As DeviceProperties) As String
                        Dim result As String = Nothing
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
                            view.DeviceName = getid(dev)

                            If view.DeviceName IsNot Nothing Then
                                Try
                                    view.DevicePath = "\\?\" & view.DeviceName
                                    Using device As New DiskDevice(view.DevicePath, FileAccess.Read)
                                        view.RawDiskSignature = device.DiskSignature
                                        view.NativePropertyDiskOffline = device.DiskOffline
                                        view.NativePropertyDiskOReadOnly = device.DiskReadOnly
                                        If device.PartitionInformationEx.HasValue Then
                                            view.NativePartitionLayout = device.PartitionInformationEx.Value.PartitionStyle
                                        End If
                                    End Using

                                Catch ex As Exception
                                    Trace.WriteLine("Error reading signature from MBR for drive " & view.DevicePath & ": " & ex.ToString())

                                End Try

                                Try
                                    view.Volumes = NativeFileIO.GetDiskVolumes(view.DevicePath).ToArray()
                                    view.MountPoints = view.Volumes.SelectMany(AddressOf NativeFileIO.GetVolumeMountPoints).ToArray()

                                Catch ex As Exception
                                    Trace.WriteLine("Error enumerating volumes for drive " & view.DevicePath & ": " & ex.ToString())

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

    End Class

End Namespace
