using FeatureLoom.Serialization;
using System;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerUnknownFieldPolicyTests
{
    public class Item
    {
        public int Value;
    }

    public class Holder
    {
        public Item Strict;
        public Item Lenient;
    }

    static JsonDeserializer.Settings CreateSettings() => new()
    {
        referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
        proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
        rethrowExceptions = true,
        logCatchedExceptions = false
    };

    [Fact]
    public void UnknownFieldsAreSkippedGloballyByDefault()
    {
        var deserializer = new JsonDeserializer(CreateSettings());

        Assert.True(deserializer.TryDeserialize("{\"unknown\":1,\"Value\":2}", out Item item));
        Assert.Equal(2, item.Value);
    }

    [Fact]
    public void GlobalUnknownFieldPolicyCanThrow()
    {
        var settings = CreateSettings();
        settings.unknownFieldPolicy = JsonDeserializer.UnknownFieldPolicy.Throw;
        var deserializer = new JsonDeserializer(settings);

        Assert.Throws<Exception>(() => deserializer.TryDeserialize("{\"unknown\":1}", out Item _));
    }

    [Fact]
    public void TypePolicyOverridesGlobalPolicy()
    {
        var settings = CreateSettings();
        settings.unknownFieldPolicy = JsonDeserializer.UnknownFieldPolicy.Throw;
        settings.ConfigureType<Item>(typeSettings => typeSettings.SetUnknownFieldPolicy(JsonDeserializer.UnknownFieldPolicy.Skip));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"unknown\":1,\"Value\":2}", out Item item));
        Assert.Equal(2, item.Value);
    }

    [Fact]
    public void MemberPolicyAppliesOnlyToThatMemberReader()
    {
        var settings = CreateSettings();
        settings.ConfigureType<Holder>(typeSettings =>
            typeSettings.ConfigureMember<Item>(nameof(Holder.Strict), member =>
                member.SetUnknownFieldPolicy(JsonDeserializer.UnknownFieldPolicy.Throw)));
        var deserializer = new JsonDeserializer(settings);

        Assert.Throws<Exception>(() => deserializer.TryDeserialize(
            "{\"Strict\":{\"unknown\":1},\"Lenient\":{\"unknown\":2}}", out Holder _));
        Assert.True(deserializer.TryDeserialize(
            "{\"Strict\":{},\"Lenient\":{\"unknown\":2}}", out Holder holder));
        Assert.NotNull(holder.Lenient);
    }

    [Fact]
    public void RecursivePolicyAppliesToDescendants()
    {
        var settings = CreateSettings();
        settings.ConfigureType<Holder>(typeSettings => typeSettings.ConfigureRecursively(recursive =>
            recursive.SetUnknownFieldPolicy(JsonDeserializer.UnknownFieldPolicy.Throw)));
        var deserializer = new JsonDeserializer(settings);

        Assert.Throws<Exception>(() => deserializer.TryDeserialize(
            "{\"Strict\":{\"unknown\":1}}", out Holder _));
    }

    [Fact]
    public void BuilderInheritsTypePolicyAndCanOverrideIt()
    {
        var settings = CreateSettings();
        settings.ConfigureType<Item>(typeSettings =>
        {
            typeSettings.SetUnknownFieldPolicy(JsonDeserializer.UnknownFieldPolicy.Throw);
            typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
                preparation.PrepareObjectReader<Item>(builder => builder
                    .AddExistingFields()
                    .SetUnknownFieldPolicy(JsonDeserializer.UnknownFieldPolicy.Skip)));
        });
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"unknown\":1,\"Value\":2}", out Item item));
        Assert.Equal(2, item.Value);
    }
}
