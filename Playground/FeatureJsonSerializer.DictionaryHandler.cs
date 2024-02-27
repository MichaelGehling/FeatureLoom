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

            bool requiresItemNames = settings.requiresItemNames;
            
            if (valueHandler.IsPrimitive)
            {
                ItemHandler<T> itemHandler = (dict, expectedType, itemInfo) =>
                {
                    if (dict == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type dictType = dict.GetType();
                    if (TryHandleItemAsRef(dict, itemInfo, dictType)) return;

                    writer.OpenObject();
                    ENUM enumerator = getEnumerator(dict);

                    if (TypeInfoRequired(dictType, expectedType))
                    {
                        writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                        writer.WriteComma();
                    }

                    if (enumerator.MoveNext())
                    {
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteKeyAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandlePrimitiveItem(pair.Value);
                    }

                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        KeyValuePair<K, V> pair = enumerator.Current;
                        keyWriter.WriteKeyAsString(pair.Key);
                        writer.WriteColon();
                        valueHandler.HandlePrimitiveItem(pair.Value);
                    }

                    writer.CloseObject();
                };
                typeHandler.SetItemHandler(itemHandler);
            }
            else
            {
                ItemHandler<T> itemHandler = (dict, expectedType, itemInfo) =>
                {
                    if (dict == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type dictType = dict.GetType();
                    if (TryHandleItemAsRef(dict, itemInfo, dictType)) return;

                    writer.OpenObject();
                    ENUM enumerator = getEnumerator(dict);

                    if (TypeInfoRequired(dictType, expectedType))
                    {
                        writer.WritePreparedByteString(typeHandler.preparedTypeInfo);
                        writer.WriteComma();
                    }

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
                            //string itemName = settings.requiresItemNames ? pair.Key.ToString() : null;
                            ItemInfo valueInfo = valueType.IsClass ? CreateItemInfoForClass(value, itemInfo, itemName) : CreateItemInfoForStruct(itemInfo, itemName);
                            actualHandler.HandleItem(value, valueInfo);
                            itemInfoRecycler.ReturnItemInfo(valueInfo);
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
                            //string itemName = settings.requiresItemNames ? pair.Key.ToString() : null;
                            ItemInfo valueInfo = valueType.IsClass ? CreateItemInfoForClass(value, itemInfo, itemName) : CreateItemInfoForStruct(itemInfo, itemName);
                            actualHandler.HandleItem(value, valueInfo);
                            itemInfoRecycler.ReturnItemInfo(valueInfo);
                        }
                    }

                    writer.CloseObject();
                };
                typeHandler.SetItemHandler(itemHandler);
            }
        }

    }

}
