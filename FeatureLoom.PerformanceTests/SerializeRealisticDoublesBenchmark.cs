using System;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using FeatureJsonSerializer = FeatureLoom.Serialization.JsonSerializer;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.Benchmarks
{
    [CPUUsageDiagnoser]
    public class SerializeRealisticDoublesBenchmark
    {
        private const int Count = 10000;
        private FeatureJsonSerializer serializer;
        private JsonSerializerOptions systemTextOptions;
        private MemoryStream stream;
        private double[] values;
        [GlobalSetup]
        public void Setup()
        {
            serializer = new FeatureJsonSerializer(new FeatureJsonSerializer.Settings() { typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo, enumAsString = true, });
            systemTextOptions = new JsonSerializerOptions()
            {
                IncludeFields = true
            };
            stream = new MemoryStream(1024 * 1024 * 4);
            var r = new Random(20240102);
            values = new double[Count];
            for (int i = 0; i < values.Length; i++)
            {
                switch (i % 8)
                {
                    case 0:
                        values[i] = Math.Round(r.NextDouble() * 100, 2);
                        break; // price
                    case 1:
                        values[i] = Math.Round(r.NextDouble() * 100, 1);
                        break; // percent
                    case 2:
                        values[i] = Math.Round(r.NextDouble() * 100 - 50, 1);
                        break; // sensor
                    case 3:
                        values[i] = Math.Round(r.NextDouble() * 360 - 180, 6);
                        break; // coordinate
                    case 4:
                        values[i] = Math.Round(r.NextDouble() * 1000, 3);
                        break; // measurement
                    case 5:
                        values[i] = Math.Round(r.NextDouble() * 10000, 2);
                        break; // amount
                    case 6:
                        values[i] = r.Next(0, 1000000);
                        break; // integral
                    default:
                        values[i] = r.Next(0, 1000);
                        break; // small integral
                }
            }
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = Count)]
        public void RealisticDoubles_Feature()
        {
            stream.Position = 0;
            serializer.Serialize(stream, values);
        }

        [Benchmark(OperationsPerInvoke = Count)]
        public void RealisticDoubles_SystemText()
        {
            stream.Position = 0;
            System.Text.Json.JsonSerializer.Serialize(stream, values, systemTextOptions);
        }
    }
}