using System;
using System.Collections.Generic;
using FeatureLoom.Helpers;
using FeatureLoom.Extensions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {

        private bool TryCreateDictionaryItemHandler(CachedTypeHandler typeHandler, Type itemType)
        {
            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IDictionary<,>), out Type keyType, out Type valueType)) return false;
            if (!TryGetCachedKeyWriter(keyType, out CachedKeyWriter keyWriter)) return false;
            CachedTypeHandler valueHandler = GetCachedTypeHandler(valueType);

            MethodInfo getEnumeratorMethod = itemType.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);

            MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(nameof(CreateDictionaryItemHandler), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, keyType, valueType, getEnumeratorMethod.ReturnType);
            genericCreateMethod.Invoke(this, new object[] { typeHandler, valueHandler, keyWriter });

            return true;
        }

        private void CreateDictionaryItemHandler<T, K, V, ENUM>(CachedTypeHandler typeHandler, CachedTypeHandler valueHandler, CachedKeyWriter keyWriter) 
            where T : IDictionary<K, V> 
            where ENUM : IEnumerator<KeyValuePair<K,V>>
        {
            Type itemType = typeof(T);
            Type expectedValueType = typeof(V);
            Type expectedKeyType = typeof(K);
            MethodInfo getEnumeratorMethod = itemType.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);
            var getEnumerator = (Func<T, ENUM>)Delegate.CreateDelegate(typeof(Func<T, ENUM>), getEnumeratorMethod);

            if (valueHandler.NoRefTypes)
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
                typeHandler.SetItemHandler_Object(itemHandler, true);
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
                typeHandler.SetItemHandler_Object(itemHandler, false);
            }
        }

    }

}
