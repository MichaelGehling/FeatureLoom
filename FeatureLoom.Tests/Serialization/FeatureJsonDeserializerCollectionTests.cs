using FeatureLoom.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonDeserializerCollectionTests
    {
        private static T Deserialize<T>(string json)
        {
            var deserializer = new FeatureJsonDeserializer();
            Assert.True(deserializer.TryDeserialize(json, out T value));
            return value;
        }

        [Fact]
        public void Deserialize_IntArray()
        {
            var value = Deserialize<int[]>("[1,-2,3]");
            Assert.Equal(new[] { 1, -2, 3 }, value);
        }

        [Fact]
        public void Deserialize_StringArray_WithNull()
        {
            var value = Deserialize<string[]>("[\"a\",null,\"b\"]");
            Assert.Equal(new[] { "a", null, "b" }, value);
        }

        [Fact]
        public void Deserialize_ListOfInt()
        {
            var value = Deserialize<List<int>>("[1,2,3]");
            Assert.Equal(new[] { 1, 2, 3 }, value);
        }

        [Fact]
        public void Deserialize_ListOfNullableInt()
        {
            var value = Deserialize<List<int?>>("[1,null,2]");
            Assert.Equal(new int?[] { 1, null, 2 }, value);
        }

        [Fact]
        public void Deserialize_IReadOnlyList()
        {
            var value = Deserialize<IReadOnlyList<string>>("[\"a\",\"b\"]");
            Assert.Equal(new[] { "a", "b" }, value);
        }

        [Fact]
        public void Deserialize_IEnumerableOfInt()
        {
            var value = Deserialize<IEnumerable<int>>("[1,2]");
            Assert.Equal(new[] { 1, 2 }, value);
        }

        [Fact]
        public void Deserialize_IEnumerable_NonGeneric()
        {
            var value = Deserialize<IEnumerable>("[1,\"a\",null]");
            var list = value.Cast<object>().ToList();
            Assert.Equal(new object[] { 1, "a", null }, list);
        }

        [Fact]
        public void Deserialize_Dictionary_String_Int()
        {
            var value = Deserialize<Dictionary<string, int>>("{\"a\":1,\"b\":2}");
            Assert.Equal(1, value["a"]);
            Assert.Equal(2, value["b"]);
        }

        [Fact]
        public void Deserialize_Dictionary_Int_String()
        {
            var value = Deserialize<Dictionary<int, string>>("{\"2\":\"b\",\"1\":\"a\"}");
            Assert.Equal("a", value[1]);
            Assert.Equal("b", value[2]);
        }

        [Fact]
        public void Deserialize_IReadOnlyDictionary_String_Int()
        {
            var value = Deserialize<IReadOnlyDictionary<string, int>>("{\"a\":1,\"b\":2}");
            Assert.Equal(2, value["b"]);
        }

        [Fact]
        public void Deserialize_ISet_Int()
        {
            var value = Deserialize<ISet<int>>("[1,2,2]");
            Assert.Equal(2, value.Count);
            Assert.Contains(1, value);
            Assert.Contains(2, value);
        }

        [Fact]
        public void Deserialize_ByteArray_FromBase64()
        {
            var value = Deserialize<byte[]>("\"AQIDBA==\"");
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, value);
        }

        [Fact]
        public void Deserialize_ByteArray_FromArray()
        {
            var value = Deserialize<byte[]>("[1,2,3,4]");
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, value);
        }
    }
}