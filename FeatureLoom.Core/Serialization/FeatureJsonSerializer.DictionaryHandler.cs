using System;
using System.Collections.Generic;
using FeatureLoom.Helpers;
using FeatureLoom.Extensions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization
{
    public sealed partial class FeatureJsonSerializer
    {

        private bool TryCreateDictionaryItemHandler(CachedTypeHandler typeHandler, Type itemType)
        {
            string methodName = null;
            if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out Type keyType, out Type valueType)) methodName = nameof(CreateIDictionaryItemHandler);
            else if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IReadOnlyDictionary<,>), out keyType, out valueType)) methodName = nameof(CreateIReadOnlyDictionaryItemHandler);
            else return false;

            if (!TryGetCachedKeyWriter(keyType, out CachedKeyWriter keyWriter)) return false;
            CachedTypeHandler valueHandler = GetCachedTypeHandler(valueType);

            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType))             
            {
                throw new ArgumentException($"The item type {itemType} does not implement IEnumerable<T> for the dictionary items.");
            }

            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            MethodInfo getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);

            MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, keyType, valueType, getEnumeratorMethod.ReturnType);
            genericCreateMethod.Invoke(this, new object[] { getEnumeratorMethod, typeHandler, valueHandler, keyWriter });

            return true;
        }

        private void CreateIDictionaryItemHandler<T, K, V, ENUM>(MethodInfo getEnumeratorMethod, CachedTypeHandler typeHandler, CachedTypeHandler valueHandler, CachedKeyWriter keyWriter) 
            where T : IDictionary<K, V> 
            where ENUM : IEnumerator<KeyValuePair<K,V>>
        {
            Type itemType = typeof(T);
            Type expectedValueType = typeof(V);
            Type expectedKeyType = typeof(K);
            var getEnumerator = (Func<T, ENUM>)Delegate.CreateDelegate(typeof(Func<T, ENUM>), getEnumeratorMethod);

            if (!valueHandler.HandlerType.IsNullable())
            {
                ItemHandler<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteKeyAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandleItem(pair.Value, default);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteKeyAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandleItem(pair.Value, default);
                    }
                };
                typeHandler.SetItemHandler_Object(itemHandler, valueHandler.NoRefTypes);
            }
            else
            {
                ItemHandler<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        var itemName = keyWriter.WriteKeyAsStringWithCopy(pair.Key);
                        writer.WriteColon();

                        var value = pair.Value;
                        if (value == null) writer.WriteNullValue();
                        else
                        {
                            Type valueType = value.GetType();
                            CachedTypeHandler actualHandler = valueHandler;
                            if (valueType != expectedValueType) actualHandler = GetCachedTypeHandler(valueType);                                                        
                            actualHandler.HandleItem(value, itemName);                            
                        }
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        var itemName = keyWriter.WriteKeyAsStringWithCopy(pair.Key);
                        writer.WriteColon();

                        var value = pair.Value;
                        if (value == null) writer.WriteNullValue();
                        else
                        {
                            Type valueType = value.GetType();
                            CachedTypeHandler actualHandler = valueHandler;
                            if (valueType != expectedValueType) actualHandler = GetCachedTypeHandler(valueType);                                                        
                            actualHandler.HandleItem(value, itemName);                            
                        }
                    }
                };
                typeHandler.SetItemHandler_Object(itemHandler, valueHandler.NoRefTypes);
            }
        }

        private void CreateIReadOnlyDictionaryItemHandler<T, K, V, ENUM>(MethodInfo getEnumeratorMethod, CachedTypeHandler typeHandler, CachedTypeHandler valueHandler, CachedKeyWriter keyWriter)
            where T : IReadOnlyDictionary<K, V>
            where ENUM : IEnumerator<KeyValuePair<K, V>>
        {
            Type itemType = typeof(T);
            Type expectedValueType = typeof(V);
            Type expectedKeyType = typeof(K);
            var getEnumerator = (Func<T, ENUM>)Delegate.CreateDelegate(typeof(Func<T, ENUM>), getEnumeratorMethod);

            if (!valueHandler.HandlerType.IsNullable())
            {
                ItemHandler<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteKeyAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandleItem(pair.Value, default);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteKeyAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandleItem(pair.Value, default);
                    }
                };
                typeHandler.SetItemHandler_Object(itemHandler, valueHandler.NoRefTypes);
            }
            else
            {
                ItemHandler<T> itemHandler = (dict) =>
                {
                    ENUM enumerator = getEnumerator(dict);
                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        var itemName = keyWriter.WriteKeyAsStringWithCopy(pair.Key);
                        writer.WriteColon();

                        var value = pair.Value;
                        if (value == null) writer.WriteNullValue();
                        else
                        {
                            Type valueType = value.GetType();
                            CachedTypeHandler actualHandler = valueHandler;
                            if (valueType != expectedValueType) actualHandler = GetCachedTypeHandler(valueType);
                            actualHandler.HandleItem(value, itemName);
                        }
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        var itemName = keyWriter.WriteKeyAsStringWithCopy(pair.Key);
                        writer.WriteColon();

                        var value = pair.Value;
                        if (value == null) writer.WriteNullValue();
                        else
                        {
                            Type valueType = value.GetType();
                            CachedTypeHandler actualHandler = valueHandler;
                            if (valueType != expectedValueType) actualHandler = GetCachedTypeHandler(valueType);
                            actualHandler.HandleItem(value, itemName);
                        }
                    }
                };
                typeHandler.SetItemHandler_Object(itemHandler, valueHandler.NoRefTypes);
            }
        }

    }

}
