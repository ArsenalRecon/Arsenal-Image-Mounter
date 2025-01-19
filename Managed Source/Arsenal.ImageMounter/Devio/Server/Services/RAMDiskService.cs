//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using DiscUtils;
using DiscUtils.Raw;
using DiscUtils.Streams;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Arsenal.ImageMounter.Devio.Server.Services;

/// <summary>
/// Service class representing kernel implemented RAM disks
/// </summary>
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public class RAMDiskService : DevioNoneService
{
    private readonly InitializeFileSystem? formatFileSystem;

    /// <summary>
    /// Path to volume created at RAM disk
    /// </summary>
    public string? Volume { get; private set; }

    /// <summary>
    /// Mount point created for volume as RAM disk
    /// </summary>
    public string? MountPoint { get; private set; }

    /// <summary>
    /// Handles device created event by creating a volume and
    /// format a file system, if selected when object was created.
    /// </summary>
    /// <param name="e"></param>
    /// <exception cref="NotSupportedException"></exception>
    protected override void OnDiskDeviceCreated(EventArgs e)
    {
        base.OnDiskDeviceCreated(e);

        if (!formatFileSystem.HasValue)
        {
            return;
        }

        using var device = OpenDiskDevice(FileAccess.ReadWrite)
            ?? throw new NotSupportedException("Cannot open disk device associated with this instance");

        device.DiskPolicyReadOnly = false;

        // Initialize partition table

        device.DiskPolicyOffline = true;

        var kernel_geometry = device.Geometry
            ?? throw new NotSupportedException("Failed to get geometry for RAM disk");

        var kernel_disksize = device.DiskSize
            ?? throw new NotSupportedException("Failed to get disk size for RAM disk");

        var discutils_geometry = new Geometry(kernel_disksize, kernel_geometry.TracksPerCylinder, kernel_geometry.SectorsPerTrack, kernel_geometry.BytesPerSector);

        var disk = new Disk(device.GetRawDiskStream(), Ownership.None, discutils_geometry);

        DiscUtilsInteraction.InitializeVirtualDisk(disk, discutils_geometry, PARTITION_STYLE.MBR, formatFileSystem.Value, "RAM disk");

        device.FlushBuffers();

        device.DiskPolicyOffline = false;

        for (; ; )
        {
            Volume = NativeFileIO.EnumerateDiskVolumes(device.DevicePath).FirstOrDefault();

            if (Volume is not null)
            {
                break;
            }

            device.UpdateProperties();

            Thread.Sleep(200);
        }

        var mountPoint = NativeFileIO.EnumerateVolumeMountPoints(Volume).FirstOrDefault();

        if (string.IsNullOrWhiteSpace(mountPoint))
        {
            var driveletter = NativeFileIO.FindFirstFreeDriveLetter();

            if (driveletter != '\0')
            {
                var newMountPoint = $@"{driveletter}:\";

                NativeFileIO.SetVolumeMountPoint(newMountPoint, Volume);

                MountPoint = newMountPoint;
            }
        }
        else
        {
            MountPoint = mountPoint.ToString();
        }
    }

    /// <summary>
    /// Creates a RAM disk at supplied adapter, creates a volume on it and
    /// formats a file system.
    /// </summary>
    /// <param name="adapter"></param>
    /// <param name="diskSize"></param>
    /// <param name="formatFileSystem"></param>
    /// <returns></returns>
    /// <exception cref="IOException">Failed to create RAM disk or format file system</exception>
    public static RAMDiskService Create(ScsiAdapter adapter,
                                        long diskSize,
                                        InitializeFileSystem formatFileSystem)
    {
        var newObj = new RAMDiskService(diskSize, formatFileSystem);

        try
        {
            newObj.StartServiceThreadAndMount(adapter, 0);
        }
        catch (Exception ex)
        {
            newObj.Dispose();

            throw new IOException("Failed to create RAM disk", ex);
        }

        return newObj;
    }

    /// <summary>
    /// Creates a service object that can later be "started" to create a RAM disk
    /// with specified size and file system.
    /// </summary>
    /// <param name="diskSize"></param>
    /// <param name="formatFileSystem"></param>
    public RAMDiskService(long diskSize, InitializeFileSystem? formatFileSystem)
        : base(diskSize)
    {
        this.formatFileSystem = formatFileSystem;
    }

    /// <summary>
    /// Creates a service object that can later be "started" to create a RAM disk
    /// from specified template image.
    /// </summary>
    /// <param name="templateImage">VHD image to use as template for RAM disk</param>
    public RAMDiskService(string templateImage)
        : base(templateImage)
    {
    }

    /// <summary>
    /// Creates a RAM disk at supplied adapter, creates a volume on it and
    /// formats a file system.
    /// </summary>
    /// <param name="adapter"></param>
    /// <param name="diskSize"></param>
    /// <param name="formatFileSystem"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="IOException">Failed to create RAM disk or format file system</exception>
    public static async Task<RAMDiskService> CreateAsync(ScsiAdapter adapter,
                                                         long diskSize,
                                                         InitializeFileSystem formatFileSystem,
                                                         CancellationToken cancellationToken)
    {
        var newObj = new RAMDiskService(diskSize, formatFileSystem: null);

        try
        {
            newObj.StartServiceThreadAndMount(adapter, 0);

            using var device = newObj.OpenDiskDevice(FileAccess.ReadWrite)
                ?? throw new NotSupportedException("Cannot open disk device associated with this instance");

            device.DiskPolicyReadOnly = false;

            // Initialize partition table

            device.DiskPolicyOffline = true;

            var kernel_geometry = device.Geometry
                ?? throw new NotSupportedException("Failed to get geometry for RAM disk");

            var kernel_disksize = device.DiskSize
                ?? throw new NotSupportedException("Failed to get disk size for RAM disk");

            var discutils_geometry = new Geometry(kernel_disksize, kernel_geometry.TracksPerCylinder, kernel_geometry.SectorsPerTrack, kernel_geometry.BytesPerSector);

            var disk = new Disk(device.GetRawDiskStream(), Ownership.None, discutils_geometry);

            await DiscUtilsInteraction.InitializeVirtualDiskAsync(disk, discutils_geometry, PARTITION_STYLE.MBR,
                                                                  formatFileSystem, "RAM disk", cancellationToken).ConfigureAwait(false);

            device.FlushBuffers();

            device.DiskPolicyOffline = false;

            for (; ; )
            {
                newObj.Volume = NativeFileIO.EnumerateDiskVolumes(device.DevicePath).FirstOrDefault();

                if (newObj.Volume is not null)
                {
                    break;
                }

                device.UpdateProperties();

                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }

            var mountPoint = NativeFileIO.EnumerateVolumeMountPoints(newObj.Volume).FirstOrDefault();

            if (string.IsNullOrWhiteSpace(mountPoint))
            {
                var driveletter = NativeFileIO.FindFirstFreeDriveLetter();

                if (driveletter != '\0')
                {
                    var newMountPoint = $@"{driveletter}:\";

                    NativeFileIO.SetVolumeMountPoint(newMountPoint, newObj.Volume);

                    newObj.MountPoint = newMountPoint;
                }
            }
            else
            {
                newObj.MountPoint = mountPoint.ToString();
            }

            return newObj;
        }
        catch (Exception ex)
        {
            newObj.Dispose();

            throw new IOException("Failed to create RAM disk", ex);
        }
    }
}
