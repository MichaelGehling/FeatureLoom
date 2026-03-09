using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class FeatureJsonDeserializerComplexTypeTests
    {
        private static T Deserialize<T>(string json, FeatureJsonDeserializer.Settings settings = null)
        {
            var deserializer = new FeatureJsonDeserializer(settings);
            Assert.True(deserializer.TryDeserialize(json, out T value));
            return value;
        }

        [Fact]
        public void Deserialize_Class_PublicFields()
        {
            const string json = "{\"Id\":1,\"Name\":\"abc\",\"Value\":1.5}";
            var value = Deserialize<SimpleClass>(json);

            Assert.Equal(1, value.Id);
            Assert.Equal("abc", value.Name);
            Assert.Equal(1.5, value.Value);
        }

        [Fact]
        public void Deserialize_Struct_PublicFields()
        {
            const string json = "{\"X\":5,\"Flag\":true}";
            var value = Deserialize<SimpleStruct>(json);

            Assert.Equal(5, value.X);
            Assert.True(value.Flag);
        }

        [Fact]
        public void Deserialize_EmbeddedClass_Field()
        {
            const string json = "{\"Id\":1,\"Inner\":{\"Text\":\"inner\"}}";
            var value = Deserialize<EmbeddedOuter>(json);

            Assert.Equal(1, value.Id);
            Assert.NotNull(value.Inner);
            Assert.Equal("inner", value.Inner.Text);
        }

        [Fact]
        public void Deserialize_PublicAndPrivateFields_Default()
        {
            const string json = "{\"PublicField\":1,\"privateField\":2}";
            var value = Deserialize<ClassWithPrivateFields>(json);

            Assert.Equal(1, value.PublicField);
            Assert.Equal(2, value.PrivateFieldValue);
        }

        [Fact]
        public void Deserialize_PublicFieldsAndProperties_IncludesJsonIncludePrivateProperty()
        {
            const string json = "{\"PublicField\":1,\"PublicProp\":\"pub\",\"PrivateProp\":\"priv\"}";
            var value = Deserialize<ClassWithProperties>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(1, value.PublicField);
            Assert.Equal("pub", value.PublicProp);
            Assert.Equal("priv", value.PrivatePropValue);
        }

        [Fact]
        public void Deserialize_Inheritance_IncludesBasePrivateFields()
        {
            const string json = "{\"DerivedPublic\":1,\"basePrivate\":3}";
            var value = Deserialize<DerivedWithPrivateFields>(json);

            Assert.Equal(1, value.DerivedPublic);
            Assert.Equal(3, value.BasePrivateValue);
        }

        [Fact]
        public void Deserialize_JsonIgnore_IsSkipped()
        {
            const string json = "{\"Visible\":2,\"Ignored\":9}";
            var value = Deserialize<IgnoredSample>(json);

            Assert.Equal(2, value.Visible);
            Assert.Equal(5, value.IgnoredValue);
        }

        [Fact]
        public void Deserialize_PublicFieldsAndProperties_JsonIncludePrivateField()
        {
            const string json = "{\"Included\":7}";
            var value = Deserialize<IncludedPrivateFieldSample>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(7, value.IncludedValue);
        }

        [Fact]
        public void Deserialize_PublicAndPrivateFields_BaseJsonIncludePrivateProperty()
        {
            const string json = "{\"DerivedPublic\":1,\"BaseIncludedProp\":5}";
            var value = Deserialize<DerivedWithIncludedBaseProperty>(json);

            Assert.Equal(1, value.DerivedPublic);
            Assert.Equal(5, value.BaseIncludedValue);
        }

        [Fact]
        public void Deserialize_MissingAndExtraFields_SkipsUnknownAndKeepsDefaults()
        {
            const string json = "{\"A\":5,\"Unknown\":9}";
            var value = Deserialize<MissingExtraSample>(json);

            Assert.Equal(5, value.A);
            Assert.Equal(2, value.B);
        }

        [Fact]
        public void Deserialize_DataAccess_PublicAndPrivateFields_IgnoresPublicProperty()
        {
            const string json = "{\"PublicField\":1,\"privateField\":5,\"PublicProp_IgnoredBacking\":\"set\"}";
            var value = Deserialize<DataAccessSample>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicAndPrivateFields
            });

            Assert.Equal(1, value.PublicField);
            Assert.Equal(5, value.PrivateFieldValue);
            Assert.Equal("default", value.PublicProp_IgnoredBacking);
        }

        [Fact]
        public void Deserialize_DataAccess_PublicFieldsAndProperties_IgnoresPrivateField()
        {
            const string json = "{\"PublicField\":2,\"privateField\":6,\"PublicProp\":\"set\"}";
            var value = Deserialize<DataAccessSample>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(2, value.PublicField);
            Assert.Equal(9, value.PrivateFieldValue);
            Assert.Equal("set", value.PublicProp);
        }

        [Fact]
        public void Deserialize_DataAccess_PublicAndPrivateFields_IncludesAutoPropertyBackingField()
        {
            const string json = "{\"PublicField\":1,\"privateField\":5,\"PublicProp\":\"set\"}";
            var value = Deserialize<DataAccessSample>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicAndPrivateFields
            });

            Assert.Equal(1, value.PublicField);
            Assert.Equal(5, value.PrivateFieldValue);
            Assert.Equal("set", value.PublicProp);
        }

        private class SimpleClass
        {
            public int Id;
            public string Name;
            public double Value;
        }

        private struct SimpleStruct
        {
            public int X;
            public bool Flag;
        }

        private class EmbeddedOuter
        {
            public int Id;
            public EmbeddedInner Inner;
        }

        private class EmbeddedInner
        {
            public string Text;
        }

        private class ClassWithPrivateFields
        {
            public int PublicField;
            private int privateField;

            public int PrivateFieldValue => privateField;
        }

        private class ClassWithProperties
        {
            public int PublicField;
            public string PublicProp { get; set; }

            [JsonInclude]
            private string PrivateProp { get; set; }

            public string PrivatePropValue => PrivateProp;
        }

        private class BaseWithPrivateFields
        {
            private int basePrivate;
            public int BasePrivateValue => basePrivate;
        }

        private class DerivedWithPrivateFields : BaseWithPrivateFields
        {
            public int DerivedPublic;
        }

        private class IgnoredSample
        {
            public int Visible = 1;

            [JsonIgnore]
            public int Ignored = 5;

            public int IgnoredValue => Ignored;
        }

        private class IncludedPrivateFieldSample
        {
            [JsonInclude]
            private int Included;

            public int IncludedValue => Included;
        }

        private class BaseWithIncludedBaseProperty
        {
            [JsonInclude]
            private int BaseIncludedProp { get; set; }

            public int BaseIncludedValue => BaseIncludedProp;
        }

        private class DerivedWithIncludedBaseProperty : BaseWithIncludedBaseProperty
        {
            public int DerivedPublic;
        }

        private class MissingExtraSample
        {
            public int A = 1;
            public int B = 2;
        }

        private class DataAccessSample
        {
            public int PublicField;
            public string PublicProp { get; set; } = "default";
            private int privateField = 9;

            public int PrivateFieldValue => privateField;

            [JsonIgnore]
            private string propBackingField = "default";
            public string PublicProp_IgnoredBacking { get{ return propBackingField; } set{ propBackingField = value; } }
        }
    }
}