using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arsenal.ImageMounter.PowerShell;

internal static class PowerShellGlobals
{
    public static bool Initialized { get; }

    static PowerShellGlobals()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            API.AddNativeLibDirectory();
        }

        Initialized = true;
    }
}
