﻿//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Streams;
using LTRData.Extensions.Async;
using LTRData.Extensions.Formatting;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Arsenal.ImageMounter;

internal static class ImageConversions
{

    public static void Checksum(this IDevioProvider provider, string[] checksums)
    {
        using (provider)
        using (var cancel = new CancellationTokenSource())
        {
            Console.WriteLine($"Calculating checksums {string.Join(", ", checksums)}...");

            var completionPosition = new CompletionPosition(provider.Length);

            Console.CancelKeyPress += (sender, e) =>
            {
                try
                {
                    Console.WriteLine();
                    Console.WriteLine("Stopping...");
                    _ = cancel.CancelAsync();
                    e.Cancel = true;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                    Console.WriteLine(ex.JoinMessages());
                }
            };

            var hashResults = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);

            var t = Task.Run(() =>
            {
                Console.WriteLine($"Bytes per sector: {provider.SectorSize}");
                Console.WriteLine();
                Console.WriteLine($"Start time: {DateTime.Now}");

                foreach (var checksum in checksums)
                {
                    hashResults.Add(checksum, null);
                }

                provider.WriteToSkipEmptyBlocks(null,
                                                ProviderSupport.ImageConversionIoBufferSize,
                                                skipWriteZeroBlocks: true,
                                                adjustTargetSize: false,
                                                hashResults,
                                                completionPosition,
                                                cancel.Token);

            }, cancel.Token);

            if (completionPosition is null)
            {
                t.GetAwaiter().GetResult();
            }
            else
            {
                var update_time = TimeSpan.FromMilliseconds(400);

                try
                {
                    while (!t.Wait(update_time))
                    {
                        Console.Write($"Reading ({completionPosition.PercentComplete:0.0}%, ETR {completionPosition.EstimatedTimeRemaining:d\\.hh\\:mm\\:ss})...\r");
                    }

                    Console.Write($"Finished: {DateTime.Now}");

                    Console.WriteLine(new string(' ', Console.WindowWidth - Console.CursorLeft - 1));

                    Console.WriteLine();

                    if (hashResults is not null)
                    {
                        Console.WriteLine($"Calculated checksums:");

                        foreach (var hash in hashResults)
                        {
                            Console.WriteLine($"{hash.Key}: {hash.Value?.ToHexString()}");
                        }
                    }
                }
                finally
                {
                    Console.WriteLine();
                }
            }
        }
    }

    public static void ConvertToImage(this IDevioProvider provider, string sourcePath, string outputImage, string OutputImageVariant, SafeWaitHandle? detachEvent)
    {
        using (provider)
        using (var cancel = new CancellationTokenSource())
        {
            var cancellationToken = cancel.Token;

            Console.WriteLine($"Converting to new image file '{outputImage}'...");

            CompletionPosition? completionPosition = null;

            if (detachEvent is not null)
            {
                ConsoleAppHelpers.CloseConsole(detachEvent);
            }
            else
            {
                completionPosition = new CompletionPosition(provider.Length);

                Console.CancelKeyPress += (sender, e) =>
                {
                    try
                    {
                        Console.WriteLine();
                        Console.WriteLine("Stopping...");
                        cancel.CancelAsync();
                        e.Cancel = true;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex.ToString());
                        Console.WriteLine(ex.JoinMessages());
                    }
                };
            }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            var image_type = Path.GetExtension(outputImage.AsSpan()).TrimStart('.').ToString().ToUpperInvariant();
#else
            var image_type = Path.GetExtension(outputImage).TrimStart('.').ToUpperInvariant();
#endif

            StreamWriter? metafile = null;

            Dictionary<string, byte[]?>? hashResults = null;

            var t = Task.Run(() =>
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                    string.IsNullOrWhiteSpace(image_type) &&
                    (outputImage.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                    outputImage.StartsWith(@"\\.\", StringComparison.Ordinal)))
                {
                    provider.WriteToPhysicalDisk(outputImage, completionPosition, cancellationToken);

                    return;
                }

                var metafilename = $"{outputImage}.txt";
                metafile = new StreamWriter(metafilename, append: false);

                metafile.WriteLine($"Created by Arsenal Image Mounter version {ConsoleApp.AssemblyFileVersion}");
                metafile.WriteLine($"Running on machine '{Environment.MachineName}' with {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture} and {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");
                metafile.WriteLine($"Saved from '{sourcePath}'");
                metafile.WriteLine($"Disk size: {provider.Length} bytes");
                metafile.WriteLine($"Bytes per sector: {provider.SectorSize}");
                metafile.WriteLine();
                metafile.WriteLine($"Start time: {DateTime.Now}");
                metafile.Flush();

                hashResults = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase)
                {
                    { "MD5", null },
                    { "SHA1", null },
                    { "SHA256", null }
                };

                switch (image_type)
                {
                    case "DD":
                    case "RAW":
                    case "IMG":
                    case "IMA":
                    case "ISO":
                    case "BIN":
                    case "001":
                        provider.ConvertToRawImage(outputImage, OutputImageVariant, hashResults, completionPosition, cancellationToken);
                        break;

                    case "E01":
                    case "EX01":
                    case "S01":
                        provider.ConvertToLibEwfImage(outputImage, image_type, hashResults, completionPosition, cancellationToken);
                        break;

                    default:
                        provider.ConvertToDiscUtilsImage(outputImage, image_type, OutputImageVariant, hashResults, completionPosition, cancellationToken);
                        break;
                }
            }, cancellationToken);

            using (metafile)
            {
                if (completionPosition is null)
                {
                    t.GetAwaiter().GetResult();
                }
                else
                {
                    var update_time = TimeSpan.FromMilliseconds(400d);

                    try
                    {
                        while (!t.Wait(update_time))
                        {
                            Console.Write($"Converting ({completionPosition.PercentComplete:0.0}%, ETR {completionPosition.EstimatedTimeRemaining:d\\.hh\\:mm\\:ss})...\r");
                        }

                        Console.Write($"Conversion finished.");

                        Console.WriteLine(new string(' ', Console.WindowWidth - Console.CursorLeft - 1));

                        if (metafile is not null)
                        {
                            metafile.WriteLine($"Finish time: {DateTime.Now}");
                            metafile.WriteLine();

                            if (hashResults is not null)
                            {
                                metafile.WriteLine($"Calculated checksums:");

                                foreach (var hash in hashResults)
                                {
                                    metafile.WriteLine($"{hash.Key}: {hash.Value?.ToHexString()}");
                                }

                                metafile.WriteLine();
                            }

                            metafile.Flush();
                        }
                    }
                    finally
                    {
                        Console.WriteLine();
                    }

                    Console.WriteLine("Image converted successfully.");
                }
            }
        }
    }
}