namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Shared workload constants for the JSON serializer benchmarks.
/// <para>
/// Every serialize benchmark measures two cases with the same payload type:
/// a single value/object (dominated by the per-serialization overhead) and an array of
/// that value/object (where the per-serialization overhead is amortized, so the actual
/// value formatting dominates). Comparing both makes it visible how much of a result is
/// overhead and how much is formatting.
/// </para>
/// <para>
/// A single significant iteration count is used instead of a sweep, because the smaller
/// counts mostly measured benchmark harness noise. The array case uses fewer iterations
/// so that both cases process a comparable total number of values.
/// </para>
/// </summary>
public static class BenchmarkSettings
{
    /// <summary>Number of serialize calls in the single-value benchmarks.</summary>
    public const int Iterations = 1000;

    /// <summary>Number of elements in the array used by the array benchmarks.</summary>
    public const int ArraySize = 1000;

    /// <summary>Number of serialize calls in the array benchmarks.</summary>
    public const int ArrayIterations = 10;
}
