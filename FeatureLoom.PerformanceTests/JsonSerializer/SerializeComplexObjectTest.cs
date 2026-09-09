using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for a balanced object covering all commonly
/// used field types. The single-object case shows the per-serialization overhead, while
/// the array case makes the actual value formatting dominate the measurement.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(25)]
[MaxIterationCount(100)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SerializeComplexObjectTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    static JsonSerializerOptions systemTextJsonSourceGenSerializerSettings = SerializerConfigs.CreateSystemTextSourceGenOptions();

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 100);

    ComplexObject single = new ComplexObject();
    ComplexObject[] array;

    [GlobalSetup]
    public void Setup()
    {
        array = new ComplexObject[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = new ComplexObject(i);

        SampleOutput.Collect("ComplexObject", single, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeComplexObject_Single_Feature()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, single);
        }
    }

    [BenchmarkCategory("Single")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeComplexObject_Single_SystemText()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, single, systemTextJsonSerializerSettings);
        }
    }

    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeComplexObject_Single_SystemTextSourceGen()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, single, systemTextJsonSourceGenSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeComplexObject_Single_SpanJson()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            // SpanJson only offers an async stream API. The MemoryStream completes synchronously,
            // so blocking here adds no measurable overhead but ensures the write actually happened.
            SerializerConfigs.SerializeWithSpanJson(single, memoryStream);
        }
    }
#endif

    [BenchmarkCategory("Array")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeComplexObject_Array_Feature()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [BenchmarkCategory("Array")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeComplexObject_Array_SystemText()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
        }
    }

    [BenchmarkCategory("Array")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeComplexObject_Array_SystemTextSourceGen()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSourceGenSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Array")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeComplexObject_Array_SpanJson()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
        }
    }
#endif
}
