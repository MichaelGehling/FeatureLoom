using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Core.Collections
{
    public class EnumerableAsCollection<T> : ICollection<T>
    {
        IEnumerable<T> enumerable;

        public EnumerableAsCollection(IEnumerable<T> enumerable)
        {
            this.enumerable = enumerable;
        }

        public int Count => enumerable.Count();

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
            return enumerable.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException();
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException();
            if (enumerable.Count() > array.Length - arrayIndex) new ArgumentException();
            
            foreach(var item in enumerable)
            {
                array[arrayIndex++] = item;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return enumerable.GetEnumerator();
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return enumerable.GetEnumerator();
        }
    }
}
