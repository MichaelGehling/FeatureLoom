using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Time
{
    public static class DateTimeExtensions
    {
                
        public static long ToUnixTimeInMilliseconds(this DateTime dateTime, bool clampZero = false)
        {
            long result = ((dateTime - AppTime.UnixTimeBase).Ticks / 10_000);
            if (clampZero) result = result.ClampLow(0);
            return result;

        }

        public static double ToUnixTimeInSeconds(this DateTime dateTime, bool clampZero = false)
        {
            double result = ((double)(dateTime - AppTime.UnixTimeBase).Ticks) / (10_000 * 1_000);
            return result;
        }

        public static DateTime TheEarlierOne(this DateTime self, DateTime other) => self < other ? self : other;
        public static DateTime TheLaterOne(this DateTime self, DateTime other) => self > other ? self : other;

    }
}
