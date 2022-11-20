using Arsenal.ImageMounter.Collections;
using Arsenal.ImageMounter.Devio.Server.Interaction;
using Arsenal.ImageMounter.Devio.Server.SpecializedProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using Arsenal.ImageMounter.IO.Streams;
using DiscUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
// '''' ProviderSupport.vb
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.GenericProviders;

public static class ProviderSupport
{

    public static int ImageConversionIoBufferSize { get; set; } = 2 << 20;

    public static string[] GetMultiSegmentFiles(string FirstFile)
    {

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP

        var pathpart = Path.GetDirectoryName(FirstFile.AsSpan());
        var filepart = Path.GetFileNameWithoutExtension(FirstFile.AsSpan());
        var extension = Path.GetExtension(FirstFile.AsSpan());
        string[]? foundfiles = null;

        if (extension.EndsWith("01", StringComparison.Ordinal) || extension.EndsWith("00", StringComparison.Ordinal))
        {

            var start = extension.Length - 3;

            while (start >= 0 && char.IsDigit(extension.GetItem(start)))
            {
                start -= 1;
            }

            start += 1;

            var segmentnumberchars = new string('?', extension.Length - start);
            var dir_pattern = string.Concat(filepart, extension.Slice(0, start), segmentnumberchars);
            var dir_name = pathpart.IsWhiteSpace() ? "." : pathpart.ToString();

            try
            {
                foundfiles = Directory.GetFiles(dir_name, dir_pattern);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed enumerating files '{dir_pattern}' in directory '{dir_name}'", ex);
            }

            for (int i = 0, loopTo = foundfiles.Length - 1; i <= loopTo; i++)
            {
                foundfiles[i] = Path.GetFullPath(foundfiles[i]);
            }

            Array.Sort(foundfiles, StringComparer.Ordinal);
        }
        else if (File.Exists(FirstFile))
        {
            foundfiles = new[] { FirstFile };
        }

        return foundfiles is null || foundfiles.Length == 0
            ? throw new FileNotFoundException("Image file not found", FirstFile)
            : foundfiles;

#else

        var pathpart = Path.GetDirectoryName(FirstFile);
        var filepart = Path.GetFileNameWithoutExtension(FirstFile);
        var extension = Path.GetExtension(FirstFile);
        string[]? foundfiles = null;

        if (extension.EndsWith("01", StringComparison.Ordinal) || extension.EndsWith("00", StringComparison.Ordinal))
        {

            var start = extension.Length - 3;

            while (start >= 0 && char.IsDigit(extension, start))
            {
                start -= 1;
            }

            start += 1;

            var segmentnumberchars = new string('?', extension.Length - start);
            var dir_pattern = string.Concat(filepart, extension.Remove(start), segmentnumberchars);
            var dir_name = pathpart;

            if (string.IsNullOrWhiteSpace(dir_name))
            {
                dir_name = ".";
            }

            try
            {
                foundfiles = Directory.GetFiles(dir_name, dir_pattern);
            }

            catch (Exception ex)
            {
                throw new Exception($"Failed enumerating files '{dir_pattern}' in directory '{dir_name}'", ex);

            }

            for (int i = 0, loopTo = foundfiles.Length - 1; i <= loopTo; i++)
            {
                foundfiles[i] = Path.GetFullPath(foundfiles[i]);
            }

            Array.Sort(foundfiles, StringComparer.Ordinal);
        }
        else if (File.Exists(FirstFile))
        {
            foundfiles = new[] { FirstFile };
        }

        return foundfiles is null || foundfiles.Length == 0
            ? throw new FileNotFoundException("Image file not found", FirstFile)
            : foundfiles;

#endif
    }

    public static void ConvertToDiscUtilsImage(this IDevioProvider provider,
                                               string outputImage,
                                               string type,
                                               string OutputImageVariant,
                                               Dictionary<string, byte[]?>? hashResults,
                                               CompletionPosition? completionPosition,
                                               CancellationToken cancel)
    {

        if (!DevioServiceFactory.DiscUtilsInitialized)
        {

            throw new NotSupportedException("DiscUtils libraries not available");

        }

        using var builder = VirtualDisk.CreateDisk(type, OutputImageVariant, outputImage, provider.Length, Geometry.FromCapacity(provider.Length, (int)provider.SectorSize), null);

        provider.WriteToSkipEmptyBlocks(builder.Content,
                                        ImageConversionIoBufferSize,
                                        skipWriteZeroBlocks: true,
                                        adjustTargetSize: false,
                                        hashResults,
                                        completionPosition,
                                        cancel);

    }

