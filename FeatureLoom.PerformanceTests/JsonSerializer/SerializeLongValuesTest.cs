using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for long values of different magnitudes.
/// The values are chosen to cover the distinct code paths: the cached byte lookup for
/// small values, the digit extraction loop for larger values and the negative variants,
/// including long.MinValue which cannot be negated.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeLongValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
    {

    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = new JsonSerializerOptions()
    {
        IncludeFields = true,
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    public static IEnumerable<long> LongValues => new long[]
    {
        0L,
        7L,
        255L,
        256L,
        -1L,
        -128L,
        -129L,
        12345L,
        1234567890L,
        1234567890123456789L,
        long.MaxValue,
        long.MinValue,
    };

    [ParamsSource(nameof(LongValues))]
    public long value;

    [Params(1000)]
    public int iterations;

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeLong_ToStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeLong_ToStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeLong_ToStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            // SpanJson only offers an async stream API. The MemoryStream completes synchronously,
            // so blocking here adds no measurable overhead but ensures the write actually happened.
            SpanJson.JsonSerializer.Generic.Utf8.SerializeAsync(value, memoryStream).GetAwaiter().GetResult();
        }
    }
#endif
}
