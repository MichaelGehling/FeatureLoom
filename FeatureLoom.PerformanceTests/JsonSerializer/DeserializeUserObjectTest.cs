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
public partial class DeserializeUserObjectTest
{
    static Serialization.JsonSerializer featureJsonSerializer = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
    {
        //indent = true,
        dataSelection = Serialization.JsonSerializer.DataSelection.PublicFieldsAndProperties,
        typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo
    });

    static JsonDeserializer featureJsonDeserializer = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        //settings.strict = false;
        //settings.proposedTypeHandling = FeatureJsonDeserializer.Settings.ProposedTypeHandling.CheckWhereReasonable;
        // settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.OnlyPerType;
        //settings.useStringCache = true,
        //populateExistingMembers = false,        
        settings.useStringCache = true;
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
        DefaultBufferSize = 1024*1024*10,                
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);
    UserObject userObject = new UserObject();

    [Params(-20000, -200)]
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
        featureJsonSerializer.Serialize(memoryStream, userObject);
        string x = featureJsonSerializer.Serialize(userObject);
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
        iterations = Math.Abs(iterations);
    }

    [Benchmark]
    public void DeserializeUserObject_FromStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            featureJsonDeserializer.TryDeserialize(memoryStream, out UserObject result);
        }
    }

    [Benchmark]
    public void DeserializeUserObject_FromStream_Feature2()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            featureJsonDeserializer2.TryDeserialize(memoryStream, out UserObject  result);
        }
    }


    [Benchmark(Baseline = true)]
    public void DeserializeUserObject_FromStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            UserObject result = System.Text.Json.JsonSerializer.Deserialize<UserObject>(memoryStream, systemTextJsonSerializerSettings);
        }
    }
#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeUserObject_FromStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            var result = SpanJson.JsonSerializer.Generic.Utf8.DeserializeAsync<UserObject>(memoryStream).Result;
        }
    }
#endif
}
