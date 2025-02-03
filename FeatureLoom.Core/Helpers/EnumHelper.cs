using FeatureLoom.Synchronization;
using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Linq;

namespace FeatureLoom.Helpers
{

    /// <summary>
    /// Improves performance for conversions of enum values.
    /// WARNING: Conversion to and from Int will only work correctly with enums that are based on
    /// int, short, ushort, byte or sbyte. uint, long and ulong will provide wrong values, because 
    /// for the conversion to the int value the GetHashCode method is used.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class EnumHelper<T> where T:struct, Enum
    {
        class ListDictionary<X> : IReadOnlyDictionary<int, X>
        {
            List<X> list;
            int[] keys;

            public ListDictionary(IEnumerable<X> values)
            {
                list = new List<X>(values);
                keys = Enumerable.Range(0, list.Count).ToArray();
            }

            public X this[int key] => list[key];

            public IEnumerable<int> Keys => keys;

            public IEnumerable<X> Values => list;

            public int Count => list.Count;

            public bool ContainsKey(int key)
            {
                return keys.Contains(key);
            }

            public IEnumerator<KeyValuePair<int, X>> GetEnumerator()
            {
                return keys.Select(k => new KeyValuePair<int, X>(k, list[k])).GetEnumerator();
            }

            public bool TryGetValue(int key, out X value)
            {
                value = default;
                if (key >= list.Count) return false;
                value = list[key];
                return true;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }            
        }


        static IReadOnlyDictionary<int, string> intToName;
        static IReadOnlyDictionary<int, T> intToEnum = new Dictionary<int, T>();
        static Func<T, int> convertToInt = EqualityComparer<T>.Default.GetHashCode;
        static MicroValueLock initLock = new MicroValueLock();
        static volatile bool initialized = false;        

        private static void Init()
        {
            if (initialized) return;

            initLock.Enter();
            try
            {
                if (initialized) return;

                Type underlyingType = Enum.GetUnderlyingType(typeof(T));
                var acceptedTypes = new[] { typeof(int), typeof(short), typeof(ushort), typeof(byte), typeof(sbyte) };
                if (!acceptedTypes.Contains(underlyingType)) throw new NotSupportedException($"EnumHelper cannot used with enum type {typeof(T)}, because only int32 based types and compatible ones are supported.");

                int count = 0;
                bool sequentialKeys = true;
                var values = Enum.GetValues(typeof(T));
                var intToNameDict = new Dictionary<int, string>();
                var intToEnumDict = new Dictionary<int, T>();
                foreach (var value in values)
                {                    
                    T enumValue = (T)value;
                    int intValue = convertToInt(enumValue);
                    string name = value.ToString();
                    intToNameDict[intValue] = name;
                    intToEnumDict[intValue] = enumValue;
                    sequentialKeys &= intValue == count++;
                }
                if (sequentialKeys)
                {
                    intToName = new ListDictionary<string>(intToNameDict.Values);
                    intToEnum = new ListDictionary<T>(intToEnumDict.Values);
                }
                else
                {
                    intToName = intToNameDict;
                    intToEnum = intToEnumDict;
                }

                initialized = true;
            }
            finally
            {
                initLock.Exit();
            }
        }

        public static int Compare(T left, T right)
        {
            return Comparer<T>.Default.Compare(left, right);
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
