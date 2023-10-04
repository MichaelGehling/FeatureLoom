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

        private void CreateGenericEnumerableItemHandler<T, E, ENUM>(CachedTypeHandler typeHandler, CachedTypeHandler elementHandler) where T : IEnumerable<E> where ENUM : IEnumerator<E>
        {
            Type itemType = typeof(T);
            MethodInfo getEnumeratorMethod = itemType.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);
            var getEnumerator = (Func<T, ENUM>)Delegate.CreateDelegate(typeof(Func<T, ENUM>), getEnumeratorMethod);

            bool requiresItemNames = settings.RequiresItemNames;
            Type expectedElementType = typeof(E);
            if (elementHandler.IsPrimitive)
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

                    bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && collectionType != expectedType);
                    if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                    writer.OpenCollection();
                    ENUM enumerator = getEnumerator(collection);
                    if (enumerator.MoveNext())
                    {
                        E element = enumerator.Current;
                        elementHandler.HandlePrimitiveItem(element);
                    }
                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        E element = enumerator.Current;
                        elementHandler.HandlePrimitiveItem(element);
                    }
                    writer.CloseCollection();

                    if (writeTypeInfo) FinishTypeInfoObject();
                };
                bool isPrimitive = !itemType.IsClass;
                typeHandler.SetItemHandler(itemHandler, isPrimitive);
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

                    bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && collectionType != expectedType);
                    if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                    writer.OpenCollection();
                    ENUM enumerator = getEnumerator(collection);
                    int index = 0;
                    if (enumerator.MoveNext())
                    {
                        E element = enumerator.Current;
                        if (element == null) writer.WriteNullValue();
                        else
                        {
                            Type elementType = element.GetType();
                            CachedTypeHandler actualHandler = elementHandler;
                            if (elementType != expectedType) actualHandler = GetCachedTypeHandler(elementType);
                            byte[] elementName = settings.RequiresItemNames ? writer.PrepareCollectionIndexName(index) : null;
                            ItemInfo elementInfo = CreateItemInfo(element, itemInfo, elementName);
                            actualHandler.HandleItem(element, elementInfo);
                            itemInfoRecycler.ReturnItemInfo(elementInfo);
                        }
                        index++;
                    }
                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();

                        E element = enumerator.Current;
                        if (element == null) writer.WriteNullValue();
                        else
                        {
                            Type elementType = element.GetType();
                            CachedTypeHandler actualHandler = elementHandler;
                            if (elementType != expectedType) actualHandler = GetCachedTypeHandler(elementType);
                            byte[] elementName = settings.RequiresItemNames ? writer.PrepareCollectionIndexName(index) : null;
                            ItemInfo elementInfo = CreateItemInfo(element, itemInfo, elementName);
                            actualHandler.HandleItem(element, elementInfo);
                            itemInfoRecycler.ReturnItemInfo(elementInfo);
                        }
                        index++;
                    }
                    writer.CloseCollection();

                    if (writeTypeInfo) FinishTypeInfoObject();
                };
                typeHandler.SetItemHandler(itemHandler, false);
            }
        }



        private void CreateEnumerableItemHandler<T>(CachedTypeHandler typeHandler) where T : IEnumerable
        {
            bool requiresItemNames = settings.RequiresItemNames;
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

                bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                    (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && collectionType != expectedType);
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
                        byte[] itemName = settings.RequiresItemNames ? writer.PrepareCollectionIndexName(index) : null;
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
                        byte[] itemName = settings.RequiresItemNames ? writer.PrepareCollectionIndexName(index) : null;
                        ItemInfo elementInfo = CreateItemInfo(element, itemInfo, itemName);
                        actualHandler.HandleItem(element, elementInfo);
                    }
                    index++;
                }
                writer.CloseCollection();

                if (writeTypeInfo) FinishTypeInfoObject();
            };
            typeHandler.SetItemHandler(itemHandler, false);

        }
    }

    

}
