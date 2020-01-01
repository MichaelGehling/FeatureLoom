using System;

namespace FeatureFlowFramework.Helper
{
    public static class EnumExtensions
    {
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
    }
}