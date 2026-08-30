using FeatureLoom.Serialization;
using System;
using Xunit;

namespace FeatureLoom.Tests.Serialization;

public class JsonDeserializerDiscriminatorMappingTests
{
    public interface IItem { }

    public class ItemA : IItem
    {
        public string Kind;
        public int Common;
        public int A;
    }

    public class ItemB : IItem
    {
        public string Kind;
        public int Common;
        public int B;
    }

    public class ItemC : IItem
    {
        public int C;
    }

    public sealed class ItemId
    {
        public string Value { get; }

        public ItemId(string value)
        {
            Value = value;
        }
    }

    static JsonDeserializer CreateDeserializer(Action<JsonDeserializer.TypeSettings<IItem>> configure)
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<IItem>(configure);
        return new JsonDeserializer(settings);
    }

    [Fact]
    public void MatchingCheckerSelectsOptionImmediately()
    {
        int laterChecks = 0;
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => { laterChecks++; return value == "a"; });
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"a\",\"A\":1}", out IItem value));
        Assert.Equal(1, Assert.IsType<ItemA>(value).A);
        Assert.Equal(0, laterChecks);
    }

    [Fact]
    public void FalseCheckerExcludesOptionFromInference()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB>();
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"b\",\"A\":1,\"B\":2}", out IItem value));
        Assert.Equal(2, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void PredicateExceptionFollowsDeserializerExceptionPolicy()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", _ => throw new InvalidOperationException("checker"));
            type.AddInstanceTypeMappingOption<ItemB>();
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            deserializer.TryDeserialize("{\"Kind\":\"a\",\"B\":2}", out IItem _));
        Assert.Equal("checker", exception.Message);
    }

    [Fact]
    public void IncompatibleCheckerValueExcludesOption()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, int>("Kind", value => value == 1);
            type.AddInstanceTypeMappingOption<ItemB>();
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"wrong\",\"A\":1,\"B\":2}", out IItem value));
        Assert.Equal(2, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void AbsentCheckerFieldLeavesOptionEligibleForInference()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB>();
        });

        Assert.True(deserializer.TryDeserialize("{\"A\":3}", out IItem value));
        Assert.Equal(3, Assert.IsType<ItemA>(value).A);
    }

    [Fact]
    public void FalseCheckerExcludesOptionFromCommonFieldFallback()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB>();
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"b\",\"Common\":3}", out IItem value));
        Assert.Equal(3, Assert.IsType<ItemB>(value).Common);
    }

    [Fact]
    public void UnresolvedCheckerPreventsEarlyInferenceSelection()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA>();
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b");
        });

        Assert.True(deserializer.TryDeserialize("{\"A\":1,\"Kind\":\"b\",\"B\":2}", out IItem value));
        Assert.Equal(2, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void CheckerCanAppearAfterNestedValues()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b");
        });

        Assert.True(deserializer.TryDeserialize("{\"unknown\":{\"nested\":[1,2]},\"Kind\":\"b\",\"B\":4}", out IItem value));
        Assert.Equal(4, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void OptionLocalSettingsApplyAfterCheckerSelection()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA>();
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b", mapped =>
                mapped.ConfigureMember<int>(nameof(ItemB.B), member => member.OverrideName("value")));
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"b\",\"value\":5}", out IItem value));
        Assert.Equal(5, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void InvalidConfigurationIsRejected()
    {
        var settings = new JsonDeserializer.Settings();

        Assert.Throws<ArgumentException>(() => settings.ConfigureType<IItem>(type =>
            type.AddInstanceTypeMappingOption<ItemA, string>(null, _ => true)));
        Assert.Throws<ArgumentNullException>(() => settings.ConfigureType<IItem>(type =>
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", null)));
    }

    [Fact]
    public void WholeValuePredicateSelectsPrimitiveMappingOption()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type =>
        {
            type.AddInstanceTypeMappingValueOption<long, int>(value => value >= int.MinValue && value <= int.MaxValue);
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
        });
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("42", out object value));
        Assert.Equal(42, Assert.IsType<int>(value));
    }

    [Fact]
    public void WholeValueConverterReturnsProducedStringEncodedResult()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type =>
            type.AddInstanceTypeMappingValueOption<string, ItemId>(
                (string input, out ItemId result) =>
                {
                    if (!input.StartsWith("id:"))
                    {
                        result = null;
                        return false;
                    }
                    result = new ItemId(input.Substring(3));
                    return true;
                }));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("\"id:123\"", out object value));
        Assert.Equal("123", Assert.IsType<ItemId>(value).Value);
    }

    [Fact]
    public void FailedWholeValueConverterFallsBackToUnknownValueHandling()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type =>
            type.AddInstanceTypeMappingValueOption<string, ItemId>(
                (string input, out ItemId result) =>
                {
                    result = null;
                    return false;
                }));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("\"plain\"", out object value));
        Assert.Equal("plain", Assert.IsType<string>(value));
    }

    [Theory]
    [InlineData("\"d85b1407-351d-4694-9392-03acc5870eb1\"", typeof(Guid))]
    [InlineData("\"2026-04-01T12:34:56Z\"", typeof(DateTimeOffset))]
    [InlineData("\"2026-04-01T12:34:56\"", typeof(DateTime))]
    [InlineData("\"1.02:03:04.5000000\"", typeof(TimeSpan))]
    public void DefaultStringValueMappingsRecognizeCanonicalValues(string json, Type expectedType)
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type => type.AddDefaultStringValueMappings());
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize(json, out object value));
        Assert.IsType(expectedType, value);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("2026-04-01")]
    [InlineData("https://example.com")]
    public void DefaultStringValueMappingsLeaveAmbiguousValuesAsStrings(string input)
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type => type.AddDefaultStringValueMappings());
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize($"\"{input}\"", out object value));
        Assert.Equal(input, Assert.IsType<string>(value));
    }

    [Fact]
    public void ExplicitValueMappingPrecedesDefaultStringValueMappings()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type =>
        {
            type.AddDefaultStringValueMappings(JsonDeserializer.StringValueMappings.Guid);
            type.AddInstanceTypeMappingValueOption<string, ItemId>(
                (string input, out ItemId result) =>
                {
                    result = new ItemId(input);
                    return true;
                });
        });
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("\"d85b1407-351d-4694-9392-03acc5870eb1\"", out object value));
        Assert.IsType<ItemId>(value);
    }
}
