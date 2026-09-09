using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for long values of different magnitudes,
/// mirroring <see cref="SerializeLongValuesTest"/>.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(25)]
[MaxIterationCount(100)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class DeserializeLongValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();
    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

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

    MemoryStream featureStream_Single = new MemoryStream();
    MemoryStream featureStream_Array = new MemoryStream();

    [GlobalSetup]
    public void Setup()
    {
        array = new long[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        featureJsonSerializer.Serialize(featureStream_Single, value);
        featureJsonSerializer.Serialize(featureStream_Array, array);

        SampleOutput.Collect($"Long({value})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }


    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void DeserializeLong_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single, out long result);
        }
    }

    [BenchmarkCategory("Single")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void DeserializeLong_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            long result = System.Text.Json.JsonSerializer.Deserialize<long>(featureStream_Single, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void DeserializeLong_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            long result = SerializerConfigs.DeserializeWithSpanJson<long>(featureStream_Single);
        }
    }
#endif

    [BenchmarkCategory("Array")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void DeserializeLong_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array, out long[] result);
        }
    }

    [BenchmarkCategory("Array")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void DeserializeLong_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            long[] result = System.Text.Json.JsonSerializer.Deserialize<long[]>(featureStream_Array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Array")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void DeserializeLong_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            long[] result = SerializerConfigs.DeserializeWithSpanJson<long[]>(featureStream_Array);
        }
    }
#endif
}
