using FeatureLoom.Serialization;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonSerializerComplexTypeTests
    {
        private static void AssertSerialized<T>(T value, string expected)
        {
            var serializer = new JsonSerializer();
            string json = serializer.Serialize(value);
            Assert.Equal(expected, json);
        }

        private static void AssertSerialized<T>(T value, string expected, JsonSerializer.Settings settings)
        {
            var serializer = new JsonSerializer(settings);
            string json = serializer.Serialize(value);
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Serialize_SimpleClass_PublicFields()
        {
            var value = new SimpleClass { Id = 1, Name = "abc", Value = 1.5 };
            const string expected = "{\"Id\":1,\"Name\":\"abc\",\"Value\":1.5}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_SimpleClass_WithNullField()
        {
            var value = new SimpleClass { Id = 1, Name = null, Value = 0.0 };
            const string expected = "{\"Id\":1,\"Name\":null,\"Value\":0}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_SimpleStruct_PublicFields()
        {
            var value = new SimpleStruct { X = 5, Flag = true };
            const string expected = "{\"X\":5,\"Flag\":true}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_NullableStruct_Null()
        {
            SimpleStruct? value = null;
            const string expected = "null";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_NullableStruct_Value()
        {
            SimpleStruct? value = new SimpleStruct { X = 7, Flag = false };
            const string expected = "{\"X\":7,\"Flag\":false}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_Class_WithJsonIncludeProperty()
        {
            var value = new ClassWithJsonInclude { Field = 1, Name = "x" };
            const string expected = "{\"Field\":1,\"Name\":\"x\"}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_EmbeddedClass_Field()
        {
            var value = new EmbeddedOuter { Id = 1, Inner = new EmbeddedInner { Text = "inner" } };
            const string expected = "{\"Id\":1,\"Inner\":{\"Text\":\"inner\"}}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_Class_WithListOfEmbeddedClasses()
        {
            var value = new ComplexWithList
            {
                Items = new List<EmbeddedInner>
                {
                    new EmbeddedInner { Text = "a" },
                    new EmbeddedInner { Text = "b" }
                }
            };
            const string expected = "{\"Items\":[{\"Text\":\"a\"},{\"Text\":\"b\"}]}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_Class_WithListOfEmbeddedClasses_WithNull()
        {
            var value = new ComplexWithList
            {
                Items = new List<EmbeddedInner>
                {
                    new EmbeddedInner { Text = "a" },
                    null
                }
            };
            const string expected = "{\"Items\":[{\"Text\":\"a\"},null]}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_Class_WithDictionaryOfEmbeddedClasses()
        {
            var value = new ComplexWithDictionary
            {
                Map = new SortedDictionary<string, EmbeddedInner>
                {
                    ["a"] = new EmbeddedInner { Text = "x" },
                    ["b"] = new EmbeddedInner { Text = "y" }
                }
            };
            const string expected = "{\"Map\":{\"a\":{\"Text\":\"x\"},\"b\":{\"Text\":\"y\"}}}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_Class_PublicAndPrivateFields_Default()
        {
            var value = new ClassWithPrivateFields(1, 2);
            const string expected = "{\"PublicField\":1,\"privateField\":2}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_Class_PublicFieldsAndProperties_IncludesJsonIncludePrivateProperty()
        {
            var value = new ClassWithProperties { PublicField = 1, PublicProp = "pub" };
            const string expected = "{\"PublicProp\":\"pub\",\"PublicField\":1,\"PrivateProp\":\"priv\"}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                dataSelection = JsonSerializer.DataSelection.PublicFieldsAndProperties
            });
        }

        [Fact]
        public void Serialize_Inheritance_IncludesBasePrivateFields()
        {
            var value = new DerivedWithPrivateFields(1, 2, 3);
            const string expected = "{\"DerivedPublic\":1,\"derivedPrivate\":2,\"basePrivate\":3}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_DataSelection_PublicAndPrivateFields_CleanBackingFields()
        {
            var value = new DataSelectionSample();
            const string expected = "{\"PublicField\":1,\"privateField\":2,\"PublicProp\":3,\"IncludedPrivateProp\":4}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                dataSelection = JsonSerializer.DataSelection.PublicAndPrivateFields_CleanBackingFields
            });
        }

        [Fact]
        public void Serialize_DataSelection_PublicAndPrivateFields_RemoveBackingFields()
        {
            var value = new DataSelectionSample();
            const string expected = "{\"PublicField\":1,\"privateField\":2,\"IncludedPrivateProp\":4}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                dataSelection = JsonSerializer.DataSelection.PublicAndPrivateFields_RemoveBackingFields
            });
        }

        [Fact]
        public void Serialize_DataSelection_PublicFieldsAndProperties_WithJsonInclude()
        {
            var value = new DataSelectionSample();
            const string expected = "{\"PublicProp\":3,\"PublicField\":1,\"IncludedPrivateProp\":4}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                dataSelection = JsonSerializer.DataSelection.PublicFieldsAndProperties
            });
        }

        [Fact]
        public void Serialize_DataSelection_CleanBackingFields_IgnoresJsonIgnoreAutoProperty()
        {
            var value = new JsonIgnoreAutoPropSample();
            const string expected = "{\"VisibleField\":1}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                dataSelection = JsonSerializer.DataSelection.PublicAndPrivateFields_CleanBackingFields
            });
        }

        [Fact]
        public void Serialize_Inheritance_BaseJsonIgnore_Excluded()
        {
            var value = new DerivedWithIgnoredBaseField();
            const string expected = "{\"DerivedPublic\":1,\"baseIncluded\":2}";

            AssertSerialized(value, expected);
        }

        [Fact]
        public void Serialize_PublicFieldsAndProperties_BaseJsonIncludePrivateProperty()
        {
            var value = new DerivedWithIncludedBaseProperty();
            const string expected = "{\"DerivedPublic\":1,\"BaseIncludedProp\":5}";

            AssertSerialized(value, expected, new JsonSerializer.Settings
            {
                dataSelection = JsonSerializer.DataSelection.PublicFieldsAndProperties
            });
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

        private class ClassWithJsonInclude
        {
            public int Field = 1;

            [JsonInclude]
            public string Name { get; set; } = "x";
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

        private class ComplexWithList
        {
            public List<EmbeddedInner> Items;
        }

        private class ComplexWithDictionary
        {
            public SortedDictionary<string, EmbeddedInner> Map;
        }

        private class ClassWithPrivateFields
        {
            public int PublicField;
            private int privateField;

            public ClassWithPrivateFields(int publicField, int privateField)
            {
                PublicField = publicField;
                this.privateField = privateField;
            }
        }

        private class ClassWithProperties
        {
            public int PublicField;
            public string PublicProp { get; set; } = "pub";

            [JsonInclude]
            private string PrivateProp { get; set; } = "priv";

            private int privateField = 9;
        }

        private class BaseWithPrivateFields
        {
            private int basePrivate;

            public BaseWithPrivateFields(int value)
            {
                basePrivate = value;
            }
        }

        private class DerivedWithPrivateFields : BaseWithPrivateFields
        {
            public int DerivedPublic;
            private int derivedPrivate;

            public DerivedWithPrivateFields(int derivedPublic, int derivedPrivate, int basePrivate)
                : base(basePrivate)
            {
                DerivedPublic = derivedPublic;
                this.derivedPrivate = derivedPrivate;
            }
        }

        private class DataSelectionSample
        {
            public int PublicField = 1;
            private int privateField = 2;

            public int PublicProp { get; set; } = 3;

            [JsonInclude]
            private int IncludedPrivateProp { get; set; } = 4;

            [JsonIgnore]
            public int IgnoredField = 5;

            [JsonIgnore]
            public int IgnoredProp { get; set; } = 6;
        }

        private class JsonIgnoreAutoPropSample
        {
            public int VisibleField = 1;

            [JsonIgnore]
            public int IgnoredAutoProp { get; set; } = 2;
        }

        private class BaseWithIgnoredField
        {
            [JsonIgnore]
            private int baseIgnored = 3;

            private int baseIncluded = 2;
        }

        private class DerivedWithIgnoredBaseField : BaseWithIgnoredField
        {
            public int DerivedPublic = 1;
        }

        private class BaseWithIncludedProperty
        {
            [JsonInclude]
            private int BaseIncludedProp { get; set; } = 5;
        }

        private class DerivedWithIncludedBaseProperty : BaseWithIncludedProperty
        {
            public int DerivedPublic = 1;
        }
    }
}