//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Client;
using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Devio.Server.Interaction;
using Arsenal.ImageMounter.Devio.Server.Services;
using Arsenal.ImageMounter.Devio.Server.SpecializedProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using DiscUtils;
using DiscUtils.Raw;
using DiscUtils.Streams;
using LTRData.Extensions.Formatting;
using LTRData.Extensions.IO;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;

namespace Arsenal.ImageMounter;

public static class ConsoleAppImplementation
{
    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

    private static readonly string[] DefaultChecksumAlgorithms = ["MD5", "SHA1", "SHA256"];

    public static void CloseConsole(SafeWaitHandle DetachEvent)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using (DetachEvent)
        {
            NativeFileIO.SetEvent(DetachEvent);
        }

        Console.In.Dispose();
        Console.Out.Dispose();
        Console.Error.Dispose();

        Console.SetIn(TextReader.Null);
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);

        foreach (var std in stackalloc[] { StdHandle.Input, StdHandle.Output, StdHandle.Error })
        {
            var handle = NativeFileIO.UnsafeNativeMethods.GetStdHandle(std);
            NativeFileIO.UnsafeNativeMethods.SetStdHandle(std, 0);
            NativeFileIO.UnsafeNativeMethods.CloseHandle(handle);
        }

        NativeFileIO.SafeNativeMethods.FreeConsole();
    }

    /// <summary>
    /// Lists mounted devices to console
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static void ListDevices(bool verbose)
    {
        var adapters = API.EnumerateAdapterDeviceInstanceNames();

        if (adapters is null)
        {
            Console.WriteLine("Driver not installed.");

            return;
        }

        foreach (var devinstNameAdapter in adapters)
        {
            var devinstAdapter = NativeFileIO.GetDevInst(devinstNameAdapter);

            if (!devinstAdapter.HasValue)
            {
                Console.Error.WriteLine($"Could not find device instance for '{devinstNameAdapter}'");

                continue;
            }

            if (verbose)
            {
                Console.WriteLine($"Adapter {devinstNameAdapter}");
            }

            var found = false;

            foreach (var dev in from devinstChild in NativeFileIO.EnumerateChildDevices(devinstAdapter.Value)
                                let DevicePath = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
                                where !string.IsNullOrWhiteSpace(DevicePath)
                                let ScsiAddress = NativeFileIO.GetScsiAddressForNtDevice(DevicePath)
                                where ScsiAddress.HasValue
                                let PhysicalDrive = NativeFileIO.GetPhysicalDriveNameForNtDevice(DevicePath)
                                select (ScsiAddress: ScsiAddress.Value, DevicePath, devinst: devinstChild, PhysicalDrive))
            {
                found = true;

                try
                {
                    if (verbose)
                    {
                        using var h = NativeFileIO.NtCreateFile(dev.DevicePath,
                                                                NtObjectAttributes.OpenIf,
                                                                0,
                                                                FileShare.ReadWrite,
                                                                NtCreateDisposition.Open,
                                                                NtCreateOptions.NonDirectoryFile | NtCreateOptions.SynchronousIoNonAlert,
                                                                FileAttributes.Normal,
                                                                null,
                                                                out _);

                        var prop = NativeFileIO.GetStorageStandardProperties(h);

                        Console.WriteLine($"Storage device properties: {JsonSerializer.Serialize(prop, jsonOptions)}");
                    }

                    var device_name = $@"\\?\{dev.PhysicalDrive}";

                    Console.WriteLine($"Device number {dev.ScsiAddress.DWordDeviceNumber:X6}");
                    Console.WriteLine($"Device is {device_name}");
                    Console.WriteLine();

                    foreach (var vol in NativeFileIO.EnumerateDiskVolumes(device_name))
                    {
                        Console.WriteLine($"Contains volume {vol}");

                        foreach (var mnt in NativeFileIO.EnumerateVolumeMountPoints(vol))
                        {
                            Console.WriteLine($"  Mounted at {mnt}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error displaying volume mount points: {ex.JoinMessages()}");
                }
            }

            if (!found)
            {
                Console.WriteLine("No virtual disks.");
            }
        }
    }

    /// <summary>
    /// Writes version information to console
    /// </summary>
    public static void ShowVersionInfo()
    {
        string driver_ver;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var adapter = new ScsiAdapter();
                driver_ver = adapter.GetDriverSubVersion()?.ToString() ?? "Unknown";
            }
            catch (Exception ex)
            {
                driver_ver = $"Error checking driver version: {ex.JoinMessages()}";
            }
        }
        else
        {
            driver_ver = "Driver is only available on Windows";
        }

        var msg = $@"Integrated command line interface to Arsenal Image Mounter virtual SCSI miniport driver.

Operating system:               {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}
.NET runtime:                   {RuntimeInformation.FrameworkDescription}
Process CPU architecture:       {RuntimeInformation.ProcessArchitecture}
Arsenal Image Mounter version:  {ConsoleApp.AssemblyFileVersion}
Driver version:                 {driver_ver}

Copyright (c) 2012-2025 Arsenal Recon.

http://www.ArsenalRecon.com

Please see EULA.txt for license information.";

        msg = StringFormatting.LineFormat(msg.AsSpan(), indentWidth: 32);

        Console.WriteLine(msg);
    }

    public enum IOCommunication
    {
        Auto,
        Tcp,
        Shm,
        Drv
    }

    public static int UnsafeMain(IDictionary<string, string[]> commands)
    {
        string? fileName = null;
        string? writeOverlayImageFile = null;
        string? objectName = null;
        var listenAddress = IPAddress.Any;
        var listenPort = 0;
        var ioCommunication = IOCommunication.Auto;
        var forceSingleThread = false;
        int? bufferSize = null;
        long? diskSize = null;
        long? imageOffset = null;
        var persistent = false;
        var diskAccess = FileAccess.Read;
        var mount = false;
        string? providerName = null;
        var showHelp = false;
        var verbose = false;
        var createImage = false;
        DeviceFlags deviceFlags = 0;
        string? debugCompare = null;
        var libewf_debug_output = Console.IsErrorRedirected ? null : ConsoleSupport.GetConsoleOutputDeviceName();
        string? outputImage = null;
        string[]? checksum = null;
        var outputImageVariant = "dynamic";
        string? dismount = null;
        var forceDismount = false;
        SafeWaitHandle? detachEvent = null;
        var fakeMbr = false;
        var ramDisk = false;
        var proxyconnect = false;
        var autoDelete = false;
        var listDevices = false;
        var autoOnline = false;
        var rescanAdapter = false;
        var lazyWrite = false;

        foreach (var cmd in commands)
        {
            var arg = cmd.Key;

            if (arg.Equals("trace", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("verbose", StringComparison.OrdinalIgnoreCase))
            {
                if (cmd.Value.Length == 0)
                {
                    if (commands.ContainsKey("detach"))
                    {
                        Console.WriteLine("Switches --trace and --background cannot be combined");
                        return -1;
                    }

#if NETFRAMEWORK || NETCOREAPP
                    Trace.Listeners.Add(new ConsoleTraceListener(true));
#endif
                }
                else
                {
                    foreach (var tracefilename in cmd.Value)
                    {
                        var tracefile = new StreamWriter(tracefilename, append: true) { AutoFlush = true };
                        Trace.Listeners.Add(new TextWriterTraceListener(tracefile));
                    }
                }

                verbose = true;
            }
            else if (arg.Equals("name", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                objectName = cmd.Value[0];
            }
            else if (arg.Equals("ipaddress", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                listenAddress = IPAddress.Parse(cmd.Value[0]);
            }
            else if (arg.Equals("port", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                listenPort = int.Parse(cmd.Value[0]);
            }
            else if (arg.Equals("disksize", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                diskSize = SizeFormatting.ParseSuffixedSize(cmd.Value[0])
                    ?? throw new InvalidOperationException($"Invalid disk size '{cmd.Value[0]}'");
            }
            else if (arg.Equals("buffersize", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                bufferSize = (int?)SizeFormatting.ParseSuffixedSize(cmd.Value[0])
                    ?? throw new InvalidOperationException($"Invalid buffer size '{cmd.Value[0]}'");
            }
            else if (arg.Equals("offset", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                imageOffset = SizeFormatting.ParseSuffixedSize(cmd.Value[0])
                    ?? throw new InvalidOperationException($"Invalid offset '{cmd.Value[0]}'");
            }
            else if (arg.Equals("persistent", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0
                && (commands.ContainsKey("name") | commands.ContainsKey("port"))
                && !commands.ContainsKey("mount"))
            {
                persistent = true;
            }
            else if ((arg.Equals("filename", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1 && !commands.ContainsKey("device")
                || arg.Equals("device", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1 && !commands.ContainsKey("filename"))
                && !commands.ContainsKey("connect"))
            {
                fileName = cmd.Value[0];
            }
            else if (arg.Equals("connect", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1
                && !commands.ContainsKey("port") && !commands.ContainsKey("ipaddress") && !commands.ContainsKey("create") && !commands.ContainsKey("provider"))
            {
                proxyconnect = true;
                fileName = cmd.Value[0];
            }
            else if (arg.Equals("create", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0
                && commands.ContainsKey("disksize") && commands.ContainsKey("filename"))
            {
                createImage = true;
            }
            else if (arg.Equals("provider", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                providerName = cmd.Value[0];
            }
            else if (arg.Equals("readonly", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                diskAccess = FileAccess.Read;
            }
            else if (arg.Equals("writable", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                diskAccess = FileAccess.ReadWrite;
            }
            else if (arg.Equals("online", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                autoOnline = true;
            }
            else if (arg.Equals("fakesig", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                deviceFlags |= DeviceFlags.FakeDiskSignatureIfZero;
            }
            else if (arg.Equals("fakembr", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                fakeMbr = true;
            }
            else if (arg.Equals("writeoverlay", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length is 0 or 1)
            {
                writeOverlayImageFile = cmd.Value.ElementAtOrDefault(0) ?? @"\\?\awealloc";
                diskAccess = FileAccess.Read;
                deviceFlags = deviceFlags | DeviceFlags.ReadOnly | DeviceFlags.WriteOverlay;
            }
            else if (arg.Equals("lazywrite", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0
                && !commands.ContainsKey("writeoverlay"))
            {
                lazyWrite = true;
            }
            else if (arg.Equals("autodelete", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0
                && commands.ContainsKey("writeoverlay"))
            {
                autoDelete = true;
            }
            else if (arg.Equals("ramdisk", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0
                && (commands.ContainsKey("filename") || commands.ContainsKey("disksize")))
            {
                ramDisk = true;
            }
            else if (arg.Equals("mount", StringComparison.OrdinalIgnoreCase))
            {
                mount = true;
                foreach (var opt in cmd.Value)
                {
                    if (opt.Equals("removable", StringComparison.OrdinalIgnoreCase))
                    {
                        deviceFlags |= DeviceFlags.Removable;
                    }
                    else if (opt.Equals("cdrom", StringComparison.OrdinalIgnoreCase))
                    {
                        deviceFlags |= DeviceFlags.DeviceTypeCD;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid mount option: '{opt}'");
                        return -1;
                    }
                }

            }
            else if (arg.Equals("convert", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("saveas", StringComparison.OrdinalIgnoreCase))
            {
                var targetcount = (commands.TryGetValue("convert", out var convert) ? convert.Length : 0)
                    + (commands.TryGetValue("saveas", out var saveas) ? saveas.Length : 0);

                if (targetcount != 1)
                {
                    showHelp = true;
                    break;
                }

                outputImage = cmd.Value[0];
                diskAccess = FileAccess.Read;
            }
            else if (arg.Equals("checksum", StringComparison.OrdinalIgnoreCase))
            {
                checksum = commands["checksum"];

                if (checksum.Length == 0)
                {
                    checksum = DefaultChecksumAlgorithms;
                }

                diskAccess = FileAccess.Read;
            }
            else if (arg.Equals("variant", StringComparison.OrdinalIgnoreCase)
                && cmd.Value.Length == 1)
            {
                outputImageVariant = cmd.Value[0];
            }
            else if (arg.Equals("libewfoutput", StringComparison.OrdinalIgnoreCase)
                && cmd.Value.Length == 1)
            {
                libewf_debug_output = cmd.Value[0];
            }
            else if (arg.Equals("debugcompare", StringComparison.OrdinalIgnoreCase)
                && cmd.Value.Length == 1)
            {
                debugCompare = cmd.Value[0];
            }
            else if (arg.Equals("dismount", StringComparison.OrdinalIgnoreCase))
            {
                if (cmd.Value.Length == 0)
                {
                    dismount = "*";
                }
                else if (cmd.Value.Length == 1)
                {
                    dismount = cmd.Value[0];
                }
                else
                {
                    showHelp = true;
                    break;
                }
            }
            else if (arg.Equals("force", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                forceDismount = true;
            }
            else if (arg.Equals("detach", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
#if NET5_0_OR_GREATER
                detachEvent = new SafeWaitHandle(nint.Parse(cmd.Value[0], NumberFormatInfo.InvariantInfo), ownsHandle: true);
#else
                detachEvent = new SafeWaitHandle((nint)long.Parse(cmd.Value[0], NumberFormatInfo.InvariantInfo), ownsHandle: true);
#endif
            }
            else if (arg.Length == 0 || arg == "?" || arg.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
                break;
            }
            else if (arg.Equals("version", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                ShowVersionInfo();
                return 0;
            }
            else if (arg.Equals("list", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                listDevices = true;
            }
            else if (arg.Equals("io", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1
                && Enum.TryParse(cmd.Value[0], ignoreCase: true, out ioCommunication))
            {
            }
            else if (arg.Equals("single", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                forceSingleThread = true;
            }
            else if (arg.Equals("rescan", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0
                && !commands.TryGetValue("filename", out _) && !commands.TryGetValue("device", out _))
            {
                rescanAdapter = true;
            }
            else if (arg.Length == 0)
            {
                Console.WriteLine($"Unsupported command line argument: {string.Join(" ", cmd.Value)}");
                showHelp = true;
                break;
            }
            else if (cmd.Value.Length == 0)
            {
                Console.WriteLine($"Unsupported command line switch or arguments: --{arg}");
                showHelp = true;
                break;
            }
            else
            {
                Console.WriteLine($"Unsupported command line switch or arguments: --{arg}={string.Join(",", cmd.Value)}");
                showHelp = true;
                break;
            }
        }

        if (rescanAdapter)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("The --rescan switch is only supported on Windows");
                Console.ResetColor();
                return -1;
            }

            foreach (var devInst in API.EnumerateAdapterDeviceInstances())
            {
                Console.WriteLine($"Rescanning adapter {devInst}...");
                API.RescanScsiAdapter(devInst);
            }

            Thread.Sleep(1000);
        }

        if (rescanAdapter || listDevices)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("The --list switch is only supported on Windows");
                Console.ResetColor();
                return -1;
            }

            ListDevices(verbose);

            return 0;
        }

        if (showHelp || (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(dismount) && !ramDisk))
        {
            var asmname = Assembly.GetExecutingAssembly().GetName().Name;

            var providers = string.Join("|", DevioServiceFactory.InstalledProvidersByNameAndFileAccess.Keys);

            var msg = $@"{asmname}

Arsenal Image Mounter CLI (AIM CLI) - an integrated command line interface to the Arsenal Image Mounter virtual SCSI miniport driver.

Before using AIM CLI, please see readme_cli.txt and ""Arsenal Recon - End User License Agreement.txt"" for detailed usage and license information.

Please note: AIM CLI should be run with administrative privileges. If you would like to use AIM CLI to interact with EnCase (E01, Ex01, S01 and AFF), AFF4 forensic disk images or QEMU Qcow images, you must make the Libewf (libewf.dll), LibAFF4 (libaff4.dll) and Libqcow (libqcow.dll) libraries available in the expected (/lib/x64) or same folder as aim_cli.exe. AIM CLI now mounts disk images in read-only mode by default.

Syntax to mount a raw/forensic/virtual machine disk image as a ""real"" disk:
{asmname} --mount[=removable|cdrom] [--buffersize=bytes] [--readonly|--writable] [--fakesig] [--fakembr] [--online] --filename=imagefilename [--provider={providers}] [--writeoverlay=differencingimagefile [--autodelete]] [--background]

Syntax to mount a RAM disk:
{asmname} --ramdisk --disksize=size
Size in bytes, can be suffixed with for example M or G for MB or GB.

Syntax to mount a RAM disk from a VHD template image file:
{asmname} --ramdisk --filename=imagefilename

Syntax to create a new disk image file:
{asmname} --create --filename=imagefilename --disksize=size [--variant=fixed|dynamic] [--mount]
Size in bytes, can be suffixed with for example M or G for MB or GB.

Syntax to start service mode, for mounting from other applications:
{asmname} --name=objectname [--buffersize=size] [--readonly|--writable] [--fakembr] --filename=imagefilename [--provider={providers}] [--background]
Size in bytes, can be suffixed with for example K or M for KB or MB.

Syntax to start TCP/IP service mode, for mounting from other computers:
{asmname} [--ipaddress=listenaddress] --port=tcpport [--readonly|--writable] [--fakembr] --filename=imagefilename [--provider={providers}] [--background]

Syntax to convert a disk image without mounting:
{asmname} --filename=imagefilename [--fakembr] [--provider={providers}] --convert=outputimagefilename [--variant=fixed|dynamic] [--background]
{asmname} --filename=imagefilename [--fakembr] [--provider={providers}] --convert=\\?\PhysicalDriveN [--background]

Syntax to save as a new disk image after mounting:
{asmname} --device=devicenumber --saveas=outputimagefilename [--variant=fixed|dynamic] [--background]

Syntax to save a physical disk as an image file:
{asmname} --device=\\?\PhysicalDriveN --convert=outputimagefilename [--variant=fixed|dynamic] [--background]

Syntax to dismount a mounted device:
{asmname} --dismount[=devicenumber] [--force]

Syntax to mount a disk at remote machine:
{asmname} --mount [--buffersize=bytes] [--readonly|--writable] [--fakesig] [--fakembr] [--online] --connnect=ipaddress[:port] [--writeoverlay=differencingimagefile [--autodelete]]

Syntax to display a list of mounted devices:
{asmname} --list

Syntax to rescan SCSI adapter:
{asmname} --rescan
Useful when there is a dead mounted disk left behind after it has lost connection to storage backend, such as after issues with underlying image file device or loss of network connection.

";

            msg = StringFormatting.LineFormat(msg.AsSpan(), indentWidth: 4);

            Console.WriteLine(msg);

            return 1;
        }

        if (dismount is not null && !string.IsNullOrWhiteSpace(dismount))
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("The --dismount switch is only supported on Windows");
                Console.ResetColor();
                return -1;
            }

            uint devicenumber;

            if (dismount == "*")
            {
                devicenumber = ScsiAdapter.AutoDeviceNumber;
            }
            else if (!uint.TryParse(dismount, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out devicenumber))
            {
                Console.WriteLine($@"Invalid device number: {dismount}

Expected hexadecimal SCSI address in the form PPTTLL, for example: 000100");

                return 1;
            }

            try
            {
                using var adapter = new ScsiAdapter();

                if (devicenumber == ScsiAdapter.AutoDeviceNumber)
                {
                    if (!adapter.EnumerateDevices().Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("No mounted devices.");
                        Console.ResetColor();
                        return 2;
                    }

                    if (forceDismount)
                    {
                        adapter.RemoveAllDevices();
                    }
                    else
                    {
                        adapter.RemoveAllDevicesSafe();
                    }

                    Console.WriteLine("All devices dismounted.");
                }
                else
                {
                    if (!adapter.EnumerateDevices().Contains(devicenumber))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine($"No device mounted with device number {devicenumber:X6}.");
                        Console.ResetColor();

                        return 2;
                    }

                    if (forceDismount)
                    {
                        adapter.RemoveDevice(devicenumber);
                    }
                    else
                    {
                        adapter.RemoveDeviceSafe(devicenumber);
                    }

                    Console.WriteLine("Devices dismounted.");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(ex.JoinMessages());
                Console.ResetColor();

                return 2;
            }
        }

        IDevioProvider provider;
        DevioServiceBase service;

        if (proxyconnect)
        {
            if (fileName is null)
            {
                return 0;
            }

            DeviceFlags proxy = ioCommunication switch
            {
                IOCommunication.Tcp => DeviceFlags.ProxyTypeTCP,
                IOCommunication.Shm => DeviceFlags.ProxyTypeSharedMemory,
                IOCommunication.Drv => DeviceFlags.FileTypeParallel,
                _ => DeviceFlags.ProxyTypeTCP
            };

            Console.WriteLine($"Connecting to '{fileName}'...");

            if (mount)
            {
                if (ioCommunication == IOCommunication.Drv)
                {
                    service = new ProxyClientService($@"\\?\DevIoDrv\{fileName}", proxy, $"Connection to {fileName}", diskSize ?? 0);
                }
                else
                {
                    service = new ProxyClientService(fileName, proxy, $"Connection to {fileName}", diskSize ?? 0);
                    service.AdditionalFlags |= DeviceFlags.TypeProxy;
                }

                provider = service.DevioProvider;
            }
            else
            {
                var portIndex = fileName.LastIndexOf(':');

                var host = fileName;
                var port = 9000;

                if (portIndex > 0)
                {
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                    port = short.Parse(fileName.AsSpan(portIndex + 1));
                    host = fileName[..portIndex];
#else
                    port = short.Parse(fileName.Substring(portIndex + 1));
                    host = fileName.Remove(portIndex);
#endif
                }

                provider = new DevioProviderFromStream(new DevioTcpStream(host, port, read_only: !diskAccess.HasFlag(FileAccess.Write)), ownsStream: true);

                if (outputImage is not null) // Convert to new image file format
                {
                    provider.ConvertToImage(fileName, outputImage, outputImageVariant, bufferSize, detachEvent);
                    provider.Dispose();

                    return 0;
                }
                else if (checksum is not null) // Calculate checksum over image
                {
                    provider.Checksum(bufferSize, checksum);
                    provider.Dispose();

                    return 0;
                }
                else
                {
                    provider.Dispose();

                    Console.WriteLine("None of --mount, --checksum or --convert switches specified, nothing to do.");

                    return 1;
                }
            }
        }
        else if (ramDisk)
        {
            if ((fileName is null
                || string.IsNullOrWhiteSpace(fileName))
                && diskSize.HasValue)  // RAM disk without template image
            {
                service = new RAMDiskService(diskSize.Value, InitializeFileSystem.NTFS);
            }
            else if (fileName is not null
                && !string.IsNullOrWhiteSpace(fileName))  // RAM disk with template image
            {
                if (!".vhd".Equals(Path.GetExtension(fileName), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Only vhd format image files are supported as RAM disk templates");
                }

                Console.WriteLine($"Using image file '{fileName}' as RAM disk template");

                service = new RAMDiskService(fileName);
            }
            else
            {
                return 0;
            }

            provider = service.DevioProvider;

            Console.WriteLine($"Creating {SizeFormatting.FormatBytes(provider.Length)} RAM disk.");
        }
        else
        {
            if (fileName is null)
            {
                return 0;
            }

            if (providerName is null || string.IsNullOrWhiteSpace(providerName))
            {
                providerName = DevioServiceFactory.GetProviderTypeFromFileName(fileName).ToString();
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(providerName, "libewf"))
            {
                DevioProviderLibEwf.SetNotificationFile(libewf_debug_output);

                ConsoleApp.RanToEnd += (sender, e) => DevioProviderLibEwf.SetNotificationFile(null);

                if (verbose)
                {
                    DevioProviderLibEwf.NotificationVerbose = true;
                }
            }

            if (createImage)
            {
                if (!diskSize.HasValue || string.IsNullOrWhiteSpace(fileName))
                {
                    throw new InvalidOperationException("--create requires --disksize and --filename");
                }

                if (File.Exists(fileName))
                {
                    throw new IOException($"File '{fileName}' already exists");
                }

                Console.WriteLine($"Creating image file '{fileName}', type {outputImageVariant}, size {SizeFormatting.FormatBytes(diskSize.Value)}...");

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
                var image_type = Path.GetExtension(fileName.AsSpan()).TrimStart('.').ToString().ToUpperInvariant();
#else
                var image_type = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
#endif

                var geometry = Geometry.FromCapacity(diskSize.Value);

                VirtualDisk? disk;

                switch (image_type)
                {
                    case "DD":
                    case "RAW":
                    case "IMG":
                    case "IMA":
                    case "ISO":
                    case "BIN":
                    case "000":
                    case "001":
                        var target = new FileStream(fileName,
                                                    FileMode.CreateNew,
                                                    FileAccess.ReadWrite,
                                                    FileShare.Delete);

                        if ("fixed".Equals(outputImageVariant, StringComparison.OrdinalIgnoreCase))
                        {
                        }
                        else if ("dynamic".Equals(outputImageVariant, StringComparison.OrdinalIgnoreCase))
                        {
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                try
                                {
                                    NativeFileIO.SetFileSparseFlag(target.SafeFileHandle, true);
                                }
                                catch (Exception ex)
                                {
                                    Trace.WriteLine($"Sparse files not supported on target platform or file system: {ex.JoinMessages()}");
                                }
                            }
                        }
                        else
                        {
                            throw new ArgumentException($"Value {outputImageVariant} not supported as output image variant. Valid values are fixed or dynamic.");
                        }

                        target.SetLength(diskSize.Value);

                        disk = new Disk(target, ownsStream: Ownership.Dispose);

                        break;

                    default:
                        if (!DiscUtilsInteraction.DiscUtilsInitialized)
                        {
                            throw new NotSupportedException();
                        }

                        disk = VirtualDisk.CreateDisk(image_type,
                                                      outputImageVariant,
                                                      fileName,
                                                      diskSize.Value,
                                                      geometry,
                                                      parameters: null,
                                                      useAsync: true);

                        break;
                }

                if (disk is null)
                {
                    throw new NotSupportedException("Failed to create image file. Unknown image file format?");
                }

                using (disk)
                {
                    var partition_style = diskSize.Value < (2L << 40) ? PARTITION_STYLE.MBR : PARTITION_STYLE.GPT;
                    DiscUtilsInteraction.InitializeVirtualDisk(disk, geometry, partition_style, InitializeFileSystem.NTFS, label: null);
                }

                diskAccess = FileAccess.ReadWrite;
            }

            if (providerName != "None")
            {
                Console.WriteLine($"Opening image file '{fileName}' with format provider '{providerName}'...");
            }
            else
            {
                Console.WriteLine($"Opening '{fileName}'...");
            }

            provider = DevioServiceFactory.GetProvider(fileName, diskAccess, providerName)
                ?? throw new NotSupportedException("Unknown image file format. Try with another format provider!");

            if (provider.Length <= 0)
            {
                throw new NotSupportedException("Unknown size of source device");
            }

            if (forceSingleThread)
            {
                provider.ForceSingleThread = true;
            }

            if (lazyWrite)
            {
                provider.UseLazyWrites = true;
            }

            if (!string.IsNullOrWhiteSpace(debugCompare))
            {
                var DebugCompareStream = new FileStream(debugCompare,
                                                        FileMode.Open,
                                                        FileAccess.Read,
                                                        FileShare.Read | FileShare.Delete,
                                                        bufferSize: 1,
                                                        useAsync: true);

                provider = new DebugProvider(provider, DebugCompareStream);
            }

            if (!mount)
            {
                if (diskSize.HasValue)
                {
                    provider = new DevioProviderWithOffset(provider, imageOffset ?? 0, diskSize.Value);
                }
                else if (imageOffset.HasValue)
                {
                    provider = new DevioProviderWithOffset(provider, imageOffset.Value);
                }
            }

            if (fakeMbr)
            {
                provider = new DevioProviderWithFakeMBR(provider);
            }

            Trace.WriteLine($"Image virtual size is {SizeFormatting.FormatBytes(provider.Length)}, sector size {provider.SectorSize} bytes.");

            if (ioCommunication == IOCommunication.Auto)
            {
                if (listenPort != 0)
                {
                    ioCommunication = IOCommunication.Tcp;
                }
                else
                {
                    ioCommunication = IOCommunication.Drv;
                }
            }

            if (ioCommunication == IOCommunication.Tcp
                && listenPort != 0) // Listen on TCP/IP socket
            {
                service = new DevioTcpService(listenAddress, listenPort, provider, ownsProvider: true);
            }
            else if (ioCommunication == IOCommunication.Shm
                && objectName is not null
                && !string.IsNullOrWhiteSpace(objectName)) // Listen on shared memory object
            {
                service = new DevioShmService(objectName, provider, ownsProvider: true, bufferSize: bufferSize ?? DevioShmService.DefaultBufferSize);
            }
            else if (ioCommunication == IOCommunication.Shm
                && mount) // Request to mount in-process
            {
                service = new DevioShmService(provider, ownsProvider: true, BufferSize: bufferSize ?? DevioShmService.DefaultBufferSize);
            }
            else if (ioCommunication == IOCommunication.Drv
                && objectName is not null
                && !string.IsNullOrWhiteSpace(objectName)) // Listen on deviodrv object
            {
                service = new DevioDrvService(objectName, provider, ownsProvider: true, initialBufferSize: bufferSize ?? DevioDrvService.DefaultInitialBufferSize);
            }
            else if (ioCommunication == IOCommunication.Drv
                && mount) // Request to mount in-process
            {
                service = new DevioDrvService(provider, ownsProvider: true, initialBufferSize: bufferSize ?? DevioDrvService.DefaultInitialBufferSize);
            }
            else if (outputImage is not null) // Convert to new image file format
            {
                provider.ConvertToImage(fileName, outputImage, outputImageVariant, bufferSize, detachEvent);
                provider.Dispose();

                return 0;
            }
            else if (checksum is not null) // Calculate checksum over image
            {
                provider.Checksum(bufferSize, checksum);
                provider.Dispose();

                return 0;
            }
            else if (createImage)
            {
                provider.Dispose();

                Console.WriteLine("Done.");

                return 0;
            }
            else
            {
                provider.Dispose();

                Console.WriteLine("None of --name, --port, --mount, --checksum, --create or --convert switches specified, nothing to do.");

                return 1;
            }
        }

        service.Persistent = persistent;

        if (detachEvent is null)
        {
            service.ClientConnected += (sender, e)
                => Console.WriteLine($"Client {service.ClientName} connected.");

            service.ClientDisconnected += (sender, e)
                => Console.WriteLine($"Client {service.ClientName} disconnected.");
        }

        if (mount || ramDisk)
        {
            Console.WriteLine("Mounting as virtual disk...");

            service.WriteOverlayImageName = writeOverlayImageFile;

            ScsiAdapter adapter;

            try
            {
                adapter = new ScsiAdapter();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to open SCSI adapter: {ex.JoinMessages()}");
                throw new IOException("Cannot access Arsenal Image Mounter driver. Check that the driver is installed and that you are running this application with administrative privileges.", ex);
            }

            if (imageOffset.HasValue)
            {
                service.Offset = imageOffset.Value;
            }

            if (diskSize.HasValue)
            {
                service.DiskSize = diskSize.Value;
            }

            service.StartServiceThreadAndMount(adapter, deviceFlags);

            if (autoDelete)
            {
                var rc = service.SetWriteOverlayDeleteOnClose();

                if (rc != NativeConstants.NO_ERROR)
                {
                    Console.WriteLine($"Failed to set auto-delete for write overlay image ({rc}): {new Win32Exception(rc).Message}");
                }
            }

            try
            {
                var device_name = $@"\\?\{service.GetDiskDeviceName()}";

                Console.WriteLine($"Device number {service.DiskDeviceNumber:X6}");
                Console.WriteLine($"Device is {device_name}");

                using (var device = new DiskDevice(device_name, FileAccess.ReadWrite))
                {
                    switch (device.DiskPolicyOffline)
                    {
                        case true:
                            Console.WriteLine($"Mounted offline");
                            break;

                        case false:
                            Console.WriteLine($"Mounted online");
                            break;
                    }

                    switch (device.IsDiskWritable)
                    {
                        case false:
                            Console.WriteLine($"Mounted read only");
                            break;

                        case true:
                            Console.WriteLine($"Mounted writable");
                            break;
                    }

                    if (autoOnline)
                    {
                        if (device.DiskPolicyReadOnly == true
                            && (diskAccess.HasFlag(FileAccess.Write)
                            || writeOverlayImageFile is not null))
                        {
                            Console.WriteLine($"Setting device policy to writable");
                            device.DiskPolicyReadOnly = false;
                        }

                        if (device.DiskPolicyOffline == true)
                        {
                            Console.WriteLine($"Setting device policy to online");
                            device.DiskPolicyOffline = false;
                        }

                        device.UpdateProperties();
                    }
                }

                Console.WriteLine();

                IEnumerable<string> volumes;

                if (autoOnline)
                {
                    volumes = NativeFileIO.OnlineDiskVolumes(device_name);
                }
                else
                {
                    volumes = NativeFileIO.EnumerateDiskVolumes(device_name);
                }

                foreach (var vol in volumes)
                {
                    Console.WriteLine($"Contains volume {vol}");

                    foreach (var mnt in NativeFileIO.EnumerateVolumeMountPoints(vol))
                    {
                        Console.WriteLine($"  Mounted at {mnt}");
                    }

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying volume mount points: {ex.JoinMessages()}");
            }
        }
        else
        {
            service.StartServiceThread();
        }

        if (detachEvent is not null)
        {
            if (mount)
            {
                Console.WriteLine($"Virtual disk mounted. To dismount, type aim_cli --dismount={service.DiskDeviceNumber:X6}");
            }
            else
            {
                Console.WriteLine("Ready for incoming connections.");
            }

            CloseConsole(detachEvent);
        }
        else if (ramDisk || service is DevioNoneService)
        {
            if (mount)
            {
                Console.WriteLine($"Virtual disk mounted. To dismount, type aim_cli --dismount={service.DiskDeviceNumber:X6}");
            }
        }
        else
        {
            if (mount)
            {
                Console.WriteLine("Virtual disk mounted. Press Ctrl+C to dismount virtual disk and exit.");
            }
            else
            {
                Console.WriteLine("Waiting for incoming connection from Arsenal Image Mounter. Press Ctrl+C to exit.");
            }

            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true;

                try
                {
                    Console.WriteLine("Stopping service...");
                    await service.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(ex.JoinMessages());
                    Console.ResetColor();
                }
            };
        }

        using (service)
        {
            if (service is not DevioNoneService)
            {
                service.WaitForExit(Timeout.InfiniteTimeSpan);

                Console.WriteLine("Service stopped.");
            }

            return service.Exception is not null
                ? throw new Exception("Service failed.", service.Exception)
                : 0;
        }
    }

    public static int StartBackgroundProcess()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("The --background switch is only supported on Windows");
            Console.ResetColor();
            return -1;
        }

        var cmdLine = NativeFileIO.GetProcessCommandLineAsArgumentArray();
        
        using var ready_wait = new ManualResetEvent(initialState: false);
        
        NativeFileIO.SetInheritable(ready_wait.SafeWaitHandle, inheritable: true);

        for (var i = 0; i < cmdLine.Length; i++)
        {
            if (cmdLine[i] is "--background" or "/background")
            {
                cmdLine[i] = $"--detach={ready_wait.SafeWaitHandle.DangerousGetHandle()}";
            }
            else if (cmdLine[i].Contains(' ') && !cmdLine[i].Contains('"'))
            {
                cmdLine[i] = $@"""{cmdLine[i]}""";
            }
        }
        
        using var process = new Process();

#if NET6_0_OR_GREATER
        process.StartInfo.FileName = Environment.ProcessPath;
#else
        process.StartInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
#endif

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.Arguments = string.Join(" ", cmdLine.Skip(1));

        process.Start();
        using var process_wait = NativeFileIO.CreateWaitHandle(process.SafeHandle, inheritable: false);
        WaitHandle.WaitAny([process_wait, ready_wait]);

        var id = process.HasExited ? 0 : process.Id;

        return id;
    }
}
