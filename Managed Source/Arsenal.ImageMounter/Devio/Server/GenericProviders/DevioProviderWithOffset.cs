//  DevioProviderWithOffset.cs
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
// 

using System;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1512 // Use ArgumentOutOfRangeException throw helper
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.Devio.Server.GenericProviders;

public class DevioProviderWithOffset : IDevioProvider
{
    /// <summary>
    /// Event when object is about to be disposed
    /// </summary>
    public event EventHandler? Disposing;

    /// <summary>
    /// Event when object has been disposed
    /// </summary>
    public event EventHandler? Disposed;

    public long Offset { get; }

    public IDevioProvider BaseProvider { get; }

    public DevioProviderWithOffset(IDevioProvider baseProvider, long offset)
        : this(baseProvider, offset, Math.Max(baseProvider.Length, baseProvider.GetVBRPartitionLength()) - offset)
    {
    }

    public DevioProviderWithOffset(IDevioProvider baseProvider, long offset, long size)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

#if NET7_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(BaseProvider);
        BaseProvider = baseProvider;
#else
        BaseProvider = baseProvider
            ?? throw new ArgumentNullException(nameof(baseProvider));
#endif

        Offset = offset;

        Length = size;
    }

    public long Length { get; }

    public uint SectorSize => BaseProvider.SectorSize;

    public bool CanWrite => BaseProvider.CanWrite;

    public bool SupportsParallel => BaseProvider.SupportsParallel;

    public bool ForceSingleThread { get; set; }

    bool IDevioProvider.SupportsShared => false;

    void IDevioProvider.SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys)
        => throw new NotImplementedException();

    public int Read(nint data, int bufferoffset, int count, long fileoffset)
        => BaseProvider.Read(data, bufferoffset, count, fileoffset + Offset);

    public int Read(byte[] data, int bufferoffset, int count, long fileoffset)
        => BaseProvider.Read(data, bufferoffset, count, fileoffset + Offset);

    public int Read(Span<byte> data, long fileoffset)
        => BaseProvider.Read(data, fileoffset + Offset);

    public ValueTask<int> ReadAsync(Memory<byte> data, long fileoffset, CancellationToken cancellationToken)
        => BaseProvider.ReadAsync(data, fileoffset + Offset, cancellationToken);

    public int Write(nint data, int bufferoffset, int count, long fileoffset)
        => BaseProvider.Write(data, bufferoffset, count, fileoffset + Offset);

    public int Write(byte[] data, int bufferoffset, int count, long fileoffset)
        => BaseProvider.Write(data, bufferoffset, count, fileoffset + Offset);

    public int Write(ReadOnlySpan<byte> data, long fileoffset)
        => BaseProvider.Write(data, fileoffset + Offset);

    public ValueTask<int> WriteAsync(ReadOnlyMemory<byte> data, long fileoffset, CancellationToken cancellationToken)
        => BaseProvider.WriteAsync(data, fileoffset + Offset, cancellationToken);

    public bool IsDisposed { get; private set; } // To detect redundant calls

    // IDisposable
    protected virtual void Dispose(bool disposing)
    {
        OnDisposing(EventArgs.Empty);

        if (!IsDisposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
            }

            // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
            // TODO: set large fields to null.
        }

        IsDisposed = true;

        OnDisposed(EventArgs.Empty);
    }

    // TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
    ~DevioProviderWithOffset()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(false);
    }

    // This code added by Visual Basic to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(true);
        // TODO: uncomment the following line if Finalize() is overridden above.
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Raises Disposing event.
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnDisposing(EventArgs e) => Disposing?.Invoke(this, e);

    /// <summary>
    /// Raises Disposed event.
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnDisposed(EventArgs e) => Disposed?.Invoke(this, e);
}
