using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization;

public sealed partial class JsonSerializer
{
    /// <summary>
    /// Curated API for writing a single JSON value. Deliberately offers no structural tokens
    /// (braces, brackets, commas, field names), so a value handler cannot produce output that
    /// breaks the wrapper the serializer builds around it.
    /// </summary>
    public sealed class ValueWriteApi
    {
        internal readonly JsonUTF8StreamWriter writer;

        internal ValueWriteApi(JsonUTF8StreamWriter writer) => this.writer = writer;

        public void WriteNull() => writer.WriteNullValue();
        public void WriteString(string value) => writer.WriteStringValue(value);
        public void WriteString(TextSegment value) => writer.WriteTextSegmentValue(value);
#if !NETSTANDARD2_0
        public void WriteString(ReadOnlySpan<char> value) => writer.WriteStringValue(value);
#endif
        public void WriteBool(bool value) => writer.WriteBoolValue(value);
        public void WriteInt(int value) => writer.WriteIntValue(value);
        public void WriteLong(long value) => writer.WriteLongValue(value);
        public void WriteUlong(ulong value) => writer.WriteUlongValue(value);
        public void WriteDouble(double value) => writer.WriteDoubleValue(value);
        public void WriteFloat(float value) => writer.WriteFloatValue(value);
        public void WriteDecimal(decimal value) => writer.WriteDecimalValue(value);
        public void WriteGuid(Guid value) => writer.WriteGuidValue(value);
        public void WriteDateTime(DateTime value) => writer.WriteDateTimeValue(value);

        /// <summary>
        /// Writes an already formatted JSON fragment. The caller is responsible for it being
        /// valid JSON; nothing is escaped or validated.
        /// </summary>
        public void WriteRawJson(string json) => writer.WriteRawJsonFragment(json);

        /// <inheritdoc cref="WriteRawJson(string)"/>
        public void WriteRawJson(JsonFragment json) => writer.WriteRawJsonFragment(json);

        /// <inheritdoc cref="WriteRawJson(string)"/>
        public void WriteRawJson(TextSegment json) => writer.WriteRawJsonFragment(json);

#if !NETSTANDARD2_0
        /// <inheritdoc cref="WriteRawJson(string)"/>
        public void WriteRawJson(ReadOnlySpan<char> json) => writer.WriteRawJsonFragment(json);
#endif
    }

    /// <summary>
    /// Escape hatch API for handlers that need full control over the emitted tokens, including
    /// structure. Everything the serializer would normally guarantee (balanced braces, correct
    /// separators) becomes the handler's responsibility.
    /// </summary>
    public sealed class RawWriteApi
    {
        internal readonly JsonUTF8StreamWriter writer;

        internal RawWriteApi(JsonUTF8StreamWriter writer) => this.writer = writer;

        public void OpenObject() => writer.OpenObject();
        public void CloseObject() => writer.CloseObject();
        public void OpenArray() => writer.OpenArray();
        public void CloseArray() => writer.CloseArray();
        public void WriteComma() => writer.WriteComma();
        public void WriteColon() => writer.WriteColon();
        public void WriteFieldName(string fieldName) => writer.WriteFieldName(fieldName);
        public void WriteFieldName(TextSegment fieldName) => writer.WriteFieldName(fieldName);
#if !NETSTANDARD2_0
        public void WriteFieldName(ReadOnlySpan<char> fieldName) => writer.WriteFieldName(fieldName);
#endif

        /// <summary>
        /// Writes a field name (including the trailing colon) that was encoded once via
        /// <see cref="WriterPreparationApi.PrepareFieldName(string)"/>.
        /// </summary>
        public void WritePrepared(byte[] preparedBytes) => writer.WritePreparedBytes(preparedBytes);

        /// <inheritdoc cref="WritePrepared(byte[])"/>
        public void WritePrepared(ByteSegment preparedBytes) => writer.WritePreparedBytes(preparedBytes);

#if !NETSTANDARD2_0
        /// <inheritdoc cref="WritePrepared(byte[])"/>
        public void WritePrepared(ReadOnlySpan<byte> preparedBytes) => writer.WritePreparedBytes(preparedBytes);
#endif

