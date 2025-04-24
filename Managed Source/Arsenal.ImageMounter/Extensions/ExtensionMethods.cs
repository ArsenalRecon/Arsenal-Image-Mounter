//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

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
}
