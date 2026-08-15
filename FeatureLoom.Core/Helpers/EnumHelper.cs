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
        static volatile IReadOnlyDictionary<int, byte[]> intToUtf8Name;
        static volatile IReadOnlyDictionary<int, T> intToEnum = new Dictionary<int, T>();

        // Names sorted by their UTF-8 bytes, so a name can be resolved directly from UTF-8
        // input without allocating a string first. Entries are ordered by length and then
        // by byte content, which allows a binary search over the whole table.
        static volatile Utf8NameEntry[] utf8NameEntries;

        // Direct array lookup for enums with contiguous int values, avoiding the interface
        // dispatch of the IReadOnlyDictionary above. Null when the values are not contiguous.
        static volatile T[] sequentialIntToEnum;
        static int sequentialOffset;

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

                // Safety note for Unsafe.As usage in this method:
                // - Every Unsafe.As<TFrom, T>(ref x) call is gated by an exact runtime type check
                //   (e.g. `underlyingType == typeof(int)`), so that branch executes only when underlyingType is exactly that type.
                // - Therefore `TFrom` and `T` have identical runtime type/layout in the executed branch, and
                //   Unsafe.As is used as a zero-allocation typed return path (no cross-type reinterpretation).

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
                var intToUtf8NameDict = new Dictionary<int, byte[]>();
                var intToEnumDict = new Dictionary<int, T>();
                foreach (var value in values)
                {                    
                    T enumValue = (T)value;
                    int intValue = convertToInt(enumValue);
                    string name = value.ToString();
                    intToNameDict[intValue] = name;
                    intToUtf8NameDict[intValue] = Encoding.UTF8.GetBytes(name);
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
                    intToUtf8Name = new ListDictionary<byte[]>(keys.Select(k => intToUtf8NameDict[k]), minKey);
                    intToEnum = new ListDictionary<T>(keys.Select(k => intToEnumDict[k]), minKey);

                    sequentialOffset = minKey;
                    sequentialIntToEnum = keys.Select(k => intToEnumDict[k]).ToArray();
                }
                else
                {
                    intToName = intToNameDict;
                    intToUtf8Name = intToUtf8NameDict;
                    intToEnum = intToEnumDict;
                }

                // Build the searchable UTF-8 name table. All int keys are included, so aliases
                // (several names mapping to the same value) stay resolvable.
                var entries = new List<Utf8NameEntry>();
                foreach (var pair in intToUtf8NameDict)
                {
                    entries.Add(new Utf8NameEntry(pair.Value, intToEnumDict[pair.Key]));
                }
                entries.Sort((x, y) => CompareUtf8(x.name, y.name));
                utf8NameEntries = entries.ToArray();

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
        /// Gets the name of the enum value as a cached UTF-8 encoded byte array, avoiding the
        /// string-to-UTF8 conversion at the call site. Enum names are valid C# identifiers and
        /// therefore pure ASCII, so the returned bytes never require escaping.
        /// WARNING: The returned array is the shared cached instance and MUST NOT be modified.
        /// </summary>
        public static byte[] ToUtf8Name(T enumValue)
        {
            if (!initialized) Init();
            int intValue = convertToInt(enumValue);
            return intToUtf8Name[intValue];
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

        private struct Utf8NameEntry
        {
            public readonly byte[] name;
            public readonly T value;

            public Utf8NameEntry(byte[] name, T value)
            {
                this.name = name;
                this.value = value;
            }
        }

        /// <summary>
        /// Orders UTF-8 names by length first and then by byte content, so a lookup can reject
        /// a candidate by its length before comparing any bytes.
        /// </summary>
        private static int CompareUtf8(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return left.Length - right.Length;
            for (int i = 0; i < left.Length; i++)
            {
                int diff = left[i] - right[i];
                if (diff != 0) return diff;
            }
            return 0;
        }

        /// <summary>
        /// Tries to resolve an enum value directly from the UTF-8 bytes of its name, without
        /// allocating an intermediate string. The match is case-sensitive, so callers must fall
        /// back to <see cref="TryFromString(string, out T)"/> when this returns false.
        /// </summary>
        public static bool TryFromUtf8Name(ArraySegment<byte> utf8Name, out T enumValue)
        {
            if (!initialized) Init();

            var entries = utf8NameEntries;
            byte[] array = utf8Name.Array;
            if (array != null && entries != null)
            {
                int offset = utf8Name.Offset;
                int count = utf8Name.Count;
                int low = 0;
                int high = entries.Length - 1;
                while (low <= high)
                {
                    int mid = (int)(((uint)(low + high)) >> 1);
                    byte[] candidate = entries[mid].name;

                    int diff = candidate.Length - count;
                    if (diff == 0)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            diff = candidate[i] - array[offset + i];
                            if (diff != 0) break;
                        }
                    }

                    if (diff == 0)
                    {
                        enumValue = entries[mid].value;
                        return true;
                    }
                    if (diff < 0) low = mid + 1;
                    else high = mid - 1;
                }
            }

            enumValue = default;
            return false;
        }

        /// <summary>
        /// Tries to get the enum value from its integer representation.
        /// </summary>
        public static bool TryFromInt(int intValue, out T enumValue)
        {
            if (!initialized) Init();

            // Contiguous enums resolve through a plain array, which avoids the interface
            // dispatch that a dictionary lookup would cost on this hot path.
            var sequential = sequentialIntToEnum;
            if (sequential != null)
            {
                int index = intValue - sequentialOffset;
                if ((uint)index < (uint)sequential.Length)
                {
                    enumValue = sequential[index];
                    return true;
                }
                enumValue = default;
                return false;
            }

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
        /// Gets the name of the enum value as a cached UTF-8 encoded byte array.
        /// WARNING: The returned array is the shared cached instance and MUST NOT be modified.
        /// </summary>
        public static byte[] ToUtf8Name<T>(T enumValue) where T : struct, Enum => EnumHelper<T>.ToUtf8Name(enumValue);

        /// <summary>
        /// Converts the enum value to its underlying integer value.
        /// </summary>
        public static int ToInt<T>(T enumValue) where T : struct, Enum => EnumHelper<T>.ToInt(enumValue);

        /// <summary>
        /// Tries to parse an enum value from a string (case-insensitive).
        /// </summary>
        public static bool TryFromString<T>(string enumString, out T enumValue) where T : struct, Enum => EnumHelper<T>.TryFromString(enumString, out enumValue);

        /// <summary>
        /// Tries to resolve an enum value directly from the UTF-8 bytes of its name.
        /// </summary>
        public static bool TryFromUtf8Name<T>(ArraySegment<byte> utf8Name, out T enumValue) where T : struct, Enum => EnumHelper<T>.TryFromUtf8Name(utf8Name, out enumValue);

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
