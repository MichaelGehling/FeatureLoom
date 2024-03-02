using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
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
            Type expectedElementType = typeof(E);
            bool requiresItemNames = settings.requiresItemNames;
            if (elementHandler.IsPrimitive)
            {
                ItemHandler<T> itemHandler = (list) =>
                {
                    int currentIndex = 0;
                    if (currentIndex < list.Count)
                    {
                        E element = list[currentIndex++];
                        elementHandler.HandleItem(element, default);
                    }
                    while (currentIndex < list.Count)
                    {
                        writer.WriteComma();
                        E element = list[currentIndex++];
                        elementHandler.HandleItem(element, default);
                    }
                };

                typeHandler.SetItemHandler_Array(itemHandler, true);
            }
            else
            {
                ItemHandler<T> itemHandler = (list) =>
                {
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
                            ArraySegment<byte> elementName = settings.requiresItemNames ? writer.PrepareCollectionIndexName(index) : default;                            
                            actualHandler.HandleItem(element, elementName);                            
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
                            ArraySegment<byte> elementName = settings.requiresItemNames ? writer.PrepareCollectionIndexName(index) : default;                            
                            actualHandler.HandleItem(element, elementName);                            
                        }
                        index++;
                    }
                };
                typeHandler.SetItemHandler_Array(itemHandler, false);

            }
        }
    }

    

}
