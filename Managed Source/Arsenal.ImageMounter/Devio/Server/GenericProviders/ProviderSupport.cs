//  ProviderSupport.vb
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Collections;
using Arsenal.ImageMounter.Devio.Server.Interaction;
using Arsenal.ImageMounter.Devio.Server.SpecializedProviders;
using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Devices;
using Arsenal.ImageMounter.IO.Native;
using Arsenal.ImageMounter.IO.Streams;
using DiscUtils;
using LTRData.Extensions.Formatting;
using LTRData.Extensions.Native;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression

#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.Devio.Server.GenericProviders;

public static class ProviderSupport
{
    public static int ImageConversionIoBufferSize { get; set; } = 2 << 20;

    public static long GetVBRPartitionLength(this IDevioProvider baseProvider)
    {
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(baseProvider);
#else
        if (baseProvider is null)
        {
            throw new ArgumentNullException(nameof(baseProvider));
        }
#endif

        var bytesPerSector = (int)baseProvider.SectorSize;

        byte[]? allocated = null;

        var vbr = bytesPerSector <= 1024
            ? stackalloc byte[bytesPerSector]
            : (allocated = ArrayPool<byte>.Shared.Rent(bytesPerSector)).AsSpan(0, bytesPerSector);

        try
        {
            if (baseProvider.Read(vbr, 0) < bytesPerSector)
            {
                return 0;
            }

            var vbr_sector_size = MemoryMarshal.Read<short>(vbr.Slice(0xB));

            if (vbr_sector_size <= 0)
            {
                return 0;
            }

            var sector_bits = 0;
            var sector_shift = vbr_sector_size;

            while ((sector_shift & 1) == 0)
            {
                sector_shift >>= 1;
                sector_bits++;
            }

            if (sector_shift != 1)
            {
                throw new InvalidDataException($"Invalid VBR sector size: {vbr_sector_size} bytes");
            }

            long total_sectors;

            total_sectors = MemoryMarshal.Read<ushort>(vbr.Slice(0x13));

            if (total_sectors == 0)
            {
                total_sectors = MemoryMarshal.Read<uint>(vbr.Slice(0x20));
            }

            if (total_sectors == 0)
            {
                total_sectors = MemoryMarshal.Read<long>(vbr.Slice(0x28));
            }

            return total_sectors < 0 ? 0 : (total_sectors << sector_bits);
        }
        finally
        {
            if (allocated is not null)
            {
                ArrayPool<byte>.Shared.Return(allocated);
            }
        }
    }

