using FeatureLoom.Helpers;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonSerializerTypeInfoTests
    {
        private static void AssertSerialized<T>(T value, string expected, JsonSerializer.Settings settings)
        {
            var serializer = new JsonSerializer(settings);
            string json = serializer.Serialize(value);
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Serialize_TypeInfo_AddNoTypeInfo_BaseAsDerived()
        {
            BaseType value = new DerivedType();
            const string expected = "{\"DerivedValue\":2,\"BaseValue\":1}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddDeviatingTypeInfo_BaseAsDerived()
        {
            BaseType value = new DerivedType();
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            string expected = $"{{\"$type\":\"{typeName}\",\"DerivedValue\":2,\"BaseValue\":1}}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddAllTypeInfo_BaseType()
        {
            var value = new BaseType();
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            string expected = $"{{\"$type\":\"{typeName}\",\"BaseValue\":1}}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddAllTypeInfo_Primitive()
        {
            const int value = 1;
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(int));
            string expected = $"{{\"$type\":\"{typeName}\",\"$value\":1}}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddDeviatingTypeInfo_NestedField()
        {
            var value = new Container { Item = new DerivedType() };
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            string expected = $"{{\"Item\":{{\"$type\":\"{typeName}\",\"DerivedValue\":2,\"BaseValue\":1}}}}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddNoTypeInfo_NestedField()
        {
            var value = new Container { Item = new DerivedType() };
            const string expected = "{\"Item\":{\"DerivedValue\":2,\"BaseValue\":1}}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddDeviatingTypeInfo_CollectionElement()
        {
            var value = new ContainerList
            {
                Items = new List<BaseType> { new DerivedType() }
            };
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            string expected = $"{{\"Items\":[{{\"$type\":\"{typeName}\",\"DerivedValue\":2,\"BaseValue\":1}}]}}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddNoTypeInfo_CollectionElement()
        {
            var value = new ContainerList
            {
                Items = new List<BaseType> { new DerivedType() }
            };
            const string expected = "{\"Items\":[{\"DerivedValue\":2,\"BaseValue\":1}]}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
            });
        }

        private class BaseType
        {
            public int BaseValue = 1;
        }

        private class DerivedType : BaseType
        {
            public int DerivedValue = 2;
        }

        private class Container
        {
            public BaseType Item;
        }

        private class ContainerList
        {
            public List<BaseType> Items;
        }
    }
}