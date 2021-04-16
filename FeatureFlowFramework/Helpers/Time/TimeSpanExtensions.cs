using System;

namespace FeatureFlowFramework.Helpers.Time
{
    public static class TimeSpanExtensions
    {
        public static TimeSpan Hours(this int hours)
        {
            return TimeSpan.FromHours(hours);
        }

        public static TimeSpan Minutes(this int minutes)
        {
            return TimeSpan.FromMinutes(minutes);
        }

        public static TimeSpan Seconds(this int seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        public static TimeSpan Milliseconds(this int milliseconds)
        {
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        public static TimeSpan Ticks(this long ticks)
        {
            return TimeSpan.FromTicks(ticks);
        }

        public static TimeSpan Hours(this double hours)
        {
            long ticks = (long)((hours - (long)hours) * 60 * 60 * 10_000_000) + ((long)(hours) * 60 * 60 * 10_000_000);
            return Ticks(ticks);
        }

        public static TimeSpan Minutes(this double minutes)
        {
            long ticks = (long)((minutes - (long)minutes) * 60 * 10_000_000) + ((long)(minutes) * 60 * 10_000_000);
            return Ticks(ticks);
        }

        public static TimeSpan Seconds(this double seconds)
        {
            long ticks = (long)((seconds - (long)seconds) * 10_000_000) + ((long)(seconds) * 10_000_000);
            return Ticks(ticks);
        }

        public static TimeSpan Milliseconds(this double milliseconds)
        {
            long ticks = (long)((milliseconds - (long)milliseconds) * 10_000) + ((long)(milliseconds) * 10_000);
            return Ticks(ticks);
        }

        public static TimeSpan Multiply(this TimeSpan timespan, int multiplier)
        {
            return TimeSpan.FromTicks(timespan.Ticks * multiplier);
        }

        public static TimeSpan Multiply(this TimeSpan timespan, double multiplier)
        {
            return TimeSpan.FromTicks((long)(timespan.Ticks * multiplier));
        }

        public static bool Matches(this TimeSpan time, TimeSpan compTime, TimeSpan tolerance)
        {
            return (time - compTime).Duration() < tolerance;
        }

        public static TimeSpan divide(this TimeSpan time, int divisor)
        {
            return new TimeSpan(time.Ticks / divisor);
        }

        public static TimeSpan divide(this TimeSpan time, double divisor)
        {
            return new TimeSpan((long)(time.Ticks / divisor));
        }

        public static TimeSpan multiply(this TimeSpan time, int factor)
        {
            return new TimeSpan(time.Ticks * factor);
        }

        public static TimeSpan multiply(this TimeSpan time, double factor)
        {
            return new TimeSpan((long)(time.Ticks * factor));
        }
    }
}