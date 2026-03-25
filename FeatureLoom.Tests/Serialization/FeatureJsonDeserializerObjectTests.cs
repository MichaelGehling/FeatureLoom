using FeatureLoom.Serialization;
using System;
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

        private static T DeserializeTyped<T>(string json)
        {
            var deserializer = new FeatureJsonDeserializer();
            Assert.True(deserializer.TryDeserialize(json, out T value));
            return value;
        }

        private static T DeserializeTyped<T>(string json, FeatureJsonDeserializer.Settings settings)
        {
            var deserializer = new FeatureJsonDeserializer(settings);
            Assert.True(deserializer.TryDeserialize(json, out T value));
            return value;
        }

        private static bool TryDeserialize<T>(string json, out T value)
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                enableReferenceResolution = true,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);
            return deserializer.TryDeserialize(json, out value);
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

        [Fact]
        public void Deserialize_Object_Number_NegativeLongBoundary()
        {
            var value = Deserialize("-2147483649");
            Assert.IsType<long>(value);
            Assert.Equal(-2147483649L, (long)value);
        }

        [Fact]
        public void Deserialize_Object_Number_ExponentNegative_AsDouble()
        {
            var value = Deserialize("1E-2");
            Assert.IsType<double>(value);
            Assert.Equal(0.01d, (double)value);
        }

        [Fact]
        public void Deserialize_Object_Array_TryCastArraysOfUnknownValues_True_MixedNumeric()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                tryCastArraysOfUnknownValues = true
            };
            var value = Deserialize("[1,2.5]", settings);

            var list = Assert.IsType<List<IComparable>>(value);
            Assert.Equal(2, list.Count);
            Assert.IsType<int>(list[0]);
            Assert.IsType<double>(list[1]);
            Assert.Equal(1, (int)list[0]);
            Assert.Equal(2.5d, (double)list[1]);
        }

        [Fact]
        public void Deserialize_Object_Array_TryCastArraysOfUnknownValues_True_IncompatibleTypes()
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                tryCastArraysOfUnknownValues = true
            };
            var value = Deserialize("[1,\"2\",null]", settings);

            var list = Assert.IsType<List<IComparable>>(value);
            Assert.Equal(3, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal("2", list[1]);
            Assert.Null(list[2]);
        }

        [Fact]
        public void Deserialize_UndefinedObjectMember_ObjectValue()
        {
            var value = DeserializeTyped<object>("{\"a\":1,\"b\":\"x\"}");

            var dict = Assert.IsType<Dictionary<string, object>>(value);
            Assert.Equal(1, dict["a"]);
            Assert.Equal("x", dict["b"]);
        }

        [Fact]
        public void Deserialize_UndefinedObjectMember_NumberValue()
        {
            var value = DeserializeTyped<object>("123");

            Assert.IsType<int>(value);
            Assert.Equal(123, value);
        }

        [Fact]
        public void Deserialize_UndefinedObjectMember_StringValue()
        {
            var value = DeserializeTyped<object>("\"abc\"");

            Assert.IsType<string>(value);
            Assert.Equal("abc", value);
        }

        [Fact]
        public void Deserialize_UndefinedObjectMember_BoolValue()
        {
            var value = DeserializeTyped<object>("true");

            Assert.IsType<bool>(value);
            Assert.Equal(true, value);
        }

        [Fact]
        public void Deserialize_UndefinedObjectMember_NullValue()
        {
            var value = DeserializeTyped<object>("null");

            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_UndefinedObjectMember_ArrayValue()
        {
            var value = DeserializeTyped<object>("[1,2,3]");

            var list = Assert.IsType<List<int>>(value);
            Assert.Equal(new[] { 1, 2, 3 }, list);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_InvalidRefPath_ReturnsFalse()
        {
            const string json = "{\"Name\":\"root\",\"Next\":{\"$ref\":\"$.Missing\"}}";
            Assert.False(TryDeserialize(json, out Node value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ReferenceResolution_MalformedRefPath_ReturnsFalse()
        {
            const string json = "{\"Items\":[{\"Name\":\"a\"},{\"$ref\":\"$.Items[x]\"}]}";
            Assert.False(TryDeserialize(json, out NodeList value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_Object_WithMultiOptionTypeMapping_SelectsFirstObjectTypeByField()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(object),
                typeof(ObjectOptionA),
                typeof(ObjectOptionB),
                typeof(Queue<object>));

            var value = DeserializeTyped<object>("{\"FieldA\":7}", settings);

            var typed = Assert.IsType<ObjectOptionA>(value);
            Assert.Equal(7, typed.FieldA);
        }

        [Fact]
        public void Deserialize_Object_WithMultiOptionTypeMapping_SelectsSecondObjectTypeByField()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(object),
                typeof(ObjectOptionA),
                typeof(ObjectOptionB),
                typeof(Queue<object>));

            var value = DeserializeTyped<object>("{\"FieldB\":\"x\"}", settings);

            var typed = Assert.IsType<ObjectOptionB>(value);
            Assert.Equal("x", typed.FieldB);
        }

        [Fact]
        public void Deserialize_Object_WithMultiOptionTypeMapping_FallsBackToDictionaryForUnknownFields()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(object),
                typeof(ObjectOptionA),
                typeof(ObjectOptionB),
                typeof(Queue<object>));

            var value = DeserializeTyped<object>("{\"Unknown\":42}", settings);

            var dict = Assert.IsType<Dictionary<string, object>>(value);
            Assert.Equal(42, dict["Unknown"]);
        }

        [Fact]
        public void Deserialize_Object_WithMultiOptionTypeMapping_UsesMappedEnumerableForArray()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(object),
                typeof(ObjectOptionA),
                typeof(ObjectOptionB),
                typeof(Queue<object>));

            var value = DeserializeTyped<object>("[1,\"x\",null]", settings);

            var queue = Assert.IsType<Queue<object>>(value);
            var array = queue.ToArray();
            Assert.Equal(3, array.Length);
            Assert.Equal(1, array[0]);
            Assert.Equal("x", array[1]);
            Assert.Null(array[2]);
        }

        [Fact]
        public void Deserialize_Object_WithMultiOptionTypeMapping_NumberStillUsesPrimitivePath()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(object),
                typeof(ObjectOptionA),
                typeof(ObjectOptionB),
                typeof(Queue<object>));

            var value = DeserializeTyped<object>("123", settings);

            Assert.IsType<int>(value);
            Assert.Equal(123, value);
        }

        [Fact]
        public void Deserialize_Object_WithMultiOptionTypeMapping_UnknownField_WithoutDictionaryFallback_UsesImplicitDictionaryFallback()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(object),
                typeof(ObjectOptionA),
                typeof(ObjectOptionB));

            var value = DeserializeTyped<object>("{\"Unknown\":42}", settings);

            var dict = Assert.IsType<Dictionary<string, object>>(value);
            Assert.Equal(42, dict["Unknown"]);
        }

        [Fact]
        public void Deserialize_Object_WithMultiOptionTypeMapping_EmptyObject_WithoutDictionaryFallback_UsesImplicitDictionaryFallback()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(object),
                typeof(ObjectOptionA),
                typeof(ObjectOptionB));

            var value = DeserializeTyped<object>("{}", settings);

            var dict = Assert.IsType<Dictionary<string, object>>(value);
            Assert.Empty(dict);
        }

        [Fact]
        public void Deserialize_Object_WithMultiOptionTypeMapping_CommonFieldOnly_FallsBackToFirstOption()
        {
            var settings = new FeatureJsonDeserializer.Settings();
            settings.AddMultiOptionTypeMapping(typeof(object),
                typeof(ObjectCommonOptionA),
                typeof(ObjectCommonOptionB));

            var value = DeserializeTyped<object>("{\"Common\":7}", settings);

            var typed = Assert.IsType<ObjectCommonOptionA>(value);
            Assert.Equal(7, typed.Common);
        }

        private class ObjectOptionA
        {
            public int FieldA;
        }

        private class ObjectOptionB
        {
            public string FieldB;
        }

        private class ObjectCommonOptionA
        {
            public int Common;
        }

        private class ObjectCommonOptionB
        {
            public int Common;
        }

        private class Node
        {
            public string Name;
            public Node Next;
        }

        private class NodeList
        {
            public List<Node> Items = new();
        }
    }
}