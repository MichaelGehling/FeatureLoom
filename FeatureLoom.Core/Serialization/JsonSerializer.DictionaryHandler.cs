using System;
using System.Collections.Generic;
using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using FeatureLoom.Extensions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization
{
    public sealed partial class JsonSerializer
    {

        /// <summary>
        /// Creates the handler that writes a dictionary as a JSON object, using the dictionary
        /// keys as property names.
        /// </summary>
        /// <remarks>
        /// This requires the key type to be writable as a JSON property name (see
        /// <see cref="TryCreateKeyWriter"/>) or a key formatter to be configured for the type.
        /// Key types that cannot be represented that way (e.g. complex objects) intentionally fall
        /// back to the generic enumerable handler, which writes the dictionary as an array of
        /// key/value pairs (<c>[{"key":...,"value":...}, ...]</c>). The deserializer accepts both
        /// shapes, so such dictionaries still round-trip. The pair array can also be requested
        /// explicitly via <see cref="DictionaryShape.KeyValuePairArray"/>.
        /// </remarks>
        private bool TryCreateDictionaryItemHandler(CachedTypeWriter typeHandler, Type itemType)
        {
            string methodName = null;
            if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out Type keyType, out Type valueType)) methodName = nameof(CreateIDictionaryItemHandler);
            else if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IReadOnlyDictionary<,>), out keyType, out valueType)) methodName = nameof(CreateIReadOnlyDictionaryItemHandler);
            else return false;

            // The key cannot become a JSON property name, or the pair array was requested, so write
            // the dictionary as an array of key/value pairs instead. This is done explicitly rather
            // than by falling through to the enumerable handler, because IReadOnlyDictionary<,>
            // does not inherit the non-generic IEnumerable that the enumerable handler requires.
            var typeSettings = typeHandler.TypeSettings;
            if (typeSettings?.dictionaryShape == DictionaryShape.KeyValuePairArray ||
                !TryCreateKeyWriter(keyType, typeSettings, out CachedKeyWriter keyWriter))
            {
                return CreateKeyValuePairArrayItemHandler(typeHandler, itemType);
            }
            CachedTypeWriter valueHandler = GetCachedTypeWriterForElement(valueType, typeHandler.TypeSettings);
            var resolveDeviating = CreateDeviatingElementWriterResolver(valueType, typeHandler.TypeSettings);

            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType))             
            {
                throw new ArgumentException($"The item type {itemType} does not implement IEnumerable<T> for the dictionary items.");
            }

            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            MethodInfo getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);

            MethodInfo createMethod = typeof(JsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, keyType, valueType, getEnumeratorMethod.ReturnType);
            genericCreateMethod.Invoke(this, new object[] { getEnumeratorMethod, typeHandler, valueHandler, keyWriter, resolveDeviating });

            return true;
        }

        /// <summary>
        /// Writes a dictionary as an array of key/value pairs, which is the fallback shape for
        /// key types that cannot be written as JSON property names.
        /// </summary>
        private bool CreateKeyValuePairArrayItemHandler(CachedTypeWriter typeHandler, Type itemType)
        {
            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType)) return false;

            CachedTypeWriter elementHandler = GetCachedTypeWriterForElement(elementType, typeHandler.TypeSettings);
            var resolveDeviating = CreateDeviatingElementWriterResolver(elementType, typeHandler.TypeSettings);

            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            MethodInfo getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);

            MethodInfo createMethod = typeof(JsonSerializer).GetMethod(nameof(CreateGenericEnumerableItemHandler), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, elementType, getEnumeratorMethod.ReturnType);
            genericCreateMethod.Invoke(this, new object[] { getEnumeratorMethod, typeHandler, elementHandler, resolveDeviating });

            return true;
        }

        private void CreateIDictionaryItemHandler<T, K, V, ENUM>(MethodInfo getEnumeratorMethod, CachedTypeWriter typeHandler, CachedTypeWriter valueHandler, CachedKeyWriter keyWriter, Func<Type, CachedTypeWriter> resolveDeviating)
            where T : IDictionary<K, V> 
            where ENUM : IEnumerator<KeyValuePair<K,V>>
        {
            Type expectedValueType = typeof(V);
            var getEnumerator = (Func<T, ENUM>)Delegate.CreateDelegate(typeof(Func<T, ENUM>), getEnumeratorMethod);

            if (!valueHandler.HandlerType.IsNullable() || valueHandler.HandlerType.IsValueType)
            {
                // The typed delegate is resolved once here, so the per entry write neither casts
                // the delegate nor selects the mode again.
                Action<K> writeKey = keyWriter.GetWriter<K>();
                Action<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        writeKey(pair.Key);
                        writer.WriteColon();
                        valueHandler.WriteItem(pair.Value, default);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        writeKey(pair.Key);
                        writer.WriteColon();
                        valueHandler.WriteItem(pair.Value, default);
                    }
                };
                typeHandler.SetItemWriter(CreateObjectItemWriter(typeHandler, itemHandler), !valueHandler.NoRefTypes);
            }
            else if (!settings.requiresItemNames)
            {
                // The values may be references, but no item names are needed, so the key does not
                // have to be copied out of the write buffer.
                Action<K> writeKey = keyWriter.GetWriter<K>();
                Action<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        writeKey(pair.Key);
                        writer.WriteColon();
                        WriteDictionaryValue(pair.Value, expectedValueType, valueHandler, default, resolveDeviating);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        writeKey(pair.Key);
                        writer.WriteColon();
                        WriteDictionaryValue(pair.Value, expectedValueType, valueHandler, default, resolveDeviating);
                    }
                };
                typeHandler.SetItemWriter(CreateObjectItemWriter(typeHandler, itemHandler), !valueHandler.NoRefTypes);
            }
            else
            {
                // The keys are used as item names for reference handling, so they have to be
                // copied out of the write buffer before it moves on.
                Func<K, ByteSegment> writeKeyWithCopy = keyWriter.GetWriterWithCopy<K>();
                Action<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        var itemName = writeKeyWithCopy(pair.Key);
                        writer.WriteColon();
                        WriteDictionaryValue(pair.Value, expectedValueType, valueHandler, itemName, resolveDeviating);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        var itemName = writeKeyWithCopy(pair.Key);
                        writer.WriteColon();
                        WriteDictionaryValue(pair.Value, expectedValueType, valueHandler, itemName, resolveDeviating);
                    }
                };
                typeHandler.SetItemWriter(CreateObjectItemWriter(typeHandler, itemHandler), !valueHandler.NoRefTypes);
            }
        }

        /// <summary>
        /// Writes a possibly null dictionary value, using the handler of the actual type if it
        /// deviates from the declared one.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteDictionaryValue<V>(V value, Type expectedValueType, CachedTypeWriter valueHandler, ByteSegment itemName, Func<Type, CachedTypeWriter> resolveDeviating)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            Type valueType = value.GetType();
            CachedTypeWriter actualHandler = valueType != expectedValueType ? resolveDeviating(valueType) : valueHandler;
            actualHandler.WriteItem(value, itemName);
        }

        private void CreateIReadOnlyDictionaryItemHandler<T, K, V, ENUM>(MethodInfo getEnumeratorMethod, CachedTypeWriter typeHandler, CachedTypeWriter valueHandler, CachedKeyWriter keyWriter, Func<Type, CachedTypeWriter> resolveDeviating)
            where T : IReadOnlyDictionary<K, V>
            where ENUM : IEnumerator<KeyValuePair<K, V>>
        {
            Type expectedValueType = typeof(V);
            var getEnumerator = (Func<T, ENUM>)Delegate.CreateDelegate(typeof(Func<T, ENUM>), getEnumeratorMethod);

            if (!valueHandler.HandlerType.IsNullable() || valueHandler.HandlerType.IsValueType)
            {
                Action<K> writeKey = keyWriter.GetWriter<K>();
                Action<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        writeKey(pair.Key);
                        writer.WriteColon();
                        valueHandler.WriteItem(pair.Value, default);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        writeKey(pair.Key);
                        writer.WriteColon();
                        valueHandler.WriteItem(pair.Value, default);
                    }
                };
                typeHandler.SetItemWriter(CreateObjectItemWriter(typeHandler, itemHandler), !valueHandler.NoRefTypes);
            }
            else if (!settings.requiresItemNames)
            {
                Action<K> writeKey = keyWriter.GetWriter<K>();
                Action<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        writeKey(pair.Key);
                        writer.WriteColon();
                        WriteDictionaryValue(pair.Value, expectedValueType, valueHandler, default, resolveDeviating);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        writeKey(pair.Key);
                        writer.WriteColon();
                        WriteDictionaryValue(pair.Value, expectedValueType, valueHandler, default, resolveDeviating);
                    }
                };
                typeHandler.SetItemWriter(CreateObjectItemWriter(typeHandler, itemHandler), !valueHandler.NoRefTypes);
            }
            else
            {
                Func<K, ByteSegment> writeKeyWithCopy = keyWriter.GetWriterWithCopy<K>();
                Action<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        var itemName = writeKeyWithCopy(pair.Key);
                        writer.WriteColon();
                        WriteDictionaryValue(pair.Value, expectedValueType, valueHandler, itemName, resolveDeviating);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        var itemName = writeKeyWithCopy(pair.Key);
                        writer.WriteColon();
                        WriteDictionaryValue(pair.Value, expectedValueType, valueHandler, itemName, resolveDeviating);
                    }
                };
                typeHandler.SetItemWriter(CreateObjectItemWriter(typeHandler, itemHandler), !valueHandler.NoRefTypes);
            }
        }

    }

}
