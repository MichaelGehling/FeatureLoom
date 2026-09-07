using BenchmarkDotNet.Attributes;
using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

[MemoryDiagnoser]
public class DeserializeStringCollectionsBenchmark
{
    private const int ItemCount = 1000;
    private static readonly JsonDeserializer CachedDeserializer = SerializerConfigs.CreateFeatureDeserializer();
    private static readonly JsonDeserializer UncachedDeserializer = new(new JsonDeserializer.Settings { useStringCache = false });
    private ByteSegment json;
    [GlobalSetup]
    public void Setup()
    {
        var values = new string[ItemCount];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (i % 10) switch
            {
                0 => $"item-{i:D4}-line1\nline2",
                1 => $"item-{i:D4}-quoted-\"value\"",
                2 => $"item-{i:D4}-unicode-äöü",
                _ => $"item-{i:D4}-ordinary-text"
            };
        }

        var serializer = SerializerConfigs.CreateFeatureSerializer();
        json = new ByteSegment(Encoding.UTF8.GetBytes(serializer.Serialize(values)));
    }

    [Benchmark]
    public string[] Array_WithStringCache()
    {
        CachedDeserializer.TryDeserialize(json, out string[] result);
        return result;
    }

    [Benchmark]
    public string[] Array_WithoutStringCache()
    {
        UncachedDeserializer.TryDeserialize(json, out string[] result);
        return result;
    }

    [Benchmark]
    public List<string> List_WithStringCache()
    {
        CachedDeserializer.TryDeserialize(json, out List<string> result);
        return result;
    }

    [Benchmark]
    public List<string> List_WithoutStringCache()
    {
        UncachedDeserializer.TryDeserialize(json, out List<string> result);
        return result;
    }
}