using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests;
[CPUUsageDiagnoser]
public class DeserializeObjectKeyParserBenchmark
{
    const int Iterations = 100;
    const string Json = "{\"1\":1,\"2\":2,\"3\":3,\"4\":4,\"5\":5,\"6\":6,\"7\":7,\"8\":8,\"9\":9,\"10\":10}";
    readonly JsonDeserializer deserializer = new JsonDeserializer(settings => settings.ConfigureType<Dictionary<int, int>>(typeSettings => typeSettings.ConfigureObjectKey<int>(key => int.Parse(key.AsString()))));
    [Benchmark(Baseline = true, OperationsPerInvoke = Iterations)]
    public void ParseObjectKeys()
    {
        for (int i = 0; i < Iterations; i++)
        {
            deserializer.TryDeserialize(Json, out Dictionary<int, int> result);
        }
    }
}