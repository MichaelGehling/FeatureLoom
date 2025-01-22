using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FeatureLoom.Extensions;

public static class DictionaryExtensions
{
    public static V GetOrAdd<K,V>(this Dictionary<K,V> dict, K key, V value) where K : notnull
    {
#if NET5_0_OR_GREATER
        ref var refValue = ref CollectionsMarshal.GetValueRefOrAddDefault(dict, key, out var exists);
        if (exists) return refValue;
        refValue = value;
        return value;
#else
        if (dict.TryGetValue(key, out var v)) return v;
        dict[key] = value;
        return value;
#endif
    }

    public static V GetOrAdd<K, V>(this Dictionary<K, V> dict, K key, V value, out bool existed) where K : notnull
    {
#if NET5_0_OR_GREATER
        ref var refValue = ref CollectionsMarshal.GetValueRefOrAddDefault(dict, key, out existed);
        if (existed) return refValue;
        refValue = value;
        return value;
#else
        existed = dict.TryGetValue(key, out var v);
        if (existed) return v;
        dict[key] = value;
        return value;
#endif
    }

    public static V GetOrCreate<K, V, VI>(this Dictionary<K, V> dict, K key, Func<VI, V> valueCreator, VI valueCreatorInput) where K : notnull
    {
#if NET5_0_OR_GREATER
        ref var refValue = ref CollectionsMarshal.GetValueRefOrAddDefault(dict, key, out var exists);
        if (exists) return refValue;

        V value = valueCreator(valueCreatorInput);
        refValue = value;
        return value;
#else
        if (dict.TryGetValue(key, out var v)) return v;

        V value = valueCreator(valueCreatorInput);
        dict[key] = value;
        return value;
#endif
    }

    public static V GetOrCreate<K, V, VI>(this Dictionary<K, V> dict, K key, Func<VI, V> valueCreator, VI valueCreatorInput, out bool existed) where K : notnull
    {
#if NET5_0_OR_GREATER
        ref var refValue = ref CollectionsMarshal.GetValueRefOrAddDefault(dict, key, out existed);
        if (existed) return refValue;

        V value = valueCreator(valueCreatorInput);
        refValue = value;
        return value;
#else
        existed = dict.TryGetValue(key, out var v);
        if (existed) return v;

        V value = valueCreator(valueCreatorInput);
        dict[key] = value;
        return value;
#endif
    }

    public static bool TryUpdate<K,V>(this Dictionary<K,V> dict, K key, V value)
    {
#if NET5_0_OR_GREATER
        ref var refValue = ref CollectionsMarshal.GetValueRefOrNullRef(dict, key);
        if (Unsafe.IsNullRef(ref refValue)) return false;
        refValue = value;
        return true;
#else
        if (!dict.ContainsKey(key)) return false;
        dict[key] = value;
        return true;
#endif
    }

    public static bool TryUpdate<K, V, VI>(this Dictionary<K, V> dict, K key, Func<VI, V> valueCreator, VI valueCreatorInput)
    {
#if NET5_0_OR_GREATER
        ref var refValue = ref CollectionsMarshal.GetValueRefOrNullRef(dict, key);
        if (Unsafe.IsNullRef(ref refValue)) return false;
        refValue = valueCreator(valueCreatorInput);
        return true;
#else
        if (!dict.ContainsKey(key)) return false;
        dict[key] = valueCreator(valueCreatorInput);
        return true;
#endif
    }
}
