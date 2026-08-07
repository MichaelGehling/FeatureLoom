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
            // Single dimensional arrays and List<E> get dedicated handlers, because accessing them
            // via the IList<E> interface costs two interface calls per element (Count and indexer),
            // which for arrays are dispatched through the slow SZArrayHelper stubs.
            if (itemType.IsArray && itemType.GetArrayRank() == 1)
            {
                return CreateSpecializedListItemHandler(typeHandler, itemType.GetElementType(), nameof(CreateArrayItemHandler));
            }
            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return CreateSpecializedListItemHandler(typeHandler, itemType.GetGenericArguments()[0], nameof(CreateListItemHandler));
            }

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

        private bool CreateSpecializedListItemHandler(CachedTypeWriter typeHandler, Type elementType, string methodName)
        {
            CachedTypeWriter elementHandler = GetCachedTypeWriter(elementType);

            MethodInfo createMethod = typeof(JsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(elementType);
            genericCreateMethod.Invoke(this, new object[] { typeHandler, elementHandler });

            return true;
        }

        private void CreateArrayItemHandler<E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler)
        {
            if (!elementHandler.HandlerType.IsNullable() || elementHandler.HandlerType.IsValueType || CanUseDirectReferenceStrategy(elementHandler, typeof(E)))
            {
                CreateIndexedItemHandler<E[], E, ArrayAccessor<E>>(typeHandler, elementHandler);
            }
            else
            {
                Action<E[]> itemHandler = (array) =>
                {
                    CachedTypeWriter alternativeHandler = elementHandler;
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (i > 0) writer.WriteComma();

                        E element = array[i];
                        if (element == null) writer.WriteNullValue();
                        else
                        {
                            Type elementType = element.GetType();
                            if (elementType == elementHandler.HandlerType) elementHandler.WriteItem(element, writer.GetCollectionIndexName(i));
                            else
                            {
                                if (elementType != alternativeHandler.HandlerType) alternativeHandler = GetCachedTypeWriter(elementType);
                                alternativeHandler.WriteItem(element, writer.GetCollectionIndexName(i));
                            }
                        }
                    }
                };

                typeHandler.SetItemWriter(CreateArrayItemWriter(typeHandler, itemHandler), true);
            }
        }

        private void CreateListItemHandler<E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler)
        {
            if (!elementHandler.HandlerType.IsNullable() || elementHandler.HandlerType.IsValueType || CanUseDirectReferenceStrategy(elementHandler, typeof(E)))
            {
                CreateIndexedItemHandler<List<E>, E, ListAccessor<E>>(typeHandler, elementHandler);
            }
            else
            {
                Action<List<E>> itemHandler = (list) =>
                {
                    CachedTypeWriter alternativeHandler = elementHandler;
                    int count = list.Count;
                    for (int i = 0; i < count; i++)
                    {
                        if (i > 0) writer.WriteComma();

                        E element = list[i];
                        if (element == null) writer.WriteNullValue();
                        else
                        {
                            Type elementType = element.GetType();
                            if (elementType == elementHandler.HandlerType) elementHandler.WriteItem(element, writer.GetCollectionIndexName(i));
                            else
                            {
                                if (elementType != alternativeHandler.HandlerType) alternativeHandler = GetCachedTypeWriter(elementType);
                                alternativeHandler.WriteItem(element, writer.GetCollectionIndexName(i));
                            }
                        }
                    }
                };

                typeHandler.SetItemWriter(CreateArrayItemWriter(typeHandler, itemHandler), true);
            }
        }

        private void CreateIListItemHandler<T, E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler) where T : IList<E>
        {
            Type itemType = typeof(T);
            Type expectedElementType = typeof(E);
            bool requiresItemNames = settings.requiresItemNames;
            if (elementHandler.HandlerType.IsValueType || CanUseDirectReferenceStrategy(elementHandler, typeof(E)))
            {
                CreateIndexedItemHandler<T, E, IListAccessor<T, E>>(typeHandler, elementHandler);
            }
            else
            {
                Action<T> itemHandler = (list) =>
                {
                    CachedTypeWriter alternativeHandler = elementHandler;
                    int index = 0;
                    int count = list.Count;
                    if (index < count)
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
                    while (index < count)
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
                typeHandler.SetItemWriter(CreateArrayItemWriter(typeHandler, itemHandler), true);

            }
        }

        private void CreateIReadOnlyListItemHandler<T, E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler) where T : IReadOnlyList<E>
        {
            Type itemType = typeof(T);
            Type expectedElementType = typeof(E);
            bool requiresItemNames = settings.requiresItemNames;
            if (!elementHandler.HandlerType.IsNullable() || elementHandler.HandlerType.IsValueType || CanUseDirectReferenceStrategy(elementHandler, typeof(E)))
            {
                CreateIndexedItemHandler<T, E, IReadOnlyListAccessor<T, E>>(typeHandler, elementHandler);
            }
            else
            {
                Action<T> itemHandler = (list) =>
                {
                    int index = 0;
                    int count = list.Count;
                    if (index < count)
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
                    while (index < count)
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
                typeHandler.SetItemWriter(CreateArrayItemWriter(typeHandler, itemHandler), true);

            }
        }
    }

    

}
