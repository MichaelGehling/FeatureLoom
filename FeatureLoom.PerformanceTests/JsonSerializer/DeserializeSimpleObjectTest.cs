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
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public partial class DeserializeSimpleObjectTest
{
    static Serialization.JsonSerializer featureJsonSerializer = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
    {
        //indent = true,
    });

    static JsonDeserializer featureJsonDeserializer = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        //settings.strict = false;
        //settings.proposedTypeHandling = FeatureJsonDeserializer.Settings.ProposedTypeHandling.CheckWhereReasonable;
        // settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.OnlyPerType;        
        //populateExistingMembers = false,        
        settings.useStringCache = true;
        settings.stringCacheBitSize = 16;
        /*settings.ConfigureType<SimpleObject>(typeSettings =>
        {
            typeSettings.ConfigureMember<string>("name", memberSettings =>
            {
                memberSettings.SetUseStringCache(true);                   
            });
        });*/
    });

    static JsonDeserializer featureJsonDeserializer2 = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        //settings.strict = true;
        //settings.proposedTypeHandling = FeatureJsonDeserializer.Settings.ProposedTypeHandling.CheckWhereReasonable;
        //settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        //settings.useStringCache = true,
        //settings.populateExistingMembers = false;
        settings.useStringCache = false;        
    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = new JsonSerializerOptions()
    {
        IncludeFields = true,
        //ReferenceHandler = ReferenceHandler.Preserve
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);
    SimpleObject obj = new SimpleObject();
    
    const int numStreams = 1000;
    static List<MemoryStream> streams = new List<MemoryStream>();

    [Params(-10000, -1000, -100, -10, -1)]
    public int iterations;

    [GlobalSetup]
    public void GlobalPrepare()
    {
        if (streams.Count == 0)
        {
            for (int i = 0; i < numStreams; i++)
            {
                var stream = new MemoryStream();
                obj.name = RandomGenerator.String(50);
                featureJsonSerializer.Serialize(stream, obj);
                streams.Add(stream);
            }
        }

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
            var stream = streams[i % numStreams];
            stream.Position = 0;
            featureJsonDeserializer.TryDeserialize(stream, out SimpleObject result);
        }
    }

    [Benchmark]
    public void DeserializeSimpleObject_FromStream_Feature2()
    {
        for (int i = 0; i < iterations; i++)
        {
            var stream = streams[i % numStreams];
            stream.Position = 0;
            featureJsonDeserializer2.TryDeserialize(stream, out SimpleObject result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeSimpleObject_FromStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            var stream = streams[i % numStreams];
            stream.Position = 0;
            var result = System.Text.Json.JsonSerializer.Deserialize<SimpleObject>(stream, systemTextJsonSerializerSettings);
        }
    }
#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeSimpleObject_FromStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            var stream = streams[i % numStreams];
            stream.Position = 0;
            var result = SpanJson.JsonSerializer.Generic.Utf8.DeserializeAsync<SimpleObject>(stream).Result;
        }
    }
#endif
}
