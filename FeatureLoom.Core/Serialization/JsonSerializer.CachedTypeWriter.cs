using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization
{
    public sealed partial class JsonSerializer
    {

        public interface ICachedTypeHandler
        {
            void SetItemWriter<T>(Action<T, bool, ByteSegment> itemWriter, bool childrenMustWriteRefPath);
            Type HandlerType { get; }
            bool NoRefTypes { get; }
        }

        public sealed class CachedTypeWriter : ICachedTypeHandler
        {
            private JsonSerializer serializer;
            private JsonUTF8StreamWriter writer;
            private Delegate itemWriter; // (T item, bool deviatingType, ByteSegment itemName)
            private Action<object, bool, ByteSegment> objectItemWriter;
            private Type handlerType;
            private bool noRefTypes;
            public byte[] preparedTypeInfo;

            public CachedTypeWriter(JsonSerializer serializer, Type handlerType)
            {
                this.serializer = serializer;
                this.writer = serializer.writer;                
                this.handlerType = handlerType;
            }

            public bool NoRefTypes => noRefTypes;

            public Type HandlerType => handlerType;

            /// <summary>
            /// True if <see cref="WriteItem_NoCheck{T}"/> may be used instead of <see cref="WriteItem{T}"/>,
            /// i.e. the call type matches the handler type and no item info bookkeeping is required.
            /// Mirrors CachedTypeReader.IsNoCheckPossible on the deserializer side.
            /// </summary>
            public bool IsNoCheckPossible<T>() => typeof(T) == handlerType && !serializer.settings.requiresItemInfos;

            public void SetItemWriter<T>(Action<T, bool, ByteSegment> itemWriter, bool childrenMustWriteRefPath)
            {
                this.handlerType = typeof(T);
                this.noRefTypes = !childrenMustWriteRefPath && this.handlerType.IsValueType;
                this.itemWriter = itemWriter;
                this.objectItemWriter = (item, deviatingType, itemName) => itemWriter.Invoke((T)item, deviatingType, itemName);
            }

            internal void OverrideHandlerType(Type handlerType) => this.handlerType = handlerType;

            internal void ForceNoRefTypes() => this.noRefTypes = true;



            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteItem<T>(T item, ByteSegment fieldName)
            {
                if (item == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                Type callType = typeof(T);
                if (callType == handlerType)
                {
                    Action<T, bool, ByteSegment> typedItemWriter = (Action<T, bool, ByteSegment>)itemWriter;
                    typedItemWriter.Invoke(item, false, fieldName);
                }
                else
                {
                    objectItemWriter(item, true, fieldName);
                }
            }

            /// <summary>
            /// Writes an item without the null check, the call type check and without an item name.
            /// Only valid if <see cref="IsNoCheckPossible{T}"/> returned true and the item is not null.
            /// Mirrors CachedTypeReader.ReadValue_NoCheck on the deserializer side.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteItem_NoCheck<T>(T item)
            {
                Action<T, bool, ByteSegment> typedItemWriter = (Action<T, bool, ByteSegment>)itemWriter;
                typedItemWriter.Invoke(item, false, default);
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

        public void SetWriterMethod<T>(Func<T, ByteSegment> writerDelegate) => this.writerDelegateWithCopy = writerDelegate;
        public void SetWriterMethod<T>(Action<T> writerDelegate) => this.writerDelegate = writerDelegate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteKeyAsStringWithCopy<T>(T item)
        {
            if (skipCopy)
            {
                var write = (Action<T>)writerDelegate;
                write(item);
                return default;
            }
            else
            {
                var write = (Func<T, ByteSegment>)writerDelegateWithCopy;
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