//  
//  Copyright (c) 2012-2026, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using DiscUtils;
using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.Extensions;

public static class ExtensionMethods
{
    public static T Read<T>(this Stream stream) where T : unmanaged
    {
        Span<T> buffer = stackalloc T[1];
        var bytes = MemoryMarshal.AsBytes(buffer);
        stream.ReadExactly(bytes);
        return buffer[0];
    }

#if NET8_0_OR_GREATER
    public static void Write<T>(this Stream stream, in T data) where T : unmanaged
    {
        ReadOnlySpan<T> buffer = MemoryMarshal.CreateReadOnlySpan(in data, 1);

        var bytes = MemoryMarshal.AsBytes(buffer);

        stream.Write(bytes);
    }
#else
    public static unsafe void Write<T>(this Stream stream, in T data) where T : unmanaged
    {
        fixed (void* buffer = &data)
        {
            var bytes = new ReadOnlySpan<byte>((byte*)buffer, sizeof(T));

            stream.Write(bytes);
        }
    }
#endif

    /// <summary>
    /// Queues dispose on a worker thread to avoid blocking calling thread.
    /// </summary>
    /// <param name="instance">Instance to dispose</param>
    public static void QueueDispose(this IDisposable instance)
        => ThreadPool.QueueUserWorkItem(_ =>
        {
            if (instance is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.QueueDispose();
                return;
            }

            try
            {
                instance.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception in {instance.GetType().FullName}.QueueDispose: {ex}");
            }
        });

    /// <summary>
    /// Queues dispose on a worker thread to avoid blocking calling thread.
    /// </summary>
    /// <param name="instance">Instance to dispose</param>
    public static void QueueDispose(this IAsyncDisposable instance)
        => ThreadPool.QueueUserWorkItem(async _ =>
        {
            try
            {
                await instance.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception in {instance.GetType().FullName}.QueueDispose: {ex}");
            }
        });

    /// <summary>
    /// Return a value indicating whether present sector 0 data indicates a valid MBR
    /// with a partition table and not blank or fake boot code.
    /// </summary>
    public static bool HasValidBootCode(this VirtualDisk disk)
    {
        Span<byte> bootsect = stackalloc byte[512];

        var stream = disk.Content;

        stream.Position = 0;

        return stream.Read(bootsect) >= 512
            && bootsect[0] != 0 &&
            !bootsect.Slice(0, NativeConstants.DefaultBootCode.Length)
                .SequenceEqual(NativeConstants.DefaultBootCode.Span)
            && MemoryMarshal.Read<ushort>(bootsect.Slice(0x1FE)) == 0xAA55
            && (bootsect[0x1BE] & 0x7F) == 0
            && (bootsect[0x1CE] & 0x7F) == 0
            && (bootsect[0x1DE] & 0x7F) == 0
            && (bootsect[0x1EE] & 0x7F) == 0;
    }
}
