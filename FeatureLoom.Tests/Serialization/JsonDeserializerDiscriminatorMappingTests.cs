using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
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

    public class ItemContainer
    {
        public IItem Item;
        public IItem Other;
        public List<IItem> Items;
    }

    public sealed class ItemId
    {
        public string Value { get; }

        public ItemId(string value)
        {
            Value = value;
        }
    }

    public sealed class ItemWithConstructor : IItem
    {
        public int Seed;
        public int A;

        public ItemWithConstructor(int seed)
        {
            Seed = seed;
        }
    }

    public enum ItemKind
    {
        A,
        B
    }

    public sealed class CustomKind
    {
        public string Value { get; }

        public CustomKind(string value)
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

    [Theory]
    [InlineData("{\"Kind\":\"a\",\"A\":1}")]
    [InlineData("{\"A\":1,\"Kind\":\"a\"}")]
    [InlineData("{\"Common\":0,\"Kind\":\"a\",\"A\":1}")]
    public void CheckerSupportsAnyFieldPosition(string json)
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemB>();
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");            
        });

        Assert.True(deserializer.TryDeserialize(json, out IItem value));
        Assert.Equal(1, Assert.IsType<ItemA>(value).A);
    }

    [Fact]
    public void SameFieldPredicatesUseRegistrationOrder()
    {
        int secondChecks = 0;
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", _ => true);
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", _ => { secondChecks++; return true; });
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"anything\",\"A\":1}", out IItem value));
        Assert.IsType<ItemA>(value);
        Assert.Equal(0, secondChecks);
    }

    [Fact]
    public void CompatibleFieldCheckersShareOneIdentificationValueRead()
    {
        int valueReads = 0;
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<CustomKind>(type => type.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareValueReader(api =>
            {
                valueReads++;
                if (!api.TryReadStringValueOrNull(out string text)) throw new Exception("Expected string");
                return new CustomKind(text);
            })));
        settings.ConfigureType<IItem>(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, CustomKind>("Kind", value => value.Value == "a");
            type.AddInstanceTypeMappingOption<ItemB, CustomKind>("Kind", value => value.Value == "b");
            type.AddInstanceTypeMappingOption<ItemC, CustomKind>("Kind", value => value.Value == "c");
        });
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"c\",\"C\":3}", out IItem value));
        Assert.IsType<ItemC>(value);
        Assert.Equal(1, valueReads);
    }

    [Fact]
    public void SameFieldSupportsDifferentCheckerTypes()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, int>("Kind", value => value == 1);
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b");
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"b\",\"B\":2}", out IItem value));
        Assert.Equal(2, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void EnumGuidNumericAndCustomReaderCheckerValuesAreSupported()
    {
        int customReaderPreparations = 0;
        var expectedGuid = new Guid("d85b1407-351d-4694-9392-03acc5870eb1");
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<CustomKind>(type => type.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
        {
            customReaderPreparations++;
            return preparation.PrepareValueReader(api =>
            {
                if (!api.TryReadStringValueOrNull(out string text)) throw new Exception("Expected string");
                return new CustomKind(text);
            });
        }));
        settings.ConfigureType<IItem>(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, ItemKind>("EnumKind", value => value == ItemKind.A);
            type.AddInstanceTypeMappingOption<ItemA, Guid>("GuidKind", value => value == expectedGuid);
            type.AddInstanceTypeMappingOption<ItemA, int>("NumberKind", value => value == 7);
            type.AddInstanceTypeMappingOption<ItemB, CustomKind>("CustomKind", value => value.Value == "b");
        });
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"CustomKind\":\"b\",\"B\":4}", out IItem value));
        Assert.Equal(4, Assert.IsType<ItemB>(value).B);
        Assert.True(deserializer.TryDeserialize("{\"CustomKind\":\"b\",\"B\":5}", out value));
        Assert.Equal(5, Assert.IsType<ItemB>(value).B);
        Assert.Equal(1, customReaderPreparations);
    }

    [Fact]
    public void DuplicateFalseCheckerFieldCannotRestoreExcludedOption()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB>();
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"b\",\"Kind\":\"a\",\"A\":1,\"B\":2}", out IItem value));
        Assert.Equal(2, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void NullCheckerValueExcludesOnlyThatOption()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, int>("Kind", value => value == 1);
            type.AddInstanceTypeMappingOption<ItemB>();
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":null,\"B\":2}", out IItem value));
        Assert.Equal(2, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void WholeValueConverterSupportsArrays()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type =>
            type.AddInstanceTypeMappingValueOption<int[], ItemId>(
                (int[] input, out ItemId result) =>
                {
                    result = input.Length == 2 ? new ItemId($"{input[0]}:{input[1]}") : null;
                    return result != null;
                }));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("[1,2]", out object value));
        Assert.Equal("1:2", Assert.IsType<ItemId>(value).Value);
    }

    [Fact]
    public void CompatibleWholeValueOptionsShareOneIdentificationValueRead()
    {
        int valueReads = 0;
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<CustomKind>(type => type.SetCustomTypeReader((JsonDeserializer.PreparationApi preparation) =>
            preparation.PrepareValueReader(api =>
            {
                valueReads++;
                if (!api.TryReadStringValueOrNull(out string text)) throw new Exception("Expected string");
                return new CustomKind(text);
            })));
        settings.ConfigureType<object>(type =>
        {
            type.AddInstanceTypeMappingValueOption<CustomKind, ItemId>((CustomKind value, out ItemId result) =>
            {
                result = null;
                return false;
            });
            type.AddInstanceTypeMappingValueOption<CustomKind, CustomKind>((CustomKind value, out CustomKind result) =>
            {
                result = value;
                return true;
            });
        });
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("\"kind\"", out object value));
        Assert.IsType<CustomKind>(value);
        Assert.Equal(1, valueReads);
    }

    [Fact]
    public void WholeValuePredicateAppliesOptionLocalSettings()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type =>
            type.AddInstanceTypeMappingValueOption<Dictionary<string, object>, ItemB>(
                value => value.ContainsKey("kind"),
                mapped => mapped.ConfigureMember<int>(nameof(ItemB.B), member => member.OverrideName("value"))));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"kind\":\"b\",\"value\":5}", out object value));
        Assert.Equal(5, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void ConverterExceptionFollowsNonRethrowPolicy()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = false,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type =>
            type.AddInstanceTypeMappingValueOption<string, ItemId>(
                (string input, out ItemId result) => throw new InvalidOperationException("converter")));
        var deserializer = new JsonDeserializer(settings);

        Assert.False(deserializer.TryDeserialize("\"id:1\"", out object _));
    }

    [Fact]
    public void CompiledSettingsAreIsolatedFromLaterMappingChanges()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        JsonDeserializer.TypeSettings<IItem> configuredType = null;
        settings.ConfigureType<IItem>(type =>
        {
            configuredType = type;
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
        });
        var firstDeserializer = new JsonDeserializer(settings);
        configuredType.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b");
        var secondDeserializer = new JsonDeserializer(settings);

        Assert.True(firstDeserializer.TryDeserialize("{\"Kind\":\"b\",\"B\":2}", out IItem first));
        Assert.Null(first);
        Assert.True(secondDeserializer.TryDeserialize("{\"Kind\":\"b\",\"B\":2}", out IItem second));
        Assert.Equal(2, Assert.IsType<ItemB>(second).B);
    }

    [Fact]
    public void ProposedTypeTakesPrecedenceOverFieldChecker()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<IItem>(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB>();
        });
        var deserializer = new JsonDeserializer(settings);
        string json = $"{{\"$type\":\"{typeof(ItemB).FullName}\",\"Kind\":\"a\",\"B\":2}}";

        Assert.True(deserializer.TryDeserialize(json, out IItem value));
        Assert.Equal(2, Assert.IsType<ItemB>(value).B);
    }

    [Fact]
    public void SelectedOptionUsesConfiguredConstructor()
    {
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemWithConstructor, string>("Kind", value => value == "ctor",
                mapped => mapped.AddConstructor(() => new ItemWithConstructor(7)));
            type.AddInstanceTypeMappingOption<ItemB>();
        });

        Assert.True(deserializer.TryDeserialize("{\"Kind\":\"ctor\",\"A\":3}", out IItem value));
        var typed = Assert.IsType<ItemWithConstructor>(value);
        Assert.Equal(7, typed.Seed);
        Assert.Equal(3, typed.A);
    }

    [Fact]
    public void MemberMappingOverridesExactTypeMapping()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<IItem>(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b");
        });
        settings.ConfigureType<ItemContainer>(type =>
            type.ConfigureMember<IItem>(nameof(ItemContainer.Item), member =>
            {
                member.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "b");
                member.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "a");
            }));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize(
            "{\"Item\":{\"Kind\":\"b\",\"A\":3},\"Other\":{\"Kind\":\"b\",\"B\":4}}",
            out ItemContainer value));
        Assert.Equal(3, Assert.IsType<ItemA>(value.Item).A);
        Assert.Equal(4, Assert.IsType<ItemB>(value.Other).B);
    }

    [Fact]
    public void ElementMappingOverridesExactTypeMapping()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<IItem>(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b");
        });
        settings.ConfigureType<List<IItem>>(type =>
            type.ConfigureElement<IItem>(element =>
            {
                element.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "b");
                element.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "a");
            }));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("[{\"Kind\":\"b\",\"A\":3}]", out List<IItem> value));
        Assert.Equal(3, Assert.IsType<ItemA>(Assert.Single(value)).A);
    }

    [Fact]
    public void SelectedMappedOptionParticipatesInReferenceResolution()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<IItem>(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b");
        });
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize(
            "{\"Item\":{\"$id\":\"item\",\"Kind\":\"a\",\"A\":3},\"Other\":{\"$ref\":\"item\"}}",
            out ItemContainer value));
        Assert.Equal(3, Assert.IsType<ItemA>(value.Item).A);
        Assert.Same(value.Item, value.Other);
    }

    [Fact]
    public void PopulateExistingInstanceUsesItsConcreteType()
    {
        int checkerCalls = 0;
        var deserializer = CreateDeserializer(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => { checkerCalls++; return value == "a"; });
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b");
        });
        var value = new ItemA { A = 1 };

        Assert.True(deserializer.TryPopulate("{\"Kind\":\"b\",\"A\":7}", value));
        Assert.Equal(7, value.A);
        Assert.Equal("b", value.Kind);
        Assert.Equal(0, checkerCalls);
    }

    [Fact]
    public void UnknownObjectFallsBackToDictionaryWhenNoOptionMatches()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.ConfigureType<object>(type =>
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a"));
        var deserializer = new JsonDeserializer(settings);

        Assert.True(deserializer.TryDeserialize("{\"unknown\":1}", out object value));
        var dictionary = Assert.IsType<Dictionary<string, object>>(value);
        Assert.Equal(1L, Convert.ToInt64(dictionary["unknown"]));
    }

    [Fact]
    public void ForbiddenMappedOptionIsRejected()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.AddForbiddenType(typeof(ItemA));
        settings.ConfigureType<IItem>(type =>
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a"));
        var deserializer = new JsonDeserializer(settings);

        Assert.ThrowsAny<Exception>(() => deserializer.TryDeserialize("{\"Kind\":\"a\",\"A\":1}", out IItem _));
    }

    [Fact]
    public void NonWhitelistedMappedOptionIsRejected()
    {
        var settings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            typeWhitelistMode = JsonDeserializer.Settings.TypeWhitelistMode.ForAllNonIntrinsicTypes,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        settings.AddAllowedType<IItem>();
        settings.ConfigureType<IItem>(type =>
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a"));
        var deserializer = new JsonDeserializer(settings);

        Assert.ThrowsAny<Exception>(() => deserializer.TryDeserialize("{\"Kind\":\"a\",\"A\":1}", out IItem _));
    }
}
