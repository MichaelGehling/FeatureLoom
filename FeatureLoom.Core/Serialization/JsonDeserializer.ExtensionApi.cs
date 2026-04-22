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

        public Func<T, T> PrepareNonCustomTypeReader<T>() => PrepareTypeReader<T>(new TypeSettings<T>());

        public ByteSegment ConvertStringToByteSegment(string value) => new ByteSegment(value, true);

        public Func<T> GetContructor<T>() => deserializer.GetConstructor<T>(null, null);

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
