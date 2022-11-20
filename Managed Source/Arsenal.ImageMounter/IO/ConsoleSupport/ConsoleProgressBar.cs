using System;
using System.Text;
using System.Threading;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Arsenal.ImageMounter.IO.ConsoleSupport;

public class ConsoleProgressBar : IDisposable
{
    public Timer Timer { get; }

    public int CurrentValue { get; private set; }

    private readonly Func<double> updateFunc;

    private ConsoleProgressBar(Func<double> update)
    {
        Timer = new(Tick);
        updateFunc = update;
        CreateConsoleProgressBar();
    }

    public ConsoleProgressBar(int dueTime, int period, Func<double> update)
        : this(update)
    {
        Timer = new(Tick);
        Timer.Change(dueTime, period);
    }

    public ConsoleProgressBar(TimeSpan dueTime, TimeSpan period, Func<double> update)
        : this(update)
    {
        Timer = new(Tick);
        Timer.Change(dueTime, period);
    }

    private void Tick(object? o)
    {

        var newvalue = updateFunc();

        if (newvalue != 1d && (int)Math.Round(100d * newvalue) == CurrentValue)
        {
            return;
        }

        UpdateConsoleProgressBar(newvalue);

    }

    public static void CreateConsoleProgressBar()
    {

        if (Console.IsOutputRedirected)
        {
            return;
        }

        var row = new StringBuilder(Console.WindowWidth);

        row.Append('[');

        row.Append('.', Math.Max(Console.WindowWidth - 3, 0));

        row.Append("]\r");

        lock (ConsoleSupport.ConsoleSync)
        {
            Console.ForegroundColor = ConsoleProgressBarColor;

            Console.Write(row.ToString());

            Console.ResetColor();
        }
    }

    public static void UpdateConsoleProgressBar(double value)
    {

        if (Console.IsOutputRedirected)
        {
            return;
        }

        if (value > 1d)
        {
            value = 1d;
        }
        else if (value < 0d)
        {
            value = 0d;
        }

        var currentPos = (int)Math.Round((Console.WindowWidth - 3) * value);

        var row = new StringBuilder(Console.WindowWidth);

        row.Append('[');

        row.Append('=', Math.Max(currentPos, 0));

        row.Append('.', Math.Max(Console.WindowWidth - 3 - currentPos, 0));

        var percent = $" {100d * value:0} % ";

        var midpos = Console.WindowWidth - 3 - percent.Length >> 1;

        if (midpos > 0 && row.Length >= percent.Length)
        {

            row.Remove(midpos, percent.Length);

            row.Insert(midpos, percent);

        }

        row.Append("]\r");

        lock (ConsoleSupport.ConsoleSync)
        {
            Console.ForegroundColor = ConsoleProgressBarColor;

            Console.Write(row.ToString());

            Console.ResetColor();
        }
    }

    public static void FinishConsoleProgressBar()
    {
        UpdateConsoleProgressBar(1d);

        Console.WriteLine();
    }

    public static ConsoleColor ConsoleProgressBarColor { get; set; } = ConsoleColor.Cyan;

    private bool disposedValue; // To detect redundant calls

    // IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                Timer.Dispose();
                FinishConsoleProgressBar();

            }

            // TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.

            // TODO: set large fields to null.
        }

        disposedValue = true;
    }

    // TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
    ~ConsoleProgressBar()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(false);
    }

    // This code added by Visual Basic to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        Dispose(true);
        // TODO: uncomment the following line if Finalize() is overridden above.
        GC.SuppressFinalize(this);
    }
}