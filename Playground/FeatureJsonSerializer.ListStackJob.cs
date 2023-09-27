﻿using System;
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
        private bool TryCreateListItemHandler(CachedTypeHandler typeHandler, Type itemType, byte[] preparedTypeInfo)
        {
            if (!itemType.TryGetTypeParamsOfGenericInterface(typeof(IList<>), out Type elementType)) return false;
            CachedTypeHandler elementHandler = GetCachedTypeHandler(elementType);

            string methodName = nameof(CreateIListItemHandler);
            MethodInfo createMethod = typeof(FeatureJsonSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo genericCreateMethod = createMethod.MakeGenericMethod(itemType, elementType);
            var itemHandler = genericCreateMethod.Invoke(this, new object[] { elementHandler, preparedTypeInfo });

            MethodInfo genericSetMethod = CachedTypeHandler.setItemHandlerMethodInfo.MakeGenericMethod(itemType);
            genericSetMethod.Invoke(typeHandler, new object[] { itemHandler, false });

            return true;
        }

        private ItemHandler<T> CreateIListItemHandler<T, E>(CachedTypeHandler elementHandler, byte[] preparedTypeInfo) where T : IList<E>
        {
            bool requiresItemNames = settings.RequiresItemNames;
            if (elementHandler.IsPrimitive && settings.AllowSkipStack)
            {
                ItemHandler<T> itemHandler = (list, expectedType, parentJob) =>
                {
                    Type listType = list.GetType();
                    if (TryHandleItemAsRef(list, parentJob, listType)) return;

                    bool writeTypeInfo = settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo ||
                        (settings.typeInfoHandling == TypeInfoHandling.AddDeviatingTypeInfo && listType != expectedType);
                    if (writeTypeInfo) PrepareTypeInfoObject(preparedTypeInfo);
                    writer.OpenCollection();
                    int currentIndex = 0;
                    if (currentIndex < list.Count)
                    {
                        E element = list[currentIndex++];
                        elementHandler.HandleItem(element, elementHandler.HandlerType, parentJob);
                    }
                    while (currentIndex < list.Count)
                    {
                        writer.WriteComma();
                        E element = list[currentIndex++];
                        elementHandler.HandleItem(element, elementHandler.HandlerType, parentJob);
                    }
                    writer.CloseCollection();
                    if (writeTypeInfo) FinishTypeInfoObject();
                };
                return itemHandler;
            }
            else
            {
                Func<ListStackJob, bool> processor = job =>
                {
                    int beforeStackSize = jobStack.Count;

                    IList<E> list = (IList<E>)job.list;
                    
                    if (job.currentIndex == 0)
                    {
                        if (job.writeTypeInfo) PrepareTypeInfoObject(preparedTypeInfo);

                        writer.OpenCollection();

                        E element = list[job.currentIndex];                        
                        if (element == null) writer.WriteNullValue();
                        else if (element.GetType() == typeof(E)) elementHandler.HandleItem(element, typeof(E), job.parentJob);
                        else GetCachedTypeHandler(element.GetType()).HandleItem(element, typeof(E), job.parentJob);                        
                        if (jobStack.Count != beforeStackSize) return false;
                    }

                    while (++job.currentIndex < list.Count)
                    {
                        writer.WriteComma();
                        E element = list[job.currentIndex];
                        if (element == null) writer.WriteNullValue();
                        else if (element.GetType() == typeof(E)) elementHandler.HandleItem(element, typeof(E), job.parentJob);
                        else GetCachedTypeHandler(element.GetType()).HandleItem(element, typeof(E), job.parentJob);
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
                        if (writeTypeInfo) PrepareTypeInfoObject(preparedTypeInfo);
                        writer.OpenCollection();
                        writer.CloseCollection();
                        if (writeTypeInfo) FinishTypeInfoObject();
                        return;
                    }
                    var job = listStackJobRecycler.GetJob(parentJob, requiresItemNames ? CreateItemName(parentJob) : null, list);
                    job.Init(this, processor, list, writeTypeInfo);
                    if (settings.AllowSkipStack && job.Process()) job.Recycle();
                    else AddJobToStack(job);
                };
                return itemHandler;
            }
        }
    }

    

}
