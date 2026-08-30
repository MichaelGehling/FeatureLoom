using FeatureLoom.Serialization;
using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerCustomTypeReaderBuilderTests
{
    public class Model
    {
        public int Existing;
        public string Added;
        public Dictionary<string, int> Dynamic = new();
    }

    public class Nested
    {
        public int Value;
    }

    public class CompositeModel
    {
        public Nested Nested;
        public List<int> Values;
        public string Optional = "initial";
    }

    public class ProposedModel
    {
        public Nested Nested;
    }

    public class ProposedDerivedModel : ProposedModel
    {
        private int hidden;
        public int GetHidden() => hidden;
    }

    static JsonDeserializer CreateDeserializer(Action<JsonDeserializer.TypeSettings<Model>> build)
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<Model>(build);
        return new JsonDeserializer(settings);
    }

    [Fact]
    public void AddedFieldsAreReadIndependentOfJsonOrder()
    {
        var deserializer = CreateDeserializer(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareObjectReader<Model>(builder => builder
                .AddField<int>("number", (item, value) => { item.Existing = value; return item; })
                .AddField<string>("text", (item, value) => { item.Added = value; return item; }))));

        Assert.True(deserializer.TryDeserialize("{\"text\":\"x\",\"number\":3}", out Model value));
        Assert.Equal(3, value.Existing);
        Assert.Equal("x", value.Added);
    }

    [Fact]
    public void FieldLocalSettingsAreAppliedWithoutLeaking()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<Model>(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareObjectReader<Model>(builder => builder.AddField<int>("number",
                (item, value) => { item.Existing = value; return item; },
                local => local.SetCustomTypeReader(api => { api.SkipNextValue(); return 7; })))));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"number\":1}", out Model value));
        Assert.Equal(7, value.Existing);
        Assert.True(deserializer.TryDeserialize("2", out int plain));
        Assert.Equal(2, plain);
    }

    [Fact]
    public void ExistingFieldsCanBeCombinedWithAddedFields()
    {
        var deserializer = CreateDeserializer(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareObjectReader<Model>(builder => builder
                .AddExistingFields()
                .AddField<string>("custom", (item, value) => { item.Added = value; return item; }))));

        Assert.True(deserializer.TryDeserialize("{\"Existing\":4,\"custom\":\"x\"}", out Model value));
        Assert.Equal(4, value.Existing);
        Assert.Equal("x", value.Added);
    }

    [Fact]
    public void UnknownFieldsAreSkippedByDefault()
    {
        var deserializer = CreateDeserializer(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareObjectReader<Model>(builder => builder.AddExistingFields())));

        Assert.True(deserializer.TryDeserialize("{\"unknown\":[1,2],\"Existing\":5}", out Model value));
        Assert.Equal(5, value.Existing);
    }

    [Fact]
    public void UnknownFieldsCanThrow()
    {
        var deserializer = CreateDeserializer(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareObjectReader<Model>(builder => builder.SetUnknownFieldPolicy(
                JsonDeserializer.UnknownFieldPolicy.Throw))));

        Assert.Throws<Exception>(() => deserializer.TryDeserialize("{\"unknown\":1}", out Model _));
    }

    [Fact]
    public void DynamicFieldsReceiveNameAndCanReadValue()
    {
        var deserializer = CreateDeserializer(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
        {
            var readInt = preparation.PrepareTypeReader<int>();
            return preparation.PrepareObjectReader<Model>(builder => builder.AddDynamicFields((name, item) =>
            {
                item.Dynamic[name.AsString()] = readInt(default);
                return item;
            }));
        }));

        Assert.True(deserializer.TryDeserialize("{\"a\":1,\"b\":2}", out Model value));
        Assert.Equal(1, value.Dynamic["a"]);
        Assert.Equal(2, value.Dynamic["b"]);
    }

    [Fact]
    public void DuplicateFieldsFollowInputOrderAndLastValueWins()
    {
        var deserializer = CreateDeserializer(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareObjectReader<Model>(builder => builder.AddExistingFields())));

        Assert.True(deserializer.TryDeserialize("{\"Existing\":1,\"Existing\":2}", out Model value));
        Assert.Equal(2, value.Existing);
    }

    [Fact]
    public void ObjectReaderPopulatesExistingInstance()
    {
        var deserializer = CreateDeserializer(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareObjectReader<Model>(builder => builder.AddExistingFields())));
        var value = new Model { Existing = 1 };

        Assert.True(deserializer.TryPopulate("{\"Existing\":6}", value));
        Assert.Equal(6, value.Existing);
    }

    [Fact]
    public void ObjectReaderHandlesNestedObjectsArraysNullAndMissingFields()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<CompositeModel>(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareObjectReader<CompositeModel>(builder => builder
                .AddField<Nested>("nested", (item, value) => { item.Nested = value; return item; })
                .AddField<List<int>>("values", (item, value) => { item.Values = value; return item; })
                .AddField<string>("optional", (item, value) => { item.Optional = value; return item; }))));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"values\":[1,2],\"nested\":{\"Value\":3},\"optional\":null}", out CompositeModel complete));
        Assert.Equal(3, complete.Nested.Value);
        Assert.Equal(new[] { 1, 2 }, complete.Values);
        Assert.Null(complete.Optional);

        Assert.True(deserializer.TryDeserialize("{\"nested\":{\"Value\":4}}", out CompositeModel missing));
        Assert.Equal("initial", missing.Optional);
        Assert.Null(missing.Values);
    }

    [Fact]
    public void FieldLocalRecursiveSettingsApplyToProposedTypeWithoutLeaking()
    {
        string typeName = TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(ProposedDerivedModel));
        var settings = new JsonDeserializer.Settings
        {
            dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties,
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<Model>(typeSettings => typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareObjectReader<Model>(builder => builder.AddField<ProposedModel>("item",
                (item, value) => { item.Added = $"{value.Nested.Value}:{((ProposedDerivedModel)value).GetHidden()}"; return item; },
                local => local.ConfigureRecursively(recursive =>
                    recursive.SetDataAccess(JsonDeserializer.DataAccess.PublicAndPrivateFields))))));
        var deserializer = new JsonDeserializer(settings);

        string json = $"{{\"item\":{{\"$type\":\"{typeName}\",\"Nested\":{{\"Value\":5}},\"hidden\":6}}}}";
        Assert.True(deserializer.TryDeserialize(json, out Model value));
        Assert.Equal("5:6", value.Added);

        string rootJson = $"{{\"$type\":\"{typeName}\",\"Nested\":{{\"Value\":7}},\"hidden\":8}}";
        Assert.True(deserializer.TryDeserialize(rootJson, out ProposedModel plain));
        Assert.Equal(0, Assert.IsType<ProposedDerivedModel>(plain).GetHidden());
    }

}
