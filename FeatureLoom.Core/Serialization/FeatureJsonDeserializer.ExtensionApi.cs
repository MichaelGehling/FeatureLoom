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
