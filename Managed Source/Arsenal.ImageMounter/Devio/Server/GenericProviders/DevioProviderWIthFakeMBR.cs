using System;
using Buffer = System.Buffer;

// '''' DevioProviderUnmanagedBase.vb
// '''' 
// '''' Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <https://www.ArsenalRecon.com>
// '''' This source code and API are available under the terms of the Affero General Public
// '''' License v3.
// ''''
// '''' Please see LICENSE.txt for full license terms, including the availability of
// '''' proprietary exceptions.
// '''' Questions, comments, or requests for clarification: https://ArsenalRecon.com/contact/
// ''''

using System.Buffers;
using System.Runtime.InteropServices;
using Arsenal.ImageMounter.Extensions;
using DiscUtils;
using DiscUtils.Partitions;
using Arsenal.ImageMounter.IO.Native;
using System.Linq;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.Devio.Server.GenericProviders;

public class DevioProviderWithFakeMBR : IDevioProvider
{

    /// <summary>
    /// Event when object is about to be disposed
    /// </summary>
    public event EventHandler? Disposing;

    /// <summary>
    /// Event when object has been disposed
    /// </summary>
    public event EventHandler? Disposed;

    public const int PrefixLength = 64 << 10;

    public IDevioProvider BaseProvider { get; private set; }

    internal byte[] PrefixBuffer { get; private set; } = new byte[65536];

    internal byte[] SuffixBuffer { get; private set; }

    public static long GetVBRPartitionLength(IDevioProvider baseProvider)
    {

        baseProvider.NullCheck(nameof(baseProvider));

        var bytesPerSector = (int)baseProvider.SectorSize;

        var vbr = bytesPerSector <= 512
            ? stackalloc byte[bytesPerSector]
            : new byte[bytesPerSector];

        if (baseProvider.Read(vbr, 0L) < bytesPerSector)
        {
            return 0L;
        }

        var vbr_sector_size = MemoryMarshal.Read<short>(vbr.Slice(0xB));

        if (vbr_sector_size <= 0)
        {
            return 0L;
        }

        long total_sectors;

        total_sectors = MemoryMarshal.Read<ushort>(vbr.Slice(0x13));

        if (total_sectors == 0L)
        {

            total_sectors = MemoryMarshal.Read<uint>(vbr.Slice(0x20));

        }

        if (total_sectors == 0L)
        {

            total_sectors = MemoryMarshal.Read<long>(vbr.Slice(0x28));

        }

        return total_sectors < 0L ? 0L : total_sectors * vbr_sector_size;
    }

    public DevioProviderWithFakeMBR(IDevioProvider BaseProvider)
        : this(BaseProvider, GetVBRPartitionLength(BaseProvider))
    {

    }

    public DevioProviderWithFakeMBR(IDevioProvider BaseProvider, long PartitionLength)
    {

        this.BaseProvider = BaseProvider.NullCheck(nameof(BaseProvider));

        PartitionLength = Math.Max(BaseProvider.Length, PartitionLength);

        SuffixBuffer = new byte[(int)(PartitionLength - BaseProvider.Length - 1L) + 1];

        var virtual_length = PrefixLength + PartitionLength;

        var sectorSize = BaseProvider.SectorSize;

        var builder = new BiosPartitionedDiskBuilder(virtual_length, Geometry.FromCapacity(virtual_length, (int)sectorSize));

        var prefix_sector_length = PrefixLength / (long)sectorSize;

        var partition_sector_length = PartitionLength / sectorSize;

        builder.PartitionTable.CreatePrimaryBySector(prefix_sector_length, prefix_sector_length + partition_sector_length - 1L, BiosPartitionTypes.Ntfs, markActive: true);

        var stream = builder.Build();

        NativeConstants.DefaultBootCode.CopyTo(PrefixBuffer);

        var signature = NativeCalls.GenerateDiskSignature();

        MemoryMarshal.Write(PrefixBuffer.AsSpan(DiskSignatureOffset), ref signature);

        stream.Position = PartitionTableOffset;
        stream.Read(PrefixBuffer, PartitionTableOffset, 16);

        PrefixBuffer[510] = 0x55;
        PrefixBuffer[511] = 0xAA;

    }

    public long Length => PrefixLength + BaseProvider.Length + SuffixBuffer.Length;

    public uint SectorSize => BaseProvider.SectorSize;

    public bool CanWrite => BaseProvider.CanWrite;

    bool IDevioProvider.SupportsShared => false;

