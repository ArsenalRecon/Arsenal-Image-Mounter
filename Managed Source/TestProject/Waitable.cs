using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using Arsenal.ImageMounter.Extensions;

namespace Arsenal.ImageMounter.Tests;

public class Waitable
{
    [Fact]
    public async Task RunProcess()
    {
        using var process = new Process
        {
            EnableRaisingEvents = true
        };

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.Arguments = "/c exit 20";

        process.Start();

        var result = await process.WaitForResultAsync();

        Assert.Equal(20, result);
    }

    [Fact]
    public async Task Event()
    {
        using var evt = new ManualResetEvent(initialState: false);

        var result = await evt.WaitAsync(1000);

        Assert.False(result);

        evt.Set();

        result = await evt.WaitAsync();

        Assert.True(result);

        evt.Reset();

        ThreadPool.QueueUserWorkItem(_ => { evt.Set(); });

        result = await evt.WaitAsync();
    }

    [Fact]
    public async Task EventSlim()
    {
        using var evt = new ManualResetEventSlim();

        var result = await evt.WaitHandle.WaitAsync(1000);

        Assert.False(result);

        evt.Set();

        result = await evt.WaitHandle.WaitAsync();

        Assert.True(result);

        evt.Reset();

        ThreadPool.QueueUserWorkItem(_ => { evt.Set(); });

        result = await evt.WaitHandle.WaitAsync();
    }
}
