using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests.JsonSerializer;
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
[CPUUsageDiagnoser]
public class SerializeDictionaryValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();
    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 100);
    /// <summary>
    /// Number of entries per dictionary. The array case multiplies this by the array size,
    /// so it is kept small to stay comparable in total work to the other benchmarks.
    /// </summary>
    const int EntryCount = 20;
    /// <summary>Array size, reduced by the entry count so the total work stays comparable.</summary>
    const int DictionaryArraySize = BenchmarkSettings.ArraySize / EntryCount;
    public enum DictionaryCase
    {
        StringToString,
        StringToInt,
        IntToString,
        StringToObject,
    }

    [ParamsAllValues]
    public DictionaryCase dictionaryCase;
    // The concrete dictionary type differs per case, so the serialize calls are resolved
    // once during setup. This keeps the generic type known to each serializer while
    // avoiding any per-iteration branching inside the measured loops.
    Action serializeSingleFeature;
    Action serializeSingleSystemText;
    Action serializeArrayFeature;
    Action serializeArraySystemText;
#if NET6_0_OR_GREATER
    Action serializeSingleSpanJson;
    Action serializeArraySpanJson;
#endif
    [GlobalSetup]
    public void Setup()
    {
        switch (dictionaryCase)
        {
            case DictionaryCase.StringToString:
                Bind(CreateStringToString, i => "key" + i, i => "value" + i);
                break;
            case DictionaryCase.StringToInt:
                Bind(CreateStringToInt, i => "key" + i, i => i);
                break;
            case DictionaryCase.IntToString:
                Bind(CreateIntToString, i => i, i => "value" + i);
                break;
            case DictionaryCase.StringToObject:
                Bind(CreateStringToObject, i => "key" + i, i => new SimpleObject { id = i });
                break;
        }
    }

    // Builds the single and array payloads for one case and binds the serialize calls,
    // so every serializer sees the concrete Dictionary<K, V> type.
    void Bind<K, V>(Func<int, Dictionary<K, V>> create, Func<int, K> keySelector, Func<int, V> valueSelector)
    {
        Dictionary<K, V> single = create(0);
        Dictionary<K, V>[] array = new Dictionary<K, V>[DictionaryArraySize];
        for (int i = 0; i < array.Length; i++)
            array[i] = create(i);
        serializeSingleFeature = () => featureJsonSerializer.Serialize(memoryStream, single);
        serializeSingleSystemText = () => System.Text.Json.JsonSerializer.Serialize(memoryStream, single, systemTextJsonSerializerSettings);
        serializeArrayFeature = () => featureJsonSerializer.Serialize(memoryStream, array);
        serializeArraySystemText = () => System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
#if NET6_0_OR_GREATER
        serializeSingleSpanJson = () => SerializerConfigs.SerializeWithSpanJson(single, memoryStream);
        serializeArraySpanJson = () => SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
#endif
        SampleOutput.Collect($"Dictionary({dictionaryCase})", single, featureJsonSerializer, systemTextJsonSerializerSettings, maxLength: 200);
    }

    static Dictionary<string, string> CreateStringToString(int seed)
    {
        var dict = new Dictionary<string, string>(EntryCount);
        for (int i = 0; i < EntryCount; i++)
            dict["key" + (seed + i)] = "value" + (seed + i);
        return dict;
    }

    static Dictionary<string, int> CreateStringToInt(int seed)
    {
        var dict = new Dictionary<string, int>(EntryCount);
        for (int i = 0; i < EntryCount; i++)
            dict["key" + (seed + i)] = seed + i;
        return dict;
    }

    static Dictionary<int, string> CreateIntToString(int seed)
    {
        var dict = new Dictionary<int, string>(EntryCount);
        for (int i = 0; i < EntryCount; i++)
            dict[seed + i] = "value" + (seed + i);
        return dict;
    }

    static Dictionary<string, SimpleObject> CreateStringToObject(int seed)
    {
        var dict = new Dictionary<string, SimpleObject>(EntryCount);
        for (int i = 0; i < EntryCount; i++)
            dict["key" + (seed + i)] = new SimpleObject
            {
                id = seed + i
            };
        return dict;
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeDictionary_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            serializeSingleFeature();
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeDictionary_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            serializeSingleSystemText();
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeDictionary_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            // SpanJson only offers an async stream API. The MemoryStream completes synchronously,
            // so blocking here adds no measurable overhead but ensures the write actually happened.
            serializeSingleSpanJson();
        }
    }
#endif
    [Benchmark]
    public void SerializeDictionary_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            serializeArrayFeature();
        }
    }

    [Benchmark]
    public void SerializeDictionary_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            serializeArraySystemText();
        }
    }
#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeDictionary_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            serializeArraySpanJson();
        }
    }
#endif
}