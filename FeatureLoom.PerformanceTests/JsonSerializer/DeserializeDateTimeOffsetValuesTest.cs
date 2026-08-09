using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for DateTimeOffset values, mirroring
/// <see cref="SerializeDateTimeOffsetValuesTest"/>.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeDateTimeOffsetValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();
    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    public static IEnumerable<SerializeDateTimeOffsetValuesTest.DateTimeOffsetCase> DateTimeOffsetValues => new SerializeDateTimeOffsetValuesTest.DateTimeOffsetCase[]
    {
        new SerializeDateTimeOffsetValuesTest.DateTimeOffsetCase("Default", default),
        new SerializeDateTimeOffsetValuesTest.DateTimeOffsetCase("ZeroOffset", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero)),
        new SerializeDateTimeOffsetValuesTest.DateTimeOffsetCase("PositiveOffset", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2))),
        new SerializeDateTimeOffsetValuesTest.DateTimeOffsetCase("NegativeOffset", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5))),
        new SerializeDateTimeOffsetValuesTest.DateTimeOffsetCase("HalfHourOffset", new DateTimeOffset(2024, 1, 2, 3, 4, 5, new TimeSpan(5, 30, 0))),
        new SerializeDateTimeOffsetValuesTest.DateTimeOffsetCase("WithFraction", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero).AddTicks(1234567)),
    };

    [ParamsSource(nameof(DateTimeOffsetValues))]
    public SerializeDateTimeOffsetValuesTest.DateTimeOffsetCase dateTimeOffsetCase;

    private DateTimeOffset value;
    private DateTimeOffset[] array;

    MemoryStream featureStream_Single = new MemoryStream();
    MemoryStream featureStream_Array = new MemoryStream();

    [GlobalSetup]
    public void Setup()
    {
        value = dateTimeOffsetCase.Value;
        array = new DateTimeOffset[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        featureJsonSerializer.Serialize(featureStream_Single, value);
        featureJsonSerializer.Serialize(featureStream_Array, array);

        SampleOutput.Collect($"DateTimeOffset({dateTimeOffsetCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        featureStream_Single.Position = 0;
        featureStream_Array.Position = 0;
    }

    [Benchmark]
    public void DeserializeDateTimeOffset_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single, out DateTimeOffset result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeDateTimeOffset_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            DateTimeOffset result = System.Text.Json.JsonSerializer.Deserialize<DateTimeOffset>(featureStream_Single, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeDateTimeOffset_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            DateTimeOffset result = SerializerConfigs.DeserializeWithSpanJson<DateTimeOffset>(featureStream_Single);
        }
    }
#endif

    [Benchmark]
    public void DeserializeDateTimeOffset_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array, out DateTimeOffset[] result);
        }
    }

    [Benchmark]
    public void DeserializeDateTimeOffset_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            DateTimeOffset[] result = System.Text.Json.JsonSerializer.Deserialize<DateTimeOffset[]>(featureStream_Array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeDateTimeOffset_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            DateTimeOffset[] result = SerializerConfigs.DeserializeWithSpanJson<DateTimeOffset[]>(featureStream_Array);
        }
    }
#endif
}
