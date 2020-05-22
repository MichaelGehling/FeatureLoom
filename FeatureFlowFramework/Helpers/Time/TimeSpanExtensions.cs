using System;

namespace FeatureFlowFramework.Helpers.Time
{
    public static class TimeSpanExtensions
    {
        public static TimeSpan Hours(this int seconds)
        {
            return TimeSpan.FromHours(seconds);
        }

        public static TimeSpan Minutes(this int seconds)
        {
            return TimeSpan.FromMinutes(seconds);
        }

        public static TimeSpan Seconds(this int seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        public static TimeSpan Milliseconds(this int seconds)
        {
            return TimeSpan.FromMilliseconds(seconds);
        }

        public static TimeSpan Hours(this double seconds)
        {
            return TimeSpan.FromHours(seconds);
        }

        public static TimeSpan Minutes(this double seconds)
        {
            return TimeSpan.FromMinutes(seconds);
        }

        public static TimeSpan Seconds(this double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        public static TimeSpan Milliseconds(this double seconds)
        {
            return TimeSpan.FromMilliseconds(seconds);
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
    }
}