using BenchmarkDotNet.Attributes;
using FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance;
using FeatureLoom.Serialization;
using Newtonsoft.Json;
using SpanJson.Resolvers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(250)]
[MaxIterationCount(1000)]
public class DeserializeTypeProposalsTest
{
    private static readonly JsonDeserializer deserializerCheckAlways = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckAlways;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault;
        settings.populateExistingMembers = true;
        settings.useStringCache = true;
    });

    private static readonly JsonDeserializer deserializerCheckWhereReasonable = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.CheckWhereReasonable;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault;
        settings.populateExistingMembers = true;
        settings.useStringCache = true;
    });

    private static readonly JsonDeserializer deserializerPlain = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        settings.populateExistingMembers = true;
        settings.useStringCache = true;
    });

    private static readonly JsonDeserializer deserializerMultiOption = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore; // no $type in payload
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        settings.populateExistingMembers = true;
        settings.useStringCache = true;

        settings.ConfigureType<ProposalAnimal>(typeSettings =>
        {
            typeSettings.AddInstanceTypeMappingOption<ProposalDog>();
            typeSettings.AddInstanceTypeMappingOption<ProposalCat>();
        });
    });

    private static readonly JsonSerializerSettings newtonsoftProposalSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto
    };

    private static readonly JsonSerializerSettings newtonsoftPlainSettings = new JsonSerializerSettings();

    private static readonly System.Text.Json.JsonSerializerOptions stjOptions = new()
    {
        IncludeFields = true
    };

    public sealed class MyResolver<TSymbol> : ResolverBase<TSymbol, MyResolver<TSymbol>> where TSymbol : struct
    {
        public MyResolver() : base(new SpanJsonOptions { EnumOption = EnumOptions.Integer }) { }
    }

    private MemoryStream proposalStream = new(1024 * 1024);
    private MemoryStream plainStream = new(1024 * 1024);
    private MemoryStream multiOptionStream = new(1024 * 1024);

    [Params(-1000, -100, -10, -1)]
    public int iterations;

    [GlobalSetup]
    public void GlobalPrepare()
    {
        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        string dogType = Escape(typeof(ProposalDog).AssemblyQualifiedName!);
        string catType = Escape(typeof(ProposalCat).AssemblyQualifiedName!);

        // Realistic: mostly normal business fields, only a few polymorphic fields.
        string proposalJson =
            "{"
            + "\"Tenant\":\"north-eu\", "
            + "\"RequestId\":\"REQ-2026-05-14-7781\","
            + "\"Region\":\"DE\","
            + "\"Count\":3,"
            + "\"PrimaryPet\":{\"$type\":\"" + dogType + "\",\"Name\":\"Rex\",\"Age\":5,\"GoodBoy\":true,\"BarkLevel\":7},"
            + "\"Pets\":[{\"$type\":\"" + dogType + "\",\"Name\":\"Rex\",\"Age\":5,\"GoodBoy\":true,\"BarkLevel\":7},"
                + "{\"$type\":\"" + catType + "\",\"Name\":\"Mimi\",\"Age\":3,\"Indoor\":true,\"NapCount\":4},"
                + "{\"$type\":\"" + dogType + "\",\"Name\":\"Bolt\",\"Age\":2,\"GoodBoy\":true,\"BarkLevel\":4}],"
            + "\"Featured\":{\"$type\":\"" + catType + "\",\"Name\":\"Luna\",\"Age\":6,\"Indoor\":false,\"NapCount\":2}"
            + "}";

        // Equivalent non-polymorphic payload for all libraries
        string plainJson =
            "{"
            + "\"Tenant\":\"north-eu\","
            + "\"RequestId\":\"REQ-2026-05-14-7781\","
            + "\"Region\":\"DE\","
            + "\"Count\":3,"
            + "\"PrimaryPet\":{\"Kind\":\"Dog\",\"Name\":\"Rex\",\"Age\":5,\"GoodBoy\":true,\"BarkLevel\":7,\"Indoor\":false,\"NapCount\":0},"
            + "\"Pets\":[{\"Kind\":\"Dog\",\"Name\":\"Rex\",\"Age\":5,\"GoodBoy\":true,\"BarkLevel\":7,\"Indoor\":false,\"NapCount\":0},"
                + "{\"Kind\":\"Cat\",\"Name\":\"Mimi\",\"Age\":3,\"GoodBoy\":false,\"BarkLevel\":0,\"Indoor\":true,\"NapCount\":4},"
                + "{\"Kind\":\"Dog\",\"Name\":\"Bolt\",\"Age\":2,\"GoodBoy\":true,\"BarkLevel\":4,\"Indoor\":false,\"NapCount\":0}],"
            + "\"Featured\":{\"Kind\":\"Cat\",\"Name\":\"Luna\",\"Age\":6,\"GoodBoy\":false,\"BarkLevel\":0,\"Indoor\":false,\"NapCount\":2}"
            + "}";

        // Another variant without $type fields (same shape as proposalJson)
        string multiOptionJson =
            "{"
            + "\"Tenant\":\"north-eu\", "
            + "\"RequestId\":\"REQ-2026-05-14-7781\","
            + "\"Region\":\"DE\", "
            + "\"Count\":3,"
            + "\"PrimaryPet\":{\"Name\":\"Rex\",\"Age\":5,\"GoodBoy\":true,\"BarkLevel\":7},"
            + "\"Pets\":[{\"Name\":\"Rex\",\"Age\":5,\"GoodBoy\":true,\"BarkLevel\":7},"
                + "{\"Name\":\"Mimi\",\"Age\":3,\"Indoor\":true,\"NapCount\":4},"
                + "{\"Name\":\"Bolt\",\"Age\":2,\"GoodBoy\":true,\"BarkLevel\":4}],"
            + "\"Featured\":{\"Name\":\"Luna\",\"Age\":6,\"Indoor\":false,\"NapCount\":2}"
            + "}";

        proposalStream = new MemoryStream(Encoding.UTF8.GetBytes(proposalJson));
        plainStream = new MemoryStream(Encoding.UTF8.GetBytes(plainJson));
        multiOptionStream = new MemoryStream(Encoding.UTF8.GetBytes(multiOptionJson));
    }

    [IterationSetup]
    public void Prepare()
    {
        proposalStream.Position = 0;
        plainStream.Position = 0;
        multiOptionStream.Position = 0;
        iterations = Math.Abs(iterations);
    }

    // ---------- Proposal payload (applicable libs) ----------

    [Benchmark]
    public void Feature_CheckAlways_Proposals()
    {
        for (int i = 0; i < iterations; i++)
        {
            proposalStream.Position = 0;
            deserializerCheckAlways.TryDeserialize(proposalStream, out ProposalRoot _);
        }
    }

    [Benchmark]
    public void Feature_CheckWhereReasonable_Proposals()
    {
        for (int i = 0; i < iterations; i++)
        {
            proposalStream.Position = 0;
            deserializerCheckWhereReasonable.TryDeserialize(proposalStream, out ProposalRoot _);
        }
    }

    [Benchmark]
    public void Newtonsoft_TypeNameHandling_Proposals()
    {
        for (int i = 0; i < iterations; i++)
        {
            proposalStream.Position = 0;
            using var reader = new StreamReader(proposalStream, Encoding.UTF8, false, 1024, true);
            string json = reader.ReadToEnd();
            _ = JsonConvert.DeserializeObject<ProposalRoot>(json, newtonsoftProposalSettings);
        }
    }

    // ---------- Plain equivalent payload (fully comparable) ----------

    [Benchmark(Baseline = true)]
    public void Feature_PlainPayload()
    {
        for (int i = 0; i < iterations; i++)
        {
            plainStream.Position = 0;
            deserializerPlain.TryDeserialize(plainStream, out PlainRoot _);
        }
    }

    [Benchmark]
    public void Newtonsoft_PlainPayload()
    {
        for (int i = 0; i < iterations; i++)
        {
            plainStream.Position = 0;
            using var reader = new StreamReader(plainStream, Encoding.UTF8, false, 1024, true);
            string json = reader.ReadToEnd();
            _ = JsonConvert.DeserializeObject<PlainRoot>(json, newtonsoftPlainSettings);
        }
    }

    [Benchmark]
    public void SystemTextJson_PlainPayload()
    {
        for (int i = 0; i < iterations; i++)
        {
            plainStream.Position = 0;
            _ = System.Text.Json.JsonSerializer.Deserialize<PlainRoot>(plainStream, stjOptions);
        }
    }

    [Benchmark]
    public void SpanJson_PlainPayload()
    {
        for (int i = 0; i < iterations; i++)
        {
            plainStream.Position = 0;
            _ = SpanJson.JsonSerializer.Generic.Utf8.DeserializeAsync<PlainRoot, MyResolver<byte>>(plainStream).Result;
        }
    }

    // ---------- Multi-option deserializer (no $type in payload) ----------

    [Benchmark]
    public void Feature_MultiOption_NoTypeFields()
    {
        for (int i = 0; i < iterations; i++)
        {
            multiOptionStream.Position = 0;
            deserializerMultiOption.TryDeserialize(multiOptionStream, out ProposalRoot _);
        }
    }

    public class ProposalRoot
    {
        public string Tenant;
        public string RequestId;
        public string Region;
        public int Count;
        public ProposalAnimal PrimaryPet;
        public List<ProposalAnimal> Pets;
        public ProposalAnimal Featured;
    }

    public abstract class ProposalAnimal
    {
        public string Name;
        public int Age;
    }

    public class ProposalDog : ProposalAnimal
    {
        public bool GoodBoy;
        public int BarkLevel;
    }

    public class ProposalCat : ProposalAnimal
    {
        public bool Indoor;
        public int NapCount;
    }

    public class PlainRoot
    {
        public string Tenant;
        public string RequestId;
        public string Region;
        public int Count;
        public PlainAnimal PrimaryPet;
        public List<PlainAnimal> Pets;
        public PlainAnimal Featured;
    }

    public class PlainAnimal
    {
        public string Kind;
        public string Name;
        public int Age;
        public bool GoodBoy;
        public int BarkLevel;
        public bool Indoor;
        public int NapCount;
    }
}