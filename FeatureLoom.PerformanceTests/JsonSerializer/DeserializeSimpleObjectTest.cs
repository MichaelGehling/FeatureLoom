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
public partial class DeserializeSimpleObjectTest
{
    static Serialization.JsonSerializer featureJsonSerializer = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
    {
        //indent = true,
    });

    static JsonDeserializer featureJsonDeserializer = new JsonDeserializer(new JsonDeserializer.Settings()
    {
        initialBufferSize = 1024 * 1024 * 10,
        dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties,
        proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore,
        //enableReferenceResolution = false
    });

    static JsonDeserializer featureJsonDeserializer2 = new JsonDeserializer(new JsonDeserializer.Settings()
    {
        initialBufferSize = 1024 * 1024 * 10,
        dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties,
        //proposedTypeHandling = FeatureJsonDeserializer.Settings.ProposedTypeHandling.Ignore,
        proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckWhereReasonable,
        //enableReferenceResolution = true,
        useStringCache = true,
        //populateExistingMembers = false,
    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = new JsonSerializerOptions()
    {
        IncludeFields = true,
        //ReferenceHandler = ReferenceHandler.Preserve
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);
    SimpleObject obj = new SimpleObject();

    [Params(-10000, -1000, -100, -10, -1)]
    public int iterations;

    [GlobalSetup]
    public void GlobalPrepare()
    {
        featureJsonSerializer.Serialize(memoryStream, obj);
        string x = featureJsonSerializer.Serialize(obj);
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
        iterations = Math.Abs(iterations);
    }

    [Benchmark]
    public void DeserializeSimpleObject_FromStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            featureJsonDeserializer.TryDeserialize(memoryStream, out SimpleObject result);
        }
    }

    [Benchmark]
    public void DeserializeSimpleObject_FromStream_Feature2()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            featureJsonDeserializer2.TryDeserialize(memoryStream, out SimpleObject result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeSimpleObject_FromStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            var result = System.Text.Json.JsonSerializer.Deserialize<SimpleObject>(memoryStream, systemTextJsonSerializerSettings);
        }
    }
#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeSimpleObject_FromStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            var result = SpanJson.JsonSerializer.Generic.Utf8.DeserializeAsync<SimpleObject>(memoryStream).Result;
        }
    }
#endif
}
