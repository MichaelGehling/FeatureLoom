using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.IO;
using System.Text.Json;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Measures writing nullable value type members that actually carry a value.
/// This exercises the nullable dispatch path in CachedTypeWriter.WriteItem, which the
/// existing ComplexObject benchmark never reaches because its nullable field is null.
/// <para>
/// An explicit out-of-process job is required because Program.cs applies a
/// DebugInProcessConfig globally, and the allocation diagnoser can only attach to an
/// out-of-process job. Boxing is an allocation effect, so it is only measurable there.
/// </para>
/// </summary>
[SimpleJob(RuntimeMoniker.Net10_0)]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(500)]
[MaxIterationCount(5000)]
[CPUUsageDiagnoser]
public class SerializeNullableMembersTest
{
    public class NullableObject
    {
        public int? myNullableInt = 42;
        public long? myNullableLong = 1234567890L;
        public double? myNullableDouble = -0.00015890432;
        public bool? myNullableBool = true;
        public System.Guid? myNullableGuid = new System.Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff");
        public System.DateTime? myNullableDateTime = new System.DateTime(2024, 1, 2, 3, 4, 5, System.DateTimeKind.Utc);
    }

    static Serialization.JsonSerializer featureJsonSerializer = SerializerConfigs.CreateFeatureSerializer();
    static JsonSerializerOptions systemTextJsonSerializerSettings = SerializerConfigs.CreateSystemTextOptions();
    MemoryStream memoryStream = new MemoryStream(1024 * 1024 * 100);
    NullableObject single = new NullableObject();
    NullableObject[] array;
    [GlobalSetup]
    public void Setup()
    {
        array = new NullableObject[BenchmarkSettings.ArraySize];
        for (int i = 0; i < array.Length; i++)
            array[i] = new NullableObject();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeNullableMembers_Single_Feature()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            featureJsonSerializer.Serialize(memoryStream, single);
        }
    }

    [Benchmark(OperationsPerInvoke = BenchmarkSettings.Iterations)]
    public void SerializeNullableMembers_Single_SystemText()
    {
        memoryStream.Position = 0;
        for (int i = 0; i < BenchmarkSettings.Iterations; i++)
        {
            System.Text.Json.JsonSerializer.Serialize(memoryStream, single, systemTextJsonSerializerSettings);
        }
    }

    [Benchmark]
    public void SerializeNullableMembers_Array_Feature()
    {
        memoryStream.Position = 0;
        featureJsonSerializer.Serialize(memoryStream, array);
    }

    [Benchmark]
    public void SerializeNullableMembers_Array_SystemText()
    {
        memoryStream.Position = 0;
        System.Text.Json.JsonSerializer.Serialize(memoryStream, array, systemTextJsonSerializerSettings);
    }
}