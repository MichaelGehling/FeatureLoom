using FeatureLoom.Helpers;
using System;

namespace FeatureLoom.Extensions
{
    public static class EnumExtensions
    {
        public static string ToName<T>(this T enumValue) where T : struct, Enum => EnumHelper<T>.ToName(enumValue);
        public static int ToInt<T>(this T enumValue) where T : struct, Enum => EnumHelper<T>.ToInt(enumValue);

        public static bool IsFlagSet<T>(this T enumValue, T enumFlag) where T : struct, Enum => EnumHelper<T>.IsFlagSet(enumValue, enumFlag);

        public static T ToEnum<T>(this string value) where T : Enum
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }

        public static T ToEnum<T>(this string value, T returnIfFailed) where T : struct, Enum
        {
            if (Enum.TryParse<T>(value, true, out T result))
            {
                return result;
            }
            else return returnIfFailed;
        }

        public static T? ToNullableEnum<T>(this string value) where T : struct, Enum
        {
            return (T?)Enum.Parse(typeof(T), value, true);
        }

        public static T? ToNullableEnum<T>(this string value, T? returnIfFailed) where T : struct, Enum
        {
            if (!typeof(T).IsEnum) throw new Exception("This method is only for Enums");

            if (Enum.TryParse<T>(value, true, out T result))
            {
                return result;
            }
            else return returnIfFailed;
        }

        public static int CompareTo<T>(this T left, T right) where T : struct, Enum => EnumHelper<T>.Compare(left, right);

        public static bool EqualTo<T>(this T left, T right) where T : struct, Enum => EnumHelper<T>.Compare(left, right) == 0;
        public static bool GreaterThan<T>(this T left, T right) where T : struct, Enum => EnumHelper<T>.Compare(left, right) > 0;
        public static bool LessThan<T>(this T left, T right) where T : struct, Enum => EnumHelper<T>.Compare(left, right) < 0;
        public static bool GreaterThanOrEqualTo<T>(this T left, T right) where T : struct, Enum => EnumHelper<T>.Compare(left, right) >= 0;
        public static bool LessThanOrEqualTo<T>(this T left, T right) where T : struct, Enum => EnumHelper<T>.Compare(left, right) <= 0;
    }
}