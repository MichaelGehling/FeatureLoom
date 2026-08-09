using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the deserialization performance for strings of different shapes, mirroring
/// <see cref="SerializeStringValuesTest"/>.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class DeserializeStringValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonDeserializer featureJsonDeserializer = SerializerConfigs.CreateFeatureDeserializer();
    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    public static IEnumerable<SerializeStringValuesTest.StringCase> StringValues => new SerializeStringValuesTest.StringCase[]
    {
        new SerializeStringValuesTest.StringCase("Empty", ""),
        new SerializeStringValuesTest.StringCase("Short", "id"),
        new SerializeStringValuesTest.StringCase("Ascii", "The quick brown fox jumps over the lazy dog."),
        new SerializeStringValuesTest.StringCase("LongAscii", new string('a', 1000)),
        new SerializeStringValuesTest.StringCase("Escaped", "Line1\r\nLine2\t\"quoted\"\\path"),
        new SerializeStringValuesTest.StringCase("EscapedHeavy", new string('"', 200)),
        new SerializeStringValuesTest.StringCase("ControlChars", new string('\u0001', 200)),
        new SerializeStringValuesTest.StringCase("Latin1", new string('ä', 200)),
        new SerializeStringValuesTest.StringCase("Cjk", new string('漢', 200)),
        new SerializeStringValuesTest.StringCase("Emoji", Repeat("\U0001F600", 200)),
        new SerializeStringValuesTest.StringCase("AsciiThenWide", new string('a', 999) + "漢"),
    };

    private static string Repeat(string text, int count)
    {
        var builder = new StringBuilder(text.Length * count);
        for (int i = 0; i < count; i++) builder.Append(text);
        return builder.ToString();
    }

    [ParamsSource(nameof(StringValues))]
    public SerializeStringValuesTest.StringCase stringCase;

    private string value;
    private string[] array;

    MemoryStream featureStream_Single = new MemoryStream();
    MemoryStream featureStream_Array = new MemoryStream();

    [GlobalSetup]
    public void Setup()
    {
        value = stringCase.Value;
        array = new string[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        featureJsonSerializer.Serialize(featureStream_Single, value);
        featureJsonSerializer.Serialize(featureStream_Array, array);

        SampleOutput.Collect($"String({stringCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings, maxLength: 200);
    }

    [IterationSetup]
    public void Prepare()
    {
        featureStream_Single.Position = 0;
        featureStream_Array.Position = 0;
    }

    [Benchmark]
    public void DeserializeString_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Single, out string result);
        }
    }

    [Benchmark(Baseline = true)]
    public void DeserializeString_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            string result = System.Text.Json.JsonSerializer.Deserialize<string>(featureStream_Single, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeString_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureStream_Single.Position = 0;
            string result = SerializerConfigs.DeserializeWithSpanJson<string>(featureStream_Single);
        }
    }
#endif

    [Benchmark]
    public void DeserializeString_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            featureJsonDeserializer.TryDeserialize(featureStream_Array, out string[] result);
        }
    }

    [Benchmark]
    public void DeserializeString_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            string[] result = System.Text.Json.JsonSerializer.Deserialize<string[]>(featureStream_Array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void DeserializeString_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureStream_Array.Position = 0;
            string[] result = SerializerConfigs.DeserializeWithSpanJson<string[]>(featureStream_Array);
        }
    }
#endif
}
