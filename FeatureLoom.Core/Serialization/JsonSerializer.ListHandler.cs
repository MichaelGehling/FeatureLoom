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
            if (itemType.IsArray)
            {
                Type multiDimElementType = itemType.GetElementType();
                CachedTypeWriter multiDimElementHandler = GetCachedTypeWriterForElement(multiDimElementType, typeHandler.TypeSettings);
                var multiDimResolveDeviating = CreateDeviatingElementWriterResolver(multiDimElementType, typeHandler.TypeSettings);
                MethodInfo multiDimMethod = typeof(JsonSerializer).GetMethod(nameof(CreateMultiDimArrayItemHandler), BindingFlags.NonPublic | BindingFlags.Instance);
                multiDimMethod.MakeGenericMethod(itemType, multiDimElementType).Invoke(this, new object[] { typeHandler, multiDimElementHandler, multiDimResolveDeviating });
                return true;
            }
            if (itemType.IsGenericType && itemType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return CreateSpecializedListItemHandler(typeHandler, itemType.GetGenericArguments()[0], nameof(CreateListItemHandler));
            }

            string methodName = null;
            if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IList<>), out Type elementType)) methodName = nameof(CreateIListItemHandler);
            else if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IReadOnlyList<>), out elementType)) methodName = nameof(CreateIReadOnlyListItemHandler);
            else return false;

            CachedTypeWriter elementHandler = GetCachedTypeWriterForElement(elementType, typeHandler.TypeSettings);
            var resolveDeviating = CreateDeviatingElementWriterResolver(elementType, typeHandler.TypeSettings);

            MethodInfo createMethod = typeof(JsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, elementType);
            genericCreateMethod.Invoke(this, new object[] { typeHandler, elementHandler, resolveDeviating });

            return true;
        }

        private bool CreateSpecializedListItemHandler(CachedTypeWriter typeHandler, Type elementType, string methodName)
        {
            CachedTypeWriter elementHandler = GetCachedTypeWriterForElement(elementType, typeHandler.TypeSettings);
            var resolveDeviating = CreateDeviatingElementWriterResolver(elementType, typeHandler.TypeSettings);

            MethodInfo createMethod = typeof(JsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(elementType);
            genericCreateMethod.Invoke(this, new object[] { typeHandler, elementHandler, resolveDeviating });

            return true;
        }

        private void CreateArrayItemHandler<E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler, Func<Type, CachedTypeWriter> resolveDeviating)
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
                                if (elementType != alternativeHandler.HandlerType) alternativeHandler = resolveDeviating(elementType);
                                alternativeHandler.WriteItem(element, writer.GetCollectionIndexName(i));
                            }
                        }
                    }
                };

                typeHandler.SetItemWriter(CreateArrayItemWriter(typeHandler, itemHandler), true);
            }
        }

        /// <summary>
        /// Writes multi-dimensional arrays (e.g. int[,]) as nested JSON arrays in row-major order.
        /// Elements are accessed typed without boxing: on .NET 6+ via the contiguous array data,
        /// otherwise via a compiled multi-index accessor.
        /// </summary>
        private void CreateMultiDimArrayItemHandler<T, E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler, Func<Type, CachedTypeWriter> resolveDeviating) where T : class
        {
            int rank = typeof(T).GetArrayRank();
            // Same decision as for single dimensional arrays: skip the per-element type check where deviation is impossible or irrelevant.
            bool directWrite = !elementHandler.HandlerType.IsNullable() || elementHandler.HandlerType.IsValueType || CanUseDirectReferenceStrategy(elementHandler, typeof(E));
#if !NET6_0_OR_GREATER
            var arrayParam = System.Linq.Expressions.Expression.Parameter(typeof(T));
            var indicesParam = System.Linq.Expressions.Expression.Parameter(typeof(int[]));
            var indexExpressions = new System.Linq.Expressions.Expression[rank];
            for (int d = 0; d < rank; d++) indexExpressions[d] = System.Linq.Expressions.Expression.ArrayIndex(indicesParam, System.Linq.Expressions.Expression.Constant(d));
            Func<T, int[], E> getElement = System.Linq.Expressions.Expression.Lambda<Func<T, int[], E>>(
                System.Linq.Expressions.Expression.ArrayAccess(arrayParam, indexExpressions), arrayParam, indicesParam).Compile();
#endif
            Action<T> itemHandler = (item) =>
            {
                Array array = (Array)(object)item;
                int[] lengths = new int[rank];
                for (int d = 0; d < rank; d++) lengths[d] = array.GetLength(d);
                // Empty outer dimension: nothing to write inside the outer brackets.
                if (lengths[0] == 0) return;

                CachedTypeWriter alternativeHandler = elementHandler;
                int total = array.Length;
#if NET6_0_OR_GREATER
                ref E first = ref System.Runtime.CompilerServices.Unsafe.As<byte, E>(ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(array));
#else
                int[] indices = new int[rank];
#endif
                for (int flatIndex = 0; flatIndex < total; flatIndex++)
                {
#if NET6_0_OR_GREATER
                    E element = System.Runtime.CompilerServices.Unsafe.Add(ref first, flatIndex);
#else
                    E element = getElement(item, indices);
                    for (int d = rank - 1; d >= 0; d--)
                    {
                        if (++indices[d] < lengths[d]) break;
                        indices[d] = 0;
                    }
#endif
                    // Open/close nested brackets based on the position within the row-major enumeration.
                    if (flatIndex > 0)
                    {
                        int closing = 0;
                        int rest = flatIndex;
                        for (int d = rank - 1; d > 0 && rest % lengths[d] == 0; d--)
                        {
                            closing++;
                            rest /= lengths[d];
                        }
                        for (int c = 0; c < closing; c++) writer.CloseArray();
                        writer.WriteComma();
                        for (int c = 0; c < closing; c++) writer.OpenArray();
                    }
                    else
                    {
                        for (int d = 1; d < rank; d++) writer.OpenArray();
                    }

                    if (element == null) writer.WriteNullValue();
                    else if (directWrite) elementHandler.WriteItem(element, writer.GetCollectionIndexName(flatIndex));
                    else
                    {
                        Type elementType = element.GetType();
                        if (elementType == elementHandler.HandlerType) elementHandler.WriteItem(element, writer.GetCollectionIndexName(flatIndex));
                        else
                        {
                            if (elementType != alternativeHandler.HandlerType) alternativeHandler = resolveDeviating(elementType);
                            alternativeHandler.WriteItem(element, writer.GetCollectionIndexName(flatIndex));
                        }
                    }
                }

                if (total > 0)
                {
                    for (int d = 1; d < rank; d++) writer.CloseArray();
                }
                else
                {
                    // Some inner dimension is empty, so no elements exist: write the empty sub-arrays of the
                    // first non-empty prefix dimensions (e.g. [[],[]] for new int[2,0]).
                    WriteEmptyMultiDimLevel(lengths, 0);
                }
            };

            typeHandler.SetItemWriter(CreateArrayItemWriter(typeHandler, itemHandler), true);
        }

        private void WriteEmptyMultiDimLevel(int[] lengths, int dimension)
        {
            if (dimension + 1 >= lengths.Length) return;
            for (int i = 0; i < lengths[dimension]; i++)
            {
                if (i > 0) writer.WriteComma();
                writer.OpenArray();
                WriteEmptyMultiDimLevel(lengths, dimension + 1);
                writer.CloseArray();
            }
        }

        private void CreateListItemHandler<E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler, Func<Type, CachedTypeWriter> resolveDeviating)
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
                                if (elementType != alternativeHandler.HandlerType) alternativeHandler = resolveDeviating(elementType);
                                alternativeHandler.WriteItem(element, writer.GetCollectionIndexName(i));
                            }
                        }
                    }
                };

                typeHandler.SetItemWriter(CreateArrayItemWriter(typeHandler, itemHandler), true);
            }
        }

        private void CreateIListItemHandler<T, E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler, Func<Type, CachedTypeWriter> resolveDeviating) where T : IList<E>
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
                                if (elementType != alternativeHandler.HandlerType) alternativeHandler = resolveDeviating(elementType);
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
                                if (elementType != alternativeHandler.HandlerType) alternativeHandler = resolveDeviating(elementType);
                                alternativeHandler.WriteItem(element, writer.GetCollectionIndexName(index));
                            }
                        }
                        index++;
                    }
                };
                typeHandler.SetItemWriter(CreateArrayItemWriter(typeHandler, itemHandler), true);

            }
        }

        private void CreateIReadOnlyListItemHandler<T, E>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler, Func<Type, CachedTypeWriter> resolveDeviating) where T : IReadOnlyList<E>
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
                            if (elementType != elementHandler.HandlerType) actualHandler = resolveDeviating(elementType);
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
                            if (elementType != elementHandler.HandlerType) actualHandler = resolveDeviating(elementType);
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
