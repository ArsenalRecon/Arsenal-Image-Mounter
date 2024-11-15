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

}
