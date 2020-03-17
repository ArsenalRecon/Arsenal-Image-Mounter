Imports System.Security.AccessControl
Imports Arsenal.ImageMounter.Devio.Server.Services
Imports Arsenal.ImageMounter.IO
Imports DiscUtils.Partitions
Imports Buffer = System.Buffer

Namespace Server.Interaction

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

                    Dim kernel_geometry = device.Geometry.Value

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

                        Case RAMDiskFileSystem.None

                        Case Else

                            Throw New NotSupportedException($"File system {FormatFileSystem} is not currently supported.")

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

                    Dim mountPoint = NativeFileIO.EnumerateVolumeMountPoints(_Volume).FirstOrDefault()

                    If mountPoint Is Nothing Then

                        Dim driveletter = NativeFileIO.FindFirstFreeDriveLetter()

                        If driveletter <> Nothing Then

                            Dim newMountPoint = $"{driveletter}:\"

                            NativeFileIO.SetVolumeMountPoint(newMountPoint, _Volume)

                            mountPoint = newMountPoint

                        End If

                    End If

                    _MountPoint = mountPoint

                    Return

                End Using

            Catch ex As Exception

                Dispose()

                Throw New Exception("Failed to create RAM disk", ex)

            End Try

        End Sub

    End Class

    Public Enum RAMDiskFileSystem
        None
        NTFS
        FAT
    End Enum

End Namespace
