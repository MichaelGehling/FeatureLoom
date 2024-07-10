using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground
{
    public class JsonValue : IDictionary<string, object>
    {
        Dictionary<string, object> fields = new Dictionary<string, object>();

        public object this[string key] { get => ((IDictionary<string, object>)fields)[key]; set => ((IDictionary<string, object>)fields)[key] = value; }

        public ICollection<string> Keys => ((IDictionary<string, object>)fields).Keys;

        public ICollection<object> Values => ((IDictionary<string, object>)fields).Values;

        public int Count => ((ICollection<KeyValuePair<string, object>>)fields).Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<string, object>>)fields).IsReadOnly;

        public void Add(string key, object value)
        {
            ((IDictionary<string, object>)fields).Add(key, value);
        }

        public void Add(KeyValuePair<string, object> item)
        {
            ((ICollection<KeyValuePair<string, object>>)fields).Add(item);
        }

        public void Clear()
        {
            ((ICollection<KeyValuePair<string, object>>)fields).Clear();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return ((ICollection<KeyValuePair<string, object>>)fields).Contains(item);
        }

        public bool ContainsKey(string key)
        {
            return ((IDictionary<string, object>)fields).ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, object>>)fields).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, object>>)fields).GetEnumerator();
        }

        public bool Remove(string key)
        {
            return ((IDictionary<string, object>)fields).Remove(key);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            return ((ICollection<KeyValuePair<string, object>>)fields).Remove(item);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value)
        {
            return ((IDictionary<string, object>)fields).TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)fields).GetEnumerator();
        }
    }
}
