using BenchmarkDotNet.Attributes;
using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System;
using System.Text;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests.JsonSerializer;
[CPUUsageDiagnoser]
public class DeserializeDateTimeFractionDigitsBenchmark
{
    private const int Iterations = 50000;
    private static readonly JsonDeserializer Deserializer = SerializerConfigs.CreateFeatureDeserializer();
    private ByteSegment json;
    [Params(1, 3, 6, 7)]
    public int FractionDigits { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string fraction = FractionDigits switch
        {
            1 => "1",
            3 => "123",
            6 => "123456",
            7 => "1234567",
            _ => throw new ArgumentOutOfRangeException()};
        json = new ByteSegment(Encoding.UTF8.GetBytes($"\"2024-01-02T03:04:05.{fraction}Z\""));
    }

    [Benchmark(OperationsPerInvoke = Iterations)]
    public DateTime DeserializeFractionalDateTime()
    {
        DateTime result = default;
        for (int i = 0; i < Iterations; i++)
            Deserializer.TryDeserialize(json, out result);
        return result;
    }
}