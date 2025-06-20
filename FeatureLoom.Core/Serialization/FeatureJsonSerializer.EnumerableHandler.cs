using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using System.Collections;

namespace FeatureLoom.Serialization
{
    public sealed partial class FeatureJsonSerializer
    {
        private bool TryCreateEnumerableItemHandler(CachedTypeHandler typeHandler, Type itemType)
        {
            if (itemType != typeof(IEnumerable) && !itemType.ImplementsInterface(typeof(IEnumerable))) return false;

            if (!(settings.treatEnumerablesAsCollections ||
                  itemType == typeof(ICollection) ||
                  itemType.ImplementsGenericInterface(typeof(ICollection<>)) || 
                  itemType.ImplementsInterface(typeof(ICollection)))) return false;

            if (itemType.TryGetTypeParamsOfGenericInterface(typeof(IEnumerable<>), out Type elementType))
            {
                CachedTypeHandler elementHandler = GetCachedTypeHandler(elementType);

                Type enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
                MethodInfo getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);

                string methodName = nameof(CreateGenericEnumerableItemHandler);
                MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, elementType, getEnumeratorMethod.ReturnType);
                genericCreateMethod.Invoke(this, new object[] { getEnumeratorMethod, typeHandler, elementHandler });
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

        private void CreateGenericEnumerableItemHandler<T, E, ENUM>(MethodInfo getEnumeratorMethod, CachedTypeHandler typeHandler, CachedTypeHandler defaultElementHandler) where T : IEnumerable<E> where ENUM : IEnumerator<E>
        {
            Type itemType = typeof(T);

            var getEnumerator = (Func<T, ENUM>)Delegate.CreateDelegate(typeof(Func<T, ENUM>), getEnumeratorMethod);

            Type expectedElementType = typeof(E);
            
            if (!defaultElementHandler.HandlerType.IsNullable())
            {
                ItemHandler<T> itemHandler = (collection) =>
                {
                    ENUM enumerator = getEnumerator(collection);
                    if (enumerator.MoveNext())
                    {
                        E element = enumerator.Current;
                        defaultElementHandler.HandleItem(element, default);
                    }
                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();
                        E element = enumerator.Current;
                        defaultElementHandler.HandleItem(element, default);
                    }
                };

                typeHandler.SetItemHandler_Array(itemHandler, true);
 
            }
            else
            {
                ItemHandler<T> itemHandler = (collection) =>
                {
                    CachedTypeHandler currentHandler = defaultElementHandler;
                    ENUM enumerator = getEnumerator(collection);
                    int index = 0;
                    if (enumerator.MoveNext())
                    {
                        E element = enumerator.Current;
                        currentHandler = HandleElement(defaultElementHandler, currentHandler, index, element, expectedElementType);
                        index++;
                    }
                    while (enumerator.MoveNext())
                    {
                        writer.WriteComma();

                        E element = enumerator.Current;
                        currentHandler = HandleElement(defaultElementHandler, currentHandler, index, element, expectedElementType);
                        index++;
                    }                    
                };
                typeHandler.SetItemHandler_Array(itemHandler, false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CachedTypeHandler HandleElement<E>(CachedTypeHandler defaultElementHandler, CachedTypeHandler currentHandler, int index, E element, Type expectedElementType)
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
                currentHandler.HandleItem(element, writer.GetCollectionIndexName(index));                
            }

            return currentHandler;
        }



        private void CreateEnumerableItemHandler<T>(CachedTypeHandler typeHandler) where T : IEnumerable
        {
            bool requiresItemNames = settings.requiresItemNames;
            Type expectedElementType = typeof(object);

            ItemHandler<T> itemHandler = (collection) =>
            {
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
                        actualHandler.HandleItem(element, writer.GetCollectionIndexName(index));                        
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
                        actualHandler.HandleItem(element, writer.GetCollectionIndexName(index));                        
                    }
                    index++;
                }
            };
            typeHandler.SetItemHandler_Array(itemHandler, false);

        }
    }

    

}
