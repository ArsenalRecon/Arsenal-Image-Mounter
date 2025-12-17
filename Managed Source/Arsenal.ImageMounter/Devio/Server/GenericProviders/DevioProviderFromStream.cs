// 
//  DevioProviderFromStream.vb
//  Proxy provider that implements devio proxy service with a .NET Stream derived
//  object as storage backend.
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Extensions;
using Arsenal.ImageMounter.IO.Streams;
using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Formatting;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.Devio.Server.GenericProviders;

/// <summary>
/// Class that implements <see>IDevioProvider</see> interface with a System.IO.Stream
/// object as storage backend.
/// </summary>
public class DevioProviderFromStream : IDevioProvider
{
    /// <summary>
    /// Stream object used by this instance.
    /// </summary>
    public Stream BaseStream { get; }

#if NET6_0_OR_GREATER
    /// <summary>
    /// File handle if target is capable of random access I/O.
    /// </summary>
    private readonly SafeFileHandle? randomAccessFileHandle;

    private readonly int randomAccessAlignment;
#endif

    /// <summary>
    /// Indicates whether base stream will be automatically closed when this
    /// instance is disposed.
    /// </summary>
    public bool OwnsBaseStream { get; }

    /// <summary>
    /// Indicates whether provider supports dispatching multiple simultaneous I/O requests.
    /// Seekable streams do not support parallel I/O, so this implementation always returns
    /// false.
    /// </summary>
    public bool SupportsParallel { get; }

    /// <summary>
    /// Set to true to force single thread operation even if provider supports multithread
    /// </summary>
    public bool ForceSingleThread { get; set; }

    /// <summary>
    /// Set to true to enable lazy writes. In this mode, write requests are queued and
    /// flushed to the underlying stream in the background.
    /// </summary>
    public bool UseLazyWrites
    {
        get;
        set
        {
            if (value && !CanWrite)
            {
                throw new InvalidOperationException("Cannot enable lazy writes on a read-only stream.");
            }

            if (value && SupportsParallel)
            {
                throw new InvalidOperationException("Cannot enable lazy writes on a file or disk stream.");
            }

            field = value;
        }
    }

    /// <summary>
    /// Creates an object implementing IDevioProvider interface with I/O redirected
    /// to an object of a class derived from System.IO.Stream.
    /// </summary>
    /// <param name="stream">Object of a class derived from System.IO.Stream.</param>
    /// <param name="ownsStream">Indicates whether Stream object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioProviderFromStream(Stream stream, bool ownsStream)
    {
        BaseStream = stream;
        OwnsBaseStream = ownsStream;

#if NET6_0_OR_GREATER
        if (stream is FileStream fileStream)
        {
            randomAccessFileHandle = fileStream.SafeFileHandle;
            SupportsParallel = true;
        }
        else if (stream is DiskStream diskStream)
        {
            randomAccessAlignment = diskStream.Alignment - 1;
            randomAccessFileHandle = diskStream.SafeFileHandle;
            SupportsParallel = true;
        }
#endif
    }

    /// <summary>
    /// Returns value of BaseStream.CanWrite.
    /// </summary>
    /// <value>Value of BaseStream.CanWrite.</value>
    /// <returns>Value of BaseStream.CanWrite.</returns>
    public bool CanWrite => BaseStream.CanWrite;

    /// <summary>
    /// Returns value of BaseStream.Length.
    /// </summary>
    /// <value>Value of BaseStream.Length.</value>
    /// <returns>Value of BaseStream.Length.</returns>
    public long Length => BaseStream.Length;

    /// <summary>
    /// Default value is 512.
    /// </summary>
    public uint CustomSectorSize { get; set; } = 512U;

    /// <summary>
    /// Returns value of <see cref="CustomSectorSize"/> property.
    /// </summary>
    public uint SectorSize => CustomSectorSize;

    bool IDevioProvider.SupportsShared => false;

    public event EventHandler? Disposing;
    public event EventHandler? Disposed;

    public unsafe int Read(nint buffer, int bufferoffset, int count, long fileOffset)
        => Read(new Span<byte>((byte*)buffer + bufferoffset, count), fileOffset);
    
