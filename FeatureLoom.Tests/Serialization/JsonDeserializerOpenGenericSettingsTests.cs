using FeatureLoom.Serialization;
using System;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerOpenGenericSettingsTests
{
    public class Box<T>
    {
        public T Value;
        public string Label;
        public int Number;
    }

    public class Leaf
    {
        public int Value;
    }

    public class LeafHolder
    {
        public Leaf Item;
    }

    public interface IItem
    {
    }

    public class Item : IItem
    {
        public int Value;
        public int Other;
    }

    static JsonDeserializer CreateDeserializer(Action<JsonDeserializer.Settings> configure)
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        configure(settings);
        return new JsonDeserializer(settings);
    }

    [Fact]
    public void ConfigureGenericType_NullTypeThrowsArgumentNullException()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentNullException>(() => settings.ConfigureGenericType(null, _ => { }));
    }

    [Fact]
    public void ConfigureGenericType_NonGenericTypeThrowsArgumentException()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureGenericType(typeof(string), _ => { }));
    }

    [Fact]
    public void ConfigureGenericType_ConstructedGenericTypeThrowsArgumentException()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureGenericType(typeof(Box<int>), _ => { }));
    }

    [Fact]
    public void ConfigureType_NullTypeThrowsArgumentNullException()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentNullException>(() => settings.ConfigureType(null, _ => { }));
    }

    [Fact]
    public void ConfigureType_GenericDefinitionThrowsArgumentException()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureType(typeof(Box<>), _ => { }));
    }

    [Fact]
    public void RuntimeConfigureType_InteroperatesWithGenericConfigureType()
    {
        var settings = new JsonDeserializer.Settings();
        settings.ConfigureType(typeof(Leaf), ts => ts.SetProposedTypeHandling(false));
        settings.ConfigureType<Leaf>(ts =>
            ts.ConfigureMember<int>(nameof(Leaf.Value), ms => ms.OverrideName("renamed")));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"renamed\":3}", out Leaf value));
        Assert.Equal(3, value.Value);
    }

    [Fact]
    public void RuntimeConfigureType_NullConfigurationRemovesEntry()
    {
        var settings = new JsonDeserializer.Settings();
        settings.ConfigureType(typeof(Leaf), ts => ts.SetProposedTypeHandling(false));
        settings.ConfigureType(typeof(Leaf), null);
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"Value\":4}", out Leaf value));
        Assert.Equal(4, value.Value);
    }

    [Fact]
    public void ClosedSettingsKeepUnspecifiedOpenGenericMemberSettings()
    {
        var deserializer = CreateDeserializer(s =>
        {
            s.ConfigureGenericType(typeof(Box<>), ts =>
            {
                ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.OverrideName("genericLabel"));
                ts.ConfigureMember<int>(nameof(Box<int>.Number), ms => ms.OverrideName("genericNumber"));
            });
            s.ConfigureType<Box<int>>(ts =>
                ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.OverrideName("exactLabel")));
        });

        Assert.True(deserializer.TryDeserialize(
            "{\"Value\":1,\"exactLabel\":\"x\",\"genericNumber\":2}", out Box<int> value));
        Assert.Equal("x", value.Label);
        Assert.Equal(2, value.Number);
    }

    [Fact]
    public void ClosedSettingsOverrideOpenGenericScalarSettingsRegardlessOfRegistrationOrder()
    {
        foreach (bool exactFirst in new[] { false, true })
        {
            var settings = new JsonDeserializer.Settings();
            Action configureGeneric = () => settings.ConfigureGenericType(typeof(Box<>), ts => ts.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields));
            Action configureExact = () => settings.ConfigureType<Box<int>>(ts => ts.SetDataAccess(JsonDeserializer.DataAccess.PublicFieldsAndProperties));
            if (exactFirst)
            {
                configureExact();
                configureGeneric();
            }
            else
            {
                configureGeneric();
                configureExact();
            }

            var deserializer = new JsonDeserializer(settings);
            Assert.True(deserializer.TryDeserialize("{\"Value\":7}", out Box<int> value));
            Assert.Equal(7, value.Value);
        }
    }

    [Fact]
    public void MemberLocalSettingsMergeOntoConfiguredMemberTypeSettings()
    {
        var deserializer = CreateDeserializer(s =>
        {
            s.ConfigureType<Leaf>(ts =>
                ts.ConfigureMember<int>(nameof(Leaf.Value), ms => ms.OverrideName("configuredValue")));
            s.ConfigureType<LeafHolder>(ts =>
                ts.ConfigureMember<Leaf>(nameof(LeafHolder.Item), ms => ms.SetProposedTypeHandling(false)));
        });

        Assert.True(deserializer.TryDeserialize("{\"Item\":{\"configuredValue\":8}}", out LeafHolder value));
        Assert.Equal(8, value.Item.Value);
    }

    [Fact]
    public void MappingNestedSettingsMergeOntoMappedTargetSettings()
    {
        var deserializer = CreateDeserializer(s =>
        {
            s.ConfigureType<Item>(ts =>
                ts.ConfigureMember<int>(nameof(Item.Value), ms => ms.OverrideName("configuredValue")));
            s.ConfigureType<IItem>(ts => ts.SetInstanceTypeMapping<Item>(mapped =>
                mapped.ConfigureMember<int>(nameof(Item.Other), ms => ms.OverrideName("mappedOther"))));
        });

        Assert.True(deserializer.TryDeserialize(
            "{\"configuredValue\":9,\"mappedOther\":10}", out IItem mapped));
        var item = Assert.IsType<Item>(mapped);
        Assert.Equal(9, item.Value);
        Assert.Equal(10, item.Other);
    }

    [Fact]
    public void CompiledOpenGenericMergeIsIsolatedFromLaterSourceMutation()
    {
        var settings = new JsonDeserializer.Settings();
        settings.ConfigureGenericType(typeof(Box<>), ts =>
            ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.OverrideName("oldName")));
        settings.ConfigureType<Box<int>>(ts => ts.SetProposedTypeHandling(false));
        var original = new JsonDeserializer(settings);

        settings.ConfigureGenericType(typeof(Box<>), ts =>
            ts.ConfigureMember<string>(nameof(Box<int>.Label), ms => ms.OverrideName("newName")));
        var changed = new JsonDeserializer(settings);

        Assert.True(original.TryDeserialize("{\"oldName\":\"old\"}", out Box<int> originalValue));
        Assert.True(changed.TryDeserialize("{\"newName\":\"new\"}", out Box<int> changedValue));
        Assert.Equal("old", originalValue.Label);
        Assert.Equal("new", changedValue.Label);
    }
}
