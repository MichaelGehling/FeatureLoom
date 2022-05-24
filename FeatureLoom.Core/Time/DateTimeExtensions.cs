using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Time
{
    public static class DateTimeExtensions
    {
        static readonly DateTime unixTimeBase = new DateTime(1970, 1, 1);
        public static long ToUnixTimeInMilliseconds(this DateTime dateTime, bool clampZero = false)
        {
            long result = ((dateTime - unixTimeBase).Ticks / 10_000);
            if (clampZero) result = result.ClampLow(0);
            return result;

        }

        public static int ToUnixTimeInSeconds(this DateTime dateTime, bool clampZero = false)
        {
            int result = (int)((dateTime - unixTimeBase).Ticks / (10_000 * 1_000)).Clamp(clampZero ? 0 : int.MinValue, int.MaxValue);
            return result;
        }

        public static DateTime ToUnixTime(this long milliseconds)
        {
            return unixTimeBase + milliseconds.Milliseconds();
        }

        public static DateTime ToUnixTime(this int seconds)
        {
            return unixTimeBase + seconds.Seconds();
        }

    }
}
