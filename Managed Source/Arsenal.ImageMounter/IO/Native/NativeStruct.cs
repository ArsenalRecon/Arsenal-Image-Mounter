//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
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
using KnownFormatsOffsetDictionary = System.Collections.Immutable.ImmutableDictionary<string, long>;
#else
using KnownFormatsOffsetDictionary = System.Collections.ObjectModel.ReadOnlyDictionary<string, long>;
#endif
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Arsenal.ImageMounter.IO.Streams;
using System.Security.Cryptography;
using Arsenal.ImageMounter.Collections;
using System.Diagnostics;
using System.Reflection;
using LTRData.Extensions.Formatting;
using System.Text;
using LTRData.Extensions.Native;
using DiscUtils.Streams.Compatibility;
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
#endif

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0056 // Use index operator


namespace Arsenal.ImageMounter.IO.Native;

public static class NativeStruct
{
    public static bool IsOsWindows { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static long GetFileSize(string path)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? NativeFileIO.GetFileSize(path)
        : new FileInfo(path).Length;

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

        var stream = NativeFileIO.OpenFileStream(path,
                                                 FileMode.Open,
                                                 FileAccess.Read,
                                                 FileShare.Read | FileShare.Delete,
                                                 65536,
                                                 NativeConstants.FILE_FLAG_BACKUP_SEMANTICS | FileOptions.Asynchronous);

        await using var _ = stream.ConfigureAwait(false);

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

#if NETCOREAPP
    [SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
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

    private static readonly KnownFormatsOffsetDictionary KnownFormatsOffsets
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
#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(imagefile);
#else
        if (imagefile is null)
        {
            throw new ArgumentNullException(nameof(imagefile));
        }
#endif

        return imagefile.EndsWith(".iso", StringComparison.OrdinalIgnoreCase) ||
            imagefile.EndsWith(".nrg", StringComparison.OrdinalIgnoreCase) ||
            imagefile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)
            ? 2048U
            : 512U;
    }

    /// <summary>
    /// Copies data asynchronously from one stream to another, optionally skipping blocks with all zeros, adjust target size to source size, calculating hash over copied data etc.
    /// </summary>
    /// <param name="source">Source stream to copy from</param>
    /// <param name="target">Target stream to copy to</param>
    /// <param name="sourceLength">Total length of source to copy. This can be null if source length is not known, in which case data will be copied until end of stream.</param>
    /// <param name="bufferSize">Number of bytes to copy in each iteration</param>
    /// <param name="skipWriteZeroBlocks">Skip writing blocks with all zeros. If true, target position is instead adjusted forward with the same size instead of writing anything when a block with all zeros is read from source</param>
    /// <param name="adjustTargetSize">Adjusts size of target stream to <paramref name="sourceLength"/></param>
    /// <param name="hashResults">If supplied, calculates hashes of each named algorithm in dictionary keys and places calculated hashes as values for each of the keys</param>
    /// <param name="completionPosition">An object that is continously updated with number of bytes copied so far</param>
    /// <param name="cancellationToken">Token checked for cancellation during copy</param>
    /// <returns>Awaitable task</returns>
    /// <exception cref="NotSupportedException">One of hash algorithms in <paramref name="hashResults"/> is not supported</exception>
    public static async Task CopyToSkipEmptyBlocksAsync(this Stream source,
                                                        Stream target,
                                                        long? sourceLength,
                                                        int bufferSize,
                                                        bool skipWriteZeroBlocks,
                                                        bool adjustTargetSize,
                                                        Dictionary<string, byte[]?>? hashResults,
                                                        CompletionPosition? completionPosition,
                                                        CancellationToken cancellationToken)
    {
        Trace.WriteLine($"Starting copy {sourceLength} bytes stream, sourceLength = {sourceLength}, bufferSize = {bufferSize}, skipWriteZeroBlocks = {skipWriteZeroBlocks}");

        using var hashProviders = new DisposableDictionary<string, HashAlgorithm>(StringComparer.OrdinalIgnoreCase);

        if (hashResults is not null)
        {
            foreach (var hashName in hashResults.Keys)
            {
#pragma warning disable SYSLIB0045 // Type or member is obsolete
                var hashProvider = HashAlgorithm.Create(hashName)
                    ?? throw new NotSupportedException($"Hash algorithm {hashName} not supported");
#pragma warning restore SYSLIB0045 // Type or member is obsolete

                hashProvider.Initialize();
                hashProviders.Add(hashName, hashProvider);
            }
        }

        var buffer1 = new byte[bufferSize];
        var buffer2 = new byte[bufferSize];

        ValueTask<int> read_task = default;
        ValueTask write_task = default;

        var count = 0;

        for (; ; )
        {
            var length_to_read = sourceLength.HasValue
                ? (int)Math.Min(buffer2.Length, sourceLength.Value - source.Position)
                : buffer2.Length;

            if (length_to_read > 0)
            {
                read_task = source.ReadAsync(buffer2.AsMemory(0, length_to_read), cancellationToken);
            }
            else
            {
                read_task = default;
            }

            if (count > 0)
            {
                if (skipWriteZeroBlocks && buffer1.IsBufferZero())
                {
                    write_task = default;
                    target.Seek(count, SeekOrigin.Current);
                }
                else
                {
                    write_task = target.WriteAsync(buffer1.AsMemory(0, count), cancellationToken);
                }
            }

            count = await read_task.ConfigureAwait(false);

            if (count > 0 && hashProviders.Count > 0)
            {
                Parallel.ForEach(hashProviders.Values, hashProvider => hashProvider.TransformBlock(buffer2, 0, count, null, 0));
            }

            await write_task.ConfigureAwait(false);

            if (completionPosition is not null)
            {
                completionPosition.LengthComplete = target.Position;
            }

            if (count <= 0)
            {
                break;
            }

            (buffer2, buffer1) = (buffer1, buffer2);
        }

        Trace.WriteLine($"Finished copy {target.Position} bytes");

        if (adjustTargetSize &&
            target.Length != target.Position)
        {
            target.SetLength(target.Position);
        }

        await target.FlushAsync(cancellationToken).ConfigureAwait(false);

        foreach (var hashProvider in hashProviders)
        {
            hashProvider.Value.TransformFinalBlock([], 0, 0);
            hashResults![hashProvider.Key] = hashProvider.Value.Hash!;
            Trace.WriteLine($"{hashProvider.Key}: {hashProvider.Value.Hash?.ToHexString()}");
        }
    }

