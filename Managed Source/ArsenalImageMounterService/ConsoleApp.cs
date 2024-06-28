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
using LTRData.Extensions.CommandLine;
using LTRData.Extensions.Formatting;
using System;
using System.Diagnostics;
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

    static ConsoleApp()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            API.AddNativeLibDirectory();
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
            var commands = CommandLineParser.ParseCommandLine(args, StringComparer.OrdinalIgnoreCase);

            if (commands.ContainsKey("background"))
            {
                return ConsoleAppHelpers.StartBackgroundProcess();
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