using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for enum values, mirroring
/// <see cref="SerializeEnumValuesTest"/>.
/// <para>
/// With enum-as-string the deserializer has to map a member name back to its value, which
/// is a distinct code path from the numeric readers: it depends on the name length and on
/// how the member is looked up. The cases therefore vary name length and member position,
/// so a linear scan over the member list would show up as a difference between them.
/// </para>
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeEnumValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();

    /// <summary>
    /// Second deserializer instance with the string cache / deduplication feature disabled.
    /// Enum names are repeated strings, so the cache could hide the real name lookup cost.
    /// </summary>
    static JsonDeserializer featureJsonDeserializer_NoStringCache = new JsonDeserializer(settings =>
    {
        settings.useStringCache = false;
    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    /// <summary>
    /// Serializer that writes enums as numbers instead of names, so the numeric reader path
    /// can be measured against the name lookup path.
    /// </summary>
    static Serialization.JsonSerializer featureJsonSerializer_EnumAsInt = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
    {
        typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
        enumAsString = false,
    });

    public static IEnumerable<SerializeEnumValuesTest.EnumCase> EnumValues => SerializeEnumValuesTest.EnumValues;

    [ParamsSource(nameof(EnumValues))]
    public SerializeEnumValuesTest.EnumCase enumCase;

    private SerializeEnumValuesTest.TestEnum value;
    private SerializeEnumValuesTest.TestEnum[] array;

    MemoryStream featureStream_Single = new MemoryStream();
    MemoryStream featureStream_Array = new MemoryStream();
    MemoryStream featureStream_Single_Int = new MemoryStream();
    MemoryStream featureStream_Array_Int = new MemoryStream();

    [GlobalSetup]
    public void Setup()
    {
        value = enumCase.Value;
        array = new SerializeEnumValuesTest.TestEnum[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        featureJsonSerializer.Serialize(featureStream_Single, value);
        featureJsonSerializer.Serialize(featureStream_Array, array);
        featureJsonSerializer_EnumAsInt.Serialize(featureStream_Single_Int, value);
        featureJsonSerializer_EnumAsInt.Serialize(featureStream_Array_Int, array);

        SampleOutput.Collect($"Enum({enumCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        featureStream_Single.Position = 0;
        featureStream_Array.Position = 0;
        featureStream_Single_Int.Position = 0;
        featureStream_Array_Int.Position = 0;
    }

    [Benchmark]
    public void DeserializeEnum_Single_Feature_AsInt()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single_Int.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single_Int, out SerializeEnumValuesTest.TestEnum result);
        }
    }

    [Benchmark]
    public void DeserializeEnum_Array_Feature_AsInt()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array_Int.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array_Int, out SerializeEnumValuesTest.TestEnum[] result);
        }
    }

    [Benchmark]
    public void DeserializeEnum_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single, out SerializeEnumValuesTest.TestEnum result);
        }
    }

    [Benchmark]
    public void DeserializeEnum_Single_Feature_NoStringCache()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer_NoStringCache.TryDeserialize(featureStream_Single, out SerializeEnumValuesTest.TestEnum result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeEnum_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            var result = System.Text.Json.JsonSerializer.Deserialize<SerializeEnumValuesTest.TestEnum>(featureStream_Single, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeEnum_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            var result = SerializerConfigs.DeserializeWithSpanJson<SerializeEnumValuesTest.TestEnum>(featureStream_Single);
        }
    }
#endif

    [Benchmark]
    public void DeserializeEnum_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array, out SerializeEnumValuesTest.TestEnum[] result);
        }
    }

    [Benchmark]
    public void DeserializeEnum_Array_Feature_NoStringCache()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer_NoStringCache.TryDeserialize(featureStream_Array, out SerializeEnumValuesTest.TestEnum[] result);
        }
    }

    [Benchmark]
    public void DeserializeEnum_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            var result = System.Text.Json.JsonSerializer.Deserialize<SerializeEnumValuesTest.TestEnum[]>(featureStream_Array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeEnum_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            var result = SerializerConfigs.DeserializeWithSpanJson<SerializeEnumValuesTest.TestEnum[]>(featureStream_Array);
        }
    }
#endif
}
