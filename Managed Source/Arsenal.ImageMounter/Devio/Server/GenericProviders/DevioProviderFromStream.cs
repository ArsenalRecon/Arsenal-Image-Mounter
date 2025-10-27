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
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
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
    private bool disposedValue;

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

        BaseStream.Position = fileOffset;

        BaseStream.Write(buffer);

        return buffer.Length;
    }

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

        BaseStream.Position = fileOffset;

        await BaseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

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

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue &&
            disposing &&
            OwnsBaseStream &&
            BaseStream is not null)
        {
            OnDisposing(EventArgs.Empty);

            BaseStream.Dispose();

            OnDisposed(EventArgs.Empty);
        }

        disposedValue = true;
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