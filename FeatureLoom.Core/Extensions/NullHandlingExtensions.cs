using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Extensions
{
    public static class NullHandlingExtensions
    {
        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> enumarable)
        {
            return enumarable ?? Enumerable.Empty<T>();
        }

        public static IEnumerable EmptyIfNull(this IEnumerable enumarable)
        {
            return enumarable ?? Enumerable.Empty<object>();
        }

        public static T[] EmptyIfNull<T>(this T[] array)
        {
            return array ?? Array.Empty<T>();
        }

        public static ICollection<T> EmptyIfNull<T>(this ICollection<T> array)
        {
            return array ?? Array.Empty<T>();
        }

        public static string EmptyIfNull(this string str)
        {
            return str ?? "";
        }

        public static T NewIfNull<T>(this T obj) where T : class, new()
        {
            return obj ?? new T();
        }

        public static bool EmptyOrNull(this string str)
        {
            return str == null || str == "";
        }

        public static bool EmptyOrNull<T>(this T[] array)
        {
            return array == null || array.Length == 0;
        }

        public static bool EmptyOrNull<T>(this ICollection<T> collection)
        {
            return collection == null || collection.Count == 0;
        }

        public static bool EmptyOrNull<T>(this IEnumerable<T> enumarable)
        {
            return enumarable == null || !enumarable.Any();
        }

        public static bool EmptyOrNull(this IEnumerable enumarable)
        {
            return enumarable == null || !enumarable.Cast<object>().Any();
        }

        public static void AddIfNotNull<T>(this ICollection<T> list, T item) where T : class
        {
            if (item != null) list?.Add(item);
        }

        public static void InvokeIfNotNull<T>(this Action<T> action, T item) where T : class
        {
            if (item != null) action?.Invoke(item);
        }

        public static T ItemOrNull<T>(this IList<T> list, int index) where T : class
        {
            if (index >= 0 && index < list.Count) return list[index];
            else return null;
        }
    }
}