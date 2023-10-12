using LTRData.Extensions.Buffers;
using LTRData.Extensions.Formatting;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Arsenal.ImageMounter.Tests;

public class Collections
{
    [Fact]
    public void SingValueTest()
    {
        var enumerable = SingleValueEnumerable.Get(2).ToArray();

        Assert.Single(enumerable);
        Assert.Equal(2, enumerable[0]);
    }

    [Fact]
    public void ToHexStringTest()
    {
        var span = "\r\n"u8;

        var spanHex = span.ToHexString();
        Assert.Equal("0d0a", spanHex);

        var array = span.ToArray();
        var arrayHex = array.ToHexString();
        Assert.Equal("0d0a", arrayHex);

        var enumerable = (IReadOnlyCollection<byte>)array;
        var enumerableHex = enumerable.ToHexString();
        Assert.Equal("0d0a", enumerableHex);
    }

    [Fact]
    public void TryFormatHexStringTest()
    {
        var span = "\r\n"u8;

        Span<char> spanHex = stackalloc char[4];
        var result = span.TryFormatHexString(default, spanHex, upperCase: false);
        Assert.True(result);
        Assert.Equal("0d0a", spanHex.ToString());

        Span<char> tooSmallSpanHex = stackalloc char[1];
        result = span.TryFormatHexString(default, tooSmallSpanHex, upperCase: false);
        Assert.False(result);
    }

}
