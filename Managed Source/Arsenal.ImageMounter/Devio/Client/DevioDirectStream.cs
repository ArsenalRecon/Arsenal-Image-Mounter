//  DevioDirectStream.vb
//  Client side component for use with devio proxy services provider objects created
//  directly within the same process. This could be useful for example for directly
//  examining virtual disk contents supplied by a proxy provider object directly in an
//  application.
//  
//  Copyright (c) 2012-2016, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;



namespace Arsenal.ImageMounter.Devio.Client;

/// <summary>
/// Base class for classes that implement Stream for client side of Devio protocol.
/// </summary>
public partial class DevioDirectStream : DevioStream
{
    public IDevioProvider Provider { get; }
    public bool OwnsProvider { get; }

    /// <summary>
    /// Initiates a new instance with supplied provider object.
    /// </summary>
    public DevioDirectStream(IDevioProvider provider, bool ownsProvider)
        : base((provider ?? throw new ArgumentNullException(nameof(provider))).ToString(), !provider.CanWrite)
    {
        Provider = provider;
        OwnsProvider = ownsProvider;
        Size = provider.Length;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesread = Provider.Read(buffer, offset, count, Position);

        if (bytesread > 0)
        {
            Position += bytesread;
        }

        return bytesread;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesread = await Provider.ReadAsync(buffer.AsMemory(offset, count), Position, cancellationToken).ConfigureAwait(false);

        if (bytesread > 0)
        {
            Position += bytesread;
        }

        return bytesread;
    }

    public override int Read(Span<byte> buffer)
    {
        var bytesread = Provider.Read(buffer, Position);

        if (bytesread > 0)
        {
            Position += bytesread;
        }

        return bytesread;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var bytesread = await Provider.ReadAsync(buffer, Position, cancellationToken).ConfigureAwait(false);

        if (bytesread > 0)
        {
            Position += bytesread;
        }

        return bytesread;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var byteswritten = Provider.Write(buffer, offset, count, Position);

        if (byteswritten > 0)
        {
            Position += byteswritten;
        }

        if (byteswritten != count)
        {
            if (byteswritten > 0)
            {
                throw new IOException("Not all data were written");
            }
            else
            {
                throw new IOException("Write error");
            }
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var byteswritten = await Provider.WriteAsync(buffer.AsMemory(offset, count), Position, cancellationToken).ConfigureAwait(false);

        if (byteswritten > 0)
        {
            Position += byteswritten;
        }

        if (byteswritten != count)
        {
            if (byteswritten > 0)
            {
                throw new IOException("Not all data were written");
            }
            else
            {
                throw new IOException("Write error");
            }
        }
    }

    public override unsafe void Write(ReadOnlySpan<byte> buffer)
    {
        var byteswritten = Provider.Write(buffer, Position);

        if (byteswritten > 0)
        {
            Position += byteswritten;
        }

        if (byteswritten != buffer.Length)
        {
            if (byteswritten > 0)
            {
                throw new IOException("Not all data were written");
            }
            else
            {
                throw new IOException("Write error");
            }
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var byteswritten = await Provider.WriteAsync(buffer, Position, cancellationToken).ConfigureAwait(false);

        if (byteswritten > 0)
        {
            Position += byteswritten;
        }

        if (byteswritten != buffer.Length)
        {
            if (byteswritten > 0)
            {
                throw new IOException("Not all data were written");
            }
            else
            {
                throw new IOException("Write error");
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (OwnsProvider)
        {
            Provider?.Dispose();
        }

        base.Dispose(disposing);
    }
}
