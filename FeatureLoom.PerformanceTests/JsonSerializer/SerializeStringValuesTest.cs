using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for strings of different shapes. The cases
/// cover the distinct cost factors of the escaping writer: length, the ratio of
/// characters requiring escaping and the UTF-8 encoding width (1, 2, 3 and 4 bytes).
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeStringValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
    {

    });

    static JsonSerializerOptions systemTextJsonSerializerSettings = new JsonSerializerOptions()
    {
        IncludeFields = true,
    };

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    public static IEnumerable<StringCase> StringValues => new StringCase[]
    {
        new StringCase("Empty", ""),
        new StringCase("Short", "id"),
        new StringCase("Ascii", "The quick brown fox jumps over the lazy dog."),
        new StringCase("LongAscii", new string('a', 1000)),
        new StringCase("Escaped", "Line1\r\nLine2\t\"quoted\"\\path"),
        new StringCase("EscapedHeavy", new string('"', 200)),
        new StringCase("ControlChars", new string('\u0001', 200)),
        new StringCase("Latin1", new string('ä', 200)),
        new StringCase("Cjk", new string('漢', 200)),
        new StringCase("Emoji", Repeat("\U0001F600", 200)),
    };

    private static string Repeat(string text, int count)
    {
        var builder = new StringBuilder(text.Length * count);
        for (int i = 0; i < count; i++) builder.Append(text);
        return builder.ToString();
    }

    /// <summary>
    /// Wraps the string so that BenchmarkDotNet shows a readable case name instead of
    /// the (potentially very long) string content.
    /// </summary>
    public class StringCase
    {
        public readonly string Name;
        public readonly string Value;

        public StringCase(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString() => $"{Name}({Value.Length})";
    }

    [ParamsSource(nameof(StringValues))]
    public StringCase stringCase;

    private string value;

    [Params(1000)]
    public int iterations;

    [IterationSetup]
    public void Prepare()
    {
        value = stringCase.Value;
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeString_ToStream_Feature()
    {
        for (int i = 0; i < iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeString_ToStream_SystemText()
    {
        for (int i = 0; i < iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeString_ToStream_SpanJson()
    {
        for (int i = 0; i < iterations; i++)
        {
            // SpanJson only offers an async stream API. The MemoryStream completes synchronously,
            // so blocking here adds no measurable overhead but ensures the write actually happened.
            SpanJson.JsonSerializer.Generic.Utf8.SerializeAsync(value, memoryStream).GetAwaiter().GetResult();
        }
    }
#endif
}
