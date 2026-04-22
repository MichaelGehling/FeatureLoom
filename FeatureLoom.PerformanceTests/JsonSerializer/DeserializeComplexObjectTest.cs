using BenchmarkDotNet.Attributes;
using FeatureLoom.Helpers;
using FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance;
using FeatureLoom.Serialization;
using FeatureLoom.Time;
using SpanJson.Resolvers;
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
    static Serialization.JsonSerializer featureJsonSerializer = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
    {
        //indent = true,
        dataSelection = Serialization.JsonSerializer.DataSelection.PublicAndPrivateFields,
        typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo
    });

    static JsonDeserializer featureJsonDeserializer = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties;
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
        //PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate,
        IgnoreReadOnlyFields = false,
        DefaultBufferSize = 1024*1024*10,
    };

    public sealed class MyResolver<TSymbol> : SpanJson.Resolvers.ResolverBase<TSymbol, MyResolver<TSymbol>> where TSymbol : struct
    {
        public MyResolver() : base(new SpanJsonOptions
        {
            EnumOption = EnumOptions.Integer,            
        })
        {
        }
    }

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
        ConsoleHelper.WriteLine(x);
        //AppTime.Wait(1.Seconds());
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

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeComplexObject_FromStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            var result = SpanJson.JsonSerializer.Generic.Utf8
                .DeserializeAsync<ComplexObject, MyResolver<byte>>(memoryStream)
                .Result;
        }
    }
#endif
}