        public void WriteNull() => writer.WriteNullValue();
        public void WriteString(string value) => writer.WriteStringValue(value);
        public void WriteString(TextSegment value) => writer.WriteTextSegmentValue(value);
#if !NETSTANDARD2_0
        public void WriteString(ReadOnlySpan<char> value) => writer.WriteStringValue(value);
#endif
        public void WriteBool(bool value) => writer.WriteBoolValue(value);
        public void WriteInt(int value) => writer.WriteIntValue(value);
        public void WriteUint(uint value) => writer.WriteUintValue(value);
        public void WriteLong(long value) => writer.WriteLongValue(value);
        public void WriteUlong(ulong value) => writer.WriteUlongValue(value);
        public void WriteShort(short value) => writer.WriteShortValue(value);
        public void WriteUshort(ushort value) => writer.WriteUshortValue(value);
        public void WriteByte(byte value) => writer.WriteByteValue(value);
        public void WriteSbyte(sbyte value) => writer.WriteSbyteValue(value);
        public void WriteDouble(double value) => writer.WriteDoubleValue(value);
        public void WriteFloat(float value) => writer.WriteFloatValue(value);
        public void WriteDecimal(decimal value) => writer.WriteDecimalValue(value);
        public void WriteGuid(Guid value) => writer.WriteGuidValue(value);
        public void WriteDateTime(DateTime value) => writer.WriteDateTimeValue(value);

        /// <summary>
        /// Writes an already formatted JSON fragment. The caller is responsible for it being
        /// valid JSON; nothing is escaped or validated.
        /// </summary>
        public void WriteRawJson(string json) => writer.WriteRawJsonFragment(json);

        /// <inheritdoc cref="WriteRawJson(string)"/>
        public void WriteRawJson(JsonFragment json) => writer.WriteRawJsonFragment(json);

        /// <inheritdoc cref="WriteRawJson(string)"/>
        public void WriteRawJson(TextSegment json) => writer.WriteRawJsonFragment(json);

#if !NETSTANDARD2_0
        /// <inheritdoc cref="WriteRawJson(string)"/>
        public void WriteRawJson(ReadOnlySpan<char> json) => writer.WriteRawJsonFragment(json);
#endif
    }

    /// <summary>
    /// Phase-1 API for building a custom type writer.
    /// <para>
    /// Every method prepares a writer that is used later for each value of the type. Anything
    /// done here happens once per type; anything done in the returned delegate happens per
    /// value. Moving work into this phase is what makes a custom writer fast.
    /// </para>
    /// </summary>
    public sealed class WriterPreparationApi
    {
        readonly JsonSerializer serializer;

        internal WriterPreparationApi(JsonSerializer serializer) => this.serializer = serializer;

        internal JsonSerializer Serializer => serializer;

        /// <summary>
        /// Encodes a field name (including the trailing colon) once, so it can be emitted with a
        /// single buffer copy via <see cref="RawWriteApi.WritePrepared(byte[])"/>.
        /// </summary>
        public byte[] PrepareFieldName(string fieldName)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            return serializer.writer.PrepareFieldNameBytes(fieldName);
        }

        /// <summary>
        /// Encodes a constant JSON fragment as UTF-8 once, so writing it later is a plain buffer
        /// copy instead of a transcoding pass.
        /// </summary>
        /// <remarks>
        /// The returned fragment is deliberately materialized as UTF-8 here: <see cref="JsonFragment"/>
        /// converts lazily and caches into itself, which a copied struct would silently redo on
        /// every write.
        /// </remarks>
        public JsonFragment PrepareRawJson(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var fragment = new JsonFragment(json);
            _ = fragment.JsonUtf8;
            return fragment;
        }