    public static IEnumerable<string> EnumerateMultiSegmentFiles(string FirstFile)
    {

        var found = false;

#if NET6_0_OR_GREATER

        var pathpart = Path.GetDirectoryName(FirstFile.AsSpan());
        var filepart = Path.GetFileNameWithoutExtension(FirstFile.AsSpan());
        var extension = Path.GetExtension(FirstFile.AsSpan());

        if (extension.EndsWith("01", StringComparison.Ordinal) ||
            extension.EndsWith("00", StringComparison.Ordinal))
        {
            static string? GetNextSegmentFile(string currentFile)
            {
                for (var pos = currentFile.Length - 1; pos >= 0; pos--)
                {
                    if (currentFile[pos] >= '0' && currentFile[pos] < '9')
                    {
                        currentFile = $"{currentFile.AsSpan()[..pos]}{(char)(currentFile[pos] + 1)}{currentFile.AsSpan()[(pos + 1)..]}";
                        return currentFile;
                    }
                    else if (currentFile[pos] == '9')
                    {
                        currentFile = $"{currentFile.AsSpan()[..pos]}0{currentFile.AsSpan()[(pos + 1)..]}";
                    }
                    else if (currentFile[pos] >= 'A' && currentFile[pos] <= 'Z'
                        && pos < (currentFile.Length - 1)
                        && currentFile.Skip(pos + 1).All('0'.Equals))
                    {
                        currentFile = $"{currentFile.AsSpan()[..(pos + 1)]}{new string('A', currentFile.Length - pos - 1)}";
                        return currentFile;
                    }
                    else if (currentFile[pos] >= 'a' && currentFile[pos] <= 'z'
                        && pos < (currentFile.Length - 1)
                        && currentFile.Skip(pos + 1).All('0'.Equals))
                    {
                        currentFile = $"{currentFile.AsSpan()[..(pos + 1)]}{new string('a', currentFile.Length - pos - 1)}";
                        return currentFile;
                    }
                    else if ((currentFile[pos] >= 'A' && currentFile[pos] < 'Z')
                        || (currentFile[pos] >= 'a' && currentFile[pos] < 'z'))
                    {
                        currentFile = $"{currentFile.AsSpan()[..pos]}{(char)(currentFile[pos] + 1)}{currentFile.AsSpan()[(pos + 1)..]}";
                        return currentFile;
                    }
                    else if (currentFile[pos] == 'Z' || currentFile[pos] == 'z')
                    {
                        currentFile = $"{currentFile.AsSpan()[..pos]}{(char)(currentFile[pos] - ('Z' - 'A'))}{currentFile.AsSpan()[(pos + 1)..]}";
                    }
                    else
                    {
                        return null;
                    }
                }

                return null;
            }

            for (var currentFile = FirstFile;
                File.Exists(currentFile);
                currentFile = GetNextSegmentFile(currentFile))
            {
                found = true;
                yield return (currentFile);
            }
        }
        else
        {
            if (File.Exists(FirstFile))
            {
                found = true;
                yield return FirstFile;
            }
        }

#else

        var pathpart = Path.GetDirectoryName(FirstFile);
        var filepart = Path.GetFileNameWithoutExtension(FirstFile);
        var extension = Path.GetExtension(FirstFile);

        if (extension.EndsWith("01", StringComparison.Ordinal) ||
            extension.EndsWith("00", StringComparison.Ordinal))
        {
            static string? GetNextSegmentFile(string currentFile)
            {
                for (var pos = currentFile.Length - 1; pos >= 0; pos--)
                {
                    if (currentFile[pos] >= '0' && currentFile[pos] < '9')
                    {
                        currentFile = $"{currentFile.Substring(0, pos)}{(char)(currentFile[pos] + 1)}{currentFile.Substring(pos + 1)}";
                        return currentFile;
                    }
                    else if (currentFile[pos] == '9')
                    {
                        currentFile = $"{currentFile.Substring(0, pos)}0{currentFile.Substring(pos + 1)}";
                    }
                    else if (currentFile[pos] >= 'A' && currentFile[pos] <= 'Z'
                        && pos < (currentFile.Length - 1)
                        && currentFile.Skip(pos + 1).All('0'.Equals))
                    {
                        currentFile = $"{currentFile.Substring(0, pos + 1)}{new string('A', currentFile.Length - pos - 1)}";
                        return currentFile;
                    }
                    else if (currentFile[pos] >= 'a' && currentFile[pos] <= 'z'
                        && pos < (currentFile.Length - 1)
                        && currentFile.Skip(pos + 1).All('0'.Equals))
                    {
                        currentFile = $"{currentFile.Substring(0, pos + 1)}{new string('a', currentFile.Length - pos - 1)}";
                        return currentFile;
                    }
                    else if ((currentFile[pos] >= 'A' && currentFile[pos] < 'Z')
                        || (currentFile[pos] >= 'a' && currentFile[pos] < 'z'))
                    {
                        currentFile = $"{currentFile.Substring(0, pos)}{(char)(currentFile[pos] + 1)}{currentFile.Substring(pos + 1)}";
                        return currentFile;
                    }
                    else if (currentFile[pos] == 'Z' || currentFile[pos] == 'z')
                    {
                        currentFile = $"{currentFile.Substring(0, pos)}{(char)(currentFile[pos] - ('Z' - 'A'))}{currentFile.Substring(pos + 1)}";
                    }
                    else
                    {
                        return null;
                    }
                }

                return null;
            }

            for (var currentFile = FirstFile;
                currentFile is not null && File.Exists(currentFile);
                currentFile = GetNextSegmentFile(currentFile))
            {
                found = true;
                yield return (currentFile);
            }
        }
        else
        {
            if (File.Exists(FirstFile))
            {
                found = true;
                yield return FirstFile;
            }
        }

#endif

        if (!found)
        {
            throw new FileNotFoundException("Image file not found", FirstFile);
        }
    }

    public static void ConvertToDiscUtilsImage(this IDevioProvider provider,
                                               string outputImage,
                                               string type,
                                               string OutputImageVariant,
                                               int? bufferSize,
                                               Dictionary<string, byte[]?>? hashResults,
                                               CompletionPosition? completionPosition,
                                               CancellationToken cancellationToken)
    {
        if (!DevioServiceFactory.DiscUtilsInitialized)
        {
            throw new NotSupportedException("DiscUtils libraries not available");
        }

        using var builder = VirtualDisk.CreateDisk(type, OutputImageVariant, outputImage, provider.Length, Geometry.FromCapacity(provider.Length, (int)provider.SectorSize), null)
            ?? throw new NotSupportedException("Failed to create image file. Unknown image file format?");

        provider.WriteToSkipEmptyBlocks(builder.Content,
                                        bufferSize ?? ImageConversionIoBufferSize,
                                        skipWriteZeroBlocks: true,
                                        adjustTargetSize: false,
                                        hashResults,
                                        completionPosition,
                                        cancellationToken);
    }

