using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for DateTime values. The cases cover the
/// distinct code paths of the writer: the shortcut for the default value, the three
/// DateTimeKind variants (which differ in the offset suffix) and values with and
/// without fractional seconds.
/// Each case is measured as a single value (dominated by the per-serialization overhead)
/// and as an array of that value (dominated by the actual date formatting).
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeDateTimeValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    public static IEnumerable<DateTimeCase> DateTimeValues => new DateTimeCase[]
    {
        new DateTimeCase("Default", default),
        new DateTimeCase("Utc", new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)),
        new DateTimeCase("UtcFraction", new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567)),
        new DateTimeCase("Local", new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Local)),
        new DateTimeCase("Unspecified", new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified)),
    };

    /// <summary>
    /// Wraps the value so that BenchmarkDotNet shows a readable case name.
    /// </summary>
    public class DateTimeCase
    {
        public readonly string Name;
        public readonly DateTime Value;

        public DateTimeCase(string name, DateTime value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString() => Name;
    }

    [ParamsSource(nameof(DateTimeValues))]
    public DateTimeCase dateTimeCase;

    private DateTime value;
    private DateTime[] array;

    [GlobalSetup]
    public void Setup()
    {
        value = dateTimeCase.Value;
        array = new DateTime[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        SampleOutput.Collect($"DateTime({dateTimeCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeDateTime_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeDateTime_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeDateTime_Single_SpanJson()
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
    public void SerializeDateTime_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [Benchmark]
    public void SerializeDateTime_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeDateTime_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
        }
    }
#endif
}
