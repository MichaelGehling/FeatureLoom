using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;

namespace FeatureLoom.Serialization;

public sealed partial class FeatureJsonDeserializer
{  
    public sealed class ExtensionApi
    {
        readonly FeatureJsonDeserializer deserializer;
        public ExtensionApi(FeatureJsonDeserializer deserializer)
        {
            this.deserializer = deserializer;
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
        public bool TryReadStringValue(out string value) => deserializer.TryReadStringValue(out value);
        public bool TryReadBoolValue(out bool value) => deserializer.TryReadBoolValue(out value);
        public bool TryReadSignedIntegerValue(out long value) => deserializer.TryReadSignedIntegerValue(out value);
        public bool TryReadUnsignedIntegerValue(out ulong value) => deserializer.TryReadUnsignedIntegerValue(out value);
        public bool TryReadFloatingPointValue(out double value) => deserializer.TryReadFloatingPointValue(out value);
        public bool TryReadObjectValue<T>(out T obj, ByteSegment fieldName) => deserializer.TryReadObjectValue(out obj, fieldName);
        public bool TryReadObjectValue(out Dictionary<string, object> obj, ByteSegment fieldName) => deserializer.TryReadObjectValue(out obj, fieldName);
        public bool TryReadArrayValue<T>(out T array, ByteSegment fieldName) where T : IEnumerable => deserializer.TryReadArrayValue(out array, fieldName);
        public bool TryReadArrayValue(out List<object> array, ByteSegment fieldName) => deserializer.TryReadArrayValue(out array, fieldName);

        public string DecodeUtf8Bytes(ArraySegment<byte> bytes)
        {
            string str = Utf8Converter.DecodeUtf8ToString(bytes, deserializer.stringBuilder);            
            deserializer.stringBuilder.Clear();
            return str;
        }
        
    }
}