    public int Read(Span<byte> buffer, long fileOffset)
    {
        if (fileOffset <= BaseStream.Length
            && buffer.Length > BaseStream.Length - fileOffset)
        {
            buffer = buffer.Slice(0, (int)(BaseStream.Length - fileOffset));
        }

#if NET6_0_OR_GREATER
        if (randomAccessFileHandle is not null
            && (buffer.Length & randomAccessAlignment) == 0
            && (fileOffset & randomAccessAlignment) == 0)
        {
            return RandomAccess.Read(randomAccessFileHandle, buffer, fileOffset);
        }
#endif

        BaseStream.Position = fileOffset;

        return BaseStream.Read(buffer);
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, long fileOffset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (fileOffset <= BaseStream.Length
            && buffer.Length > BaseStream.Length - fileOffset)
        {
            buffer = buffer.Slice(0, (int)(BaseStream.Length - fileOffset));
        }

#if NET6_0_OR_GREATER
        if (randomAccessFileHandle is not null
            && (buffer.Length & randomAccessAlignment) == 0
            && (fileOffset & randomAccessAlignment) == 0)
        {
            return RandomAccess.ReadAsync(randomAccessFileHandle, buffer, fileOffset, cancellationToken);
        }
#endif

        if (!PendingWrites.IsEmpty)
        {
            async ValueTask<int> AwaitWriteThenRead()
            {
                while (!PendingWrites.IsEmpty)
                {
                    WriteFlushEvent.Set();
                    await WriteFlushSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    WriteFlushSemaphore.Release();
                }

                BaseStream.Position = fileOffset;
                
                return await BaseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            
            return AwaitWriteThenRead();
        }

        BaseStream.Position = fileOffset;

        return BaseStream.ReadAsync(buffer, cancellationToken);
    }

    public int Read(byte[] buffer, int bufferoffset, int count, long fileOffset)
    {
        if (fileOffset <= BaseStream.Length
            && count > BaseStream.Length - fileOffset)
        {
            count = (int)(BaseStream.Length - fileOffset);
        }

#if NET6_0_OR_GREATER
        if (randomAccessFileHandle is not null
            && (count & randomAccessAlignment) == 0
            && (fileOffset & randomAccessAlignment) == 0)
        {
            return RandomAccess.Read(randomAccessFileHandle, buffer.AsSpan(bufferoffset, count), fileOffset);
        }
#endif

        while (!PendingWrites.IsEmpty)
        {
            WriteFlushEvent.Set();
            WriteFlushSemaphore.Wait();
            WriteFlushSemaphore.Release();
        }

        BaseStream.Position = fileOffset;

        return BaseStream.Read(buffer, bufferoffset, count);
    }

    public unsafe int Write(nint buffer, int bufferoffset, int count, long fileOffset)
        => Write(new ReadOnlySpan<byte>((byte*)buffer + bufferoffset, count), fileOffset);

    public int Write(ReadOnlySpan<byte> buffer, long fileOffset)
    {
#if NET6_0_OR_GREATER
        if (randomAccessFileHandle is not null
            && (buffer.Length & randomAccessAlignment) == 0
            && (fileOffset & randomAccessAlignment) == 0)
        {
            RandomAccess.Write(randomAccessFileHandle, buffer, fileOffset);

            return buffer.Length;
        }
#endif

        if (UseLazyWrites)
        {
            throw new InvalidOperationException("Lazy writes are not supported for synchronous Write calls.");
        }

        BaseStream.Position = fileOffset;

        BaseStream.Write(buffer);

        return buffer.Length;
    }

    private ConcurrentQueue<LazyWriteItem> PendingWrites => field ??= new();

    private readonly record struct LazyWriteItem(ArraySegment<byte> Buffer, long FileOffset);

    private Task? lazyWriter;

    private SemaphoreSlim WriteFlushSemaphore => field ??= new(initialCount: 1);

    private AutoResetEvent WriteFlushEvent => field ??= new(initialState: false);

    private bool isShuttingDown;

    public async ValueTask<int> WriteAsync(ReadOnlyMemory<byte> buffer, long fileOffset, CancellationToken cancellationToken)
    {
#if NET6_0_OR_GREATER
        if (randomAccessFileHandle is not null
            && (buffer.Length & randomAccessAlignment) == 0
            && (fileOffset & randomAccessAlignment) == 0)
        {
            await RandomAccess.WriteAsync(randomAccessFileHandle, buffer, fileOffset, cancellationToken).ConfigureAwait(false);

            return buffer.Length;
        }
#endif

        if (!UseLazyWrites)
        {
            BaseStream.Position = fileOffset;
            await BaseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer.Length;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

        buffer.CopyTo(array);

        PendingWrites.Enqueue(new(new(array, 0, buffer.Length), fileOffset));

        WriteFlushEvent.Set();

        if (lazyWriter is null || lazyWriter.IsCompleted)
        {
            lazyWriter = Task.Run(async () =>
            {
                while (!isShuttingDown)
                {
                    var time = Environment.TickCount;

                    await WriteFlushEvent.WaitAsync(CancellationToken.None).ConfigureAwait(false);

                    await WriteFlushSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);

                    try
                    {
                        while (PendingWrites.TryDequeue(out var item))
                        {
                            try
                            {
                                if (Environment.TickCount - time > 200)
                                {
                                    Console.Write($"DevioProviderFromStream: Lazy write backlog processing {PendingWrites.Count + 1} items of {SizeFormatting.FormatBytes(PendingWrites.Sum(item => (long)item.Buffer.Count) + item.Buffer.Count)}...  \r");

                                    time = Environment.TickCount;
                                }

                                BaseStream.Position = item.FileOffset;

                                await BaseStream.WriteAsync(item.Buffer, CancellationToken.None).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                // Log and continue with next write
                                var msg = $"DevioProviderFromStream: Lazy write failed, data lost: {ex}";

                                Trace.WriteLine(msg);
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Error.WriteLine(msg);
                                Console.ResetColor();
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(item.Buffer.Array!);
                            }
                        }

                        const string donemsg = "DevioProviderFromStream: Lazy write backlog processing done.";
                        
                        Console.Write(donemsg);
                        Console.WriteLine(new string(' ', Console.WindowWidth - donemsg.Length - 1));

                        time = Environment.TickCount;
                    }
                    finally
                    {
                        WriteFlushSemaphore.Release();
                    }
                }

            }, cancellationToken);
        }

        return buffer.Length;
    }

    public int Write(byte[] buffer, int bufferoffset, int count, long fileOffset)
    {
#if NET6_0_OR_GREATER
        if (randomAccessFileHandle is not null
            && (count & randomAccessAlignment) == 0
            && (fileOffset & randomAccessAlignment) == 0)
        {
            RandomAccess.Write(randomAccessFileHandle, buffer.AsSpan(bufferoffset, count), fileOffset);

            return count;
        }
#endif

        BaseStream.Position = fileOffset;
        BaseStream.Write(buffer, bufferoffset, count);
        
        return count;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int disposedValue; // 0 = false, 1 = true

    [DebuggerHidden]
    private bool ShouldCleanup() => Interlocked.Exchange(ref disposedValue, 1) == 0;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        if (OwnsBaseStream
            && BaseStream is not null)
        {
            var wasNotDisposed = ShouldCleanup();

            if (wasNotDisposed)
            {
                OnDisposing(EventArgs.Empty);
            }

            if (!PendingWrites.IsEmpty)
            {
                isShuttingDown = true;
                WriteFlushEvent.Set();
                lazyWriter?.GetAwaiter().GetResult();
            }

            if (wasNotDisposed)
            {
                BaseStream.Dispose();

                OnDisposed(EventArgs.Empty);
            }
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (OwnsBaseStream
            && BaseStream is not null)
        {
            var wasNotDisposed = ShouldCleanup();

            if (wasNotDisposed)
            {
                OnDisposing(EventArgs.Empty);
            }

            if (!PendingWrites.IsEmpty && lazyWriter is not null)
            {
                isShuttingDown = true;
                WriteFlushEvent.Set();
                await lazyWriter.ConfigureAwait(false);
            }

            if (wasNotDisposed)
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
                await BaseStream.DisposeAsync().ConfigureAwait(false);
#else
                BaseStream.Dispose();
#endif

                OnDisposed(EventArgs.Empty);
            }
        }

        GC.SuppressFinalize(this);
    }

    protected virtual void OnDisposing(EventArgs e) => Disposing?.Invoke(this, e);

    protected virtual void OnDisposed(EventArgs e) => Disposed?.Invoke(this, e);

    void IDevioProvider.SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys)
        => throw new NotImplementedException();

    ~DevioProviderFromStream()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}