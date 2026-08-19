using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for a balanced object covering all commonly
/// used field types. The single-object case shows the per-deserialization overhead, while
/// the array case makes the actual value parsing dominate the measurement.
/// <para>
/// SpanJson cannot parse a base64-encoded byte array (it only understands its own
/// number-array representation), so its fixture is serialized with SpanJson itself
/// instead of being shared with the other serializers.
/// </para>
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeComplexObjectTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonDeserializer featureJsonDeserializer = new JsonDeserializer(settings =>
    {
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
    });

    /// <summary>
    /// Second deserializer instance with the string cache / deduplication feature disabled,
    /// so its effect on a realistic object graph can be measured separately.
    /// </summary>
    static JsonDeserializer featureJsonDeserializer_NoStringCache = new JsonDeserializer(settings =>
    {
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        settings.useStringCache = false;
    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    static JsonSerializerOptions systemTextJsonSourceGenSerializerSettings = SerializerConfigs.CreateSystemTextSourceGenOptions();

    MemoryStream featureStream_Single = new MemoryStream();
    MemoryStream featureStream_Array = new MemoryStream();

#if NET6_0_OR_GREATER
    MemoryStream spanJsonStream_Single = new MemoryStream();
    MemoryStream spanJsonStream_Array = new MemoryStream();
#endif

    ComplexObject single = new ComplexObject();
    ComplexObject[] array;

    [GlobalSetup]
    public void Setup()
    {
        array = new ComplexObject[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = new ComplexObject(i);

        featureJsonSerializer.Serialize(featureStream_Single, single);
        featureJsonSerializer.Serialize(featureStream_Array, array);

#if NET6_0_OR_GREATER
        SerializerConfigs.SerializeWithSpanJson(single, spanJsonStream_Single);
        SerializerConfigs.SerializeWithSpanJson(array, spanJsonStream_Array);
#endif

        SampleOutput.Collect("ComplexObject", single, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        featureStream_Single.Position = 0;
        featureStream_Array.Position = 0;
#if NET6_0_OR_GREATER
        spanJsonStream_Single.Position = 0;
        spanJsonStream_Array.Position = 0;
#endif
    }

    [Benchmark]
    public void DeserializeComplexObject_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single, out ComplexObject result);
        }
    }

    [Benchmark]
    public void DeserializeComplexObject_Single_Feature_NoStringCache()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer_NoStringCache.TryDeserialize(featureStream_Single, out ComplexObject result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeComplexObject_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            ComplexObject result = System.Text.Json.JsonSerializer.Deserialize<ComplexObject>(featureStream_Single, systemTextJsonSerializerSettings);
        }
    }

    [Benchmark]
    public void DeserializeComplexObject_Single_SystemTextSourceGen()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            ComplexObject result = System.Text.Json.JsonSerializer.Deserialize<ComplexObject>(featureStream_Single, systemTextJsonSourceGenSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeComplexObject_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            spanJsonStream_Single.Position = 0;
            ComplexObject result = SerializerConfigs.DeserializeWithSpanJson<ComplexObject>(spanJsonStream_Single);
        }
    }
#endif

    [Benchmark]
    public void DeserializeComplexObject_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array, out ComplexObject[] result);
        }
    }

    [Benchmark]
    public void DeserializeComplexObject_Array_Feature_NoStringCache()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer_NoStringCache.TryDeserialize(featureStream_Array, out ComplexObject[] result);
        }
    }

    [Benchmark]
    public void DeserializeComplexObject_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            ComplexObject[] result = System.Text.Json.JsonSerializer.Deserialize<ComplexObject[]>(featureStream_Array, systemTextJsonSerializerSettings);
        }
    }

    [Benchmark]
    public void DeserializeComplexObject_Array_SystemTextSourceGen()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            ComplexObject[] result = System.Text.Json.JsonSerializer.Deserialize<ComplexObject[]>(featureStream_Array, systemTextJsonSourceGenSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeComplexObject_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            spanJsonStream_Array.Position = 0;
            ComplexObject[] result = SerializerConfigs.DeserializeWithSpanJson<ComplexObject[]>(spanJsonStream_Array);
        }
    }
#endif
}
