//  DiskStream.vb
//  Stream implementation for direct access to raw disk data.
//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using Arsenal.ImageMounter.IO.Native;
using DiscUtils.Streams.Compatibility;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator


namespace Arsenal.ImageMounter.IO.Streams;

/// <summary>
/// A AligningStream derived class that represents disk devices. It aligns I/O requests to complete
/// sectors for compatibility with underlying device.
/// </summary>
public class DiskStream : AligningStream
{
    /// <summary>
    /// Initializes an DiskStream object for an open disk device.
    /// </summary>
    /// <param name="safeFileHandle">Open file handle for disk device.</param>
    /// <param name="AccessMode">Access to request for stream.</param>
    protected internal DiskStream(SafeFileHandle safeFileHandle, FileAccess AccessMode)
        : base(new DiskFileStream(safeFileHandle, AccessMode),
               alignment: (NativeStruct.GetDiskGeometry(safeFileHandle)?.BytesPerSector) ?? 512,
               ownsBaseStream: true)
    {
        SafeFileHandle = safeFileHandle;
    }

    /// <summary>
    /// Initializes an DiskStream object for an open disk device.
    /// </summary>
    /// <param name="safeFileHandle">Open file handle for disk device.</param>
    /// <param name="AccessMode">Access to request for stream.</param>
    /// <param name="DiskSize">Size that should be returned by Length property</param>
    protected internal DiskStream(SafeFileHandle safeFileHandle, FileAccess AccessMode, long DiskSize)
        : base(new DiskFileStream(safeFileHandle, AccessMode),
               alignment: (NativeStruct.GetDiskGeometry(safeFileHandle)?.BytesPerSector) ?? 512,
               ownsBaseStream: true)
    {
        ((DiskFileStream)BaseStream).cachedLength = DiskSize;
        SafeFileHandle = safeFileHandle;
    }

    public SafeFileHandle SafeFileHandle { get; }

    private bool size_from_vbr;

    public bool SizeFromVBR
    {
        get => size_from_vbr;
        set
        {
            if (value)
            {
                ((DiskFileStream)BaseStream).cachedLength = GetVBRPartitionLength()
                    ?? throw new NotSupportedException();
            }
            else
            {
                ((DiskFileStream)BaseStream).cachedLength = NativeStruct.GetDiskSize(SafeFileHandle)
                    ?? throw new NotSupportedException();
            }

            size_from_vbr = value;
        }
    }

    /// <summary>
    /// Get partition length as indicated by VBR. Valid for volumes with formatted file system.
    /// </summary>
    public long? GetVBRPartitionLength()
    {
        var bytesPerSector = NativeStruct.GetDiskGeometry(SafeFileHandle)?.BytesPerSector ?? 512;

        byte[]? allocated = null;

        var vbr = bytesPerSector <= 1024
            ? stackalloc byte[bytesPerSector]
            : (allocated = ArrayPool<byte>.Shared.Rent(bytesPerSector)).AsSpan(0, bytesPerSector);

        try
        {
            Position = 0;

            if (this.Read(vbr) < bytesPerSector)
            {
                return default;
            }

            var vbr_sector_size = MemoryMarshal.Read<short>(vbr.Slice(0xB));

            if (vbr_sector_size <= 0)
            {
                return default;
            }

            long total_sectors;

            total_sectors = MemoryMarshal.Read<ushort>(vbr.Slice(0x13));

            if (total_sectors == 0)
            {
                total_sectors = MemoryMarshal.Read<uint>(vbr.Slice(0x20));
            }

            if (total_sectors == 0)
            {
                total_sectors = MemoryMarshal.Read<long>(vbr.Slice(0x28));
            }

            if (total_sectors < 0)
            {
                return default;
            }

            return total_sectors * vbr_sector_size;
        }
        finally
        {
            if (allocated is not null)
            {
                ArrayPool<byte>.Shared.Return(allocated);
            }
        }
    }

#if NETCOREAPP
    private sealed class DiskFileStream(SafeFileHandle handle, FileAccess access)
        : CompatibilityStream
    {
        internal long? cachedLength;

        private readonly SafeFileHandle handle = handle;

        public override bool CanRead { get; } = access.HasFlag(FileAccess.Read);

        public override bool CanWrite { get; } = access.HasFlag(FileAccess.Write);

        public override bool CanSeek => true;

        /// <summary>
        /// Retrieves raw disk size.
        /// </summary>
        public override long Length
            => cachedLength ??= NativeStruct.GetDiskSize(handle)
            ?? throw new NotSupportedException("Disk size not available");

        /// <summary>
        /// Not implemented.
        /// </summary>
        public override void SetLength(long value)
            => throw new NotImplementedException();

        public override long Position { get; set; }

        public override long Seek(long offset, SeekOrigin origin)
            => Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };

