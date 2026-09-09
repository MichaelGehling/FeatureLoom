using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for long values of different magnitudes.
/// The values are chosen to cover the distinct code paths: the cached byte lookup for
/// small values, the digit extraction loop for larger values and the negative variants,
/// including long.MinValue which cannot be negated.
/// Each case is measured as a single value (dominated by the per-serialization overhead)
/// and as an array of that value (dominated by the actual number formatting).
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(25)]
[MaxIterationCount(100)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SerializeLongValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    public static IEnumerable<long> LongValues => new long[]
    {
        0L,
        7L,
        255L,
        256L,
        -1L,
        -128L,
        -129L,
        12345L,
        1234567890L,
        1234567890123456789L,
        long.MaxValue,
        long.MinValue,
    };

    [ParamsSource(nameof(LongValues))]
    public long value;

    private long[] array;

    [GlobalSetup]
    public void Setup()
    {
        array = new long[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        SampleOutput.Collect($"Long({value})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeLong_Single_Feature()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [BenchmarkCategory("Single")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeLong_Single_SystemText()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeLong_Single_SpanJson()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            // SpanJson only offers an async stream API. The MemoryStream completes synchronously,
            // so blocking here adds no measurable overhead but ensures the write actually happened.
            SerializerConfigs.SerializeWithSpanJson(value, memoryStream);
        }
    }
#endif

    [BenchmarkCategory("Array")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeLong_Array_Feature()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [BenchmarkCategory("Array")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeLong_Array_SystemText()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Array")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeLong_Array_SpanJson()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
        }
    }
#endif
}
