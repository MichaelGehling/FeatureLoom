using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for byte arrays of different sizes. Byte
/// arrays are a common payload and are written through a dedicated path, so the sizes
/// cover the empty case, very short arrays (where the per-call overhead dominates) and
/// a large one (where the per-element writing dominates).
/// Each case is measured as a single array and as an array of arrays.
/// <para>
/// Byte arrays are the one case where the serializers cannot be aligned to a single
/// output format: FeatureLoom and System.Text.Json write base64 by default, SpanJson
/// always writes a JSON number array. Both formats are therefore measured separately
/// (suffix Base64 / Numbers) so each column is compared against an equivalent workload.
/// SpanJson only appears in the number variant, since it cannot produce base64.
/// </para>
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeByteArrayValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    static Serialization.JsonSerializer featureJsonSerializerNumbers = SerializerConfigs.CreateFeatureSerializerWithByteArrayAsNumbers();

    static JsonSerializerOptions systemTextJsonSerializerSettingsNumbers = SerializerConfigs.CreateSystemTextOptionsWithByteArrayAsNumbers();

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 100);

    [Params(0, 1, 16, 1000)]
    public int size;

    private byte[] value;
    private byte[][] array;

    // The large case uses a smaller outer array, so the iteration count is scaled up to
    // keep the total number of serialized byte arrays identical for all sizes.
    private int arrayIterations;

    [GlobalSetup]
    public void Setup()
    {
        value = new byte[size];
        // Deterministic content covering the whole byte range, so the number of written
        // digits per element is representative.
        for (int i = 0; i < value.Length; i++) value[i] = (byte)(i * 7);

        // Fewer outer elements for the large case, so the total payload stays comparable.
        int outerSize = size > 16 ? BenchmarkSettings.ArraySize / 100 : BenchmarkSettings.ArraySize;
        array = new byte[outerSize][];
        for (int i = 0; i < array.Length; i++) array[i] = value;
        arrayIterations = BenchmarkSettings.ArraySize * BenchmarkSettings.ArrayIterations / outerSize;

        SampleOutput.Collect($"ByteArray({size},Base64)", value, featureJsonSerializer, systemTextJsonSerializerSettings, maxLength: 200);
        SampleOutput.Collect($"ByteArray({size},Numbers)", value, featureJsonSerializerNumbers, systemTextJsonSerializerSettingsNumbers, maxLength: 200);
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeByteArray_Single_Base64_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeByteArray_Single_Base64_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettings);
        }
    }

    [Benchmark]
    public void SerializeByteArray_Single_Numbers_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializerNumbers.Serialize(memoryStream, value);
        }
    }

    [Benchmark]
    public void SerializeByteArray_Single_Numbers_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettingsNumbers);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeByteArray_Single_Numbers_SpanJson()
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
    public void SerializeByteArray_Array_Base64_Feature()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [Benchmark]
    public void SerializeByteArray_Array_Base64_SystemText()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
        }
    }

    [Benchmark]
    public void SerializeByteArray_Array_Numbers_Feature()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            featureJsonSerializerNumbers.Serialize(memoryStream, array);
        }
    }

    [Benchmark]
    public void SerializeByteArray_Array_Numbers_SystemText()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettingsNumbers);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeByteArray_Array_Numbers_SpanJson()
    {
        for (int i = 0; i < arrayIterations; i++)
        {
            SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
        }
    }
#endif
}
