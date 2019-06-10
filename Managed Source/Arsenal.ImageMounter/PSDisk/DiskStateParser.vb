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

            Return GetSimpleViewSpecial(Of DiskStateView)(portnumber, deviceProperties)

        End Function

        Public Function GetSimpleViewSpecial(Of T As {New, DiskStateView})(portnumber As Byte, deviceProperties As List(Of DeviceProperties)) As List(Of T)

            Try
                Dim ids = NativeFileIO.GetDevicesScsiAddresses(portnumber)

                Dim getid =
                    Function(dev As DeviceProperties) As String
                        Dim scsiaddress As New NativeFileIO.ScsiAddressAndLength(New NativeFileIO.Win32API.SCSI_ADDRESS(portnumber, dev.DeviceNumber), dev.DiskSize)
                        Dim result As String = Nothing
                        If ids.TryGetValue(scsiaddress, result) Then
                            Return result
                        Else
                            Trace.WriteLine("No PhysicalDrive object found for " & scsiaddress.ToString())
                            Return Nothing
                        End If
                    End Function

                Return _
                    deviceProperties.
                    ConvertAll(
                        Function(dev)

                            Dim view As New T With {
                                .DeviceProperties = dev,
                                .DeviceName = getid(dev)
                            }

                            If view.DeviceName IsNot Nothing Then
                                Try
                                    view.DevicePath = $"\\?\{view.DeviceName}"
                                    Using device As New DiskDevice(view.DevicePath, FileAccess.Read)
                                        view.RawDiskSignature = device.DiskSignature
                                        view.FakeDiskSignature = (dev.Flags And DeviceFlags.FakeDiskSignatureIfZero) = DeviceFlags.FakeDiskSignatureIfZero
                                        view.NativePropertyDiskOffline = device.DiskOffline
                                        view.NativePropertyDiskOReadOnly = device.DiskReadOnly
                                        If device.HasValidMBR Then
                                            view.NativePartitionLayout = device.PartitionInformationEx?.PartitionStyle
                                        Else
                                            view.NativePartitionLayout = Nothing
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
