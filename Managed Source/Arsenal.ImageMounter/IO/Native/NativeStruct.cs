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
using LTRData.Extensions.Buffers;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
#if NET6_0_OR_GREATER
using System.Collections.Immutable;
#endif
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0056 // Use index operator


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
