using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for DateTime values, mirroring
/// <see cref="SerializeDateTimeValuesTest"/>. The cases cover the three DateTimeKind
/// variants and values with and without fractional seconds.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeDateTimeValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();
    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    public static IEnumerable<SerializeDateTimeValuesTest.DateTimeCase> DateTimeValues => new SerializeDateTimeValuesTest.DateTimeCase[]
    {
        new SerializeDateTimeValuesTest.DateTimeCase("Default", default),
        new SerializeDateTimeValuesTest.DateTimeCase("Utc", new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)),
        new SerializeDateTimeValuesTest.DateTimeCase("UtcFraction", new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(1234567)),
        new SerializeDateTimeValuesTest.DateTimeCase("Local", new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Local)),
        new SerializeDateTimeValuesTest.DateTimeCase("Unspecified", new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified)),
    };

    [ParamsSource(nameof(DateTimeValues))]
    public SerializeDateTimeValuesTest.DateTimeCase dateTimeCase;

    private DateTime value;
    private DateTime[] array;

    MemoryStream featureStream_Single = new MemoryStream();
    MemoryStream featureStream_Array = new MemoryStream();

    [GlobalSetup]
    public void Setup()
    {
        value = dateTimeCase.Value;
        array = new DateTime[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        featureJsonSerializer.Serialize(featureStream_Single, value);
        featureJsonSerializer.Serialize(featureStream_Array, array);

        SampleOutput.Collect($"DateTime({dateTimeCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        featureStream_Single.Position = 0;
        featureStream_Array.Position = 0;
    }

    [Benchmark]
    public void DeserializeDateTime_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single, out DateTime result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeDateTime_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            DateTime result = System.Text.Json.JsonSerializer.Deserialize<DateTime>(featureStream_Single, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeDateTime_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            DateTime result = SerializerConfigs.DeserializeWithSpanJson<DateTime>(featureStream_Single);
        }
    }
#endif

    [Benchmark]
    public void DeserializeDateTime_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array, out DateTime[] result);
        }
    }

    [Benchmark]
    public void DeserializeDateTime_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            DateTime[] result = System.Text.Json.JsonSerializer.Deserialize<DateTime[]>(featureStream_Array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeDateTime_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            DateTime[] result = SerializerConfigs.DeserializeWithSpanJson<DateTime[]>(featureStream_Array);
        }
    }
#endif
}
