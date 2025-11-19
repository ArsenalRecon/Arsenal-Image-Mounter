using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Async;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.IO.Streams;

public class CombinedMemoryStream : CompatibilityStream
{
    private readonly List<KeyValuePair<long, ReadOnlyMemory<byte>>> streams;

    private KeyValuePair<long, ReadOnlyMemory<byte>> current;

    public bool Extendable { get; }

    public IReadOnlyCollection<KeyValuePair<long, ReadOnlyMemory<byte>>> BaseStreams => streams;

    public ReadOnlyMemory<byte> CurrentBaseStream => current.Value;

    public CombinedMemoryStream()
        : this(true)
    {
    }

    public CombinedMemoryStream(params ReadOnlyMemory<byte>[] inputStreams)
        : this(false, inputStreams)
    {
    }

    public CombinedMemoryStream(IEnumerable<ReadOnlyMemory<byte>> inputStreams)
        : this(false, inputStreams)
    {
    }

    public CombinedMemoryStream(bool writable, params ReadOnlyMemory<byte>[] inputStreams)
    {
        if (inputStreams is null || inputStreams.Length == 0)
        {
            streams = [];

            Extendable = true;
        }
        else
        {
            streams = new(inputStreams.Length);

            Array.ForEach(inputStreams, AddMemory);

            Seek(0, SeekOrigin.Begin);
        }

        CanWrite = writable;
    }

    public CombinedMemoryStream(bool writable, IEnumerable<ReadOnlyMemory<byte>> inputStreams)
    {
        streams = [];

        foreach (var stream in inputStreams)
        {
            AddMemory(stream);
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

    public void AddMemory(ReadOnlyMemory<byte> stream)
    {
        if (stream.IsEmpty)
        {
            return;
        }

        checked
        {
            _length += stream.Length;
        }

        streams.Add(new(_length, stream));

        Seek(0, SeekOrigin.Current);
    }

    public override int Read(byte[] buffer, int index, int count)
        => Read(buffer.AsSpan(index, count));

    public override Task<int> ReadAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
        => Task.FromResult(Read(buffer.AsSpan(index, count)));

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override int EndRead(IAsyncResult asyncResult) =>
        ((Task<int>)asyncResult).GetAwaiter().GetResult();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => new(Read(buffer.Span));

    public override int Read(Span<byte> buffer)
    {
        var count = buffer.Length;
        var num = 0;

        while (!current.Value.IsEmpty && count > 0)
        {
            var pos = current.Value.Span.Slice((int)(current.Value.Length - (current.Key - position)));

            var r = Math.Min(pos.Length, count);

            if (r <= 0)
            {
                break;
            }

            pos.Slice(0, r).CopyTo(buffer.Slice(num, count));

            Seek(r, SeekOrigin.Current);

            num += r;
            count -= r;
        }

        return num;
    }

    public override void Write(byte[] buffer, int index, int count)
        => Write(buffer.AsSpan(index, count));

    public override Task WriteAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(index, count));
        return Task.CompletedTask;
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        WriteAsync(buffer, offset, count, CancellationToken.None).AsAsyncResult(callback, state);

    public override void EndWrite(IAsyncResult asyncResult) =>
        ((Task)asyncResult).GetAwaiter().GetResult();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return default;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
        => throw new NotImplementedException();

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private long position;

    public override long Position
    {
        get => position;
        set => Seek(value, SeekOrigin.Begin);
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

        position = offset;

        return offset;
    }

    protected override void Dispose(bool disposing)
    {
        streams?.Clear();

        base.Dispose(disposing);
    }
}
