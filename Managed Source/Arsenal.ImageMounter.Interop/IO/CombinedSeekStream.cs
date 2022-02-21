using Arsenal.ImageMounter.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA1835 // Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'

namespace Arsenal.ImageMounter.IO;

public class CombinedSeekStream : Stream
{
    private readonly Dictionary<long, Stream> _streams;

    private KeyValuePair<long, Stream> _current;

    public bool Extendable { get; }

    public ICollection<Stream> BaseStreams => _streams.Values;

    public Stream CurrentBaseStream => _current.Value;

    public CombinedSeekStream()
        : this(true)
    {
    }

    public CombinedSeekStream(params Stream[] inputStreams)
        : this(false, inputStreams)
    {
    }

    public CombinedSeekStream(bool writable, params Stream[] inputStreams)
    {
        if (inputStreams is null || inputStreams.Length == 0)
        {
            _streams = new();

            Extendable = true;
        }
        else
        {
            _streams = new(inputStreams.Length);

            Array.ForEach(inputStreams, AddStream);

            Seek(0, SeekOrigin.Begin);
        }

        CanWrite = writable;
    }

    public void AddStream(Stream stream)
    {
        if (!stream.CanSeek || !stream.CanRead)
        {
            throw new NotSupportedException("Needs seekable and readable streams");
        }

        if (stream.Length == 0)
        {
            return;
        }

        checked
        {
            _length += stream.Length;
        }

        _streams.Add(_length, stream);
    }

    public override int Read(byte[] buffer, int index, int count)
    {
        var num = 0;

        while (_current.Value is not null && count > 0)
        {
            var r = _current.Value.Read(buffer, index, count);

            if (r <= 0)
            {
                break;
            }

            Seek(r, SeekOrigin.Current);

            num += r;
            index += r;
            count -= r;
        }

        return num;
    }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    public override async Task<int> ReadAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
    {
        var num = 0;

        while (_current.Value is not null && count > 0)
        {
            var r = await _current.Value.ReadAsync(buffer, index, count, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            if (r <= 0)
            {
                break;
            }

            Seek(r, SeekOrigin.Current);

            num += r;
            index += r;
            count -= r;
        }

        return num;
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override int EndRead(IAsyncResult asyncResult) =>
        ((Task<int>)asyncResult).Result;

#endif

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = buffer.Length;
        var index = 0;
        var num = 0;

        while (_current.Value is not null && count > 0)
        {
            var r = await _current.Value.ReadAsync(buffer.Slice(index, count), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            if (r <= 0)
            {
                break;
            }

            Seek(r, SeekOrigin.Current);

            num += r;
            index += r;
            count -= r;
        }

        return num;
    }

    public override int Read(Span<byte> buffer)
    {
        var count = buffer.Length;
        var index = 0;
        var num = 0;

        while (_current.Value is not null && count > 0)
        {
            var r = _current.Value.Read(buffer.Slice(index, count));

            if (r <= 0)
            {
                break;
            }

            Seek(r, SeekOrigin.Current);

            num += r;
            index += r;
            count -= r;
        }

        return num;
    }

#endif

    public override void Write(byte[] buffer, int index, int count)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        if (_position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer, index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (_position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (_current.Value is not null && count > 0)
        {
            var current_count = (int)Math.Min(count, _current.Value.Length - _current.Value.Position);

            _current.Value.Write(buffer, index, current_count);

            Seek(current_count, SeekOrigin.Current);

            index += current_count;
            count -= current_count;
        }
    }

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    public override async Task WriteAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        if (_position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer, index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (_position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (_current.Value is not null && count > 0)
        {
            var current_count = (int)Math.Min(count, _current.Value.Length - _current.Value.Position);

            await _current.Value.WriteAsync(buffer, index, current_count, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            Seek(current_count, SeekOrigin.Current);

            index += current_count;
            count -= current_count;
        }
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) =>
        WriteAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override void EndWrite(IAsyncResult asyncResult) =>
        ((Task)asyncResult).Wait();

#endif

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
    public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        var count = buffer.Length;
        var index = 0;

        if (_position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer.ToArray(), index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (_position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (_current.Value is not null && count > 0)
        {
            var current_count = (int)Math.Min(count, _current.Value.Length - _current.Value.Position);

            await _current.Value.WriteAsync(buffer.Slice(index, current_count), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            Seek(current_count, SeekOrigin.Current);

            index += current_count;
            count -= current_count;
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        var count = buffer.Length;
        var index = 0;

        if (_position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer.ToArray(), index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (_position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (_current.Value is not null && count > 0)
        {
            var current_count = (int)Math.Min(count, _current.Value.Length - _current.Value.Position);

            _current.Value.Write(buffer.Slice(index, current_count));

            Seek(current_count, SeekOrigin.Current);

            index += current_count;
            count -= current_count;
        }
    }
#endif

    public override void Flush() => _current.Value?.Flush();

#if NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
    public override Task FlushAsync(CancellationToken cancellationToken) => _current.Value?.FlushAsync(cancellationToken) ?? Task.FromResult(0);
#endif

    private long _position;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public long? PhysicalPosition
    {
        get
        {
            var stream = _current.Value;
            if (stream is null)
            {
                var last_stream = _streams.LastOrDefault();
                if (last_stream.Value is null)
                {
                    return null;
                }

                stream = last_stream.Value;
            }

            if (stream is null)
            {
                return null;
            }
            else
            {
                return stream.Position;
            }
        }
    }

    private long _length;

    public override long Length => _length;

    public override void SetLength(long value) => throw new NotSupportedException();

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite { get; }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Current:
                offset += _position;
                break;

            case SeekOrigin.End:
                offset = Length + offset;
                break;
        }

        if (offset < 0)
        {
            throw new ArgumentException("Negative stream positions not supported");
        }

        _current = _streams.FirstOrDefault(s => s.Key > offset);

        if (_current.Value is not null)
        {
            _current.Value.Position = _current.Value.Length - (_current.Key - offset);
        }

        _position = offset;

        return offset;
    }

    public override void Close()
    {
        if (_streams != null)
        {
            _streams.Values.AsParallel().ForAll(stream => stream.Dispose());
            _streams.Clear();
        }

        base.Close();
    }
}
