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

        [Fact]
        public void Deserialize_ReadonlyFields_Class()
        {
            const string json = "{\"Id\":10,\"Name\":\"readonly\"}";
            var value = Deserialize<ReadonlyFieldClass>(json);

            Assert.Equal(10, value.Id);
            Assert.Equal("readonly", value.Name);
        }

        [Fact]
        public void Deserialize_ReadonlyFields_Struct()
        {
            const string json = "{\"X\":7,\"Flag\":true}";
            var value = Deserialize<ReadonlyFieldStruct>(json);

            Assert.Equal(7, value.X);
            Assert.True(value.Flag);
        }

        [Fact]
        public void Deserialize_InitOnlyProperties_Class()
        {
            const string json = "{\"Id\":20,\"Name\":\"init\"}";
            var value = Deserialize<InitOnlyPropertyClass>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(20, value.Id);
            Assert.Equal("init", value.Name);
        }

        [Fact]
        public void Deserialize_InitOnlyProperties_Struct()
        {
            const string json = "{\"Id\":30,\"Name\":\"struct-init\"}";
            var value = Deserialize<InitOnlyPropertyStruct>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(30, value.Id);
            Assert.Equal("struct-init", value.Name);
        }

        [Fact]
        public void Deserialize_PublicInitOnlyProperties_Class()
        {
            const string json = "{\"Id\":11,\"Name\":\"alpha\"}";
            var value = Deserialize<InitOnlyPublicClass>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(11, value.Id);
            Assert.Equal("alpha", value.Name);
        }

        [Fact]
        public void Deserialize_PublicInitOnlyProperties_Struct()
        {
            const string json = "{\"Id\":22,\"Name\":\"beta\"}";
            var value = Deserialize<InitOnlyPublicStruct>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(22, value.Id);
            Assert.Equal("beta", value.Name);
        }

        [Fact]
        public void Deserialize_JsonInclude_PrivateInitOnlyProperty()
        {
            const string json = "{\"PrivateInit\":33}";
            var value = Deserialize<PrivateInitIncludedClass>(json, new FeatureJsonDeserializer.Settings
            {
                dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(33, value.PrivateInitValue);
        }

        [Fact]
        public void Populate_ReadonlyField_WithNestedClass_PopulatesExistingInstance()
        {
            var holder = new ReadonlyNestedClassHolder();
            var originalRef = holder.Node;

            var deserializer = new FeatureJsonDeserializer(new FeatureJsonDeserializer.Settings
            {
                populateExistingMembers = true
            });

            Assert.True(deserializer.TryPopulate("{\"Node\":{\"A\":10}}", holder));

            Assert.Same(originalRef, holder.Node);
            Assert.Equal(10, holder.Node.A);
            Assert.Equal(2, holder.Node.B); // unchanged -> existing object was populated
        }

        [Fact]
        public void Populate_ReadonlyField_WithNestedStruct_PopulatesExistingValue()
        {
            var holder = new ReadonlyNestedStructHolder();

            var deserializer = new FeatureJsonDeserializer(new FeatureJsonDeserializer.Settings
            {
                populateExistingMembers = true
            });

            Assert.True(deserializer.TryPopulate("{\"Node\":{\"A\":10}}", holder));

            Assert.Equal(10, holder.Node.A);
            Assert.Equal(2, holder.Node.B); // unchanged -> existing value was populated
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
            public string PublicProp_IgnoredBacking { get { return propBackingField; } set { propBackingField = value; } }
        }

        private class ReadonlyFieldClass
        {
            public readonly int Id;
            public readonly string Name;
        }

        private struct ReadonlyFieldStruct
        {
            public readonly int X;
            public readonly bool Flag;
        }

        private class InitOnlyPropertyClass
        {
            public int Id { get; init; }
            public string Name { get; init; }
        }

        private struct InitOnlyPropertyStruct
        {
            public int Id { get; init; }
            public string Name { get; init; }
        }

        private class InitOnlyPublicClass
        {
            public int Id { get; init; }
            public string Name { get; init; }
        }

        private struct InitOnlyPublicStruct
        {
            public int Id { get; init; }
            public string Name { get; init; }
        }

        private class PrivateInitIncludedClass
        {
            [JsonInclude]
            private int PrivateInit { get; init; }

            public int PrivateInitValue => PrivateInit;
        }

        private class ReadonlyNestedClassHolder
        {
            public readonly MutableNodeClass Node = new MutableNodeClass { A = 1, B = 2 };
        }

        private class MutableNodeClass
        {
            public int A;
            public int B;
        }

        private class ReadonlyNestedStructHolder
        {
            public readonly MutableNodeStruct Node = new MutableNodeStruct { A = 1, B = 2 };
        }

        private struct MutableNodeStruct
        {
            public int A;
            public int B;
        }
    }
}