// 
//  Driver Version / Setup Verify application.
//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using LTRData.Extensions.Buffers;
using LTRData.Extensions.Formatting;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;



namespace Arsenal.ImageMounter;

[SupportedOSPlatform("windows")]
public static class Module1
{

    public static int Main()
    {

        var devices = (from dev in NativeFileIO.QueryDosDevice()
                       where dev.StartsWith("SCSI", StringComparison.OrdinalIgnoreCase) && dev.EndsWith(':')
                       orderby dev
                       select dev).ToList();

        Console.WriteLine("Found SCSI adapters:");

        foreach (var dev in devices)
        {
            Console.Write(dev);
            Console.Write(" => ");
            Console.WriteLine(string.Join(", ", NativeFileIO.QueryDosDevice(dev)));
        }

        Console.WriteLine();
        Console.WriteLine("Attempting to open...");

        foreach (var dev in devices)
        {
            Console.Write(dev);
            Console.Write(" => ");
            try
            {
                using (NativeFileIO.OpenFileHandle($@"\\?\{dev}", FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open, false))
                {
                }

                Console.WriteLine("Successful.");
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");

            }
        }

        Console.WriteLine();
        Console.WriteLine("Attempting to query version...");
        foreach (var dev in devices)
        {
            Console.Write(dev);
            Console.Write(" => ");
            try
            {
                using var h = NativeFileIO.OpenFileHandle($@"\\?\{dev}", FileAccess.ReadWrite, FileShare.ReadWrite, FileMode.Open, false);
                var ReturnCode = CheckDriverVersion(h);
                Console.WriteLine($"Driver version: {ReturnCode:X4}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.JoinMessages()}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Checks if version of running Arsenal Image Mounter SCSI miniport servicing this device object is compatible with this API
    /// library. If this device object is not created by Arsenal Image Mounter SCSI miniport, an exception is thrown.
    /// </summary>
    public static int CheckDriverVersion(SafeFileHandle SafeFileHandle)
    {
        _ = NativeFileIO.PhDiskMntCtl.SendSrbIoControl(SafeFileHandle, NativeFileIO.PhDiskMntCtl.SMP_IMSCSI_QUERY_VERSION, 0U, null, out var ReturnCode);

        return ReturnCode;
    }
}