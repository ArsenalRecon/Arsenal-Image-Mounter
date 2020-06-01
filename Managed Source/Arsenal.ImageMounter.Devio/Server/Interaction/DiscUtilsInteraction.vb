Imports System.Security.AccessControl
Imports System.Windows.Forms
Imports Arsenal.ImageMounter.Extensions
Imports DiscUtils.Partitions
Imports Buffer = System.Buffer

Namespace Server.Interaction

    Public MustInherit Class DiscUtilsInteraction

        Public Shared Property RAMDiskPartitionOffset As Integer = 65536

        Public Shared Property RAMDiskEndOfDiskFreeSpace As Integer = 1 << 20

        Private Sub New()
        End Sub

        Shared Sub New()
            DevioServiceFactory.Initialize()
        End Sub

        Public Shared Sub InitializeVirtualDisk(disk As VirtualDisk, discutils_geometry As Geometry, file_system As InitializeFileSystem, label As String)

            Dim partition_table = BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsNtfs)

            ' Format file system

            Dim partition = partition_table(0)

            Select Case file_system

                Case InitializeFileSystem.NTFS

                    Using fs = Ntfs.NtfsFileSystem.Format(partition.Open(), label, discutils_geometry, 0, partition.SectorCount)

                        fs.SetSecurity("\", New RawSecurityDescriptor("O:LAG:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;FA;;;CO)(A;OICI;FA;;;WD)"))

                    End Using

                Case InitializeFileSystem.FAT

                    Using fs = Fat.FatFileSystem.FormatPartition(partition.Open(), label, discutils_geometry, 0, CInt(partition.SectorCount), 0)

                    End Using

                Case InitializeFileSystem.None

                Case Else

                    Throw New NotSupportedException($"File system {file_system} is not currently supported.")

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
        End Sub

        Public Shared Function InteractiveCreateRAMDisk(adapter As ScsiAdapter) As RAMDiskService

            Dim DeviceNumber As UInteger = ScsiAdapter.AutoDeviceNumber

            Dim strsize = Microsoft.VisualBasic.InputBox("Enter size in MB", "RAM disk", "0")

            Dim size_mb As Long
            If Not Long.TryParse(strsize, size_mb) Then
                Return Nothing
            End If

            Dim ramdisk As New RAMDiskService(adapter, size_mb << 20, InitializeFileSystem.NTFS)

            If ramdisk.MountPoint IsNot Nothing Then
                Try
                    Process.Start(ramdisk.MountPoint)

                Catch ex As Exception
                    MessageBox.Show($"Failed to open Explorer window for created RAM disk: {ex.JoinMessages()}")

                End Try
            End If

            Return ramdisk

        End Function

    End Class

End Namespace
