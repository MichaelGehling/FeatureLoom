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

/// <summary>
/// Shows the effect of per-member string-cache configuration on a realistic mixed payload.
/// <para>
/// Each record carries 6 low-cardinality strings (country, region, status, category, 2 tags) and
/// 4 that are unique per record (user name, session id, description, transaction tag). A global
/// string cache helps the first group and actively hurts the second, because unique values can
/// never hit yet still pay hash + probe + insert and evict entries that would have hit.
/// </para>
/// <para>
/// The three FeatureLoom variants isolate that trade-off: cache everything, cache nothing, or
/// cache only the fields where it pays off.
/// </para>
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(50)]
[MaxIterationCount(200)]
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
        settings.initialBufferSize = 1024 * 1024 * 10;        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.useStringCache = false;
    });

    // Selective caching: the dataset has 6 low-cardinality strings per record (Country 5,
    // Region 4, Status 4, Category 20, Tags[0..1] 5 each) and 4 that are unique per record
    // (UserName, SessionId, Description, Tags[2]). The unique ones can never hit the cache but
    // still pay hash+probe+insert and evict useful entries, so caching is disabled just for them.
    private static readonly JsonDeserializer featureWithSelectiveStringCache = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.useStringCache = true;
        settings.stringCacheBitSize = 14;
        settings.stringCacheMaxLength = 128;

        settings.ConfigureType<MixedStringRecord>(ts =>
        {
            ts.ConfigureMember<string>(nameof(MixedStringRecord.UserName), ms => ms.SetUseStringCache(false));
            ts.ConfigureMember<string>(nameof(MixedStringRecord.SessionId), ms => ms.SetUseStringCache(false));
            ts.ConfigureMember<string>(nameof(MixedStringRecord.Description), ms => ms.SetUseStringCache(false));
        });
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

    // Each record is deserialized exactly once per invocation. This matters: if the dataset were
    // replayed, the per-record unique fields would repeat and the string cache could dedupe them,
    // which flatters the fully-cached variant in a way real payloads never would.
    private const int DatasetSize = 10000;
    private readonly List<MemoryStream> streams = new(DatasetSize);

    private int iterations = DatasetSize;

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
                // Low cardinality: a handful of distinct values across the whole dataset, so the
                // string cache resolves these to shared instances instead of new allocations.
                Country = recurringCountries[i % recurringCountries.Length],     // 5 distinct
                Region = recurringRegions[(i / 2) % recurringRegions.Length],    // 4 distinct
                Status = recurringStatus[(i / 3) % recurringStatus.Length],      // 4 distinct
                Category = "CAT-" + (i % 20),                                    // 20 distinct

                // High cardinality: unique per record, so a cache lookup can never hit. These pay
                // hash + probe + insert on every single value and evict useful entries while doing it.
                UserName = "user_" + i,                                          // unique
                SessionId = Guid.NewGuid().ToString("N"),                        // unique, 32 chars
                Description = "desc_" + i + "_"                                  // unique, longest
                    + recurringCountries[i % recurringCountries.Length] + "_"
                    + recurringStatus[(i / 3) % recurringStatus.Length],
                Tags = new List<string>
                {
                    recurringTags[i % recurringTags.Length],                     // 5 distinct
                    recurringTags[(i + 1) % recurringTags.Length],               // 5 distinct
                    "txn_" + i                                                   // unique
                }
            };

            var ms = new MemoryStream(512);
            featureJsonSerializer.Serialize(ms, item);
            streams.Add(ms);
        }
    }

    // The hit ratio is scale invariant: every iteration processes the same dataset, so the value
    // accumulated over the whole run is the same as for a single pass. Reporting it therefore costs
    // nothing during measurement but explains the timing/allocation table: the fully cached variant
    // wastes a large share of its lookups on values that can never hit, while the selective variant
    // only looks up fields that actually repeat.
    // The report is not printed here, because [GlobalCleanup] runs once per benchmark case and in a
    // separate worker process, which would scatter it through BenchmarkDotNet's progress output.
    // It is deferred and printed once after the summary instead.
    [GlobalCleanup]
    public void ReportStringCacheStatistics()
    {
        var report = new StringBuilder();
        report.AppendLine("// A low hit ratio means the cached members carry mostly unique values: those lookups can");
        report.AppendLine("// never hit, but still pay hash + probe + insert and evict entries that would have hit.");
        Append(report, "WithStringCache", featureWithStringCache);
        Append(report, "WithoutStringCache", featureWithoutStringCache);
        Append(report, "SelectiveStringCache", featureWithSelectiveStringCache);

        DeferredReport.Collect("String cache statistics (mixed string dataset)", report.ToString().TrimEnd());

        static void Append(StringBuilder report, string name, JsonDeserializer deserializer)
        {
            long hits = deserializer.StringCacheHitCount;
            long misses = deserializer.StringCacheMissCount;
            long total = hits + misses;
            if (total == 0)
            {
                report.AppendLine($"//   {name,-22} string cache not used");
                return;
            }
            report.AppendLine($"//   {name,-22} lookups: {total,12:N0}  hits: {hits,12:N0}  misses: {misses,12:N0}  hit ratio: {deserializer.StringCacheHitRatio:P1}");
        }
    }

    [Benchmark]
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
    public void DeserializeMixedStrings_SelectiveStringCache()
    {
        for (int i = 0; i < iterations; i++)
        {
            var stream = streams[i % DatasetSize];
            stream.Position = 0;
            featureWithSelectiveStringCache.TryDeserialize(stream, out MixedStringRecord _);
        }
    }

    [Benchmark(Baseline = true)]
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