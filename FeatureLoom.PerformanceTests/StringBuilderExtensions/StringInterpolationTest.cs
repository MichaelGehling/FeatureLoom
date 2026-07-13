using BenchmarkDotNet.Attributes;
using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using System;
using System.Text;

namespace FeatureLoom.PerformanceTests.StringBuilderExtensions;

[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(100)]
[MaxIterationCount(500)]
public class StringInterpolationTest
{
    // A realistic mix of names/identifiers as they might appear in log messages: short first
    // names, full names, e-mail addresses, service/host identifiers, etc. Lengths vary but stay
    // well within typical caching thresholds. Picking cyclically from this pool gives a natural
    // "categorical value" access pattern (a limited set of distinct values, repeated many times)
    // instead of an artificial single-value or fully-unique extreme.
    static readonly string[] realisticNames =
    [
        "Alice",
        "Bob",
        "Christopher",
        "Dr. Elizabeth Montgomery",
        "svc-billing-worker-03",
        "user.smith@example.com",
        "Node-EU-West-1a",
        "Jean-Pierre",
        "田中太郎",
        "OrderProcessingService",
        "guest",
        "admin@company.internal",
        "10.0.42.17",
        "Sarah O'Connor",
        "background-job-scheduler"
    ];

    int count = 42;
    double value = 3.14159;

    // Pre-created, reusable StringBuilder for the "cached" scenario, so that
    // preparing/resetting the builder is not part of the measured work.
    StringBuilder preparedBuilder = new StringBuilder();

    // Dedicated, individual cache instance instead of StringInternCache.Shared, so that
    // this benchmark's cache contents/pressure don't affect (or get affected by) other tests.
    StringInternCache internCache = new StringInternCache(13, 128);

    [Params(1_000_000, 10_000, 100, 1)]
    public int iterations;

    [IterationSetup]
    public void Prepare()
    {
        iterations = Math.Abs(iterations);
        preparedBuilder.Clear();
    }

    // Cycles through the realistic name pool, giving a repeating, categorical-value-like pattern.
    string GetName(int i) => realisticNames[i % realisticNames.Length];

    [Benchmark(Baseline = true)]
    public void StandardInterpolation()
    {
        for (int i = 0; i < iterations; i++)
        {
            string result = $"Hello {GetName(i)}, you have {count} messages (value: {value:F2})";
        }
    }

    [Benchmark]
    public void StringFormat()
    {
        for (int i = 0; i < iterations; i++)
        {
            string result = string.Format("Hello {0}, you have {1} messages (value: {2:F2})", GetName(i), count, value);
        }
    }

    [Benchmark]
    public void BuildString_Pooled()
    {
        for (int i = 0; i < iterations; i++)
        {
            string result = StringBuilder.BuildString($"Hello {GetName(i)}, you have {count} messages (value: {value:F2})");
        }
    }

    [Benchmark]
    public void BuildCachedString_Pooled()
    {
        for (int i = 0; i < iterations; i++)
        {
            string result = StringBuilder.BuildCachedString($"Hello {GetName(i)}, you have {count} messages (value: {value:F2})", internCache);
        }
    }

    [Benchmark]
    public void BuildCachedString_PreparedBuilder()
    {
        for (int i = 0; i < iterations; i++)
        {
            preparedBuilder.Append($"Hello {GetName(i)}, you have {count} messages (value: {value:F2})");
            string result = preparedBuilder.BuildWithCacheAndClear(internCache);
        }
    }
}


