//  
//  Copyright (c) 2012-2026, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 



using System;
using System.Diagnostics;
using System.Threading;

namespace Arsenal.ImageMounter.IO.Streams;

public class CompletionPosition(long totalLength)
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    
    private long lengthComplete;

    public virtual long LengthComplete
    {
        get => lengthComplete;
        set => lengthComplete = value;
    }

    public bool UnreliablePosition { get; set; }

    public virtual long LengthTotal { get; set; } = totalLength;

    public virtual double PercentComplete => 100d * LengthComplete / LengthTotal;

    public virtual TimeSpan ElapsedTime => stopwatch.Elapsed;

    public virtual TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            if (UnreliablePosition)
            {
                return null;
            }

            if (LengthComplete >= LengthTotal)
            {
                return TimeSpan.Zero;
            }

            var elapsedTicks = stopwatch.ElapsedMilliseconds;

            var totalTicks = elapsedTicks / ((double)LengthComplete / LengthTotal);

            var ticksLeft = totalTicks - elapsedTicks;

            if (double.IsInfinity(ticksLeft) || double.IsNaN(ticksLeft))
            {
                return null;
            }

            return TimeSpan.FromMilliseconds(ticksLeft);
        }
    }

    public virtual void Reset()
    {
        LengthComplete = 0;

        stopwatch.Reset();
        stopwatch.Start();
    }

    public void InterlockedIncrement()
        => Interlocked.Increment(ref lengthComplete);
}