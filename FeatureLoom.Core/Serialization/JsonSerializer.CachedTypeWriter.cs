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

        public sealed class CachedTypeWriter
        {
            private JsonSerializer serializer;
            private JsonUTF8StreamWriter writer;
            private Delegate itemWriter; // (T item, bool deviatingType, ByteSegment itemName)
            private Action<object, bool, ByteSegment> objectItemWriter;
            private Type handlerType;
            private bool noRefTypes;
            public byte[] preparedTypeInfo;

            /// <summary>
            /// Type info handling for this type, resolved once from the global setting and the
            /// optional per-type override. Because a writer is cached per type, this can be
            /// decided at creation time and never has to be resolved on the write path.
            /// </summary>
            public readonly TypeInfoHandling typeInfoHandling;

            /// <summary>
            /// Type info layout for this type scope, resolved once from the global setting and the
            /// optional per-type/member override.
            /// </summary>
            public readonly TypeInfoFormat typeInfoFormat;

            /// <summary>
            /// True if type info must be omitted where it would require an envelope, i.e. for
            /// arrays and primitives. Resolved from <see cref="typeInfoFormat"/>.
            /// </summary>
            public readonly bool skipTypeInfoEnvelope;

            /// <summary>
            /// True if an array wrapped in a type info envelope uses "$values" instead of "$value".
            /// </summary>
            public readonly bool useValuesFieldName;

            /// <summary>
            /// The type settings this writer was built with: either the settings configured for
            /// <see cref="HandlerType"/>, or a locally overriding set (e.g. member settings).
            /// May be <see langword="null"/> when nothing is configured.
            /// <para>
            /// Nested creation steps resolve from this instead of looking the settings up by type
            /// again, which is what makes context-local variants possible. Mirrors
            /// <c>CachedTypeReader.TypeSettings</c>.
            /// </para>
            /// </summary>
            public BaseTypeWriteSettings TypeSettings => typeSettings;
            private readonly BaseTypeWriteSettings typeSettings;

            public CachedTypeWriter(JsonSerializer serializer, Type handlerType, BaseTypeWriteSettings typeSettings = null)
            {
                this.serializer = serializer;
                this.writer = serializer.writer;                
                this.handlerType = handlerType;
                this.typeSettings = typeSettings;
                this.typeInfoHandling = serializer.settings.ResolveTypeInfoHandling(typeSettings);
                this.typeInfoFormat = serializer.settings.ResolveTypeInfoFormat(typeSettings);
                this.skipTypeInfoEnvelope = this.typeInfoFormat == TypeInfoFormat.OnlyInlineForObjects;
                this.useValuesFieldName = serializer.settings.ResolveArrayValueFieldName(typeSettings) == ValueFieldName.Values;
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

            /// <summary>
            /// Writes a boxed item with a writer that was already resolved from the item's runtime
            /// type, so it is not a type deviation and must not produce a $type/$value envelope.
            /// Used where the value type is only known at write time, e.g. dynamic fields.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void WriteItemOfRuntimeType(object item, ByteSegment itemName)
                => objectItemWriter(item, false, itemName);



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
                else if (NullableInfo<T>.underlyingType == handlerType)
                {
                    // The call type is the nullable version of the handler type, e.g. int? handled
                    // by the int handler. Boxing a Nullable<T> yields the underlying type, so this
                    // is not a type deviation and must not produce a $type/$value envelope.
                    objectItemWriter(item, false, fieldName);
                }
                else
                {
                    objectItemWriter(item, true, fieldName);
                }
            }

            /// <summary>
            /// Caches the underlying type of a nullable value type per generic instantiation, so
            /// the nullable check in <see cref="WriteItem{T}"/> is a plain reference comparison
            /// against a static readonly field instead of a repeated reflection call.
            /// </summary>
            private static class NullableInfo<T>
            {
                public static readonly Type underlyingType = Nullable.GetUnderlyingType(typeof(T));
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

    /// <summary>
    /// Holds the delegates that write a dictionary key as a JSON string.
    /// It is only used while a dictionary type handler is created, never on the write path
    /// itself: the handler resolves the typed delegate once via <see cref="GetWriter{T}"/> or
    /// <see cref="GetWriterWithCopy{T}"/> and calls it directly afterwards. That keeps the
    /// delegate cast and the mode selection out of the per-entry loop.
    /// </summary>
    sealed class CachedKeyWriter
    {
        private Delegate writerDelegateWithCopy;
        private Delegate writerDelegate;

        public bool HasMethod => writerDelegate != null;

        /// <summary>
        /// True if a writer is available that also returns the written key, which is required
        /// when keys are used as item names for reference handling.
        /// </summary>
        public bool HasMethodWithCopy => writerDelegateWithCopy != null;

        public void SetWriterWithCopyMethod<T>(Func<T, ByteSegment> writerDelegate) => this.writerDelegateWithCopy = writerDelegate;
        public void SetWriterMethod<T>(Action<T> writerDelegate) => this.writerDelegate = writerDelegate;

        /// <summary>
        /// Returns the writer that writes the key without providing a copy of it.
        /// </summary>
        public Action<T> GetWriter<T>() => (Action<T>)writerDelegate;

        /// <summary>
        /// Returns the writer that writes the key and returns it as a copy, so it stays valid
        /// after the write buffer moved on.
        /// </summary>
        public Func<T, ByteSegment> GetWriterWithCopy<T>() => (Func<T, ByteSegment>)writerDelegateWithCopy;
    }
}
