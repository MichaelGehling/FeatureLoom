using System.Text.Json.Serialization;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Source-generated System.Text.Json metadata for <see cref="ComplexObject"/>, used to
/// compare the code-gen serialization path against the reflection-based one.
/// </summary>
[JsonSerializable(typeof(ComplexObject))]
[JsonSerializable(typeof(ComplexObject[]))]
public partial class ComplexObjectJsonContext : JsonSerializerContext
{
}
