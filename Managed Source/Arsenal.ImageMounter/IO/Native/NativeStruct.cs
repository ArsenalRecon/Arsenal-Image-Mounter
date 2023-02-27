//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Devices;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
#if NET6_0_OR_GREATER
using System.Collections.Immutable;
#endif
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0056 // Use index operator
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO.Native;

public static class NativeStruct
{
    public static bool IsOsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static long GetFileSize(string path) => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new FileInfo(path).Length : NativeFileIO.GetFileSize(path);

    public static byte[] ReadAllBytes(string path)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return File.ReadAllBytes(path);
        }

        using var stream = NativeFileIO.OpenFileStream(path,
                                                       FileMode.Open,
                                                       FileAccess.Read,
                                                       FileShare.Read | FileShare.Delete,
                                                       65536,
                                                       NativeConstants.FILE_FLAG_BACKUP_SEMANTICS);

        var buffer = new byte[stream.Length];

        return stream.Read(buffer, 0, buffer.Length) != stream.Length
            ? throw new IOException($"Incomplete read from '{path}'")
            : buffer;
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP

    public static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }

        using var stream = NativeFileIO.OpenFileStream(path,
                                                       FileMode.Open,
                                                       FileAccess.Read,
                                                       FileShare.Read | FileShare.Delete,
                                                       65536,
                                                       NativeConstants.FILE_FLAG_BACKUP_SEMANTICS | FileOptions.Asynchronous);

        var buffer = new byte[stream.Length];

        return await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) != stream.Length
            ? throw new IOException($"Incomplete read from '{path}'")
            : buffer;
    }

#endif

    public static long GetFileOrDiskSize(string imagefile)
        => imagefile.StartsWith("/dev/", StringComparison.Ordinal)
            || RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && (imagefile.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) || imagefile.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
            && !HasExtension(imagefile)
            && (!NativeFileIO.TryGetFileAttributes(imagefile, out var attributes) || attributes.HasFlag(FileAttributes.Directory))
            ? GetDiskSize(imagefile)
            : GetFileSize(imagefile);

    public static bool HasExtension(string filepath) =>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        !Path.GetExtension(filepath.AsSpan()).IsEmpty;
#else
        !string.IsNullOrEmpty(Path.GetExtension(filepath));
#endif

    public static long GetDiskSize(string imagefile)
    {
        using var disk = new DiskDevice(imagefile, FileAccess.Read);

        var diskSize = disk.DiskSize;

        return diskSize ?? throw new NotSupportedException($"Failed to identify size of device '{imagefile}'");
    }

    public static long? GetDiskSize(SafeFileHandle SafeFileHandle)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? NativeFileIO.GetDiskSize(SafeFileHandle)
            : NativeUnixIO.GetDiskSize(SafeFileHandle);

    public static DISK_GEOMETRY? GetDiskGeometry(SafeFileHandle SafeFileHandle)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? NativeFileIO.GetDiskGeometry(SafeFileHandle)
            : NativeUnixIO.GetDiskGeometry(SafeFileHandle);

    /// <summary>
    /// Calls Win32 API CreateFile() function and encapsulates returned handle in a SafeFileHandle object.
    /// </summary>
    /// <param name="FileName">Name of file to open.</param>
    /// <param name="DesiredAccess">File access to request.</param>
    /// <param name="ShareMode">Share mode to request.</param>
    /// <param name="CreationDisposition">Open/creation mode.</param>
    /// <param name="Overlapped">Specifies whether to request overlapped I/O.</param>
    public static SafeFileHandle OpenFileHandle(string FileName, FileAccess DesiredAccess, FileShare ShareMode, FileMode CreationDisposition, bool Overlapped)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? NativeFileIO.OpenFileHandle(FileName, DesiredAccess, ShareMode, CreationDisposition, Overlapped)
            : new FileStream(FileName, CreationDisposition, DesiredAccess, ShareMode, 1, Overlapped).SafeFileHandle;

    private static readonly IReadOnlyDictionary<string, long> KnownFormatsOffsets
        = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "nrg", 600 << 9 },
            { "sdi", 8 << 9 }
        }
