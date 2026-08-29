using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonSerializerOpenGenericSettingsTests
{
    public enum Color { Red, Green, Blue }

    public class Box<T>
    {
        public T Value;
        public string Label;
        public FixedChild Child;
    }

    public class FixedChild
    {
        public Color Color;
    }

    public readonly struct Key
    {
        public readonly int Value;
        public Key(int value) => Value = value;
    }

    static JsonSerializer CreateSerializer(Action<JsonSerializer.Settings> configure)
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
    public void OpenGenericSettings_TypePolicyAppliesToEveryConstructedType()
    {
        var serializer = CreateSerializer(s => s.ConfigureGenericType(typeof(Box<>), ts =>
            ts.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties)));

        Assert.Equal("{\"Value\":1,\"Label\":\"a\",\"Child\":null}",
            serializer.Serialize(new Box<int> { Value = 1, Label = "a" }));
        Assert.Equal("{\"Value\":\"x\",\"Label\":\"b\",\"Child\":null}",
            serializer.Serialize(new Box<string> { Value = "x", Label = "b" }));
    }

    [Fact]
    public void OpenGenericSettings_FixedMemberConfigurationAppliesToEveryConstructedType()
    {
        var serializer = CreateSerializer(s => s.ConfigureGenericType(typeof(Box<>), ts =>
            ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.OverrideName("name"))));

        Assert.Contains("\"name\":\"a\"", serializer.Serialize(new Box<int> { Label = "a" }));
        Assert.Contains("\"name\":\"b\"", serializer.Serialize(new Box<string> { Label = "b" }));
    }

    [Fact]
    public void OpenGenericSettings_GenericDependentMemberCannotBeConfiguredAsConcreteType()
    {
        Assert.Throws<Exception>(() => CreateSerializer(s => s.ConfigureGenericType(typeof(Box<>), ts =>
            ts.ConfigureMember<int>(nameof(Box<int>.Value), ms => ms.SetEnumAsString(true)))));
    }

    [Fact]
    public void OpenGenericSettings_MismatchingFixedMemberTypeThrows()
    {
        Assert.Throws<Exception>(() => CreateSerializer(s => s.ConfigureGenericType(typeof(Box<>), ts =>
            ts.ConfigureMember<int>(nameof(Box<int>.Label), ms => ms.SetEnumAsString(true)))));
    }

    [Fact]
    public void OpenGenericSettings_ElementConfigurationAppliesOnlyToMatchingConstruction()
    {
        var serializer = CreateSerializer(s => s.ConfigureGenericType(typeof(List<>), ts =>
            ts.ConfigureElement<Color>(es => es.SetEnumAsString(true))));

        Assert.Equal("[\"Green\"]", serializer.Serialize(new List<Color> { Color.Green }));
        Assert.Equal("[1]", serializer.Serialize(new List<int> { 1 }));
    }

    [Fact]
    public void OpenGenericSettings_KeyFormatterAppliesOnlyToMatchingConstruction()
    {
        var serializer = CreateSerializer(s => s.ConfigureGenericType(typeof(Dictionary<,>), ts =>
            ts.ConfigureKey<Key>(key => $"key-{key.Value}")));

        Assert.Equal("{\"key-2\":\"x\"}",
            serializer.Serialize(new Dictionary<Key, string> { [new Key(2)] = "x" }));
        Assert.Equal("{\"plain\":\"x\"}",
            serializer.Serialize(new Dictionary<string, string> { ["plain"] = "x" }));
    }

    [Fact]
    public void OpenGenericSettings_ReconfigurationAccumulatesSettings()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureGenericType(typeof(Box<>), ts => ts.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties));
            s.ConfigureGenericType(typeof(Box<>), ts =>
                ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.SetIgnore()));
        });

        var json = serializer.Serialize(new Box<int> { Value = 1, Label = "hidden" });

        Assert.Equal("{\"Value\":1,\"Child\":null}", json);
    }

    [Fact]
    public void OpenGenericSettings_NullConfigurationRemovesEntry()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureGenericType(typeof(Box<>), ts =>
                ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.SetIgnore()));
            s.ConfigureGenericType(typeof(Box<>), null);
        });

        Assert.Contains("\"Label\":\"visible\"", serializer.Serialize(new Box<int> { Label = "visible" }));
    }

    [Fact]
    public void ConfigureGenericType_NullTypeThrowsArgumentNullException()
    {
        var settings = new JsonSerializer.Settings();

        Assert.Throws<ArgumentNullException>(() => settings.ConfigureGenericType(null, _ => { }));
    }

    [Fact]
    public void ConfigureGenericType_NonGenericTypeThrowsArgumentException()
    {
        var settings = new JsonSerializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureGenericType(typeof(string), _ => { }));
    }

    [Fact]
    public void ConfigureGenericType_ConstructedGenericTypeThrowsArgumentException()
    {
        var settings = new JsonSerializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureGenericType(typeof(Box<int>), _ => { }));
    }

    [Fact]
    public void ClosedSettingsKeepUnspecifiedOpenGenericMemberSettings()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureGenericType(typeof(Box<>), ts =>
                ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.SetIgnore()));
            s.ConfigureType<Box<int>>(ts => ts.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties));
        });

        var intJson = serializer.Serialize(new Box<int> { Value = 1, Label = "hidden" });
        var stringJson = serializer.Serialize(new Box<string> { Value = "x", Label = "hidden" });

        Assert.DoesNotContain("Label", intJson);
        Assert.DoesNotContain("Label", stringJson);
    }

    [Fact]
    public void ClosedMemberSettingsOverrideSameOpenGenericMemberAndKeepOtherOpenSettings()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureGenericType(typeof(Box<>), ts =>
            {
                ts.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties);
                ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.SetIgnore());
            });
            s.ConfigureType<Box<int>>(ts =>
                ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.OverrideName("name")));
        });

        var json = serializer.Serialize(new Box<int> { Value = 1, Label = "visible" });

        Assert.Contains("\"name\":\"visible\"", json);
        Assert.DoesNotContain("\"Label\"", json);
    }

    [Fact]
    public void ClosedSettingsKeepOpenGenericRecursiveSettings()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureGenericType(typeof(Box<>), ts =>
                ts.ConfigureRecursively(rs => rs.SetEnumAsString(true)));
            s.ConfigureType<Box<FixedChild>>(ts =>
                ts.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties));
        });

        var json = serializer.Serialize(new Box<FixedChild>
        {
            Child = new FixedChild { Color = Color.Green }
        });

        Assert.Contains("\"Color\":\"Green\"", json);
    }

    [Fact]
    public void ClosedRecursiveSettingsLayerOntoOpenGenericRecursiveSettings()
    {
        var serializer = CreateSerializer(s =>
        {
            s.ConfigureGenericType(typeof(Box<>), ts => ts.ConfigureRecursively(rs =>
            {
                rs.SetEnumAsString(true);
                rs.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo);
            }));
            s.ConfigureType<Box<FixedChild>>(ts => ts.ConfigureRecursively(rs =>
                rs.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties)));
        });

        var json = serializer.Serialize(new Box<FixedChild>
        {
            Child = new FixedChild { Color = Color.Blue }
        });

        Assert.Contains("\"Color\":\"Blue\"", json);
    }
}
