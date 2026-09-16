using FeatureLoom.Synchronization;
using System.Collections.Generic;

namespace FeatureLoom.DependencyInversion
{
    // Stored in AsyncLocal before child contexts are created; the entries themselves are shared.
    internal sealed class LocalServiceScope
    {
        private readonly Dictionary<object, object> instances = new Dictionary<object, object>();
        private readonly MicroLock modifyLock = new MicroLock();
        private volatile bool active = true;

        internal bool IsActive => active;

        internal bool TryGet<T>(object key, out T instance) where T : class
        {
            using (modifyLock.Lock())
            {
                instance = null;
                if (!active || !instances.TryGetValue(key, out var value)) return false;
                instance = (T)value;
                return true;
            }
        }

        internal T GetOrAdd<T>(object key, T candidate) where T : class
        {
            using (modifyLock.Lock())
            {
                // Only pending holders are published here; service factories must execute after returning.
                if (!active) return null;
                if (instances.TryGetValue(key, out var value)) return (T)value;
                instances.Add(key, candidate);
                return candidate;
            }
        }

        internal void Set(object key, object instance)
        {
            using (modifyLock.Lock())
            {
                if (active) instances[key] = instance;
            }
        }

        internal void Clear()
        {
            using (modifyLock.Lock())
            {
                active = false;
                instances.Clear();
            }
        }
    }
}
