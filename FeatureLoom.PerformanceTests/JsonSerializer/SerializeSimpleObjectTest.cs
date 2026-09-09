using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for a small object with only three fields.
/// It serves as the low-overhead reference next to <see cref="SerializeComplexObjectTest"/>:
/// the single-object case is dominated by the per-serialization overhead, while the
/// array case shows the cost of the actual field writing.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(25)]
[MaxIterationCount(100)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class SerializeSimpleObjectTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 100);

    SimpleObject single = new SimpleObject();
    SimpleObject[] array;

    [GlobalSetup]
    public void Setup()
    {
        array = new SimpleObject[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = new SimpleObject() { id = i };

        SampleOutput.Collect("SimpleObject", single, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeSimpleObject_Single_Feature()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, single);
        }
    }

    [BenchmarkCategory("Single")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeSimpleObject_Single_SystemText()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, single, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Single")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeSimpleObject_Single_SpanJson()
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
    public void SerializeSimpleObject_Array_Feature()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [BenchmarkCategory("Array")]
    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeSimpleObject_Array_SystemText()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [BenchmarkCategory("Array")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.ArrayIterations)]
    public void SerializeSimpleObject_Array_SpanJson()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
        }
    }
#endif
}
