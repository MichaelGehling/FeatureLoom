using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for TimeSpan values. The cases cover the
/// distinct code paths of the writer: the zero shortcut, negative values, values with
/// and without a day part and values with fractional seconds.
/// Each case is measured as a single value (dominated by the per-serialization overhead)
/// and as an array of that value (dominated by the actual formatting).
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeTimeSpanValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    public static IEnumerable<TimeSpanCase> TimeSpanValues => new TimeSpanCase[]
    {
        new TimeSpanCase("Zero", TimeSpan.Zero),
        new TimeSpanCase("Time", new TimeSpan(1, 2, 3)),
        new TimeSpanCase("WithDays", new TimeSpan(2, 3, 4, 5)),
        new TimeSpanCase("WithFraction", new TimeSpan(2, 3, 4, 5, 6)),
        new TimeSpanCase("Negative", new TimeSpan(2, 3, 4, 5).Negate()),
    };

    /// <summary>
    /// Wraps the value so that BenchmarkDotNet shows a readable case name.
    /// </summary>
    public class TimeSpanCase
    {
        public readonly string Name;
        public readonly TimeSpan Value;

        public TimeSpanCase(string name, TimeSpan value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString() => Name;
    }

    [ParamsSource(nameof(TimeSpanValues))]
    public TimeSpanCase timeSpanCase;

    private TimeSpan value;
    private TimeSpan[] array;

    [GlobalSetup]
    public void Setup()
    {
        value = timeSpanCase.Value;
        array = new TimeSpan[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        SampleOutput.Collect($"TimeSpan({timeSpanCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeTimeSpan_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeTimeSpan_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeTimeSpan_Single_SpanJson()
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
    public void SerializeTimeSpan_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [Benchmark]
    public void SerializeTimeSpan_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeTimeSpan_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
        }
    }
#endif
}
