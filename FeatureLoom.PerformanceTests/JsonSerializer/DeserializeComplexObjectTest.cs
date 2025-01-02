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
public partial class DeserializeComplexObjectTest
{
    FeatureJsonSerializer featureJsonSerializer = new FeatureJsonSerializer(new FeatureJsonSerializer.Settings()
    {
        indent = true,
    });

    FeatureJsonDeserializer featureJsonDeserializer = new FeatureJsonDeserializer(new FeatureJsonDeserializer.Settings()
    {
        initialBufferSize = 1024*1024*100,        
    });

    JsonSerializerOptions systemTextJsonSerializerSettings = new JsonSerializerOptions()
    {
        IncludeFields = true,
        //ReferenceHandler = ReferenceHandler.Preserve
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 100);
    ComplexObject complexObject = new ComplexObject();

    [Params(-10000, -1000, -100, -10, -1)]
    public int iterations;

    [GlobalSetup]
    public void GlobalPrepare()
    {
        featureJsonSerializer.Serialize(memoryStream, complexObject);
        string x = featureJsonSerializer.Serialize(complexObject);
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
        iterations = Math.Abs(iterations);
    }

    [Benchmark(Baseline = true)]
    public void DeserializeComplexObject_FromStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            ComplexObject result = System.Text.Json.JsonSerializer.Deserialize<ComplexObject>(memoryStream, systemTextJsonSerializerSettings);
        }
    }

    [Benchmark]
    public void DeserializeComplexObject_FromStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            featureJsonDeserializer.TryDeserialize(memoryStream, out ComplexObject result);
        }
    }
}
