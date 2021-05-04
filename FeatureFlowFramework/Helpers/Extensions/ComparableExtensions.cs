using System;

namespace FeatureLoom.Helpers.Extensions
{
    public static class ComparableExtensions
    {
        public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T>
        {
            if(value.CompareTo(min) <= 0) return min;
            if(value.CompareTo(max) >= 0) return max;
            return value;
        }

        public static T ClampLow<T>(this T value, T min) where T : IComparable<T>
        {
            if(value.CompareTo(min) <= 0) return min;
            return value;
        }

        public static T ClampHigh<T>(this T value, T max) where T : IComparable<T>
        {
            if(value.CompareTo(max) >= 0) return max;
            return value;
        }

    }
}