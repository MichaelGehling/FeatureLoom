using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{
    PreparationApi preparationApi;
    ExtensionApi extensionApi;

    public sealed class PreparationApi
    {
        readonly JsonDeserializer deserializer;
        BaseTypeSettings currentTypeSettings;
        public PreparationApi(JsonDeserializer deserializer)
        {
            this.deserializer = deserializer;
        }

        public Func<T,T> PrepareTypeReader<T>(TypeSettings<T> typeSettingsOverride = null)
        {
            if (typeSettingsOverride == null)
            {
                var typeReader = deserializer.GetCachedTypeReader(typeof(T));
                if (typeReader.IsNoCheckPossible<T>()) return (itemToPopulate) => typeReader.ReadValue_NoCheck<T>(itemToPopulate);
                else return (itemToPopulate) => typeReader.ReadValue_CheckProposed<T>(itemToPopulate);
            }
            else
            {
                var typeReader = deserializer.CreateCachedTypeReader(typeof(T), typeSettingsOverride);
                if (typeReader.IsNoCheckPossible<T>()) return (itemToPopulate) => typeReader.ReadValue_NoCheck<T>(itemToPopulate);
                else return (itemToPopulate) => typeReader.ReadValue_CheckProposed<T>(itemToPopulate);
            }
        }

        /// <summary>
        /// Prepares a context-local reader using settings that are merged onto the configured settings for <typeparamref name="T"/>.
        /// </summary>
        /// <param name="configure">Callback configuring the local reader. It must not be <see langword="null"/>.</param>
        /// <remarks>The prepared reader is isolated from the shared per-type cache.</remarks>
        public Func<T, T> PrepareTypeReader<T>(Action<TypeSettings<T>> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var typeSettings = new TypeSettings<T>();
            configure(typeSettings);
            return PrepareTypeReader(typeSettings);
        }

        /// <summary>
        /// Prepares the normal reader for <typeparamref name="T"/> while retaining its configured non-custom settings.
        /// </summary>
        /// <remarks>The configured custom reader is bypassed, preventing recursion when called from that reader's preparation.</remarks>
        public Func<T, T> PrepareNonCustomTypeReader<T>() =>
            PrepareTypeReader<T>(new TypeSettings<T> { suppressCustomTypeReader = true });

        /// <summary>Prepares a declarative JSON object reader.</summary>
        /// <param name="build">Callback declaring fields and unknown-field behavior.</param>
        public Func<ExtensionApi, T, T> PrepareObjectReader<T>(Action<ObjectReaderBuilder<T>> build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            var builder = new ObjectReaderBuilder<T>(deserializer, currentTypeSettings);
            build(builder);
            return builder.Build();
        }

        /// <summary>Adapts a raw value-reading callback for use as a prepared custom reader.</summary>
        /// <param name="readValue">Callback that reads exactly one JSON value.</param>
        public Func<ExtensionApi, T> PrepareValueReader<T>(Func<ExtensionApi, T> readValue) =>
            readValue ?? throw new ArgumentNullException(nameof(readValue));

        /// <summary>Prepares an array reader using the configured reader for each element.</summary>
        /// <typeparam name="TCollection">Collection type returned by the constructor.</typeparam>
        /// <typeparam name="TElement">JSON array element type.</typeparam>
        /// <param name="constructor">Creates the result from the completely read element sequence.</param>
        public Func<ExtensionApi, TCollection> PrepareArrayReader<TCollection, TElement>(Func<IEnumerable<TElement>, TCollection> constructor) =>
            PrepareArrayReader(constructor, null);

        /// <summary>Prepares an array reader using context-local settings for each element.</summary>
        /// <typeparam name="TCollection">Collection type returned by the constructor.</typeparam>
        /// <typeparam name="TElement">JSON array element type.</typeparam>
        /// <param name="constructor">Creates the result from the completely read element sequence.</param>
        /// <param name="configureElement">Optional context-local element settings.</param>
        public Func<ExtensionApi, TCollection> PrepareArrayReader<TCollection, TElement>(
            Func<IEnumerable<TElement>, TCollection> constructor,
            Action<TypeSettings<TElement>> configureElement)
        {
            if (constructor == null) throw new ArgumentNullException(nameof(constructor));
            Func<TElement, TElement> readElement = configureElement == null
                ? PrepareTypeReader<TElement>()
                : PrepareTypeReader(configureElement);

            return api =>
            {
                if (api.TryReadNullValue()) return default;
                if (api.SkipWhiteSpaces() != (byte)'[') throw new Exception("Failed reading array");
                api.TryNextByte();
                var elements = new List<TElement>();
                while (true)
                {
                    byte current = api.SkipWhiteSpaces();
                    if (current == (byte)']') break;
                    elements.Add(readElement(default));
                    current = api.SkipWhiteSpaces();
                    if (current == (byte)',') api.TryNextByte();
                    else if (current != (byte)']') throw new Exception("Failed reading array");
                }
                api.TryNextByte();
                return constructor(elements);
            };
        }

        public ByteSegment ConvertStringToByteSegment(string value) => new ByteSegment(value, true);

        /// <summary>Gets the constructor effective in the current reader-preparation context.</summary>
        public Func<T> GetConstructor<T>() => deserializer.GetConstructor<T>(null, currentTypeSettings);

        internal void PrepareReader<T>(ICustomTypeReader<T> reader, BaseTypeSettings typeSettings)
        {
            var previousTypeSettings = currentTypeSettings;
            currentTypeSettings = typeSettings;
            try
            {
                reader.PrepareReader(this);
            }
            finally
            {
                currentTypeSettings = previousTypeSettings;
            }
        }

    }

    public sealed class ExtensionApi
    {
        readonly JsonDeserializer deserializer;
        public ExtensionApi(JsonDeserializer deserializer)
        {
            this.deserializer = deserializer;
        }

        public UndoReadHandle CreateUndoReadHandle(bool initUndo = true)
        {
            return deserializer.CreateUndoReadHandle(initUndo);
        }

        public byte GetCurrentByte() => deserializer.buffer.CurrentByte;
        public bool TryNextByte() => deserializer.buffer.TryNextByte();
        
        public void SkipNextValue() => deserializer.SkipValue();        

        public void ReadRawJsonValue(out ByteSegment utf8Bytes)
        {
            deserializer.SkipWhiteSpaces();
            var rec = deserializer.buffer.StartRecording();            
            deserializer.SkipValue();
            utf8Bytes = rec.GetRecordedBytes(deserializer.buffer.IsBufferReadToEnd);
        }

        public bool TryReadRawJsonValue(out ByteSegment utf8Bytes)
        {
            deserializer.SkipWhiteSpaces();
            using (var undoHandle = deserializer.CreateUndoReadHandle(true)) 
            {
                try
                {
                    deserializer.SkipValue();
                    utf8Bytes = undoHandle.GetReadBytes();
                    undoHandle.SetUndoReading(false);
                    return true;
                }
                catch
                {
                    utf8Bytes = default;
                    undoHandle.SetUndoReading(true);
                    return false;
                }
            }            
        }

        public void ReadRawJsonValue(out string jsonValue)
        {
            ReadRawJsonValue(out ByteSegment utf8Bytes);
            jsonValue = DecodeUtf8Bytes(utf8Bytes);
        }

        public bool TryReadRawJsonValue(out string jsonValue)
        {
            jsonValue = null;
            if (!TryReadRawJsonValue(out ByteSegment utf8Bytes)) return false;
            jsonValue = DecodeUtf8Bytes(utf8Bytes);
            return true;
        }
        public byte SkipWhiteSpaces() => deserializer.SkipWhiteSpaces();

        public bool TryReadNullValue() => deserializer.TryReadNullValue();        
        public bool TryReadStringValueOrNull(out string value) => deserializer.TryReadStringValueOrNull(out value);
        public bool TryReadBoolValue(out bool value) => deserializer.TryReadBoolValue(out value);
        public bool TryReadSignedIntegerValue(out long value) => deserializer.TryReadSignedIntegerValue(out value);
        public bool TryReadUnsignedIntegerValue(out ulong value) => deserializer.TryReadUnsignedIntegerValue(out value);
        public bool TryReadFloatingPointValue(out double value) => deserializer.TryReadFloatingPointValue(out value);
        public bool TryReadObjectValue<T>(out T obj, ByteSegment fieldName) => deserializer.TryReadObjectValue(out obj, fieldName);
        public bool TryReadObjectValue(out Dictionary<string, object> obj, ByteSegment fieldName) => deserializer.TryReadObjectValue(out obj, fieldName);
        public bool TryReadArrayValue<T>(out T array, ByteSegment fieldName) where T : IEnumerable => deserializer.TryReadArrayValue(out array, fieldName);
        public bool TryReadArrayValue(out object[] array, ByteSegment fieldName) => deserializer.TryReadArrayValue(out array, fieldName);

        public string DecodeUtf8Bytes(ByteSegment bytes)
        {
            string str = Utf8Converter.DecodeUtf8ToString(bytes, deserializer.stringBuilder);            
            deserializer.stringBuilder.Clear();
            return str;
        }
        
    }
}
