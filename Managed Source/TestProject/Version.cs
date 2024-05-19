using Arsenal.ImageMounter.IO.Native;
using System.IO;
using Xunit;

namespace Arsenal.ImageMounter.Tests;

public class Version
{
    [Fact]
    public void CheckVersion()
    {
        var ver = new NativeFileVersion(File.ReadAllBytes(@"C:\Windows\system32\ntdll.dll"));
        Assert.Equal("Microsoft Corporation", ver.Fields["CompanyName"]);
    }
}
