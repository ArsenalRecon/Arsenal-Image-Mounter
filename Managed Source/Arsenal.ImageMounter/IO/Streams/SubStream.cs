//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using Arsenal.ImageMounter.Extensions;
using DiscUtils.Streams.Compatibility;
using LTRData.Extensions.Async;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0057 // Use range operator

namespace Arsenal.ImageMounter.IO.Streams;

public class SubStream : CompatibilityStream
{
    private readonly long length;

    public bool OwnsParent { get; }
    public long Start { get; }
    public Stream Parent { get; }

    public SubStream(Stream parent, long start, long length)
    {
        Parent = parent;
        Start = start;
        this.length = length;
        OwnsParent = false;

        if (checked(Start + this.length) > Parent.Length)
        {
            throw new ArgumentException("SubStream extends beyond end of parent stream");
        }
    }

    public SubStream(Stream parent, bool ownsParent, long start, long length)
    {
        Parent = parent;
        OwnsParent = ownsParent;
        Start = start;
        this.length = length;

        if (checked(Start + this.length) > Parent.Length)
        {
            throw new ArgumentException("SubStream extends beyond end of parent stream");
        }
    }

    public override bool CanRead => Parent.CanRead;

    public override bool CanSeek => Parent.CanSeek;

    public override bool CanWrite => Parent.CanWrite;

    public override long Length => length;

    public override long Position
    {
        get => checked(Parent.Position - Start);

        set
        {
            if (value >= 0 && value <= length)
            {
                Parent.Position = checked(value + Start);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Attempt to move beyond start or end of stream");
            }
        }
    }

    public override long? GetPositionInBaseStream(Stream baseStream, long virtualPosition)
    {
        if (ReferenceEquals(baseStream, this))
        {
            return virtualPosition;
        }

        return Parent.GetPositionInBaseStream(baseStream, Start + virtualPosition);
    }

    public override void Flush() => Parent.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken)
        => Parent.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to read negative bytes");
        }

        return Position >= length ? 0 : Parent.Read(buffer, offset, (int)Math.Min(count, checked(length - Position)));
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to read negative bytes");
        }

        return Position >= length
            ? AsyncCompatExtensions.ZeroCompletedTask
            : Parent.ReadAsync(buffer, offset, (int)Math.Min(count, checked(length - Position)), cancellationToken);
    }

    public override int Read(Span<byte> buffer)
        => Position >= length
        ? 0
        : Parent.Read(buffer.Slice(0, (int)Math.Min(buffer.Length, checked(length - Position))));

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => Position >= length
        ? new ValueTask<int>(0)
        : Parent.ReadAsync(buffer.Slice(0, (int)Math.Min(buffer.Length, checked(length - Position))), cancellationToken);

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to read negative bytes");
        }

        if (Position >= length)
        {
            count = 0;
        }

        return Parent.BeginRead(buffer, offset, (int)Math.Min(count, checked(length - Position)), callback, state);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (origin == SeekOrigin.Current)
        {
            Position += offset;
        }
        else if (origin == SeekOrigin.End)
        {
            Position = length + offset;
        }
        else if (origin == SeekOrigin.Begin)
        {
            Position = offset;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "Invalid origin");
        }

        return Position;
    }

    public override void SetLength(long value)
        => throw new NotSupportedException("Attempt to change length of a SubStream");

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write negative bytes");
        }

        if (checked(Position + count) > length)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write beyond end of SubStream");
        }

        Parent.Write(buffer, offset, count);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write negative bytes");
        }

        return checked(Position + count) > length
            ? throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write beyond end of SubStream")
            : Parent.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (checked(Position + buffer.Length) > length)
        {
            throw new ArgumentOutOfRangeException(nameof(buffer), "Attempt to write beyond end of SubStream");
        }

        Parent.Write(buffer);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => checked(Position + buffer.Length) > length
        ? throw new ArgumentOutOfRangeException(nameof(buffer), "Attempt to write beyond end of SubStream")
        : Parent.WriteAsync(buffer, cancellationToken);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write negative bytes");
        }

        return checked(Position + count) > length
            ? throw new ArgumentOutOfRangeException(nameof(count), "Attempt to write beyond end of SubStream")
            : Parent.BeginWrite(buffer, offset, count, callback, state);
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                if (OwnsParent)
                {
                    Parent.Dispose();
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
