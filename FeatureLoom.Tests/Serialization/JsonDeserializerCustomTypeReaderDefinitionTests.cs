using FeatureLoom.Serialization;
using System;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerCustomTypeReaderDefinitionTests
{
    public class Wrapper<T>
    {
        public T Value;
    }

    public class Pair<T1, T2>
    {
        public T1 First;
        public T2 Second;
    }

    public class DerivedWrapper : Wrapper<int>
    {
        public int Other;
    }

    public class WrapperReader<T> : JsonDeserializer.CustomTypeReaderDefinition<Wrapper<T>>
    {
        public static int PreparationCount;

        protected override JsonDeserializer.ICustomTypeReader<Wrapper<T>> Prepare(JsonDeserializer.PreparationApi api)
        {
            PreparationCount++;
            var readValue = api.PrepareTypeReader<T>();
            return new JsonDeserializer.CustomTypeReader<Wrapper<T>>(readApi => new Wrapper<T> { Value = readValue(default) });
        }
    }

    public class PairReader<T1, T2> : JsonDeserializer.CustomTypeReaderDefinition<Pair<T1, T2>>
    {
        protected override JsonDeserializer.ICustomTypeReader<Pair<T1, T2>> Prepare(JsonDeserializer.PreparationApi api)
        {
            var readFirst = api.PrepareTypeReader<T1>();
            return new JsonDeserializer.CustomTypeReader<Pair<T1, T2>>(_ =>
                new Pair<T1, T2> { First = readFirst(default) });
        }
    }

    public class ForeignReader<T> : JsonDeserializer.CustomTypeReaderDefinition<Pair<T, T>>
    {
        protected override JsonDeserializer.ICustomTypeReader<Pair<T, T>> Prepare(JsonDeserializer.PreparationApi api) =>
            new JsonDeserializer.CustomTypeReader<Pair<T, T>>(_ => new Pair<T, T>());
    }

    public abstract class AbstractWrapperReader<T> : JsonDeserializer.CustomTypeReaderDefinition<Wrapper<T>>
    {
    }

    public class NoPublicConstructorReader<T> : JsonDeserializer.CustomTypeReaderDefinition<Wrapper<T>>
    {
        private NoPublicConstructorReader() { }

        protected override JsonDeserializer.ICustomTypeReader<Wrapper<T>> Prepare(JsonDeserializer.PreparationApi api) =>
            new JsonDeserializer.CustomTypeReader<Wrapper<T>>(_ => new Wrapper<T>());
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
    public void OpenGenericCustomReaderIsUsedForEveryConstructedType()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureGenericType(typeof(Wrapper<>), typeSettings =>
            typeSettings.SetCustomTypeReader(typeof(WrapperReader<>))));

        Assert.True(deserializer.TryDeserialize("42", out Wrapper<int> intWrapper));
        Assert.True(deserializer.TryDeserialize("\"x\"", out Wrapper<string> stringWrapper));
        Assert.Equal(42, intWrapper.Value);
        Assert.Equal("x", stringWrapper.Value);
    }

    [Fact]
    public void OpenGenericCustomReaderSupportsMultipleTypeParameters()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureGenericType(typeof(Pair<,>), typeSettings =>
            typeSettings.SetCustomTypeReader(typeof(PairReader<,>))));

        Assert.True(deserializer.TryDeserialize("3", out Pair<int, string> pair));
        Assert.Equal(3, pair.First);
        Assert.Null(pair.Second);
    }

    [Fact]
    public void ClosedCustomReaderOverridesOpenGenericDefinition()
    {
        var deserializer = CreateDeserializer(settings =>
        {
            settings.ConfigureGenericType(typeof(Wrapper<>), typeSettings =>
                typeSettings.SetCustomTypeReader(typeof(WrapperReader<>)));
            settings.ConfigureType<Wrapper<int>>(typeSettings =>
                typeSettings.SetCustomTypeReader(api =>
                {
                    api.SkipNextValue();
                    return new Wrapper<int> { Value = 99 };
                }));
        });

        Assert.True(deserializer.TryDeserialize("42", out Wrapper<int> wrapper));
        Assert.Equal(99, wrapper.Value);
    }

    [Fact]
    public void DefinitionIsPreparedOncePerConstructedType()
    {
        WrapperReader<int>.PreparationCount = 0;
        var deserializer = CreateDeserializer(settings => settings.ConfigureGenericType(typeof(Wrapper<>), typeSettings =>
            typeSettings.SetCustomTypeReader(typeof(WrapperReader<>))));

        Assert.True(deserializer.TryDeserialize("1", out Wrapper<int> _));
        Assert.True(deserializer.TryDeserialize("2", out Wrapper<int> _));
        Assert.Equal(1, WrapperReader<int>.PreparationCount);
    }

    [Fact]
    public void OpenGenericCustomReaderDoesNotApplyToDerivedTypes()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureGenericType(typeof(Wrapper<>), typeSettings =>
            typeSettings.SetCustomTypeReader(typeof(WrapperReader<>))));

        Assert.True(deserializer.TryDeserialize("{\"Value\":1,\"Other\":2}", out DerivedWrapper wrapper));
        Assert.Equal(1, wrapper.Value);
        Assert.Equal(2, wrapper.Other);
    }

    [Fact]
    public void MismatchingGenericArityThrowsDuringConfiguration()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureGenericType(typeof(Pair<,>), typeSettings =>
            typeSettings.SetCustomTypeReader(typeof(WrapperReader<>))));
    }

    [Fact]
    public void NonDefinitionTypeThrowsDuringConfiguration()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureGenericType(typeof(Wrapper<>), typeSettings =>
            typeSettings.SetCustomTypeReader(typeof(Wrapper<>))));
    }

    [Fact]
    public void AbstractDefinitionThrowsDuringConfiguration()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureGenericType(typeof(Wrapper<>), typeSettings =>
            typeSettings.SetCustomTypeReader(typeof(AbstractWrapperReader<>))));
    }

    [Fact]
    public void DefinitionWithoutPublicParameterlessConstructorThrowsDuringConfiguration()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureGenericType(typeof(Wrapper<>), typeSettings =>
            typeSettings.SetCustomTypeReader(typeof(NoPublicConstructorReader<>))));
    }

    [Fact]
    public void DefinitionReadingAnotherTypeThrowsWhenReaderIsPrepared()
    {
        var deserializer = CreateDeserializer(settings => settings.ConfigureGenericType(typeof(Wrapper<>), typeSettings =>
            typeSettings.SetCustomTypeReader(typeof(ForeignReader<>))));

        Assert.Throws<ArgumentException>(() => deserializer.TryDeserialize("1", out Wrapper<int> _));
    }
}
