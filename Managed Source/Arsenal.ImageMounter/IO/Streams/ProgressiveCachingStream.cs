using DiscUtils.Streams.Compatibility;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arsenal.ImageMounter.IO.Streams;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator
#pragma warning disable IDE0056 // Use index operator

/// <summary>
/// A read-only <see cref="Stream"/> wrapper that progressively buffers data from a source stream
/// (which may be non-seekable) to allow random access up to the furthest point read.
/// </summary>
/// <remarks>
/// <para>
/// If the source stream is non-seekable but its length is known, this wrapper allows limited seeking
/// and rereading without pre-buffering the entire stream. Forward seeks cause data to be read and cached
/// up to the target offset; backward seeks read from the in-memory cache.
/// </para>
/// <para>
/// Data is cached in fixed-size chunks obtained from <see cref="ArrayPool{T}"/> to reduce allocation cost.
/// </para>
/// </remarks>
public sealed class ProgressiveCachingStream : CompatibilityStream
{
    private readonly bool _leaveOpen;
    private readonly long _length;
    private readonly int _chunkSize;
    private readonly ArrayPool<byte> _pool;
    private readonly List<byte[]> _chunks = [];
    
    private long _position;
    private bool _disposed;

    public Stream BaseStream { get; }

    public long Buffered { get; private set; } // total bytes we have pulled from source

    public bool IsCompleted { get; private set; } // reached end-of-source (or fully buffered to _length)

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressiveCachingStream"/> class.
    /// </summary>
    /// <param name="source">The source <see cref="Stream"/> to read from. Must be readable.</param>
    /// <param name="knownLength">
    /// Optional known total length of the stream.  
    /// Required if <paramref name="source"/> is non-seekable or throws on <see cref="Stream.Length"/>.
    /// </param>
    /// <param name="chunkSize">
    /// Size (in bytes) of each internal buffer chunk.  
    /// Defaults to 64 KB. Must be greater than zero.
    /// </param>
    /// <param name="pool">
    /// The <see cref="ArrayPool{T}"/> to use for renting buffers.  
    /// If <c>null</c>, <see cref="ArrayPool{T}.Shared"/> is used.
    /// </param>
    /// <param name="leaveOpen">
    /// If <c>true</c>, the underlying <paramref name="source"/> stream remains open after this stream is disposed.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="source"/> is not readable, or if a non-seekable source has no <paramref name="knownLength"/>.
    /// </exception>
    public ProgressiveCachingStream(Stream source, long? knownLength = null,
        int chunkSize = 2 * 1024 * 1024, ArrayPool<byte>? pool = null, bool leaveOpen = false)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }
#endif

#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
#else
        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize));
        }
