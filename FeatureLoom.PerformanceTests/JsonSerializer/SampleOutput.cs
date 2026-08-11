using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Prints the produced JSON of every compared serializer for a benchmark case.
/// <para>
/// This is a sanity check: a benchmark result is only meaningful if all serializers
/// actually produce comparable output. Differences in escaping, number formatting or
/// date representation would otherwise silently distort the comparison.
/// </para>
/// <para>
/// The samples are collected once per distinct case (the same case is reported for every
/// benchmark method of a class, so it is deduplicated) and only for the single-value
/// payload, since the array payload just repeats it. They are printed after the whole
/// benchmark run, so they appear directly with the summary instead of far above it.
/// </para>
/// <para>
/// BenchmarkDotNet runs each benchmark class in a separate out-of-process worker by
/// default, so an in-memory dictionary populated by <see cref="Collect"/> would never be
/// visible to the host process that calls <see cref="PrintAll"/>. Samples are therefore
/// exchanged through a shared temp file instead of a static field.
/// </para>
/// </summary>
public static class SampleOutput
{
    const string CaseMarker = "###CASE:";
    const string EndMarker = "###END###";

    static readonly string sampleFilePath = Path.Combine(Path.GetTempPath(), "FeatureLoom.PerformanceTests.SampleOutput.tmp");

    /// <summary>
    /// Deletes any leftover samples from a previous run. Call this once before the
    /// benchmark switcher runs, so <see cref="PrintAll"/> only shows samples from the
    /// current run.
    /// </summary>
    public static void Reset()
    {
        try { File.Delete(sampleFilePath); } catch { /* best effort */ }
    }

    /// <summary>
    /// Records the JSON produced by each serializer for the given value. The samples are
    /// not printed immediately, because the setup runs long before the summary and in a
    /// different process; use <see cref="PrintAll"/> after the benchmark run to show them
    /// next to the results.
    /// <para>
    /// The serializer instances of the benchmark itself must be passed, so the shown
    /// output uses exactly the same configuration as the measured calls.
    /// </para>
    /// </summary>
    /// <param name="maxLength">
    /// Optional limit for the printed characters per serializer. 0 (default) prints the
    /// complete output; use a limit only for huge payloads like big byte arrays or long strings.
    /// </param>
    public static void Collect<T>(string caseName, T value, Serialization.JsonSerializer featureSerializer, JsonSerializerOptions systemTextOptions, int maxLength = 0)
    {
        string sample = $"//   Feature    : {Truncate(SerializeWithFeature(value, featureSerializer), maxLength)}" + Environment.NewLine +
                        $"//   SystemText : {Truncate(SerializeWithSystemText(value, systemTextOptions), maxLength)}";
#if NET6_0_OR_GREATER
        sample += Environment.NewLine +
                  $"//   SpanJson   : {Truncate(SerializeWithSpanJson(value), maxLength)}";
#endif

        string entry = CaseMarker + caseName + Environment.NewLine + sample + Environment.NewLine + EndMarker + Environment.NewLine;

        // Multiple benchmark processes may append concurrently, so retry on sharing violations.
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                File.AppendAllText(sampleFilePath, entry);
                return;
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(10);
            }
        }
    }

    /// <summary>
    /// Prints all collected samples. Call this after the benchmark run, so the sanity
    /// check is visible directly below the summary instead of scrolled far above it.
    /// </summary>
    public static void PrintAll()
    {
        if (!File.Exists(sampleFilePath)) return;

        string content = File.ReadAllText(sampleFilePath);
        var samples = new Dictionary<string, string>();
        foreach (string block in content.Split(new[] { EndMarker }, StringSplitOptions.RemoveEmptyEntries))
        {
            int markerIndex = block.IndexOf(CaseMarker, StringComparison.Ordinal);
            if (markerIndex < 0) continue;
            string afterMarker = block.Substring(markerIndex + CaseMarker.Length);
            int newlineIndex = afterMarker.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            if (newlineIndex < 0) continue;
            string caseName = afterMarker.Substring(0, newlineIndex);
            string sample = afterMarker.Substring(newlineIndex + Environment.NewLine.Length).Trim(Environment.NewLine.ToCharArray());
            samples[caseName] = sample;
        }

        try { File.Delete(sampleFilePath); } catch { /* best effort */ }

        if (samples.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine("// ===== Serializer output samples (sanity check) =====");
        Console.WriteLine("// The benchmark results are only comparable if all serializers produce equivalent output.");
        foreach (var entry in samples)
        {
            Console.WriteLine();
            Console.WriteLine($"// [{entry.Key}]");
            Console.WriteLine(entry.Value);
        }
        Console.WriteLine();
    }

    static string SerializeWithFeature<T>(T value, Serialization.JsonSerializer featureSerializer)
    {
        try
        {
            using var stream = new MemoryStream();
            featureSerializer.Serialize(stream, value);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (Exception e)
        {
            return $"<{e.GetType().Name}: {e.Message}>";
        }
    }

    static string SerializeWithSystemText<T>(T value, JsonSerializerOptions systemTextOptions)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(value, systemTextOptions);
        }
        catch (Exception e)
        {
            return $"<{e.GetType().Name}: {e.Message}>";
        }
    }

#if NET6_0_OR_GREATER
    static string SerializeWithSpanJson<T>(T value)
    {
        try
        {
            using var stream = new MemoryStream();
            SerializerConfigs.SerializeWithSpanJson(value, stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (Exception e)
        {
            return $"<{e.GetType().Name}: {e.Message}>";
        }
    }
#endif

    static string Truncate(string text, int maxLength)
    {
        if (text == null) return "<null>";
        if (maxLength <= 0 || text.Length <= maxLength) return text;

        // Never cut inside a surrogate pair, otherwise a lone surrogate would be written to
        // the console and corrupt the output stream that BenchmarkDotNet parses.
        int cut = maxLength;
        if (char.IsHighSurrogate(text[cut - 1])) cut--;

        return text.Substring(0, cut) + $"... (+{text.Length - cut} chars)";
    }
}
