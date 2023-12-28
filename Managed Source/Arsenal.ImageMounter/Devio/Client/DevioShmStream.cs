// 
//  DevioShmStream.vb
//  Client side component for use with devio proxy services from other clients
//  than actual Arsenal Image Mounter driver. This could be useful for example
//  for directly examining virtual disk contents directly in an application,
//  even if that disk contents is accessed through a proxy.
//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using LTRData.Extensions.Buffers;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using static Arsenal.ImageMounter.Devio.IMDPROXY_CONSTANTS;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.Devio.Client;

/// <summary>
/// Derives DevioStream and implements client side of Devio shared memory communication
/// proxy.
/// </summary>
[SupportedOSPlatform(NativeConstants.SUPPORTED_WINDOWS_PLATFORM)]
public partial class DevioShmStream : DevioStream
{
    private readonly EventWaitHandle requestEvent;
    private readonly EventWaitHandle responseEvent;
    private readonly Mutex serverMutex;
    private readonly SafeBuffer mapView;
    private readonly WaitHandle[] waitHandles;

    /// <summary>
    /// Creates a new instance by opening an existing Devio shared memory object and starts
    /// communication with a Devio service using this shared memory object.
    /// </summary>
    /// <param name="name">Name of shared memory object to use for communication.</param>
    /// <param name="read_only">Specifies if communication should be read-only.</param>
    /// <returns>Returns new instance of DevioShmStream.</returns>
    public static DevioShmStream Open(string name, bool read_only) => new(name, read_only);

    /// <summary>
    /// Creates a new instance by opening an existing Devio shared memory object and starts
    /// communication with a Devio service using this shared memory object.
    /// </summary>
    /// <param name="name">Name of shared memory object to use for communication.</param>
    /// <param name="read_only">Specifies if communication should be read-only.</param>
    public DevioShmStream(string name, bool read_only)
        : base(name, read_only)
    {
        try
        {
            using (var Mapping = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.ReadWrite))
            {
                mapView = Mapping.CreateViewAccessor().SafeMemoryMappedViewHandle;
            }

            requestEvent = new EventWaitHandle(initialState: false, mode: EventResetMode.AutoReset, name: $@"Global\{ObjectName}_Request");
            responseEvent = new EventWaitHandle(initialState: false, mode: EventResetMode.AutoReset, name: $@"Global\{ObjectName}_Response");
            serverMutex = new Mutex(initiallyOwned: false, name: $@"Global\{ObjectName}_Server");
            waitHandles = [responseEvent, serverMutex];
            mapView.Write(0x0, IMDPROXY_REQ.IMDPROXY_REQ_INFO);
            requestEvent.Set();
            if (WaitHandle.WaitAny(waitHandles) != 0)
            {
                throw new EndOfStreamException("Server exit.");
            }

            var Response = mapView.Read<IMDPROXY_INFO_RESP>(0x0UL);
            Size = (long)Response.file_size;
            Alignment = (long)Response.req_alignment;
            Flags |= Response.flags;
        }
        catch (Exception ex)
        {
            Dispose();
            throw new Exception("Error initializing stream based shared memory proxy", ex);
        }
    }

    public override void Close()
    {
        if (mapView is not null && requestEvent is not null)
        {
            try
            {
                mapView.Write(0x0, IMDPROXY_REQ.IMDPROXY_REQ_CLOSE);
                requestEvent.Set();
            }
            catch
            {
            }
        }

        base.Close();
        foreach (var obj in new IDisposable?[] { serverMutex, mapView, requestEvent, responseEvent })
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
        var Request = default(IMDPROXY_READ_REQ);
        Request.request_code = IMDPROXY_REQ.IMDPROXY_REQ_READ;
        Request.offset = (ulong)Position;
        Request.length = (ulong)count;
        mapView.Write(0x0, Request);
        requestEvent.Set();
        if (WaitHandle.WaitAny(waitHandles) != 0)
        {
            throw new EndOfStreamException("Server exit.");
        }

        var Response = mapView.Read<IMDPROXY_READ_RESP>(0x0UL);
        if (Response.errorno != 0)
        {
            throw new EndOfStreamException($"Read error: {Response.errorno}");
        }

        var Length = (int)Response.length;
        mapView.ReadArray(IMDPROXY_HEADER_SIZE, buffer, offset, Length);
        Position += Length;
        return Length;
    }

    public override int Read(Span<byte> buffer)
    {
        var Request = default(IMDPROXY_READ_REQ);
        Request.request_code = IMDPROXY_REQ.IMDPROXY_REQ_READ;
        Request.offset = (ulong)Position;
        Request.length = (ulong)buffer.Length;
        mapView.Write(0x0, Request);
        requestEvent.Set();
        if (WaitHandle.WaitAny(waitHandles) != 0)
        {
            throw new EndOfStreamException("Server exit.");
        }

        var Response = mapView.Read<IMDPROXY_READ_RESP>(0x0UL);
        if (Response.errorno != 0)
        {
            throw new EndOfStreamException($"Read error: {Response.errorno}");
        }

        var Length = (int)Response.length;

        mapView.ReadSpan(IMDPROXY_HEADER_SIZE, buffer.Slice(0, Length));

        Position += Length;
        return Length;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var Request = default(IMDPROXY_WRITE_REQ);
        Request.request_code = IMDPROXY_REQ.IMDPROXY_REQ_WRITE;
        Request.offset = (ulong)Position;
        Request.length = (ulong)count;
        mapView.Write(0x0, Request);
        mapView.WriteArray(IMDPROXY_HEADER_SIZE, buffer, offset, count);
        requestEvent.Set();
        if (WaitHandle.WaitAny(waitHandles) != 0)
        {
            throw new EndOfStreamException("Server exit.");
        }

        var Response = mapView.Read<IMDPROXY_WRITE_RESP>(0x0UL);
        if (Response.errorno != 0)
        {
            throw new EndOfStreamException($"Write error: {Response.errorno}");
        }

        var Length = (int)Response.length;
        Position += Length;
        if (Length != count)
        {
            throw new EndOfStreamException($"Write length mismatch. Wrote {Length} of {count} bytes.");
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var Request = default(IMDPROXY_WRITE_REQ);
        Request.request_code = IMDPROXY_REQ.IMDPROXY_REQ_WRITE;
        Request.offset = (ulong)Position;
        Request.length = (ulong)buffer.Length;
        mapView.Write(0x0, Request);
        mapView.WriteSpan(IMDPROXY_HEADER_SIZE, buffer);

        requestEvent.Set();
        if (WaitHandle.WaitAny(waitHandles) != 0)
        {
            throw new EndOfStreamException("Server exit.");
        }

        var Response = mapView.Read<IMDPROXY_WRITE_RESP>(0x0UL);
        if (Response.errorno != 0)
        {
            throw new EndOfStreamException($"Write error: {Response.errorno}");
        }

        var Length = (int)Response.length;
        Position += Length;
        if (Length != buffer.Length)
        {
            throw new EndOfStreamException($"Write length mismatch. Wrote {Length} of {buffer.Length} bytes.");
        }
    }
}
