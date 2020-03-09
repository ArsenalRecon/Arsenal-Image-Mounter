Imports Arsenal.ImageMounter.IO
Imports Arsenal.ImageMounter.Extensions
Imports System.Windows.Forms
Imports System.Security.AccessControl
Imports Buffer = System.Buffer
Imports Arsenal.ImageMounter.Devio.Server.Services
Imports DiscUtils.Partitions

Namespace Server.Interaction

    Public MustInherit Class DiscUtilsInteraction

        Private Sub New()
        End Sub

        Shared Sub New()
            DevioServiceFactory.Initialize()
        End Sub

        Public Shared Function InteractiveCreateRAMDisk(adapter As ScsiAdapter) As RAMDiskService

            Dim DeviceNumber As UInteger = ScsiAdapter.AutoDeviceNumber

            Dim strsize = Microsoft.VisualBasic.InputBox("Enter size in MB", "RAM disk", "0")

            Dim size_mb As Long
            If Not Long.TryParse(strsize, size_mb) Then
                Return Nothing
            End If

            Dim ramdisk As New RAMDiskService(adapter, size_mb << 20, RAMDiskFileSystem.NTFS)

            If ramdisk.MountPoint IsNot Nothing Then
                Try
                    Process.Start(ramdisk.MountPoint)

                Catch ex As Exception
                    MessageBox.Show($"Failed to open Explorer window for created RAM disk: {ex.JoinMessages()}")

                End Try
            End If

            Return ramdisk

        End Function

        Public Shared Property RAMDiskPartitionOffset As Integer = 65536
        Public Shared Property RAMDiskEndOfDiskFreeSpace As Integer = 1 << 20

        Public Enum RAMDiskFileSystem
            None
            NTFS
            FAT
        End Enum

        Public Class RAMDiskService
            Inherits DevioNoneService

            Public ReadOnly Property Volume As String

            Public ReadOnly Property MountPoint As String

            Public Sub New(Adapter As ScsiAdapter, DiskSize As Long, FormatFileSystem As RAMDiskFileSystem)
                MyBase.New(DiskSize)

                Try
                    StartServiceThreadAndMount(Adapter, 0)

                    Using device = OpenDiskDevice(FileAccess.ReadWrite)

                        device.DiskPolicyReadOnly = False

                        ' Initialize partition table

                        device.DiskPolicyOffline = True

                        Dim kernel_geometry = device.Geometry

                        Dim discutils_geometry As New Geometry(
                            device.DiskSize,
                            kernel_geometry.TracksPerCylinder,
                            kernel_geometry.SectorsPerTrack,
                            kernel_geometry.BytesPerSector)

                        Dim disk As New Raw.Disk(device.GetRawDiskStream(), Ownership.None, discutils_geometry)

                        Dim partition_table = BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsNtfs)

                        ' Format file system

                        Dim partition = partition_table(0)

                        Select Case FormatFileSystem

                            Case RAMDiskFileSystem.NTFS
                                Using fs = Ntfs.NtfsFileSystem.Format(partition.Open(), "RAM disk", discutils_geometry, 0, partition.SectorCount)

                                    fs.SetSecurity("\", New RawSecurityDescriptor("O:LAG:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;FA;;;CO)(A;OICI;FA;;;WD)"))

                                End Using

                            Case RAMDiskFileSystem.FAT
                                Using fs = Fat.FatFileSystem.FormatPartition(partition.Open(), "RAM disk", discutils_geometry, 0, CInt(partition.SectorCount), 0)

                                End Using

                        End Select

                        '' Adjust hidden sectors count

                        Using raw = partition.Open()

                            Dim vbr(0 To disk.SectorSize - 1) As Byte

                            raw.Read(vbr, 0, vbr.Length)

                            Dim newvalue = BitConverter.GetBytes(CUInt(partition.FirstSector))
                            Buffer.BlockCopy(newvalue, 0, vbr, &H1C, newvalue.Length)

                            raw.Position = 0

                            raw.Write(vbr, 0, vbr.Length)

                        End Using

                        device.FlushBuffers()

                        device.DiskPolicyOffline = False

                        Do
                            _Volume = NativeFileIO.EnumerateDiskVolumes(device.DevicePath).FirstOrDefault()

                            If _Volume IsNot Nothing Then
                                Exit Do
                            End If

                            device.UpdateProperties()

                            Thread.Sleep(200)

                        Loop

                        Dim mountPoints = NativeFileIO.GetVolumeMountPoints(_Volume)

                        If mountPoints.Length = 0 Then

                            Dim driveletter = NativeFileIO.FindFirstFreeDriveLetter()

                            If driveletter <> Nothing Then

                                Dim newMountPoint = $"{driveletter}:\"

                                NativeFileIO.SetVolumeMountPoint(newMountPoint, _Volume)

                                mountPoints = {newMountPoint}

                            End If

                        End If

                        _MountPoint = mountPoints.FirstOrDefault()

                        Return

                    End Using

                Catch ex As Exception

                    Dispose()

                    Throw New Exception("Failed to create RAM disk", ex)

                End Try

            End Sub

        End Class

    End Class

End Namespace
