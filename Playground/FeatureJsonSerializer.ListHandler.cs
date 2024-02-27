using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FeatureLoom.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
     
        private bool TryCreateListItemHandler(CachedTypeHandler typeHandler, Type itemType)
        {
            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IList<>), out Type elementType)) return false;
            CachedTypeHandler elementHandler = GetCachedTypeHandler(elementType);

            string methodName = nameof(CreateIListItemHandler);
            MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, elementType);
            genericCreateMethod.Invoke(this, new object[] { typeHandler, elementHandler });

            return true;
        }

        private void CreateIListItemHandler<T, E>(CachedTypeHandler typeHandler, CachedTypeHandler elementHandler) where T : IList<E>
        {
            Type itemType = typeof(T);
            bool requiresItemNames = settings.requiresItemNames;
            if (elementHandler.IsPrimitive)
            {
                bool isPrimitive = !itemType.IsClass;
                if (isPrimitive)
                {
                    PrimitiveItemHandler<T> itemHandler = (list) =>
                    {
                        bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo;
                        if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                        writer.OpenCollection();
                        int currentIndex = 0;
                        if (currentIndex < list.Count)
                        {
                            E element = list[currentIndex++];
                            elementHandler.HandlePrimitiveItem(element);
                        }
                        while (currentIndex < list.Count)
                        {
                            writer.WriteComma();
                            E element = list[currentIndex++];
                            elementHandler.HandlePrimitiveItem(element);
                        }
                        writer.CloseCollection();

                        if (writeTypeInfo) FinishTypeInfoObject();
                    };

                    typeHandler.SetItemHandler(itemHandler);
                }
                else
                {
                    ItemHandler<T> itemHandler = (list, expectedType, parentJob) =>
                    {
                        if (list == null)
                        {
                            writer.WriteNullValue();
                            return;
                        }

                        Type listType = list.GetType();
                        if (TryHandleItemAsRef(list, parentJob, listType)) return;

                        bool writeTypeInfo = TypeInfoRequired(listType, expectedType);
                        if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                        writer.OpenCollection();
                        int currentIndex = 0;
                        if (currentIndex < list.Count)
                        {
                            E element = list[currentIndex++];
                            elementHandler.HandlePrimitiveItem(element);
                        }
                        while (currentIndex < list.Count)
                        {
                            writer.WriteComma();
                            E element = list[currentIndex++];
                            elementHandler.HandlePrimitiveItem(element);
                        }
                        writer.CloseCollection();

                        if (writeTypeInfo) FinishTypeInfoObject();
                    };

                    typeHandler.SetItemHandler(itemHandler);
                }
            }
            else
            {
                ItemHandler<T> itemHandler = (list, expectedType, itemInfo) =>
                {
                    if (list == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type listType = list.GetType();
                    if (TryHandleItemAsRef(list, itemInfo, listType)) return;

                    bool writeTypeInfo = TypeInfoRequired(listType, expectedType);
                    if (writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                    writer.OpenCollection();
                    int index = 0;
                    if (index < list.Count)
                    {
                        E element = list[index];
                        if (element == null) writer.WriteNullValue();
                        else
                        {
                            Type elementType = element.GetType();
                            CachedTypeHandler actualHandler = elementHandler;
                            if (elementType != elementHandler.HandlerType) actualHandler = GetCachedTypeHandler(elementType);
                            byte[] elementName = settings.requiresItemNames ? writer.PrepareCollectionIndexName(index) : null;
                            ItemInfo elementInfo = elementType.IsClass ? CreateItemInfoForClass(element, itemInfo, elementName) : CreateItemInfoForStruct(itemInfo, elementName);
                            actualHandler.HandleItem(element, elementInfo);
                            itemInfoRecycler.ReturnItemInfo(elementInfo);
                        }
                        index++;
                    }
                    while (index < list.Count)
                    {
                        writer.WriteComma();

                        E element = list[index];
                        if (element == null) writer.WriteNullValue();
                        else
                        {
                            Type elementType = element.GetType();
                            CachedTypeHandler actualHandler = elementHandler;
                            if (elementType != elementHandler.HandlerType) actualHandler = GetCachedTypeHandler(elementType);
                            byte[] elementName = settings.requiresItemNames ? writer.PrepareCollectionIndexName(index) : null;
                            ItemInfo elementInfo = elementType.IsClass ? CreateItemInfoForClass(element, itemInfo, elementName) : CreateItemInfoForStruct(itemInfo, elementName);
                            actualHandler.HandleItem(element, elementInfo);
                            itemInfoRecycler.ReturnItemInfo(elementInfo);
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

    

}
