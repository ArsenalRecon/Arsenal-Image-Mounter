using System.Runtime.InteropServices;
using Xunit;

namespace Arsenal.ImageMounter.Tests;

public class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
#if NETSTANDARD || NETCOREAPP
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = null;
        }
        else
        {
            Skip = "This test runs on Windows only";
        }
#else
        Skip = null;
#endif
    }
}
