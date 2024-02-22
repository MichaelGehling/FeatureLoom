using System;
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

            public void SetItemHandler<T>(ItemHandler<T> itemHandler, bool isPrimitive)
            {
                this.isPrimitive = isPrimitive;
                this.handlerType = typeof(T);
                this.itemHandler = itemHandler;
                this.objectItemHandler = (item, expectedType, baseJob) => itemHandler.Invoke((T)item, expectedType, baseJob);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void HandleItem<T>(T item, ItemInfo itemInfo)
            {
                Type type = typeof(T);
                if (type == handlerType)
                {
                    ItemHandler<T> typedItemHandler = (ItemHandler<T>)itemHandler;
                    typedItemHandler.Invoke(item, type, itemInfo);
                }
                else
                {
                    objectItemHandler(item, type, itemInfo);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void HandlePrimitiveItem<T>(T item)
            {
                ItemHandler<T> typedItemHandler = (ItemHandler<T>)itemHandler;
                typedItemHandler.Invoke(item, typeof(T), null);
            }
        }
    }

    sealed class CachedStringValueWriter
    {
        private Delegate writerDelegate;

        public bool HasMethod => writerDelegate != null;

        public void SetWriterMethod<T>(Action<T> writerDelegate) => this.writerDelegate = writerDelegate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteValueAsString<T>(T item)
        {
            var write = (Action<T>)writerDelegate;
            write(item);
        }
    }
}
