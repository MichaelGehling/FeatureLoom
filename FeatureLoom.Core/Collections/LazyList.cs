using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace FeatureLoom.Core.Collections
{
    public struct LazyList<T> : IList<T>
    {
        LazyValue<List<T>> list;

        public List<T> GetList()
        {
            return list.Obj;
        }

        public T this[int index]
        {
            get => list.Exists ? list.Obj[index] : throw new IndexOutOfRangeException();
            set
            {
                if (list.Exists) list.Obj[index] = value;
                else throw new IndexOutOfRangeException();
            }
        }

        public int Count => list.Exists ? list.Obj.Count : 0;

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            list.Obj.Add(item);
        }

        public void Clear()
        {
            if (!list.Exists) return;
            list.Obj.Clear();
        }

        public bool Contains(T item)
        {
            if (!list.Exists) return false;
            return list.Obj.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (!list.Exists) return;
            list.Obj.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (!list.Exists) return (IEnumerator<T>)Array.Empty<T>().GetEnumerator();
            return list.Obj.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            if (!list.Exists) return -1;
            return list.Obj.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            list.Obj.Insert(index, item);
        }

        public bool Remove(T item)
        {
            if (!list.Exists) return false;
            return list.Obj.Remove(item);
        }

        public void RemoveAt(int index)
        {
            if (!list.Exists) throw new ArgumentOutOfRangeException();
            list.Obj.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (!list.Exists) return Array.Empty<T>().GetEnumerator();
            return list.Obj.GetEnumerator();
        }

        public static implicit operator List<T>(LazyList<T> lazy) => lazy.GetList();

        public static implicit operator LazyList<T>(List<T> obj)
        {
            var lazy = new LazyList<T>();
            lazy.list.Obj = obj;
            return lazy;
        }
    }

}