    /// <summary>
    /// Gets a reference to named resource data embedded in assembly
    /// </summary>
    /// <param name="assembly">Assembly to search for resource</param>
    /// <param name="resourceKey">Name of embedded resource</param>
    /// <returns>Span reference to embedded data</returns>
    public static ReadOnlySpan<byte> GetManifestResourceSpan(this Assembly assembly, string resourceKey)
    {
        using var resource = assembly.GetManifestResourceStream(resourceKey);

        if (resource is UnmanagedMemoryStream unmanagedMemoryStream)
        {
            return unmanagedMemoryStream.AsSpan();
        }
        else if (resource is MemoryStream memoryStream)
        {
            return memoryStream.AsSpan();
        }
        else
        {
            return default;
        }
    }

    /// <summary>
    /// Gets a named resource string embedded in assembly
    /// </summary>
    /// <param name="assembly">Assembly to search for resource</param>
    /// <param name="resourceKey">Name of embedded resource</param>
    /// <param name="encoding">Encoding of string</param>
    /// <returns>Copy of embedded string</returns>
    public static unsafe string? GetManifestResourceString(this Assembly assembly, string resourceKey, Encoding encoding)
    {
        using var resource = assembly.GetManifestResourceStream(resourceKey);

        if (resource is UnmanagedMemoryStream unmanagedMemoryStream)
        {
            return encoding.GetString(unmanagedMemoryStream.PositionPointer, (int)unmanagedMemoryStream.Length);
        }
        else if (resource is MemoryStream memoryStream)
        {
            return encoding.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
        }
        else if (resource is Stream stream)
        {
            using var reader = new StreamReader(stream, encoding);
            return reader.ReadToEnd();
        }
        else
        {
            return null;
        }
    }

#if NET5_0_OR_GREATER
    /// <summary>
    /// The cpuid string from CPU
    /// </summary>
    public static string? CpuId { get; } = GetCpuId();

    public static bool IsIntel { get; } = CpuId is { } cpuid && cpuid == CpuIdGenuineIntel;

    public static bool IsAmd { get; } = CpuId is { } cpuid && cpuid == CpuIdAuthenticAMD;

    /// <summary>
    /// The hypervisor id string from current hypervisor, or null if no hypervisor is running on CPU
    /// </summary>
    public static string? HypervisorId { get; } = GetHypervisorId();

    public static bool HostCpuSupportsCet { get; } = GetHostCpuSupportsCet();

    public static bool HostCpuSupportsNestedVirtualization { get; } = GetHostCpuSupportsNestedVirtualization();

    private static bool GetHostCpuSupportsCet()
    {
        if (!X86Base.IsSupported)
        {
            return false;
        }

        var (_, _, Ecx, _) = X86Base.CpuId(0x07, 0);

        return (Ecx & (1 << 7)) != 0;
    }

    private static bool GetHostCpuSupportsNestedVirtualization() =>
        (CpuId == CpuIdGenuineIntel
        && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10000)) ||  // Intel and Windows 10/Server 2016
        (CpuId == CpuIdAuthenticAMD
        && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20000)); // AMD and Windows 11/Server 2022

    public const string MicrosoftHvId = "Microsoft Hv";

    public const string CpuIdGenuineIntel = "GenuineIntel";

    public const string CpuIdAuthenticAMD = "AuthenticAMD";

    [SuppressMessage("Style", "IDE0042:Deconstruct variable declaration", Justification = "Complete value tuple needed for string marshalling")]
    private static string? GetHypervisorId()
    {
        if (!X86Base.IsSupported)
        {
            return null;
        }

        var values = X86Base.CpuId(0x40000000, 0);

        if (values.Eax < 0x40000000)
        {
            return null;
        }

        var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref values.Ebx, 3));

        span = span[..span.IndexOfTerminator()];

        foreach (var b in span)
        {
            if (b < 0x20)
            {
                return null;
            }
        }

        return Encoding.ASCII.GetString(span);
    }

    [SuppressMessage("Style", "IDE0042:Deconstruct variable declaration", Justification = "Complete value tuple needed for string marshalling")]
    private static string? GetCpuId()
    {
        if (!X86Base.IsSupported)
        {
            return null;
        }

        var cpuid = X86Base.CpuId(0x00000000, 0);

        var values = (cpuid.Ebx, cpuid.Edx, cpuid.Ecx);

        var span = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref values.Ebx, 3));

        span = span[..span.IndexOfTerminator()];

        foreach (var b in span)
        {
            if (b < 0x20)
            {
                return null;
            }
        }

        return Encoding.ASCII.GetString(span);
    }
#endif

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
