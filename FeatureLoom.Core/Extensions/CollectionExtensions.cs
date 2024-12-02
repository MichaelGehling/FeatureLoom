using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections;
using FeatureLoom.Collections;

namespace FeatureLoom.Extensions
{
    public static class CollectionExtensions
    {
        public static bool All(this IEnumerable items, Func<object, bool> check)
        {
            foreach(object item in items)
            {
                if (!check(item)) return false;
            }
            return true;
        }

        public static IEnumerable<T> Select<T>(this IEnumerable source, Func<object, T> selector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            return SelectIterator(source, selector);
        }

        private static IEnumerable<T> SelectIterator<T>(IEnumerable source, Func<object, T> selector)
        {
            foreach (var item in source)
            {
                yield return selector(item);
            }
        }

        public static IEnumerable Skip(this IEnumerable source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");

            return SkipIterator(source, count);
        }

        private static IEnumerable SkipIterator(IEnumerable source, int count)
        {
            var enumerator = source.GetEnumerator();
            try
            {
                // Skip the specified number of elements
                while (count > 0 && enumerator.MoveNext())
                {
                    count--;
                }

                // Yield the remaining elements
                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current;
                }
            }
            finally
            {
                if (enumerator is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        public static IEnumerator<T> GetEnumerator<T>(this T[] array) => ((IEnumerable<T>)array).GetEnumerator();
        public static ICollection<T> ToCollection<T>(this IEnumerable<T> enumerable) => new EnumerableAsCollection<T>(enumerable);
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

        public static bool Contains<T, TEnum>(this TEnum self, T elementToFind) where TEnum : IEnumerable<T>
        {            
            foreach (T element in self)
            {
                if (Equals(element, elementToFind)) return true;                
            }
            return false;
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

        public static void CopyToArray<T>(this IEnumerable<T> source, T[] targetArray, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (targetArray == null) throw new ArgumentNullException(nameof(targetArray));
            if (count < 0 || count > targetArray.Length) throw new ArgumentOutOfRangeException(nameof(count));

            using (var enumerator = source.GetEnumerator())
            {
                int i = 0;
                while (i < count && enumerator.MoveNext())
                {
                    targetArray[i++] = enumerator.Current;
                }

                // Optional: Clear the remaining elements in the target array if count is less than targetArray.Length
                if (i < targetArray.Length)
                {
                    Array.Clear(targetArray, i, targetArray.Length - i);
                }
            }
        }

        public static T[] AddToCopy<T>(this T[] array, T newElement)
        {
            T[] newArray = new T[array.Length + 1];
            Array.Copy(array, newArray, array.Length);
            newArray[newArray.Length - 1] = newElement;
            return newArray;
        }

        public static T[] RemoveFromCopy<T>(this T[] array, T elementToRemove)
        {
            int index = array.IndexOf(elementToRemove);
            if (index == -1) return array.ToArray();
            return RemoveIndexFromCopy(array, index);
        }

        public static T[] RemoveIndexFromCopy<T>(this T[] array, int indexToRemove)
        {
            if (indexToRemove < 0 || indexToRemove >= array.Length) return array.ToArray();
            T[] newArray = new T[array.Length - 1];
            if (indexToRemove > 0) Array.Copy(array, 0, newArray, 0, indexToRemove);
            if (indexToRemove+1 < array.Length) Array.Copy(array, indexToRemove+1, newArray, 0, array.Length-indexToRemove);
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

        public static bool TryFindFirst<T>(this IEnumerable<T> items, Func<T, bool> predicate, out T item)
        {
            item = default;
            foreach (var itemToCheck in items)
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


        public static bool TryElementsOut<T>(this IList<T> list, out T item0)
        {
            item0 = default;
            if (list.EmptyOrNull() || list.Count < 1) return false;
            item0 = list[0];
            return true;
        }

        public static bool TryElementsOut<T>(this IList<T> list, out T item0, out T item1)
        {
            item0 = default;
            item1 = default;
            if (list.EmptyOrNull() || list.Count < 2) return false;
            item0 = list[0];
            item1 = list[1];
            return true;
        }

        public static bool TryElementsOut<T>(this IList<T> list, out T item0, out T item1, out T item2)
        {
            item0 = default;
            item1 = default;
            item2 = default;
            if (list.EmptyOrNull() || list.Count < 3) return false;
            item0 = list[0];
            item1 = list[1];
            item2 = list[2];
            return true;
        }

        public static bool TryElementsOut<T>(this IList<T> list, out T item0, out T item1, out T item2, out T item3)
        {
            item0 = default;
            item1 = default;
            item2 = default;
            item3 = default;
            if (list.EmptyOrNull() || list.Count < 4) return false;
            item0 = list[0];
            item1 = list[1];
            item2 = list[2];
            item3 = list[3];
            return true;
        }
#if NETSTANDARD2_0
        public static bool TryPop<T>(this Stack<T> stack, out T value)
        {
            value = default;
            if (stack.Count == 0) return false;
            value = stack.Pop();
            return true;
        }

        public static bool TryPeek<T>(this Stack<T> stack, out T value)
        {
            value = default;
            if (stack.Count == 0) return false;
            value = stack.Peek();
            return true;
        }
#endif

    }
}