    public static void ConvertToRawImage(this IDevioProvider provider,
                                         string outputImage,
                                         string OutputImageVariant,
                                         int? bufferSize,
                                         Dictionary<string, byte[]?>? hashResults,
                                         CompletionPosition? completionPosition,
                                         CancellationToken cancellationToken)
    {
        using var target = new FileStream(outputImage,
                                          FileMode.Create,
                                          FileAccess.Write,
                                          FileShare.Delete,
                                          bufferSize ?? ImageConversionIoBufferSize);

        if ("fixed".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase))
        {
        }
        else if ("dynamic".Equals(OutputImageVariant, StringComparison.OrdinalIgnoreCase))
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
            throw new ArgumentException($"Value {OutputImageVariant} not supported as output image variant. Valid values are fixed or dynamic.");
        }

        provider.WriteToSkipEmptyBlocks(target,
                                        ImageConversionIoBufferSize,
                                        skipWriteZeroBlocks: true,
                                        hashResults: hashResults,
                                        adjustTargetSize: true,
                                        completionPosition: completionPosition,
                                        cancellationToken: cancellationToken);
    }

    public static void WriteToPhysicalDisk(this IDevioProvider provider,
                                           string outputDevice,
                                           int? bufferSize,
                                           CompletionPosition? completionPosition,
                                           CancellationToken cancellationToken)
    {
        using var disk = new DiskDevice(outputDevice, FileAccess.ReadWrite);

        provider.WriteToSkipEmptyBlocks(disk.GetRawDiskStream(),
                                        bufferSize ?? ImageConversionIoBufferSize,
                                        skipWriteZeroBlocks: false,
                                        hashResults: null,
                                        adjustTargetSize: false,
                                        completionPosition: completionPosition,
                                        cancellationToken: cancellationToken);
    }

    public static void ConvertToLibEwfImage(this IDevioProvider provider,
                                            string outputImage,
                                            string image_type,
                                            int? bufferSize,
                                            Dictionary<string, byte[]?>? hashResults,
                                            CompletionPosition? completionPosition, CancellationToken cancellationToken)
    {
        var imaging_parameters = new DevioProviderLibEwf.ImagingParameters
        {
            MediaSize = (ulong)provider.Length,
            BytesPerSector = provider.SectorSize
        };

        switch (image_type)
        {
            case "E01":
                imaging_parameters.UseEWFFileFormat = DevioProviderLibEwf.LIBEWF_FORMAT.ENCASE6;
                imaging_parameters.SegmentFileSize = 2UL << 40;
                break;

            case "EX01":
                imaging_parameters.UseEWFFileFormat = DevioProviderLibEwf.LIBEWF_FORMAT.V2_ENCASE7;
                imaging_parameters.SegmentFileSize = 2UL << 40;
                break;

            case "S01":
                imaging_parameters.UseEWFFileFormat = DevioProviderLibEwf.LIBEWF_FORMAT.SMART;
                imaging_parameters.SegmentFileSize = int.MaxValue;
                break;
        }

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

        using var target = new DevioProviderLibEwf([Path.ChangeExtension(outputImage, null)], DevioProviderLibEwf.AccessFlagsWrite);

        target.SetOutputParameters(imaging_parameters);

        using (var stream = new Client.DevioDirectStream(target, ownsProvider: false))
        {
            provider.WriteToSkipEmptyBlocks(stream,
                                            bufferSize ?? 64 * (int)imaging_parameters.BytesPerSector,
                                            skipWriteZeroBlocks: false,
                                            hashResults: hashResults,
                                            adjustTargetSize: false,
                                            completionPosition: completionPosition,
                                            cancellationToken: cancellationToken);
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
                                              CancellationToken cancellationToken)
    {
        using var hashProviders = new DisposableDictionary<string, HashAlgorithm>(StringComparer.OrdinalIgnoreCase);

        if (hashResults is not null)
        {
            foreach (var hashName in hashResults.Keys)
            {
#pragma warning disable SYSLIB0045 // Type or member is obsolete
                var hashProvider = HashAlgorithm.Create(hashName)
                    ?? throw new NotSupportedException($"Hash algorithm '{hashName}' not supported");
#pragma warning restore SYSLIB0045 // Type or member is obsolete

                hashProvider.Initialize();
                hashProviders.Add(hashName, hashProvider);
            }
        }

        var buffer = new byte[buffersize];

        var count = 0;

        var source_position = 0L;

        for (; ; )
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            Parallel.ForEach(hashProviders.Values,
                hashProvider => hashProvider.TransformBlock(buffer, 0, count, null, 0));

            source_position += count;

            if (completionPosition is not null)
            {
                completionPosition.LengthComplete = source_position;
            }

            cancellationToken.ThrowIfCancellationRequested();

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
                cancellationToken.ThrowIfCancellationRequested();

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
                hashProvider.Value.TransformFinalBlock([], 0, 0);
                hashResults[hashProvider.Key] = hashProvider.Value.Hash;
            }
        }
    }
}
