using FeatureLoom.Helpers;
using FeatureLoom.Serialization;
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