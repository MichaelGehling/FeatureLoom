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
            private Delegate itemHandler;
            private ItemHandler<object> objectItemHandler;
            private Type handlerType;
            private bool isPrimitive;
            public byte[] preparedTypeInfo;
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
            public void HandleItem<T>(T item, Type expectedType, StackJob parentJob)
            {
                if (typeof(T) == handlerType)
                {
                    ItemHandler<T> typedItemHandler = (ItemHandler<T>)itemHandler;
                    typedItemHandler.Invoke(item, expectedType, parentJob);
                }
                else
                {
                    objectItemHandler(item, expectedType, parentJob);
                }
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
