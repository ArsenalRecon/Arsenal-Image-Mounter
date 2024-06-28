//  DiskDevice.vb
//  Class for controlling Arsenal Image Mounter Disk Devices.
//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;
using Arsenal.ImageMounter.IO.Streams;
using LTRData.Extensions.Buffers;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator
#pragma warning disable CS9191 // The 'ref' modifier for an argument corresponding to 'in' parameter is equivalent to 'in'. Consider using 'in' instead.

namespace Arsenal.ImageMounter.IO.Devices;

/// <summary>
/// Represents disk objects, attached to a virtual or physical SCSI adapter.
/// </summary>
public class DiskDevice : DeviceObject
{

    private DiskStream? rawDiskStream;

    private SCSI_ADDRESS? cachedAddress;

    /// <summary>
    /// Returns the device path used to open this device object, if opened by name.
    /// If the object was opened in any other way, such as by supplying an already
    /// open handle, this property returns null/Nothing.
    /// </summary>
    public string DevicePath { get; }

    private void AllowExtendedDasdIo()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        if (!NativeFileIO.UnsafeNativeMethods.DeviceIoControl(SafeFileHandle, NativeConstants.FSCTL_ALLOW_EXTENDED_DASD_IO, 0, 0U, 0, 0U, out _, 0))
        {
            var errcode = Marshal.GetLastWin32Error();
            if (errcode is not NativeConstants.ERROR_INVALID_PARAMETER and not NativeConstants.ERROR_INVALID_FUNCTION)
            {

                Trace.WriteLine($"FSCTL_ALLOW_EXTENDED_DASD_IO failed for '{DevicePath}': {errcode}");
            }
        }
    }

    protected internal DiskDevice(KeyValuePair<string, SafeFileHandle> deviceNameAndHandle, FileAccess accessMode)
        : base(deviceNameAndHandle.Value, accessMode)
    {
        DevicePath = deviceNameAndHandle.Key;

        AllowExtendedDasdIo();
    }

    /// <summary>
    /// Opens an disk device object without requesting read or write permissions. The
    /// resulting object can only be used to query properties like SCSI address, disk
    /// size and similar, but not for reading or writing raw disk data.
    /// </summary>
    /// <param name="devicePath"></param>
    public DiskDevice(string devicePath)
        : base(devicePath)
    {
        this.DevicePath = devicePath;

        AllowExtendedDasdIo();
    }

    /// <summary>
    /// Opens an disk device object, requesting read, write or both permissions.
    /// </summary>
    /// <param name="DevicePath"></param>
    /// <param name="AccessMode"></param>
    public DiskDevice(string DevicePath, FileAccess AccessMode)
        : base(DevicePath, AccessMode)
    {
        this.DevicePath = DevicePath;

        AllowExtendedDasdIo();
    }

    /// <summary>
    /// Opens an disk device object.
    /// </summary>
    /// <param name="scsiAddress"></param>
    /// <param name="accessMode"></param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public DiskDevice(SCSI_ADDRESS scsiAddress, FileAccess accessMode)
        : this(NativeFileIO.OpenDiskByScsiAddress(scsiAddress, accessMode), accessMode)
    {
    }

    /// <summary>
    /// Retrieves device number for this disk on the owner SCSI adapter.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public uint DeviceNumber
    {
        get
        {
            if (cachedAddress is null)
            {
                var scsi_address = ScsiAddress
                    ?? throw new KeyNotFoundException("Cannot find SCSI address for this instance");

                using var driver = new ScsiAdapter(scsi_address.PortNumber);

                cachedAddress = scsi_address;
            }

            return cachedAddress.Value.DWordDeviceNumber;
        }
    }

    /// <summary>
    /// Retrieves SCSI address for this disk.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public SCSI_ADDRESS? ScsiAddress => NativeFileIO.GetScsiAddress(SafeFileHandle);

    /// <summary>
    /// Retrieves storage device type and physical disk number information.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public STORAGE_DEVICE_NUMBER? StorageDeviceNumber => NativeFileIO.GetStorageDeviceNumber(SafeFileHandle);

    /// <summary>
    /// Retrieves StorageStandardProperties information.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public StorageStandardProperties? StorageStandardProperties => NativeFileIO.GetStorageStandardProperties(SafeFileHandle);

    /// <summary>
    /// Retrieves TRIM enabled information.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public bool? TrimEnabled => NativeFileIO.GetStorageTrimProperties(SafeFileHandle);

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void TrimRange(long startingOffset, ulong lengthInBytes)
        => NativeCalls.TrimDiskRange(SafeFileHandle, startingOffset, lengthInBytes);

    /// <summary>
    /// Enumerates disk volumes that use extents of this disk.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public IEnumerable<string>? EnumerateDiskVolumes()
    {
        var disk_number = StorageDeviceNumber;

        if (!disk_number.HasValue)
        {
            return null;
        }

        Trace.WriteLine($"Found disk number: {disk_number.Value.DeviceNumber}");

        return NativeFileIO.EnumerateDiskVolumes(disk_number.Value.DeviceNumber);
    }

    /// <summary>
    /// Opens SCSI adapter that created this virtual disk.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public ScsiAdapter OpenAdapter()
        => new(NativeFileIO.GetScsiAddress(SafeFileHandle)?.PortNumber
            ?? throw new KeyNotFoundException("Cannot find SCSI adapter for this instance"));

    /// <summary>
    /// Updates disk properties by re-enumerating partition table.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void UpdateProperties()
        => NativeFileIO.UpdateDiskProperties(SafeFileHandle, throwOnFailure: true);

    /// <summary>
    /// Retrieves the physical location of a specified volume on one or more disks. 
    /// </summary>
    /// <returns></returns>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public DiskExtent[] GetVolumeDiskExtents()
        => NativeFileIO.GetVolumeDiskExtents(SafeFileHandle);

    /// <summary>
    /// Gets or sets disk signature stored in boot record.
    /// </summary>
    public uint? DiskSignature
    {
        get
        {
            var bytesPerSector = Geometry?.BytesPerSector ?? 512;
            
            byte[]? allocated = null;
            
            var rawsig = bytesPerSector <= 1024
                ? stackalloc byte[bytesPerSector]
                : (allocated = ArrayPool<byte>.Shared.Rent(bytesPerSector)).AsSpan(0, bytesPerSector);
            
            try
            {
                Read(rawsig, 0);

                return MemoryMarshal.Read<ushort>(rawsig.Slice(0x1FE)) == 0xAA55
                    && rawsig[0x1C2] != 0xEE
                    && (rawsig[0x1BE] & 0x7F) == 0
                    && (rawsig[0x1CE] & 0x7F) == 0
                    && (rawsig[0x1DE] & 0x7F) == 0
                    && (rawsig[0x1EE] & 0x7F) == 0
                    ? MemoryMarshal.Read<uint>(rawsig.Slice(0x1B8))
                    : (uint?)default;
            }
            finally
            {
                if (allocated is not null)
                {
                    ArrayPool<byte>.Shared.Return(allocated);
                }
            }
        }
        set
        {
            if (!value.HasValue)
            {
                return;
            }

            var bytesPerSector = Geometry?.BytesPerSector ?? 512;

            byte[]? allocated = null;

            var rawsig = bytesPerSector <= 1024
                ? stackalloc byte[bytesPerSector]
                : (allocated = ArrayPool<byte>.Shared.Rent(bytesPerSector)).AsSpan(0, bytesPerSector);

            try
            {
                Read(rawsig, 0);
                var argvalue = value.Value;
                MemoryMarshal.Write(rawsig.Slice(0x1B8), ref argvalue);
                Write(rawsig, 0);
            }
            finally
            {
                if (allocated is not null)
                {
                    ArrayPool<byte>.Shared.Return(allocated);
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets disk signature stored in boot record.
    /// </summary>
    public uint? VBRHiddenSectorsCount
    {
        get
        {
            var bytesPerSector = Geometry?.BytesPerSector ?? 512;

            byte[]? allocated = null;

            var rawsig = bytesPerSector <= 1024
                ? stackalloc byte[bytesPerSector]
                : (allocated = ArrayPool<byte>.Shared.Rent(bytesPerSector)).AsSpan(0, bytesPerSector);

            try
            {
                Read(rawsig, 0);

                return MemoryMarshal.Read<ushort>(rawsig.Slice(0x1FE)) == 0xAA55
                    ? MemoryMarshal.Read<uint>(rawsig.Slice(0x1C))
                    : (uint?)default;
            }
            finally
            {
                if (allocated is not null)
                {
                    ArrayPool<byte>.Shared.Return(allocated);
                }
            }
        }
        set
        {
            if (!value.HasValue)
            {
                return;
            }

            var bytesPerSector = Geometry?.BytesPerSector ?? 512;

            byte[]? allocated = null;

            var rawsig = bytesPerSector <= 1024
                ? stackalloc byte[bytesPerSector]
                : (allocated = ArrayPool<byte>.Shared.Rent(bytesPerSector)).AsSpan(0, bytesPerSector);

            try
            {
                Read(rawsig, 0);
                var argvalue = value.Value;
                MemoryMarshal.Write(rawsig.Slice(0x1C), ref argvalue);
                Write(rawsig, 0);
            }
            finally
            {
                if (allocated is not null)
                {
                    ArrayPool<byte>.Shared.Return(allocated);
                }
            }
        }
    }

    /// <summary>
    /// Gets number of bytes per cluster for file system on a disk volume represented
    /// by this object. If this object does not represent a disk volume with a file,
    /// system, null is returned.
    /// </summary>
    /// <returns>Number of bytes per cluster for file system on a disk volume represented
    /// by this object. If this object does not represent a disk volume with a file,
    /// system, null is returned</returns>
    public FileSystemClusterSizes? GetFileSystemClusterSizes()
        => NativeFileIO.GetDiskFreeSpace($"{DevicePath}\\",
                                         out var sectorsPerCluster,
                                         out var bytesPerSector,
                                         out var numberOfFreeClusters,
                                         out var totalNumberOfClusters)
        ? new(bytesPerSector,
              sectorsPerCluster,
              numberOfFreeClusters,
              totalNumberOfClusters)
        : null;

    /// <summary>
    /// Gets allocation bitmap for file system on disk volume represented by this object.
    /// </summary>
    public Memory<byte> GetFileSystemAllocationBitmap(out FileSystemClusterSizes clusterSizes)
    {
        var clusterSizesResult = GetFileSystemClusterSizes();

        if (!clusterSizesResult.HasValue)
        {
            clusterSizes = default;
            return default;
        }

        clusterSizes = clusterSizesResult.Value;

        var startingCluster = 0L;

        if (NativeFileIO.GetAllocationBitmap(SafeFileHandle,
                                             ref startingCluster,
                                             clusterSizes.TotalNumberOfClusters,
                                             out var bitmap))
        {
            return bitmap;
        }

        return default;
    }

    /// <summary>
    /// Reads first sector of disk or disk volume
    /// </summary>
    public byte[]? ReadBootSector()
    {
        var bootsect = new byte[Geometry?.BytesPerSector ?? 512];
        int bytesread;

        bytesread = ReadBootSector(bootsect);

        if (bytesread < 512)
        {
            return null;
        }

        if (bytesread != bootsect.Length)
        {
            Array.Resize(ref bootsect, bytesread);
        }

        return bootsect;
    }

    public int Read(Span<byte> buffer, long offset)
    {
#if NET6_0_OR_GREATER
        return RandomAccess.Read(SafeFileHandle, buffer, offset);
#else
        var stream = GetRawDiskStream();
        stream.Position = offset;
        var bytesread = stream.Read(buffer);
        return bytesread;
#endif
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, long offset, CancellationToken cancellationToken)
    {
#if NET6_0_OR_GREATER
        return RandomAccess.ReadAsync(SafeFileHandle, buffer, offset, cancellationToken);
#else
        var stream = GetRawDiskStream();
        stream.Position = offset;
        var bytesread = stream.ReadAsync(buffer, cancellationToken);
        return bytesread;
#endif
    }

    public void Write(ReadOnlySpan<byte> buffer, long offset)
    {
#if NET6_0_OR_GREATER
        RandomAccess.Write(SafeFileHandle, buffer, offset);
#else
        var stream = GetRawDiskStream();
        stream.Position = offset;
        stream.Write(buffer);
#endif
    }

    public ValueTask WriteAsync(Memory<byte> buffer, long offset, CancellationToken cancellationToken)
    {
#if NET6_0_OR_GREATER
        return RandomAccess.WriteAsync(SafeFileHandle, buffer, offset, cancellationToken);
#else
        var stream = GetRawDiskStream();
        stream.Position = offset;
        return stream.WriteAsync(buffer, cancellationToken);
#endif
    }

    private int ReadBootSector(Span<byte> bootsect)
        => Read(bootsect, 0);

    /// <summary>
    /// Return a value indicating whether present sector 0 data indicates a valid MBR
    /// with a partition table.
    /// </summary>
    public bool HasValidPartitionTable
    {
        get
        {
            var bytesPerSector = Geometry?.BytesPerSector ?? 512;

            byte[]? allocated = null;

            var bootsect = bytesPerSector <= 1024
                ? stackalloc byte[bytesPerSector]
                : (allocated = ArrayPool<byte>.Shared.Rent(bytesPerSector)).AsSpan(0, bytesPerSector);

            try
            {
                return ReadBootSector(bootsect) >= 512
                && MemoryMarshal.Read<ushort>(bootsect.Slice(0x1FE)) == 0xAA55
                && (bootsect[0x1BE] & 0x7F) == 0
                && (bootsect[0x1CE] & 0x7F) == 0
                && (bootsect[0x1DE] & 0x7F) == 0
                && (bootsect[0x1EE] & 0x7F) == 0;
            }
            finally
            {
                if (allocated is not null)
                {
                    ArrayPool<byte>.Shared.Return(allocated);
                }
            }
        }
    }

    /// <summary>
    /// Return a value indicating whether present sector 0 data indicates a valid MBR
    /// with a partition table and not blank or fake boot code.
    /// </summary>
    public bool HasValidBootCode
    {
        get
        {
            var bytesPerSector = Geometry?.BytesPerSector ?? 512;

            byte[]? allocated = null;

            var bootsect = bytesPerSector <= 1024
                ? stackalloc byte[bytesPerSector]
                : (allocated = ArrayPool<byte>.Shared.Rent(bytesPerSector)).AsSpan(0, bytesPerSector);

            try
            {
                return ReadBootSector(bootsect) >= 512
                && bootsect[0] != 0 &&
                !bootsect.Slice(0, NativeConstants.DefaultBootCode.Length)
                    .SequenceEqual(NativeConstants.DefaultBootCode.Span)
                && MemoryMarshal.Read<ushort>(bootsect.Slice(0x1FE)) == 0xAA55
                && (bootsect[0x1BE] & 0x7F) == 0
                && (bootsect[0x1CE] & 0x7F) == 0
                && (bootsect[0x1DE] & 0x7F) == 0
                && (bootsect[0x1EE] & 0x7F) == 0;
            }
            finally
            {
                if (allocated is not null)
                {
                    ArrayPool<byte>.Shared.Return(allocated);
                }
            }
        }
    }

    /// <summary>
    /// Flush buffers for a disk or volume.
    /// </summary>
    public void FlushBuffers()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (rawDiskStream is not null)
            {
                rawDiskStream.Flush();
            }
            else
            {
                NativeFileIO.FlushBuffers(SafeFileHandle);
            }
        }
        else
        {
            GetRawDiskStream().Flush();
        }
    }

    /// <summary>
    /// Gets or sets physical disk offline attribute. Only valid for
    /// physical disk objects, not volumes or partitions.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public bool? DiskPolicyOffline
    {
        get => NativeFileIO.GetDiskOffline(SafeFileHandle);
        set
        {
            if (value.HasValue)
            {
                NativeFileIO.SetDiskOffline(SafeFileHandle, value.Value);
            }
        }
    }

    /// <summary>
    /// Gets or sets physical disk read only attribute. Only valid for
    /// physical disk objects, not volumes or partitions.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public bool? DiskPolicyReadOnly
    {
        get => NativeFileIO.GetDiskReadOnly(SafeFileHandle);
        set
        {
            if (value.HasValue)
            {
                NativeFileIO.SetDiskReadOnly(SafeFileHandle, value.Value);
            }
        }
    }

    /// <summary>
    /// Sets disk volume offline attribute. Only valid for logical
    /// disk volumes, not physical disk drives.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void SetVolumeOffline(bool offline)
        => NativeFileIO.SetVolumeOffline(SafeFileHandle, offline);

    /// <summary>
    /// Gets information about a partition stored on a disk with MBR
    /// partition layout. This property is not available for physical
    /// disks, only disk partitions are supported.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public PARTITION_INFORMATION? PartitionInformation => NativeFileIO.GetPartitionInformation(SafeFileHandle);

    /// <summary>
    /// Gets information about a disk partition. This property is not
    /// available for physical disks, only disk partitions are supported.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public PARTITION_INFORMATION_EX? PartitionInformationEx => NativeFileIO.GetPartitionInformationEx(SafeFileHandle);

    /// <summary>
    /// Gets information about a disk partitions. This property is available
    /// for physical disks, not disk partitions.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public NativeFileIO.DriveLayoutInformationType? DriveLayoutEx
    {
        get => NativeFileIO.GetDriveLayoutEx(SafeFileHandle);
        set => NativeFileIO.SetDriveLayoutEx(SafeFileHandle, value ?? throw new ArgumentNullException(nameof(DriveLayoutEx)));
    }

    /// <summary>
    /// Initialize a raw disk device for use with Windows. This method is available
    /// for physical disks, not disk partitions.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void InitializeDisk(PARTITION_STYLE PartitionStyle)
        => NativeCalls.InitializeDisk(SafeFileHandle, PartitionStyle);

    /// <summary>
    /// Disk identifier string.
    /// </summary>
    /// <returns>8 digit hex string for MBR disks or disk GUID for
    /// GPT disks.</returns>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public string DiskId => (DriveLayoutEx?.ToString()) ?? "(Unknown)";

    /// <summary>
    /// Retrieves properties for an existing virtual disk.
    /// </summary>
    /// <param name="deviceNumber">Device number of virtual disk.</param>
    /// <param name="diskSize">Size of virtual disk.</param>
    /// <param name="bytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
    /// <param name="imageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter or Windows
    /// filesystem drivers.</param>
    /// <param name="flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    /// <param name="filename">Name of disk image file holding storage for file type virtual disk or used to create a
    /// virtual memory type virtual disk.</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void QueryDevice(out uint deviceNumber,
                            out long diskSize,
                            out uint bytesPerSector,
                            out long imageOffset,
                            out DeviceFlags flags,
                            out string? filename)
    {
        var scsi_address = ScsiAddress
            ?? throw new KeyNotFoundException("Cannot find SCSI address for this instance");

        using var adapter = new ScsiAdapter(scsi_address.PortNumber);

        deviceNumber = scsi_address.DWordDeviceNumber;

        adapter.QueryDevice(deviceNumber, out diskSize, out bytesPerSector, out imageOffset, out flags, out filename);
    }

    /// <summary>
    /// Retrieves properties for an existing virtual disk.
    /// </summary>
    /// <param name="deviceNumber">Device number of virtual disk.</param>
    /// <param name="diskSize">Size of virtual disk.</param>
    /// <param name="bytesPerSector">Number of bytes per sector for virtual disk geometry.</param>
    /// <param name="imageOffset">A skip offset if virtual disk data does not begin immediately at start of disk image file.
    /// Frequently used with image formats like Nero NRG which start with a file header not used by Arsenal Image Mounter or Windows
    /// filesystem drivers.</param>
    /// <param name="flags">Flags specifying properties for virtual disk. See comments for each flag value.</param>
    /// <param name="filename">Name of disk image file holding storage for file type virtual disk or used to create a
    /// virtual memory type virtual disk.</param>
    /// <param name="writeOverlayImagefile">Path to differencing file used in write-temporary mode.</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void QueryDevice(out uint deviceNumber,
                            out long diskSize,
                            out uint bytesPerSector,
                            out long imageOffset,
                            out DeviceFlags flags,
                            out string? filename,
                            out string? writeOverlayImagefile)
    {
        var scsi_address = ScsiAddress
            ?? throw new KeyNotFoundException("Cannot find SCSI address for this instance");

        using var adapter = new ScsiAdapter(scsi_address.PortNumber);

        deviceNumber = scsi_address.DWordDeviceNumber;

        adapter.QueryDevice(deviceNumber,
                            out diskSize,
                            out bytesPerSector,
                            out imageOffset,
                            out flags,
                            out filename,
                            out writeOverlayImagefile);
    }

    /// <summary>
    /// Retrieves properties for an existing virtual disk.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public DeviceProperties QueryDevice()
    {
        var scsi_address = ScsiAddress
            ?? throw new KeyNotFoundException("Cannot find SCSI address for this instance");

        using var adapter = new ScsiAdapter(scsi_address.PortNumber);

        return adapter.QueryDevice(scsi_address.DWordDeviceNumber);
    }

    /// <summary>
    /// Removes this virtual disk from adapter.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void RemoveDevice()
    {
        var scsi_address = ScsiAddress
            ?? throw new KeyNotFoundException("Cannot find SCSI address for this instance");

        using var adapter = new ScsiAdapter(scsi_address.PortNumber);

        adapter.RemoveDevice(scsi_address.DWordDeviceNumber);
    }

    private long? diskSize;

    /// <summary>
    /// Retrieves volume size of disk device.
    /// </summary>
    public long? DiskSize => diskSize ??= NativeStruct.GetDiskSize(SafeFileHandle);

    private FILE_FS_FULL_SIZE_INFORMATION? volumeSizeInformation;

    /// <summary>
    /// Retrieves partition information.
    /// </summary>
    /// <returns></returns>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public FILE_FS_FULL_SIZE_INFORMATION? VolumeSizeInformation
        => volumeSizeInformation ??= NativeCalls.GetVolumeSizeInformation(SafeFileHandle);

    /// <summary>
    /// Determines whether disk is writable or read-only.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public bool IsDiskWritable => NativeFileIO.IsDiskWritable(SafeFileHandle);

    private DISK_GEOMETRY? geometry;

    /// <summary>
    /// Returns logical disk geometry. Normally, only the BytesPerSector member
    /// contains data of interest.
    /// </summary>
    public DISK_GEOMETRY? Geometry
        => geometry ??= NativeStruct.GetDiskGeometry(SafeFileHandle);

    /// <summary>
    /// Locks and dismounts filesystem on a volume. Upon successful return, further access to the device
    /// can only be done through this device object instance until it is either closed (disposed) or lock is
    /// released on the underlying handle.
    /// </summary>
    /// <param name="force">Indicates if True that volume should be immediately dismounted even if it
    /// cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
    /// successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void DismountVolumeFilesystem(bool force)
        => NativeFileIO.Win32Try(NativeFileIO.DismountVolumeFilesystem(SafeFileHandle, force));

    /// <summary>
    /// Locks and dismounts filesystem on a volume. Upon successful return, further access to the device
    /// can only be done through this device object instance until it is either closed (disposed) or lock is
    /// released on the underlying handle.
    /// </summary>
    /// <param name="force">Indicates if True that volume should be immediately dismounted even if it
    /// cannot be locked. This causes all open handles to files on the volume to become invalid. If False,
    /// successful lock (no other open handles) is required before attempting to dismount filesystem.</param>
    /// <param name="cancellationToken"></param>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public async Task DismountVolumeFilesystemAsync(bool force, CancellationToken cancellationToken)
        => NativeFileIO.Win32Try(await NativeFileIO.DismountVolumeFilesystemAsync(SafeFileHandle, force, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Get live statistics from write filter driver.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public WriteFilterStatistics? WriteOverlayStatus
        => API.GetWriteOverlayStatus(SafeFileHandle, out var statistics) == NativeConstants.NO_ERROR
        ? (WriteFilterStatistics?)statistics
        : default;

    /// <summary>
    /// Deletes the write overlay image file after use. Also sets the filter driver to
    /// silently ignore flush requests to improve performance when integrity of the write
    /// overlay image is not needed for future sessions.
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public void SetWriteOverlayDeleteOnClose()
    {
        var rc = API.SetWriteOverlayDeleteOnClose(SafeFileHandle);
        if (rc != NativeConstants.NO_ERROR)
        {
            throw new Win32Exception(rc);
        }
    }

    /// <summary>
    /// Returns an DiskStream object that can be used to directly access disk data.
    /// The returned stream automatically sector-aligns I/O.
    /// </summary>
    public DiskStream GetRawDiskStream()
    {
        rawDiskStream ??= new DiskStream(SafeFileHandle, AccessMode == 0 ? FileAccess.Read : AccessMode);

        return rawDiskStream;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            rawDiskStream?.Dispose();
        }

        rawDiskStream = null;

        base.Dispose(disposing);
    }
}
