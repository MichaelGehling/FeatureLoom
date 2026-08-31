using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests;
[CPUUsageDiagnoser]
public class DeserializeMultiOptionSharedValueBenchmark
{
    private JsonDeserializer fieldCheckerDeserializer;
    private JsonDeserializer wholeValueDeserializer;
    [GlobalSetup]
    public void Setup()
    {
        var fieldSettings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        fieldSettings.ConfigureType<IItem>(type =>
        {
            type.AddInstanceTypeMappingOption<ItemA, string>("Kind", value => value == "a");
            type.AddInstanceTypeMappingOption<ItemB, string>("Kind", value => value == "b");
            type.AddInstanceTypeMappingOption<ItemC, string>("Kind", value => value == "c");
        });
        fieldCheckerDeserializer = new JsonDeserializer(fieldSettings);
        var wholeValueSettings = new JsonDeserializer.Settings
        {
            referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled,
            proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
            rethrowExceptions = true,
            logCatchedExceptions = false
        };
        wholeValueSettings.ConfigureType<object>(type =>
        {
            type.AddInstanceTypeMappingValueOption<string, ItemId>((string value, out ItemId result) =>
            {
                result = null;
                return false;
            });
            type.AddInstanceTypeMappingValueOption<string, ItemCode>((string value, out ItemCode result) =>
            {
                result = null;
                return false;
            });
            type.AddInstanceTypeMappingValueOption<string, ItemName>((string value, out ItemName result) =>
            {
                result = value == "name:42" ? new ItemName(value) : null;
                return result != null;
            });
        });
        wholeValueDeserializer = new JsonDeserializer(wholeValueSettings);
    }

    [Benchmark]
    public IItem SharedFieldCheckerValue()
    {
        fieldCheckerDeserializer.TryDeserialize("{\"Kind\":\"c\",\"C\":42}", out IItem value);
        return value;
    }

    [Benchmark]
    public object SharedWholeValue()
    {
        wholeValueDeserializer.TryDeserialize("\"name:42\"", out object value);
        return value;
    }

    public interface IItem
    {
    }

    public class ItemA : IItem
    {
        public string Kind;
        public int A;
    }

    public class ItemB : IItem
    {
        public string Kind;
        public int B;
    }

    public class ItemC : IItem
    {
        public string Kind;
        public int C;
    }

    public sealed class ItemId
    {
        public string Value { get; }

        public ItemId(string value) => Value = value;
    }

    public sealed class ItemCode
    {
        public string Value { get; }

        public ItemCode(string value) => Value = value;
    }

    public sealed class ItemName
    {
        public string Value { get; }

        public ItemName(string value) => Value = value;
    }
}