    public static void ConvertToRawImage(this IDevioProvider provider,
                                         string outputImage,
                                         string OutputImageVariant,
                                         Dictionary<string, byte[]?>? hashResults,
                                         CompletionPosition? completionPosition,
                                         CancellationToken cancel)
    {

        using var target = new FileStream(outputImage, FileMode.Create, FileAccess.Write, FileShare.Delete, ImageConversionIoBufferSize);

        if ("fixed".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase))
        {
        }
        else if ("dynamic".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase))
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Sparse files not supported on target platform or OS");
            }

            try
            {
                NativeFileIO.SetFileSparseFlag(target.SafeFileHandle, true);
            }
            catch (Exception ex)
            {
                throw new NotSupportedException("Sparse files not supported on target platform or OS", ex);
            }
        }
        else
        {
            throw new ArgumentException($"Value {OutputImageVariant} not supported as output image variant. Valid values are fixed or dynamic.");
        }

        provider.WriteToSkipEmptyBlocks(target,
                                        ImageConversionIoBufferSize,
                                        skipWriteZeroBlocks: true,
                                        hashResults: hashResults,
                                        adjustTargetSize: true,
                                        completionPosition: completionPosition,
                                        cancel: cancel);

    }

    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
    public static void WriteToPhysicalDisk(this IDevioProvider provider,
                                           ReadOnlyMemory<char> outputDevice,
                                           CompletionPosition? completionPosition,
                                           CancellationToken cancel)
    {

        using var disk = new DiskDevice(outputDevice, FileAccess.ReadWrite);

        provider.WriteToSkipEmptyBlocks(disk.GetRawDiskStream(),
                                        ImageConversionIoBufferSize,
                                        skipWriteZeroBlocks: false,
                                        hashResults: null,
                                        adjustTargetSize: false,
                                        completionPosition: completionPosition,
                                        cancel: cancel);

    }

    public static void ConvertToLibEwfImage(this IDevioProvider provider,
                                            string outputImage,
                                            Dictionary<string, byte[]?>? hashResults,
                                            CompletionPosition? completionPosition,
                                            CancellationToken cancel)
    {

        var imaging_parameters = new DevioProviderLibEwf.ImagingParameters()
        {
            MediaSize = (ulong)provider.Length,
            BytesPerSector = provider.SectorSize
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {

            var physical_disk_handle = ((provider as DevioProviderFromStream)?.BaseStream as FileStream)?.SafeFileHandle;

            if (physical_disk_handle is not null)
            {

                var storageproperties = NativeFileIO.GetStorageStandardProperties(physical_disk_handle);
                if (storageproperties.HasValue)
                {

                    imaging_parameters.StorageStandardProperties = storageproperties.Value;
                    Trace.WriteLine($"Source disk vendor '{imaging_parameters.StorageStandardProperties.VendorId}' model '{imaging_parameters.StorageStandardProperties.ProductId}', serial number '{imaging_parameters.StorageStandardProperties.SerialNumber}'");

                }
            }
        }

        using var target = new DevioProviderLibEwf(new[] { Path.ChangeExtension(outputImage, null) }, DevioProviderLibEwf.AccessFlagsWrite);

        target.SetOutputParameters(imaging_parameters);

        using (var stream = new Client.DevioDirectStream(target, ownsProvider: false))
        {
            provider.WriteToSkipEmptyBlocks(stream,
                                            ImageConversionIoBufferSize,
                                            skipWriteZeroBlocks: false,
                                            hashResults: hashResults,
                                            adjustTargetSize: false,
                                            completionPosition: completionPosition,
                                            cancel: cancel);
        }

        if (hashResults is not null)
        {
            foreach (var hash in hashResults)
            {
                if (hash.Value is not null)
                {
                    target.SetOutputHashParameter(hash.Key, hash.Value);
                }
            }
        }
    }

    public static void WriteToSkipEmptyBlocks(this IDevioProvider source,
                                              Stream? target,
                                              int buffersize,
                                              bool skipWriteZeroBlocks,
                                              bool adjustTargetSize,
                                              Dictionary<string, byte[]?>? hashResults,
                                              CompletionPosition? completionPosition,
                                              CancellationToken cancel)
    {

        using var hashProviders = new DisposableDictionary<string, HashAlgorithm>(StringComparer.OrdinalIgnoreCase);

        if (hashResults is not null)
        {
            foreach (var hashName in hashResults.Keys)
            {
                var hashProvider = HashAlgorithm.Create(hashName)
                    ?? throw new NotSupportedException($"Hash algorithm '{hashName}' not supported");

                hashProvider.Initialize();
                hashProviders.Add(hashName, hashProvider);
            }
        }

        var buffer = new byte[buffersize];

        var count = 0;

        var source_position = 0L;

        for (; ; )
        {

            cancel.ThrowIfCancellationRequested();

            var length_to_read = (int)Math.Min(buffer.Length, source.Length - source_position);

            if (length_to_read == 0)
            {

                break;

            }

            count = source.Read(buffer, 0, length_to_read, source_position);

            if (count == 0)
            {

                throw new IOException($"Read error, {length_to_read} bytes from {source_position}");

            }

            Parallel.ForEach(hashProviders.Values, hashProvider => hashProvider.TransformBlock(buffer, 0, count, null, 0));

            source_position += count;

            if (completionPosition is not null)
            {
                completionPosition.LengthComplete = source_position;
            }

            if (target is null)
            {
                continue;
            }

            if (skipWriteZeroBlocks && buffer.IsBufferZero())
            {
                target.Seek(count, SeekOrigin.Current);
            }
            else
            {
                cancel.ThrowIfCancellationRequested();

                target.Write(buffer, 0, count);
            }
        }

        if (target is not null &&
            adjustTargetSize &&
            target.Length != target.Position)
        {

            target.SetLength(target.Position);

        }

        if (hashResults is not null)
        {
            foreach (var hashProvider in hashProviders)
            {
                hashProvider.Value.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                hashResults[hashProvider.Key] = hashProvider.Value.Hash;
            }
        }
    }
}