using Arsenal.ImageMounter.IO.Streams;
using System;
using System.IO;
using Xunit;

namespace Arsenal.ImageMounter.Tests;

public static class Streams
{
    [Fact]
    public static void Misaligned()
    {
        using var memoryStream = new MemoryStream();
        using var aligningStream = new AligningStream(memoryStream, 0x4000, ownsBaseStream: true) { GrowInterval = 0x4000 };

        var verifyBytes = "TEST0001"u8;

        aligningStream.Position = 0x4000;
        aligningStream.Write(verifyBytes);

        Assert.Equal(0x8000, memoryStream.Length);

        memoryStream.SetLength(0x5000);

        Assert.Equal(0x5000, aligningStream.Length);

        Span<byte> buffer = stackalloc byte[verifyBytes.Length];

        aligningStream.Position = 0x4000;
        Assert.Equal(buffer.Length, aligningStream.Read(buffer));

#if NETCOREAPP
        Assert.Equal(buffer, verifyBytes);
#else
        Assert.Equal(buffer.ToArray(), verifyBytes.ToArray());
#endif

        verifyBytes = "TEST0002"u8;

        aligningStream.Position = 0x4000 - 4;
        aligningStream.Write(verifyBytes);
        memoryStream.SetLength(0x5000);

        Assert.Equal(0x5000, memoryStream.Length);
        Assert.Equal(0x5000, aligningStream.Length);

        aligningStream.Position = 0x4000 - 4;
        Assert.Equal(buffer.Length, aligningStream.Read(buffer));

#if NETCOREAPP
        Assert.Equal(buffer, verifyBytes);
#else
        Assert.Equal(buffer.ToArray(), verifyBytes.ToArray());
#endif
    }

    [Fact]
    public static void AlignedToEndOfStream()
    {
        using var memoryStream = new MemoryStream();
        using var aligningStream = new AligningStream(memoryStream, 0x4000, ownsBaseStream: true) { GrowInterval = 0x4000 };

        var verifyBytes = "TEST0001"u8;

        aligningStream.Position = 0x5000 - verifyBytes.Length;
        aligningStream.Write(verifyBytes);

        Assert.Equal(0x8000, memoryStream.Length);

        memoryStream.SetLength(0x5000);

        Assert.Equal(0x5000, aligningStream.Length);

        Span<byte> buffer = stackalloc byte[4];

        aligningStream.Position = 0x5000 - buffer.Length;
        Assert.Equal(buffer.Length, aligningStream.Read(buffer));

#if NETCOREAPP
        Assert.Equal(buffer, verifyBytes[4..]);
#else
        Assert.Equal(buffer.ToArray(), verifyBytes.Slice(4).ToArray());
#endif

        aligningStream.Position = 0;
        Assert.Equal(buffer.Length, aligningStream.Read(buffer));

#if NETCOREAPP
        Assert.Equal(buffer, "\0\0\0\0"u8);
#else
        Assert.Equal(buffer.ToArray(), new byte[4]);
#endif

        aligningStream.Position = 0x5000 - buffer.Length;
        Assert.Equal(buffer.Length, aligningStream.Read(buffer));

#if NETCOREAPP
        Assert.Equal(buffer, verifyBytes[4..]);
#else
        Assert.Equal(buffer.ToArray(), verifyBytes.Slice(4).ToArray());
#endif
    }

    [Fact]
    public static void MisalignedToEndOfStream()
    {
        using var memoryStream = new MemoryStream();
        using var aligningStream = new AligningStream(memoryStream, 0x4000, ownsBaseStream: true) { GrowInterval = 0x4000 };

        var verifyBytes = "TEST0001"u8;

        aligningStream.Position = 0x4000;
        aligningStream.Write(verifyBytes);

        Assert.Equal(0x8000, memoryStream.Length);

        memoryStream.SetLength(0x5000);

        Assert.Equal(0x5000, aligningStream.Length);

        var buffer = new byte[0x4000];

        var blank = new byte[0x4000];

        aligningStream.Position = 0x4000;
        Assert.Equal(0x1000, aligningStream.Read(buffer, 0, buffer.Length));

#if NETCOREAPP
        Assert.Equal(buffer.AsSpan(0, verifyBytes.Length), verifyBytes);
#else
        Assert.Equal(buffer.AsSpan(0, verifyBytes.Length).ToArray(), verifyBytes.ToArray());
#endif

        aligningStream.Position = 0x0000;
        Assert.Equal(0x4000, aligningStream.Read(buffer, 0, buffer.Length));

        Assert.Equal(blank, buffer);

        aligningStream.Position = 0x4000;
        Assert.Equal(0x1000, aligningStream.Read(buffer, 0, buffer.Length));

#if NETCOREAPP
        Assert.Equal(buffer.AsSpan(0, verifyBytes.Length), verifyBytes);
#else
        Assert.Equal(buffer.AsSpan(0, verifyBytes.Length).ToArray(), verifyBytes.ToArray());
#endif

        aligningStream.Position = 0x4000;
        Assert.Equal(0x1000, aligningStream.Read(buffer));

#if NETCOREAPP
        Assert.Equal(buffer.AsSpan(0, verifyBytes.Length), verifyBytes);
#else
        Assert.Equal(buffer.AsSpan(0, verifyBytes.Length).ToArray(), verifyBytes.ToArray());
#endif

        aligningStream.Position = 0x0000;
        Assert.Equal(0x4000, aligningStream.Read(buffer));

        Assert.Equal(blank, buffer);

        aligningStream.Position = 0x4000;
        Assert.Equal(0x1000, aligningStream.Read(buffer));

#if NETCOREAPP
        Assert.Equal(buffer.AsSpan(0, verifyBytes.Length), verifyBytes);
#else
        Assert.Equal(buffer.AsSpan(0, verifyBytes.Length).ToArray(), verifyBytes.ToArray());
#endif
    }

}
