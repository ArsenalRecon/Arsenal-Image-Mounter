//  
//  Copyright (c) 2012-2022, Arsenal Consulting, Inc. (d/b/a Arsenal Recon) <http://www.ArsenalRecon.com>
//  This source code and API are available under the terms of the Affero General Public
//  License v3.
// 
//  Please see LICENSE.txt for full license terms, including the availability of
//  proprietary exceptions.
//  Questions, comments, or requests for clarification: http://ArsenalRecon.com/contact/
// 

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Diagnostics;

namespace Arsenal.ImageMounter.IO.Streams;

public class CompletionPosition
{
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();

    public CompletionPosition(long totalLength)
    {
        LengthTotal = totalLength;
    }

    public virtual long LengthComplete { get; set; }

    public virtual long LengthTotal { get; set; }

    public virtual double PercentComplete => 100d * LengthComplete / LengthTotal;

    public virtual TimeSpan ElapsedTime => stopwatch.Elapsed;

    public virtual TimeSpan? EstimatedTimeRemaining
    {
        get
        {
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
}