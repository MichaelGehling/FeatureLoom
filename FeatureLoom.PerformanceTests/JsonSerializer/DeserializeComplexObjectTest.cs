using BenchmarkDotNet.Attributes;
using FeatureLoom.Helpers;
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
[MinIterationCount(1000)]
[MaxIterationCount(5000)]
public partial class DeserializeComplexObjectTest
{
    static FeatureJsonSerializer featureJsonSerializer = new FeatureJsonSerializer(new FeatureJsonSerializer.Settings()
    {
        //indent = true,
        dataSelection = FeatureJsonSerializer.DataSelection.PublicFieldsAndProperties,
        typeInfoHandling = FeatureJsonSerializer.TypeInfoHandling.AddNoTypeInfo
    });

    static FeatureJsonDeserializer featureJsonDeserializer = new FeatureJsonDeserializer(new FeatureJsonDeserializer.Settings()
    {
        initialBufferSize = 1024*1024*10,        
        dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties, 
        //enableProposedTypes = true,
        //enableReferenceResolution = true
    });

    static FeatureJsonDeserializer featureJsonDeserializer2 = new FeatureJsonDeserializer(new FeatureJsonDeserializer.Settings()
    {
        initialBufferSize = 1024 * 1024 * 10,
        dataAccess = FeatureJsonDeserializer.DataAccess.PublicFieldsAndProperties,
        //enableProposedTypes = true,
        //enableReferenceResolution = true
        useStringCache = true,
        stringCacheBitSize = 12,        
    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = new JsonSerializerOptions()
    {
        IncludeFields = true,        
        DefaultBufferSize = 1024*1024*10,                
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);
    ComplexObject complexObject = new ComplexObject();

    [Params(-10000, -1000, -100, -10, -1)]
    public int iterations;

    [GlobalSetup]
    public void GlobalPrepare()
    {
        /*for (int i = 0; i < 10; i++)
        {
            complexObject.id = i;
            complexObject.myInt = RandomGenerator.Int32();
            complexObject.myString = RandomGenerator.String(20, false);
            complexObject.myString2 = RandomGenerator.String(40, false);
            complexObject.myString3 = RandomGenerator.String(80, false);
            complexObject.myString4 = RandomGenerator.String(160, false);
            featureJsonSerializer.Serialize(memoryStream, complexObject);
            memoryStream.WriteByte((byte)'\n');
        }*/
        featureJsonSerializer.Serialize(memoryStream, complexObject);
        string x = featureJsonSerializer.Serialize(complexObject);
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
        iterations = Math.Abs(iterations);
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

    [Benchmark]
    public void DeserializeComplexObject_FromStream_Feature2()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            featureJsonDeserializer2.TryDeserialize(memoryStream, out ComplexObject result);
        }
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


}
