//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Arsenal.ImageMounter.Devio.Server.GenericProviders;

public sealed class DummyProvider(long Length) : IDevioProvider
{
    /// <summary>
    /// Event when object is about to be disposed
    /// </summary>
    public event EventHandler? Disposing;

    /// <summary>
    /// Event when object has been disposed
    /// </summary>
    public event EventHandler? Disposed;

    public long Length { get; } = Length;

    public uint SectorSize => 512U;

    public bool CanWrite => true;

    public bool SupportsParallel => true;

    bool IDevioProvider.SupportsShared => false;

    bool IDevioProvider.ForceSingleThread { get; set; }

    void IDevioProvider.SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys) => throw new NotImplementedException();

    int IDevioProvider.Read(nint buffer, int bufferoffset, int count, long fileoffset) => throw new NotImplementedException();

    int IDevioProvider.Read(byte[] buffer, int bufferoffset, int count, long fileoffset) => throw new NotImplementedException();

    int IDevioProvider.Read(Span<byte> buffer, long fileoffset) => throw new NotImplementedException();

    ValueTask<int> IDevioProvider.ReadAsync(Memory<byte> buffer, long fileoffset, CancellationToken cancellationToken) => throw new NotImplementedException();

    int IDevioProvider.Write(nint buffer, int bufferoffset, int count, long fileoffset) => throw new NotImplementedException();

    int IDevioProvider.Write(byte[] buffer, int bufferoffset, int count, long fileoffset) => throw new NotImplementedException();

    int IDevioProvider.Write(ReadOnlySpan<byte> buffer, long fileoffset) => throw new NotImplementedException();

    ValueTask<int> IDevioProvider.WriteAsync(ReadOnlyMemory<byte> buffer, long fileoffset, CancellationToken cancellationToken) => throw new NotImplementedException();

    public void Dispose()
    {
        Disposing?.Invoke(this, EventArgs.Empty);
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}

