using BenchmarkDotNet.Attributes;
using FeatureLoom.Collections;
using FeatureLoom.Serialization;
using System.Text;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests.JsonSerializer;
[CPUUsageDiagnoser]
public class SkipJsonValueBenchmark
{
    private static readonly JsonDeserializer Deserializer = SerializerConfigs.CreateFeatureDeserializer();
    private ByteSegment stringValue;
    private ByteSegment numberValue;
    private ByteSegment boolValue;
    private ByteSegment nullValue;
    private ByteSegment arrayValue;
    private ByteSegment objectValue;
    [GlobalSetup]
    public void Setup()
    {
        stringValue = Utf8("\"FeatureLoom escaped \\\"value\\\" with \\\\ and unicode 6\"");
        numberValue = Utf8("-1234567890.123456789e-42");
        boolValue = Utf8("true");
        nullValue = Utf8("null");
        arrayValue = Utf8("[\"alpha\",-123.456e7,true,null,{\"nested\":[1,2,3,false]}]");
        objectValue = Utf8("{\"text\":\"alpha\",\"number\":-123.456e7,\"bool\":true,\"nil\":null,\"array\":[1,2,3],\"object\":{\"nested\":false}}");
    }

    private static ByteSegment Utf8(string json) => new ByteSegment(Encoding.UTF8.GetBytes(json));
    private static JsonFragment Skip(ByteSegment json)
    {
        Deserializer.TryDeserialize(json, out JsonFragment fragment);
        return fragment;
    }

    [Benchmark]
    public JsonFragment SkipString() => Skip(stringValue);
    [Benchmark]
    public JsonFragment SkipNumber() => Skip(numberValue);
    [Benchmark]
    public JsonFragment SkipBool() => Skip(boolValue);
    [Benchmark]
    public JsonFragment SkipNull() => Skip(nullValue);
    [Benchmark]
    public JsonFragment SkipArray() => Skip(arrayValue);
    [Benchmark]
    public JsonFragment SkipObject() => Skip(objectValue);
}