#if NET6_0_OR_GREATER
        .ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
#else
        .AsReadOnly();
#endif

    /// <summary>
    /// Checks if filename contains a known extension for which PhDskMnt knows of a constant offset value. That value can be
    /// later passed as Offset parameter to CreateDevice method.
    /// </summary>
    /// <param name="ImageFile">Name of disk image file.</param>
    public static long GetOffsetByFileExt(string ImageFile) => KnownFormatsOffsets.TryGetValue(Path.GetExtension(ImageFile), out var Offset) ? Offset : 0;

    /// <summary>
    /// Returns sector size typically used for image file name extensions. Returns 2048 for
    /// .iso, .nrg and .bin. Returns 512 for all other file name extensions.
    /// </summary>
    /// <param name="imagefile">Name of disk image file.</param>
    public static uint GetSectorSizeFromFileName(string imagefile)
    {
        imagefile.NullCheck(nameof(imagefile));

        return imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) || imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) || imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)
            ? 2048U
            : 512U;
    }

    private static readonly IReadOnlyList<(ulong Size, string Suffix)> Multipliers = new List<(ulong Size, string Suffix)>
    {
        (1UL << 60, " EB"),
        (1UL << 50, " PB"),
        (1UL << 40, " TB"),
        (1UL << 30, " GB"),
        (1UL << 20, " MB"),
        (1UL << 10, " KB")
    };

    public static string FormatBytes(ulong size)
    {
        foreach (var (Size, Suffix) in Multipliers)
        {
            if (size >= Size)
            {
                return $"{size / (double)Size:0.0}{Suffix}";
            }
        }

        return $"{size} byte";
    }

    private static readonly ConcurrentDictionary<int, string> PrecisionFormatStrings = new();

    public static string FormatBytes(ulong size, int precision)
    {
        foreach (var (Size, Suffix) in Multipliers)
        {
            if (size >= Size)
            {
                var precisionFormatString =
                    PrecisionFormatStrings.GetOrAdd(precision,
                                                    precision => $"0.{new string('0', precision - 1)}");

                return $"{(size / (double)Size).ToString(precisionFormatString)}{Suffix}";
            }
        }

        return $"{size} byte";
    }

    public static string FormatBytes(long size)
    {
        foreach (var (Size, Suffix) in Multipliers)
        {
            if (Math.Abs(size) >= (long)Size)
            {
                return $"{size / (double)Size:0.0}{Suffix}";
            }
        }

        return size == 1L ? $"{size} byte" : $"{size} bytes";
    }

    public static string FormatBytes(long size, int precision)
    {
        foreach (var (Size, Suffix) in Multipliers)
        {
            if (size >= (long)Size)
            {
                var precisionFormatString =
                    PrecisionFormatStrings.GetOrAdd(precision,
                                                    precision => $"0.{new string('0', precision - 1)}");

                return $"{(size / (double)Size).ToString(precisionFormatString)}{Suffix}";
            }
        }

        return $"{size} byte";
    }

    public static long? ParseSuffixedSize(string Str)
        => TryParseSuffixedSize(Str, out var result) ? result : null;

    public static bool TryParseSuffixedSize(string Str, out long ParseSuffixedSizeRet)
    {
        ParseSuffixedSizeRet = 0;

        if (string.IsNullOrEmpty(Str))
        {
            return false;
        }

        if (Str.StartsWith("0x", StringComparison.Ordinal) || Str.StartsWith("&H", StringComparison.Ordinal))
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            return long.TryParse(Str.AsSpan(2), NumberStyles.AllowHexSpecifier, provider: null, out ParseSuffixedSizeRet);
#else
            return long.TryParse(Str.Substring(2), NumberStyles.AllowHexSpecifier, provider: null, out ParseSuffixedSizeRet);
#endif
        }

        var Suffix = Str[Str.Length - 1];

        if (char.IsLetter(Suffix))
        {
            var factor = char.ToUpper(Suffix) switch
            {
                'E' => 1L << 60,
                'P' => 1L << 50,
                'T' => 1L << 40,
                'G' => 1L << 30,
                'M' => 1L << 20,
                'K' => 1L << 10,
                _ => throw new FormatException($"Bad suffix: {Suffix}"),
            };

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            if (long.TryParse(Str.AsSpan(0, Str.Length - 1), NumberStyles.Any, provider: null, out ParseSuffixedSizeRet))
            {
                ParseSuffixedSizeRet *= factor;
                return true;
            }
#else
            if (long.TryParse(Str.Substring(0, Str.Length - 1), NumberStyles.Any, provider: null, out ParseSuffixedSizeRet))
            {
                ParseSuffixedSizeRet *= factor;
                return true;
            }
#endif

            return false;
        }
        else
        {
            return long.TryParse(Str, NumberStyles.Any, provider: null, out ParseSuffixedSizeRet);
        }
    }

    public static byte[] ParseHexString(string str)
    {
        var bytes = new byte[(str.Length >> 1)];

        for (int i = 0, loopTo = bytes.Length - 1; i <= loopTo; i++)
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            bytes[i] = byte.Parse(str.AsSpan(i << 1, 2), NumberStyles.HexNumber);
#else
            bytes[i] = byte.Parse(str.Substring(i << 1, 2), NumberStyles.HexNumber);
#endif
        }

        return bytes;
    }

    public static IEnumerable<byte> ParseHexString(IEnumerable<char> str)
    {
        var buffer = new char[2];

        foreach (var c in str)
        {
            if (buffer[0] == default(char))
            {
                buffer[0] = c;
            }
            else
            {
                buffer[1] = c;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
                yield return byte.Parse(buffer, NumberStyles.HexNumber);
#else
                yield return byte.Parse(new string(buffer), NumberStyles.HexNumber);
#endif

                Array.Clear(buffer, 0, 2);
            }
        }
    }

    public static byte[] ParseHexString(string str, int offset, int count)
    {
        var bytes = new byte[(count >> 1)];

        for (int i = 0, loopTo = count - 1; i <= loopTo; i++)
        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
            bytes[i] = byte.Parse(str.AsSpan(i + offset << 1, 2), NumberStyles.HexNumber);
#else
            bytes[i] = byte.Parse(str.Substring(i + offset << 1, 2), NumberStyles.HexNumber);
#endif
        }

        return bytes;
    }

    /// <summary>
    /// Checks if Flags specifies a read only virtual disk.
    /// </summary>
    /// <param name="Flags">Flag field to check.</param>
    public static bool IsReadOnly(this DeviceFlags Flags) => Flags.HasFlag(DeviceFlags.ReadOnly);

    /// <summary>
    /// Checks if Flags specifies a removable virtual disk.
    /// </summary>
    /// <param name="Flags">Flag field to check.</param>
    public static bool IsRemovable(this DeviceFlags Flags) => Flags.HasFlag(DeviceFlags.Removable);

    /// <summary>
    /// Checks if Flags specifies a modified virtual disk.
    /// </summary>
    /// <param name="Flags">Flag field to check.</param>
    public static bool IsModified(this DeviceFlags Flags) => Flags.HasFlag(DeviceFlags.Modified);

    /// <summary>
    /// Gets device type bits from a Flag field.
    /// </summary>
    /// <param name="Flags">Flag field to check.</param>
    public static DeviceFlags GetDeviceType(this DeviceFlags Flags) => (DeviceFlags)((uint)Flags & 0xF0U);

    /// <summary>
    /// Gets disk type bits from a Flag field.
    /// </summary>
    /// <param name="Flags">Flag field to check.</param>
    public static DeviceFlags GetDiskType(this DeviceFlags Flags) => (DeviceFlags)((uint)Flags & 0xF00U);

    /// <summary>
    /// Gets proxy type bits from a Flag field.
    /// </summary>
    /// <param name="Flags">Flag field to check.</param>
    public static DeviceFlags GetProxyType(this DeviceFlags Flags) => (DeviceFlags)((uint)Flags & 0xF000U);

}
