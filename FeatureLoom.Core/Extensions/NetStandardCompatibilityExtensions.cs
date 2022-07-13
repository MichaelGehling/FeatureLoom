using System.Collections.Generic;

namespace FeatureLoom.Extensions
{
    public static class NetStandardCompatibilityExtensions
    {

#if NETSTANDARD2_0
        // Available since .Net Standard 2.1
        public static bool TryAdd<K, V>(this Dictionary<K, V> dict, K key, V value)
        {
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, value);
                return true;
            }
            else return false;
        }

        // Available since .Net Standard 2.1
        public static bool TryDequeue<T>(this Queue<T> queue, out T item)
        {
            if (queue.Count > 0)
            {
                item = queue.Dequeue();
                return true;
            }
            else
            {
                item = default;
                return false;
            }
        }

        // Available since .Net Standard 2.1
        public static string[] Split(this string str, char seperator, int count)
        {
            return str.Split(seperator.ToSingleEntryArray(), count);
        }
#endif
    }
}