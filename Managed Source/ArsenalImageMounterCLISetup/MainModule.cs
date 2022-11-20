using Arsenal.ImageMounter.IO.Native;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using static Arsenal.ImageMounter.API;
// '''' MainModule.vb
// '''' Console driver setup application, for scripting and similar.
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

using static Arsenal.ImageMounter.DriverSetup;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter;

public static class MainModule
{

    private static readonly Dialogs.NativeWindowHandle ownerWindow = new(NativeFileIO.SafeNativeMethods.GetConsoleWindow());

    public static int Main(params string[] args)
    {

        Trace.Listeners.Add(new ConsoleTraceListener());

        var opMode = OpMode.Status;

        if (args is not null && args.Length > 0)
        {
            if (args[0].Equals("/install", StringComparison.OrdinalIgnoreCase))
            {
                opMode = OpMode.Install;
            }
            else if (args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
            {
                opMode = OpMode.Uninstall;
            }
            else if (args[0].Equals("/status", StringComparison.OrdinalIgnoreCase))
            {
                opMode = OpMode.Status;
            }
            else
            {
                Trace.WriteLine($"Syntax: {Assembly.GetExecutingAssembly().GetName().Name} /install|/uninstall|/status");
            }
        }

        try
        {
            return SetupOperation(opMode);
        }
        catch (Exception ex)
        {
            if (ex is Win32Exception win32exception)
            {
                Trace.WriteLine($"Win32 error: {win32exception.NativeErrorCode}");
            }

            Trace.WriteLine(ex.ToString());
            return -1;
        }
    }

    public static int SetupOperation(OpMode opMode)
    {

        Trace.WriteLine($"Kernel type: {Kernel}");
        Trace.WriteLine($"Kernel supports StorPort: {HasStorPort}");

        switch (opMode)
        {

            case OpMode.Install:
                {
                    using (var zipStream = typeof(MainModule).Assembly.GetManifestResourceStream(typeof(MainModule), "DriverFiles.zip")
                        ?? throw new KeyNotFoundException("Cannot find embedded DriverFiles.zip"))
                    {
                        InstallFromZipStream(ownerWindow, zipStream);
                    }

                    try
                    {
                        using (new ScsiAdapter())
                        {
                        }

                        Trace.WriteLine("Driver successfully installed.");
                        return 0;
                    }
                    catch
                    {
                        Trace.WriteLine("A reboot may be required to complete driver setup.");
                        return 1;
                    }
                }

            case OpMode.Uninstall:
                {
                    if (AdapterDevicePresent)
                    {
                        Uninstall(ownerWindow);
                        Trace.WriteLine("Driver successfully uninstalled.");

                        try
                        {
                            using (new ScsiAdapter())
                            {
                            }

                            Trace.WriteLine("A reboot may be required to complete driver setup.");
                            return 2;
                        }
                        catch
                        {
                            return 0;
                        }
                    }
                    else
                    {
                        Trace.WriteLine("Virtual SCSI adapter not installed.");
                        return 1;
                    }
                }

            case OpMode.Status:
                {
                    if (AdapterDevicePresent)
                    {
                        Trace.WriteLine("Virtual SCSI adapter installed.");
                        return 0;
                    }
                    else
                    {
                        Trace.WriteLine("Virtual SCSI adapter not installed.");
                        return 1;
                    }
                }

            default:
                {
                    return -1;
                }
        }
    }
}

public enum OpMode
{
    Install,
    Uninstall,
    Status
}

