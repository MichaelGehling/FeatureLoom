using BenchmarkDotNet.Attributes;
using FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance;
using FeatureLoom.Serialization;
using Newtonsoft.Json;
using SpanJson.Resolvers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(250)]
[MaxIterationCount(1000)]
public class DeserializeMixedStringDatasetTest
{
    private static readonly Serialization.JsonSerializer featureJsonSerializer = new(new Serialization.JsonSerializer.Settings
    {
        dataSelection = Serialization.JsonSerializer.DataSelection.PublicAndPrivateFields,
        typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo
    });

    private static readonly JsonDeserializer featureWithStringCache = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.useStringCache = true;
        settings.stringCacheBitSize = 14;
        settings.stringCacheMaxLength = 128;
    });

    private static readonly JsonDeserializer featureWithoutStringCache = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.useStringCache = false;
    });

    private static readonly JsonSerializerOptions stjOptions = new()
    {
        IncludeFields = true
    };

    private static readonly Newtonsoft.Json.JsonSerializer newtonsoftSerializer = Newtonsoft.Json.JsonSerializer.CreateDefault();

    public sealed class MyResolver<TSymbol> : ResolverBase<TSymbol, MyResolver<TSymbol>> where TSymbol : struct
    {
        public MyResolver() : base(new SpanJsonOptions { EnumOption = EnumOptions.Integer }) { }
    }

    private const int DatasetSize = 1024;
    private readonly List<MemoryStream> streams = new(DatasetSize);

    [Params(-10000, -1000, -100, -10, -1)]
    public int iterations;

    [GlobalSetup]
    public void GlobalPrepare()
    {
        if (streams.Count > 0) return;

        string[] recurringCountries = { "DE", "US", "FR", "SE", "JP" };
        string[] recurringRegions = { "north", "south", "west", "east" };
        string[] recurringStatus = { "Open", "Closed", "InProgress", "OnHold" };
        string[] recurringTags = { "vip", "priority", "export", "internal", "b2b" };

        for (int i = 0; i < DatasetSize; i++)
        {
            var item = new MixedStringRecord
            {
                Id = i,
                Country = recurringCountries[i % recurringCountries.Length],
                Region = recurringRegions[(i / 2) % recurringRegions.Length],
                Status = recurringStatus[(i / 3) % recurringStatus.Length],
                Category = "CAT-" + (i % 20),
                UserName = "user_" + i,
                SessionId = Guid.NewGuid().ToString("N"),
                Description = "desc_" + i + "_"
                    + recurringCountries[i % recurringCountries.Length] + "_"
                    + recurringStatus[(i / 3) % recurringStatus.Length],
                Tags = new List<string>
                {
                    recurringTags[i % recurringTags.Length],
                    recurringTags[(i + 1) % recurringTags.Length],
                    "txn_" + i
                }
            };

            var ms = new MemoryStream(512);
            featureJsonSerializer.Serialize(ms, item);
            streams.Add(ms);
        }
    }

    [IterationSetup]
    public void Prepare()
    {
        iterations = Math.Abs(iterations);
    }

    [Benchmark(Baseline = true)]
    public void DeserializeMixedStrings_WithStringCache()
    {
        for (int i = 0; i < iterations; i++)
        {
            var stream = streams[i % DatasetSize];
            stream.Position = 0;
            featureWithStringCache.TryDeserialize(stream, out MixedStringRecord _);
        }
    }

    [Benchmark]
    public void DeserializeMixedStrings_WithoutStringCache()
    {
        for (int i = 0; i < iterations; i++)
        {
            var stream = streams[i % DatasetSize];
            stream.Position = 0;
            featureWithoutStringCache.TryDeserialize(stream, out MixedStringRecord _);
        }
    }

    [Benchmark]
    public void DeserializeMixedStrings_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            var stream = streams[i % DatasetSize];
            stream.Position = 0;
            _ = System.Text.Json.JsonSerializer.Deserialize<MixedStringRecord>(stream, stjOptions);
        }
    }

    [Benchmark]
    public void DeserializeMixedStrings_Newtonsoft()
    {
        for (int i = 0; i < iterations; i++)
        {
            var stream = streams[i % DatasetSize];
            stream.Position = 0;
            using var reader = new StreamReader(stream, leaveOpen: true);
            using var jsonReader = new JsonTextReader(reader);
            _ = newtonsoftSerializer.Deserialize<MixedStringRecord>(jsonReader);
        }
    }

    [Benchmark]
    public void DeserializeMixedStrings_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            var stream = streams[i % DatasetSize];
            stream.Position = 0;
            _ = SpanJson.JsonSerializer.Generic.Utf8.DeserializeAsync<MixedStringRecord, MyResolver<byte>>(stream).Result;
        }
    }

    public class MixedStringRecord
    {
        public int Id;
        public string Country;
        public string Region;
        public string Status;
        public string Category;
        public string UserName;
        public string SessionId;
        public string Description;
        public List<string> Tags;
    }
}