#endif

        if (!source.CanRead)
        {
            throw new ArgumentException("Source stream must be readable.", nameof(source));
        }

        BaseStream = source;
        _leaveOpen = leaveOpen;
        _chunkSize = chunkSize;
        _pool = pool ?? ArrayPool<byte>.Shared;

        try
        {
            _length = knownLength ?? source.Length;
        }
        catch
        {
            if (knownLength is null)
            {
                throw new ArgumentException("Provide knownLength when source length is not available.", nameof(knownLength));
            }

            _length = knownLength.Value;
        }
    }

    /// <inheritdoc/>
    public override bool CanRead => !_disposed;

    /// <inheritdoc/>
    public override bool CanSeek => !_disposed;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    /// <inheritdoc/>
    public override void Flush() { /* no-op */ }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Seeks to a new position in the stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the origin parameter.</param>
    /// <param name="origin">A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
    /// <returns>The new position within the stream.</returns>
    /// <remarks>
    /// If the target position lies beyond the currently buffered range, the method reads and buffers data
    /// from the source until the target position is reached.
    /// </remarks>
    /// <exception cref="IOException">Thrown when attempting to seek outside the valid range (0 … Length).</exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureNotDisposed();

        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (target < 0 || target > _length)
        {
            throw new IOException("Attempted to seek outside the stream bounds.");
        }

        // If seeking forward past what we have buffered, pull just enough data.
        EnsureBuffered(target);

        _position = target;
        return _position;
    }

    /// <summary>
    /// Reads a sequence of bytes from the current stream and advances the position.
    /// </summary>
    /// <param name="buffer">The buffer to read data into.</param>
    /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing data.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The total number of bytes read into <paramref name="buffer"/>.</returns>
    /// <remarks>
    /// Reads are served from the in-memory cache if available; otherwise,
    /// data is read from the underlying source and cached progressively.
    /// </remarks>
    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    /// <summary>
    /// Reads a sequence of bytes from the current stream and advances the position.
    /// </summary>
    /// <param name="destination">The buffer to read data into.</param>
    /// <returns>The total number of bytes read into <paramref name="destination"/>.</returns>
    /// <remarks>
    /// Reads are served from the in-memory cache if available; otherwise,
    /// data is read from the underlying source and cached progressively.
    /// </remarks>
    public override int Read(Span<byte> destination)
    {
        EnsureNotDisposed();

        if (_position >= _length || destination.Length == 0)
        {
            return 0;
        }

        long maxWanted = Math.Min(_position + destination.Length, _length);
        EnsureBuffered(maxWanted);

        int toCopy = (int)(maxWanted - _position);
        CopyFromCache(_position, destination.Slice(0, toCopy));
        _position += toCopy;
        return toCopy;
    }

    /// <summary>
    /// Asynchronously reads a sequence of bytes from the current stream.
    /// </summary>
    /// <param name="destination">The buffer to read data into.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The total number of bytes read into <paramref name="destination"/>.</returns>
    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        if (_position >= _length || destination.Length == 0)
        {
            return 0;
        }

        long maxWanted = Math.Min(_position + destination.Length, _length);
        await EnsureBufferedAsync(maxWanted, cancellationToken).ConfigureAwait(false);

        int toCopy = (int)(maxWanted - _position);
        CopyFromCache(_position, destination.Span.Slice(0, toCopy));
        _position += toCopy;
        return toCopy;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    /// Releases all resources used by the current <see cref="ProgressiveCachingStream"/>.
    /// </summary>
    /// <param name="disposing">
    /// <c>true</c> to release both managed and unmanaged resources;
    /// <c>false</c> to release only unmanaged resources.
    /// </param>
    /// <remarks>
    /// <para>
    /// When called with <paramref name="disposing"/> set to <c>true</c>, this method:
    /// </para>
    /// <list type="bullet">
    ///   <item>Returns all rented buffer chunks to the <see cref="ArrayPool{T}"/> that provided them.</item>
    ///   <item>Clears the internal chunk list and marks the stream as disposed so that further operations throw <see cref="ObjectDisposedException"/>.</item>
    ///   <item>Disposes the underlying source stream unless the instance was created with <c>leaveOpen = true</c>.</item>
    /// </list>
    /// <para>
    /// After disposal, the buffered data is no longer accessible and all allocated memory from the pool
    /// becomes available for reuse by other components.
    /// </para>
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Return pooled arrays
        foreach (var chunk in _chunks)
        {
            _pool.Return(chunk);
        }

        _chunks.Clear();

        if (disposing && !_leaveOpen)
        {
            BaseStream.Dispose();
        }

        base.Dispose(disposing);
    }

    // ---- internals ----

    private void EnsureBuffered(long target)
    {
        if (IsCompleted || target <= Buffered)
        {
            return;
        }

        // Grow chunks and copy from source until we have >= target or hit EOF.
        while (!IsCompleted && Buffered < target)
        {
            int withinChunk = (int)(Buffered % _chunkSize);
            bool needNewChunk = withinChunk == 0;

            if (needNewChunk)
            {
                _chunks.Add(_pool.Rent(_chunkSize));
            }

            var chunk = _chunks[_chunks.Count - 1];
            int space = _chunkSize - withinChunk;
            int toRead = (int)Math.Min(space, _length - Buffered);

            if (toRead <= 0)
            {
                IsCompleted = true;
                break;
            }

            int read;

            if (BaseStream.CanSeek)
            {
                // Keep source position in sync with buffered extent if it's seekable
                if (BaseStream.Position != Buffered)
                {
                    BaseStream.Seek(Buffered, SeekOrigin.Begin);
                }
            }

            EnsureNotDisposed();

            read = BaseStream.Read(chunk.AsSpan(withinChunk, toRead));

            if (read == 0)
            {
                // Unexpected EOF before advertised _length
                IsCompleted = true;
                if (Buffered < target)
                {
                    throw new EndOfStreamException("Source ended before the known length.");
                }

                break;
            }

            Buffered += read;
            if (Buffered >= _length)
            {
                IsCompleted = true;
            }
        }
    }

    private async ValueTask EnsureBufferedAsync(long target, CancellationToken ct = default)
    {
        if (IsCompleted || target <= Buffered)
        {
            return;
        }

        // Grow chunks and copy from source until we have >= target or hit EOF.
        while (!IsCompleted && Buffered < target)
        {
            int withinChunk = (int)(Buffered % _chunkSize);
            bool needNewChunk = withinChunk == 0;

            if (needNewChunk)
            {
                _chunks.Add(_pool.Rent(_chunkSize));
            }

            var chunk = _chunks[_chunks.Count - 1];
            int space = _chunkSize - withinChunk;
            int toRead = (int)Math.Min(space, _length - Buffered);

            if (toRead <= 0)
            {
                IsCompleted = true;
                break;
            }

            int read;

            if (BaseStream.CanSeek)
            {
                // Keep source position in sync with buffered extent if it's seekable
                if (BaseStream.Position != Buffered)
                {
                    BaseStream.Seek(Buffered, SeekOrigin.Begin);
                }
            }

            EnsureNotDisposed();

            read = await BaseStream.ReadAsync(chunk.AsMemory(withinChunk, toRead), ct).ConfigureAwait(false);

            if (read == 0)
            {
                // Unexpected EOF before advertised _length
                IsCompleted = true;
                if (Buffered < target)
                {
                    throw new EndOfStreamException("Source ended before the known length.");
                }

                break;
            }

            Buffered += read;
            if (Buffered >= _length)
            {
                IsCompleted = true;
            }
        }
    }

    private void CopyFromCache(long srcOffset, Span<byte> dest)
    {
        int remaining = dest.Length;
        int destOffset = 0;

        while (remaining > 0)
        {
            int chunkIndex = (int)(srcOffset / _chunkSize);
            int withinChunk = (int)(srcOffset % _chunkSize);
            var chunk = _chunks[chunkIndex];

            int availableInChunk = Math.Min(_chunkSize - withinChunk, remaining);
            chunk.AsSpan(withinChunk, availableInChunk)
                 .CopyTo(dest.Slice(destOffset, availableInChunk));

            remaining -= availableInChunk;
            destOffset += availableInChunk;
            srcOffset += availableInChunk;
        }
    }

    private void EnsureNotDisposed()
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProgressiveCachingStream));
        }
#endif
    }
}