        private static unsafe bool IsBufferAligned(ReadOnlySpan<byte> buffer)
        {
            const int aligmentMask = sizeof(long) - 1;

            if ((buffer.Length & aligmentMask) != 0)
            {
                return false;
            }

            fixed (byte* ptr = &MemoryMarshal.GetReference(buffer))
            {
                if (((nint)ptr & aligmentMask) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        public override int Read(byte[] array, int offset, int count)
            => Read(array.AsSpan(offset, count));

        public unsafe override int Read(Span<byte> buffer)
        {
            if (!CanRead)
            {
                throw new InvalidOperationException("Stream does not support reading");
            }

            if (IsBufferAligned(buffer))
            {
                var count = RandomAccess.Read(handle, buffer, Position);
                Position += count;
                return count;
            }
            else
            {
                var array = ArrayPool<byte>.Shared.Rent(buffer.Length);
                try
                {
                    var count = RandomAccess.Read(handle, array.AsSpan(0, buffer.Length), Position);
                    array.AsSpan(0, count).CopyTo(buffer);
                    Position += count;
                    return count;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!CanRead)
            {
                throw new InvalidOperationException("Stream does not support reading");
            }

            if (IsBufferAligned(buffer.Span))
            {
                var count = await RandomAccess.ReadAsync(handle, buffer, Position, cancellationToken).ConfigureAwait(false);
                Position += count;
                return count;
            }
            else
            {
                var array = ArrayPool<byte>.Shared.Rent(buffer.Length);
                try
                {
                    var count = await RandomAccess.ReadAsync(handle, array.AsMemory(0, buffer.Length), Position, cancellationToken).ConfigureAwait(false);
                    array.AsSpan(0, count).CopyTo(buffer.Span);
                    Position += count;
                    return count;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }
        }

        public override void Flush()
        {
            if (CanWrite)
            {
#if NET8_0_OR_GREATER
                RandomAccess.FlushToDisk(handle);
#else
                if (OperatingSystem.IsWindows())
                {
                    NativeFileIO.FlushBuffers(handle);
                }
#endif
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Flush();
            return Task.CompletedTask;
        }

        public override int ReadByte()
        {
            byte data = 0;
            var count = Read(MemoryMarshal.CreateSpan(ref data, 1));
            if (count != 1)
            {
                return -1;
            }

            return data;
        }

        public override void Write(byte[] buffer, int offset, int count)
            => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Stream does not support writing");
            }

            byte[]? array = null;

            if (!IsBufferAligned(buffer))
            {
                array = ArrayPool<byte>.Shared.Rent(buffer.Length);
                buffer.CopyTo(array);
                buffer = array.AsSpan(0, buffer.Length);
            }

            try
            {
                RandomAccess.Write(handle, buffer, Position);
                Position += buffer.Length;
            }
            finally
            {
                if (array is not null)
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!CanWrite)
            {
                throw new InvalidOperationException("Stream does not support writing");
            }

            byte[]? array = null;

            if (!IsBufferAligned(buffer.Span))
            {
                array = ArrayPool<byte>.Shared.Rent(buffer.Length);
                buffer.Span.CopyTo(array);
                buffer = array.AsMemory(0, buffer.Length);
            }

            try
            {
                await RandomAccess.WriteAsync(handle, buffer, Position, cancellationToken).ConfigureAwait(false);
                Position += buffer.Length;
            }
            finally
            {
                if (array is not null)
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }
        }

        public override void WriteByte(byte value)
            => Write(MemoryMarshal.CreateReadOnlySpan(ref value, 1));

        public override void Close()
        {
            handle?.Dispose();
            base.Close();
        }
    }
#else
    private sealed class DiskFileStream(SafeFileHandle handle, FileAccess access)
        : FileStream(handle, access, bufferSize: 1)
    {
        internal long? cachedLength;

        private readonly SafeFileHandle handle = handle;

        /// <summary>
        /// Retrieves raw disk size.
        /// </summary>
        public override long Length
            => cachedLength ??= NativeStruct.GetDiskSize(handle)
            ?? throw new NotSupportedException("Disk size not available");

        /// <summary>
        /// Not implemented.
        /// </summary>
        public override void SetLength(long value)
            => throw new NotImplementedException();
    }
#endif
            }
