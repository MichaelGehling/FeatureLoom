using FeatureLoom.Serialization;
using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerElementSettingsTests
{
    public class Value
    {
        public int Number;
    }

    public class Container
    {
        public List<Value> Configured;
        public List<Value> Plain;
    }

    public class EnumerableWrapper : IEnumerable<Value>
    {
        readonly List<Value> values;

        public EnumerableWrapper(IEnumerable<Value> values) => this.values = values.ToList();

        public IEnumerator<Value> GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class BaseValue
    {
        public int Number;
    }

    public class DerivedValue : BaseValue
    {
        private int hidden;
        public int GetHidden() => hidden;
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

    static void ConfigureRenamedValue(JsonDeserializer.TypeSettings<Value> settings) =>
        settings.ConfigureMember<int>(nameof(Value.Number), member => member.OverrideName("n"));

    [Fact]
    public void ArrayElementSettingsApplyToEveryElement()
    {
        var deserializer = CreateDeserializer(s =>
            s.ConfigureType<Value[]>(ts => ts.ConfigureElement<Value>(ConfigureRenamedValue)));

        Assert.True(deserializer.TryDeserialize("[{\"n\":1},{\"n\":2}]", out Value[] values));
        Assert.Equal(1, values[0].Number);
        Assert.Equal(2, values[1].Number);
    }

    [Fact]
    public void ListElementSettingsApplyToEveryElement()
    {
        var deserializer = CreateDeserializer(s =>
            s.ConfigureType<List<Value>>(ts => ts.ConfigureElement<Value>(ConfigureRenamedValue)));

        Assert.True(deserializer.TryDeserialize("[{\"n\":3}]", out List<Value> values));
        Assert.Equal(3, values[0].Number);
    }

    [Fact]
    public void EnumerableConstructorElementSettingsApplyToEveryElement()
    {
        var deserializer = CreateDeserializer(s =>
            s.ConfigureType<EnumerableWrapper>(ts => ts.ConfigureElement<Value>(ConfigureRenamedValue)));

        Assert.True(deserializer.TryDeserialize("[{\"n\":3},{\"n\":4}]", out EnumerableWrapper values));
        Assert.Equal(new[] { 3, 4 }, values.Select(value => value.Number));
    }

    [Fact]
    public void NumericElementCustomReaderDisablesBulkFastPath()
    {
        var deserializer = CreateDeserializer(s =>
            s.ConfigureType<int[]>(ts => ts.ConfigureElement<int>(element =>
                element.SetCustomTypeReader(api =>
                {
                    Assert.True(api.TryReadSignedIntegerValue(out long value));
                    return (int)value + 10;
                }))));

        Assert.True(deserializer.TryDeserialize("[1,2]", out int[] values));
        Assert.Equal(new[] { 11, 12 }, values);
    }

    [Fact]
    public void DictionaryElementSettingsApplyToObjectValues()
    {
        var deserializer = CreateDeserializer(s =>
            s.ConfigureType<Dictionary<string, Value>>(ts => ts.ConfigureElement<Value>(ConfigureRenamedValue)));

        Assert.True(deserializer.TryDeserialize("{\"a\":{\"n\":4}}", out Dictionary<string, Value> values));
        Assert.Equal(4, values["a"].Number);
    }

    [Fact]
    public void DictionaryElementSettingsApplyToPairArrayValues()
    {
        var deserializer = CreateDeserializer(s =>
            s.ConfigureType<Dictionary<string, Value>>(ts => ts.ConfigureElement<Value>(ConfigureRenamedValue)));

        const string json = "[{\"Key\":\"a\",\"Value\":{\"n\":5}}]";
        Assert.True(deserializer.TryDeserialize(json, out Dictionary<string, Value> values));
        Assert.Equal(5, values["a"].Number);
    }

    [Fact]
    public void DictionaryElementSettingsApplyWhenPopulatingObjectValues()
    {
        var deserializer = CreateDeserializer(s =>
            s.ConfigureType<Dictionary<string, Value>>(ts => ts.ConfigureElement<Value>(ConfigureRenamedValue)));
        var values = new Dictionary<string, Value> { ["a"] = new Value { Number = 1 } };

        Assert.True(deserializer.TryPopulate("{\"a\":{\"n\":11}}", values));
        Assert.Equal(11, values["a"].Number);
    }

    [Fact]
    public void DictionaryElementSettingsApplyWhenPopulatingPairArrayValues()
    {
        var deserializer = CreateDeserializer(s =>
            s.ConfigureType<Dictionary<string, Value>>(ts => ts.ConfigureElement<Value>(ConfigureRenamedValue)));
        var values = new Dictionary<string, Value>();

        const string json = "[{\"Key\":\"a\",\"Value\":{\"n\":12}}]";
        Assert.True(deserializer.TryPopulate(json, values));
        Assert.Equal(12, values["a"].Number);
    }

    [Fact]
    public void ElementSettingsApplyToProposedRuntimeType()
    {
        string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(DerivedValue));
        var deserializer = CreateDeserializer(s =>
        {
            s.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways;
            s.ConfigureType<List<BaseValue>>(ts => ts.ConfigureElement<BaseValue>(element =>
                element.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields)));
        });

        string json = $"[{{\"$type\":\"{typeName}\",\"Number\":14,\"hidden\":15}}]";
        Assert.True(deserializer.TryDeserialize(json, out List<BaseValue> values));
        var value = Assert.IsType<DerivedValue>(values[0]);
        Assert.Equal(14, value.Number);
        Assert.Equal(15, value.GetHidden());
    }

    [Fact]
    public void MemberLocalElementSettingsDoNotLeakToSiblingContainer()
    {
        var deserializer = CreateDeserializer(s => s.ConfigureType<Container>(ts =>
            ts.ConfigureMember<List<Value>>(nameof(Container.Configured), member =>
                member.ConfigureElement<Value>(ConfigureRenamedValue))));

        const string json = "{\"Configured\":[{\"n\":6}],\"Plain\":[{\"Number\":7}]}";
        Assert.True(deserializer.TryDeserialize(json, out Container value));
        Assert.Equal(6, value.Configured[0].Number);
        Assert.Equal(7, value.Plain[0].Number);
    }

    [Fact]
    public void OpenGenericElementSettingsApplyOnlyToMatchingElementType()
    {
        var deserializer = CreateDeserializer(s =>
            s.ConfigureGenericType(typeof(List<>), ts => ts.ConfigureElement<Value>(ConfigureRenamedValue)));

        Assert.True(deserializer.TryDeserialize("[{\"n\":8}]", out List<Value> configured));
        Assert.True(deserializer.TryDeserialize("[9]", out List<int> plain));
        Assert.Equal(8, configured[0].Number);
        Assert.Equal(9, plain[0]);
    }

    [Fact]
    public void ClosedContainerElementTypeMismatchThrowsDuringConfiguration()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<Exception>(() =>
            settings.ConfigureType<List<int>>(ts => ts.ConfigureElement<string>(_ => { })));
    }

    [Fact]
    public void NonContainerElementConfigurationThrowsDuringConfiguration()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<Exception>(() =>
            settings.ConfigureType<Value>(ts => ts.ConfigureElement<Value>(_ => { })));
    }

    [Fact]
    public void NullElementConfigurationRemovesSettings()
    {
        var settings = new JsonDeserializer.Settings();
        settings.ConfigureType<List<Value>>(ts => ts.ConfigureElement<Value>(ConfigureRenamedValue));
        settings.ConfigureType<List<Value>>(ts => ts.ConfigureElement<Value>(null));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("[{\"Number\":10}]", out List<Value> values));
        Assert.Equal(10, values[0].Number);
    }
}
