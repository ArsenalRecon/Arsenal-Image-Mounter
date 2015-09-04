Public MustInherit Class DiscUtilsInteraction

    Private Sub New()
    End Sub

    Public Shared Sub CreateRamDisk(Adapter As ScsiAdapter, DiskSize As Long, ByRef DeviceNumber As UInteger)

        Adapter.CreateDevice(DiskSize, 0, 0, DeviceFlags.FileTypeAwe, Nothing, False, DeviceNumber)

        Using device = Adapter.OpenDevice(DeviceNumber, FileAccess.ReadWrite)

            Dim stream = device.GetRawDiskStream()

            Dim win32_geometry = device.Geometry
            Dim geometry As New Geometry(
                device.DiskSize,
                win32_geometry.TracksPerCylinder,
                win32_geometry.SectorsPerTrack,
                win32_geometry.BytesPerSector)

            Dim mbr As New Partitions.BiosPartitionedDiskBuilder(device.DiskSize, geometry)

            mbr.PartitionTable.CreatePrimaryBySector((1 << 20) \ geometry.BytesPerSector,
                                                     (device.DiskSize - (1 << 20)) \ geometry.BytesPerSector,
                                                     7, True)

            'Using volume = mbr.PartitionTable(0).Open()

            '    Ntfs.NtfsFileSystem.Format(volume, "RAM disk", geometry, 0,
            '                               (device.DiskSize - (2 << 20)) \ geometry.BytesPerSector)

            'End Using

            mbr.Build(stream)

            device.UpdateProperties()

        End Using

    End Sub

End Class
