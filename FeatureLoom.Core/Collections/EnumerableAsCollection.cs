using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Collections
{
    /// <summary>
    /// Wraps an IEnumerable as a read-only ICollection, with lazy caching for efficient repeated access.
    /// </summary>
    public class EnumerableAsCollection<T> : ICollection<T>
    {
        private IEnumerable<T> enumerable;
        private T[] cachedArray;

        public EnumerableAsCollection(IEnumerable<T> enumerable)
        {
            this.enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        }

        /// <summary>
        /// Materializes the enumerable into an array if not already done, and returns the array.
        /// </summary>
        private T[] GetArray()
        {
            if (cachedArray != null) return cachedArray;
            if (enumerable is T[] arr) cachedArray = arr;            
            else cachedArray = enumerable.ToArray();            
            enumerable = cachedArray; // Replace the enumerable with the array for future use
            return cachedArray;
        }

        public int Count => GetArray().Length;

        public bool IsReadOnly => true;

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            return Array.IndexOf(GetArray(), item) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            var src = GetArray();
            if (src.Length > array.Length - arrayIndex) throw new ArgumentException("Destination array is not large enough.");
            Array.Copy(src, 0, array, arrayIndex, src.Length);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)GetArray()).GetEnumerator();
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetArray().GetEnumerator();
        }
    }
}
