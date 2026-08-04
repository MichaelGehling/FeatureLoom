using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeDoubleValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
    {

    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = new JsonSerializerOptions()
    {
        IncludeFields = true,
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    public static IEnumerable<double> DoubleValues => new double[]
    {
        0.0,
        1.0,
        -1.0,
        123.456,
        3.33,
        0.1,
        1.0 / 3.0,
        1e-7,
        1e21,
        12345678.9,
        1234567890123456.0,
        double.Epsilon,
        double.MaxValue,
        double.MinValue,
    };

    [ParamsSource(nameof(DoubleValues))]
    public double value;

    [Params(1000)]
    public int iterations;

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeDouble_ToStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeDouble_ToStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeDouble_ToStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            var jsonBytes = SpanJson.JsonSerializer.Generic.Utf8.SerializeAsync(value, memoryStream);
        }
    }
#endif
}
