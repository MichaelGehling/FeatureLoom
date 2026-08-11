using BenchmarkDotNet.Attributes;
using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System.IO;
using System.Text;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests.JsonSerializer;
[MinIterationCount(500)]
[MaxIterationCount(5000)]
[CPUUsageDiagnoser]
public class DeserializeEntryOverheadTest
{
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();
    const int Iterations = 1000;
    // Smallest possible payloads: parsing cost is near zero, so the entry path dominates.
    string json_Int = "0";
    string json_Bool = "true";
    string json_EmptyObject = "{}";
    ByteSegment bytes_Int;
    ByteSegment bytes_Bool;
    ByteSegment bytes_EmptyObject;
    MemoryStream stream_Int = new MemoryStream();
    MemoryStream stream_Bool = new MemoryStream();
    MemoryStream stream_EmptyObject = new MemoryStream();
    public class EmptyObject
    {
    }

    [GlobalSetup]
    public void Setup()
    {
        bytes_Int = new ByteSegment(Encoding.UTF8.GetBytes(json_Int));
        bytes_Bool = new ByteSegment(Encoding.UTF8.GetBytes(json_Bool));
        bytes_EmptyObject = new ByteSegment(Encoding.UTF8.GetBytes(json_EmptyObject));
        WriteToStream(stream_Int, json_Int);
        WriteToStream(stream_Bool, json_Bool);
        WriteToStream(stream_EmptyObject, json_EmptyObject);
    }

    private static void WriteToStream(MemoryStream stream, string json)
    {
        stream.SetLength(0);
        byte[] data = Encoding.UTF8.GetBytes(json);
        stream.Write(data, 0, data.Length);
        stream.Position = 0;
    }

    [IterationSetup]
    public void Prepare()
    {
        stream_Int.Position = 0;
        stream_Bool.Position = 0;
        stream_EmptyObject.Position = 0;
    }

    // ---- string source ----
    [Benchmark(Baseline = true, OperationsPerInvoke = Iterations)]
    public void EntryOverhead_String_Int()
    {
        for (int i = 0; i < Iterations; i++)
        {
            featureJsonDeserializer.TryDeserialize(json_Int, out int result);
        }
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public void EntryOverhead_String_Bool()
    {
        for (int i = 0; i < Iterations; i++)
        {
            featureJsonDeserializer.TryDeserialize(json_Bool, out bool result);
        }
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public void EntryOverhead_String_EmptyObject()
    {
        for (int i = 0; i < Iterations; i++)
        {
            featureJsonDeserializer.TryDeserialize(json_EmptyObject, out EmptyObject result);
        }
    }

    // ---- ByteSegment source ----
    [Benchmark(OperationsPerInvoke = Iterations)]
    public void EntryOverhead_Bytes_Int()
    {
        for (int i = 0; i < Iterations; i++)
        {
            featureJsonDeserializer.TryDeserialize(bytes_Int, out int result);
        }
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public void EntryOverhead_Bytes_Bool()
    {
        for (int i = 0; i < Iterations; i++)
        {
            featureJsonDeserializer.TryDeserialize(bytes_Bool, out bool result);
        }
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public void EntryOverhead_Bytes_EmptyObject()
    {
        for (int i = 0; i < Iterations; i++)
        {
            featureJsonDeserializer.TryDeserialize(bytes_EmptyObject, out EmptyObject result);
        }
    }

    // ---- Stream source ----
    [Benchmark(OperationsPerInvoke = Iterations)]
    public void EntryOverhead_Stream_Int()
    {
        for (int i = 0; i < Iterations; i++)
        {
            stream_Int.Position = 0;
            featureJsonDeserializer.TryDeserialize(stream_Int, out int result);
        }
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public void EntryOverhead_Stream_Bool()
    {
        for (int i = 0; i < Iterations; i++)
        {
            stream_Bool.Position = 0;
            featureJsonDeserializer.TryDeserialize(stream_Bool, out bool result);
        }
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public void EntryOverhead_Stream_EmptyObject()
    {
        for (int i = 0; i < Iterations; i++)
        {
            stream_EmptyObject.Position = 0;
            featureJsonDeserializer.TryDeserialize(stream_EmptyObject, out EmptyObject result);
        }
    }
}