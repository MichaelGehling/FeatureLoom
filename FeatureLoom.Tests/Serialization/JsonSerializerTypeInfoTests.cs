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

        private struct TypeInfoStruct { public int Z; }

        [Fact]
        public void Serialize_TypeInfo_AddAllTypeInfo_Struct()
        {
            var value = new TypeInfoStruct { Z = 7 };
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(TypeInfoStruct));
            string expected = $"{{\"$type\":\"{typeName}\",\"Z\":7}}";
            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddDeviatingTypeInfo_ExactRoot_NoTypeInfo()
        {
            BaseType value = new BaseType();
            const string expected = "{\"BaseValue\":1}";
            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddDeviatingTypeInfo_DictionaryValue_Derived()
        {
            var value = new SortedDictionary<string, BaseType> { ["x"] = new DerivedType() };
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            string expected = $"{{\"x\":{{\"$type\":\"{typeName}\",\"DerivedValue\":2,\"BaseValue\":1}}}}";
            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo
            });
        }

        [Fact]
        public void Serialize_TypeInfo_AddAllTypeInfo_CollectionElement_NonDeviating()
        {
            var value = new ContainerList { Items = new List<BaseType> { new BaseType() } };
            string containerTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ContainerList));
            string listTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(List<BaseType>));
            string baseTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            string expected = $"{{\"$type\":\"{containerTypeName}\",\"Items\":{{\"$type\":\"{listTypeName}\",\"$value\":[{{\"$type\":\"{baseTypeName}\",\"BaseValue\":1}}]}}}}";
            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            });
        }

        [Fact]
        public void Serialize_ArrayValueFieldName_Values_UsedForArrayEnvelope()
        {
            var value = new ContainerList { Items = new List<BaseType> { new BaseType() } };
            string containerTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ContainerList));
            string listTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(List<BaseType>));
            string baseTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            string expected = $"{{\"$type\":\"{containerTypeName}\",\"Items\":{{\"$type\":\"{listTypeName}\",\"$values\":[{{\"$type\":\"{baseTypeName}\",\"BaseValue\":1}}]}}}}";
            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo,
                arrayValueFieldName = JsonSerializer.ValueFieldName.Values
            });
        }

        [Fact]
        public void Serialize_AlwaysEnvelope_AddAllTypeInfo_WrapsObjects()
        {
            var value = new BaseType();
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            string expected = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"BaseValue\":1}}}}";
            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo,
                typeInfoFormat = JsonSerializer.TypeInfoFormat.AlwaysEnvelope
            });
        }

        [Fact]
        public void Serialize_AlwaysEnvelope_AddDeviatingTypeInfo_WrapsOnlyDeviating()
        {
            BaseType value = new DerivedType();
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            string expected = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"DerivedValue\":2,\"BaseValue\":1}}}}";
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo,
                typeInfoFormat = JsonSerializer.TypeInfoFormat.AlwaysEnvelope
            };
            AssertSerialized(value, expected, settings);

            // Non-deviating values carry no type info, so they are not wrapped either.
            AssertSerialized<BaseType>(new BaseType(), "{\"BaseValue\":1}", settings);
        }

        [Fact]
        public void Deserialize_AlwaysEnvelope_RoundTrips()
        {
            BaseType value = new DerivedType();
            var serializer = new JsonSerializer(new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo,
                typeInfoFormat = JsonSerializer.TypeInfoFormat.AlwaysEnvelope
            });
            string json = serializer.Serialize(value);

            var deserializer = new JsonDeserializer();
            Assert.True(deserializer.TryDeserialize<BaseType>(json, out var restored));
            var derived = Assert.IsType<DerivedType>(restored);
            Assert.Equal(2, derived.DerivedValue);
            Assert.Equal(1, derived.BaseValue);
        }

        [Fact]
        public void Serialize_OnlyInlineForObjects_KeepsInlineTypeInfoButDropsEnvelopes()
        {
            var value = new ContainerList { Items = new List<BaseType> { new BaseType() } };
            string containerTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ContainerList));
            string baseTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            // The list would need an envelope and therefore loses its type info, while both the
            // container and the element keep their inline "$type".
            string expected = $"{{\"$type\":\"{containerTypeName}\",\"Items\":[{{\"$type\":\"{baseTypeName}\",\"BaseValue\":1}}]}}";
            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo,
                typeInfoFormat = JsonSerializer.TypeInfoFormat.OnlyInlineForObjects
            });
        }

        [Fact]
        public void Serialize_OnlyInlineForObjects_PrimitiveHasNoTypeInfo()
        {
            AssertSerialized(1, "1", new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo,
                typeInfoFormat = JsonSerializer.TypeInfoFormat.OnlyInlineForObjects
            });
        }

        [Fact]
        public void Serialize_OnlyInlineForObjects_AddDeviatingTypeInfo_KeepsObjectTypeInfo()
        {
            BaseType value = new DerivedType();
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            string expected = $"{{\"$type\":\"{typeName}\",\"DerivedValue\":2,\"BaseValue\":1}}";
            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo,
                typeInfoFormat = JsonSerializer.TypeInfoFormat.OnlyInlineForObjects
            });
        }

        [Fact]
        public void Serialize_TypeInfoFormat_PerType_OverridesGlobalFormat()
        {
            var value = new BaseType();
            string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            string expected = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"BaseValue\":1}}}}";
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<BaseType>(ts => ts.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.AlwaysEnvelope));
            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_TypeInfoFormat_PerMember_OverridesGlobalFormat()
        {
            var value = new Container { Item = new BaseType() };
            string containerTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(Container));
            string baseTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            // Only the member is enveloped, the container itself keeps the inline layout.
            string expected = $"{{\"$type\":\"{containerTypeName}\",\"Item\":{{\"$type\":\"{baseTypeName}\",\"$value\":{{\"BaseValue\":1}}}}}}";
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<Container>(ts =>
                ts.ConfigureMember<BaseType>(nameof(Container.Item), ms => ms.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.AlwaysEnvelope)));
            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_ArrayValueFieldName_PerType_OverridesGlobalName()
        {
            var value = new ContainerList { Items = new List<BaseType> { new BaseType() } };
            string containerTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ContainerList));
            string listTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(List<BaseType>));
            string baseTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            string expected = $"{{\"$type\":\"{containerTypeName}\",\"Items\":{{\"$type\":\"{listTypeName}\",\"$values\":[{{\"$type\":\"{baseTypeName}\",\"BaseValue\":1}}]}}}}";
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<List<BaseType>>(ts => ts.SetArrayValueFieldName(JsonSerializer.ValueFieldName.Values));
            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_ConfigureElement_AppliesToListElements()
        {
            var value = new List<BaseType> { new BaseType() };
            string listTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(List<BaseType>));
            string baseTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            // Only the elements are enveloped, the list itself keeps the inline layout.
            string expected = $"{{\"$type\":\"{listTypeName}\",\"$value\":[{{\"$type\":\"{baseTypeName}\",\"$value\":{{\"BaseValue\":1}}}}]}}";
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<List<BaseType>>(ts =>
                ts.ConfigureElement<BaseType>(es => es.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.AlwaysEnvelope)));
            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_ConfigureElement_AppliesToArrayElements()
        {
            var value = new BaseType[] { new BaseType() };
            string arrayTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType[]));
            string baseTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            string expected = $"{{\"$type\":\"{arrayTypeName}\",\"$value\":[{{\"$type\":\"{baseTypeName}\",\"$value\":{{\"BaseValue\":1}}}}]}}";
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<BaseType[]>(ts =>
                ts.ConfigureElement<BaseType>(es => es.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.AlwaysEnvelope)));
            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_ConfigureElement_AppliesToDictionaryValues()
        {
            var value = new Dictionary<string, BaseType> { ["a"] = new BaseType() };
            string dictTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(Dictionary<string, BaseType>));
            string baseTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(BaseType));
            string expected = $"{{\"$type\":\"{dictTypeName}\",\"a\":{{\"$type\":\"{baseTypeName}\",\"$value\":{{\"BaseValue\":1}}}}}}";
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<Dictionary<string, BaseType>>(ts =>
                ts.ConfigureElement<BaseType>(es => es.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.AlwaysEnvelope)));
            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void Serialize_ConfigureElement_AppliesToDeviatingRuntimeElementType()
        {
            var value = new List<BaseType> { new DerivedType() };
            string listTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(List<BaseType>));
            string derivedTypeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedType));
            // The element settings must survive the runtime type deviation.
            string expected = $"{{\"$type\":\"{listTypeName}\",\"$value\":[{{\"$type\":\"{derivedTypeName}\",\"$value\":{{\"DerivedValue\":2,\"BaseValue\":1}}}}]}}";
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<List<BaseType>>(ts =>
                ts.ConfigureElement<BaseType>(es => es.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.AlwaysEnvelope)));
            AssertSerialized(value, expected, settings);
        }

        [Fact]
        public void ConfigureElement_WithWrongElementType_Throws()
        {
            var settings = new JsonSerializer.Settings();
            Assert.ThrowsAny<System.Exception>(() =>
                settings.ConfigureType<List<BaseType>>(ts => ts.ConfigureElement<string>(es => { })));
        }
    }
}