using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Collections
{
    /// <summary>
    /// A lightweight, allocation-on-demand list wrapper that implements <see cref="IList{T}"/>.
    /// <para>
    /// <b>LazyList&lt;T&gt;</b> only allocates the underlying <see cref="List{T}"/>
    /// when a write operation (such as <see cref="Add"/>, <see cref="Insert"/>, or indexer set) is performed.
    /// Read operations (such as <see cref="Count"/>, <see cref="Contains"/>, or enumeration) are safe and return sensible
    /// defaults (e.g., 0, false, -1, or an empty enumerator) if the list has not been created.
    /// </para>
    /// <para>
    /// This is useful for optional or rarely-used lists, reducing memory usage in scenarios where
    /// many instances may never need to store any items. The struct supports implicit conversion to and
    /// from <see cref="List{T}"/> for easy interoperability.
    /// </para>
    /// <para>
    /// <b>Note:</b> This struct is not thread-safe. Copying a <c>LazyList&lt;T&gt;</c> copies the wrapper,
    /// but both wrappers will reference the same underlying list if it has been created.
    /// </para>
    /// </summary>
    public struct LazyList<T> : IList<T>
    {
        // Underlying lazy value holding the list instance.
        LazyValue<List<T>> list;

        /// <summary>
        /// Gets the underlying <see cref="List{T}"/> instance, allocating it if necessary.
        /// </summary>
        public List<T> GetList()
        {
            return list.Obj;
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// Throws <see cref="ArgumentOutOfRangeException"/> if the list is not allocated or the index is out of range.
        /// </summary>
        public T this[int index]
        {
            get => list.Exists ? list.Obj[index] : throw new ArgumentOutOfRangeException();
            set
            {
                if (list.Exists) list.Obj[index] = value;
                else throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the list.
        /// Returns 0 if the list has not been created.
        /// </summary>
        public int Count => list.Exists ? list.Obj.Count : 0;

        /// <summary>
        /// Gets a value indicating whether the list is read-only.
        /// Always returns false.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Adds an item to the list.
        /// Allocates the underlying list if it does not exist.
        /// </summary>
        /// <param name="item">The object to add.</param>
        public void Add(T item)
        {
            list.Obj.Add(item);
        }

        /// <summary>
        /// Removes all items from the list.
        /// Does nothing if the list has not been created.
        /// </summary>
        public void Clear()
        {
            if (!list.Exists) return;
            list.Obj.Clear();
        }

        /// <summary>
        /// Determines whether the list contains a specific value.
        /// Returns false if the list has not been created.
        /// </summary>
        /// <param name="item">The object to locate.</param>
        public bool Contains(T item)
        {
            if (!list.Exists) return false;
            return list.Obj.Contains(item);
        }

        /// <summary>
        /// Copies the elements of the list to an array, starting at a particular array index.
        /// Does nothing if the list has not been created.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (!list.Exists) return;
            list.Obj.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the list.
        /// Returns an empty enumerator if the list has not been created.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            if (!list.Exists) return Enumerable.Empty<T>().GetEnumerator();
            return list.Obj.GetEnumerator();
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within the list.
        /// Returns -1 if the list has not been created or the item is not found.
        /// </summary>
        /// <param name="item">The object to locate.</param>
        public int IndexOf(T item)
        {
            if (!list.Exists) return -1;
            return list.Obj.IndexOf(item);
        }

        /// <summary>
        /// Inserts an item to the list at the specified index.
        /// Allocates the underlying list if it does not exist.
        /// </summary>
        /// <param name="index">The zero-based index at which item should be inserted.</param>
        /// <param name="item">The object to insert.</param>
        public void Insert(int index, T item)
        {
            list.Obj.Insert(index, item);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the list.
        /// Returns false if the list has not been created or the item is not found.
        /// </summary>
        /// <param name="item">The object to remove.</param>
        public bool Remove(T item)
        {
            if (!list.Exists) return false;
            return list.Obj.Remove(item);
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// Throws <see cref="ArgumentOutOfRangeException"/> if the list is not allocated or the index is out of range.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        public void RemoveAt(int index)
        {
            if (!list.Exists) throw new ArgumentOutOfRangeException();
            list.Obj.RemoveAt(index);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the list (non-generic).
        /// Returns an empty enumerator if the list has not been created.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            if (!list.Exists) return Enumerable.Empty<T>().GetEnumerator();
            return list.Obj.GetEnumerator();
        }

        /// <summary>
        /// Implicitly converts a <see cref="LazyList{T}"/> to a <see cref="List{T}"/>.
        /// </summary>
        public static implicit operator List<T>(LazyList<T> lazy) => lazy.GetList();

        /// <summary>
        /// Implicitly converts a <see cref="List{T}"/> to a <see cref="LazyList{T}"/>.
        /// </summary>
        public static implicit operator LazyList<T>(List<T> obj)
        {
            var lazy = new LazyList<T>();
            lazy.list.Obj = obj;
            return lazy;
        }
    }
}
