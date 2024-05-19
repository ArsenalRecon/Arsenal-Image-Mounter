using Arsenal.ImageMounter.IO.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Arsenal.ImageMounter;

public class Version
{
    [Fact]
    public void CheckVersion()
    {
        var ver = new NativeFileVersion(File.ReadAllBytes(@"C:\Windows\system32\ntdll.dll"));
        Assert.Equal("Microsoft Corporation", ver.Fields["CompanyName"]);
    }
}
