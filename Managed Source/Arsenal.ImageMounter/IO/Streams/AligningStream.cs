//  
//  Copyright (c) 2012-2024, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using DiscUtils.Streams;
using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Async;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator


namespace Arsenal.ImageMounter.IO.Streams;

public class AligningStream(Stream baseStream, int alignment, bool forceReadOnly, bool ownsBaseStream) : CompatibilityStream
{
    private readonly byte[] lastReadBuffer = new byte[alignment];

    private long lastReadPos = -1;

    public Stream BaseStream { get; } = baseStream;

    public int Alignment { get; } = alignment;

    public event EventHandler? Disposing;

    public event EventHandler? Disposed;

    public AligningStream(Stream baseStream, int alignment, bool ownsBaseStream)
        : this(baseStream, alignment, forceReadOnly: false, ownsBaseStream)
    {
    }

    public override bool CanRead => BaseStream.CanRead;

    public override bool CanSeek => BaseStream.CanSeek;

    public override bool CanWrite { get; } = !forceReadOnly && baseStream.CanWrite;

    public override long Length => BaseStream.Length;

    public override long Position { get; set; }

    public override void Flush() => BaseStream.Flush();

    private int SafeBaseRead(byte[] buffer, int offset, int count)
    {
        if (Position >= Length)
        {
            Trace.WriteLine($"Attempt to read {count} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            return 0;
        }
        
        if (checked(Position + count > Length))
        {
#if DEBUG
            Trace.WriteLine($"Attempt to read {count} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
#endif
            count = (int)(Length - Position);
        }

        if (Position == lastReadPos && count <= Alignment)
        {
            count = Math.Min(count, Alignment);

            Array.Copy(lastReadBuffer, 0, buffer, offset, count);

            Position += count;

            return count;
        }

        var totalSize = 0;

        while (count > 0)
        {
            BaseStream.Position = Position;
            var blockSize = BaseStream.Read(buffer, offset, count);
            Position = BaseStream.Position;

            if (blockSize == 0)
            {
                break;
            }

            count -= blockSize;
            offset += blockSize;
            totalSize += blockSize;
        }

        if (totalSize >= Alignment)
        {
            Array.Copy(buffer, offset - Alignment, lastReadBuffer, 0, Alignment);
            lastReadPos = Position - Alignment;
        }
        else
        {
            lastReadPos = -1;
        }

        return totalSize;
    }

    private async ValueTask<int> SafeBaseReadAsync(Memory<byte> buffer, CancellationToken token)
    {
        if (Position >= Length)
        {
            Trace.WriteLine($"Attempt to read {buffer.Length} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            return 0;
        }
        
        if (checked(Position + buffer.Length > Length))
        {
#if DEBUG
            Trace.WriteLine($"Attempt to read {buffer.Length} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
#endif
            buffer = buffer.Slice(0, (int)(Length - Position));
        }

        if (Position == lastReadPos && buffer.Length <= Alignment)
        {
            var count = Math.Min(buffer.Length, Alignment);

            lastReadBuffer.AsSpan(0, count).CopyTo(buffer.Span);

            Position += count;

            return count;
        }

        var totalSize = 0;

        var slice = buffer;

        while (slice.Length > 0)
        {            
            BaseStream.Position = Position;
            
            var blockSize = await BaseStream.ReadAsync(slice, token).ConfigureAwait(false);
            
            Position = BaseStream.Position;

            if (blockSize == 0)
            {
                break;
            }

            slice = slice.Slice(blockSize);
            totalSize += blockSize;
        }
        
        if (totalSize >= Alignment)
        {
            buffer.Span.Slice(totalSize - Alignment, Alignment).CopyTo(lastReadBuffer);
            lastReadPos = Position - Alignment;
        }
        else
        {
            lastReadPos = -1;
        }

        return totalSize;
    }

    private int SafeBaseRead(Span<byte> buffer)
    {
        if (Position >= Length)
        {
            Trace.WriteLine($"Attempt to read {buffer.Length} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
            return 0;
        }
        
        if (checked(Position + buffer.Length > Length))
        {
#if DEBUG
            Trace.WriteLine($"Attempt to read {buffer.Length} bytes from 0x{Position:X} which is beyond end of physical media, base stream length is 0x{Length:X}");
#endif
            buffer = buffer.Slice(0, (int)(Length - Position));
        }

        if (Position == lastReadPos && buffer.Length <= Alignment)
        {
            var count = Math.Min(buffer.Length, Alignment);

            lastReadBuffer.AsSpan(0, count).CopyTo(buffer);

            Position += count;

            return count;
        }

        var totalSize = 0;

        var slice = buffer;

        while (slice.Length > 0)
        {
            BaseStream.Position = Position;

            var blockSize = BaseStream.Read(slice);

            Position = BaseStream.Position;

            if (blockSize == 0)
            {
                break;
            }

            slice = slice.Slice(blockSize);
            totalSize += blockSize;
        }

        if (totalSize >= Alignment)
        {
            buffer.Slice(totalSize - Alignment, Alignment).CopyTo(lastReadBuffer);
            lastReadPos = Position - Alignment;
        }
        else
        {
            lastReadPos = -1;
        }

        return totalSize;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => InternalReadAsync(buffer, cancellationToken);

    public override int Read(Span<byte> buffer)
    {
        var count = buffer.Length;

        var prefix = (int)(Position & Alignment - 1);
        var suffix = -checked(prefix + count) & Alignment - 1;
        var newsize = prefix + count + suffix;

        if (newsize == count)
        {
            return SafeBaseRead(buffer);
        }

        using var newBufferHandle = MemoryPool<byte>.Shared.Rent(newsize);
        var newBuffer = newBufferHandle.Memory.Span.Slice(0, newsize);
        Position -= prefix;
        var result = SafeBaseRead(newBuffer);
        if (result < prefix)
        {
            return 0;
        }

        result -= prefix;

        if (result > count)
        {
            Position += count - result;
            result = count;
        }

        newBuffer.Slice(prefix, result).CopyTo(buffer);

        return result;
    }

    private async ValueTask<int> InternalReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = buffer.Length;
        var offset = 0;

        var prefix = (int)(Position & Alignment - 1);
        var suffix = -checked(prefix + count) & Alignment - 1;
        var newsize = prefix + count + suffix;

        if (newsize == count)
        {
            return await SafeBaseReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        using var newBufferHandle = MemoryPool<byte>.Shared.Rent(newsize);
        var newBuffer = newBufferHandle.Memory.Slice(0, newsize);
        Position -= prefix;
        var result = await SafeBaseReadAsync(newBuffer, cancellationToken).ConfigureAwait(false);
        if (result < prefix)
        {
            return 0;
        }

        result -= prefix;

        if (result > count)
        {
            Position += count - result;
            result = count;
        }

        newBuffer.Slice(prefix, result).CopyTo(buffer.Slice(offset));

        return result;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var prefix = (int)(Position & Alignment - 1);
        var suffix = -checked(prefix + count) & Alignment - 1;
        var newsize = prefix + count + suffix;

        if (newsize == count)
        {
            return SafeBaseRead(buffer, offset, count);
        }

        var newbuffer = ArrayPool<byte>.Shared.Rent(newsize);
        try
        {
            Position -= prefix;
            var result = SafeBaseRead(newbuffer, 0, newsize);
            if (result < prefix)
            {
                return 0;
            }

            result -= prefix;

            if (result > count)
            {
                Position += count - result;
                result = count;
            }

            System.Buffer.BlockCopy(newbuffer, prefix, buffer, offset, result);

            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(newbuffer);
        }
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override int EndRead(IAsyncResult asyncResult) => ((Task<int>)asyncResult).GetAwaiter().GetResult();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => InternalReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override long Seek(long offset, SeekOrigin origin) => origin switch
    {
        SeekOrigin.Begin => Position = offset,
        SeekOrigin.Current => Position += offset,
        SeekOrigin.End => Position = Length + offset,
        _ => throw new ArgumentOutOfRangeException(nameof(origin)),
    };

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!CanWrite)
        {
            throw new UnauthorizedAccessException("Attempt to write to read-only stream");
        }

        var original_position = Position;

        if (count == 0)
        {
            return;
        }

        var prefix = (int)(Position & Alignment - 1);
        var suffix = -checked(prefix + count) & Alignment - 1;

        var new_array_size = checked(prefix + count + suffix);

        var new_buffer = new_array_size != count ?
            ArrayPool<byte>.Shared.Rent(new_array_size) :
            null;

        try
        {
            if (new_array_size != count)
            {
                if (new_buffer is null)
                {
#if NET7_0_OR_GREATER
                    throw new UnreachableException();
#else
                    throw new InvalidProgramException();
#endif
                }

                if (prefix != 0)
                {
                    Position = checked(original_position - prefix);

                    SafeBaseRead(new_buffer, 0, Alignment);
                }

                if (suffix != 0)
                {
                    Position = checked(original_position + count + suffix - Alignment);

                    SafeBaseRead(new_buffer, new_array_size - Alignment, Alignment);
                }

                System.Buffer.BlockCopy(buffer, offset, new_buffer, prefix, count);

                Position = original_position - prefix;

                buffer = new_buffer;
                offset = 0;
                count = new_array_size;
            }

            var absolute = Position + new_array_size;

            if (GrowInterval > 0 && absolute > Length)
            {
                absolute += -absolute & GrowInterval;

                SetLength(absolute);
            }

            BaseStream.Position = Position;
            BaseStream.Write(buffer, offset, count);
            Position = BaseStream.Position;

            Array.Copy(buffer, offset + count - Alignment, lastReadBuffer, 0, Alignment);
            lastReadPos = Position - Alignment;

            if (suffix > 0)
            {
                Position -= suffix;
            }
        }
        finally
        {
            if (new_buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(new_buffer);
            }
        }
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        WriteAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override void EndWrite(IAsyncResult asyncResult) => ((Task)asyncResult).Wait();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => InternalWriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        BaseStream.Position = Position;
        await BaseStream.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
        Position = BaseStream.Position;
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
        => BaseStream.FlushAsync(cancellationToken);

    private async ValueTask InternalWriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!CanWrite)
        {
            throw new UnauthorizedAccessException("Attempt to write to read-only stream");
        }

        var count = buffer.Length;

        if (count == 0)
        {
            return;
        }

        var original_position = Position;

        var prefix = (int)(Position & Alignment - 1);
        var suffix = -checked(prefix + count) & Alignment - 1;

        var new_array_size = checked(prefix + count + suffix);

        Position -= prefix;

        using var memory_owner = count != new_array_size
            ? MemoryPool<byte>.Shared.Rent(new_array_size)
            : null;

        if (count != new_array_size)
        {
            if (memory_owner is null)
            {
#if NET7_0_OR_GREATER
                throw new UnreachableException();
#else
                throw new InvalidProgramException();
#endif
            }

            var new_buffer = memory_owner.Memory.Slice(0, new_array_size);

            if (prefix != 0)
            {
                Position = checked(original_position - prefix);

                await this.ReadAsync(new_buffer.Slice(0, Alignment), cancellationToken).ConfigureAwait(false);
            }

            if (suffix != 0)
            {
                Position = checked(original_position + count + suffix - Alignment);

                await this.ReadAsync(new_buffer.Slice(new_buffer.Length - Alignment), cancellationToken).ConfigureAwait(false);
            }

            buffer.CopyTo(new_buffer.Slice(prefix));

            Position = original_position - prefix;

            buffer = new_buffer;
            count = new_array_size;
        }

        var absolute = Position + new_array_size;

        if (GrowInterval > 0 && absolute > Length)
        {
            absolute += -absolute & GrowInterval;

            SetLength(absolute);
        }

        BaseStream.Position = Position;
        await BaseStream.WriteAsync(buffer.Slice(0, count), cancellationToken).ConfigureAwait(false);
        Position = BaseStream.Position;

        buffer.Span.Slice(count - Alignment, Alignment).CopyTo(lastReadBuffer);
        lastReadPos = Position - Alignment;

        if (suffix > 0)
        {
            Position -= suffix;
        }
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => InternalWriteAsync(buffer, cancellationToken);

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!CanWrite)
        {
            throw new UnauthorizedAccessException("Attempt to write to read-only stream");
        }

        var count = buffer.Length;
        var offset = 0;

        if (count == 0)
        {
            return;
        }

        var original_position = Position;

        var prefix = (int)(Position & Alignment - 1);
        var suffix = -checked(prefix + count) & Alignment - 1;

        var new_array_size = checked(prefix + count + suffix);

        Position -= prefix;

        using var memory_owner = count != new_array_size
            ? MemoryPool<byte>.Shared.Rent(new_array_size)
            : null;

        if (count != new_array_size)
        {
            if (memory_owner is null)
            {
#if NET7_0_OR_GREATER
                throw new UnreachableException();
#else
                throw new InvalidProgramException();
#endif
            }

            var new_buffer = memory_owner.Memory.Span.Slice(0, new_array_size);

            if (prefix != 0)
            {
                Position = checked(original_position - prefix);

                this.ReadExactly(new_buffer.Slice(0, Alignment));
            }

            if (suffix != 0)
            {
                Position = checked(original_position + count + suffix - Alignment);

                this.ReadExactly(new_buffer.Slice(new_buffer.Length - Alignment));
            }

            buffer.CopyTo(new_buffer.Slice(prefix));

            Position = original_position - prefix;

            buffer = new_buffer;
            offset = 0;
            count = new_array_size;
        }

        var absolute = Position + new_array_size;

        if (GrowInterval > 0 && absolute > Length)
        {
            absolute += -absolute & GrowInterval;

            SetLength(absolute);
        }

        BaseStream.Position = Position;
        BaseStream.Write(buffer.Slice(offset, count));
        Position = BaseStream.Position;

        buffer.Slice(count - Alignment, Alignment).CopyTo(lastReadBuffer);
        lastReadPos = Position - Alignment;

        if (suffix > 0)
        {
            Position -= suffix;
        }
    }

    public override bool CanTimeout => BaseStream.CanTimeout;

    protected override void Dispose(bool disposing)
    {
        if (disposing && ownsBaseStream)
        {
            OnDisposing(EventArgs.Empty);

            BaseStream.Dispose();

            OnDisposed(EventArgs.Empty);
        }

        base.Dispose(disposing);
    }

    protected virtual void OnDisposing(EventArgs e) => Disposing?.Invoke(this, e);

    protected virtual void OnDisposed(EventArgs e) => Disposed?.Invoke(this, e);

    public override int ReadTimeout
    {
        get => BaseStream.ReadTimeout;
        set => BaseStream.ReadTimeout = value;
    }

    public override void SetLength(long value) => BaseStream.SetLength(value);

    public override int WriteTimeout
    {
        get => BaseStream.WriteTimeout;
        set => BaseStream.WriteTimeout = value;
    }

    public long GrowInterval { get; set; } = (32L << 20) - 1;
}
