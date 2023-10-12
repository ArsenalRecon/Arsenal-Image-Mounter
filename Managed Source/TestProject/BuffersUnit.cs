using Arsenal.ImageMounter.Extensions;
using LTRData.Extensions.Split;
using System;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace Arsenal.ImageMounter.Tests;

public class BuffersUnit
{
#if NET6_0_OR_GREATER
    [Fact]
    public void TestSpanSplit1()
    {
        var str = "  123 ; 456 ,789; 012  ";

        var i = 0;

        foreach (var result in str.AsSpan().Split(',', StringSplitOptions.TrimEntries))
        {
            Trace.WriteLine($"'{result}'");

            foreach (var inner in result.Split(';', StringSplitOptions.TrimEntries))
            {
                i++;

                Trace.WriteLine($"'{inner}'");

                Assert.Equal(3, inner.Length);
            }
        }

        Assert.Equal(4, i);
    }

    [Fact]
    public void TestSpanSplit2()
    {
        var str = "  123 xx 456 yyyy789xx 012  ";

        var i = 0;

        foreach (var result in str.AsSpan().Split("yyyy", StringSplitOptions.TrimEntries))
        {
            Trace.WriteLine($"'{result}'");

            foreach (var inner in result.Split("xx", StringSplitOptions.TrimEntries))
            {
                i++;

                Trace.WriteLine($"'{inner}'");

                Assert.Equal(3, inner.Length);
            }
        }

        Assert.Equal(4, i);
    }

    [Fact]
    public void TestSpanReversSplit1()
    {
        var str = "  123 ; 456 ,789; 012  ";

        var i = 0;

        foreach (var outer in str.AsSpan().SplitReverse(',', StringSplitOptions.TrimEntries))
        {
            Trace.WriteLine($"'{outer}'");

            foreach (var inner in outer.SplitReverse(';', StringSplitOptions.TrimEntries))
            {
                i++;

                Trace.WriteLine($"'{inner}'");

                Assert.Equal(3, inner.Length);
            }
        }

        Assert.Equal(4, i);

        var result = str.AsSpan().SplitReverse(',', StringSplitOptions.TrimEntries).Last().SplitReverse(';', StringSplitOptions.TrimEntries).First();

        Assert.Equal("456", result.ToString());
    }

    [Fact]
    public void TestSpanReverseSplit2()
    {
        var str = "  123 xx 456 yyyy789xx 012  ";

        var i = 0;

        foreach (var outer in str.AsSpan().SplitReverse("yyyy", StringSplitOptions.TrimEntries))
        {
            Trace.WriteLine($"'{outer}'");

            foreach (var inner in outer.SplitReverse("xx", StringSplitOptions.TrimEntries))
            {
                i++;

                Trace.WriteLine($"'{inner}'");

                Assert.Equal(3, inner.Length);
            }
        }

        Assert.Equal(4, i);

        var result = str.AsSpan().Split("yyyy", StringSplitOptions.TrimEntries).Last().Split("xx", StringSplitOptions.TrimEntries).First();

        Assert.Equal("789", result.ToString());
    }

    [Fact]
    public void Test1()
    {
        var str = "123;456 789;012";

        var result = str.AsMemory().Split(' ', StringSplitOptions.TrimEntries).Last().Split(';', StringSplitOptions.TrimEntries).First();

        Assert.Equal("789", result.ToString());
    }

    [Fact]
    public void TestReverse1()
    {
        var str = "123;456 789;012";

        var result = str.AsMemory().SplitReverse(' ', StringSplitOptions.TrimEntries).Last().SplitReverse(';', StringSplitOptions.TrimEntries).First();

        Assert.Equal("456", result.ToString());
    }

    [Fact]
    public void TestStrSep1()
    {
        var str = "123xxx456---789xxx012";

        var result = str.AsMemory().Split("---".AsMemory(), StringSplitOptions.TrimEntries).Last().Split("xxx".AsMemory(), StringSplitOptions.TrimEntries).First();

        Assert.Equal("789", result.ToString());
    }

    [Fact]
    public void TestReverseStrSep1()
    {
        var str = "123xxx456---789xxx012";

        var result = str.AsMemory().SplitReverse("---".AsMemory(), StringSplitOptions.TrimEntries).First().SplitReverse("xxx".AsMemory(), StringSplitOptions.TrimEntries).Last();

        Assert.Equal("789", result.ToString());
    }
#endif
}
