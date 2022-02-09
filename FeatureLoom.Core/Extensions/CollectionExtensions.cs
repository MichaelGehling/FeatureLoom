using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

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

        public static int IndexOf<T>(this IEnumerable<T> self, T elementToFind)
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
            int i = 0;
            foreach (var item in self)
            {
                array[i++] = item;
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

        public static T[] AddToCopy<T>(this T[] array, T newElement)
        {
            T[] newArray = new T[array.Length + 1];
            Array.Copy(array, newArray, array.Length);
            newArray[newArray.Length - 1] = newElement;
            return newArray;
        }

        public static Task foreachAsync<T>(this IEnumerable<T> items, Func<T, Task> asyncAction)
        {
            if (items.EmptyOrNull()) return Task.CompletedTask;

            List<Task> tasks = new List<Task>();
            foreach (var item in items)
            {
                tasks.Add(asyncAction(item));
            }
            return Task.WhenAll(tasks);
        }

        public static bool TryFindFirst<T>(this IEnumerable<T> items, Func<T, bool> predicate, out T item)
        {
            if (!items.Any())
            {
                item = default;
                return false;
            }
            else
            {
                item = items.FirstOrDefault(predicate);
                return predicate(item);
            }
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
    }
}