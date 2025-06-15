using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Helpers
{

    /// <summary>
    /// Improves performance for conversions of enum values.
    /// WARNING: Conversion to and from Int will only work correctly with enums that are based on
    /// int, short, ushort, byte or sbyte. uint, long and ulong will provide wrong values, 
    /// because int is used as the underlying type for the conversions.
    /// </summary>
    /// <typeparam name="T">Enum type to be handled.</typeparam>
    public static class EnumHelper<T> where T:struct, Enum
    {
        /// <summary>
        /// Optimized dictionary for sequential integer keys (0..N-1).
        /// Used to improve lookup performance and memory usage for sequential enums.
        /// </summary>
        /// <typeparam name="X">Value type.</typeparam>
        class ListDictionary<X> : IReadOnlyDictionary<int, X>
        {
            private readonly List<X> list;
            private readonly int[] keys;
            private readonly int offset;

            public ListDictionary(IEnumerable<X> values, int offset)
            {
                list = new List<X>(values);
                this.offset = offset;
                keys = Enumerable.Range(offset, list.Count).ToArray();
            }

            public X this[int key] => list[key - offset];

            public IEnumerable<int> Keys => keys;

            public IEnumerable<X> Values => list;

            public int Count => list.Count;

            public bool ContainsKey(int key)
            {
                return key >= offset && key < offset + list.Count;
            }

            public IEnumerator<KeyValuePair<int, X>> GetEnumerator()
            {
                for (int i = 0; i < list.Count; i++)
                {
                    yield return new KeyValuePair<int, X>(i + offset, list[i]);
                }
            }

            public bool TryGetValue(int key, out X value)
            {
                int idx = key - offset;
                if (idx >= 0 && idx < list.Count)
                {
                    value = list[idx];
                    return true;
                }
                value = default;
                return false;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }            
        }

        // Caches for enum value-to-name and value-to-enum lookups
        static volatile IReadOnlyDictionary<int, string> intToName;
        static volatile IReadOnlyDictionary<int, T> intToEnum = new Dictionary<int, T>();

        // Delegate for fast, boxing-free enum-to-int conversion
        static Func<T, int> convertToInt = _ => 0;

        // Lock and flag for thread-safe, one-time initialization
        static MicroValueLock initLock = new MicroValueLock();
        static volatile bool initialized = false;        

        /// <summary>
        /// Initializes lookup tables and conversion delegate for the enum type.
        /// Ensures thread safety and only runs once per enum type.
        /// </summary>
        private static void Init()
        {
            if (initialized) return;

            initLock.Enter();
            try
            {
                if (initialized) return;

                // Only allow enums with int, short, ushort, byte, or sbyte as underlying type
                Type underlyingType = Enum.GetUnderlyingType(typeof(T));
                var acceptedTypes = new[] { typeof(int), typeof(short), typeof(ushort), typeof(byte), typeof(sbyte) };
                if (!acceptedTypes.Contains(underlyingType)) throw new NotSupportedException($"EnumHelper cannot used with enum type {typeof(T)}, because only int32 based types and compatible ones are supported.");
                
                // Assign the most efficient conversion delegate for the enum's underlying type
                if (underlyingType == typeof(int))  convertToInt = e => Unsafe.As<T, int>(ref e);
                else if (underlyingType == typeof(byte)) convertToInt = e => Unsafe.As<T, byte>(ref e);
                else if (underlyingType == typeof(sbyte)) convertToInt = e => Unsafe.As<T, sbyte>(ref e);
                else if (underlyingType == typeof(short)) convertToInt = e => Unsafe.As<T, short>(ref e);
                else if (underlyingType == typeof(ushort)) convertToInt = e => Unsafe.As<T, ushort>(ref e);
                else throw new NotSupportedException($"EnumHelper cannot be used with enum type {typeof(T)}, because only int32-based types and compatible ones are supported.");

                // Build lookup tables for enum value <-> name and value <-> enum
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
                }

                // Check if keys are sequential (contiguous) regardless of starting value
                var keys = intToNameDict.Keys.OrderBy(k => k).ToArray();
                bool sequentialKeys = keys.Length > 0 && keys.Last() - keys.First() + 1 == keys.Length;
                for (int i = 1; sequentialKeys && i < keys.Length; i++)
                {
                    if (keys[i] != keys[i - 1] + 1) sequentialKeys = false;
                }

                if (sequentialKeys)
                {
                    int minKey = keys.First();
                    intToName = new ListDictionary<string>(keys.Select(k => intToNameDict[k]), minKey);
                    intToEnum = new ListDictionary<T>(keys.Select(k => intToEnumDict[k]), minKey);
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

        /// <summary>
        /// Compares two enum values using the default comparer.
        /// </summary>
        public static int Compare(T left, T right)
        {
            return Comparer<T>.Default.Compare(left, right);
        }

        /// <summary>
        /// Gets the name of the enum value.
        /// </summary>
        public static string ToName (T enumValue)
        {
            if (!initialized) Init();
            int intValue = convertToInt(enumValue);
            return intToName[intValue];
        }

        /// <summary>
        /// Converts the enum value to its underlying integer value.
        /// </summary>
        public static int ToInt(T enumValue)
        {
            if (!initialized) Init();
            int intValue = convertToInt(enumValue);
            return intValue;            
        }

        /// <summary>
        /// Tries to parse an enum value from a string (case-insensitive).
        /// </summary>
        public static bool TryFromString(string enumString, out T enumValue)
        {
            if (!initialized) Init();
            return Enum.TryParse(enumString, true, out enumValue);
        }

        /// <summary>
        /// Tries to get the enum value from its integer representation.
        /// </summary>
        public static bool TryFromInt(int intValue, out T enumValue)
        {
            if (!initialized) Init();
            return intToEnum.TryGetValue(intValue, out enumValue);
        }

        /// <summary>
        /// Checks if the specified flag is set in the enum value.
        /// </summary>
        public static bool IsFlagSet(T enumValue, T enumFlag)
        {
            if (!initialized) Init();
            int intValue = convertToInt(enumValue);
            int intFlag = convertToInt(enumFlag);
            return (intValue & intFlag) != 0;
        }

    }

    /// <summary>
    /// Non-generic helper for enum operations, inferring the enum type from usage.
    /// </summary>
    public static class EnumHelper
    {
        /// <summary>
        /// Gets the name of the enum value.
        /// </summary>
        public static string ToName<T>(T enumValue) where T : struct, Enum => EnumHelper<T>.ToName(enumValue);

        /// <summary>
        /// Converts the enum value to its underlying integer value.
        /// </summary>
        public static int ToInt<T>(T enumValue) where T : struct, Enum => EnumHelper<T>.ToInt(enumValue);

        /// <summary>
        /// Tries to parse an enum value from a string (case-insensitive).
        /// </summary>
        public static bool TryFromString<T>(string enumString, out T enumValue) where T : struct, Enum => EnumHelper<T>.TryFromString(enumString, out enumValue);

        /// <summary>
        /// Tries to get the enum value from its integer representation.
        /// </summary>
        public static bool TryFromInt<T>(int intValue, out T enumValue) where T : struct, Enum => EnumHelper<T>.TryFromInt(intValue, out enumValue);

        /// <summary>
        /// Checks if the specified flag is set in the enum value.
        /// </summary>
        public static bool IsFlagSet<T>(T enumValue, T enumFlag) where T : struct, Enum => EnumHelper<T>.IsFlagSet(enumValue, enumFlag);
        
    }

}
