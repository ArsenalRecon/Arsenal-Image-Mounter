using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using System.IO;
using System.Linq;
using Xunit;

namespace Arsenal.ImageMounter.Tests;

public class Win32API
{
    [Fact]
    public void QueryDosDevice()
    {
        var deviceLinks = NativeFileIO.QueryDosDevice();

        Assert.True(deviceLinks.Any());

        var target = NativeFileIO.QueryDosDevice("C:");

        Assert.NotNull(target);
    }

    [Fact]
    public void EnumeratePhysicalDisks()
    {
        var rc = NativeFileIO.EnumerateDeviceInstancesForSetupClass(NativeConstants.DiskDriveGuid, out var disks, out var errorCode);

        Assert.True(rc, $"CR error code: {errorCode}");
        Assert.NotNull(disks);

        var array = disks.ToArray();

        Assert.NotEmpty(array);
    }

    [Fact]
    public void CheckVerifyDisk()
    {
        using var disk = new DiskDevice(@"\\?\PhysicalDrive0", 0);

        var result = disk.CheckVerify;

        Assert.True(result);
    }

    [Fact]
    public void QueryDefaultReparsePoints()
    {
        var (TargetPath, DisplayName, Flags) = NativeFileIO.QueryDirectoryJunction(@"C:\Users\All Users");

        Assert.Equal(@"\??\C:\ProgramData", TargetPath);
        Assert.Equal(@"C:\ProgramData", DisplayName);
        Assert.Equal(SymlinkFlags.FullPath, Flags);

        (TargetPath, DisplayName, Flags) = NativeFileIO.QueryDirectoryJunction(@"C:\Users\Default User");

        Assert.Equal(@"\??\C:\Users\Default", TargetPath);
        Assert.Equal(@"C:\Users\Default", DisplayName);
        Assert.Equal(SymlinkFlags.FullPath, Flags);
    }

    [Fact]
    public void PartitionTests()
    {
        using var disk = new DiskDevice(@"\\?\C:", FileAccess.Read);

        var driveLayoutEx = disk.DriveLayoutEx;

        Assert.NotNull(driveLayoutEx);

        Assert.NotEmpty(driveLayoutEx.Partitions);

        var offset1 = disk.PartitionInformation?.HiddenSectors;

        Assert.NotNull(offset1);

        var offset2 = disk.PartitionInformationEx?.StartingOffset;

        Assert.NotNull(offset2);

        var found = driveLayoutEx.Partitions.Where(p => p.StartingOffset == offset2);

        Assert.Single(found);
    }
}
