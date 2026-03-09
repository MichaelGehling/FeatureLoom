using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonSerializerCustomTypeHandlerTests
    {
        [Fact]
        public void Serialize_CustomTypeHandler_ExactType()
        {
            var settings = new FeatureJsonSerializer.Settings();
            settings.AddCustomTypeHandlerCreator<CustomType>(
                JsonDataTypeCategory.Primitive,
                api => item => api.Writer.WriteStringValue($"custom:{item.Value}")
            );

            var serializer = new FeatureJsonSerializer(settings);
            string json = serializer.Serialize(new CustomType { Value = 5 });

            Assert.Equal("\"custom:5\"", json);
        }

        [Fact]
        public void Serialize_CustomTypeHandler_AssignableType()
        {
            var settings = new FeatureJsonSerializer.Settings();
            settings.AddCustomTypeHandlerCreator<BaseCustom>(
                JsonDataTypeCategory.Primitive,
                api => item => api.Writer.WriteStringValue($"base:{item.Code}"),
                onlyExactType: false
            );
            //settings.typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo;

            var serializer = new FeatureJsonSerializer(settings);
            string json = serializer.Serialize(new DerivedCustom { Code = 7 });

            Assert.Equal("\"base:7\"", json);
        }

        [Fact]
        public void Serialize_CustomTypeHandler_SupportsTypePredicate()
        {
            var settings = new FeatureJsonSerializer.Settings();
            settings.AddCustomTypeHandlerCreator<MarkedType>(
                type => type.Name.EndsWith("Marked"),
                JsonDataTypeCategory.Primitive,
                api => item => api.Writer.WriteStringValue($"marked:{item.Tag}")
            );

            var serializer = new FeatureJsonSerializer(settings);
            string json = serializer.Serialize(new SpecialMarked { Tag = "x" });

            Assert.Equal("\"marked:x\"", json);
        }

        [Fact]
        public void Serialize_CustomTypeHandler_Precedence_FirstMatchWins()
        {
            var settings = new FeatureJsonSerializer.Settings();

            settings.AddCustomTypeHandlerCreator<BaseCustom>(
                JsonDataTypeCategory.Primitive,
                api => item => api.Writer.WriteStringValue($"base:{item.Code}"),
                onlyExactType: false);

            settings.AddCustomTypeHandlerCreator<DerivedCustom>(
                JsonDataTypeCategory.Primitive,
                api => item => api.Writer.WriteStringValue($"derived:{item.Code}"));

            var serializer = new FeatureJsonSerializer(settings);
            string json = serializer.Serialize(new DerivedCustom { Code = 7 });

            Assert.Equal("\"base:7\"", json);
        }

        [Fact]
        public void Serialize_CustomTypeHandler_Precedence_OrderMatters()
        {
            var settings = new FeatureJsonSerializer.Settings();

            settings.AddCustomTypeHandlerCreator<DerivedCustom>(
                JsonDataTypeCategory.Primitive,
                api => item => api.Writer.WriteStringValue($"derived:{item.Code}"));

            settings.AddCustomTypeHandlerCreator<BaseCustom>(
                JsonDataTypeCategory.Primitive,
                api => item => api.Writer.WriteStringValue($"base:{item.Code}"),
                onlyExactType: false);

            var serializer = new FeatureJsonSerializer(settings);
            string json = serializer.Serialize(new DerivedCustom { Code = 7 });

            Assert.Equal("\"derived:7\"", json);
        }

        private class CustomType
        {
            public int Value;
        }

        private class BaseCustom
        {
            public int Code;
        }

        private class DerivedCustom : BaseCustom
        {
        }

        private class MarkedType
        {
            public string Tag;
        }

        private class SpecialMarked : MarkedType
        {
        }
    }
}