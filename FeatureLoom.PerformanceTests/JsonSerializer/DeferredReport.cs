using System;
using System.Collections.Generic;
using System.IO;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Collects free-form report text produced during a benchmark run and prints it after the whole
/// run, so it appears next to the summary table instead of being buried in the middle of
/// BenchmarkDotNet's progress output.
/// <para>
/// Like <see cref="SampleOutput"/>, this has to work across processes: BenchmarkDotNet executes
/// each benchmark class in a separate out-of-process worker by default, so a static field written
/// by <see cref="Collect"/> would never be visible to the host process that calls
/// <see cref="PrintAll"/>. A shared temp file is used instead.
/// </para>
/// </summary>
public static class DeferredReport
{
    const string TitleMarker = "###REPORT:";
    const string EndMarker = "###END###";

    static readonly string reportFilePath = Path.Combine(Path.GetTempPath(), "FeatureLoom.PerformanceTests.DeferredReport.tmp");

    /// <summary>
    /// Deletes leftovers from a previous run. Call once before the benchmark switcher runs.
    /// </summary>
    public static void Reset()
    {
        try { File.Delete(reportFilePath); } catch { /* best effort */ }
    }

    /// <summary>
    /// Records a report block under the given title. Blocks with the same title are deduplicated,
    /// so a report emitted from a per-benchmark hook (like <c>[GlobalCleanup]</c>, which runs once
    /// per benchmark case) is shown only once.
    /// </summary>
    public static void Collect(string title, string content)
    {
        string entry = TitleMarker + title + Environment.NewLine + content + Environment.NewLine + EndMarker + Environment.NewLine;

        // Multiple benchmark processes may append concurrently, so retry on sharing violations.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                File.AppendAllText(reportFilePath, entry);
                return;
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(10);
            }
        }
    }

    /// <summary>
    /// Prints all collected reports. Call this after the benchmark run.
    /// </summary>
    public static void PrintAll()
    {
        if (!File.Exists(reportFilePath)) return;

        string content = File.ReadAllText(reportFilePath);
        var reports = new Dictionary<string, string>();
        foreach (string block in content.Split(new[] { EndMarker }, StringSplitOptions.RemoveEmptyEntries))
        {
            int markerIndex = block.IndexOf(TitleMarker, StringComparison.Ordinal);
            if (markerIndex < 0) continue;

            string afterMarker = block.Substring(markerIndex + TitleMarker.Length);
            int newlineIndex = afterMarker.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            if (newlineIndex < 0) continue;

            string title = afterMarker.Substring(0, newlineIndex);
            string body = afterMarker.Substring(newlineIndex + Environment.NewLine.Length).Trim(Environment.NewLine.ToCharArray());
            reports[title] = body;
        }

        try { File.Delete(reportFilePath); } catch { /* best effort */ }

        if (reports.Count == 0) return;

        foreach (var entry in reports)
        {
            Console.WriteLine();
            Console.WriteLine($"// ===== {entry.Key} =====");
            Console.WriteLine(entry.Value);
        }
        Console.WriteLine();
    }
}
