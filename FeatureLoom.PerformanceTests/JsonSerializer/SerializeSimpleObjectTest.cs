using BenchmarkDotNet.Attributes;
using FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance;
using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.JsonSerializer;


[MemoryDiagnoser]
[CsvMeasurementsExporter]    
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeSimpleObjectTest
{
    FeatureJsonSerializer featureJsonSerializer = new FeatureJsonSerializer(new FeatureJsonSerializer.Settings()
    {

    });

    JsonSerializerOptions systemTextJsonSerializerSettings = new JsonSerializerOptions()
    {
        IncludeFields = true,
        //ReferenceHandler = ReferenceHandler.Preserve
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 100);
    SimpleObject obj = new SimpleObject();

    [Params(-10000, -1000, -100, -10, -1)]
    public int iterations;

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
        iterations = Math.Abs(iterations);
    }

    [Benchmark(Baseline = true)]
    public void SerializeSimpleObject_ToStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, obj, systemTextJsonSerializerSettings);
        }
    }

    [Benchmark]
    public void SerializeSimpleObject_ToStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, obj);
        }
    }


    /*  [Benchmark]
      public void SerializeSimpleObject_ToString_SystemText()
      {
          string json = System.Text.Json.JsonSerializer.Serialize(simpleObject, systemTextJsonSerializerSettings);
      }

      [Benchmark]
      public void SerializeSimpleObject_ToString_Feature()
      {
          string json = featureJsonSerializer.Serialize(simpleObject);
      }
      */
}
