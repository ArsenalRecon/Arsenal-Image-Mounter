using System.Reflection;
using System.Runtime.InteropServices;

namespace Arsenal.ImageMounter.PowerShell;

internal static class PowerShellGlobals
{
    public static bool Initialized { get; }

    static PowerShellGlobals()
    {
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            API.AddNativeLibDirectory();
        }

        Initialized = true;
    }

    private static string? asmpath;

    private static Assembly? AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        asmpath ??= Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (asmpath is null)
        {
            return null;
        }

        var asmname = new AssemblyName(args.Name);

        var filename = $"{asmname.Name}.dll";

        var testname = Path.Combine(asmpath, filename);

        if (File.Exists(testname))
        {
            var asm = Assembly.LoadFrom(testname);

            return asm;
        }

        return null;
    }
}