    void IDevioProvider.SharedKeys(IMDPROXY_SHARED_REQ Request, out IMDPROXY_SHARED_RESP Response, out ulong[] Keys)
        => throw new NotImplementedException();

    public int Read(IntPtr data, int bufferoffset, int count, long fileoffset)
    {

        var prefix_count = 0;

        if (count > 0 && fileoffset < PrefixLength)
        {

            prefix_count = Math.Min((int)(PrefixLength - fileoffset), count);

            Marshal.Copy(PrefixBuffer, (int)fileoffset, data + bufferoffset, prefix_count);

            fileoffset += prefix_count;
            bufferoffset += prefix_count;
            count -= prefix_count;

        }

        var base_count = 0;

        if (count > 0 && fileoffset < PrefixLength + BaseProvider.Length)
        {

            base_count = (int)Math.Min(PrefixLength + BaseProvider.Length - fileoffset, count);

            base_count = BaseProvider.Read(data, bufferoffset, base_count, fileoffset - PrefixLength);

            if (base_count < 0)
            {

                return base_count;

            }

            fileoffset += base_count;
            bufferoffset += base_count;
            count -= base_count;

        }

        var suffix_count = 0;

        if (count > 0 && fileoffset < Length)
        {

            suffix_count = (int)Math.Min(PrefixLength + BaseProvider.Length + SuffixBuffer.Length - fileoffset, count);

            Marshal.Copy(SuffixBuffer, (int)(fileoffset - BaseProvider.Length - PrefixLength), data + bufferoffset, suffix_count);

        }

        return prefix_count + base_count + suffix_count;

    }

    public int Read(byte[] data, int bufferoffset, int count, long fileoffset)
    {

        var prefix_count = 0;

        if (count > 0 && fileoffset < PrefixLength)
        {

            prefix_count = Math.Min((int)(PrefixLength - fileoffset), count);

            Buffer.BlockCopy(PrefixBuffer, (int)fileoffset, data, bufferoffset, prefix_count);

            fileoffset += prefix_count;
            bufferoffset += prefix_count;
            count -= prefix_count;

        }

        var base_count = 0;

        if (count > 0 && fileoffset < PrefixLength + BaseProvider.Length)
        {

            base_count = (int)Math.Min(PrefixLength + BaseProvider.Length - fileoffset, count);

            base_count = BaseProvider.Read(data, bufferoffset, base_count, fileoffset - PrefixLength);

            if (base_count < 0)
            {

                return base_count;

            }

            fileoffset += base_count;
            bufferoffset += base_count;
            count -= base_count;

        }

        var suffix_count = 0;

        if (count > 0 && fileoffset < Length)
        {

            suffix_count = (int)Math.Min(PrefixLength + BaseProvider.Length + SuffixBuffer.Length - fileoffset, count);

            Buffer.BlockCopy(SuffixBuffer, (int)(fileoffset - BaseProvider.Length - PrefixLength), data, bufferoffset, suffix_count);

        }

        return prefix_count + base_count + suffix_count;

    }

    public int Read(Span<byte> data, long fileoffset)
    {

        var bufferoffset = 0;
        var prefix_count = 0;
        var count = data.Length;

        if (count > 0 && fileoffset < PrefixLength)
        {

            prefix_count = Math.Min((int)(PrefixLength - fileoffset), count);

            PrefixBuffer.AsSpan((int)fileoffset, prefix_count).CopyTo(data.Slice(bufferoffset));

            fileoffset += prefix_count;
            bufferoffset += prefix_count;
            count -= prefix_count;

        }

        var base_count = 0;

        if (count > 0 && fileoffset < PrefixLength + BaseProvider.Length)
        {

            base_count = (int)Math.Min(PrefixLength + BaseProvider.Length - fileoffset, count);

            base_count = BaseProvider.Read(data.Slice(bufferoffset, base_count), fileoffset - PrefixLength);

            if (base_count < 0)
            {

                return base_count;

            }

            fileoffset += base_count;
            bufferoffset += base_count;
            count -= base_count;

        }

        var suffix_count = 0;

        if (count > 0 && fileoffset < Length)
        {

            suffix_count = (int)Math.Min(PrefixLength + BaseProvider.Length + SuffixBuffer.Length - fileoffset, count);

            SuffixBuffer.AsSpan((int)(fileoffset - BaseProvider.Length - PrefixLength), suffix_count).CopyTo(data.Slice(bufferoffset));

        }

        return prefix_count + base_count + suffix_count;

    }

