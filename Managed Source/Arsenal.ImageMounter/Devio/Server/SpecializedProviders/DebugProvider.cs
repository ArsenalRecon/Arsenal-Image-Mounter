//  DebugProvider.vb
//  
//  Copyright (c) 2012-2023, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.Devio.Server.GenericProviders;
using Arsenal.ImageMounter.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.SpecializedProviders;

/// <summary>
/// A class to support test cases to verify that correct data is received through providers
/// compared to raw image files.
/// </summary>
public class DebugProvider : DevioProviderUnmanagedBase
{

    public IDevioProvider BaseProvider { get; }

    public Stream DebugCompareStream { get; }

    public DebugProvider(IDevioProvider BaseProvider, Stream DebugCompareStream)
    {
        if (BaseProvider is null)
        {
            throw new ArgumentNullException(nameof(BaseProvider));
        }

        if (DebugCompareStream is null)
        {
            throw new ArgumentNullException(nameof(DebugCompareStream));
        }

        if (!DebugCompareStream.CanSeek || !DebugCompareStream.CanRead)
        {
            throw new ArgumentException("Debug compare stream must support seek and read operations.", nameof(DebugCompareStream));
        }

        this.BaseProvider = BaseProvider;
        this.DebugCompareStream = DebugCompareStream;

    }

    public override bool CanWrite => BaseProvider.CanWrite;

    public override long Length => BaseProvider.Length;

    public override uint SectorSize => BaseProvider.SectorSize;

    private byte[]? read_buf2;

    public override int Read(IntPtr buf1, int bufferoffset, int count, long fileoffset)
    {

        if (read_buf2 is null || read_buf2.Length < count)
        {
            Array.Resize(ref read_buf2, count);
        }

        DebugCompareStream.Position = fileoffset;
        var compareTask = DebugCompareStream.ReadAsync(read_buf2, 0, count);

        var rc1 = BaseProvider.Read(buf1, bufferoffset, count, fileoffset);
        var rc2 = compareTask.Result;

        if (rc1 != rc2)
        {
            Trace.WriteLine($"Read request at position 0x{fileoffset:X}, 0x{count:X)} bytes, returned 0x{rc1:X)} bytes from image provider and 0x{rc2:X} bytes from debug compare stream.");
        }

        if (!BufferExtensions.BinaryEqual((buf1 + bufferoffset).AsSpan(rc1), read_buf2.AsSpan(0, rc2)))
        {
            Trace.WriteLine($"Read request at position 0x{fileoffset:X}, 0x{count:X} bytes, returned different data from image provider than from debug compare stream.");
        }

        return rc1;
    }

    public override int Write(IntPtr buffer, int bufferoffset, int count, long fileoffset)
        => BaseProvider.Write(buffer, bufferoffset, count, fileoffset);

    public override bool SupportsShared => BaseProvider.SupportsShared;

    public override void SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys)
        => BaseProvider.SharedKeys(Request, out Response, out Keys);

    protected override void OnDisposed(EventArgs e)
    {
        BaseProvider.Dispose();
        DebugCompareStream.Close();

        base.OnDisposed(e);
    }
}