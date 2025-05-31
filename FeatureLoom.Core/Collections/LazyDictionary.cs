using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FeatureLoom.Collections
{
    /// <summary>
    /// A lightweight, allocation-on-demand dictionary wrapper that implements <see cref="IDictionary{TKey, TValue}"/>.
    /// <para>
    /// <b>LazyDictionary&lt;TKey, TValue&gt;</b> only allocates the underlying <see cref="Dictionary{TKey, TValue}"/>
    /// when a write operation (such as <see cref="Add"/>, indexer set, or <see cref="Remove"/>) is performed.
    /// Read operations (such as <see cref="Count"/>, <see cref="ContainsKey"/>, or enumeration) are safe and return sensible
    /// defaults (e.g., 0, false, or an empty enumerator) if the dictionary has not been created.
    /// </para>
    /// <para>
    /// This is useful for optional or rarely-used dictionaries, reducing memory usage in scenarios where
    /// many instances may never need to store any items. The struct supports implicit conversion to and
    /// from <see cref="Dictionary{TKey, TValue}"/> for easy interoperability.
    /// </para>
    /// <para>
    /// <b>Note:</b> This struct is not thread-safe. Copying a <c>LazyDictionary&lt;TKey, TValue&gt;</c> copies the wrapper,
    /// but both wrappers will reference the same underlying dictionary if it has been created.
    /// </para>
    /// </summary>
    public struct LazyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private LazyValue<Dictionary<TKey, TValue>> dict;

        public Dictionary<TKey, TValue> GetDictionary()
        {
            return dict.Obj;
        }

        public TValue this[TKey key]
        {
            get
            {
                if (!dict.Exists) throw new KeyNotFoundException();
                return dict.Obj[key];
            }
            set
            {
                dict.Obj[key] = value;
            }
        }

        public ICollection<TKey> Keys => dict.Exists ? (ICollection<TKey>)dict.Obj.Keys : Array.Empty<TKey>();
        public ICollection<TValue> Values => dict.Exists ? (ICollection<TValue>)dict.Obj.Values : Array.Empty<TValue>();
        public int Count => dict.Exists ? dict.Obj.Count : 0;
        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            dict.Obj.Add(key, value);
        }

        public bool ContainsKey(TKey key)
        {
            if (!dict.Exists) return false;
            return dict.Obj.ContainsKey(key);
        }

        public bool Remove(TKey key)
        {
            if (!dict.Exists) return false;
            return dict.Obj.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (!dict.Exists)
            {
                value = default;
                return false;
            }
            return dict.Obj.TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            dict.Obj.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            if (!dict.Exists) return;
            dict.Obj.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (!dict.Exists) return false;
            return ((ICollection<KeyValuePair<TKey, TValue>>)dict.Obj).Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (!dict.Exists) return;
            ((ICollection<KeyValuePair<TKey, TValue>>)dict.Obj).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (!dict.Exists) return false;
            return ((ICollection<KeyValuePair<TKey, TValue>>)dict.Obj).Remove(item);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            if (!dict.Exists) return Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();
            return dict.Obj.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (!dict.Exists) return Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();
            return dict.Obj.GetEnumerator();
        }

        public static implicit operator Dictionary<TKey, TValue>(LazyDictionary<TKey, TValue> lazy) => lazy.GetDictionary();

        public static implicit operator LazyDictionary<TKey, TValue>(Dictionary<TKey, TValue> obj)
        {
            var lazy = new LazyDictionary<TKey, TValue>();
            lazy.dict.Obj = obj;
            return lazy;
        }
    }
}