using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for decimal values. The cases cover the cheap
/// small values as well as high-precision and extreme values, which take the slow
/// formatting path.
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
public class SerializeDecimalValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    public static IEnumerable<decimal> DecimalValues => new decimal[]
    {
        0m,
        1m,
        -1m,
        1.25m,
        12345.6789m,
        0.0000000000000000000000000001m,
        decimal.MaxValue,
        decimal.MinValue,
    };

    [ParamsSource(nameof(DecimalValues))]
    public decimal value;

    private decimal[] array;

    [GlobalSetup]
    public void Setup()
    {
        array = new decimal[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        SampleOutput.Collect($"Decimal({value})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeDecimal_Single_Feature()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [BenchmarkCategory("Single")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeDecimal_Single_SystemText()
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
    public void SerializeDecimal_Single_SpanJson()
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
    public void SerializeDecimal_Array_Feature()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [BenchmarkCategory("Array")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeDecimal_Array_SystemText()
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
    public void SerializeDecimal_Array_SpanJson()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
        }
    }
#endif
}
