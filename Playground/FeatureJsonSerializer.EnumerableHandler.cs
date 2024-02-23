using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using System.Collections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal;
using System.Linq;
using Microsoft.VisualBasic;
using System.Linq.Expressions;
using Newtonsoft.Json.Linq;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        private bool TryCreateEnumerableItemHandler(CachedTypeHandler typeHandler, Type itemType)
        {
            if (!itemType.ImplementsInterface(typeof(IEnumerable))) return false;

            if (!(settings.treatEnumerablesAsCollections ||
                  itemType.ImplementsGenericInterface(typeof(ICollection<>)) || 
                  itemType.ImplementsInterface(typeof(ICollection)))) return false;

            if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType))
            {
                CachedTypeHandler elementHandler = GetCachedTypeHandler(elementType);

                MethodInfo getEnumeratorMethod = itemType.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);

                string methodName = nameof(CreateGenericEnumerableItemHandler);
                MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, elementType, getEnumeratorMethod.ReturnType);
                genericCreateMethod.Invoke(this, new object[] { typeHandler, elementHandler });
            }
            else
            {
                string methodName = nameof(CreateEnumerableItemHandler);
                MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType);
                genericCreateMethod.Invoke(this, new object[] { typeHandler });
            }

            return true;
        }

        private void CreateGenericEnumerableItemHandler<T, E, ENUM>(CachedTypeHandler typeHandler, CachedTypeHandler defaultElementHandler) where T : IEnumerable<E> where ENUM : IEnumerator<E>
        {
            Type itemType = typeof(T);
            MethodInfo getEnumeratorMethod = itemType.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);
            var getEnumerator = (Func<T, ENUM>)Delegate.CreateDelegate(typeof(Func<T, ENUM>), getEnumeratorMethod);

            bool requiresItemNames = settings.requiresItemNames;
            Type expectedElementType = typeof(E);
            
            if (defaultElementHandler.IsPrimitive)
            {
                bool isPrimitive = !itemType.IsClass;
                if (isPrimitive)
                {
                    PrimitiveItemHandler<T> itemHandler = (collection) =>
                    {
                        if (collection == null)
                        {
                            writer.WriteNullValue();
                            return;
                        }

                        bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo;
                        if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                        writer.OpenCollection();
                        ENUM enumerator = getEnumerator(collection);
                        if (enumerator.MoveNext())
                        {
                            E element = enumerator.Current;
                            defaultElementHandler.HandlePrimitiveItem(element);
                        }
                        while (enumerator.MoveNext())
                        {
                            writer.WriteComma();
                            E element = enumerator.Current;
                            defaultElementHandler.HandlePrimitiveItem(element);
                        }
                        writer.CloseCollection();

                        if (writeTypeInfo) FinishTypeInfoObject();
                    };

                    typeHandler.SetItemHandler(itemHandler);
                }
                else
                {
                    ItemHandler<T> itemHandler = (collection, expectedType, itemInfo) =>
                    {
                        if (collection == null)
                        {
                            writer.WriteNullValue();
                            return;
                        }

                        Type collectionType = collection.GetType();
                        if (TryHandleItemAsRef(collection, itemInfo, collectionType)) return;

                        bool writeTypeInfo = TypeInfoRequired(collectionType, expectedType);
                        if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                        writer.OpenCollection();
                        ENUM enumerator = getEnumerator(collection);
                        if (enumerator.MoveNext())
                        {
                            E element = enumerator.Current;
                            defaultElementHandler.HandlePrimitiveItem(element);
                        }
                        while (enumerator.MoveNext())
                        {
                            writer.WriteComma();
                            E element = enumerator.Current;
                            defaultElementHandler.HandlePrimitiveItem(element);
                        }
                        writer.CloseCollection();

                        if (writeTypeInfo) FinishTypeInfoObject();
                    };

                    typeHandler.SetItemHandler(itemHandler);
                }
            }
            else
            {
                ItemHandler<T> itemHandler = (collection, expectedType, itemInfo) =>
                {
                    if (collection == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type collectionType = collection.GetType();
                    if (TryHandleItemAsRef(collection, itemInfo, collectionType)) return;

                    bool writeTypeInfo = TypeInfoRequired(collectionType, expectedType);
                    if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                    writer.OpenCollection();
                    CachedTypeHandler currentHandler = defaultElementHandler;
                    ENUM enumerator = getEnumerator(collection);
                    int index = 0;
                    if (enumerator.MoveNext())
                    {
                        E element = enumerator.Current;
                        currentHandler = HandleElement(defaultElementHandler, itemInfo, currentHandler, index, element);
                        index++;
                    }
                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();

                        E element = enumerator.Current;
                        currentHandler = HandleElement(defaultElementHandler, itemInfo, currentHandler, index, element);
                        index++;
                    }
                    writer.CloseCollection();

                    if (writeTypeInfo) FinishTypeInfoObject();
                };
                typeHandler.SetItemHandler(itemHandler);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CachedTypeHandler HandleElement<E>(CachedTypeHandler defaultElementHandler, ItemInfo itemInfo, CachedTypeHandler currentHandler, int index, E element)
        {
            if (element == null) writer.WriteNullValue();
            else
            {
                Type elementType = element.GetType();
                if (elementType != currentHandler.HandlerType)
                {
                    if (elementType == defaultElementHandler.HandlerType) currentHandler = defaultElementHandler;
                    else currentHandler = GetCachedTypeHandler(elementType);
                }
                byte[] elementName = settings.requiresItemNames ? writer.PrepareCollectionIndexName(index) : null;
                ItemInfo elementInfo = CreateItemInfo(element, itemInfo, elementName);
                currentHandler.HandleItem(element, elementInfo);
            }

            return currentHandler;
        }



        private void CreateEnumerableItemHandler<T>(CachedTypeHandler typeHandler) where T : IEnumerable
        {
            bool requiresItemNames = settings.requiresItemNames;
            Type expectedElementType = typeof(object);

            ItemHandler<T> itemHandler = (collection, expectedType, itemInfo) =>
            {
                if (collection == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                Type collectionType = collection.GetType();
                if (TryHandleItemAsRef(collection, itemInfo, collectionType)) return;

                bool writeTypeInfo = TypeInfoRequired(collectionType, expectedType);
                if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                writer.OpenCollection();
                var enumerator = collection.GetEnumerator();
                int index = 0;
                if (enumerator.MoveNext())
                {
                    object element = enumerator.Current;
                    if (element == null) writer.WriteNullValue();
                    else
                    {
                        Type elementType = element.GetType();
                        CachedTypeHandler actualHandler = GetCachedTypeHandler(elementType);
                        byte[] itemName = settings.requiresItemNames ? writer.PrepareCollectionIndexName(index) : null;
                        ItemInfo elementInfo = CreateItemInfo(element, itemInfo, itemName);
                        actualHandler.HandleItem(element, elementInfo);
                    }
                    index++;
                }
                while (enumerator.MoveNext())
                {
                    writer.WriteComma();

                    object element = enumerator.Current;
                    if (element == null) writer.WriteNullValue();
                    else
                    {
                        Type elementType = element.GetType();
                        CachedTypeHandler actualHandler = GetCachedTypeHandler(elementType);
                        byte[] itemName = settings.requiresItemNames ? writer.PrepareCollectionIndexName(index) : null;
                        ItemInfo elementInfo = CreateItemInfo(element, itemInfo, itemName);
                        actualHandler.HandleItem(element, elementInfo);
                    }
                    index++;
                }
                writer.CloseCollection();

                if (writeTypeInfo) FinishTypeInfoObject();
            };
            typeHandler.SetItemHandler(itemHandler);

        }
    }

    

}
