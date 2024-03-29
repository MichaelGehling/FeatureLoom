﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Time
{
    public static class TimeSpanExtensions
    {
        public static TimeSpan Years(this int years)
        {
            return TimeSpan.FromTicks((long)years * 10_000 * 1000 * 60 * 60 * 24 * 365);
        }

        public static TimeSpan Years(this long years)
        {
            return TimeSpan.FromTicks(years * 10_000 * 1000 * 60 * 60 * 24 * 365);
        }

        public static TimeSpan Years(this double years)
        {
            return TimeSpan.FromTicks((long)(years * 10_000 * 1000 * 60 * 60 * 24 * 365));
        }

        public static TimeSpan Days(this int days)
        {
            return TimeSpan.FromTicks((long)days * 10_000 * 1000 * 60 * 60 * 24);
        }

        public static TimeSpan Days(this long days)
        {
            return TimeSpan.FromTicks(days * 10_000 * 1000 * 60 * 60 * 24);
        }

        public static TimeSpan Days(this double days)
        {
            return TimeSpan.FromTicks((long)(days * 10_000 * 1000 * 60 * 60 * 24));
        }

        public static TimeSpan Hours(this int hours)
        {            
            return TimeSpan.FromTicks((long)hours * 10_000 * 1000 * 60 * 60);
        }

        public static TimeSpan Hours(this long hours)
        {
            return TimeSpan.FromTicks(hours * 10_000 * 1000 * 60 * 60);
        }

        public static TimeSpan Minutes(this int minutes)
        {            
            return TimeSpan.FromTicks((long)minutes * 10_000 * 1000 * 60);
        }

        public static TimeSpan Minutes(this long minutes)
        {
            return TimeSpan.FromTicks(minutes * 10_000 * 1000 * 60);
        }

        public static TimeSpan Seconds(this int seconds)
        {            
            return TimeSpan.FromTicks((long)seconds * 10_000 * 1000);
        }

        public static TimeSpan Seconds(this long seconds)
        {
            return TimeSpan.FromTicks(seconds * 10_000 * 1000);
        }

        public static TimeSpan Milliseconds(this int milliseconds)
        {
            return TimeSpan.FromTicks((long)milliseconds * 10_000);
        }

        public static TimeSpan Milliseconds(this long milliseconds)
        {
            return TimeSpan.FromTicks(milliseconds * 10_000);
        }

        public static TimeSpan Ticks(this long ticks)
        {
            return TimeSpan.FromTicks(ticks);
        }

        public static TimeSpan Hours(this double hours)
        {
            return TimeSpan.FromTicks((long)(hours * 10_000 * 1000 * 60 * 60));
        }

        public static TimeSpan Minutes(this double minutes)
        {
            return TimeSpan.FromTicks((long)(minutes * 10_000 * 1000 * 60));
        }

        public static TimeSpan Seconds(this double seconds)
        {
            return TimeSpan.FromTicks((long)(seconds * 10_000 * 1000));
        }

        public static TimeSpan Milliseconds(this double milliseconds)
        {
            return TimeSpan.FromTicks((long)(milliseconds * 10_000));
        }

        public static TimeSpan Multiply(this TimeSpan timespan, int factor)
        {
            return TimeSpan.FromTicks(timespan.Ticks * factor);
        }

        public static TimeSpan Multiply(this TimeSpan timespan, double factor)
        {
            return TimeSpan.FromTicks((long)(timespan.Ticks * factor));
        }

        public static bool Matches(this TimeSpan time, TimeSpan compTime, TimeSpan tolerance)
        {
            return (time - compTime).Duration() < tolerance;
        }

        public static TimeSpan Divide(this TimeSpan time, int divisor)
        {
            return new TimeSpan(time.Ticks / divisor);
        }

        public static TimeSpan Divide(this TimeSpan time, double divisor)
        {
            return new TimeSpan((long)(time.Ticks / divisor));            
        }

        public static void Wait(this TimeSpan waitTime) => AppTime.Wait(waitTime, waitTime);
        public static void Wait(this TimeSpan waitTime, CancellationToken ct) => AppTime.Wait(waitTime, waitTime, ct);
        public static void Wait(this TimeSpan waitTime, TimeSpan tolerance) => AppTime.Wait(waitTime, waitTime + tolerance);
        public static void Wait(this TimeSpan waitTime, TimeSpan tolerance, CancellationToken ct) => AppTime.Wait(waitTime, waitTime + tolerance, ct);

        public static Task WaitAsync(this TimeSpan waitTime) => AppTime.WaitAsync(waitTime, waitTime);
        public static Task WaitAsync(this TimeSpan waitTime, CancellationToken ct) => AppTime.WaitAsync(waitTime, waitTime, ct);
        public static Task WaitAsync(this TimeSpan waitTime, TimeSpan tolerance) => AppTime.WaitAsync(waitTime, waitTime + tolerance);
        public static Task WaitAsync(this TimeSpan waitTime, TimeSpan tolerance, CancellationToken ct) => AppTime.WaitAsync(waitTime, waitTime + tolerance, ct);

    }
}