using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerDeclarativeValueArrayReaderTests
{
    public readonly struct WrappedInt
    {
        public readonly int Value;
        public WrappedInt(int value) => Value = value;
    }

    public class NumberCollection : List<int>
    {
        public NumberCollection(IEnumerable<int> values) : base(values) { }
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
    public void PrepareValueReaderAdaptsRawValueReader()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureType<WrappedInt>(typeSettings =>
            typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
                preparation.PrepareValueReader<WrappedInt>(api =>
                {
                    Assert.True(api.TryReadSignedIntegerValue(out long value));
                    return new WrappedInt((int)value + 1);
                }))));

        Assert.True(deserializer.TryDeserialize("4", out WrappedInt value));
        Assert.Equal(5, value.Value);
    }

    [Fact]
    public void PrepareArrayReaderUsesPreparedElementReaderAndConstructor()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureType<NumberCollection>(typeSettings =>
            typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
                preparation.PrepareArrayReader<NumberCollection, int>(values => new NumberCollection(values)))));

        Assert.True(deserializer.TryDeserialize("[1,2,3]", out NumberCollection value));
        Assert.Equal(new[] { 1, 2, 3 }, value);
    }

    [Fact]
    public void PrepareArrayReaderSupportsLocalElementSettingsWithoutLeaking()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureType<NumberCollection>(typeSettings =>
            typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
                preparation.PrepareArrayReader<NumberCollection, int>(
                    values => new NumberCollection(values),
                    element => element.SetCustomTypeReader(api =>
                    {
                        Assert.True(api.TryReadSignedIntegerValue(out long value));
                        return (int)value + 10;
                    })))));

        Assert.True(deserializer.TryDeserialize("[1,2]", out NumberCollection value));
        Assert.Equal(new[] { 11, 12 }, value);
        Assert.True(deserializer.TryDeserialize("3", out int plain));
        Assert.Equal(3, plain);
    }

    [Fact]
    public void PrepareArrayReaderHandlesNull()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureType<NumberCollection>(typeSettings =>
            typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
                preparation.PrepareArrayReader<NumberCollection, int>(values => new NumberCollection(values)))));

        Assert.True(deserializer.TryDeserialize("null", out NumberCollection value));
        Assert.Null(value);
    }

    [Fact]
    public void PrepareArrayReaderHandlesEmptyArray()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureType<NumberCollection>(typeSettings =>
            typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
                preparation.PrepareArrayReader<NumberCollection, int>(values => new NumberCollection(values)))));

        Assert.True(deserializer.TryDeserialize("[]", out NumberCollection value));
        Assert.Empty(value);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[1,,2]")]
    [InlineData("[1")]
    public void PrepareArrayReaderRejectsMalformedOrWrongShape(string json)
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureType<NumberCollection>(typeSettings =>
            typeSettings.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
                preparation.PrepareArrayReader<NumberCollection, int>(values => new NumberCollection(values)))));

        Assert.ThrowsAny<Exception>(() => deserializer.TryDeserialize(json, out NumberCollection _));
    }
}
