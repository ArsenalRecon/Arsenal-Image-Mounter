Imports System.IO
Imports System.Security.AccessControl
Imports System.Threading
Imports Arsenal.ImageMounter.Devio.Server.Services
Imports Arsenal.ImageMounter.IO
Imports DiscUtils
Imports DiscUtils.Partitions
Imports DiscUtils.Raw
Imports DiscUtils.Streams
Imports Buffer = System.Buffer

Namespace Server.Interaction

    Public Class RAMDiskService
        Inherits DevioNoneService

        Public ReadOnly Property Volume As String

        Public ReadOnly Property MountPoint As String

        Public Sub New(Adapter As ScsiAdapter, DiskSize As Long, FormatFileSystem As InitializeFileSystem)
            MyBase.New(DiskSize)

            Try
                StartServiceThreadAndMount(Adapter, 0)

                Using device = OpenDiskDevice(FileAccess.ReadWrite)

                    device.DiskPolicyReadOnly = False

                    ' Initialize partition table

                    device.DiskPolicyOffline = True

                    Dim kernel_geometry = device.Geometry.Value

                    Dim discutils_geometry As New Geometry(
                        device.DiskSize.Value,
                        kernel_geometry.TracksPerCylinder,
                        kernel_geometry.SectorsPerTrack,
                        kernel_geometry.BytesPerSector)

                    Dim disk As New Raw.Disk(device.GetRawDiskStream(), Ownership.None, discutils_geometry)

                    DiscUtilsInteraction.InitializeVirtualDisk(disk, discutils_geometry, NativeFileIO.PARTITION_STYLE.PARTITION_STYLE_MBR, FormatFileSystem, "RAM disk")

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

    Public Enum InitializeFileSystem
        None
        NTFS
        FAT
    End Enum

End Namespace
