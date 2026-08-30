using BenchmarkDotNet.Attributes;
using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests;
[CPUUsageDiagnoser]
public class DeserializeObjectKeyBytesParserBenchmark
{
    const int Iterations = 100;
    const string Json = "{\"1\":1,\"2\":2,\"3\":3,\"4\":4,\"5\":5,\"6\":6,\"7\":7,\"8\":8,\"9\":9,\"10\":10}";
    readonly JsonDeserializer deserializer = new JsonDeserializer(settings =>
        settings.ConfigureType<Dictionary<int, int>>(typeSettings =>
            typeSettings.ConfigureObjectKey<int>(key => ParseInt(key.Bytes))));
    static int ParseInt(ByteSegment bytes)
    {
        int result = 0;
        for (int i = 0; i < bytes.Count; i++)
            result = result * 10 + bytes[i] - (byte)'0';
        return result;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Iterations)]
    public void ParseObjectKeyBytes()
    {
        for (int i = 0; i < Iterations; i++)
        {
            deserializer.TryDeserialize(Json, out Dictionary<int, int> result);
        }
    }
}