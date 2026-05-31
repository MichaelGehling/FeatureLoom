using System;
using System.Collections.Generic;
using System.Reflection;
using FeatureLoom.Extensions;

namespace FeatureLoom.Serialization
{
    public sealed partial class JsonSerializer
    {
     
        private bool TryCreateListItemHandler(CachedTypeWriter typeHandler, Type itemType)
        {
            string methodName = null;
            if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IList<>), out Type elementType)) methodName = nameof(CreateIListItemHandler);
            else if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IReadOnlyList<>), out elementType)) methodName = nameof(CreateIReadOnlyListItemHandler);
            else return false;

            CachedTypeWriter elementHandler = GetCachedTypeWriter(elementType);

            MethodInfo createMethod = typeof(JsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, elementType);
            genericCreateMethod.Invoke(this, new object[] { typeHandler, elementHandler });

            return true;
        }

        private void CreateIListItemHandler<T, E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler) where T : IList<E>
        {
            Type itemType = typeof(T);
            Type expectedElementType = typeof(E);
            bool requiresItemNames = settings.requiresItemNames;
            if (elementHandler.HandlerType.IsValueType)
            {                
                ItemHandler<T> itemHandler = (list) =>
                {
                    int currentIndex = 0;
                    if (currentIndex < list.Count)
                    {
                        E element = list[currentIndex++];
                        elementHandler.WriteItem(element, default);
                    }
                    while (currentIndex < list.Count)
                    {
                        writer.WriteComma();
                        E element = list[currentIndex++];
                        elementHandler.WriteItem(element, default);
                    }
                };

                typeHandler.SetItemHandler_Array(itemHandler, true);
            }
            else
            {
                ItemHandler<T> itemHandler = (list) =>
                {
                    CachedTypeWriter alternativeHandler = elementHandler;
                    int index = 0;                    
                    if (index < list.Count)
                    {
                        E element = list[index];
                        if (element == null) writer.WriteNullValue();
                        else
                        {
                            Type elementType = element.GetType();
                            if (elementType == elementHandler.HandlerType) elementHandler.WriteItem(element, writer.GetCollectionIndexName(index));
                            else
                            {
                                if (elementType != alternativeHandler.HandlerType) alternativeHandler = GetCachedTypeWriter(elementType);
                                alternativeHandler.WriteItem(element, writer.GetCollectionIndexName(index));
                            }
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
                            if (elementType == elementHandler.HandlerType) elementHandler.WriteItem(element, writer.GetCollectionIndexName(index));
                            else
                            {
                                if (elementType != alternativeHandler.HandlerType) alternativeHandler = GetCachedTypeWriter(elementType);
                                alternativeHandler.WriteItem(element, writer.GetCollectionIndexName(index));
                            }
                        }
                        index++;
                    }
                };
                typeHandler.SetItemHandler_Array(itemHandler, false);

            }
        }

        private void CreateIReadOnlyListItemHandler<T, E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler) where T : IReadOnlyList<E>
        {
            Type itemType = typeof(T);
            Type expectedElementType = typeof(E);
            bool requiresItemNames = settings.requiresItemNames;
            if (!elementHandler.HandlerType.IsNullable() || elementHandler.HandlerType.IsValueType)
            {
                ItemHandler<T> itemHandler = (list) =>
                {
                    int currentIndex = 0;
                    if (currentIndex < list.Count)
                    {
                        E element = list[currentIndex++];
                        elementHandler.WriteItem(element, default);
                    }
                    while (currentIndex < list.Count)
                    {
                        writer.WriteComma();
                        E element = list[currentIndex++];
                        elementHandler.WriteItem(element, default);
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
                            CachedTypeWriter actualHandler = elementHandler;
                            if (elementType != elementHandler.HandlerType) actualHandler = GetCachedTypeWriter(elementType);
                            actualHandler.WriteItem(element, writer.GetCollectionIndexName(index));
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
                            CachedTypeWriter actualHandler = elementHandler;
                            if (elementType != elementHandler.HandlerType) actualHandler = GetCachedTypeWriter(elementType);
                            actualHandler.WriteItem(element, writer.GetCollectionIndexName(index));
                        }
                        index++;
                    }
                };
                typeHandler.SetItemHandler_Array(itemHandler, false);

            }
        }
    }

    

}
