//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.Interaction;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using DiscUtils;
using DiscUtils.Core.WindowsSecurity.AccessControl;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator
#pragma warning disable CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.

namespace Arsenal.ImageMounter;

public enum InitializeFileSystem
{
    None,
    NTFS,
    FAT
}

public static class DiscUtilsInteraction
{
    public static int PartitionOffsetMBR { get; set; } = 65536;

    public static int PartitionOffsetGPT { get; set; } = 1 << 20;

    public static bool DiscUtilsInitialized { get; } = DevioServiceFactory.DiscUtilsInitialized;

    public static void InitializeVirtualDisk(VirtualDisk disk,
                                             Geometry discutils_geometry,
                                             PARTITION_STYLE partition_style,
                                             InitializeFileSystem file_system,
                                             string? label)
    {
        Span<byte> mbr = stackalloc byte[512];
        disk.GetMasterBootRecord(mbr);
        NativeConstants.DefaultBootCode.Span.CopyTo(mbr);
        var signature = NativeCalls.GenerateDiskSignature();
        MemoryMarshal.Write(mbr.Slice(0x1B8), ref signature);
        disk.SetMasterBootRecord(mbr);

        Stream volume;
        long first_sector;
        long sector_count;

        switch (partition_style)
        {
            case PARTITION_STYLE.MBR:
                {
                    var partition_table = BiosPartitionTable.Initialize(disk);
                    var partition_number = partition_table.CreateAligned(WellKnownPartitionType.WindowsNtfs, active: true, alignment: PartitionOffsetMBR);
                    var partition = partition_table[partition_number];
                    volume = partition.Open();
                    first_sector = partition.FirstSector;
                    sector_count = partition.SectorCount;
                    break;
                }

            case PARTITION_STYLE.GPT:
                {
                    var partition_table = GuidPartitionTable.Initialize(disk);
                    var partition_number = partition_table.CreateAligned(WellKnownPartitionType.WindowsNtfs, active: true, alignment: PartitionOffsetGPT);
                    var partition = partition_table[partition_number];
                    volume = partition.Open();
                    first_sector = partition.FirstSector;
                    sector_count = partition.SectorCount;
                    break;
                }

            case PARTITION_STYLE.RAW:
                {
                    volume = disk.Content;
                    first_sector = 0;
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
                    using var fs = DiscUtils.Ntfs.NtfsFileSystem.Format(volume, label, discutils_geometry, 0, sector_count);
                    fs.SetSecurity(@"\", new RawSecurityDescriptor("O:LAG:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;FA;;;CO)(A;OICI;FA;;;WD)"));
                    break;
                }

            case InitializeFileSystem.FAT:
                {
                    using var fs = DiscUtils.Fat.FatFileSystem.FormatPartition(volume, label, discutils_geometry, 0, (int)Math.Min(sector_count, int.MaxValue), 0);
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

        // Adjust hidden sectors count
        var sector_size = disk.SectorSize;

        byte[]? allocated = null;

        var vbr = sector_size <= 1024
            ? stackalloc byte[sector_size]
            : (allocated = ArrayPool<byte>.Shared.Rent(sector_size)).AsSpan(0, sector_size);

        try
        {
            volume.Position = 0;

            volume.ReadExactly(vbr);

            NativeConstants.DefaultBootCode.Span.CopyTo(vbr);

            var argvalue = (uint)first_sector;
            MemoryMarshal.Write(vbr.Slice(0x1C), ref argvalue);

            vbr[0x1fe] = 0x55;
            vbr[0x1ff] = 0xaa;

            volume.Position = 0;

            volume.Write(vbr);
        }
        finally
        {
            if (allocated is not null)
            {
                ArrayPool<byte>.Shared.Return(allocated);
            }
        }
    }

    public static async Task InitializeVirtualDiskAsync(VirtualDisk disk,
                                                        Geometry discutils_geometry,
                                                        PARTITION_STYLE partition_style,
                                                        InitializeFileSystem file_system,
                                                        string? label,
                                                        CancellationToken cancellationToken)
    {
        using var mbrMem = MemoryPool<byte>.Shared.Rent(512);
        var mbr = mbrMem.Memory.Slice(0, 512);
        await disk.GetMasterBootRecordAsync(mbr, cancellationToken).ConfigureAwait(false);
        NativeConstants.DefaultBootCode.CopyTo(mbr);
        var signature = NativeCalls.GenerateDiskSignature();
        MemoryMarshal.Write(mbr.Span.Slice(0x1B8), ref signature);
        await disk.SetMasterBootRecordAsync(mbr, cancellationToken).ConfigureAwait(false);

        Stream volume;
        long first_sector;
        long sector_count;

        switch (partition_style)
        {
            case PARTITION_STYLE.MBR:
                {
                    var partition_table = BiosPartitionTable.Initialize(disk);
                    var partition_number = partition_table.CreateAligned(WellKnownPartitionType.WindowsNtfs, active: true, alignment: PartitionOffsetMBR);
                    var partition = partition_table[partition_number];
                    volume = partition.Open();
                    first_sector = partition.FirstSector;
                    sector_count = partition.SectorCount;
                    break;
                }

            case PARTITION_STYLE.GPT:
                {
                    var partition_table = GuidPartitionTable.Initialize(disk);
                    var partition_number = partition_table.CreateAligned(WellKnownPartitionType.WindowsNtfs, active: true, alignment: PartitionOffsetGPT);
                    var partition = partition_table[partition_number];
                    volume = partition.Open();
                    first_sector = partition.FirstSector;
                    sector_count = partition.SectorCount;
                    break;
                }

            case PARTITION_STYLE.RAW:
                volume = disk.Content;
                first_sector = 0;
                sector_count = disk.Capacity / disk.SectorSize;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(partition_style));
        }

        // Format file system
        switch (file_system)
        {
            case InitializeFileSystem.NTFS:
                {
                    using var fs = DiscUtils.Ntfs.NtfsFileSystem.Format(volume, label, discutils_geometry, 0, sector_count);
                    fs.SetSecurity(@"\", new RawSecurityDescriptor("O:LAG:BUD:(A;OICI;FA;;;BA)(A;OICI;FA;;;SY)(A;OICI;FA;;;CO)(A;OICI;FA;;;WD)"));
                    break;
                }

            case InitializeFileSystem.FAT:
                {
                    using var fs = DiscUtils.Fat.FatFileSystem.FormatPartition(volume, label, discutils_geometry, 0, (int)Math.Min(sector_count, int.MaxValue), 0);
                    break;
                }

            case InitializeFileSystem.None:
                break;

            default:
                throw new NotSupportedException($"File system {file_system} is not currently supported.");
        }

        // Adjust hidden sectors count
        var sector_size = disk.SectorSize;

        using var allocated = MemoryPool<byte>.Shared.Rent(sector_size);

        var vbr = allocated.Memory.Slice(0, sector_size);

        volume.Position = 0;

        await volume.ReadAsync(vbr, cancellationToken).ConfigureAwait(false);

        NativeConstants.DefaultBootCode.CopyTo(vbr);

        var argvalue = (uint)first_sector;
        MemoryMarshal.Write(vbr.Span.Slice(0x1C), ref argvalue);

        vbr.Span[0x1fe] = 0x55;
        vbr.Span[0x1ff] = 0xaa;

        volume.Position = 0;

        await volume.WriteAsync(vbr, cancellationToken).ConfigureAwait(false);
    }

    public static DiscUtils.Raw.Disk OpenPhysicalDiskAsDiscUtilsDisk(this DiskDevice diskDevice, Ownership ownsStream)
    {
        try
        {
            var native_geometry = diskDevice.Geometry
                ?? throw new InvalidOperationException("Unknown geometry");

            var native_disk_size = diskDevice.DiskSize
                ?? throw new InvalidOperationException("Unknown size");

            var geometry = new Geometry(native_disk_size, native_geometry.TracksPerCylinder, native_geometry.SectorsPerTrack, native_geometry.BytesPerSector);

            var align_stream = diskDevice.GetRawDiskStream();

            var disk = new DiscUtils.Raw.Disk(align_stream, ownsStream, geometry);

            if (ownsStream == Ownership.Dispose)
            {
                disk.Disposed += (sender, e) => diskDevice.Dispose();
            }

            return disk;
        }
        catch (Exception ex)
        {
            if (ownsStream == Ownership.Dispose)
            {
                diskDevice.Dispose();
            }

            throw new IOException($"Failed to open device '{diskDevice.DevicePath}'", ex);
        }
    }

    public static DiscUtils.Raw.Disk OpenPhysicalDiskAsDiscUtilsDisk(string devicePath, FileAccess access)
        => OpenPhysicalDiskAsDiscUtilsDisk(new DiskDevice(devicePath, access), Ownership.Dispose);
}

