using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {

        sealed class CachedTypeHandler
        {
            public readonly static MethodInfo setItemHandlerMethodInfo = typeof(CachedTypeHandler).GetMethod("SetItemHandler");
            private FeatureJsonSerializer serializer;
            private Delegate itemHandler;
            private ItemHandler<object> objectItemHandler;
            private Type handlerType;
            private bool isPrimitive;
            public byte[] preparedTypeInfo;

            public CachedTypeHandler(FeatureJsonSerializer serializer)
            {
                this.serializer = serializer;
            }

            public bool IsPrimitive => isPrimitive;

            public Type HandlerType => handlerType;

            public void SetItemHandler<T>(ItemHandler<T> itemHandler)
            {
                this.isPrimitive = false;
                this.handlerType = typeof(T);
                this.itemHandler = itemHandler;
                this.objectItemHandler = (item, expectedType, baseJob) => itemHandler.Invoke((T)item, expectedType, baseJob);
            }

            public void SetItemHandler<T>(PrimitiveItemHandler<T> itemHandler)
            {
                this.isPrimitive = true;
                this.handlerType = typeof(T);
                this.itemHandler = itemHandler;
                this.objectItemHandler = (item, _, _) => itemHandler.Invoke((T)item);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void HandleItem<T>(T item, ItemInfo itemInfo, Type expectedType)
            {                
                if (expectedType == handlerType)
                {
                    if (IsPrimitive)
                    {
                        HandlePrimitiveItem(item);
                    }
                    else
                    {
                        ItemHandler<T> typedItemHandler = (ItemHandler<T>)itemHandler;
                        typedItemHandler.Invoke(item, expectedType, itemInfo);
                    }
                }
                else
                {
                    objectItemHandler(item, expectedType, itemInfo);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void HandlePrimitiveItem<T>(T item)
            {
                PrimitiveItemHandler<T> typedItemHandler = (PrimitiveItemHandler<T>)itemHandler;
                typedItemHandler.Invoke(item);
            }
        }
    }

    sealed class CachedKeyWriter
    {
        private Delegate writerDelegateWithCopy;
        private Delegate writerDelegate;
        private bool skipCopy;

        public CachedKeyWriter(bool skipCopy)
        {
            this.skipCopy = skipCopy;
        }

        public bool HasMethod => writerDelegate != null;

        public void SetWriterMethod<T>(Func<T, SlicedBuffer<byte>.Slice> writerDelegate) => this.writerDelegateWithCopy = writerDelegate;
        public void SetWriterMethod<T>(Action<T> writerDelegate) => this.writerDelegate = writerDelegate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SlicedBuffer<byte>.Slice WriteKeyAsStringWithCopy<T>(T item)
        {
            if (skipCopy)
            {
                var write = (Action<T>)writerDelegate;
                write(item);
                return default;
            }
            else
            {
                var write = (Func<T, SlicedBuffer<byte>.Slice>)writerDelegateWithCopy;
                return write(item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteKeyAsString<T>(T item)
        {
            var write = (Action<T>)writerDelegate;
            write(item);
        }
    }
}
