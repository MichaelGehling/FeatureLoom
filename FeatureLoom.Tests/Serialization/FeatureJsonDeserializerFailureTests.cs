using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonDeserializerFailureTests
    {
        private static bool TryDeserialize<T>(string json, out T value)
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);
            return deserializer.TryDeserialize(json, out value);
        }

        [Fact]
        public void Deserialize_InvalidJson_UnterminatedObject_ReturnsFalse()
        {
            Assert.False(TryDeserialize("{\"a\":1", out Dictionary<string, int> value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_InvalidJson_UnterminatedString_ReturnsFalse()
        {
            Assert.False(TryDeserialize("\"abc", out string value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_InvalidJson_MissingColon_ReturnsFalse()
        {
            Assert.False(TryDeserialize("{\"a\" 1}", out Dictionary<string, int> value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_InvalidJson_UnterminatedArray_ReturnsFalse()
        {
            Assert.False(TryDeserialize("[1,2", out int[] value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_Int_Overflow_ReturnsFalse()
        {
            Assert.False(TryDeserialize("2147483648", out int value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Deserialize_Byte_NegativeValue_ReturnsFalse()
        {
            Assert.False(TryDeserialize("-1", out byte value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Deserialize_Double_InvalidFormat_ReturnsFalse()
        {
            Assert.False(TryDeserialize("1..2", out double value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Deserialize_ByteArray_InvalidBase64_ReturnsFalse()
        {
            Assert.False(TryDeserialize("\"@@@\"", out byte[] value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ByteSegment_InvalidBase64_ReturnsFalse()
        {
            Assert.False(TryDeserialize("\"@@@\"", out ByteSegment value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Deserialize_Enum_InvalidString_ReturnsFalse()
        {
            Assert.False(TryDeserialize("\"NotExisting\"", out FailEnum value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void Deserialize_Dictionary_IntKey_InvalidKey_ReturnsFalse()
        {
            Assert.False(TryDeserialize("{\"x\":\"value\"}", out Dictionary<int, string> value));
            Assert.Null(value);
        }

        private enum FailEnum
        {
            Zero = 0,
            One = 1
        }
    }
}