using System.Management.Automation;
using System.Reflection;
using System.Runtime.InteropServices;
#if NETCOREAPP
using System.Runtime.Loader;
#endif

namespace Arsenal.ImageMounter.PowerShell;

public class AlcModuleResolveEventHandler : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    public void OnImport()
    {
#if NETFRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
#elif NETCOREAPP
        AssemblyLoadContext.Default.Resolving += AssemblyResolve;
#endif

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            API.AddNativeLibDirectory();
        }
    }

    public void OnRemove(PSModuleInfo psModuleInfo)
    {
#if NETFRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolve;
#elif NETCOREAPP
        AssemblyLoadContext.Default.Resolving -= AssemblyResolve;
#endif
    }

    private static readonly string? asmpath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

#if NETFRAMEWORK
    private static Assembly? AssemblyResolve(object? sender, ResolveEventArgs args)
    {
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
#elif NETCOREAPP
    private static Assembly? AssemblyResolve(AssemblyLoadContext alc, AssemblyName asmname)
    {
        if (asmpath is null)
        {
            return null;
        }

        var filename = $"{asmname.Name}.dll";

        var testname = Path.Combine(asmpath, filename);

        if (File.Exists(testname))
        {
            var asm = alc.LoadFromAssemblyPath(testname);

            return asm;
        }

        return null;
    }
#endif
}
