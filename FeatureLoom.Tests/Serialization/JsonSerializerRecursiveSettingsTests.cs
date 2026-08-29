using FeatureLoom.Serialization;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonSerializerRecursiveSettingsTests
{
    public enum Color { Red, Green, Blue }

    public class Root
    {
        public Color Own;
        public Child Child;
        public List<Color> Colors;
    }

    public class Child
    {
        public Color Value;
        public GrandChild GrandChild;
    }

    public class GrandChild
    {
        public Color Value;
    }

    public class Node
    {
        public Color Value;
        public Node Next;
    }

    public class ObjectRoot
    {
        public object Value;
        public DynamicRoot Dynamic;
        public IEnumerable Values;
    }

    public class DynamicRoot
    {
        public Dictionary<string, object> Values = new();
    }

    public class ByteRoot
    {
        public byte[] Bytes;
        public ByteChild Child;
    }

    public class ByteChild
    {
        public byte[] Bytes;
    }

    public class DictionaryRoot
    {
        public Dictionary<string, Color> Values;
        public Dictionary<Color, int> EnumKeys;
    }

    public class PropertyRoot
    {
        public string PublicField;
        private string PrivateField = "private";
        public string PublicProperty { get; set; }
        public PropertyChild Child;
    }

    public class PropertyChild
    {
        public string PublicField;
        private string PrivateField = "private";
        public string PublicProperty { get; set; }
    }

    public class CustomRoot
    {
        public CustomChild Child;
    }

    public class CustomChild
    {
        public Color Value;
    }

    public class NestedCustomRoot
    {
        public Color Own;
        public Child Nested;
    }

    public class EnumerableRoot
    {
        public LazyColors Values;
    }

    public class LazyColors : IEnumerable<Color>
    {
        public string Label = "colors";
        public IEnumerator<Color> GetEnumerator() => ((IEnumerable<Color>)new[] { Color.Green }).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class AlternateRoot
    {
        public Child Child;
    }

    public class GenericRoot<T>
    {
        public T Value;
    }

    public class TypeInfoRoot
    {
        public Child Child;
        public List<Color> Colors;
    }

    public class CustomArrayRoot
    {
        public List<Color> Colors;
    }

    public class ExistingFieldsRoot
    {
        public Color Value;
        public string Ignored;
    }

    public class ScopedRoot
    {
        public Child RecursiveChild;
        public Child PlainChild;
        public List<Child> RecursiveItems;
        public List<Child> PlainItems;
    }

    public class PolymorphicRoot
    {
        public object First;
        public object Second;
        public object Third;
    }

    public class OtherChild
    {
        public Color Value;
    }

    static JsonSerializer CreateSerializer(System.Action<JsonSerializer.Settings> configure)
    {
        var settings = new JsonSerializer.Settings
        {
            enumAsString = false,
            typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
            referenceCheck = JsonSerializer.ReferenceCheck.NoRefCheck
        };
        configure(settings);
        return new JsonSerializer(settings);
    }

    [Fact]
    public void RecursiveSettings_ApplyToDeclaringTypeMembersAndNestedValues()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Root>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        var json = serializer.Serialize(new Root
        {
            Own = Color.Red,
            Child = new Child { Value = Color.Green, GrandChild = new GrandChild { Value = Color.Blue } },
            Colors = new List<Color> { Color.Green }
        });

        Assert.Equal("{\"Own\":\"Red\",\"Child\":{\"Value\":\"Green\",\"GrandChild\":{\"Value\":\"Blue\"}},\"Colors\":[\"Green\"]}", json);
    }

    [Fact]
    public void RecursiveSettings_LocalTypeSettingWins()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<Root>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<Color>(ts => ts.SetEnumAsString(false));
        });

        Assert.Contains("\"Own\":0", serializer.Serialize(new Root { Own = Color.Red }));
    }

    [Fact]
    public void RecursiveSettings_MemberSettingWins()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Root>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true));
            ts.ConfigureMember<Color>(nameof(Root.Own), ms => ms.SetEnumAsString(false));
        }));

        Assert.Contains("\"Own\":0", serializer.Serialize(new Root { Own = Color.Red }));
    }

    [Fact]
    public void RecursiveSettings_NestedContextsAreLayered()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<Root>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<Child>(ts => ts.ConfigureRecursively(rs => rs.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo)));
        });

        var json = serializer.Serialize(new Root { Child = new Child { GrandChild = new GrandChild { Value = Color.Blue } } });

        Assert.Contains("\"Value\":\"Blue\"", json);
    }

    [Fact]
    public void RecursiveSettings_SelfReferencingTypeTerminatesDuringPreparation()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Node>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        Assert.Equal("{\"Value\":\"Green\",\"Next\":null}", serializer.Serialize(new Node { Value = Color.Green }));
    }

    [Fact]
    public void RecursiveSettings_DoNotLeakToSiblingRootValues()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Root>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        Assert.Equal("1", serializer.Serialize(Color.Green));
    }

    [Fact]
    public void RecursiveSettings_FollowDeviatingRuntimeTypes()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<ObjectRoot>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        var json = serializer.Serialize(new ObjectRoot { Value = new Child { Value = Color.Green } });

        Assert.Contains("\"Value\":\"Green\"", json);
    }

    [Fact]
    public void RecursiveSettings_FollowDynamicFieldRuntimeTypes()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<ObjectRoot>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<DynamicRoot>(ts => ts.SetCustomTypeWriter(prep =>
                prep.PrepareObjectWriter<DynamicRoot>(obj => obj.AddDynamicFields((fields, item) =>
                {
                    foreach (var entry in item.Values) fields.WriteField(entry.Key, entry.Value);
                }))));
        });

        var root = new ObjectRoot { Dynamic = new DynamicRoot() };
        root.Dynamic.Values["color"] = Color.Blue;

        Assert.Contains("\"color\":\"Blue\"", serializer.Serialize(root));
    }

    [Fact]
    public void RecursiveSettings_FollowNonGenericEnumerableRuntimeTypes()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<ObjectRoot>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        var json = serializer.Serialize(new ObjectRoot { Values = new ArrayList { Color.Green } });

        Assert.Contains("\"Values\":[\"Green\"]", json);
    }

    [Fact]
    public void RecursiveSettings_TypeWriterPreparedOutsideContextDoesNotBypassRecursiveVariant()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Root>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        Assert.Equal("1", serializer.Serialize(Color.Green));
        Assert.Contains("\"Own\":\"Green\"", serializer.Serialize(new Root { Own = Color.Green }));
        Assert.Equal("1", serializer.Serialize(Color.Green));
    }

    [Fact]
    public void RecursiveSettings_ContextualWriterPreparedFirstDoesNotLeakIntoSharedWriter()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Root>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        Assert.Contains("\"Own\":\"Green\"", serializer.Serialize(new Root { Own = Color.Green }));
        Assert.Equal("1", serializer.Serialize(Color.Green));
    }

    [Fact]
    public void RecursiveSettings_ConfigureRecursivelyCalledTwiceBuildsOneLocalConfiguration()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Root>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true));
            ts.ConfigureRecursively(rs => rs.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo));
        }));

        Assert.Contains("\"Own\":\"Green\"", serializer.Serialize(new Root { Own = Color.Green }));
    }

    [Fact]
    public void RecursiveSettings_NullConfigurationRemovesRecursiveSettings()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Root>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true));
            ts.ConfigureRecursively(null);
        }));

        Assert.Contains("\"Own\":1", serializer.Serialize(new Root { Own = Color.Green }));
    }

    [Fact]
    public void RecursiveSettings_ByteArrayFormattingPropagatesAndMemberOverrideWins()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<ByteRoot>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetWriteByteArrayAsBase64String(true));
            ts.ConfigureMember<byte[]>(nameof(ByteRoot.Bytes), ms => ms.SetWriteByteArrayAsBase64String(false));
        }));

        var json = serializer.Serialize(new ByteRoot
        {
            Bytes = new byte[] { 1, 2 },
            Child = new ByteChild { Bytes = new byte[] { 1, 2 } }
        });

        Assert.Equal("{\"Bytes\":[1,2],\"Child\":{\"Bytes\":\"AQI=\"}}", json);
    }

    [Fact]
    public void RecursiveSettings_DataSelectionAppliesToDeclaringAndNestedTypes()
    {
        var serializer = CreateSerializer(s =>
        {
            s.dataSelection = JsonSerializer.DataSelection.PublicAndPrivateFields_RemoveBackingFields;
            s.ConfigureType<PropertyRoot>(ts => ts.ConfigureRecursively(rs =>
                rs.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties)));
        });

        var json = serializer.Serialize(new PropertyRoot
        {
            PublicField = "field",
            PublicProperty = "property",
            Child = new PropertyChild { PublicField = "childField", PublicProperty = "childProperty" }
        });

        Assert.Contains("\"PublicField\":\"field\"", json);
        Assert.Contains("\"PublicProperty\":\"property\"", json);
        Assert.DoesNotContain("PrivateField", json);
        Assert.Contains("\"Child\":{", json);
        Assert.Contains("\"PublicField\":\"childField\"", json);
        Assert.Contains("\"PublicProperty\":\"childProperty\"", json);
    }

    [Fact]
    public void RecursiveSettings_DictionaryShapeAndValueFormattingPropagateTogether()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<DictionaryRoot>(ts =>
            ts.ConfigureRecursively(rs =>
            {
                rs.SetDictionaryShape(JsonSerializer.DictionaryShape.KeyValuePairArray);
                rs.SetEnumAsString(true);
            })));

        var json = serializer.Serialize(new DictionaryRoot
        {
            Values = new Dictionary<string, Color> { ["a"] = Color.Green }
        });

        Assert.StartsWith("{\"Values\":[", json);
        Assert.Contains("\"key\":\"a\"", json);
        Assert.Contains("\"value\":\"Green\"", json);
    }

    [Fact]
    public void RecursiveSettings_DictionaryTypeSettingOverridesRecursiveShapeButValuesStayRecursive()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<DictionaryRoot>(ts => ts.ConfigureRecursively(rs =>
            {
                rs.SetDictionaryShape(JsonSerializer.DictionaryShape.KeyValuePairArray);
                rs.SetEnumAsString(true);
            }));
            s.ConfigureType<Dictionary<string, Color>>(ts => ts.SetDictionaryShape(JsonSerializer.DictionaryShape.Auto));
        });

        var json = serializer.Serialize(new DictionaryRoot
        {
            Values = new Dictionary<string, Color> { ["a"] = Color.Green }
        });

        Assert.Contains("\"Values\":{\"a\":\"Green\"}", json);
    }

    [Fact]
    public void RecursiveSettings_ReachValuesWrittenByCustomAddField()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<CustomRoot>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<CustomChild>(ts => ts.SetCustomTypeWriter(prep =>
                prep.PrepareObjectWriter<CustomChild>(obj => obj.AddField("renamed", child => child.Value))));
        });

        var json = serializer.Serialize(new CustomRoot { Child = new CustomChild { Value = Color.Blue } });

        Assert.Equal("{\"Child\":{\"renamed\":\"Blue\"}}", json);
    }

    [Fact]
    public void RecursiveSettings_DeclaredByCustomWrittenTypeLayerOntoInheritedContext()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<CustomRoot>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<CustomChild>(ts =>
            {
                ts.ConfigureRecursively(rs => rs.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo));
                ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<CustomChild>(obj =>
                    obj.AddField("renamed", child => child.Value)));
            });
        });

        var json = serializer.Serialize(new CustomRoot { Child = new CustomChild { Value = Color.Blue } });

        Assert.Equal("{\"Child\":{\"renamed\":\"Blue\"}}", json);
    }

    [Fact]
    public void RecursiveSettings_CustomNestedObjectBuilderInheritsContext()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<NestedCustomRoot>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true));
            ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<NestedCustomRoot>(obj => obj
                .AddField("own", root => root.Own)
                .AddObject("nested", root => root.Nested, nested => nested
                    .AddField("value", child => child.Value))));
        }));

        var json = serializer.Serialize(new NestedCustomRoot
        {
            Own = Color.Green,
            Nested = new Child { Value = Color.Blue }
        });

        Assert.Equal("{\"own\":\"Green\",\"nested\":{\"value\":\"Blue\"}}", json);
    }

    [Fact]
    public void RecursiveSettings_EnumFormattingDoesNotChangeDictionaryKeys()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<DictionaryRoot>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        var json = serializer.Serialize(new DictionaryRoot
        {
            EnumKeys = new Dictionary<Color, int> { [Color.Green] = 1 }
        });

        Assert.Contains("\"1\":1", json);
        Assert.DoesNotContain("\"Green\":1", json);
    }

    [Fact]
    public void RecursiveSettings_ElementSettingWinsWhileOtherRecursiveSettingsRemain()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Root>(ts =>
        {
            ts.ConfigureRecursively(rs =>
            {
                rs.SetEnumAsString(true);
                rs.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo);
            });
            ts.ConfigureMember<List<Color>>(nameof(Root.Colors), ms =>
                ms.ConfigureElement<Color>(es => es.SetEnumAsString(false)));
        }));

        var json = serializer.Serialize(new Root
        {
            Own = Color.Green,
            Colors = new List<Color> { Color.Blue }
        });

        Assert.Contains("\"Own\":\"Green\"", json);
        Assert.Contains("\"Colors\":[2]", json);
    }

    [Fact]
    public void RecursiveSettings_TreatEnumerablesAsCollectionsPropagates()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<EnumerableRoot>(ts =>
            ts.ConfigureRecursively(rs =>
            {
                rs.SetTreatEnumerablesAsCollections(true);
                rs.SetEnumAsString(true);
            })));

        Assert.Equal("{\"Values\":[\"Green\"]}", serializer.Serialize(new EnumerableRoot { Values = new LazyColors() }));
    }

    [Fact]
    public void RecursiveSettings_LocalEnumerableSettingOverridesRecursiveSetting()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<EnumerableRoot>(ts => ts.ConfigureRecursively(rs =>
                rs.SetTreatEnumerablesAsCollections(true)));
            s.ConfigureType<LazyColors>(ts => ts.SetTreatEnumerablesAsCollections(false));
        });

        var json = serializer.Serialize(new EnumerableRoot { Values = new LazyColors() });

        Assert.Contains("\"Label\":\"colors\"", json);
        Assert.DoesNotContain("[1]", json);
    }

    [Fact]
    public void RecursiveSettings_CustomAddFieldLocalSettingWinsOnlyForThatField()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<NestedCustomRoot>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true));
            ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<NestedCustomRoot>(obj => obj
                .AddField("numeric", root => root.Own, fs => fs.SetEnumAsString(false))
                .AddField("text", root => root.Nested.Value)));
        }));

        var json = serializer.Serialize(new NestedCustomRoot
        {
            Own = Color.Green,
            Nested = new Child { Value = Color.Blue }
        });

        Assert.Equal("{\"numeric\":1,\"text\":\"Blue\"}", json);
    }

    [Fact]
    public void RecursiveSettings_InnerRecursiveSettingWinsOnConflictAndOuterStillAppliesElsewhere()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<Root>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<Child>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(false)));
        });

        var json = serializer.Serialize(new Root
        {
            Own = Color.Green,
            Child = new Child
            {
                Value = Color.Green,
                GrandChild = new GrandChild { Value = Color.Blue }
            }
        });

        Assert.Contains("\"Own\":\"Green\"", json);
        Assert.Contains("\"Child\":{\"Value\":1", json);
        Assert.Contains("\"GrandChild\":{\"Value\":2}", json);
    }

    [Fact]
    public void RecursiveSettings_SameTypeGetsDistinctWritersForDistinctRecursiveContexts()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<Root>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<AlternateRoot>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(false)));
        });

        var textJson = serializer.Serialize(new Root { Child = new Child { Value = Color.Green } });
        var numericJson = serializer.Serialize(new AlternateRoot { Child = new Child { Value = Color.Green } });
        var textAgainJson = serializer.Serialize(new Root { Child = new Child { Value = Color.Green } });

        Assert.Contains("\"Value\":\"Green\"", textJson);
        Assert.Contains("\"Value\":1", numericJson);
        Assert.Contains("\"Value\":\"Green\"", textAgainJson);
    }

    [Fact]
    public void RecursiveSettings_EquivalentContextOnRecursiveTypeReusesStableWriter()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<Node>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<Root>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
        });

        var json = serializer.Serialize(new Root
        {
            Child = new Child { GrandChild = new GrandChild { Value = Color.Green } }
        });
        var nodeJson = serializer.Serialize(new Node { Value = Color.Blue });

        Assert.Contains("\"Value\":\"Green\"", json);
        Assert.Equal("{\"Value\":\"Blue\",\"Next\":null}", nodeJson);
    }

    [Fact]
    public void RecursiveSettings_OnGenericTypeDefinitionApplyToConstructedTypeAndChildren()
    {
        var serializer = CreateSerializer(s => s.ConfigureGenericType(typeof(GenericRoot<>), ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        var json = serializer.Serialize(new GenericRoot<Child>
        {
            Value = new Child { Value = Color.Green }
        });

        Assert.Contains("\"Value\":\"Green\"", json);
    }

    [Fact]
    public void RecursiveSettings_ConcreteTypeSettingWinsOverGenericRecursiveSetting()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureGenericType(typeof(GenericRoot<>), ts =>
                ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<Color>(ts => ts.SetEnumAsString(false));
        });

        Assert.Equal("{\"Value\":1}", serializer.Serialize(new GenericRoot<Color> { Value = Color.Green }));
    }

    [Fact]
    public void RecursiveSettings_TypeInfoHandlingAndFormatApplyToObjects()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<TypeInfoRoot>(ts =>
            ts.ConfigureRecursively(rs =>
            {
                rs.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddAllTypeInfo);
                rs.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.AlwaysEnvelope);
            })));

        var json = serializer.Serialize(new TypeInfoRoot { Child = new Child() });

        Assert.StartsWith("{\"$type\":", json);
        Assert.Contains("\"$value\":{", json);
        Assert.Contains("\"Child\":{\"$type\":", json);
        Assert.Contains("\"$value\":{\"Value\":", json);
    }

    [Fact]
    public void RecursiveSettings_ArrayValueFieldNameAppliesToNestedArrayEnvelope()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<TypeInfoRoot>(ts =>
            ts.ConfigureRecursively(rs =>
            {
                rs.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddAllTypeInfo);
                rs.SetArrayValueFieldName(JsonSerializer.ValueFieldName.Values);
            })));

        var json = serializer.Serialize(new TypeInfoRoot { Colors = new List<Color> { Color.Green } });

        Assert.Contains("\"Colors\":{\"$type\":", json);
        Assert.Contains("\"$values\":[", json);
    }

    [Fact]
    public void RecursiveSettings_LocalTypeInfoFormatWinsButRecursiveHandlingContinuesBelow()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureType<TypeInfoRoot>(ts => ts.ConfigureRecursively(rs =>
            {
                rs.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddAllTypeInfo);
                rs.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.AlwaysEnvelope);
            }));
            s.ConfigureType<Child>(ts => ts.SetTypeInfoFormat(JsonSerializer.TypeInfoFormat.InlineForObjects));
        });

        var json = serializer.Serialize(new TypeInfoRoot
        {
            Child = new Child { GrandChild = new GrandChild() }
        });

        Assert.Contains("\"Child\":{\"$type\":", json);
        Assert.DoesNotContain("\"Child\":{\"$type\":" +
            "\"JsonSerializerRecursiveSettingsTests.Child\",\"$value\":", json);
        Assert.Contains("\"GrandChild\":{\"$type\":", json);
        Assert.Contains("\"$value\":{\"Value\":", json);
    }

    [Fact]
    public void RecursiveSettings_CustomAddArrayInheritsContextAndElementOverrideWinsLocally()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<CustomArrayRoot>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true));
            ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<CustomArrayRoot>(obj => obj
                .AddArray("text", root => root.Colors)
                .AddArray("numeric", root => root.Colors, es => es.SetEnumAsString(false))));
        }));

        var json = serializer.Serialize(new CustomArrayRoot { Colors = new List<Color> { Color.Green } });

        Assert.Equal("{\"text\":[\"Green\"],\"numeric\":[1]}", json);
    }

    [Fact]
    public void RecursiveSettings_AddExistingFieldsHonorsMemberConfigurationAndContext()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<ExistingFieldsRoot>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true));
            ts.ConfigureMember<string>(nameof(ExistingFieldsRoot.Ignored), ms => ms.SetIgnore());
            ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<ExistingFieldsRoot>(obj => obj
                .AddExistingFields()
                .AddField("extra", root => root.Value, fs => fs.SetEnumAsString(false))));
        }));

        var json = serializer.Serialize(new ExistingFieldsRoot { Value = Color.Blue, Ignored = "secret" });

        Assert.Equal("{\"Value\":\"Blue\",\"extra\":2}", json);
    }

    [Fact]
    public void RecursiveSettings_NullAndEmptyContainersRemainValid()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<Root>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        Assert.Equal("{\"Own\":\"Red\",\"Child\":null,\"Colors\":null}",
            serializer.Serialize(new Root { Own = Color.Red }));
        Assert.Equal("{\"Own\":\"Red\",\"Child\":null,\"Colors\":[]}",
            serializer.Serialize(new Root { Own = Color.Red, Colors = new List<Color>() }));
    }

    [Fact]
    public void RecursiveSettings_CircularReferenceUsesConfiguredReferenceHandling()
    {
        var settings = new JsonSerializer.Settings
        {
            enumAsString = false,
            typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
            referenceCheck = JsonSerializer.ReferenceCheck.OnLoopReplaceByNull
        };
        settings.ConfigureType<Node>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
        var serializer = new JsonSerializer(settings);
        var node = new Node { Value = Color.Green };
        node.Next = node;

        Assert.Equal("{\"Value\":\"Green\",\"Next\":null}", serializer.Serialize(node));
    }

    [Fact]
    public void RecursiveSettings_OnMemberApplyOnlyToThatMemberSubtree()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<ScopedRoot>(ts =>
            ts.ConfigureMember<Child>(nameof(ScopedRoot.RecursiveChild), ms =>
                ms.ConfigureRecursively(rs => rs.SetEnumAsString(true)))));

        var json = serializer.Serialize(new ScopedRoot
        {
            RecursiveChild = new Child { Value = Color.Green, GrandChild = new GrandChild { Value = Color.Blue } },
            PlainChild = new Child { Value = Color.Green, GrandChild = new GrandChild { Value = Color.Blue } }
        });

        Assert.Contains("\"RecursiveChild\":{\"Value\":\"Green\",\"GrandChild\":{\"Value\":\"Blue\"}}", json);
        Assert.Contains("\"PlainChild\":{\"Value\":1,\"GrandChild\":{\"Value\":2}}", json);
    }

    [Fact]
    public void RecursiveSettings_OnElementApplyToEachElementSubtreeOnly()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<ScopedRoot>(ts =>
            ts.ConfigureMember<List<Child>>(nameof(ScopedRoot.RecursiveItems), ms =>
                ms.ConfigureElement<Child>(es =>
                    es.ConfigureRecursively(rs => rs.SetEnumAsString(true))))));

        var json = serializer.Serialize(new ScopedRoot
        {
            RecursiveItems = new List<Child> { new Child { Value = Color.Green, GrandChild = new GrandChild { Value = Color.Blue } } },
            PlainItems = new List<Child> { new Child { Value = Color.Green, GrandChild = new GrandChild { Value = Color.Blue } } }
        });

        Assert.Contains("\"RecursiveItems\":[{\"Value\":\"Green\",\"GrandChild\":{\"Value\":\"Blue\"}}]", json);
        Assert.Contains("\"PlainItems\":[{\"Value\":1,\"GrandChild\":{\"Value\":2}}]", json);
    }

    [Fact]
    public void RecursiveSettings_OnMemberLayerOntoTypeRecursiveSettings()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<ScopedRoot>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true));
            ts.ConfigureMember<Child>(nameof(ScopedRoot.RecursiveChild), ms =>
                ms.ConfigureRecursively(rs => rs.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties)));
        }));

        var json = serializer.Serialize(new ScopedRoot
        {
            RecursiveChild = new Child { Value = Color.Green, GrandChild = new GrandChild { Value = Color.Blue } }
        });

        Assert.Contains("\"RecursiveChild\":{\"Value\":\"Green\",\"GrandChild\":{\"Value\":\"Blue\"}}", json);
    }

    [Fact]
    public void RecursiveSettings_IdReferenceHandlingWorksWithContextSpecificWriters()
    {
        var settings = new JsonSerializer.Settings
        {
            enumAsString = false,
            typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
            referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef,
            referenceFormat = JsonSerializer.ReferenceFormat.IdBased
        };
        settings.ConfigureType<ScopedRoot>(ts => ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
        var serializer = new JsonSerializer(settings);
        var child = new Child { Value = Color.Green };
        var root = new ScopedRoot { RecursiveChild = child, PlainChild = child };

        var json = serializer.Serialize(root);

        Assert.Contains("\"Value\":\"Green\"", json);
        Assert.Contains("\"$ref\":", json);
    }

    [Fact]
    public void RecursiveSettings_RepeatedPolymorphicRuntimeTypesKeepCapturedContext()
    {
        var serializer = CreateSerializer(s => s.ConfigureType<PolymorphicRoot>(ts =>
            ts.ConfigureRecursively(rs => rs.SetEnumAsString(true))));

        var json = serializer.Serialize(new PolymorphicRoot
        {
            First = new Child { Value = Color.Green },
            Second = new OtherChild { Value = Color.Blue },
            Third = new Child { Value = Color.Red }
        });

        Assert.Contains("\"First\":{\"Value\":\"Green\"", json);
        Assert.Contains("\"Second\":{\"Value\":\"Blue\"}", json);
        Assert.Contains("\"Third\":{\"Value\":\"Red\"", json);
    }
}
