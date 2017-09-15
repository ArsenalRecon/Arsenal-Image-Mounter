Imports Arsenal.ImageMounter.IO
Imports Arsenal.ImageMounter.Extensions
Imports System.Windows.Forms

Public MustInherit Class DiscUtilsInteraction

    Private Sub New()
    End Sub

    Public Shared Sub CreateRamDisk(Owner As IWin32Window, Adapter As ScsiAdapter, DiskSize As Long, ByRef DeviceNumber As UInteger)

        Adapter.CreateDevice(DiskSize, 0, 0, DeviceFlags.FileTypeAwe, Nothing, False, DeviceNumber)

        Dim created_device = DeviceNumber

        Try
            Using device = Adapter.OpenDevice(DeviceNumber, FileAccess.ReadWrite)

                device.DiskOffline = True
                device.DiskReadOnly = False

                Dim win32_geometry = device.Geometry
                Dim geometry As New Geometry(
                    device.DiskSize,
                    win32_geometry.TracksPerCylinder,
                    win32_geometry.SectorsPerTrack,
                    win32_geometry.BytesPerSector)

                Using disk As New Raw.Disk(device.GetRawDiskStream(), Ownership.None, geometry)

                    Partitions.BiosPartitionTable.Initialize(disk).CreatePrimaryBySector(
                        first:=(1 << 20) \ geometry.BytesPerSector,
                        last:=(device.DiskSize - (1 << 20)) \ geometry.BytesPerSector,
                        type:=7,
                        markActive:=True)

                    'Fat.FatFileSystem.FormatPartition(disk, 0, "RAM disk")

                    Ntfs.NtfsFileSystem.Format(disk.Partitions(0).Open(), "RAM disk", geometry, 0,
                                               (device.DiskSize - (2 << 20)) \ geometry.BytesPerSector)

                End Using

                Try
                    device.DiskOffline = False

                    device.UpdateProperties()

                    For Each volume In NativeFileIO.GetDiskVolumes(device.DevicePath)

                        Dim mountPoints = NativeFileIO.GetVolumeMountPoints(volume)

                        If mountPoints.Length = 0 Then

                            Dim driveletter = NativeFileIO.FindFirstFreeDriveLetter()

                            If driveletter <> Nothing Then

                                Dim mountPoint = driveletter & ":\"

                                NativeFileIO.SetVolumeMountPoint(mountPoint, volume)

                                mountPoints = {mountPoint}

                            End If

                        End If

                        Array.ForEach(mountPoints, AddressOf Process.Start)

                    Next

                Catch ex As Exception
                    Dim errmsg = ex.JoinMessages()

                    MessageBox.Show(
                            Owner, errmsg, "Arsenal Image Mounter",
                            MessageBoxButtons.OK, MessageBoxIcon.Exclamation)

                End Try

            End Using

        Catch When Function()
                       Adapter.RemoveDevice(created_device)
                       Return False
                   End Function()

        End Try

    End Sub

End Class
