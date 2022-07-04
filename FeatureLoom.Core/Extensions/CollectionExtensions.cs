﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections;

namespace FeatureLoom.Extensions
{
    public static class CollectionExtensions
    {
        public static int RemoveWhere<T>(this IList<T> list, Predicate<T> predicate)
        {
            int numRemoved = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (predicate(list[i]))
                {
                    list.RemoveAt(i);
                    numRemoved++;
                }
            }
            return numRemoved;
        }

        public static T[] ToSingleEntryArray<T>(this T item)
        {
            return new T[] { item };
        }

        public static int IndexOf<T, TEnum>(this TEnum self, T elementToFind) where TEnum : IEnumerable<T>
        {
            int i = 0;
            foreach (T element in self)
            {
                if (Equals(element, elementToFind)) return i;
                i++;
            }
            return -1;
        }

        public static T[] ToArray<T>(this IReadOnlyList<T> self)
        {
            var array = new T[self.Count];            
            for(int i = 0; i< array.Length; i++)
            {
                array[i] = self[i];
            }
            return array;
        }

        public static string AllItemsToString<T>(this IEnumerable<T> collection, string delimiter = null)
        {
            if (collection.EmptyOrNull()) return "";
            StringBuilder sb = new StringBuilder();
            bool isFirst = true;
            foreach (var item in collection)
            {
                if (item == null) continue;
                if (!isFirst && delimiter != null) sb.Append(delimiter);
                else isFirst = false;
                sb.Append(item.ToString());
            }
            return sb.ToString();
        }

        public static string AllItemsToString<T, TEnum>(this TEnum collection, string delimiter = null) where TEnum : IEnumerable<T>
        {
            if (collection.EmptyOrNull()) return "";
            StringBuilder sb = new StringBuilder();
            bool isFirst = true;
            foreach (var item in collection)
            {
                if (item == null) continue;
                if (!isFirst && delimiter != null) sb.Append(delimiter);
                else isFirst = false;
                sb.Append(item.ToString());
            }
            return sb.ToString();
        }

        public static T[] AddToCopy<T>(this T[] array, T newElement)
        {
            T[] newArray = new T[array.Length + 1];
            Array.Copy(array, newArray, array.Length);
            newArray[newArray.Length - 1] = newElement;
            return newArray;
        }

        public static Task ForeachAsync<T, TEnum>(this TEnum items, Func<T, Task> asyncAction) where TEnum : IEnumerable<T>
        {
            if (items.EmptyOrNull()) return Task.CompletedTask;

            List<Task> tasks = new List<Task>();
            foreach (var item in items)
            {
                tasks.Add(asyncAction(item));
            }
            return Task.WhenAll(tasks);
        }

        public static bool TryFindFirst<T, TEnum>(this TEnum items, Func<T, bool> predicate, out T item) where TEnum : IEnumerable<T>
        {
            item = default;
            foreach(var itemToCheck in items)
            {
                if (predicate(itemToCheck))
                {
                    item = itemToCheck;
                    return true;
                }
            }
            return false;
        }

        public static bool Replace<T>(this IList<T> list, T item, Predicate<T> predicate, bool replaceOnlyFirst = false)
        {
            bool replaced = false;
            for(int i=0; i < list.Count; i++)
            {
                if (predicate(list[i]))
                {
                    list[i] = item;
                    replaced = true;
                }

                if (replaced && replaceOnlyFirst) break;
            }
            return replaced;
        }

        public static bool TryConvertAll<T1,T2,TEnum>(this TEnum items, out T2[] convertedItems) where T1 : IConvertible where TEnum : IEnumerable<T1>
        {
            convertedItems = new T2[items.Count()];
            int i = 0;
            foreach(var item in items)
            {
                if (item is T2 casted) convertedItems[i++] = casted;
                else if (item.TryConvertTo(out T2 converted)) convertedItems[i++] = converted;
                else return false;
            }
            return true;                
        }

        public static bool TryConvertAll<T, TEnum>(this TEnum items, out T[] convertedItems) where TEnum : IEnumerable
        {
            List<T> convertedList = new List<T>();
            convertedItems = null;
            foreach (var item in items)
            {
                if (item is T casted) convertedList.Add(casted);
                else if (item is IConvertible convertible && convertible.TryConvertTo(out T converted)) convertedList.Add(converted);
                else return false;
            }
            convertedItems = convertedList.ToArray();
            return true;
        }

        public static object[] ToArray(this IEnumerable items)
        {
            List<object> convertedList = new List<object>();
            foreach (var item in items)
            {
                convertedList.Add(item);
            }
            return convertedList.ToArray();            
        }
    }
}