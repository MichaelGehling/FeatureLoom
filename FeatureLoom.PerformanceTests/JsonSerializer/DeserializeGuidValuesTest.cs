using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for Guid values, mirroring
/// <see cref="SerializeGuidValuesTest"/>.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeGuidValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();
    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    public static IEnumerable<SerializeGuidValuesTest.GuidCase> GuidValues => new SerializeGuidValuesTest.GuidCase[]
    {
        new SerializeGuidValuesTest.GuidCase("Empty", Guid.Empty),
        new SerializeGuidValuesTest.GuidCase("Mixed", new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff")),
        new SerializeGuidValuesTest.GuidCase("AllF", new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff")),
    };

    [ParamsSource(nameof(GuidValues))]
    public SerializeGuidValuesTest.GuidCase guidCase;

    private Guid value;
    private Guid[] array;

    MemoryStream featureStream_Single = new MemoryStream();
    MemoryStream featureStream_Array = new MemoryStream();

    [GlobalSetup]
    public void Setup()
    {
        value = guidCase.Value;
        array = new Guid[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        featureJsonSerializer.Serialize(featureStream_Single, value);
        featureJsonSerializer.Serialize(featureStream_Array, array);

        SampleOutput.Collect($"Guid({guidCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        featureStream_Single.Position = 0;
        featureStream_Array.Position = 0;
    }

    [Benchmark]
    public void DeserializeGuid_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single, out Guid result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeGuid_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            Guid result = System.Text.Json.JsonSerializer.Deserialize<Guid>(featureStream_Single, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeGuid_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            Guid result = SerializerConfigs.DeserializeWithSpanJson<Guid>(featureStream_Single);
        }
    }
#endif

    [Benchmark]
    public void DeserializeGuid_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array, out Guid[] result);
        }
    }

    [Benchmark]
    public void DeserializeGuid_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            Guid[] result = System.Text.Json.JsonSerializer.Deserialize<Guid[]>(featureStream_Array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeGuid_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            Guid[] result = SerializerConfigs.DeserializeWithSpanJson<Guid[]>(featureStream_Array);
        }
    }
#endif
}
