using Arsenal.ImageMounter.Collections;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.ConsoleSupport;
using Arsenal.ImageMounter.IO.Native;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Arsenal.ImageMounter;

/// <summary>
/// Provides command line interface to Arsenal Image Mounter driver and features.
/// </summary>
public static class ConsoleApp
{
    internal static event EventHandler? RanToEnd;

    /// <summary>
    /// Gets path to assembly DLL file containing this class
    /// </summary>
    public static readonly string AssemblyLocation = Assembly.GetExecutingAssembly().Location;

    /// <summary>
    /// Gets version number of assembly DLL file containing this class
    /// </summary>
    public static readonly Version? AssemblyFileVersion =
        string.IsNullOrWhiteSpace(AssemblyLocation)
        ? null
        : NativePE.GetFixedFileVerInfo(AssemblyLocation).FileVersion;

    private static string GetArchitectureLibPath()
        => RuntimeInformation.ProcessArchitecture.ToString();

    private static readonly string[] AssemblyPaths = {
        Path.Combine("lib", GetArchitectureLibPath()),
        "Lib",
        "DiskDriver"
    };

    static ConsoleApp()
    {
        if (NativeStruct.IsOsWindows)
        {
            var appPath = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);

            for (int i = 0, loopTo = AssemblyPaths.Length - 1; i <= loopTo; i++)
            {
                AssemblyPaths[i] = Path.Combine(appPath ?? "", AssemblyPaths[i]);
            }

            var native_dll_paths = AssemblyPaths.Append(Environment.GetEnvironmentVariable("PATH"));

            Environment.SetEnvironmentVariable("PATH", string.Join(";", native_dll_paths));
        }
    }

    /// <summary>
    /// aim_cli command line interface
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Status value</returns>
    public static int Main(params string[] args)
    {
        try
        {
            var commands = ConsoleSupport.ParseCommandLine(args, StringComparer.OrdinalIgnoreCase);

            if (commands.ContainsKey("background"))
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("The --background switch is only supported on Windows");
                    Console.ResetColor();
                    return -1;
                }

                using var ready_wait = new ManualResetEvent(initialState: false);

                NativeFileIO.SetInheritable(ready_wait.SafeWaitHandle, inheritable: true);

                var cmdline = string.Join(" ", commands.SelectMany(cmd =>
                {
                    if (cmd.Key.Equals("background", StringComparison.OrdinalIgnoreCase))
                    {
                        return Enumerable.Empty<string>();
                    }
                    else
                    {
                        return cmd.Value.Length == 0
                            ? SingleValueEnumerable.Get($"--{cmd.Key}")
                            : cmd.Value.Select(value => $"--{cmd.Key}=\"{value}\"");
                    }
                }));

                cmdline = $"{cmdline} --detach={ready_wait.SafeWaitHandle.DangerousGetHandle()}";

                using var process = new Process();

#if NET6_0_OR_GREATER
                process.StartInfo.FileName = Environment.ProcessPath;
#else
                process.StartInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
#endif

                process.StartInfo.UseShellExecute = false;
                process.StartInfo.Arguments = cmdline;

                process.Start();

                using (var process_wait = NativeFileIO.CreateWaitHandle(process.SafeHandle, inheritable: false))
                {
                    WaitHandle.WaitAny(new[] { process_wait, ready_wait });
                }

                return process.HasExited ? 0 : process.Id;
            }

            return ConsoleAppHelpers.UnsafeMain(commands);
        }
        catch (AbandonedMutexException ex)
        {
            Trace.WriteLine(ex.ToString());
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Unexpected client exit.");
            Console.ResetColor();
            return Marshal.GetHRForException(ex);
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.ToString());
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(ex.JoinMessages());
            Console.ResetColor();
            return Marshal.GetHRForException(ex);
        }
        finally
        {
            RanToEnd?.Invoke(null, EventArgs.Empty);
        }
    }
}