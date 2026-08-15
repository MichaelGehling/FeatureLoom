using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for enum values. All benchmarks use
/// enum-as-string (see <see cref="SerializerConfigs"/>), because that is SpanJson's only
/// built-in behavior and therefore the only setting where all three serializers produce
/// equivalent output.
/// <para>
/// The cases isolate the properties that can affect name writing: the length of the
/// member name, and the position of the member within the enum (a late member is the
/// worst case for implementations that scan the member list linearly).
/// </para>
/// Each case is measured as a single value (dominated by the per-serialization overhead)
/// and as an array of that value (dominated by the actual name writing).
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeEnumValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    /// <summary>
    /// An enum with enough members that the position of the selected member matters, and
    /// with deliberately different name lengths.
    /// </summary>
    public enum TestEnum
    {
        A,
        Red,
        Pending,
        Active,
        Inactive,
        Cancelled,
        Processing,
        Initialized,
        WaitingForApproval,
        PermanentlyUnavailable,
    }

    public static IEnumerable<EnumCase> EnumValues => new EnumCase[]
    {
        // Shortest possible name, first member: best case.
        new EnumCase("ShortFirst", TestEnum.A),
        // Typical name length, middle of the member list.
        new EnumCase("Medium", TestEnum.Active),
        // Longest name, last member: worst case for linear name lookup.
        new EnumCase("LongLast", TestEnum.PermanentlyUnavailable),
    };

    /// <summary>
    /// Wraps the value so that BenchmarkDotNet shows a readable case name.
    /// </summary>
    public class EnumCase
    {
        public readonly string Name;
        public readonly TestEnum Value;

        public EnumCase(string name, TestEnum value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString() => Name;
    }

    [ParamsSource(nameof(EnumValues))]
    public EnumCase enumCase;

    private TestEnum value;
    private TestEnum[] array;

    [GlobalSetup]
    public void Setup()
    {
        value = enumCase.Value;
        array = new TestEnum[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        SampleOutput.Collect($"Enum({enumCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeEnum_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeEnum_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeEnum_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            // SpanJson only offers an async stream API. The MemoryStream completes synchronously,
            // so blocking here adds no measurable overhead but ensures the write actually happened.
            SerializerConfigs.SerializeWithSpanJson(value, memoryStream);
        }
    }
#endif

    [Benchmark]
    public void SerializeEnum_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [Benchmark]
    public void SerializeEnum_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeEnum_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
        }
    }
#endif
}
