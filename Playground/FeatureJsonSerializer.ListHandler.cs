using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Abstractions.Internal;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        class ListStackJob : StackJob
        {
            internal object list;
            internal Type listType;            
            internal int currentIndex;
            internal FeatureJsonSerializer serializer;
            Func<ListStackJob, bool> processor;
            internal bool writeTypeInfo;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Init(FeatureJsonSerializer serializer, Func<ListStackJob, bool> processor, object list, bool writeTypeInfo)
            {
                this.processor = processor;
                this.list = list;
                this.serializer = serializer;
                this.listType = list.GetType();
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
                processor = null;
                currentIndex = 0;
                listType = null;
                base.Reset();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override byte[] GetCurrentChildItemName()
            {
                return serializer.writer.PrepareCollectionIndexName(currentIndex);
            }
        }

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
            bool requiresItemNames = settings.RequiresItemNames;
            if (elementHandler.IsPrimitive)
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

                    bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && listType != expectedType);
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
                bool isPrimitive = !itemType.IsClass;
                typeHandler.SetItemHandler(itemHandler, isPrimitive);
            }
            else
            {
                Func<ListStackJob, bool> processor = job =>
                {
                    int beforeStackSize = jobStack.Count;

                    IList<E> list = (IList<E>)job.list;
                    
                    if (job.currentIndex == 0)
                    {
                        if (job.writeTypeInfo) StartTypeInfoObject(typeHandler.preparedTypeInfo);

                        writer.OpenCollection();

                        E element = list[job.currentIndex];                        
                        if (element == null) writer.WriteNullValue();
                        else if (element.GetType() == typeof(E)) elementHandler.HandleItem(element, typeof(E), job);
                        else GetCachedTypeHandler(element.GetType()).HandleItem(element, typeof(E), job);
                        job.currentIndex++;
                        if (jobStack.Count != beforeStackSize) return false;
                    }

                    while (job.currentIndex < list.Count)
                    {
                        writer.WriteComma();
                        E element = list[job.currentIndex];
                        if (element == null) writer.WriteNullValue();
                        else if (element.GetType() == typeof(E)) elementHandler.HandleItem(element, typeof(E), job);
                        else GetCachedTypeHandler(element.GetType()).HandleItem(element, typeof(E), job);
                        job.currentIndex++;
                        if (jobStack.Count != beforeStackSize) return false;
                    }

                    writer.CloseCollection();
                    if (job.writeTypeInfo) FinishTypeInfoObject();
                    return true;
                };

                ItemHandler<T> itemHandler = (list, expectedType, parentJob) =>
                {
                    if (list == null)
                    {
                        writer.WriteNullValue();
                        return;
                    }

                    Type listType = list.GetType();
                    if (TryHandleItemAsRef(list, parentJob, listType)) return;

                    bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && listType != expectedType);

                    if (list.Count == 0)
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
                    var job = listStackJobRecycler.GetJob(parentJob, requiresItemNames ? CreateItemName(parentJob) : null, list);
                    job.Init(this, processor, list, writeTypeInfo);
                    AddJobToStack(job);
                };
                typeHandler.SetItemHandler(itemHandler, false);
            }
        }
    }

    

}
