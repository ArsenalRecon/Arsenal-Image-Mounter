// 
//  DevioTcpStream.vb
//  Client side component for use with devio proxy services from other clients
//  than actual Arsenal Image Mounter driver. This could be useful for example
//  for directly examining virtual disk contents directly in an application,
//  even if that disk contents is accessed through a proxy.
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
using Arsenal.ImageMounter.IO.Native;
using Arsenal.ImageMounter.IO.Streams;
using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.Devio.Client;

/// <summary>
/// Derives DevioStream and implements client side of Devio shared memory communication
/// proxy.
/// </summary>
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public partial class DevioTcpStream : DevioStream
{
    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly MemoryStream outBuffer;

    /// <summary>
    /// Creates a new instance by opening an existing Devio shared memory object and starts
    /// communication with a Devio service using this shared memory object.
    /// </summary>
    /// <param name="hostname"></param>
    /// <param name="port"></param>
    /// <param name="read_only">Specifies if communication should be read-only.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Returns new instance of DevioTcpStream.</returns>
    public static async Task<DevioTcpStream> ConnectAsync(string hostname, int port, bool read_only, CancellationToken cancellationToken)
    {
        var client = new TcpClient();

#if NET5_0_OR_GREATER
        await client.ConnectAsync(hostname, port, cancellationToken).ConfigureAwait(false);
#else
        await client.ConnectAsync(hostname, port).ConfigureAwait(false);
#endif

        return new(client, read_only);
    }

    /// <summary>
    /// Creates a new instance by opening an existing Devio shared memory object and starts
    /// communication with a Devio service using this shared memory object.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="read_only">Specifies if communication should be read-only.</param>
    public DevioTcpStream(TcpClient client, bool read_only)
        : base(client.Client.RemoteEndPoint?.ToString(), read_only)
    {
        this.client = client;
        try
        {
            stream = client.GetStream();
            outBuffer = new();

            stream.Write(IMDPROXY_REQ.IMDPROXY_REQ_INFO);

            var response = stream.Read<IMDPROXY_INFO_RESP>();
            Size = (long)response.file_size;
            Alignment = (long)response.req_alignment;
            Flags |= response.flags;
        }
        catch (Exception ex)
        {
            Dispose();
            throw new Exception("Error initializing stream based shared memory proxy", ex);
        }
    }

    public override void Close()
    {
        if (stream is not null)
        {
            try
            {
                stream.Write(IMDPROXY_REQ.IMDPROXY_REQ_CLOSE);
            }
            catch
            {
            }
        }

        base.Close();
        foreach (var obj in new IDisposable?[] { stream, client })
        {
            try
            {
                obj?.Dispose();
            }
            catch
            {
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var request = new IMDPROXY_READ_REQ
        {
            request_code = IMDPROXY_REQ.IMDPROXY_REQ_READ,
            offset = (ulong)Position,
            length = (ulong)count
        };

        stream.Write(request);

        var response = stream.Read<IMDPROXY_READ_RESP>();

        if (response.errorno != 0)
        {
            throw new EndOfStreamException($"Read error: {response.errorno}");
        }

        var length = (int)response.length;

        stream.ReadExactly(buffer, offset, length);

        Position += length;
        return length;
    }

    public override int Read(Span<byte> buffer)
    {
        var request = new IMDPROXY_READ_REQ
        {
            request_code = IMDPROXY_REQ.IMDPROXY_REQ_READ,
            offset = (ulong)Position,
            length = (ulong)buffer.Length
        };

        stream.Write(request);

        var response = stream.Read<IMDPROXY_READ_RESP>();

        if (response.errorno != 0)
        {
            throw new EndOfStreamException($"Read error: {response.errorno}");
        }

        var length = (int)response.length;

        stream.ReadExactly(buffer.Slice(0, length));

        base.Position += length;
        return length;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException("Read-only connection");
        }

        var request = new IMDPROXY_WRITE_REQ
        {
            request_code = IMDPROXY_REQ.IMDPROXY_REQ_WRITE,
            offset = (ulong)Position,
            length = (ulong)count
        };

        outBuffer.SetLength(0);
        outBuffer.Write(request);
        outBuffer.Write(buffer, offset, count);
        outBuffer.WriteTo(stream);

        var response = stream.Read<IMDPROXY_WRITE_RESP>();
        if (response.errorno != 0)
        {
            throw new EndOfStreamException($"Write error: {response.errorno}");
        }

        var length = (int)response.length;
        Position += length;
        if (length != count)
        {
            throw new EndOfStreamException($"Write length mismatch. Wrote {length} of {count} bytes.");
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!CanWrite)
        {
            throw new InvalidOperationException("Read-only connection");
        }

        var request = new IMDPROXY_WRITE_REQ
        {
            request_code = IMDPROXY_REQ.IMDPROXY_REQ_WRITE,
            offset = (ulong)Position,
            length = (ulong)buffer.Length
        };

        outBuffer.SetLength(0);
        outBuffer.Write(request);
        outBuffer.Write(buffer);
        outBuffer.WriteTo(stream);

        var response = stream.Read<IMDPROXY_WRITE_RESP>();
        if (response.errorno != 0)
        {
            throw new EndOfStreamException($"Write error: {response.errorno}");
        }

        var length = (int)response.length;
        Position += length;
        if (length != buffer.Length)
        {
            throw new EndOfStreamException($"Write length mismatch. Wrote {length} of {buffer.Length} bytes.");
        }
    }
}
