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

        public byte GetCurrentByte() => deserializer.CurrentByte;
        public bool TryNextByte() => deserializer.TryNextByte();
        
        public void SkipNextValue() => deserializer.SkipValue();
        public void SkipWhiteSpaces() => deserializer.SkipWhiteSpaces();

        public bool TryReadNullValue() => deserializer.TryReadNullValue();
        public bool TryReadStringValue(out string value) => deserializer.TryReadStringValue(out value);
        public bool TryReadBoolValue(out bool value) => deserializer.TryReadBoolValue(out value);
        public bool TryReadSignedIntegerValue(out long value) => deserializer.TryReadSignedIntegerValue(out value);
        public bool TryReadUnsignedIntegerValue(out ulong value) => deserializer.TryReadUnsignedIntegerValue(out value);
        public bool TryReadFloatingPointValue(out double value) => deserializer.TryReadFloatingPointValue(out value);
        public bool TryReadObjectValue<T>(out T obj, EquatableByteSegment fieldName) => deserializer.TryReadObjectValue(out obj, fieldName);
        public bool TryReadObjectValue(out Dictionary<string, object> obj, EquatableByteSegment fieldName) => deserializer.TryReadObjectValue(out obj, fieldName);
        public bool TryReadArrayValue<T>(out T array, EquatableByteSegment fieldName) where T : IEnumerable => deserializer.TryReadArrayValue(out array, fieldName);
        public bool TryReadArrayValue(out List<object> array, EquatableByteSegment fieldName) => deserializer.TryReadArrayValue(out array, fieldName);

        public string DecodeUtf8Bytes(ArraySegment<byte> bytes)
        {
            DecodeUtf8(bytes, deserializer.stringBuilder);
            string str = deserializer.stringBuilder.ToString();
            deserializer.stringBuilder.Clear();
            return str;
        }
        
    }
}
