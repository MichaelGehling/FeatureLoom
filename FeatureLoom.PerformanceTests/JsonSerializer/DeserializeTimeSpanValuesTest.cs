using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for TimeSpan values, mirroring
/// <see cref="SerializeTimeSpanValuesTest"/>.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeTimeSpanValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();
    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    public static IEnumerable<SerializeTimeSpanValuesTest.TimeSpanCase> TimeSpanValues => new SerializeTimeSpanValuesTest.TimeSpanCase[]
    {
        new SerializeTimeSpanValuesTest.TimeSpanCase("Zero", TimeSpan.Zero),
        new SerializeTimeSpanValuesTest.TimeSpanCase("Time", new TimeSpan(1, 2, 3)),
        new SerializeTimeSpanValuesTest.TimeSpanCase("WithDays", new TimeSpan(2, 3, 4, 5)),
        new SerializeTimeSpanValuesTest.TimeSpanCase("WithFraction", new TimeSpan(2, 3, 4, 5, 6)),
        new SerializeTimeSpanValuesTest.TimeSpanCase("Negative", new TimeSpan(2, 3, 4, 5).Negate()),
    };

    [ParamsSource(nameof(TimeSpanValues))]
    public SerializeTimeSpanValuesTest.TimeSpanCase timeSpanCase;

    private TimeSpan value;
    private TimeSpan[] array;

    MemoryStream featureStream_Single = new MemoryStream();
    MemoryStream featureStream_Array = new MemoryStream();

    [GlobalSetup]
    public void Setup()
    {
        value = timeSpanCase.Value;
        array = new TimeSpan[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        featureJsonSerializer.Serialize(featureStream_Single, value);
        featureJsonSerializer.Serialize(featureStream_Array, array);

        SampleOutput.Collect($"TimeSpan({timeSpanCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        featureStream_Single.Position = 0;
        featureStream_Array.Position = 0;
    }

    [Benchmark]
    public void DeserializeTimeSpan_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single, out TimeSpan result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeTimeSpan_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            TimeSpan result = System.Text.Json.JsonSerializer.Deserialize<TimeSpan>(featureStream_Single, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeTimeSpan_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            TimeSpan result = SerializerConfigs.DeserializeWithSpanJson<TimeSpan>(featureStream_Single);
        }
    }
#endif

    [Benchmark]
    public void DeserializeTimeSpan_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array, out TimeSpan[] result);
        }
    }

    [Benchmark]
    public void DeserializeTimeSpan_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            TimeSpan[] result = System.Text.Json.JsonSerializer.Deserialize<TimeSpan[]>(featureStream_Array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeTimeSpan_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            TimeSpan[] result = SerializerConfigs.DeserializeWithSpanJson<TimeSpan[]>(featureStream_Array);
        }
    }
#endif
}
