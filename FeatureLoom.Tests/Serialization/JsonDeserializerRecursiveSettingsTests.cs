using FeatureLoom.Helpers;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerRecursiveSettingsTests
{
    public class Node
    {
        public string Text;
        public Hidden Hidden;
        public Node Next;
        public List<Node> Children;
    }

    public class Hidden
    {
        private int value;
        public int GetValue() => value;
    }

    public class Holder
    {
        public Node Scoped;
        public Node Plain;
    }

    public interface IItem
    {
    }

    public class Item : IItem
    {
        private int value;
        public int GetValue() => value;
    }

    public class MappingHolder
    {
        public IItem Item;
    }

    public class ProposedBase
    {
        public Hidden Hidden;
    }

    public class ProposedDerived : ProposedBase
    {
        public string Extra;
    }

    public class Left
    {
        public Hidden Hidden;
        public Right Right;
    }

    public class Right
    {
        public Hidden Hidden;
        public Left Left;
    }

    static JsonDeserializer CreateDeserializer(Action<JsonDeserializer.Settings> configure)
    {
        var settings = new JsonDeserializer.Settings
        {
            dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties,
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        configure(settings);
        return new JsonDeserializer(settings);
    }

    [Fact]
    public void RecursiveSettingsApplyToDeclaringTypeAndDescendants()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Node>(ts =>
            ts.ConfigureRecursively(rs => rs.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields))));

        Assert.True(deserializer.TryDeserialize("{\"Hidden\":{\"value\":1},\"Next\":{\"Hidden\":{\"value\":2}}}", out Node value));
        Assert.Equal(1, value.Hidden.GetValue());
        Assert.Equal(2, value.Next.Hidden.GetValue());
    }

    [Fact]
    public void LocalTypeSettingOverridesRecursiveSettingWithoutRemovingItBelow()
    {
        var deserializer = CreateDeserializer(s =>
        {
            s.ConfigureType<Node>(ts => ts.ConfigureRecursively(rs =>
                rs.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields)));
            s.ConfigureType<Hidden>(ts => ts.SetDataAccess(JsonDeserializer.DataAccess.PublicFieldsAndProperties));
        });

        Assert.True(deserializer.TryDeserialize("{\"Hidden\":{\"value\":1},\"Next\":{\"Text\":\"x\"}}", out Node value));
        Assert.Equal(0, value.Hidden.GetValue());
        Assert.Equal("x", value.Next.Text);
    }

    [Fact]
    public void MemberRecursiveScopeDoesNotLeakToSiblingMember()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Holder>(ts =>
            ts.ConfigureMember<Node>(nameof(Holder.Scoped), member =>
                member.ConfigureRecursively(rs => rs.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields)))));

        const string json = "{\"Scoped\":{\"Hidden\":{\"value\":3}},\"Plain\":{\"Hidden\":{\"value\":4}}}";
        Assert.True(deserializer.TryDeserialize(json, out Holder value));
        Assert.Equal(3, value.Scoped.Hidden.GetValue());
        Assert.Equal(0, value.Plain.Hidden.GetValue());
    }

    [Fact]
    public void ElementRecursiveScopeAppliesInsideEachElement()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<List<Node>>(ts =>
            ts.ConfigureElement<Node>(element => element.ConfigureRecursively(rs =>
                rs.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields)))));

        Assert.True(deserializer.TryDeserialize("[{\"Hidden\":{\"value\":5}}]", out List<Node> values));
        Assert.Equal(5, values[0].Hidden.GetValue());
    }

    [Fact]
    public void NestedRecursiveSettingsLayerWithInnerValuesWinning()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Node>(ts =>
        {
            ts.ConfigureRecursively(rs =>
            {
                rs.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields);
                rs.SetUseStringCache(false);
            });
            ts.ConfigureMember<Node>(nameof(Node.Next), member =>
                member.ConfigureRecursively(rs => rs.SetUseStringCache(true)));
        }));

        const string json = "{\"Text\":\"same\",\"Next\":{\"Text\":\"same\",\"Hidden\":{\"value\":6}}}";
        Assert.True(deserializer.TryDeserialize(json, out Node value));
        Assert.NotSame(value.Text, string.Copy(value.Text));
        Assert.Equal(6, value.Next.Hidden.GetValue());
    }

    [Fact]
    public void RecursiveSettingsTerminateForSelfReferencingType()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Node>(ts =>
            ts.ConfigureRecursively(rs => rs.SetProposedTypeHandling(false))));

        const string json = "{\"Text\":\"a\",\"Next\":{\"Text\":\"b\",\"Next\":{\"Text\":\"c\"}}}";
        Assert.True(deserializer.TryDeserialize(json, out Node value));
        Assert.Equal("c", value.Next.Next.Text);
    }

    [Fact]
    public void RecursiveSettingsTerminateForMutuallyRecursiveTypes()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Left>(ts =>
            ts.ConfigureRecursively(rs => rs.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields))));

        const string json = "{\"Hidden\":{\"value\":1},\"Right\":{\"Hidden\":{\"value\":2},\"Left\":{\"Hidden\":{\"value\":3}}}}";
        Assert.True(deserializer.TryDeserialize(json, out Left value));
        Assert.Equal(1, value.Hidden.GetValue());
        Assert.Equal(2, value.Right.Hidden.GetValue());
        Assert.Equal(3, value.Right.Left.Hidden.GetValue());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContextualReaderIsIsolatedRegardlessOfPreparationOrder(bool preparePlainFirst)
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Holder>(ts =>
            ts.ConfigureMember<Node>(nameof(Holder.Scoped), member =>
                member.ConfigureRecursively(rs => rs.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields)))));

        if (preparePlainFirst)
        {
            Assert.True(deserializer.TryDeserialize("{\"Hidden\":{\"value\":9}}", out Node plain));
            Assert.Equal(0, plain.Hidden.GetValue());
        }

        const string json = "{\"Scoped\":{\"Hidden\":{\"value\":10}},\"Plain\":{\"Hidden\":{\"value\":11}}}";
        Assert.True(deserializer.TryDeserialize(json, out Holder holder));
        Assert.Equal(10, holder.Scoped.Hidden.GetValue());
        Assert.Equal(0, holder.Plain.Hidden.GetValue());

        if (!preparePlainFirst)
        {
            Assert.True(deserializer.TryDeserialize("{\"Hidden\":{\"value\":12}}", out Node plain));
            Assert.Equal(0, plain.Hidden.GetValue());
        }
    }

    [Fact]
    public void RecursiveSettingsFlowThroughMappedTypeButMappingItselfDoesNotPropagate()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<MappingHolder>(ts =>
        {
            ts.ConfigureRecursively(rs => rs.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields));
            ts.ConfigureMember<IItem>(nameof(MappingHolder.Item), member => member.SetInstanceTypeMapping<Item>());
        }));

        Assert.True(deserializer.TryDeserialize("{\"Item\":{\"value\":7}}", out MappingHolder value));
        Assert.Equal(7, Assert.IsType<Item>(value.Item).GetValue());
    }

    [Fact]
    public void RecursiveSettingsApplyToProposedRuntimeType()
    {
        string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ProposedDerived));
        var deserializer = CreateDeserializer(s =>
        {
            s.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways;
            s.ConfigureType<ProposedBase>(ts => ts.ConfigureRecursively(rs =>
                rs.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields)));
        });

        string json = $"{{\"$type\":\"{typeName}\",\"Hidden\":{{\"value\":8}},\"Extra\":\"x\"}}";
        Assert.True(deserializer.TryDeserialize(json, out ProposedBase value));
        Assert.Equal(8, Assert.IsType<ProposedDerived>(value).Hidden.GetValue());
    }
}
