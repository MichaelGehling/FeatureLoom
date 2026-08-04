using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance of different container types (Array, List&lt;T&gt;
/// and a plain IEnumerable&lt;T&gt; that is not an array or list, i.e. does not support fast
/// indexed access or a known Count) holding int values, across different element counts.
/// Int is used as the element type since it is cheap to serialize and keeps the comparison
/// focused on the container overhead rather than element-serialization cost.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeContainerTypesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
    {

    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = new JsonSerializerOptions()
    {
        IncludeFields = true,
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    int[] array;
    List<int> list;
    IEnumerable<int> enumerable;

    [Params(10, 100, 1000, 10000)]
    public int size;

    [Params(100)]
    public int iterations;

    [GlobalSetup]
    public void Setup()
    {
        array = System.Linq.Enumerable.Range(0, size).ToArray();
        list = new List<int>(array);
        enumerable = CreateEnumerable(array);
    }

    // A lazily evaluated iterator that is neither an array nor a list, so it does not
    // support indexed access or a cheaply known count.
    static IEnumerable<int> CreateEnumerable(int[] source)
    {
        foreach (var item in source)
        {
            yield return item;
        }
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeArray_ToStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeArray_ToStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
        }
    }

    #if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeArray_ToStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            var jsonBytes = SpanJson.JsonSerializer.Generic.Utf8.SerializeAsync(array, memoryStream);
        }
    }
#endif

    [Benchmark]
    public void SerializeList_ToStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, list);
        }
    }

    [Benchmark]
    public void SerializeList_ToStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, list, systemTextJsonSerializerSettings);
        }
    }

    #if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeList_ToStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            var jsonBytes = SpanJson.JsonSerializer.Generic.Utf8.SerializeAsync(list, memoryStream);
        }
    }
#endif

    [Benchmark]
    public void SerializeEnumerable_ToStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, enumerable);
        }
    }

    [Benchmark]
    public void SerializeEnumerable_ToStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, enumerable, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeEnumerable_ToStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            var jsonBytes = SpanJson.JsonSerializer.Generic.Utf8.SerializeAsync(enumerable, memoryStream);
        }
    }
#endif
}
