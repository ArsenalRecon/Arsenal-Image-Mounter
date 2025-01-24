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

}
