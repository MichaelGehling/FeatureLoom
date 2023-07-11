using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers
{
    public static class EnumHelper<T> where T:struct, Enum
    {
        static Dictionary<int, string> intToName = new Dictionary<int, string>();
        static Dictionary<int, T> intToEnum = new Dictionary<int, T>();
        static Func<T, int> convertToInt = EqualityComparer<T>.Default.GetHashCode;
        static MicroValueLock initLock = new MicroValueLock();
        static bool initialized = false;

        private static void Init()
        {
            if (initialized) return;

            initLock.Enter();
            try
            {
                if (initialized) return;

                Type underlyingType = Enum.GetUnderlyingType(typeof(T));
                if (underlyingType != typeof(int)) throw new Exception($"EnumHelper cannot used with enum type {typeof(T)}, because only int32 based types are supported.");

                var values = Enum.GetValues(typeof(T));
                foreach(var value in values)
                {
                    T enumValue = (T)value;
                    int intValue = convertToInt(enumValue);
                    string name = value.ToString();
                    intToName[intValue] = name;
                    intToEnum[intValue] = enumValue;
                }
                initialized = true;
            }
            finally
            {
                initLock.Exit();
            }
        }

        public static string ToName (T enumValue)
        {
            if (!initialized) Init();
            int intValue = convertToInt(enumValue);
            return intToName[intValue];
        }

        public static int ToInt(T enumValue)
        {
            if (!initialized) Init();
            int intValue = convertToInt(enumValue);
            return intValue;            
        }

        public static bool TryFromString(string enumString, out T enumValue)
        {
            if (!initialized) Init();
            return Enum.TryParse(enumString, true, out enumValue);
        }

        public static bool TryFromInt(int intValue, out T enumValue)
        {
            if (!initialized) Init();
            return intToEnum.TryGetValue(intValue, out enumValue);
        }

        public static bool IsFlagSet(T enumValue, T enumFlag)
        {
            if (!initialized) Init();
            int intValue = convertToInt(enumValue);
            int intFlag = convertToInt(enumFlag);
            return (intValue & intFlag) != 0;
        }

    }

    public static class EnumHelper
    {

        public static string ToName<T>(T enumValue) where T : struct, Enum => EnumHelper<T>.ToName(enumValue);

        public static int ToInt<T>(T enumValue) where T : struct, Enum => EnumHelper<T>.ToInt(enumValue);

        public static bool TryFromString<T>(string enumString, out T enumValue) where T : struct, Enum => EnumHelper<T>.TryFromString(enumString, out enumValue);

        public static bool TryFromInt<T>(int intValue, out T enumValue) where T : struct, Enum => EnumHelper<T>.TryFromInt(intValue, out enumValue);

        public static bool IsFlagSet<T>(T enumValue, T enumFlag) where T : struct, Enum => EnumHelper<T>.IsFlagSet(enumValue, enumFlag);

    }

}
