//  
//  Copyright (c) 2012-2025, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Async;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression

namespace Arsenal.ImageMounter.IO.Streams;

public class CombinedSeekStream : CompatibilityStream
{
    private readonly List<KeyValuePair<long, Stream>> streams;

    private KeyValuePair<long, Stream> current;

    public bool Extendable { get; }

    public IReadOnlyCollection<KeyValuePair<long, Stream>> BaseStreams => streams;

    public Stream CurrentBaseStream => current.Value;

    public CombinedSeekStream()
        : this(true)
    {
    }

    public CombinedSeekStream(params Stream[] inputStreams)
        : this(false, inputStreams)
    {
    }

    public CombinedSeekStream(IEnumerable<Stream> inputStreams)
        : this(false, inputStreams)
    {
    }

    public CombinedSeekStream(bool writable, params Stream[] inputStreams)
    {
        if (inputStreams is null || inputStreams.Length == 0)
        {
            streams = [];

            Extendable = true;
        }
        else
        {
            streams = new(inputStreams.Length);

            Array.ForEach(inputStreams, AddStream);

            Seek(0, SeekOrigin.Begin);
        }

        CanWrite = writable;
    }

    public CombinedSeekStream(bool writable, IEnumerable<Stream> inputStreams)
    {
        streams = [];

        foreach (var stream in inputStreams)
        {
            AddStream(stream);
        }

        if (streams.Count == 0)
        {
            Extendable = true;
        }
        else
        {
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

        streams.Add(new(_length, stream));
    }

    public override int Read(byte[] buffer, int index, int count)
    {
        var num = 0;

        while (current.Value is not null && count > 0)
        {
            var r = current.Value.Read(buffer, index, count);

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

    public override async Task<int> ReadAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
    {
        var num = 0;

        while (current.Value is not null && count > 0)
        {
            var r = await current.Value.ReadAsync(buffer.AsMemory(index, count), cancellationToken).ConfigureAwait(false);

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

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override int EndRead(IAsyncResult asyncResult) =>
        ((Task<int>)asyncResult).GetAwaiter().GetResult();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var count = buffer.Length;
        var index = 0;
        var num = 0;

        while (current.Value is not null && count > 0)
        {
            var r = await current.Value.ReadAsync(buffer.Slice(index, count), cancellationToken).ConfigureAwait(false);

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
        var num = 0;

        while (current.Value is not null && count > 0)
        {
            var r = current.Value.Read(buffer.Slice(num, count));

            if (r <= 0)
            {
                break;
            }

            Seek(r, SeekOrigin.Current);

            num += r;
            count -= r;
        }

        return num;
    }

    public override void Write(byte[] buffer, int index, int count)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        if (position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer, index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (current.Value is not null && count > 0)
        {
            var current_count = (int)Math.Min(count, current.Value.Length - current.Value.Position);

            current.Value.Write(buffer, index, current_count);

            Seek(current_count, SeekOrigin.Current);

            index += current_count;
            count -= current_count;
        }
    }

    public override async Task WriteAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        if (position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer, index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (current.Value is not null && count > 0)
        {
            var current_count = (int)Math.Min(count, current.Value.Length - current.Value.Position);

            await current.Value.WriteAsync(buffer.AsMemory(index, current_count), cancellationToken).ConfigureAwait(false);

            Seek(current_count, SeekOrigin.Current);

            index += current_count;
            count -= current_count;
        }
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        WriteAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override void EndWrite(IAsyncResult asyncResult) =>
        ((Task)asyncResult).GetAwaiter().GetResult();

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!CanWrite)
        {
            throw new NotSupportedException();
        }

        var count = buffer.Length;
        var index = 0;

        if (position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer.ToArray(), index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (current.Value is not null && count > 0)
        {
            var current_count = (int)Math.Min(count, current.Value.Length - current.Value.Position);

            await current.Value.WriteAsync(buffer.Slice(index, current_count), cancellationToken).ConfigureAwait(false);

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

        if (position == _length && count > 0 && Extendable)
        {
            AddStream(new MemoryStream(buffer.ToArray(), index, count, writable: true, publiclyVisible: true));

            Seek(count, SeekOrigin.Current);

            return;
        }

        if (position >= _length && count > 0)
        {
            throw new EndOfStreamException();
        }

        while (current.Value is not null && count > 0)
        {
            var current_count = (int)Math.Min(count, current.Value.Length - current.Value.Position);

            current.Value.Write(buffer.Slice(index, current_count));

            Seek(current_count, SeekOrigin.Current);

            index += current_count;
            count -= current_count;
        }
    }

    public override void Flush() => current.Value?.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken)
        => current.Value?.FlushAsync(cancellationToken) ?? Task.CompletedTask;

    private long position;

    public override long Position
    {
        get => position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
    {
        if (ReferenceEquals(baseStream, this))
        {
            return virtualPosition;
        }

        var stream = streams.FirstOrDefault(s => s.Key > virtualPosition);

        if (stream.Value is null)
        {
            throw new EndOfStreamException();
        }

        var subStreamPosition = virtualPosition - (stream.Key - stream.Value.Length);

        return stream.Value.GetPositionInBaseStream(baseStream, subStreamPosition);
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
                offset += position;
                break;

            case SeekOrigin.End:
                offset += Length;
                break;
        }

        if (offset < 0)
        {
            throw new ArgumentException("Negative stream positions not supported");
        }

        current = streams.FirstOrDefault(s => s.Key > offset);

        current.Value?.Position = current.Value.Length - (current.Key - offset);

        position = offset;

        return offset;
    }

    protected override void Dispose(bool disposing)
    {
        if (streams != null)
        {
            if (disposing)
            {
                streams.AsParallel().ForAll(stream => stream.Value.Dispose());
            }

            streams.Clear();
        }

        base.Dispose(disposing);
    }
}
