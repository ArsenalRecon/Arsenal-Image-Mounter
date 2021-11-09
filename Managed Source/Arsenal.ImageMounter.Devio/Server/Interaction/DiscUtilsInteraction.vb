Imports System.IO
Imports System.Windows.Forms
Imports Arsenal.ImageMounter.Extensions
Imports Arsenal.ImageMounter.IO
Imports DiscUtils
Imports DiscUtils.Core.WindowsSecurity.AccessControl
Imports DiscUtils.Partitions
Imports Buffer = System.Buffer

Namespace Server.Interaction

    Public MustInherit Class DiscUtilsInteraction

        Public Shared Property RAMDiskPartitionOffset As Integer = 65536

        Public Shared Property RAMDiskEndOfDiskFreeSpace As Integer = 1 << 20

        Private Sub New()
        End Sub

        Public Shared ReadOnly Property DiscUtilsInitialized As Boolean = DevioServiceFactory.DiscUtilsInitialized

        Public Shared Sub InitializeVirtualDisk(disk As VirtualDisk, discutils_geometry As Geometry, partition_style As NativeFileIO.PARTITION_STYLE, file_system As InitializeFileSystem, label As String)

            Dim open_volume As Func(Of Stream)
            Dim first_sector As Long
            Dim sector_count As Long

            Select Case partition_style
                Case NativeFileIO.PARTITION_STYLE.PARTITION_STYLE_MBR
                    Dim partition_table = BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsNtfs)
                    Dim partition = partition_table(0)
                    open_volume = AddressOf partition.Open
                    first_sector = partition.FirstSector
                    sector_count = partition.SectorCount

                Case NativeFileIO.PARTITION_STYLE.PARTITION_STYLE_GPT
                    Dim partition_table = GuidPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsNtfs)
                    Dim partition = partition_table(1)
                    open_volume = AddressOf partition.Open
                    first_sector = partition.FirstSector
                    sector_count = partition.SectorCount

                Case NativeFileIO.PARTITION_STYLE.PARTITION_STYLE_RAW
                    open_volume = Function() disk.Content
                    first_sector = 0
                    sector_count = disk.Capacity \ disk.SectorSize

                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(partition_style))
            End Select

            ' Format file system

            Select Case file_system

                Case InitializeFileSystem.NTFS

                    Using fs = Ntfs.NtfsFileSystem.Format(open_volume(), label, discutils_geometry, 0, sector_count)

                        fs.SetSecurity("\", New RawSecurityDescriptor("O:LAG:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;FA;;;CO)(A;OICI;FA;;;WD)"))

                    End Using

                Case InitializeFileSystem.FAT

                    Using fs = Fat.FatFileSystem.FormatPartition(open_volume(), label, discutils_geometry, 0, CInt(Math.Min(sector_count, Integer.MaxValue)), 0)

                    End Using

                Case InitializeFileSystem.None

                Case Else

                    Throw New NotSupportedException($"File system {file_system} is not currently supported.")

            End Select

            '' Adjust hidden sectors count
            If partition_style = NativeFileIO.PARTITION_STYLE.PARTITION_STYLE_MBR Then

                Using raw = open_volume()

                    Dim vbr(0 To disk.SectorSize - 1) As Byte

                    raw.Read(vbr, 0, vbr.Length)

                    Dim newvalue = BitConverter.GetBytes(CUInt(first_sector))
                    Buffer.BlockCopy(newvalue, 0, vbr, &H1C, newvalue.Length)

                    raw.Position = 0

                    raw.Write(vbr, 0, vbr.Length)

                End Using

            End If

        End Sub

        Public Shared Function InteractiveCreateRAMDisk(adapter As ScsiAdapter) As RAMDiskService

            Dim strsize = InputBox("Enter size in MB", "RAM disk", "0")

            Dim size_mb As Long
            If Not Long.TryParse(strsize, size_mb) Then
                Return Nothing
            End If

            Dim ramdisk As New RAMDiskService(adapter, size_mb << 20, InitializeFileSystem.NTFS)

            If ramdisk.MountPoint IsNot Nothing Then
                Try
                    NativeFileIO.BrowseTo(ramdisk.MountPoint)

                Catch ex As Exception
                    MessageBox.Show($"Failed to open Explorer window for created RAM disk: {ex.JoinMessages()}")

                End Try
            End If

            Return ramdisk

        End Function

    End Class

End Namespace
