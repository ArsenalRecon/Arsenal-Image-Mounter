using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Devio.Server.Interaction;
using Arsenal.ImageMounter.Devio.Server.Services;
using Arsenal.ImageMounter.Devio.Server.SpecializedProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.ConsoleSupport;
using Arsenal.ImageMounter.IO.Native;
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

namespace Arsenal.ImageMounter;

internal static class ConsoleApp
{

    private static readonly string[] defaultChecksumAlgorithms = { "MD5", "SHA1", "SHA256" };

    /// <summary>
    /// Lists mounted devices to console
    /// </summary>
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static void ListDevices()
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
                continue;
            }

            Console.WriteLine($"Adapter {devinstNameAdapter}");

            foreach (var dev in from devinstChild in NativeFileIO.EnumerateChildDevices(devinstAdapter.Value)
                                let path = NativeFileIO.GetPhysicalDeviceObjectNtPath(devinstChild)
                                where !string.IsNullOrWhiteSpace(path)
                                let address = NativeFileIO.GetScsiAddressForNtDevice(path)
                                let physical_drive_path = NativeFileIO.GetPhysicalDriveNameForNtDevice(path)
                                select (address, path, devinstChild, physical_drive_path))
            {

                Console.WriteLine($"SCSI address {dev.address} found at {dev.path} devinst {dev.devinstChild} ({dev.physical_drive_path}).");

#if DEBUG
                using var h = NativeFileIO.NtCreateFile(dev.path,
                                                        NtObjectAttributes.OpenIf,
                                                        (FileAccess)0,
                                                        FileShare.ReadWrite,
                                                        NtCreateDisposition.Open,
                                                        NtCreateOptions.NonDirectoryFile | NtCreateOptions.SynchronousIoNonAlert,
                                                        FileAttributes.Normal,
                                                        null,
                                                        out var argWasCreated);

                var prop = NativeFileIO.GetStorageStandardProperties(h);

                Console.WriteLine(prop?.ToMembersString());
#endif
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
                driver_ver = $"Driver version: {adapter.GetDriverSubVersion()}";
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

        Console.WriteLine($@"Integrated command line interface to Arsenal Image Mounter virtual
SCSI miniport driver.

Operating system: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}
.NET runtime: {RuntimeInformation.FrameworkDescription}
Process CPU architecture: {RuntimeInformation.ProcessArchitecture}

Arsenal Image Mounter version {Program.assemblyFileVersion}

{driver_ver}
            
Copyright (c) 2012-2022 Arsenal Recon.

http://www.ArsenalRecon.com

Please see EULA.txt for license information.");
    }

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

        Console.SetIn(TextReader.Null);
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);

        NativeFileIO.SafeNativeMethods.FreeConsole();

    }

    public static int UnsafeMain(IDictionary<string, string[]> commands)
    {
        string? image_path = null;
        string? write_overlay_image_file = null;
        string? ObjectName = null;
        var listen_address = IPAddress.Any;
        var listen_port = 0;
        var buffer_size = DevioShmService.DefaultBufferSize;
        long? disk_size = null;
        var disk_access = FileAccess.ReadWrite;
        var mount = false;
        string? provider_name = null;
        var show_help = false;
        var verbose = false;
        DeviceFlags device_flags = 0;
        string? debug_compare = null;
        var libewf_debug_output = ConsoleSupport.GetConsoleOutputDeviceName();
        string? output_image = null;
        string[]? checksum = null;
        var output_image_variant = "dynamic";
        string? dismount = null;
        var force_dismount = false;
        SafeWaitHandle? detach_event = null;
        var fake_mbr = false;
        var auto_delete = false;
        var list_devices = false;

        foreach (var cmd in commands)
        {
            var arg = cmd.Key;

            if (arg.Equals("trace", StringComparison.OrdinalIgnoreCase))
            {
                if (cmd.Value.Length == 0)
                {
                    if (commands.ContainsKey("detach"))
                    {
                        Console.WriteLine("Switches --trace and --background cannot be combined");
                        return -1;
                    }

                    Trace.Listeners.Add(new ConsoleTraceListener(true));
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
                ObjectName = cmd.Value[0];
            }
            else if (arg.Equals("ipaddress", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                listen_address = IPAddress.Parse(cmd.Value[0]);
            }
            else if (arg.Equals("port", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                listen_port = int.Parse(cmd.Value[0]);
            }
            else if (arg.Equals("disksize", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                disk_size = long.Parse(cmd.Value[0]);
            }
            else if (arg.Equals("buffersize", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                buffer_size = long.Parse(cmd.Value[0]);
            }
            else if (arg.Equals("filename", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1 || arg.Equals("device", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                image_path = cmd.Value[0];
            }
            else if (arg.Equals("provider", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                provider_name = cmd.Value[0];
            }
            else if (arg.Equals("readonly", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                disk_access = FileAccess.Read;
            }
            else if (arg.Equals("fakesig", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                device_flags |= DeviceFlags.FakeDiskSignatureIfZero;
            }
            else if (arg.Equals("fakembr", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                fake_mbr = true;
            }
            else if (arg.Equals("writeoverlay", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                write_overlay_image_file = cmd.Value[0];
                disk_access = FileAccess.Read;
                device_flags = device_flags | DeviceFlags.ReadOnly | DeviceFlags.WriteOverlay;
            }
            else if (arg.Equals("autodelete", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                if (!commands.ContainsKey("writeoverlay"))
                {
                    show_help = true;
                    break;
                }

                auto_delete = true;
            }
            else if (arg.Equals("mount", StringComparison.OrdinalIgnoreCase))
            {
                mount = true;
                foreach (var opt in cmd.Value)
                {
                    if (opt.Equals("removable", StringComparison.OrdinalIgnoreCase))
                    {
                        device_flags |= DeviceFlags.Removable;
                    }
                    else if (opt.Equals("cdrom", StringComparison.OrdinalIgnoreCase))
                    {
                        device_flags |= DeviceFlags.DeviceTypeCD;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid mount option: '{opt}'");
                        return -1;
                    }
                }
            }
            else if (arg.Equals("convert", StringComparison.OrdinalIgnoreCase) || arg.Equals("saveas", StringComparison.OrdinalIgnoreCase))
            {
                var targetcount = (commands.TryGetValue("convert", out var convert) ? convert.Length : 0) +
                    (commands.TryGetValue("saveas", out var saveas) ? saveas.Length : 0);

                if (targetcount != 1)
                {
                    show_help = true;
                    break;
                }

                output_image = cmd.Value[0];
                disk_access = FileAccess.Read;
            }
            else if (arg.Equals("checksum", StringComparison.OrdinalIgnoreCase))
            {
                checksum = commands["checksum"];

                if (checksum.Length == 0)
                {
                    checksum = defaultChecksumAlgorithms;
                }

                disk_access = FileAccess.Read;
            }
            else if (arg.Equals("variant", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                output_image_variant = cmd.Value[0];
            }
            else if (arg.Equals("libewfoutput", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                libewf_debug_output = cmd.Value[0];
            }
            else if (arg.Equals("debugcompare", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                debug_compare = cmd.Value[0];
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
                    show_help = true;
                    break;
                }
            }
            else if (arg.Equals("force", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                force_dismount = true;
            }
            else if (arg.Equals("detach", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 1)
            {
                detach_event = new SafeWaitHandle(new IntPtr(long.Parse(cmd.Value[0], NumberFormatInfo.InvariantInfo)), ownsHandle: true);
            }
            else if (arg.Length == 0 || arg == "?" || arg.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                show_help = true;
                break;
            }
            else if (arg.Equals("version", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                ShowVersionInfo();
                return 0;
            }
            else if (arg.Equals("list", StringComparison.OrdinalIgnoreCase) && cmd.Value.Length == 0)
            {
                list_devices = true;
                return 0;
            }
            else if (arg.Length == 0)
            {
                Console.WriteLine($"Unsupported command line argument: {cmd.Value.FirstOrDefault()}");
                show_help = true;
                break;
            }
            else
            {
                Console.WriteLine($"Unsupported command line switch: --{arg}");
                show_help = true;
                break;
            }
        }

        if (list_devices)
        {

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("The --list switch is only supported on Windows");
                Console.ResetColor();
                return -1;

            }

            ListDevices();

            return 0;
        }

        if (show_help || string.IsNullOrWhiteSpace(image_path) && string.IsNullOrWhiteSpace(dismount))
        {
            var asmname = Assembly.GetExecutingAssembly().GetName().Name;

            var providers = string.Join("|", DevioServiceFactory.InstalledProvidersByNameAndFileAccess.Keys);

            var msg = $@"{asmname}

Arsenal Image Mounter CLI (AIM CLI) - an integrated command line interface to the Arsenal Image Mounter virtual SCSI miniport driver.

Before using AIM CLI, please see readme_cli.txt and ""Arsenal Recon - End User License Agreement.txt"" for detailed usage and license information.

Please note: AIM CLI should be run with administrative privileges. If you would like to use AIM CLI to interact with EnCase (E01 and Ex01), AFF4 forensic disk images or QEMU Qcow images, you must make the Libewf (libewf.dll), LibAFF4 (libaff4.dll) and Libqcow (libqcow.dll) libraries available in the expected (/lib/x64) or same folder as aim_cli.exe. AIM CLI mounts disk images in write-original mode by default, to maintain compatibility with a large number of scripts in which users have replaced other solutions with AIM CLI.

Syntax to mount a raw/forensic/virtual machine disk image as a ""real"" disk:
{asmname} --mount[=removable|cdrom] [--buffersize=bytes] [--readonly] [--fakesig] [--fakembr] --filename=imagefilename --provider={providers} [--writeoverlay=differencingimagefile [--autodelete]] [--background]

Syntax to start shared memory service mode, for mounting from other applications:
{asmname} --name=objectname [--buffersize=bytes] [--readonly] [--fakembr] --filename=imagefilename --provider={providers} [--background]

Syntax to start TCP/IP service mode, for mounting from other computers:
aim_cli.exe [--ipaddress=listenaddress] --port=tcpport [--readonly] [--fakembr] --filename=imagefilename --provider={providers} [--background]

Syntax to convert a disk image without mounting:
{asmname} --filename=imagefilename [--fakembr] --provider={providers} --convert=outputimagefilename [--variant=fixed|dynamic] [--background]
{asmname} --filename=imagefilename [--fakembr] --provider={providers} --convert=\\?\PhysicalDriveN [--background]

Syntax to save as a new disk image after mounting:
{asmname} --device=devicenumber --saveas=outputimagefilename [--variant=fixed|dynamic] [--background]

Syntax to save a physical disk as an image file:
{asmname} --device=\\?\PhysicalDriveN --convert=outputimagefilename [--variant=fixed|dynamic] [--background]

Syntax to dismount a mounted device:
{asmname} --dismount[=devicenumber] [--force]

Syntax to display a list of mounted devices:
{asmname} --list

";

            msg = msg.LineFormat(4);

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
                    if (adapter.GetDeviceList().Length == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("No mounted devices.");
                        Console.ResetColor();
                        return 2;
                    }

                    if (force_dismount)
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
                    if (!adapter.GetDeviceList().Contains(devicenumber))
                    {

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine($"No device mounted with device number {devicenumber:X6}.");
                        Console.ResetColor();
                        return 2;

                    }

                    if (force_dismount)
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

        if (image_path is null
            || string.IsNullOrWhiteSpace(image_path))
        {
            return 0;
        }

        if (provider_name is null || string.IsNullOrWhiteSpace(provider_name))
        {
            provider_name = DevioServiceFactory.GetProviderTypeFromFileName(image_path).ToString();
        }

        Console.WriteLine($"Opening image file '{image_path}' with format provider '{provider_name}'...");

        if (StringComparer.OrdinalIgnoreCase.Equals(provider_name, "libewf"))
        {
            DevioProviderLibEwf.SetNotificationFile(libewf_debug_output);

            Program.RanToEnd += (sender, e) => DevioProviderLibEwf.SetNotificationFile(null);

            if (verbose)
            {
                DevioProviderLibEwf.NotificationVerbose = true;
            }
        }

        var provider = DevioServiceFactory.GetProvider(image_path, disk_access, provider_name);

        if (provider is null)
        {
            throw new NotSupportedException("Unknown image file format. Try with another format provider!");
        }

        if (provider.Length <= 0L)
        {
            throw new NotSupportedException("Unknown size of source device");
        }

        if (!string.IsNullOrWhiteSpace(debug_compare))
        {
            var DebugCompareStream = new FileStream(debug_compare, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, bufferSize: 1, useAsync: true);

            provider = new DebugProvider(provider, DebugCompareStream);
        }

        if (fake_mbr)
        {

            provider = new DevioProviderWithFakeMBR(provider);

        }

        Console.WriteLine($"Image virtual size is {NativeStruct.FormatBytes(provider.Length)}");

        DevioServiceBase service;

        if (ObjectName is not null && !string.IsNullOrWhiteSpace(ObjectName)) // Listen on shared memory object
        {
            service = new DevioShmService(ObjectName, provider, OwnsProvider: true, BufferSize: buffer_size);
        }
        else if (listen_port != 0) // Listen on TCP/IP socket
        {
            service = new DevioTcpService(listen_address, listen_port, provider, OwnsProvider: true);
        }
        else if (mount) // Request to mount in-process
        {
            service = new DevioShmService(provider, OwnsProvider: true, BufferSize: buffer_size);
        }
        else if (output_image is not null) // Convert to new image file format
        {
            provider.ConvertToImage(image_path, output_image, output_image_variant, detach_event);

            return 0;
        }
        else if (checksum is not null) // Calculate checksum over image
        {
            provider.Checksum(checksum);

            return 0;
        }
        else
        {
            provider.Dispose();

            Console.WriteLine("None of --name, --port, --mount, --checksum or --convert switches specified, nothing to do.");

            return 1;
        }

        if (mount)
        {
            Console.WriteLine("Mounting as virtual disk...");

            service.WriteOverlayImageName = write_overlay_image_file;

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

            if (disk_size.HasValue)
            {
                service.DiskSize = disk_size.Value;
            }

            service.StartServiceThreadAndMount(adapter, device_flags);

            if (auto_delete)
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

        else
        {

            service.StartServiceThread();

        }

        if (detach_event is not null)
        {

            if (mount)
            {

                Console.WriteLine($"Virtual disk created. To dismount, type aim_cli --dismount={service.DiskDeviceNumber:X6}");
            }

            else
            {

                Console.WriteLine("Image file opened, ready for incoming connections.");

            }

            CloseConsole(detach_event);
        }

        else
        {

            if (mount)
            {
                Console.WriteLine("Virtual disk created. Press Ctrl+C to remove virtual disk and exit.");
            }
            else
            {
                Console.WriteLine("Image file opened, waiting for incoming connections. Press Ctrl+C to exit.");
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                try
                {
                    Console.WriteLine("Stopping service...");
                    service.Dispose();
                    e.Cancel = true;
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

        service.WaitForServiceThreadExit();

        Console.WriteLine("Service stopped.");

        return service.Exception is not null
            ? throw new Exception("Service failed.", service.Exception)
            : 0;
    }
}