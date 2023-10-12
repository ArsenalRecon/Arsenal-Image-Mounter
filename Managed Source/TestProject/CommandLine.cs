using Arsenal.ImageMounter.IO.ConsoleIO;
using System;
using Xunit;

namespace Arsenal.ImageMounter.Tests;

public class CommandLine
{
    [Fact]
    public void Test1()
    {
        var args = new[] {
            "--switch1=arg1",
            "--switch2=arg with spaces2",
            "--switch3=arg3first",
            "--switch3=arg3another",
            "-S",
            "-h",
            "parameter1",
            "parameter with spaces2",
            "parameter2"
        };

        var cmd = ConsoleSupport.ParseCommandLine(args, StringComparer.Ordinal);

        Assert.Equal(6, cmd.Count);
        Assert.Equal(3, cmd[""].Length);
        Assert.Equal(2, cmd["switch3"].Length);
        Assert.Equal("arg with spaces2", cmd["switch2"][0]);
        Assert.Equal("parameter with spaces2", cmd[""][1]);
    }

    [WindowsOnlyFact]
    public void Test2Windows()
    {
        var args = new[] {
            "/switch1=arg1",
            "/switch2=arg with spaces2",
            "--switch3=arg3first",
            "--switch3=arg3another",
            "-S",
            "-h",
            "parameter1",
            "parameter with spaces2",
            "parameter2"
        };

        var cmd = ConsoleSupport.ParseCommandLine(args, StringComparer.Ordinal);

        Assert.Equal(6, cmd.Count);
        Assert.Equal(3, cmd[""].Length);
        Assert.Equal(2, cmd["switch3"].Length);
        Assert.Equal("arg with spaces2", cmd["switch2"][0]);
        Assert.Equal("parameter with spaces2", cmd[""][1]);
    }

    [NotOnWindowsFact]
    public void Test2Unix()
    {
        var args = new[] {
            "/mnt/New folder/New Bitmap Image.bmp"
        };

        var cmd = ConsoleSupport.ParseCommandLine(args, StringComparer.Ordinal);

        Assert.Equal(args[0], cmd[""][0]);
    }

}
