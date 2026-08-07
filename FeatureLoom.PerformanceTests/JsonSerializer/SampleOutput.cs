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
/// </summary>
public static class SampleOutput
{
    static readonly Dictionary<string, string> samples = new Dictionary<string, string>();

    /// <summary>
    /// Records the JSON produced by each serializer for the given value. The samples are
    /// not printed immediately, because the setup runs long before the summary; use
    /// <see cref="PrintAll"/> after the benchmark run to show them next to the results.
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

        lock (samples)
        {
            samples[caseName] = sample;
        }
    }

    /// <summary>
    /// Prints all collected samples. Call this after the benchmark run, so the sanity
    /// check is visible directly below the summary instead of scrolled far above it.
    /// </summary>
    public static void PrintAll()
    {
        KeyValuePair<string, string>[] collected;
        lock (samples)
        {
            if (samples.Count == 0) return;
            collected = new List<KeyValuePair<string, string>>(samples).ToArray();
        }

        Console.WriteLine();
        Console.WriteLine("// ===== Serializer output samples (sanity check) =====");
        Console.WriteLine("// The benchmark results are only comparable if all serializers produce equivalent output.");
        foreach (var entry in collected)
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
        return text.Substring(0, maxLength) + $"... (+{text.Length - maxLength} chars)";
    }
}
