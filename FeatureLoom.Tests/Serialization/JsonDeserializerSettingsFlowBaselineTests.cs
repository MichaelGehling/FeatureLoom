using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

/// <summary>
/// Captures the public settings-flow behavior that must remain stable while type, element,
/// recursive and custom-reader configuration are brought to serializer parity.
/// </summary>
public class JsonDeserializerSettingsFlowBaselineTests
{
    public class Payload
    {
        public int First;
        public int Second;
        public string Text;
    }

    public class Leaf
    {
        public int Value;
    }

    public class LeafHolder
    {
        public Leaf Custom;
        public Leaf Plain;
    }

    public interface IItem
    {
    }

    public class ItemA : IItem
    {
        public int A;
    }

    public class ItemB : IItem
    {
        public int B;
    }

    public class MappingHolder
    {
        public IItem First;
        public IItem Second;
    }

    public class RecursiveNode
    {
        public string Name;
        public RecursiveNode Next;
    }

    public class GenericBox<T>
    {
        public T Value;
        public string Label;
    }

    public class PreparedValue
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
        configure?.Invoke(settings);
        return new JsonDeserializer(settings);
    }

    [Fact]
    public void MemberSettings_CombineRenameIgnoreReorderedUnknownAndMissingFields()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Payload>(ts =>
        {
            ts.ConfigureMember<int>(nameof(Payload.First), ms => ms.OverrideName("renamed"));
            ts.ConfigureMember<int>(nameof(Payload.Second), ms => ms.SetIgnore());
        }));

        Assert.True(deserializer.TryDeserialize(
            "{\"unknown\":7,\"Text\":\"ok\",\"Second\":99,\"renamed\":3}", out Payload value));

        Assert.Equal(3, value.First);
        Assert.Equal(0, value.Second);
        Assert.Equal("ok", value.Text);
    }

    [Fact]
    public void MemberLocalCustomReader_DoesNotLeakToSameTypeInSiblingMemberOrRoot()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<LeafHolder>(ts =>
            ts.ConfigureMember<Leaf>(nameof(LeafHolder.Custom), ms => ms.SetCustomTypeReader(api =>
            {
                api.SkipNextValue();
                return new Leaf { Value = 99 };
            }))));

        Assert.True(deserializer.TryDeserialize(
            "{\"Custom\":{\"Value\":1},\"Plain\":{\"Value\":2}}", out LeafHolder holder));
        Assert.Equal(99, holder.Custom.Value);
        Assert.Equal(2, holder.Plain.Value);

        Assert.True(deserializer.TryDeserialize("{\"Value\":3}", out Leaf root));
        Assert.Equal(3, root.Value);
    }

    [Fact]
    public void MemberLocalMappings_CanSelectDifferentImplementationsForSiblingMembers()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<MappingHolder>(ts =>
        {
            ts.ConfigureMember<IItem>(nameof(MappingHolder.First), ms => ms.SetInstanceTypeMapping<ItemA>());
            ts.ConfigureMember<IItem>(nameof(MappingHolder.Second), ms => ms.SetInstanceTypeMapping<ItemB>());
        }));

        Assert.True(deserializer.TryDeserialize(
            "{\"First\":{\"A\":1},\"Second\":{\"B\":2}}", out MappingHolder holder));

        Assert.Equal(1, Assert.IsType<ItemA>(holder.First).A);
        Assert.Equal(2, Assert.IsType<ItemB>(holder.Second).B);
    }

    [Fact]
    public void MappingNestedSettings_ApplyOnlyToMappedTargetContext()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<IItem>(ts =>
            ts.SetInstanceTypeMapping<ItemA>(mapped =>
                mapped.ConfigureMember<int>(nameof(ItemA.A), ms => ms.OverrideName("mappedA")))));

        Assert.True(deserializer.TryDeserialize("{\"mappedA\":4}", out IItem mapped));
        Assert.Equal(4, Assert.IsType<ItemA>(mapped).A);

        Assert.True(deserializer.TryDeserialize("{\"A\":5}", out ItemA direct));
        Assert.Equal(5, direct.A);
    }

    [Fact]
    public void PreparedCustomReader_IsPreparedOnceAndUsedForRepeatedReads()
    {
        int preparationCount = 0;
        int readCount = 0;
        var deserializer = CreateDeserializer(s => s.ConfigureType<PreparedValue>(ts =>
            ts.SetCustomTypeReader(preparation =>
            {
                preparationCount++;
                return (api, item) =>
                {
                    api.SkipNextValue();
                    item.Value = ++readCount;
                    return item;
                };
            })));

        Assert.True(deserializer.TryDeserialize("{}", out PreparedValue first));
        Assert.True(deserializer.TryDeserialize("{}", out PreparedValue second));

        Assert.Equal(1, preparationCount);
        Assert.Equal(1, first.Value);
        Assert.Equal(2, second.Value);
    }

    [Fact]
    public void CompiledSettings_AreIsolatedFromLaterMemberConfigurationMutation()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore
        };
        settings.ConfigureType<Payload>(ts =>
            ts.ConfigureMember<int>(nameof(Payload.First), ms => ms.OverrideName("oldName")));
        var original = new JsonDeserializer(settings);

        settings.ConfigureType<Payload>(ts =>
            ts.ConfigureMember<int>(nameof(Payload.First), ms => ms.OverrideName("newName")));
        var changed = new JsonDeserializer(settings);

        Assert.True(original.TryDeserialize("{\"oldName\":1,\"newName\":2}", out Payload originalValue));
        Assert.True(changed.TryDeserialize("{\"oldName\":1,\"newName\":2}", out Payload changedValue));
        Assert.Equal(1, originalValue.First);
        Assert.Equal(2, changedValue.First);
    }

    [Fact]
    public void CompiledSettings_AreIsolatedFromLaterTypeConfigurationRemoval()
    {
        var settings = new JsonDeserializer.Settings();
        settings.ConfigureType<Payload>(ts =>
            ts.ConfigureMember<int>(nameof(Payload.First), ms => ms.OverrideName("renamed")));
        var original = new JsonDeserializer(settings);

        settings.ConfigureType<Payload>(null);
        var changed = new JsonDeserializer(settings);

        Assert.True(original.TryDeserialize("{\"renamed\":3}", out Payload originalValue));
        Assert.True(changed.TryDeserialize("{\"renamed\":3}", out Payload changedValue));
        Assert.Equal(3, originalValue.First);
        Assert.Equal(0, changedValue.First);
    }

    [Fact]
    public void OpenGenericMemberSettings_ApplyToMultipleConstructedTypes()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureGenericType(typeof(GenericBox<>), ts =>
            ts.ConfigureMember<string>(nameof(GenericBox<int>.Label), ms => ms.OverrideName("name"))));

        Assert.True(deserializer.TryDeserialize("{\"Value\":1,\"name\":\"int\"}", out GenericBox<int> intBox));
        Assert.True(deserializer.TryDeserialize("{\"Value\":\"x\",\"name\":\"string\"}", out GenericBox<string> stringBox));
        Assert.Equal("int", intBox.Label);
        Assert.Equal("string", stringBox.Label);
    }

    [Fact]
    public void RecursiveMemberSettings_StayFiniteAndLocal()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<RecursiveNode>(ts =>
            ts.ConfigureMember<RecursiveNode>(nameof(RecursiveNode.Next), next =>
                next.ConfigureMember<string>(nameof(RecursiveNode.Name), name => name.OverrideName("nestedName")))));

        const string json = "{\"Name\":\"root\",\"Next\":{\"nestedName\":\"child\",\"Next\":{\"Name\":\"grand\"}}}";
        Assert.True(deserializer.TryDeserialize(json, out RecursiveNode value));

        Assert.Equal("root", value.Name);
        Assert.Equal("child", value.Next.Name);
        Assert.Equal("grand", value.Next.Next.Name);
    }

    [Fact]
    public void PopulateExisting_MemberSettingsApplyWithoutReplacingRoot()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Payload>(ts =>
            ts.ConfigureMember<int>(nameof(Payload.First), ms => ms.OverrideName("renamed"))));
        var target = new Payload { First = 1, Second = 2, Text = "before" };

        Assert.True(deserializer.TryPopulate("{\"renamed\":7,\"Text\":\"after\"}", target));

        Assert.Equal(7, target.First);
        Assert.Equal(2, target.Second);
        Assert.Equal("after", target.Text);
    }
}
