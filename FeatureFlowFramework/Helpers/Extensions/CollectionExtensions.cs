using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers.Extensions
{
    public static class CollectionExtensions
    {
        public static int RemoveWhere<T>(this IList<T> list, Predicate<T> predicate)
        {
            int numRemoved = 0;
            for(int i = list.Count - 1; i >= 0; i--)
            {
                if(predicate(list[i]))
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
            foreach(T element in self)
            {
                if(Equals(element, elementToFind)) return i;
                i++;
            }
            return -1;
        }

        public static T[] ToArray<T>(this IReadOnlyList<T> self)
        {
            var array = new T[self.Count];
            int i = 0;
            foreach(var item in self)
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
                if (!isFirst && delimiter != null) sb.Append(delimiter);
                else isFirst = false;
                sb.Append(item.ToString());
            }
            return sb.ToString();
        }

    }
}