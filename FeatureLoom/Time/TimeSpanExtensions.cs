using System;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Time
{
    public static class TimeSpanExtensions
    {
        public static TimeSpan Hours(this int hours)
        {            
            return TimeSpan.FromTicks((long)hours * 10_000 * 1000 * 60 * 60);
        }

        public static TimeSpan Minutes(this int minutes)
        {            
            return TimeSpan.FromTicks((long)minutes * 10_000 * 1000 * 60);
        }

        public static TimeSpan Seconds(this int seconds)
        {            
            return TimeSpan.FromTicks((long)seconds * 10_000 * 1000);
        }

        public static TimeSpan Milliseconds(this int milliseconds)
        {
            return TimeSpan.FromTicks((long)milliseconds * 10_000);
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

    }
}