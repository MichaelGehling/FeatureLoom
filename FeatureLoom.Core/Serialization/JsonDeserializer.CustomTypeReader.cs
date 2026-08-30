using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{
    /// <summary>Controls handling of object fields not declared by an <see cref="ObjectReaderBuilder{T}"/>.</summary>
    public enum UnknownFieldPolicy
    {
        /// <summary>Skip the unknown field value.</summary>
        Skip,
        /// <summary>Throw when an unknown field is encountered.</summary>
        Throw
    }

    /// <summary>Builds a custom object reader during reader preparation.</summary>
    /// <typeparam name="T">Object type read by the resulting reader.</typeparam>
    public sealed class ObjectReaderBuilder<T>
    {
        readonly JsonDeserializer deserializer;
        readonly BaseTypeSettings typeSettings;
        readonly List<(ByteSegment name, Func<T, T> reader)> fieldReaders = new();
        Func<BufferSegment, T, T> dynamicFieldReader;
        UnknownFieldPolicy? unknownFieldPolicy;

        internal ObjectReaderBuilder(JsonDeserializer deserializer, BaseTypeSettings typeSettings)
        {
            this.deserializer = deserializer;
            this.typeSettings = typeSettings;
        }

        /// <summary>Adds a named field using the configured reader for <typeparamref name="TValue"/>.</summary>
        public ObjectReaderBuilder<T> AddField<TValue>(string name, Func<T, TValue, T> setValue) =>
            AddField(name, setValue, null);

        /// <summary>Adds a named field using context-local settings for <typeparamref name="TValue"/>.</summary>
        public ObjectReaderBuilder<T> AddField<TValue>(string name, Func<T, TValue, T> setValue, Action<TypeSettings<TValue>> configure)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (setValue == null) throw new ArgumentNullException(nameof(setValue));

            BaseTypeSettings localSettings = null;
            if (configure != null)
            {
                var settings = new TypeSettings<TValue>();
                configure(settings);
                localSettings = settings;
            }
            var valueReader = deserializer.GetCachedTypeReader(typeof(TValue), localSettings);
            ByteSegment preparedName = new ByteSegment(name.ToByteArray(), true);
            fieldReaders.Add((preparedName, item => setValue(item, valueReader.ReadFieldValue<TValue>(preparedName))));
            return this;
        }

        /// <summary>
        /// Adds the normal configured members of <typeparamref name="T"/>, including access, attributes,
        /// name overrides, member settings, recursive settings, population, and reference handling.
        /// </summary>
        public ObjectReaderBuilder<T> AddExistingFields()
        {
            var existing = deserializer.CreateExistingObjectFieldReaders<T>(typeSettings);
            fieldReaders.AddRange(existing.fieldReaders);
            return this;
        }

        /// <summary>
        /// Handles fields not declared by <see cref="AddField{TValue}(string, Func{T, TValue, T})"/> or
        /// <see cref="AddExistingFields"/>. The field name is a temporary buffer view; the callback must read its value.
        /// </summary>
        public ObjectReaderBuilder<T> AddDynamicFields(Func<BufferSegment, T, T> readField)
        {
            dynamicFieldReader = readField ?? throw new ArgumentNullException(nameof(readField));
            return this;
        }

        /// <summary>Sets handling for unmatched fields when no dynamic-field reader is configured.</summary>
        public ObjectReaderBuilder<T> SetUnknownFieldPolicy(UnknownFieldPolicy policy)
        {
            unknownFieldPolicy = policy;
            return this;
        }

        internal Func<ExtensionApi, T, T> Build()
        {
            var readers = fieldReaders.ToArray();
            var lookup = new Dictionary<ByteSegment, Func<T, T>>();
            foreach (var entry in readers) lookup[entry.name] = entry.reader;
            var dynamicReader = dynamicFieldReader;
            var policy = unknownFieldPolicy ?? typeSettings?.unknownFieldPolicy ?? deserializer.settings.unknownFieldPolicy;
            T ReadObject(ExtensionApi api, T item)
            {
                if (api.TryReadNullValue()) return default;
                if (api.SkipWhiteSpaces() != (byte)'{') throw new Exception("Failed reading object");
                api.TryNextByte();
                int expectedIndex = 0;
                while (true)
                {
                    byte current = api.SkipWhiteSpaces();
                    if (current == (byte)'}') break;
                    ByteSegment fieldName = deserializer.ReadStringBytes();
                    if (api.SkipWhiteSpaces() != (byte)':') throw new Exception("Failed reading object");
                    api.TryNextByte();

                    Func<T, T> reader = null;
                    if (readers.Length > 0)
                    {
                        var expected = readers[expectedIndex];
                        if (expected.name == fieldName)
                        {
                            reader = expected.reader;
                            expectedIndex = (expectedIndex + 1) % readers.Length;
                        }
                        else
                        {
                            fieldName.EnsureHashCode();
                            lookup.TryGetValue(fieldName, out reader);
                        }
                    }

                    if (reader != null) item = reader(item);
                    else if (dynamicReader != null) item = dynamicReader(new BufferSegment(fieldName), item);
                    else if (policy == UnknownFieldPolicy.Throw) throw new Exception($"Unknown field '{fieldName}'.");
                    else api.SkipNextValue();

                    current = api.SkipWhiteSpaces();
                    if (current == (byte)',') api.TryNextByte();
                }
                api.TryNextByte();
                return item;
            }

            return ReadObject;
        }
    }
}
