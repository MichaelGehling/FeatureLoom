using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using FeatureLoom.Serialization;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for byte arrays of different sizes, mirroring
/// <see cref="SerializeByteArrayValuesTest"/>.
/// <para>
/// FeatureLoom's byte array reader auto-detects base64 vs. number-array input, so a single
/// deserializer instance handles both formats. SpanJson only ever produces (and reads) the
/// number-array format, so it is measured against a dedicated fixture serialized with
/// SpanJson itself.
/// </para>
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(25)]
[MaxIterationCount(100)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class DeserializeByteArrayValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static Serialization.JsonSerializer featureJsonSerializerNumbers = SerializerConfigs.CreateFeatureSerializerWithByteArrayAsNumbers();

    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();
    static JsonSerializerOptions systemTextJsonSerializerSettingsNumbers = SerializerConfigs.CreateSystemTextOptionsWithByteArrayAsNumbers();

    [Params(0, 1, 16, 1000)]
    public int size;

    private byte[] value;
    private byte[][] array;
    private int arrayIterations;

    MemoryStream base64Stream_Single = new MemoryStream();
    MemoryStream base64Stream_Array = new MemoryStream();
    MemoryStream numbersStream_Single = new MemoryStream();
    MemoryStream numbersStream_Array = new MemoryStream();
#if NET6_0_OR_GREATER
    MemoryStream spanJsonStream_Single = new MemoryStream();
    MemoryStream spanJsonStream_Array = new MemoryStream();
#endif

    [GlobalSetup]
    public void Setup()
    {
        value = new byte[size];
        for (int i = 0; i < value.Length; i++) value[i] = (byte)(i * 7);

        int outerSize = size > 16 ? BenchmarkSettings.ArraySize / 100 : BenchmarkSettings.ArraySize;
        array = new byte[outerSize][];
        for (int i = 0; i < array.Length; i++) array[i] = value;
        arrayIterations = BenchmarkSettings.ArraySize * BenchmarkSettings.ArrayIterations / outerSize;

        featureJsonSerializer.Serialize(base64Stream_Single, value);
        featureJsonSerializer.Serialize(base64Stream_Array, array);
        featureJsonSerializerNumbers.Serialize(numbersStream_Single, value);
        featureJsonSerializerNumbers.Serialize(numbersStream_Array, array);
#if NET6_0_OR_GREATER
        SerializerConfigs.SerializeWithSpanJson(value, spanJsonStream_Single);
        SerializerConfigs.SerializeWithSpanJson(array, spanJsonStream_Array);
#endif

        SampleOutput.Collect($"ByteArray({size},Base64)", value, featureJsonSerializer, systemTextJsonSerializerSettings, maxLength: 200);
        SampleOutput.Collect($"ByteArray({size},Numbers)", value, featureJsonSerializerNumbers, systemTextJsonSerializerSettingsNumbers, maxLength: 200);
    }


    [BenchmarkCategory("Single-Base64")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void DeserializeByteArray_Single_Base64_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            base64Stream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(base64Stream_Single, out byte[] result);
        }
    }

    [BenchmarkCategory("Single-Base64")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void DeserializeByteArray_Single_Base64_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            base64Stream_Single.Position = 0;
            byte[] result = System.Text.Json.JsonSerializer.Deserialize<byte[]>(base64Stream_Single, systemTextJsonSerializerSettings);
        }
    }

    [BenchmarkCategory("Single-Numbers")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void DeserializeByteArray_Single_Numbers_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            numbersStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(numbersStream_Single, out byte[] result);
        }
    }

    [BenchmarkCategory("Single-Numbers")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void DeserializeByteArray_Single_Numbers_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            numbersStream_Single.Position = 0;
            byte[] result = System.Text.Json.JsonSerializer.Deserialize<byte[]>(numbersStream_Single, systemTextJsonSerializerSettingsNumbers);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Single-Numbers")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void DeserializeByteArray_Single_Numbers_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            spanJsonStream_Single.Position = 0;
            byte[] result = SerializerConfigs.DeserializeWithSpanJson<byte[]>(spanJsonStream_Single);
        }
    }
#endif

    // arrayIterations depends on the size parameter and therefore cannot be used in an attribute.
    // Since arrayIterations * outerSize is constant, the operation count is expressed as the total
    // number of byte arrays processed per invocation, which is identical for all sizes.
    [BenchmarkCategory("Array-Base64")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArraySize * BenchmarkSettings.ArrayIterations)]
    public void DeserializeByteArray_Array_Base64_Feature()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            base64Stream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(base64Stream_Array, out byte[][] result);
        }
    }

    [BenchmarkCategory("Array-Base64")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.ArraySize * BenchmarkSettings.ArrayIterations)]
    public void DeserializeByteArray_Array_Base64_SystemText()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            base64Stream_Array.Position = 0;
            byte[][] result = System.Text.Json.JsonSerializer.Deserialize<byte[][]>(base64Stream_Array, systemTextJsonSerializerSettings);
        }
    }

    [BenchmarkCategory("Array-Numbers")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArraySize * BenchmarkSettings.ArrayIterations)]
    public void DeserializeByteArray_Array_Numbers_Feature()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            numbersStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(numbersStream_Array, out byte[][] result);
        }
    }

    [BenchmarkCategory("Array-Numbers")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.ArraySize * BenchmarkSettings.ArrayIterations)]
    public void DeserializeByteArray_Array_Numbers_SystemText()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            numbersStream_Array.Position = 0;
            byte[][] result = System.Text.Json.JsonSerializer.Deserialize<byte[][]>(numbersStream_Array, systemTextJsonSerializerSettingsNumbers);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Array-Numbers")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArraySize * BenchmarkSettings.ArrayIterations)]
    public void DeserializeByteArray_Array_Numbers_SpanJson()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            spanJsonStream_Array.Position = 0;
            byte[][] result = SerializerConfigs.DeserializeWithSpanJson<byte[][]>(spanJsonStream_Array);
        }
    }
#endif
}
