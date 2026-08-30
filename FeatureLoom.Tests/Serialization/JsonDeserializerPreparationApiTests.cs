using FeatureLoom.Serialization;
using System;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerPreparationApiTests
{
    public class Leaf
    {
        public int Value;
    }

    public class Wrapper
    {
        public Leaf Value;
    }

    public class ConstructorItem
    {
        public int Value;
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
    public void PrepareTypeReaderConfigureCallbackUsesLocalSettingsWithoutLeaking()
    {
        var deserializer = CreateDeserializer(settings =>
        {
            settings.ConfigureType<Leaf>(typeSettings =>
                typeSettings.ConfigureMember<int>(nameof(Leaf.Value), member => member.OverrideName("global")));
            settings.ConfigureType<Wrapper>(typeSettings => typeSettings.SetCustomTypeReader(preparation =>
            {
                var readLeaf = preparation.PrepareTypeReader<Leaf>(leafSettings =>
                    leafSettings.ConfigureMember<int>(nameof(Leaf.Value), member => member.OverrideName("local")));
                return (api, wrapper) =>
                {
                    wrapper.Value = readLeaf(wrapper.Value);
                    return wrapper;
                };
            }));
        });

        Assert.True(deserializer.TryDeserialize("{\"local\":3}", out Wrapper wrapper));
        Assert.Equal(3, wrapper.Value.Value);
        Assert.True(deserializer.TryDeserialize("{\"global\":4}", out Leaf leaf));
        Assert.Equal(4, leaf.Value);
    }

    [Fact]
    public void PrepareNonCustomTypeReaderBypassesCustomReaderButKeepsConfiguredSettings()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureType<Leaf>(typeSettings =>
        {
            typeSettings.ConfigureMember<int>(nameof(Leaf.Value), member => member.OverrideName("renamed"));
            typeSettings.SetCustomTypeReader(preparation =>
            {
                var readDefault = preparation.PrepareNonCustomTypeReader<Leaf>();
                return (api, leaf) => readDefault(leaf);
            });
        }));

        Assert.True(deserializer.TryDeserialize("{\"renamed\":5}", out Leaf leaf));
        Assert.Equal(5, leaf.Value);
    }

    [Fact]
    public void GetConstructorReturnsConfiguredConstructor()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureType<ConstructorItem>(typeSettings =>
        {
            typeSettings.AddConstructor(() => new ConstructorItem { Value = 7 });
            typeSettings.SetCustomTypeReader(preparation =>
            {
                Func<ConstructorItem> constructor = preparation.GetConstructor<ConstructorItem>();
                return (api, item) =>
                {
                    api.SkipNextValue();
                    return constructor();
                };
            });
        }));

        Assert.True(deserializer.TryDeserialize("null", out ConstructorItem item));
        Assert.Equal(7, item.Value);
    }
}
