using BenchmarkDotNet.Attributes;
using FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance;
using FeatureLoom.Serialization;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(1000)]
public class PopulateObjectTest
{
    private static readonly Serialization.JsonSerializer featureJsonSerializer = new(new Serialization.JsonSerializer.Settings
    {
        dataSelection = Serialization.JsonSerializer.DataSelection.PublicAndPrivateFields,
        typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo
    });

    private static readonly JsonDeserializer featurePopulateDeserializer = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        settings.populateExistingMembers = true;
        settings.useStringCache = true;
    });

    private static readonly JsonDeserializer featureDeserializeDeserializer = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        settings.populateExistingMembers = false;
        settings.useStringCache = true;
    });

    private static readonly JsonSerializerOptions stjPopulateOptions = new()
    {
        IncludeFields = true,
        PreferredObjectCreationHandling = System.Text.Json.Serialization.JsonObjectCreationHandling.Populate
    };

    private static readonly JsonSerializerOptions stjDeserializeOptions = new()
    {
        IncludeFields = true
    };

    private static readonly JsonSerializerSettings newtonsoftPopulateSettings = new()
    {
        ObjectCreationHandling = ObjectCreationHandling.Reuse
    };

    private static readonly JsonSerializerSettings newtonsoftDeserializeSettings = new();

    private MemoryStream memoryStream = new(1024 * 1024);
    private string json = string.Empty;

    private PopulateRoot featureTarget = new();
    private PopulateRoot stjTarget = new();
    private PopulateRoot newtonsoftTarget = new();

    [Params(-10000, -1000, -100, -10, -1)]
    public int iterations;

    [GlobalSetup]
    public void GlobalPrepare()
    {
        var source = new PopulateRoot
        {
            Id = 42,
            Name = "updated-name",
            Score = 12345.678,
            Version = 987654321,
            A = new PopulateChild { X = 11, Y = "A-child", Z = true },
            B = new PopulateChild { X = 22, Y = "B-child", Z = false }
        };

        json = featureJsonSerializer.Serialize(source);
        memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    [IterationSetup]
    public void Prepare()
    {
        iterations = Math.Abs(iterations);

        featureTarget = CreateInitialTarget();
        stjTarget = CreateInitialTarget();
        newtonsoftTarget = CreateInitialTarget();

        memoryStream.Position = 0;
    }

    private static PopulateRoot CreateInitialTarget() => new PopulateRoot
    {
        Id = -1,
        Name = "initial",
        Score = -1,
        Version = -1,
        A = new PopulateChild { X = -1, Y = "old-a", Z = false },
        B = new PopulateChild { X = -1, Y = "old-b", Z = true }
    };

    [Benchmark(Baseline = true)]
    public void PopulateObject_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            featurePopulateDeserializer.TryPopulate(memoryStream, featureTarget);
        }
    }

    [Benchmark]
    public void DeserializeObject_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            featureDeserializeDeserializer.TryDeserialize(memoryStream, out PopulateRoot _);
        }
    }

    [Benchmark]
    public void PopulateObject_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            _ = System.Text.Json.JsonSerializer.Deserialize(memoryStream, stjTarget.GetType(), stjPopulateOptions);
        }
    }

    [Benchmark]
    public void DeserializeObject_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            _ = System.Text.Json.JsonSerializer.Deserialize<PopulateRoot>(memoryStream, stjDeserializeOptions);
        }
    }

    [Benchmark]
    public void PopulateObject_Newtonsoft()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8, false, 1024, true);
            string text = reader.ReadToEnd();
            JsonConvert.PopulateObject(text, newtonsoftTarget, newtonsoftPopulateSettings);
        }
    }

    [Benchmark]
    public void DeserializeObject_Newtonsoft()
    {
        for (int i = 0; i < iterations; i++)
        {
            memoryStream.Position = 0;
            using var reader = new StreamReader(memoryStream, Encoding.UTF8, false, 1024, true);
            string text = reader.ReadToEnd();
            _ = JsonConvert.DeserializeObject<PopulateRoot>(text, newtonsoftDeserializeSettings);
        }
    }

    public class PopulateRoot
    {
        public int Id;
        public string Name = string.Empty;
        public double Score;
        public long Version;
        public PopulateChild A = new();
        public PopulateChild B = new();
    }

    public class PopulateChild
    {
        public int X;
        public string Y = string.Empty;
        public bool Z;
    }
}