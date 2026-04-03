using FeatureLoom.Helpers;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonDeserializerProposedTypeTests
    {
        private static T Deserialize<T>(string json)
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                enableProposedTypes = true
            };
            var deserializer = new FeatureJsonDeserializer(settings);
            Assert.True(deserializer.TryDeserialize(json, out T value));
            return value;
        }

        private static bool TryDeserialize<T>(string json, out T value)
        {
            var settings = new FeatureJsonDeserializer.Settings
            {
                enableProposedTypes = true,
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new FeatureJsonDeserializer(settings);
            return deserializer.TryDeserialize(json, out value);
        }

        [Fact]
        public void Deserialize_ProposedType_WithValueField()
        {
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"BaseValue\":1,\"DerivedValue\":2}}}}";

            BaseType value = Deserialize<BaseType>(json);

            Assert.IsType<DerivedType>(value);
            Assert.Equal(1, value.BaseValue);
            Assert.Equal(2, ((DerivedType)value).DerivedValue);
        }

        [Fact]
        public void Deserialize_ProposedType_EmbeddedFields()
        {
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            string json = $"{{\"$type\":\"{typeName}\",\"BaseValue\":1,\"DerivedValue\":2}}";

            BaseType value = Deserialize<BaseType>(json);

            Assert.IsType<DerivedType>(value);
            Assert.Equal(1, value.BaseValue);
            Assert.Equal(2, ((DerivedType)value).DerivedValue);
        }

        [Fact]
        public void Deserialize_ProposedType_Primitive_WithValueField()
        {
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(byte));
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":1}}";

            object value = Deserialize<object>(json);

            Assert.IsType<byte>(value);
            Assert.Equal(1, (byte)value);
        }

        [Fact]
        public void Deserialize_ProposedType_GenericList_WithValueField()
        {
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(Queue<string>));
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":[\"a\",\"b\"]}}";

            object value = Deserialize<object>(json);

            var queue = Assert.IsType<Queue<string>>(value);
            Assert.Equal(new[] { "a", "b" }, queue.ToArray());
        }

        private static string GetNewtonsoftCompatibleTypeName(Type type)
        {
            string typeName = type.AssemblyQualifiedName;
            Assert.False(string.IsNullOrWhiteSpace(typeName));
            return typeName;
        }

        [Fact]
        public void Deserialize_ProposedType_WithValueField_NewtonsoftCompatibleTypeName()
        {
            string typeName = GetNewtonsoftCompatibleTypeName(typeof(DerivedType));
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"BaseValue\":1,\"DerivedValue\":2}}}}";

            BaseType value = Deserialize<BaseType>(json);

            Assert.IsType<DerivedType>(value);
            Assert.Equal(1, value.BaseValue);
            Assert.Equal(2, ((DerivedType)value).DerivedValue);
        }

        [Fact]
        public void Deserialize_ProposedType_GenericList_WithValuesField_NewtonsoftCompatible()
        {
            string typeName = GetNewtonsoftCompatibleTypeName(typeof(List<int>));
            string json = $"{{\"$type\":\"{typeName}\",\"$values\":[1,2,3]}}";

            object value = Deserialize<object>(json);

            var list = Assert.IsType<List<int>>(value);
            Assert.Equal(new[] { 1, 2, 3 }, list);
        }

        [Fact]
        public void Deserialize_ProposedType_GenericList_WithUppercaseValuesField_NewtonsoftCompatible()
        {
            string typeName = GetNewtonsoftCompatibleTypeName(typeof(List<int>));
            string json = $"{{\"$type\":\"{typeName}\",\"$VALUES\":[4,5]}}";

            object value = Deserialize<object>(json);

            var list = Assert.IsType<List<int>>(value);
            Assert.Equal(new[] { 4, 5 }, list);
        }

        [Fact]
        public void Deserialize_ProposedType_WithValuesField_AndAdditionalFields_SkipsRemainder()
        {
            string typeName = GetNewtonsoftCompatibleTypeName(typeof(List<int>));
            string json = $"{{\"$type\":\"{typeName}\",\"$values\":[7,8],\"meta\":{{\"x\":1}}}}";

            object value = Deserialize<object>(json);

            var list = Assert.IsType<List<int>>(value);
            Assert.Equal(new[] { 7, 8 }, list);
        }

        [Fact]
        public void Deserialize_ProposedType_GenericDictionary_WithValueField()
        {
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(Dictionary<string, int>));
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"a\":1,\"b\":2}}}}";

            object value = Deserialize<object>(json);

            var dict = Assert.IsType<Dictionary<string, int>>(value);
            Assert.Equal(1, dict["a"]);
            Assert.Equal(2, dict["b"]);
        }

        [Fact]
        public void Deserialize_ProposedType_GenericSet_WithValueField()
        {
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(HashSet<int>));
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":[1,2,2]}}";

            object value = Deserialize<object>(json);

            var set = Assert.IsType<HashSet<int>>(value);
            Assert.True(set.SetEquals(new[] { 1, 2 }));
        }

        [Fact]
        public void Deserialize_ProposedType_UnknownType_ValueHandledAsUnknownType()
        {
            const string json = "{\"$type\":\"Unknown.Namespace.UnknownType\",\"$value\":1}";

            object value = Deserialize<object>(json);

            var intValue = Assert.IsType<int>(value);
            Assert.Equal(1, intValue);
        }

        [Fact]
        public void Deserialize_ProposedType_IncompatibleType_ReturnsFalse()
        {
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(byte));
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":1}}";

            Assert.False(TryDeserialize(json, out BaseType value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ProposedType_MalformedPayload_ReturnsFalse()
        {
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":[";

            Assert.False(TryDeserialize(json, out BaseType value));
            Assert.Null(value);
        }

        private class BaseType
        {
            public int BaseValue = 1;
        }

        private class DerivedType : BaseType
        {
            public int DerivedValue = 2;
        }
    }
}