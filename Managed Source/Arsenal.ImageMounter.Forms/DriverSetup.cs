using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Arsenal.ImageMounter.Data;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Native;
using Microsoft.Win32;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter;

/// <summary>
/// Routines for installing or uninstalling Arsenal Image Mounter kernel level
/// modules.
/// </summary>
public static class DriverSetup
{
    /// <summary>
    /// Returns version of driver located inside a setup zip archive.
    /// </summary>
    /// <param name="zipFile">ZipFile object with setup files</param>
    public static Version GetDriverVersionFromZipArchive(ZipArchive zipFile)
    {
        var path1 = $"{API.Kernel}\\x86\\phdskmnt.sys";
        var path2 = $"{API.Kernel}/x86/phdskmnt.sys";
        var entry = zipFile.Entries.FirstOrDefault(e => e.FullName.Equals(path1, StringComparison.OrdinalIgnoreCase) || e.FullName.Equals(path2, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            throw new KeyNotFoundException($"Driver file phdskmnt.sys for {API.Kernel} missing in zip archive.");
        }
        using var versionFile = entry.Open();
        return NativePE.GetFixedFileVerInfo(versionFile).FileVersion;
    }

    /// <summary>
    /// Installs Arsenal Image Mounter driver components from a zip archive.
    /// This routine automatically selects the correct driver version for
    /// current version of Windows.
    /// </summary>
    /// <param name="ownerWindow">This needs to be a valid handle to a Win32
    /// window that will be parent to dialog boxes etc shown by setup API. In
    /// console Applications, you could call
    /// NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    /// console window.</param>
    /// <param name="zipFile">An System.IO.Compression.ZipArchive opened for reading that
    /// contains setup source files. Directory layout in zip file needs to be
    /// like in DriverSetup.zip found in DriverSetup directory in repository,
    /// that is, one subdirectory for each kernel version followed by one
    /// subdirectory for each architecture.</param>
    public static void InstallFromZipArchive(IWin32Window ownerWindow, ZipArchive zipFile)
    {
        var origdir = Environment.CurrentDirectory;

        var temppath = Path.Combine(Path.GetTempPath(), "ArsenalImageMounter-DriverSetup");

        Trace.WriteLine($"Using temp path: {temppath}");

        if (Directory.Exists(temppath))
        {
            Directory.Delete(temppath, recursive: true);
        }
        Directory.CreateDirectory(temppath);
        zipFile.ExtractToDirectory(temppath);
        Install(ownerWindow, temppath);

        Environment.CurrentDirectory = origdir;

        if (!API.HasStorPort)
        {
            return;
        }

        new Thread(() =>
        {

            try
            {
                var stopwatch = Stopwatch.StartNew();
                while (Directory.Exists(temppath))
                {
                    try
                    {
                        Directory.Delete(temppath, recursive: true);
                    }
                    catch (IOException ex) when (stopwatch.Elapsed.TotalMinutes < 15.0)
                    {
                        Trace.WriteLine($"I/O Error removing temporary directory: {ex.JoinMessages()}");
                        Thread.Sleep(TimeSpan.FromSeconds(10.0));
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error removing temporary directory: {ex.JoinMessages()}");
            }
        }).Start();
    }

    /// <summary>
    /// Returns version of driver located inside a setup zip archive.
    /// </summary>
    /// <param name="zipStream">Stream containing a zip archive with setup files</param>
    public static Version GetDriverVersionFromZipStream(Stream zipStream) =>
        GetDriverVersionFromZipArchive(new ZipArchive(zipStream, (ZipArchiveMode)0));

    /// <summary>
    /// Installs Arsenal Image Mounter driver components from a zip archive.
    /// This routine automatically selects the correct driver version for
    /// current version of Windows.
    /// </summary>
    /// <param name="ownerWindow">This needs to be a valid handle to a Win32
    /// window that will be parent to dialog boxes etc shown by setup API. In
    /// console Applications, you could call
    /// NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    /// console window.</param>
    /// <param name="zipStream">A stream opened for reading a zip file
    /// containing setup source files. Directory layout in zip file needs to be
    /// like in DriverSetup.zip found in DriverSetup directory in repository,
    /// that is, one subdirectory for each kernel version followed by one
    /// subdirectory for each architecture.</param>
    public static void InstallFromZipStream(IWin32Window ownerWindow, Stream zipStream) =>
        InstallFromZipArchive(ownerWindow, new ZipArchive(zipStream, (ZipArchiveMode)0));

    /// <summary>
    /// Returns version of driver located in setup files directory.
    /// </summary>
    /// <param name="setupRoot">Root directory of setup files.</param>
    public static Version GetSetupFileDriverVersion(string setupRoot) =>
        GetSetupFileDriverVersion(new CachedIniFile(Path.Combine(setupRoot, API.Kernel, "phdskmnt.inf"), Encoding.ASCII));

    /// <summary>
    /// Returns version of driver located in setup files directory.
    /// </summary>
    /// <param name="infFile">.inf file used to identify version of driver.</param>
    public static Version GetSetupFileDriverVersion(CachedIniFile infFile) =>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        Version.Parse(GetInfFileVersionTag(infFile).AsSpan().SplitReverse(',').First());
#else
        Version.Parse(GetInfFileVersionTag(infFile).AsSpan().SplitReverse(',').First().ToString());
#endif

    private static string GetInfFileVersionTag(CachedIniFile infFile)
        => infFile["Version"]?["DriverVer"]
        ?? throw new InvalidDataException("Invalid .inf file: Missing Version/DriverVer tag");

    /// <summary>
    /// Installs Arsenal Image Mounter driver components from specified source
    /// path. This routine automatically selects the correct driver version for
    /// current version of Windows.
    /// </summary>
    /// <param name="ownerWindow">This needs to be a valid handle to a Win32
    /// window that will be parent to dialog boxes etc shown by setup API. In
    /// console Applications, you could call
    /// NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    /// console window.</param>
    /// <param name="setupsource">Directory with setup files. Directory layout
    /// at this path needs to be like in DriverSetup.7z found in DriverSetup
    /// directory in repository, that is, one subdirectory for each kernel
    /// version followed by one subdirectory for each architecture.</param>
    public static void Install(IWin32Window ownerWindow, string setupsource)
    {
        CheckCompatibility(ownerWindow);
        if (API.HasStorPort)
        {
            InstallStorPortDriver(ownerWindow, setupsource);
        }
        else
        {
            InstallScsiPortDriver(ownerWindow, setupsource);
        }
        StartInstalledServices();
    }

    private static void StartInstalledServices()
    {
        var array = new[] { "vhdaccess", "awealloc", "dokan2" };

        foreach (var service_name in array)
        {
            try
            {
                using var service = new ServiceController(service_name);

                switch (service.Status)
                {
                    case ServiceControllerStatus.Stopped:
                    case ServiceControllerStatus.Paused:
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running);
                        Trace.WriteLine($"Successfully loaded driver '{service_name}'");
                        break;
                    case ServiceControllerStatus.Running:
                        Trace.WriteLine($"Driver '{service_name}' is already loaded.");
                        break;
                    default:
                        Trace.WriteLine($"Driver '{service_name}' is '{service.Status}' and cannot be loaded at this time.");
                        break;
                }
            }
            catch
            {
                Trace.WriteLine($"Warning: Failed to open service controller for driver '{service_name}'. Driver not loaded.");
            }
        }
    }

    /// <summary>
    /// Removes Arsenal Image Mounter device objects and driver components.
    /// </summary>
    public static void Uninstall(IWin32Window ownerWindow)
    {
        CheckCompatibility(ownerWindow);
        RemoveDevices(ownerWindow);
        RemoveDriver();
    }

    /// <summary>
    /// Installs Arsenal Image Mounter driver components from specified source
    /// path. This routine installs the StorPort version of the driver, for use
    /// on Windows Server 2003 or later.
    /// </summary>
    /// <param name="ownerWindow">This needs to be a valid handle to a Win32
    /// window that will be parent to dialog boxes etc shown by setup API. In
    /// console Applications, you could call
    /// NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    /// console window.</param>
    /// <param name="setupsource">Directory with setup files. Directory layout
    /// at this path needs to be like in DriverSetup.7z found in DriverSetup
    /// directory in repository, that is, one subdirectory for each kernel
    /// version followed by one subdirectory for each architecture.</param>
    internal static void InstallStorPortDriver(IWin32Window ownerWindow, string setupsource)
    {
        try
        {
            RemoveDevices(ownerWindow);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Error removing existing device nodes. This will be ignored and driver update operation will continue anyway. Exception: {ex.JoinMessages()}");
        }

        var infPath = Path.Combine(setupsource, API.Kernel, "phdskmnt.inf");

        NativeFileIO.CreateRootPnPDevice(ownerWindow.Handle, infPath, @"root\phdskmnt".AsMemory(), ForceReplaceExistingDrivers: false, out var reboot_required);

        if (!reboot_required)
        {
            if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager", "PendingFileRenameOperations", null) is string[] array)
            {
                array = (from p in array
                         where p != null && p.Length > 4
                         select p.Substring(4) into p
                         where File.Exists(p)
                         select FileVersionInfo.GetVersionInfo(p)?.OriginalFilename ?? Path.GetFileName(p)).ToArray();

                Trace.WriteLine($"Pending file replace operations: '{string.Join("', '", array)}'");

                var pending_install_file = (from item in CachedIniFile.EnumerateFileSectionValuePairs(infPath.AsMemory(), "PhysicalDiskMounterDevice.Services".AsMemory())
                                            where "AddService".Equals(item.Key, StringComparison.OrdinalIgnoreCase)
                                            select $"{item.Value.AsMemory().Split(',').First()}.sys").FirstOrDefault(installfile => array.Contains(installfile, StringComparer.OrdinalIgnoreCase));

                if (pending_install_file != null)
                {
                    Trace.WriteLine($"Detected pending file replace operation for '{pending_install_file}'. Requesting reboot.");
                    reboot_required = true;
                }
            }
        }

        if (reboot_required &&
            MessageBox.Show(ownerWindow, "You need to restart your computer to finish driver setup. Do you want to restart now?", "Arsenal Image Mounter", MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk, MessageBoxDefaultButton.Button2) == DialogResult.OK)
        {
            try
            {
                NativeFileIO.ShutdownSystem(ShutdownFlags.Reboot, ShutdownReasons.ReasonFlagPlanned);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                MessageBox.Show(ownerWindow, $"Reboot failed: {ex.JoinMessages()}", "Arsenal Image Mounter", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
        }
    }

    /// <summary>
    /// Removes all plug-and-play device objects owned by Arsenal Image Mounter
    /// driver, in preparation for uninstalling the driver at later time.
    /// </summary>
    /// <param name="ownerWindow">This needs to be a valid handle to a Win32
    /// window that will be parent to dialog boxes etc shown by setup API. In
    /// console Applications, you could call
    /// NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    /// console window.</param>
    internal static void RemoveDevices(IWin32Window ownerWindow)
    {
        if (NativeFileIO.EnumerateDeviceInstancesForService("phdskmnt".AsMemory(), out var hwinstances) != 0u
            || hwinstances is null)
        {
            return;
        }

        foreach (var hwinstance in hwinstances)
        {
            NativeFileIO.RemovePnPDevice(hwid: hwinstance, OwnerWindow: ownerWindow.Handle);
        }
    }

    /// <summary>
    /// Installs Arsenal Image Mounter driver components from specified source
    /// path. This routine installs the ScsiPort version of the driver, for use
    /// on Windows XP or earlier.
    /// </summary>
    /// <param name="ownerWindow">This needs to be a valid handle to a Win32
    /// window that will be parent to dialog boxes etc shown by setup API. In
    /// console Applications, you could call
    /// NativeFileIO.Win32API.GetConsoleWindow() to get a window handle to the
    /// console window.</param>
    /// <param name="setupsource">Directory with setup files. Directory layout
    /// at this path needs to be like in DriverSetup.7z found in DriverSetup
    /// directory in repository, that is, one subdirectory for each kernel
    /// version followed by one subdirectory for each architecture.</param>
    internal static void InstallScsiPortDriver(IWin32Window ownerWindow, string setupsource)
    {
        Trace.WriteLine($"Pre-installed controller inf: '{(NativeFileIO.SetupCopyOEMInf(Path.Combine(setupsource, "CtlUnit", "ctlunit.inf"), NoOverwrite: false))}'");
        Directory.SetCurrentDirectory(setupsource);
        NativeFileIO.UnsafeNativeMethods.SetupSetNonInteractiveMode(state: false);
        NativeFileIO.RunDLLInstallHinfSection(InfPath: Path.Combine(".", API.Kernel, "phdskmnt.inf"), OwnerWindow: ownerWindow.Handle, InfSection: "DefaultInstall".AsMemory());
        using (var scm = new ServiceController("phdskmnt"))
        {
            while (scm.Status != ServiceControllerStatus.Running)
            {
                scm.Start();
                scm.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(3.0));
            }
        }
        var thread = new Thread(NativeFileIO.ScanForHardwareChanges);
        thread.Start();
        thread.Join();
    }

    /// <summary>
    /// Removes Arsenal Image Mounter driver components.
    /// </summary>
    internal static void RemoveDriver()
    {
        using (var scm = NativeFileIO.UnsafeNativeMethods.OpenSCManagerW(IntPtr.Zero, IntPtr.Zero, 983103))
        {
            if (scm.IsInvalid)
            {
                throw new Win32Exception("OpenSCManager");
            }
            var array = new[] { "phdskmnt", "aimwrfltr" };
            for (var i = 0; i < array.Length; i++)
            {
                using var svc = NativeFileIO.UnsafeNativeMethods.OpenServiceW(scm, array[i].AsSpan()[0], 983103);
                if (svc.IsInvalid)
                {
                    throw new Exception("OpenService", new Win32Exception());
                }
                NativeFileIO.UnsafeNativeMethods.DeleteService(svc);
            }
        }

        var array2 = new[] { "phdskmnt", "aimwrfltr" };

        for (var j = 0; j < array2.Length; j++)
        {
            var driverSysFile = Path.Combine(path2: $@"drivers\{array2[j]}.sys", path1: Environment.GetFolderPath(Environment.SpecialFolder.System, Environment.SpecialFolderOption.DoNotVerify));
            if (File.Exists(driverSysFile))
            {
                File.Delete(driverSysFile);
            }
        }
    }

    private static void CheckCompatibility(IWin32Window ownerWindow)
    {
        var native_arch = NativeFileIO.SystemArchitecture;
        var process_arch = NativeFileIO.ProcessArchitecture;

        if (!ReferenceEquals(native_arch, process_arch))
        {
            Trace.WriteLine($"WARNING: Driver setup is starting in a {process_arch} process on {native_arch} OS. Setup may fail!");

            if (ownerWindow != null)
            {
                MessageBox.Show(ownerWindow, "This is a 32 bit process running on a 64 bit version of Windows. There are known problems with installing drivers in this case. If driver setup fails, please retry from a 64 bit application!", "Compatibility warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }
}
