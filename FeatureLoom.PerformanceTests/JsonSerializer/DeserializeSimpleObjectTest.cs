using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for a small object with only three fields.
/// It serves as the low-overhead reference next to <see cref="DeserializeComplexObjectTest"/>:
/// the single-object case is dominated by the per-deserialization overhead, while the
/// array case shows the cost of the actual field parsing.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeSimpleObjectTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonDeserializer featureJsonDeserializer = new JsonDeserializer(settings =>
    {
        settings.dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    MemoryStream featureStream_Single = new MemoryStream();
    MemoryStream featureStream_Array = new MemoryStream();

    SimpleObject single = new SimpleObject();
    SimpleObject[] array;

    [GlobalSetup]
    public void Setup()
    {
        array = new SimpleObject[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = new SimpleObject() { id = i };

        featureJsonSerializer.Serialize(featureStream_Single, single);
        featureJsonSerializer.Serialize(featureStream_Array, array);

        SampleOutput.Collect("SimpleObject", single, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        featureStream_Single.Position = 0;
        featureStream_Array.Position = 0;
    }

    [Benchmark]
    public void DeserializeSimpleObject_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single, out SimpleObject result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeSimpleObject_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            SimpleObject result = System.Text.Json.JsonSerializer.Deserialize<SimpleObject>(featureStream_Single, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeSimpleObject_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            SimpleObject result = SerializerConfigs.DeserializeWithSpanJson<SimpleObject>(featureStream_Single);
        }
    }
#endif

    [Benchmark]
    public void DeserializeSimpleObject_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array, out SimpleObject[] result);
        }
    }

    [Benchmark]
    public void DeserializeSimpleObject_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            SimpleObject[] result = System.Text.Json.JsonSerializer.Deserialize<SimpleObject[]>(featureStream_Array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeSimpleObject_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            SimpleObject[] result = SerializerConfigs.DeserializeWithSpanJson<SimpleObject[]>(featureStream_Array);
        }
    }
#endif
}
