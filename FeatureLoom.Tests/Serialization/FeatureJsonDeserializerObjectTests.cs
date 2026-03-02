using FeatureLoom.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonDeserializerObjectTests
    {
        private static object Deserialize(string json)
        {
            var deserializer = new FeatureJsonDeserializer();
            Assert.True(deserializer.TryDeserialize(json, out object value));
            return value;
        }

        private static object Deserialize(string json, FeatureJsonDeserializer.Settings settings)
        {
            var deserializer = new FeatureJsonDeserializer(settings);
            Assert.True(deserializer.TryDeserialize(json, out object value));
            return value;
        }

        [Fact]
        public void Deserialize_Object_Null()
        {
            Assert.Null(Deserialize("null"));
        }

        [Fact]
        public void Deserialize_Object_Bool()
        {
            Assert.True((bool)Deserialize("true"));
        }

        [Fact]
        public void Deserialize_Object_String()
        {
            Assert.Equal("abc", Deserialize("\"abc\""));
        }

        [Fact]
        public void Deserialize_Object_Number_Int()
        {
            var value = Deserialize("1");
            Assert.IsType<int>(value);
            Assert.Equal(1, (int)value);
        }

        [Fact]
        public void Deserialize_Object_Number_Long()
        {
            var value = Deserialize("9223372036854775807");
            Assert.IsType<long>(value);
            Assert.Equal(long.MaxValue, (long)value);
        }

        [Fact]
        public void Deserialize_Object_Number_Ulong()
        {
            var value = Deserialize("18446744073709551615");
            Assert.IsType<ulong>(value);
            Assert.Equal(ulong.MaxValue, (ulong)value);
        }

        [Fact]
        public void Deserialize_Object_Number_Double()
        {
            var value = Deserialize("1.5");
            Assert.IsType<double>(value);
            Assert.Equal(1.5, (double)value);
        }

        [Fact]
        public void Deserialize_Object_Array()
        {
            var value = (IEnumerable)Deserialize("[1,\"a\",null]");
            var list = value.Cast<object>().ToList();

            Assert.Equal(3, list.Count);
            Assert.IsType<int>(list[0]);
            Assert.Equal(1, list[0]);
            Assert.Equal("a", list[1]);
            Assert.Null(list[2]);
        }

        [Fact]
        public void Deserialize_Object_Array_TryCastArraysOfUnknownValues_True()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                tryCastArraysOfUnknownValues = true
            };
            var value = Deserialize("[1,2]", settings);

            var list = Assert.IsType<List<int>>(value);
            Assert.Equal(new[] { 1, 2 }, list);
        }

        [Fact]
        public void Deserialize_Object_Array_TryCastArraysOfUnknownValues_False()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                tryCastArraysOfUnknownValues = false
            };
            var value = Deserialize("[1,2]", settings);

            var list = Assert.IsType<List<object>>(value);
            Assert.Equal(new object[] { 1, 2 }, list);
        }

        [Fact]
        public void Deserialize_Object_Object()
        {
            var value = (Dictionary<string, object>)Deserialize("{\"a\":1,\"b\":\"x\"}");

            Assert.Equal(1, value["a"]);
            Assert.Equal("x", value["b"]);
        }

        [Fact]
        public void Deserialize_Object_TypeInfo_IsNotSpecialWithoutProposals()
        {
            var value = (Dictionary<string, object>)Deserialize("{\"$type\":\"System.String\",\"Value\":1}");

            Assert.Equal("System.String", value["$type"]);
            Assert.Equal(1, value["Value"]);
        }
    }
}