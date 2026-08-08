using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the serialization performance for DateTimeOffset values. The cases cover the
/// distinct code paths of the writer: the shortcut for the default value, a zero, a positive
/// and a negative UTC offset, an offset with a non-zero minute part and a value with
/// fractional seconds.
/// Each case is measured as a single value (dominated by the per-serialization overhead)
/// and as an array of that value (dominated by the actual formatting).
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class SerializeDateTimeOffsetValuesTest
{
    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();

    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();

    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 10);

    public static IEnumerable<DateTimeOffsetCase> DateTimeOffsetValues => new DateTimeOffsetCase[]
    {
        new DateTimeOffsetCase("Default", default),
        new DateTimeOffsetCase("ZeroOffset", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero)),
        new DateTimeOffsetCase("PositiveOffset", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2))),
        new DateTimeOffsetCase("NegativeOffset", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(-5))),
        new DateTimeOffsetCase("HalfHourOffset", new DateTimeOffset(2024, 1, 2, 3, 4, 5, new TimeSpan(5, 30, 0))),
        new DateTimeOffsetCase("WithFraction", new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero).AddTicks(1234567)),
    };

    /// <summary>
    /// Wraps the value so that BenchmarkDotNet shows a readable case name.
    /// </summary>
    public class DateTimeOffsetCase
    {
        public readonly string Name;
        public readonly DateTimeOffset Value;

        public DateTimeOffsetCase(string name, DateTimeOffset value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString() => Name;
    }

    [ParamsSource(nameof(DateTimeOffsetValues))]
    public DateTimeOffsetCase dateTimeOffsetCase;

    private DateTimeOffset value;
    private DateTimeOffset[] array;

    [GlobalSetup]
    public void Setup()
    {
        value = dateTimeOffsetCase.Value;
        array = new DateTimeOffset[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = value;

        SampleOutput.Collect($"DateTimeOffset({dateTimeOffsetCase})", value, featureJsonSerializer, systemTextJsonSerializerSettings);
    }

    [IterationSetup]
    public void Prepare()
    {
        memoryStream.Position = 0;
    }

    [Benchmark]
    public void SerializeDateTimeOffset_Single_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, value);
        }
    }

    [Benchmark(Baseline = true)]
    public void SerializeDateTimeOffset_Single_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, value, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeDateTimeOffset_Single_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            // SpanJson only offers an async stream API. The MemoryStream completes synchronously,
            // so blocking here adds no measurable overhead but ensures the write actually happened.
            SerializerConfigs.SerializeWithSpanJson(value, memoryStream);
        }
    }
#endif

    [Benchmark]
    public void SerializeDateTimeOffset_Array_Feature()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, array);
        }
    }

    [Benchmark]
    public void SerializeDateTimeOffset_Array_SystemText()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
        }
    }

#if NET6_0_OR_GREATER
    [Benchmark]
    public void SerializeDateTimeOffset_Array_SpanJson()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            SerializerConfigs.SerializeWithSpanJson(array, memoryStream);
        }
    }
#endif
}
