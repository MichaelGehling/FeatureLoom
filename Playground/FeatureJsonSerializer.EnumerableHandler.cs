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

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        class EnumerableStackJob : StackJob
        {
            IBox enumeratorBox;
            internal Type collectionType;
            internal int currentIndex;
            internal FeatureJsonSerializer serializer;
            Func<EnumerableStackJob, bool> processor;
            internal bool writeTypeInfo;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void SetEnumerator<T>(T enumerator)
            {
                if (!(enumeratorBox is Box<T> castedBox)) enumeratorBox = new Box<T>(enumerator);
                else castedBox.value = enumerator;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Box<T> GetEnumeratorBox<T>()
            {
                return enumeratorBox as Box<T>;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Init(FeatureJsonSerializer serializer, Func<EnumerableStackJob, bool> processor, object collection, bool writeTypeInfo)
            {
                this.processor = processor;
                this.serializer = serializer;
                this.collectionType = collection.GetType();
                this.writeTypeInfo = writeTypeInfo;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Process()
            {
                return processor.Invoke(this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override void Reset()
            {
                enumeratorBox.Clear();
                processor = null;
                currentIndex = 0;
                collectionType = null;
                base.Reset();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override byte[] GetCurrentChildItemName()
            {
                return serializer.writer.PrepareCollectionIndexName(currentIndex);
            }
        }

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
                ItemHandler<T> itemHandler = (collection, expectedType, parentJob) =>
                {
                    if (collection == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type collectionType = collection.GetType();
                    if (TryHandleItemAsRef(collection, parentJob, collectionType)) return;

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
                bool isPrimitive = !itemType.IsClass || itemType.IsSealed;
                typeHandler.SetItemHandler(itemHandler, isPrimitive);
            }
            else
            {
                Func<EnumerableStackJob, bool> processor = job =>
                {
                    int beforeStackSize = jobStack.Count;
                    var enumeratorBox = job.GetEnumeratorBox<IEnumerator<E>>();

                    if (job.currentIndex == 0)
                    {
                        if (job.writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                        writer.OpenCollection();

                        // We did the first MoveNext already to check if the collection is empty, so we can directly get the current element.
                        E element = enumeratorBox.value.Current;
                        if (element == null) writer.WriteNullValue();
                        else if (element.GetType() == expectedElementType) elementHandler.HandleItem(element, expectedElementType, job);
                        else GetCachedTypeHandler(element.GetType()).HandleItem(element, typeof(E), job);
                        job.currentIndex++;
                        if (jobStack.Count != beforeStackSize) return false;
                    }

                    while(enumeratorBox.value.MoveNext())
                    {
                        writer.WriteComma();
                        E element = enumeratorBox.value.Current;
                        if (element == null) writer.WriteNullValue();
                        else if (element.GetType() == expectedElementType) elementHandler.HandleItem(element, expectedElementType, job);
                        else GetCachedTypeHandler(element.GetType()).HandleItem(element, typeof(E), job);
                        job.currentIndex++;
                        if (jobStack.Count != beforeStackSize) return false;
                    }

                    writer.CloseCollection();
                    if (job.writeTypeInfo) FinishTypeInfoObject();
                    return true;
                };

                ItemHandler<T> itemHandler = (collection, expectedType, parentJob) =>
                {
                    if (collection == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type collectionType = collection.GetType();
                    if (TryHandleItemAsRef(collection, parentJob, collectionType)) return;

                    bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && collectionType != expectedType);

                    var enumerator = getEnumerator(collection);
                    if (!enumerator.MoveNext()) // We do the first MoveNet already here, so we don't do it later in processing
                    {
                        if (writeTypeInfo)
                        {
                            StartTypeInfoObject(typeHandler.preparedTypeInfo);
                            writer.OpenCollection();
                            writer.CloseCollection();
                            FinishTypeInfoObject();
                        }
                        else
                        {
                            writer.OpenCollection();
                            writer.CloseCollection();
                        }
                        return;
                    }
                    var job = enumerableStackJobRecycler.GetJob(parentJob, requiresItemNames ? CreateItemName(parentJob) : null, collection);
                    job.Init(this, processor, collection, writeTypeInfo);
                    job.SetEnumerator(enumerator);
                    AddJobToStack(job);
                };
                typeHandler.SetItemHandler(itemHandler, false);
            }
        }

        private void CreateEnumerableItemHandler<T>(CachedTypeHandler typeHandler) where T : IEnumerable
        {
            bool requiresItemNames = settings.RequiresItemNames;
            Type expectedElementType = typeof(object);

            Func<EnumerableStackJob, bool> processor = job =>
            {
                int beforeStackSize = jobStack.Count;
                var enumeratorBox = job.GetEnumeratorBox<IEnumerator>();

                if (job.currentIndex == 0)
                {
                    if (job.writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                    writer.OpenCollection();

                    // We did the first MoveNext already to check if the collection is empty, so we can directly get the current element.
                    object element = enumeratorBox.value.Current;
                    if (element == null) writer.WriteNullValue();
                    else GetCachedTypeHandler(element.GetType()).HandleItem(element, expectedElementType, job);
                    job.currentIndex++;
                    if (jobStack.Count != beforeStackSize) return false;
                }

                while (enumeratorBox.value.MoveNext())
                {
                    writer.WriteComma();
                    object element = enumeratorBox.value.Current;
                    if (element == null) writer.WriteNullValue();
                    else GetCachedTypeHandler(element.GetType()).HandleItem(element, expectedElementType, job);
                    job.currentIndex++;
                    if (jobStack.Count != beforeStackSize) return false;
                }

                writer.CloseCollection();
                if (job.writeTypeInfo) FinishTypeInfoObject();
                return true;
            };

            ItemHandler<T> itemHandler = (collection, expectedType, parentJob) =>
            {
                if (collection == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                Type collectionType = collection.GetType();
                if (TryHandleItemAsRef(collection, parentJob, collectionType)) return;

                bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                    (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && collectionType != expectedType);

                var enumerator = collection.GetEnumerator();
                if (!enumerator.MoveNext()) // We do the first MoveNet already here, so we don't do it later in processing
                {
                    if (writeTypeInfo)
                    {
                        StartTypeInfoObject(typeHandler.preparedTypeInfo);
                        writer.OpenCollection();
                        writer.CloseCollection();
                        FinishTypeInfoObject();
                    }
                    else
                    {
                        writer.OpenCollection();
                        writer.CloseCollection();
                    }
                    return;
                }
                var job = enumerableStackJobRecycler.GetJob(parentJob, requiresItemNames ? CreateItemName(parentJob) : null, collection);
                job.Init(this, processor, collection, writeTypeInfo);
                job.SetEnumerator(enumerator);
                AddJobToStack(job);
            };
            typeHandler.SetItemHandler(itemHandler, false);
        }
    }

    

}
