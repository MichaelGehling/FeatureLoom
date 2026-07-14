using BenchmarkDotNet.Attributes;
using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using System;
using System.Text;

namespace FeatureLoom.PerformanceTests.StringBuilderExtensions;

[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(50)]
[MaxIterationCount(500)]
public class StringInterpolationTest
{
    // Models a typical telemetry/logging workload: 20% low-cardinality categorical values, 30%
    // medium-cardinality tenant/user identifiers, and 50% request-like values from a long tail.
    // The corpus is pre-built so input generation is not included in the interpolation benchmark.
    const int WorkloadSize = 65_536;

    static readonly string[] commonValues =
    [
        "anonymous",
        "authenticated",
        "checkout-service",
        "catalog-service",
        "payment-worker-03",
        "Node-EU-West-1a",
        "10.0.42.17",
        "guest@example.com",
        "admin@company.internal",
        "OrderProcessingService",
        "background-job-scheduler",
        "cache-miss",
        "rate-limited",
        "success",
        "retrying",
        "田中太郎"
    ];

    int count = 42;
    double value = 3.14159;
    string[] workload;

    // Pre-created, reusable StringBuilder for the "cached" scenario, so that
    // preparing/resetting the builder is not part of the measured work.
    StringBuilder preparedBuilder = new StringBuilder();

    // Dedicated, individual cache instance instead of StringInternCache.Shared, so that
    // this benchmark's cache contents/pressure don't affect (or get affected by) other tests.
    StringInternCache internCache = new StringInternCache(16, 256);

    [Params(1_000_000, 10_000, 100, 1)]
    public int iterations;

    [GlobalSetup]
    public void CreateWorkload()
    {
        workload = new string[WorkloadSize];
        for (int i = 0; i < workload.Length; i++)
        {
            int bucket = i % 10;
            if (bucket < 2)
            {
                workload[i] = commonValues[i % commonValues.Length];
            }
            else if (bucket < 5)
            {
                workload[i] = $"customer-{i % 4_096:D4}@tenant-{i % 64:D2}.example.com";
            }
            else
            {
                workload[i] = $"POST /v2/orders/{i:D8}/items?region=eu-west-{(i % 3) + 1}&trace={i * 2_654_435_761u:X8}";
            }
        }

        internCache.Clear();
    }

    [IterationSetup]
    public void Prepare()
    {
        iterations = Math.Abs(iterations);
        preparedBuilder.Clear();
    }

    string GetWorkloadValue(int i) => workload[i % workload.Length];

    [Benchmark(Baseline = true)]
    public void StandardInterpolation()
    {
        for (int i = 0; i < iterations; i++)
        {
            string result = $"Hello {GetWorkloadValue(i)}, you have {count} messages (value: {value:F2})";
        }
    }

    [Benchmark]
    public void StringFormat()
    {
        for (int i = 0; i < iterations; i++)
        {
            string result = string.Format("Hello {0}, you have {1} messages (value: {2:F2})", GetWorkloadValue(i), count, value);
        }
    }

    [Benchmark]
    public void BuildString_Pooled()
    {
        for (int i = 0; i < iterations; i++)
        {
            string result = StringBuilder.BuildString($"Hello {GetWorkloadValue(i)}, you have {count} messages (value: {value:F2})");
        }
    }

    [Benchmark]
    public void BuildCachedString_Pooled()
    {
        for (int i = 0; i < iterations; i++)
        {
            string result = StringBuilder.BuildCachedString($"Hello {GetWorkloadValue(i)}, you have {count} messages (value: {value:F2})", internCache);
        }
    }

    [Benchmark]
    public void BuildCachedString_PreparedBuilder()
    {
        for (int i = 0; i < iterations; i++)
        {
            preparedBuilder.Append($"Hello {GetWorkloadValue(i)}, you have {count} messages (value: {value:F2})");
            string result = preparedBuilder.BuildWithCacheAndClear(internCache);
        }
    }
}


