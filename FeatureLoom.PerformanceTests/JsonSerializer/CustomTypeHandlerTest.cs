using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using System.IO;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Baseline for the custom type handler API redesign.
/// <para>
/// The custom handler path is only useful if it stays close to the generated one. So every
/// case is measured twice with the same payload: once handled by the built-in
/// (generated) handler and once by a hand-written custom handler producing equivalent JSON.
/// The built-in variant is the reference; the interesting number is the ratio between them,
/// not the absolute time.
/// </para>
/// <para>
/// Two shapes are covered, because they exercise different wrappers and are the two the
/// redesign's builders map onto:
/// </para>
/// <list type="bullet">
/// <item>Value: a type collapsed into a single JSON string (primitive wrapper).</item>
/// <item>Object: a type written as a JSON object with three fields (object wrapper) — this
/// is the case the planned object builder must not be slower than.</item>
/// </list>
/// <para>
/// The array variants amortize the per-call overhead so the handler body dominates; the
/// single variants show the overhead itself.
/// </para>
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
public class CustomTypeHandlerTest
{
    // Built-in reference: no custom handler registered, the generated handler is used.
    static Serialization.JsonSerializer builtInSerializer = CreateSerializer(withCustomHandlers: false);

    // Same settings, but both payload types are handled by a custom handler.
    static Serialization.JsonSerializer customSerializer = CreateSerializer(withCustomHandlers: true);

    static JsonDeserializer builtInDeserializer = CreateDeserializer(withCustomReader: false);
    static JsonDeserializer customDeserializer = CreateDeserializer(withCustomReader: true);

    static Serialization.JsonSerializer CreateSerializer(bool withCustomHandlers)
    {
        var settings = new Serialization.JsonSerializer.Settings()
        {
            typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
        };

        if (withCustomHandlers)
        {
            settings.ConfigureType<CustomValue>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<CustomValue>((value, item) => value.WriteString(item.text))));

            settings.ConfigureType<CustomObject>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareObjectWriter<CustomObject>(obj => obj
                    .AddField("id", item => item.id)
                    .AddField("name", item => item.name)
                    .AddField("value", item => item.value))));
        }

        return new Serialization.JsonSerializer(settings);
    }

    static JsonDeserializer CreateDeserializer(bool withCustomReader)
    {
        return new JsonDeserializer(settings =>
        {
            settings.dataAccess = JsonDeserializer.DataAccess.PublicFieldsAndProperties;
            settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
            settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.ForceDisabled;

            if (withCustomReader)
            {
                settings.ConfigureType<CustomValue>(typeSettings => typeSettings.SetCustomTypeReader(
                    api =>
                    {
                        api.TryReadStringValueOrNull(out string text);
                        return new CustomValue { text = text };
                    }));
            }
        });
    }

    MemoryStream serializeStream = new MemoryStream(1024 * 1024 * 16);

    MemoryStream builtInValueStream_Array = new MemoryStream();
    MemoryStream customValueStream_Array = new MemoryStream();

    CustomValue singleValue = new CustomValue();
    CustomValue[] valueArray;

    CustomObject singleObject = new CustomObject();
    CustomObject[] objectArray;

    [GlobalSetup]
    public void Setup()
    {
        valueArray = new CustomValue[BenchmarkSettings.ArraySize];
        for (int i = 0; i < valueArray.Length; i++) valueArray[i] = new CustomValue { text = "value " + i };

        objectArray = new CustomObject[BenchmarkSettings.ArraySize];
        for (int i = 0; i < objectArray.Length; i++) objectArray[i] = new CustomObject { id = i };

        // Each deserializer gets the JSON its own serializer produces: the built-in one an
        // object, the custom one a bare string. The byte counts therefore differ, so this
        // pair does not compare parsing speed for identical input - it compares the total
        // cost of the two representations end to end.
        builtInSerializer.Serialize(builtInValueStream_Array, valueArray);
        customSerializer.Serialize(customValueStream_Array, valueArray);
    }

    [IterationSetup]
    public void Prepare()
    {
        serializeStream.Position = 0;
    }

    [Benchmark(Baseline = true)]
    public void SerializeValue_Single_BuiltIn()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++) builtInSerializer.Serialize(serializeStream, singleValue);
    }

    [Benchmark]
    public void SerializeValue_Single_Custom()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++) customSerializer.Serialize(serializeStream, singleValue);
    }

    [Benchmark]
    public void SerializeValue_Array_BuiltIn()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++) builtInSerializer.Serialize(serializeStream, valueArray);
    }

    [Benchmark]
    public void SerializeValue_Array_Custom()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++) customSerializer.Serialize(serializeStream, valueArray);
    }

    [Benchmark]
    public void SerializeObject_Single_BuiltIn()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++) builtInSerializer.Serialize(serializeStream, singleObject);
    }

    [Benchmark]
    public void SerializeObject_Single_Custom()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++) customSerializer.Serialize(serializeStream, singleObject);
    }

    [Benchmark]
    public void SerializeObject_Array_BuiltIn()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++) builtInSerializer.Serialize(serializeStream, objectArray);
    }

    [Benchmark]
    public void SerializeObject_Array_Custom()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++) customSerializer.Serialize(serializeStream, objectArray);
    }

    [Benchmark]
    public void DeserializeValue_Array_BuiltIn()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            builtInValueStream_Array.Position = 0;
            builtInDeserializer.TryDeserialize(builtInValueStream_Array, out CustomValue[] _);
        }
    }

    [Benchmark]
    public void DeserializeValue_Array_Custom()
    {
        for (int i = 0; i < BenchmarkSettings.ArrayIterations; i++)
        {
            customValueStream_Array.Position = 0;
            customDeserializer.TryDeserialize(customValueStream_Array, out CustomValue[] _);
        }
    }
}

/// <summary>Type that is naturally a single JSON string when handled by a custom handler.</summary>
public class CustomValue
{
    public string text = "This is a string";
}

/// <summary>Type with the same shape as <see cref="SimpleObject"/>, used to compare a custom object handler against the generated one.</summary>
public class CustomObject
{
    public int id = 0;
    public string name = "This is a string";
    public double value = 123.456;
}
