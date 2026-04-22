using FeatureLoom.Serialization;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonDeserializerComplexTypeTests
    {
        private static T Deserialize<T>(string json, JsonDeserializer.Settings settings = null)
        {
            var deserializer = new JsonDeserializer(settings);
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
            var value = Deserialize<ClassWithProperties>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
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
            var value = Deserialize<IncludedPrivateFieldSample>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
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
            var value = Deserialize<DataAccessSample>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields
            });

            Assert.Equal(1, value.PublicField);
            Assert.Equal(5, value.PrivateFieldValue);
            Assert.Equal("default", value.PublicProp_IgnoredBacking);
        }

        [Fact]
        public void Deserialize_DataAccess_PublicFieldsAndProperties_IgnoresPrivateField()
        {
            const string json = "{\"PublicField\":2,\"privateField\":6,\"PublicProp\":\"set\"}";
            var value = Deserialize<DataAccessSample>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(2, value.PublicField);
            Assert.Equal(9, value.PrivateFieldValue);
            Assert.Equal("set", value.PublicProp);
        }

        [Fact]
        public void Deserialize_DataAccess_PublicAndPrivateFields_IncludesAutoPropertyBackingField()
        {
            const string json = "{\"PublicField\":1,\"privateField\":5,\"PublicProp\":\"set\"}";
            var value = Deserialize<DataAccessSample>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields
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
            var value = Deserialize<InitOnlyPropertyClass>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(20, value.Id);
            Assert.Equal("init", value.Name);
        }

        [Fact]
        public void Deserialize_InitOnlyProperties_Struct()
        {
            const string json = "{\"Id\":30,\"Name\":\"struct-init\"}";
            var value = Deserialize<InitOnlyPropertyStruct>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(30, value.Id);
            Assert.Equal("struct-init", value.Name);
        }

        [Fact]
        public void Deserialize_PublicInitOnlyProperties_Class()
        {
            const string json = "{\"Id\":11,\"Name\":\"alpha\"}";
            var value = Deserialize<InitOnlyPublicClass>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(11, value.Id);
            Assert.Equal("alpha", value.Name);
        }

        [Fact]
        public void Deserialize_PublicInitOnlyProperties_Struct()
        {
            const string json = "{\"Id\":22,\"Name\":\"beta\"}";
            var value = Deserialize<InitOnlyPublicStruct>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(22, value.Id);
            Assert.Equal("beta", value.Name);
        }

        [Fact]
        public void Deserialize_JsonInclude_PrivateInitOnlyProperty()
        {
            const string json = "{\"PrivateInit\":33}";
            var value = Deserialize<PrivateInitIncludedClass>(json, new JsonDeserializer.Settings
            {
                dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties
            });

            Assert.Equal(33, value.PrivateInitValue);
        }

        [Fact]
        public void Populate_ReadonlyField_WithNestedClass_PopulatesExistingInstance()
        {
            var holder = new ReadonlyNestedClassHolder();
            var originalRef = holder.Node;

            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
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

            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
            {
                populateExistingMembers = true
            });

            Assert.True(deserializer.TryPopulate("{\"Node\":{\"A\":10}}", holder));

            Assert.Equal(10, holder.Node.A);
            Assert.Equal(2, holder.Node.B); // unchanged -> existing value was populated
        }

        [Fact]
        public void Deserialize_AbstractType_WithoutMapping_ReturnsFalse()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{\"Value\":1}", out AbstractSample value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ClassWithoutDefaultConstructor_ReturnsFalse()
        {
            var settings = new JsonDeserializer.Settings
            {
                rethrowExceptions = false,
                logCatchedExceptions = false
            };
            var deserializer = new JsonDeserializer(settings);

            Assert.False(deserializer.TryDeserialize("{\"X\":5}", out NoDefaultCtorSample value));
            Assert.Null(value);
        }

        [Fact]
        public void Deserialize_ClassWithoutDefaultConstructor_WithConfiguredConstructor_Works()
        {
            var settings = new JsonDeserializer.Settings();
            settings.AddConstructor(() => new NoDefaultCtorSample(42));

            var value = Deserialize<NoDefaultCtorSample>("{\"X\":5}", settings);

            Assert.Equal(5, value.X);
        }

        [Fact]
        public void Deserialize_ProposedType_WithValueWrapper_UsesDerivedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };

            string typeName = typeof(ProposedDerivedSample).FullName;
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"A\":1,\"B\":2}}}}";

            var value = Deserialize<ProposedBaseSample>(json, settings);

            var derived = Assert.IsType<ProposedDerivedSample>(value);
            Assert.Equal(1, derived.A);
            Assert.Equal(2, derived.B);
        }

        [Fact]
        public void Deserialize_ProposedType_WithValueWrapper_AndTrailingFields_SkipsRemainingTypeObjectFields()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };

            string typeName = typeof(ProposedDerivedSample).FullName;
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"A\":3,\"B\":4}},\"Ignored\":99}}";

            var value = Deserialize<ProposedBaseSample>(json, settings);

            var derived = Assert.IsType<ProposedDerivedSample>(value);
            Assert.Equal(3, derived.A);
            Assert.Equal(4, derived.B);
        }

        [Fact]
        public void Deserialize_ProposedType_NotFirstField_IsIgnored()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };

            string typeName = typeof(ProposedDerivedSample).FullName;
            string json = $"{{\"A\":5,\"$type\":\"{typeName}\",\"B\":6}}";

            var value = Deserialize<ProposedBaseSample>(json, settings);

            var baseValue = Assert.IsType<ProposedBaseSample>(value);
            Assert.Equal(5, baseValue.A);
        }

        [Fact]
        public void Deserialize_ProposedType_WithUnknownTypeName_AndValueWrapper_FallsBackToOriginalType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };

            const string json = "{\"$type\":\"Does.Not.Exist\",\"$value\":{\"A\":10,\"B\":20}}";
            var value = Deserialize<ProposedBaseSample>(json, settings);

            var typed = Assert.IsType<ProposedBaseSample>(value);
            Assert.Equal(10, typed.A);
            Assert.Equal(20, typed.B);
        }

        [Fact]
        public void Deserialize_ProposedType_WithIncompatibleType_AndValueWrapper_FallsBackToOriginalType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };

            const string json = "{\"$type\":\"System.String\",\"$value\":{\"A\":11,\"B\":22}}";
            var value = Deserialize<ProposedBaseSample>(json, settings);

            var typed = Assert.IsType<ProposedBaseSample>(value);
            Assert.Equal(11, typed.A);
            Assert.Equal(22, typed.B);
        }

        [Fact]
        public void Deserialize_ProposedType_FirstFieldIncompatibleWithoutValue_FallsBackToNormalObjectRead()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };

            const string json = "{\"$type\":\"System.String\",\"A\":12,\"B\":23}";
            var value = Deserialize<ProposedBaseSample>(json, settings);

            var typed = Assert.IsType<ProposedBaseSample>(value);
            Assert.Equal(12, typed.A);
            Assert.Equal(23, typed.B);
        }

        [Fact]
        public void Deserialize_ProposedType_WithoutValueWrapper_UsesDerivedType()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };

            string typeName = typeof(ProposedDerivedSample).FullName;
            string json = $"{{\"$type\":\"{typeName}\",\"A\":13,\"B\":24}}";

            var value = Deserialize<ProposedBaseSample>(json, settings);

            var derived = Assert.IsType<ProposedDerivedSample>(value);
            Assert.Equal(13, derived.A);
            Assert.Equal(24, derived.B);
        }

        [Fact]
        public void Deserialize_ProposedType_Disabled_IgnoresTypeAndValueWrapper()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore
            };

            string typeName = typeof(ProposedDerivedSample).FullName;
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"A\":1,\"B\":2}}}}";

            var value = Deserialize<ProposedBaseSample>(json, settings);

            var typed = Assert.IsType<ProposedBaseSample>(value);
            Assert.Equal(0, typed.A);
            Assert.Equal(0, typed.B);
        }

        [Fact]
        public void Deserialize_ProposedType_Disabled_ParsesNormalFields()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore
            };

            const string json = "{\"A\":31,\"B\":42}";
            var value = Deserialize<ProposedBaseSample>(json, settings);

            Assert.Equal(31, value.A);
            Assert.Equal(42, value.B);
        }

        [Fact]
        public void Populate_ProposedType_WithValueWrapper_CompatibleType_PopulatesExistingInstance()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            var deserializer = new JsonDeserializer(settings);

            var item = new ProposedDerivedSample { A = 1, B = 2 };
            var originalRef = item;

            string typeName = typeof(ProposedDerivedSample).FullName;
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"A\":7,\"B\":8}}}}";

            Assert.True(deserializer.TryPopulate(json, item));

            Assert.Same(originalRef, item);
            Assert.Equal(7, item.A);
            Assert.Equal(8, item.B);
        }

        [Fact]
        public void Populate_ProposedType_WithIncompatibleTypeAndValueWrapper_FallsBackToOriginalTypePopulate()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            var deserializer = new JsonDeserializer(settings);

            var item = new ProposedBaseSample { A = 1, B = 2 };
            const string json = "{\"$type\":\"System.String\",\"$value\":{\"A\":11,\"B\":22}}";

            Assert.True(deserializer.TryPopulate(json, item));

            Assert.Equal(11, item.A);
            Assert.Equal(22, item.B);
        }

        [Fact]
        public void Populate_ProposedType_Disabled_IgnoresTypeWrapper_AndKeepsExistingValues()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore
            };
            var deserializer = new JsonDeserializer(settings);

            var item = new ProposedBaseSample { A = 3, B = 4 };

            string typeName = typeof(ProposedDerivedSample).FullName;
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"A\":100,\"B\":200}}}}";

            Assert.True(deserializer.TryPopulate(json, item));

            Assert.Equal(3, item.A);
            Assert.Equal(4, item.B);
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

        private abstract class AbstractSample
        {
            public int Value;
        }

        private class NoDefaultCtorSample
        {
            public int X { get; }

            public NoDefaultCtorSample(int x)
            {
                X = x;
            }
        }

        private class ProposedBaseSample
        {
            public int A;
            public int B;
        }

        private class ProposedDerivedSample : ProposedBaseSample
        {
        }
        
        [Fact]
        public void Deserialize_ProposedType_RepeatedSameTypeName_GenericThenTypeOverload_UsesDerivedTypeInBothCalls()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            var deserializer = new JsonDeserializer(settings);

            string typeName = typeof(ProposedDerivedSample).FullName;
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"A\":41,\"B\":42}}}}";

            Assert.True(deserializer.TryDeserialize(json, out ProposedBaseSample first));
            Assert.True(deserializer.TryDeserialize(json, typeof(ProposedBaseSample), out object secondObj));

            var firstDerived = Assert.IsType<ProposedDerivedSample>(first);
            var secondDerived = Assert.IsType<ProposedDerivedSample>(secondObj);

            Assert.Equal(41, firstDerived.A);
            Assert.Equal(42, firstDerived.B);
            Assert.Equal(41, secondDerived.A);
            Assert.Equal(42, secondDerived.B);
        }

        [Fact]
        public void Deserialize_ProposedType_RepeatedSameTypeName_TypeOverloadThenGeneric_UsesDerivedTypeInBothCalls()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            var deserializer = new JsonDeserializer(settings);

            string typeName = typeof(ProposedDerivedSample).FullName;
            string json = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"A\":51,\"B\":52}}}}";

            Assert.True(deserializer.TryDeserialize(json, typeof(ProposedBaseSample), out object firstObj));
            Assert.True(deserializer.TryDeserialize(json, out ProposedBaseSample second));

            var firstDerived = Assert.IsType<ProposedDerivedSample>(firstObj);
            var secondDerived = Assert.IsType<ProposedDerivedSample>(second);

            Assert.Equal(51, firstDerived.A);
            Assert.Equal(52, firstDerived.B);
            Assert.Equal(51, secondDerived.A);
            Assert.Equal(52, secondDerived.B);
        }

        [Fact]
        public void Deserialize_ProposedType_RepeatedSameTypeName_WithAndWithoutValueWrapper_UsesDerivedTypeInBothCalls()
        {
            var settings = new JsonDeserializer.Settings
            {
                proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways
            };
            var deserializer = new JsonDeserializer(settings);

            string typeName = typeof(ProposedDerivedSample).FullName;
            string wrappedJson = $"{{\"$type\":\"{typeName}\",\"$value\":{{\"A\":61,\"B\":62}}}}";
            string embeddedJson = $"{{\"$type\":\"{typeName}\",\"A\":63,\"B\":64}}";

            Assert.True(deserializer.TryDeserialize(wrappedJson, out ProposedBaseSample wrapped));
            Assert.True(deserializer.TryDeserialize(embeddedJson, out ProposedBaseSample embedded));

            var wrappedDerived = Assert.IsType<ProposedDerivedSample>(wrapped);
            var embeddedDerived = Assert.IsType<ProposedDerivedSample>(embedded);

            Assert.Equal(61, wrappedDerived.A);
            Assert.Equal(62, wrappedDerived.B);
            Assert.Equal(63, embeddedDerived.A);
            Assert.Equal(64, embeddedDerived.B);
        }
    }
}