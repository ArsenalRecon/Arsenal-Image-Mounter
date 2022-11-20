
// '''' DevioProviderFromStream.vb
// '''' Proxy provider that implements devio proxy service with a .NET Stream derived
// '''' object as storage backend.
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// ''''

using DiscUtils.Streams.Compatibility;
using System;
using System.IO;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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
    public Stream BaseStream { get; private set; }

    /// <summary>
    /// Indicates whether base stream will be automatically closed when this
    /// instance is disposed.
    /// </summary>
    public bool OwnsBaseStream { get; private set; }

    /// <summary>
    /// Creates an object implementing IDevioProvider interface with I/O redirected
    /// to an object of a class derived from System.IO.Stream.
    /// </summary>
    /// <param name="Stream">Object of a class derived from System.IO.Stream.</param>
    /// <param name="ownsStream">Indicates whether Stream object will be automatically closed when this
    /// instance is disposed.</param>
    public DevioProviderFromStream(Stream Stream, bool ownsStream)
    {
        BaseStream = Stream;
        OwnsBaseStream = ownsStream;
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
    /// Returns a fixed value of 512.
    /// </summary>
    /// <value>512</value>
    /// <returns>512</returns>
    public uint CustomSectorSize { get; set; } = 512U;

    /// <summary>
    /// Returns a fixed value of 512.
    /// </summary>
    /// <value>512</value>
    /// <returns>512</returns>
    public uint SectorSize => CustomSectorSize;

    bool IDevioProvider.SupportsShared => false;

    public event EventHandler? Disposing;
    public event EventHandler? Disposed;

    public int Read(IntPtr buffer, int bufferoffset, int count, long fileoffset)
    {

        BaseStream.Position = fileoffset;

        if (BaseStream.Position <= BaseStream.Length
            && count > BaseStream.Length - BaseStream.Position)
        {

            count = (int)(BaseStream.Length - BaseStream.Position);

        }

        var mem = Extensions.BufferExtensions.AsSpan(buffer + bufferoffset, count);
        return BaseStream.Read(mem);

    }

    public int Write(IntPtr buffer, int bufferoffset, int count, long fileoffset)
    {

        BaseStream.Position = fileoffset;

        var mem = Extensions.BufferExtensions.AsReadOnlySpan(buffer + bufferoffset, count);
        BaseStream.Write(mem);
        return count;

    }

    public int Read(Span<byte> buffer, long fileoffset)
    {

        BaseStream.Position = fileoffset;

        if (BaseStream.Position <= BaseStream.Length
            && buffer.Length > BaseStream.Length - BaseStream.Position)
        {

            buffer = buffer.Slice(0, (int)(BaseStream.Length - BaseStream.Position));

        }

        return BaseStream.Read(buffer);

    }

    public int Write(ReadOnlySpan<byte> buffer, long fileoffset)
    {

        BaseStream.Position = fileoffset;

        BaseStream.Write(buffer);
        return buffer.Length;

    }

    public int Read(byte[] buffer, int bufferoffset, int count, long fileoffset)
    {

        BaseStream.Position = fileoffset;

        if (BaseStream.Position <= BaseStream.Length
            && count > BaseStream.Length - BaseStream.Position)
        {

            count = (int)(BaseStream.Length - BaseStream.Position);

        }

        return BaseStream.Read(buffer, bufferoffset, count);

    }

    public int Write(byte[] buffer, int bufferoffset, int count, long fileoffset)
    {

        BaseStream.Position = fileoffset;
        BaseStream.Write(buffer, bufferoffset, count);
        return count;

    }

    protected virtual void Dispose(bool disposing)
    {

        OnDisposing(EventArgs.Empty);

        if (!disposedValue &&
            disposing &&
            OwnsBaseStream &&
            BaseStream is not null)
        {
            BaseStream.Dispose();
        }

        BaseStream = null!;
        disposedValue = true;

        OnDisposed(EventArgs.Empty);
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