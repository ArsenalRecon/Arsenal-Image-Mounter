using Arsenal.ImageMounter.Devio.Server.Services;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;
using DiscUtils;
using DiscUtils.Raw;
using DiscUtils.Streams;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.Interaction;

public class RAMDiskService : DevioNoneService
{
    public string? Volume { get; }

    public string? MountPoint { get; }

    public RAMDiskService(ScsiAdapter Adapter, long DiskSize, InitializeFileSystem FormatFileSystem)
        : base(DiskSize)
    {

        try
        {
            StartServiceThreadAndMount(Adapter, 0);

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

            DiscUtilsInteraction.InitializeVirtualDisk(disk, discutils_geometry, PARTITION_STYLE.MBR, FormatFileSystem, "RAM disk");

            device.FlushBuffers();

            device.DiskPolicyOffline = false;

            do
            {
                Volume = NativeFileIO.EnumerateDiskVolumes(device.DevicePath).FirstOrDefault();

                if (Volume is not null)
                {
                    break;
                }

                device.UpdateProperties();

                Thread.Sleep(200);
            }

            while (true);

            var mountPoint = NativeFileIO.EnumerateVolumeMountPoints(Volume).FirstOrDefault();

            if (string.IsNullOrWhiteSpace(mountPoint))
            {

                var driveletter = NativeFileIO.FindFirstFreeDriveLetter();

                if (driveletter != '\0')
                {

                    var newMountPoint = $@"{driveletter}:\";

                    NativeFileIO.SetVolumeMountPoint(newMountPoint.AsMemory(), Volume.AsMemory());

                    MountPoint = newMountPoint;

                }
            }

            else
            {

                MountPoint = mountPoint.ToString();

            }

            return;
        }

        catch (Exception ex)
        {

            Dispose();

            throw new Exception("Failed to create RAM disk", ex);

        }
    }

    public static RAMDiskService? InteractiveCreate(IWin32Window? _, ScsiAdapter adapter)
    {

        var strsize = Microsoft.VisualBasic.Interaction.InputBox("Enter size in MB", "RAM disk");

        if (strsize is null || string.IsNullOrWhiteSpace(strsize))
        {
            return null;
        }

        if (!long.TryParse(strsize, out var size_mb) || size_mb <= 0L)
        {
            return null;
        }

        var ramdisk = new RAMDiskService(adapter, size_mb << 20, InitializeFileSystem.NTFS);

        if (ramdisk.MountPoint is not null)
        {
            try
            {
                NativeFileIO.BrowseTo(ramdisk.MountPoint);
            }

            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Explorer window for created RAM disk: {ex.JoinMessages()}");
            }
        }

        return ramdisk;

    }
}
