using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
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
}
