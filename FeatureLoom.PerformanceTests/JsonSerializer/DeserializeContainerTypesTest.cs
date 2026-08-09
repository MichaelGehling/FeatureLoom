using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance of different container types (Array and
/// List&lt;T&gt;) holding int values, mirroring <see cref="SerializeContainerTypesTest"/>.
/// The lazy IEnumerable&lt;T&gt; case has no meaningful deserialization counterpart (there
/// is no lazy container to populate), so only Array and List are measured here.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeContainerTypesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();
    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    MemoryStream arrayStream = new MemoryStream();
    MemoryStream listStream = new MemoryStream();

    [GlobalSetup]
    public void Setup()
    {
        int[] array = Enumerable.Range(0, BenchmarkSettings.ArraySize).ToArray();
        List<int> list = new List<int>(array);

        featureJsonSerializer.Serialize(arrayStream, array);
        featureJsonSerializer.Serialize(listStream, list);

        SampleOutput.Collect($"Container(Array,{BenchmarkSettings.ArraySize})", array, featureJsonSerializer, systemTextJsonSerializerSettings, maxLength: 200);
        SampleOutput.Collect($"Container(List,{BenchmarkSettings.ArraySize})", list, featureJsonSerializer, systemTextJsonSerializerSettings, maxLength: 200);
    }

    [IterationSetup]
    public void Prepare()
    {
        arrayStream.Position = 0;
        listStream.Position = 0;
    }

    [Benchmark]
    public void DeserializeArray_FromStream_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            arrayStream.Position = 0;
            featureJsonDeserializer.TryDeserialize(arrayStream, out int[] result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeArray_FromStream_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            arrayStream.Position = 0;
            int[] result = System.Text.Json.JsonSerializer.Deserialize<int[]>(arrayStream, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeArray_FromStream_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            arrayStream.Position = 0;
            int[] result = SerializerConfigs.DeserializeWithSpanJson<int[]>(arrayStream);
        }
    }
#endif

    [Benchmark]
    public void DeserializeList_FromStream_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            listStream.Position = 0;
            featureJsonDeserializer.TryDeserialize(listStream, out List<int> result);
        }
    }

    [Benchmark]
    public void DeserializeList_FromStream_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            listStream.Position = 0;
            List<int> result = System.Text.Json.JsonSerializer.Deserialize<List<int>>(listStream, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeList_FromStream_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            listStream.Position = 0;
            List<int> result = SerializerConfigs.DeserializeWithSpanJson<List<int>>(listStream);
        }
    }
#endif
}
