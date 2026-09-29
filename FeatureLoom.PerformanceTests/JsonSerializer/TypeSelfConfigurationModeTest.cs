using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using FeatureLoom.Serialization;
using System.IO;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Measures the cost of the <see cref="TypeSelfConfigurationMode"/> options.
/// "Steady" shows the per-value cost of an already prepared (de)serializer (expected: no difference,
/// except for the deserializer in <see cref="TypeSelfConfigurationMode.Enabled"/>, where reference
/// resolution is not force-disabled). "Cold" creates a new (de)serializer per operation, so it includes
/// settings compilation and reader/writer creation, where the type-own configuration is resolved.
/// The type has no config method, which is the common case for the default <see cref="TypeSelfConfigurationMode.IgnoreButWarn"/>.
/// </summary>
[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(25)]
[MaxIterationCount(100)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams)]
[CategoriesColumn]
public class TypeSelfConfigurationModeTest
{
    const int ColdIterations = 100;

    [Params(TypeSelfConfigurationMode.Ignore,
            TypeSelfConfigurationMode.IgnoreButWarn,
            TypeSelfConfigurationMode.Enabled,
            TypeSelfConfigurationMode.EnabledKeepRefTrackingOff)]
    public TypeSelfConfigurationMode Mode;

    Serialization.JsonSerializer serializer;
    JsonDeserializer deserializer;
    MemoryStream writeStream = new MemoryStream();
    MemoryStream readStream_Single = new MemoryStream();
    MemoryStream readStream_Array = new MemoryStream();
    UserObject single = new UserObject();
    UserObject[] array;

    Serialization.JsonSerializer CreateSerializer() =>
        new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings { typeSelfConfigurationMode = Mode });

    // Reference resolution and proposed types are left at their defaults on purpose,
    // because the mode influences the automatic reference resolution downgrade.
    JsonDeserializer CreateDeserializer() =>
        new JsonDeserializer(new JsonDeserializer.Settings { typeSelfConfigurationMode = Mode });

    [GlobalSetup]
    public void Setup()
    {
        array = new UserObject[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++) array[i] = new UserObject() { id = i };

        serializer = CreateSerializer();
        deserializer = CreateDeserializer();
        serializer.Serialize(readStream_Single, single);
        serializer.Serialize(readStream_Array, array);
    }

    [BenchmarkCategory("Steady_Serialize")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void Serialize_Array()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            writeStream.Position = 0;
            serializer.Serialize(writeStream, array);
        }
    }

    [BenchmarkCategory("Steady_Deserialize")]
    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void Deserialize_Array()
    {
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            readStream_Array.Position = 0;
            deserializer.TryDeserialize(readStream_Array, out UserObject[] _);
        }
    }

    [BenchmarkCategory("Cold_Serialize")]
    [Benchmark(OperationsPerInvoke = ColdIterations)]
    public void Serialize_Single_NewSerializer()
    {
        for (int i = 0; i < ColdIterations; i++)
        {
            writeStream.Position = 0;
            CreateSerializer().Serialize(writeStream, single);
        }
    }

    [BenchmarkCategory("Cold_Deserialize")]
    [Benchmark(OperationsPerInvoke = ColdIterations)]
    public void Deserialize_Single_NewDeserializer()
    {
        for (int i = 0; i < ColdIterations; i++)
        {
            readStream_Single.Position = 0;
            CreateDeserializer().TryDeserialize(readStream_Single, out UserObject _);
        }
    }
}