    public int Write(IntPtr data, int bufferoffset, int count, long fileoffset)
    {

        var prefix_count = 0;

        if (count > 0 && fileoffset < PrefixLength)
        {

            prefix_count = Math.Min((int)(PrefixLength - fileoffset), count);

            Marshal.Copy(data + bufferoffset, PrefixBuffer, (int)fileoffset, prefix_count);

            fileoffset += prefix_count;
            bufferoffset += prefix_count;
            count -= prefix_count;

        }

        var base_count = 0;

        if (count > 0 && fileoffset < PrefixLength + BaseProvider.Length)
        {

            base_count = (int)Math.Min(PrefixLength + BaseProvider.Length - fileoffset, count);

            base_count = BaseProvider.Write(data, bufferoffset, base_count, fileoffset - PrefixLength);

            if (base_count < 0)
            {

                return base_count;

            }

            fileoffset += base_count;
            bufferoffset += base_count;
            count -= base_count;

        }

        var suffix_count = 0;

        if (count > 0 && fileoffset < Length)
        {

            suffix_count = (int)Math.Min(PrefixLength + BaseProvider.Length + SuffixBuffer.Length - fileoffset, count);

            Marshal.Copy(data + bufferoffset, SuffixBuffer, (int)(fileoffset - BaseProvider.Length - PrefixLength), suffix_count);

        }

        return prefix_count + base_count + suffix_count;

    }

    public int Write(byte[] data, int bufferoffset, int count, long fileoffset)
    {

        var prefix_count = 0;

        if (count > 0 && fileoffset < PrefixLength)
        {

            prefix_count = Math.Min((int)(PrefixLength - fileoffset), count);

            Buffer.BlockCopy(data, bufferoffset, PrefixBuffer, (int)fileoffset, prefix_count);

            fileoffset += prefix_count;
            bufferoffset += prefix_count;
            count -= prefix_count;

        }

        var base_count = 0;

        if (count > 0 && fileoffset < PrefixLength + BaseProvider.Length)
        {

            base_count = (int)Math.Min(PrefixLength + BaseProvider.Length - fileoffset, count);

            base_count = BaseProvider.Write(data, bufferoffset, base_count, fileoffset - PrefixLength);

            if (base_count < 0)
            {

                return base_count;

            }

            fileoffset += base_count;
            bufferoffset += base_count;
            count -= base_count;

        }

        var suffix_count = 0;

        if (count > 0 && fileoffset < Length)
        {

            suffix_count = (int)Math.Min(PrefixLength + BaseProvider.Length + SuffixBuffer.Length - fileoffset, count);

            Buffer.BlockCopy(data, bufferoffset, SuffixBuffer, (int)(fileoffset - BaseProvider.Length - PrefixLength), suffix_count);

        }

        return prefix_count + base_count + suffix_count;

    }

    public int Write(ReadOnlySpan<byte> data, long fileoffset)
    {

        var bufferoffset = 0;
        var prefix_count = 0;
        var count = data.Length;

        if (count > 0 && fileoffset < PrefixLength)
        {

            prefix_count = Math.Min((int)(PrefixLength - fileoffset), count);

            data.Slice(bufferoffset, prefix_count).CopyTo(PrefixBuffer.AsSpan((int)fileoffset));

            fileoffset += prefix_count;
            bufferoffset += prefix_count;
            count -= prefix_count;

        }

        var base_count = 0;

        if (count > 0 && fileoffset < PrefixLength + BaseProvider.Length)
        {

            base_count = (int)Math.Min(PrefixLength + BaseProvider.Length - fileoffset, count);

            base_count = BaseProvider.Write(data.Slice(bufferoffset, base_count), fileoffset - PrefixLength);

            if (base_count < 0)
            {

                return base_count;

            }

            fileoffset += base_count;
            bufferoffset += base_count;
            count -= base_count;

        }

        var suffix_count = 0;

        if (count > 0 && fileoffset < Length)
        {

            suffix_count = (int)Math.Min(PrefixLength + BaseProvider.Length + SuffixBuffer.Length - fileoffset, count);

            data.Slice(bufferoffset, suffix_count).CopyTo(SuffixBuffer.AsSpan((int)(fileoffset - BaseProvider.Length - PrefixLength)));

        }

        return prefix_count + base_count + suffix_count;

    }

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
    ~DevioProviderWithFakeMBR()
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

    private const int DiskSignatureOffset = 0x1B8;

    private const int PartitionTableOffset = 512 - 2 - 4 * 16;

}