Imports Arsenal.ImageMounter.IO
Imports Arsenal.ImageMounter.Extensions
Imports System.Windows.Forms
Imports System.Security.AccessControl
Imports System.Security.Principal

Namespace Server.Interaction

    Public MustInherit Class DiscUtilsInteraction

        Private Sub New()
        End Sub

        Public Shared Function InteractiveCreateRAMDisk(owner As IWin32Window, adapter As ScsiAdapter) As Boolean

            Dim DeviceNumber As UInteger = ScsiAdapter.AutoDeviceNumber

            Dim ssize = Microsoft.VisualBasic.InputBox("Enter size in MB", "RAM disk", "0")

            Dim size As Long
            If Not Long.TryParse(ssize, size) Then
                Return False
            End If

            Dim Flags As DeviceFlags = 0

            Using handle = NativeFileIO.Win32API.
            CreateFile("\\?\awealloc",
                       NativeFileIO.Win32API.FILE_READ_ATTRIBUTES,
                       0,
                       IntPtr.Zero,
                       NativeFileIO.Win32API.OPEN_EXISTING,
                       NativeFileIO.Win32API.FILE_ATTRIBUTE_NORMAL,
                       IntPtr.Zero)

                If Not handle.IsInvalid Then
                    Flags = Flags Or DeviceFlags.FileTypeAwe
                End If
            End Using

            CreateRamDisk(owner, adapter, size << 20, Flags, DeviceNumber)

            Return True

        End Function

        Public Shared Sub CreateRamDisk(Owner As IWin32Window, Adapter As ScsiAdapter, DiskSize As Long, Flags As DeviceFlags, ByRef DeviceNumber As UInteger)

            Adapter.CreateDevice(DiskSize, 0, 0, Flags, Nothing, False, DeviceNumber)

            Dim created_device = DeviceNumber

            Try
                Using device = Adapter.OpenDevice(DeviceNumber, FileAccess.ReadWrite)

                    device.DiskOffline = True
                    device.DiskReadOnly = False

                    Dim kernel_geometry = device.Geometry
                    Dim discutils_geometry As New Geometry(
                        device.DiskSize,
                        kernel_geometry.TracksPerCylinder,
                        kernel_geometry.SectorsPerTrack,
                        kernel_geometry.BytesPerSector)

                    Using disk As New Raw.Disk(device.GetRawDiskStream(), Ownership.None, discutils_geometry)

                        Partitions.BiosPartitionTable.Initialize(disk).CreatePrimaryBySector(
                            first:=(1 << 20) \ discutils_geometry.BytesPerSector,
                            last:=(device.DiskSize - (1 << 20)) \ discutils_geometry.BytesPerSector,
                            type:=7,
                            markActive:=True)

                        'Fat.FatFileSystem.FormatPartition(disk, 0, "RAM disk")

                        Using fs = Ntfs.NtfsFileSystem.Format(disk.Partitions(0).Open(), "RAM disk", discutils_geometry, 0,
                                                   (device.DiskSize - (2 << 20)) \ discutils_geometry.BytesPerSector)

                            fs.SetSecurity("\", New RawSecurityDescriptor("O:LAG:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;FA;;;CO)(A;OICI;FA;;;WD)"))

                        End Using

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
                                MessageBoxButtons.OK, MessageBoxIcon.Error)

                    End Try

                End Using

            Catch When Function()
                           Adapter.RemoveDevice(created_device)
                           Return False
                       End Function()

            End Try

        End Sub

    End Class

End Namespace
