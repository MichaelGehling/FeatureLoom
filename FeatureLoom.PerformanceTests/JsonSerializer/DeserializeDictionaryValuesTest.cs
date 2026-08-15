using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance of different dictionary shapes, mirroring
/// <see cref="SerializeDictionaryValuesTest"/>.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeDictionaryValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();

    /// <summary>
    /// Second deserializer instance with the string cache / deduplication feature disabled,
    /// so its effect on dictionary keys and values can be measured separately.
    /// </summary>
    static JsonDeserializer featureJsonDeserializer_NoStringCache = new JsonDeserializer(settings =>
    {
        settings.useStringCache = false;
    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    const int EntryCount = 20;
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

    MemoryStream singleStream = new MemoryStream();
    MemoryStream arrayStream = new MemoryStream();

    Action deserializeSingleFeature;
    Action deserializeSingleFeatureNoStringCache;
    Action deserializeSingleSystemText;
    Action deserializeArrayFeature;
    Action deserializeArrayFeatureNoStringCache;
    Action deserializeArraySystemText;
#if NET6_0_OR_GREATER
    Action deserializeSingleSpanJson;
    Action deserializeArraySpanJson;
#endif

    [GlobalSetup]
    public void Setup()
    {
        switch (dictionaryCase)
        {
            case DictionaryCase.StringToString:
                Bind(CreateStringToString);
                break;
            case DictionaryCase.StringToInt:
                Bind(CreateStringToInt);
                break;
            case DictionaryCase.IntToString:
                Bind(CreateIntToString);
                break;
            case DictionaryCase.StringToObject:
                Bind(CreateStringToObject);
                break;
        }
    }

    void Bind<K, V>(Func<int, Dictionary<K, V>> create)
    {
        Dictionary<K, V> single = create(0);
        Dictionary<K, V>[] array = new Dictionary<K, V>[DictionaryArraySize];
        for (int i = 0; i < array.Length; i++)
            array[i] = create(i);

        featureJsonSerializer.Serialize(singleStream, single);
        featureJsonSerializer.Serialize(arrayStream, array);

        deserializeSingleFeature = () => featureJsonDeserializer.TryDeserialize(singleStream, out Dictionary<K, V> _);
        deserializeSingleFeatureNoStringCache = () => featureJsonDeserializer_NoStringCache.TryDeserialize(singleStream, out Dictionary<K, V> _);
        deserializeSingleSystemText = () => System.Text.Json.JsonSerializer.Deserialize<Dictionary<K, V>>(singleStream, systemTextJsonSerializerSettings);
        deserializeArrayFeature = () => featureJsonDeserializer.TryDeserialize(arrayStream, out Dictionary<K, V>[] _);
        deserializeArrayFeatureNoStringCache = () => featureJsonDeserializer_NoStringCache.TryDeserialize(arrayStream, out Dictionary<K, V>[] _);
        deserializeArraySystemText = () => System.Text.Json.JsonSerializer.Deserialize<Dictionary<K, V>[]>(arrayStream, systemTextJsonSerializerSettings);
#if NET6_0_OR_GREATER
        deserializeSingleSpanJson = () => SerializerConfigs.DeserializeWithSpanJson<Dictionary<K, V>>(singleStream);
        deserializeArraySpanJson = () => SerializerConfigs.DeserializeWithSpanJson<Dictionary<K, V>[]>(arrayStream);
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
        singleStream.Position = 0;
        arrayStream.Position = 0;
    }

    [Benchmark]
    public void DeserializeDictionary_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            singleStream.Position = 0;
            deserializeSingleFeature();
        }
    }

    [Benchmark]
    public void DeserializeDictionary_Single_Feature_NoStringCache()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            singleStream.Position = 0;
            deserializeSingleFeatureNoStringCache();
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeDictionary_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            singleStream.Position = 0;
            deserializeSingleSystemText();
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeDictionary_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            singleStream.Position = 0;
            deserializeSingleSpanJson();
        }
    }
#endif

    [Benchmark]
    public void DeserializeDictionary_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            arrayStream.Position = 0;
            deserializeArrayFeature();
        }
    }

    [Benchmark]
    public void DeserializeDictionary_Array_Feature_NoStringCache()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            arrayStream.Position = 0;
            deserializeArrayFeatureNoStringCache();
        }
    }

    [Benchmark]
    public void DeserializeDictionary_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            arrayStream.Position = 0;
            deserializeArraySystemText();
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeDictionary_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            arrayStream.Position = 0;
            deserializeArraySpanJson();
        }
    }
#endif
}