        /// <summary>
        /// Prepares a writer that represents the value as a single JSON value, e.g. a string or
        /// a number. Such a value has no children, so it can never contain a reference.
        /// </summary>
        public CustomWriter<T> PrepareValueWriter<T>(Action<ValueWriteApi, T> write)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            var api = new ValueWriteApi(serializer.writer);
            return new CustomWriter<T>(item => write(api, item), CustomWriterShape.Value, childrenMayContainRefs: false);
        }

        /// <summary>
        /// Prepares a writer that represents the value as a JSON object built from declared
        /// fields. The serializer emits the braces and separators.
        /// </summary>
        public CustomWriter<T> PrepareObjectWriter<T>(Action<ObjectWriterBuilder<T>> build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            var builder = new ObjectWriterBuilder<T>(serializer);
            build(builder);
            return builder.Build();
        }

        /// <summary>
        /// Prepares a writer that represents the value as a JSON array of <typeparamref name="TItem"/>.
        /// Each item is written by the serializer's writer for <typeparamref name="TItem"/>, so
        /// nested custom writers and settings still apply. The serializer emits the brackets and
        /// separators.
        /// </summary>
        public CustomWriter<T> PrepareArrayWriter<T, TItem>(Func<T, IEnumerable<TItem>> getItems)
        {
            if (getItems == null) throw new ArgumentNullException(nameof(getItems));

            var itemWriter = serializer.GetCachedTypeWriter(typeof(TItem));
            return CreateArrayWriter(getItems, element => itemWriter.WriteItem(element, default), !itemWriter.NoRefTypes);
        }

        /// <summary>
        /// Prepares a writer that represents the value as a JSON array whose items are written as
        /// objects built from declared fields. A null item is written as the JSON null literal.
        /// </summary>
        public CustomWriter<T> PrepareArrayWriter<T, TItem>(Func<T, IEnumerable<TItem>> getItems, Action<ObjectWriterBuilder<TItem>> build)
        {
            if (getItems == null) throw new ArgumentNullException(nameof(getItems));
            if (build == null) throw new ArgumentNullException(nameof(build));

            var nested = new ObjectWriterBuilder<TItem>(serializer);
            build(nested);
            var writeNested = nested.Build().writeBody;
            var w = serializer.writer;
            bool canBeNull = ObjectWriterBuilder<TItem>.CanBeNull(typeof(TItem));

            return CreateArrayWriter(getItems, element =>
            {
                if (canBeNull && element == null)
                {
                    w.WriteNullValue();
                    return;
                }
                w.OpenObject();
                writeNested(element);
                w.CloseObject();
            }, nested.ChildrenMayContainRefs);
        }

        /// <summary>
        /// Prepares a writer that represents the value as a JSON array whose items are written
        /// with full control over the emitted tokens. The serializer still emits the brackets and
        /// separators, so the action must write exactly one JSON value per item.
        /// </summary>
        public CustomWriter<T> PrepareArrayWriter<T, TItem>(Func<T, IEnumerable<TItem>> getItems, Action<RawWriteApi, TItem> writeItem)
        {
            if (getItems == null) throw new ArgumentNullException(nameof(getItems));
            if (writeItem == null) throw new ArgumentNullException(nameof(writeItem));

            var api = new RawWriteApi(serializer.writer);
            return CreateArrayWriter(getItems, element => writeItem(api, element), itemsMayContainRefs: true);
        }

        CustomWriter<T> CreateArrayWriter<T, TItem>(Func<T, IEnumerable<TItem>> getItems, Action<TItem> writeItem, bool itemsMayContainRefs)
        {
            var w = serializer.writer;

            void WriteItems(T item)
            {
                var items = getItems(item);
                if (items == null) return;
                bool first = true;
                foreach (var element in items)
                {
                    if (!first) w.WriteComma();
                    first = false;
                    writeItem(element);
                }
            }

            return new CustomWriter<T>(WriteItems, CustomWriterShape.Array, itemsMayContainRefs);
        }

        /// <summary>
        /// Prepares a writer with full control over the emitted tokens. Use only when the value,
        /// object and array builders cannot express the desired output: the serializer can make
        /// no assumptions about the result, so it must conservatively assume that written
        /// children may contain references.
        /// </summary>
        public CustomWriter<T> PrepareRawWriter<T>(Action<RawWriteApi, T> write)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            var api = new RawWriteApi(serializer.writer);
            return new CustomWriter<T>(item => write(api, item), CustomWriterShape.Raw, childrenMayContainRefs: true);
        }

        /// <summary>
        /// Resolves the writer for another type once, so a custom handler can delegate nested
        /// values without paying for a lookup per value.
        /// </summary>
        public Action<TOther> PrepareTypeWriter<TOther>()
        {
            var typeWriter = serializer.GetCachedTypeWriter(typeof(TOther));
            return item => typeWriter.WriteItem(item, default);
        }

        /// <summary>
        /// Resolves the writer for another type once, using settings that deviate from the ones
        /// configured for that type, e.g. a different type info handling or an own custom writer.
        /// <para>
        /// The resulting writer is local to this preparation and is not shared via the per-type
        /// cache, so the deviating settings cannot leak into other usages of that type.
        /// </para>
        /// </summary>
        /// <param name="configure">Callback that applies the deviating settings.</param>
        public Action<TOther> PrepareTypeWriter<TOther>(Action<TypeWriteSettings<TOther>> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var typeSettings = new TypeWriteSettings<TOther>();
            configure(typeSettings);
            var typeWriter = serializer.GetCachedTypeWriter(typeof(TOther), typeSettings);
            return item => typeWriter.WriteItem(item, default);
        }
    }

    /// <summary>
    /// Declares the fields of a custom object writer. Field names are encoded once here, so
    /// writing a value only copies prepared bytes.
    /// <para>
    /// Fields are written in declaration order. Declaring them in the order they are most often
    /// read does not change correctness, but keeps the produced JSON stable and comparable.
    /// </para>
    /// </summary>
    public sealed class ObjectWriterBuilder<T>
    {
        readonly JsonSerializer serializer;
        readonly List<Action<T>> fieldWriters = new();
        bool childrenMayContainRefs;

        internal ObjectWriterBuilder(JsonSerializer serializer) => this.serializer = serializer;

        /// <summary>
        /// Adds a field whose value is written by the serializer's writer for
        /// <typeparamref name="TValue"/>, so nested custom writers and settings still apply.
        /// </summary>
        public ObjectWriterBuilder<T> AddField<TValue>(string fieldName, Func<T, TValue> getValue)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            if (getValue == null) throw new ArgumentNullException(nameof(getValue));

            byte[] preparedName = PrepareName(fieldName);
            var valueWriter = serializer.GetCachedTypeWriter(typeof(TValue));
            if (!valueWriter.NoRefTypes) childrenMayContainRefs = true;
            var w = serializer.writer;

            fieldWriters.Add(item =>
            {
                w.WritePreparedBytes(preparedName);
                valueWriter.WriteItem(getValue(item), default);
            });
            return this;
        }

        /// <summary>
        /// Adds a field whose value is written with full control over the emitted tokens. Use only
        /// when the other field methods cannot express the desired output: the serializer can make
        /// no assumptions about the result, so it must conservatively assume that written children
        /// may contain references. The action must write exactly one JSON value.
        /// </summary>
        public ObjectWriterBuilder<T> AddRawField(string fieldName, Action<RawWriteApi, T> writeRaw)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            if (writeRaw == null) throw new ArgumentNullException(nameof(writeRaw));

            byte[] preparedName = PrepareName(fieldName);
            var w = serializer.writer;
            var api = new RawWriteApi(w);
            childrenMayContainRefs = true;

            fieldWriters.Add(item =>
            {
                w.WritePreparedBytes(preparedName);
                writeRaw(api, item);
            });
            return this;
        }

        /// <summary>
        /// Adds a field that is written as a nested JSON object, built with its own field
        /// declarations. The serializer emits the braces and separators; a null value is written
        /// as the JSON null literal.
        /// </summary>
        public ObjectWriterBuilder<T> AddObject<TValue>(string fieldName, Func<T, TValue> getValue, Action<ObjectWriterBuilder<TValue>> build)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            if (getValue == null) throw new ArgumentNullException(nameof(getValue));
            if (build == null) throw new ArgumentNullException(nameof(build));

            byte[] preparedName = PrepareName(fieldName);
            var nested = new ObjectWriterBuilder<TValue>(serializer);
            build(nested);
            var writeNested = nested.Build().writeBody;
            if (nested.childrenMayContainRefs) childrenMayContainRefs = true;
            var w = serializer.writer;
            bool canBeNull = CanBeNull(typeof(TValue));

            fieldWriters.Add(item =>
            {
                w.WritePreparedBytes(preparedName);
                var value = getValue(item);
                if (canBeNull && value == null)
                {
                    w.WriteNullValue();
                    return;
                }
                w.OpenObject();
                writeNested(value);
                w.CloseObject();
            });
            return this;
        }

        /// <summary>
        /// Adds a field that is written as a JSON array. Each item is written by the serializer's
        /// writer for <typeparamref name="TItem"/>, so nested custom writers and settings still
        /// apply. A null collection is written as the JSON null literal.
        /// </summary>
        public ObjectWriterBuilder<T> AddArray<TItem>(string fieldName, Func<T, IEnumerable<TItem>> getItems)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            if (getItems == null) throw new ArgumentNullException(nameof(getItems));

            byte[] preparedName = PrepareName(fieldName);
            var itemWriter = serializer.GetCachedTypeWriter(typeof(TItem));
            if (!itemWriter.NoRefTypes) childrenMayContainRefs = true;
            var w = serializer.writer;

            AddArrayField(preparedName, w, getItems, element => itemWriter.WriteItem(element, default));
            return this;
        }

        /// <summary>
        /// Adds a field that is written as a JSON array of nested objects, each built with its own
        /// field declarations. A null collection is written as the JSON null literal.
        /// </summary>
        public ObjectWriterBuilder<T> AddArray<TItem>(string fieldName, Func<T, IEnumerable<TItem>> getItems, Action<ObjectWriterBuilder<TItem>> build)
        {
            if (fieldName == null) throw new ArgumentNullException(nameof(fieldName));
            if (getItems == null) throw new ArgumentNullException(nameof(getItems));
            if (build == null) throw new ArgumentNullException(nameof(build));

            byte[] preparedName = PrepareName(fieldName);
            var nested = new ObjectWriterBuilder<TItem>(serializer);
            build(nested);
            var writeNested = nested.Build().writeBody;
            if (nested.childrenMayContainRefs) childrenMayContainRefs = true;
            var w = serializer.writer;
            bool canBeNull = CanBeNull(typeof(TItem));

            AddArrayField(preparedName, w, getItems, element =>
            {
                if (canBeNull && element == null)
                {
                    w.WriteNullValue();
                    return;
                }
                w.OpenObject();
                writeNested(element);
                w.CloseObject();
            });
            return this;
        }

        void AddArrayField<TItem>(byte[] preparedName, JsonUTF8StreamWriter w, Func<T, IEnumerable<TItem>> getItems, Action<TItem> writeItem)
        {
            fieldWriters.Add(item =>
            {
                w.WritePreparedBytes(preparedName);
                var items = getItems(item);
                if (items == null)
                {
                    w.WriteNullValue();
                    return;
                }
                w.OpenArray();
                bool first = true;
                foreach (var element in items)
                {
                    if (!first) w.WriteComma();
                    first = false;
                    writeItem(element);
                }
                w.CloseArray();
            });
        }

        static internal bool CanBeNull(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

        internal bool ChildrenMayContainRefs => childrenMayContainRefs;

        /// <summary>
        /// Encodes the field name, including its colon and, for all but the first field, the
        /// separating comma, so it can be emitted with a single buffer copy.
        /// </summary>
        byte[] PrepareName(string fieldName)
        {
            byte[] nameAndColon = serializer.writer.PrepareFieldNameBytes(fieldName);
            if (fieldWriters.Count == 0) return nameAndColon;

            var withComma = new byte[nameAndColon.Length + 1];
            withComma[0] = (byte)',';
            Array.Copy(nameAndColon, 0, withComma, 1, nameAndColon.Length);
            return withComma;
        }

        internal CustomWriter<T> Build()
        {
            var writers = fieldWriters.ToArray();
            Action<T> writeBody;
            switch (writers.Length)
            {
                case 0: writeBody = _ => { }; break;
                case 1:
                    var single = writers[0];
                    writeBody = single;
                    break;
                default:
                    writeBody = item =>
                    {
                        for (int i = 0; i < writers.Length; i++) writers[i](item);
                    };
                    break;
            }
            return new CustomWriter<T>(writeBody, CustomWriterShape.Object, childrenMayContainRefs);
        }
    }

    /// <summary>Output shape a custom writer produces, which determines the wrapper around it.</summary>
    internal enum CustomWriterShape
    {
        Value,
        Object,
        Array,
        Raw
    }

    /// <summary>
    /// Result of a phase-1 preparation: the per-value write action plus the facts the serializer
    /// needs to wrap it correctly. Created only through <see cref="WriterPreparationApi"/>.
    /// </summary>
    public sealed class CustomWriter<T>
    {
        internal readonly Action<T> writeBody;
        internal readonly CustomWriterShape shape;
        internal readonly bool childrenMayContainRefs;

        internal CustomWriter(Action<T> writeBody, CustomWriterShape shape, bool childrenMayContainRefs)
        {
            this.writeBody = writeBody;
            this.shape = shape;
            this.childrenMayContainRefs = childrenMayContainRefs;
        }
    }

    /// <summary>
    /// Registered custom writer: matches types and runs the phase-1 preparation for them.
    /// Matching happens once per type, when the type's writer is created, never per value.
    /// </summary>
    internal sealed class CustomTypeWriterCreator<T> : ITypeHandlerCreator
    {
        readonly Func<WriterPreparationApi, CustomWriter<T>> prepare;
        readonly Func<Type, bool> supportsType;

        /// <summary>
        /// Exact type registration, stored in the type settings and found by direct lookup.
        /// </summary>
        internal CustomTypeWriterCreator(Func<WriterPreparationApi, CustomWriter<T>> prepare)
        {
            this.prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
        }

        /// <summary>
        /// Predicate registration, matched by scanning the registered convention based handlers.
        /// </summary>
        internal CustomTypeWriterCreator(Func<WriterPreparationApi, CustomWriter<T>> prepare, Func<Type, bool> supportsType)
        {
            this.prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
            this.supportsType = supportsType ?? throw new ArgumentNullException(nameof(supportsType));
        }

        public bool SupportsType(Type type)
        {
            if (supportsType != null) return supportsType(type);
            return typeof(T) == type;
        }

        public void CreateTypeHandler(JsonSerializer serializer, CachedTypeWriter typeWriter, Type type)
        {
            if (!type.IsAssignableTo(typeof(T))) throw new ArgumentException($"The custom writer for type {typeof(T).FullName} is not compatible with the actual item type {type.FullName}");

            var customWriter = prepare(new WriterPreparationApi(serializer));
            serializer.ApplyCustomWriter(typeWriter, customWriter, type);
        }
    }

    /// <summary>
    /// Wraps the prepared body according to its shape and applies it to the cached type writer.
    /// The shape is what tells the serializer how the value must be framed (type info object,
    /// braces, brackets) and whether written children can contain references.
    /// </summary>
    internal void ApplyCustomWriter<T>(CachedTypeWriter typeWriter, CustomWriter<T> customWriter, Type handlerType)
    {
        switch (customWriter.shape)
        {
            case CustomWriterShape.Value:
                typeWriter.SetItemWriter(CreatePrimitiveItemWriter(typeWriter, customWriter.writeBody), false);
                // A single value has no children, so it can never contain a reference path,
                // even when the handled type itself is a reference type (e.g. string).
                typeWriter.ForceNoRefTypes();
                break;
            case CustomWriterShape.Object:
                typeWriter.SetItemWriter(CreateObjectItemWriter(typeWriter, customWriter.writeBody), customWriter.childrenMayContainRefs);
                break;
            case CustomWriterShape.Array:
                typeWriter.SetItemWriter(CreateArrayItemWriter(typeWriter, customWriter.writeBody), customWriter.childrenMayContainRefs);
                break;
            case CustomWriterShape.Raw:
                typeWriter.SetItemWriter(CreatePrimitiveItemWriter(typeWriter, customWriter.writeBody), customWriter.childrenMayContainRefs);
                break;
        }
        typeWriter.OverrideHandlerType(handlerType);
    }
}
