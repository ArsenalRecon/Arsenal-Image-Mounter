using Arsenal.ImageMounter.Devio.Server.Interaction;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;
using DiscUtils;
using DiscUtils.Core.WindowsSecurity.AccessControl;
using DiscUtils.Partitions;
using DiscUtils.Streams.Compatibility;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter;

public enum InitializeFileSystem
{
    None,
    NTFS,
    FAT
}

public static class DiscUtilsInteraction
{

    public static int RAMDiskPartitionOffset { get; set; } = 65536;

    public static int RAMDiskEndOfDiskFreeSpace { get; set; } = 1 << 20;

    public static bool DiscUtilsInitialized { get; } = DevioServiceFactory.DiscUtilsInitialized;

    public static void InitializeVirtualDisk(VirtualDisk disk, Geometry discutils_geometry, PARTITION_STYLE partition_style, InitializeFileSystem file_system, string? label)
    {

        Func<Stream> open_volume;
        long first_sector;
        long sector_count;

        switch (partition_style)
        {
            case PARTITION_STYLE.MBR:
                {
                    var partition_table = BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsNtfs);
                    var partition = partition_table[0];
                    open_volume = partition.Open;
                    first_sector = partition.FirstSector;
                    sector_count = partition.SectorCount;
                    break;
                }

            case PARTITION_STYLE.GPT:
                {
                    var partition_table = GuidPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsNtfs);
                    var partition = partition_table[1];
                    open_volume = partition.Open;
                    first_sector = partition.FirstSector;
                    sector_count = partition.SectorCount;
                    break;
                }

            case PARTITION_STYLE.RAW:
                {
                    open_volume = new Func<Stream>(() => disk.Content);
                    first_sector = 0L;
                    sector_count = disk.Capacity / disk.SectorSize;
                    break;
                }

            default:
                {
                    throw new ArgumentOutOfRangeException(nameof(partition_style));
                }
        }

        // Format file system

        switch (file_system)
        {

            case InitializeFileSystem.NTFS:
                {

                    using var fs = DiscUtils.Ntfs.NtfsFileSystem.Format(open_volume(), label, discutils_geometry, 0L, sector_count);

                    fs.SetSecurity(@"\", new RawSecurityDescriptor("O:LAG:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;FA;;;CO)(A;OICI;FA;;;WD)"));

                    break;
                }

            case InitializeFileSystem.FAT:
                {

                    using var fs = DiscUtils.Fat.FatFileSystem.FormatPartition(open_volume(), label, discutils_geometry, 0, (int)Math.Min(sector_count, int.MaxValue), 0);

                    break;
                }

            case InitializeFileSystem.None:
                {
                    break;
                }

            default:
                {

                    throw new NotSupportedException($"File system {file_system} is not currently supported.");
                }
        }

        // ' Adjust hidden sectors count
        if (partition_style == PARTITION_STYLE.MBR)
        {

            using var raw = open_volume();

            var sector_size = disk.SectorSize;

            Span<byte> vbr = sector_size <= 512
                ? stackalloc byte[sector_size]
                : new byte[sector_size];

            raw.Read(vbr);

            var argvalue = (uint)first_sector;
            MemoryMarshal.Write(vbr.Slice(0x1C), ref argvalue);

            raw.Position = 0L;

            raw.Write(vbr);
        }
    }

    public static async Task InitializeVirtualDiskAsync(VirtualDisk disk, Geometry discutils_geometry, PARTITION_STYLE partition_style, InitializeFileSystem file_system, string label, CancellationToken cancellationToken)
    {

        Func<Stream> open_volume;
        long first_sector;
        long sector_count;

        switch (partition_style)
        {
            case PARTITION_STYLE.MBR:
                {
                    var partition_table = BiosPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsNtfs);
                    var partition = partition_table[0];
                    open_volume = partition.Open;
                    first_sector = partition.FirstSector;
                    sector_count = partition.SectorCount;
                    break;
                }

            case PARTITION_STYLE.GPT:
                {
                    var partition_table = GuidPartitionTable.Initialize(disk, WellKnownPartitionType.WindowsNtfs);
                    var partition = partition_table[1];
                    open_volume = partition.Open;
                    first_sector = partition.FirstSector;
                    sector_count = partition.SectorCount;
                    break;
                }

            case PARTITION_STYLE.RAW:
                {
                    open_volume = new Func<Stream>(() => disk.Content);
                    first_sector = 0L;
                    sector_count = disk.Capacity / disk.SectorSize;
                    break;
                }

            default:
                {
                    throw new ArgumentOutOfRangeException(nameof(partition_style));
                }
        }

        // Format file system

        switch (file_system)
        {

            case InitializeFileSystem.NTFS:
                {

                    using var fs = DiscUtils.Ntfs.NtfsFileSystem.Format(open_volume(), label, discutils_geometry, 0L, sector_count);

                    fs.SetSecurity(@"\", new RawSecurityDescriptor("O:LAG:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;FA;;;CO)(A;OICI;FA;;;WD)"));

                    break;
                }

            case InitializeFileSystem.FAT:
                {

                    using var fs = DiscUtils.Fat.FatFileSystem.FormatPartition(open_volume(), label, discutils_geometry, 0, (int)Math.Min(sector_count, int.MaxValue), 0);

                    break;
                }

            case InitializeFileSystem.None:
                {
                    break;
                }

            default:
                {

                    throw new NotSupportedException($"File system {file_system} is not currently supported.");
                }
        }

        // ' Adjust hidden sectors count
        if (partition_style == PARTITION_STYLE.MBR)
        {

            using var raw = open_volume();

            var sector_size = disk.SectorSize;

            var vbr = ArrayPool<byte>.Shared.Rent(sector_size);

            try
            {
                await raw.ReadAsync(vbr.AsMemory(0, sector_size), cancellationToken).ConfigureAwait(false);

                var argvalue = (uint)first_sector;
                MemoryMarshal.Write(vbr.AsSpan(0x1C), ref argvalue);

                raw.Position = 0L;

                await raw.WriteAsync(vbr.AsMemory(0, sector_size), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(vbr);
            }
        }
    }
}