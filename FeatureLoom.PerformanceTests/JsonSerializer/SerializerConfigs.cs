using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Central serializer configuration for all benchmarks.
/// <para>
/// A throughput comparison is only meaningful if all serializers produce equivalent
/// output, because every deviation changes the number of written bytes and the used code
/// path. The defaults of the three serializers differ, so they are aligned here:
/// </para>
/// <list type="bullet">
/// <item>Escaping: System.Text.Json escapes '"' as the 6-char '\u0022' by default, while
/// the others write '\"'. The relaxed encoder makes all of them write the short form.</item>
/// <item>Null members: SpanJson omits them by default, the others write them. The
/// include-nulls resolver makes all of them write the member.</item>
/// <item>Enums: SpanJson writes the enum name, the others write the numeric value.
/// Enum-as-string is used everywhere, since that is SpanJson's only built-in behavior.</item>
/// </list>
/// <para>
/// One difference cannot be aligned: byte arrays. FeatureLoom and System.Text.Json write
/// them as a base64 string, SpanJson writes them as a JSON number array. There is no
/// built-in switch for either side, so byte array results of SpanJson are not directly
/// comparable and must be interpreted with that in mind.
/// </para>
/// </summary>
public static class SerializerConfigs
{
    public static Serialization.JsonSerializer CreateFeatureSerializer() =>
        new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings()
        {
            typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
            enumAsString = true,
        });

    public static JsonSerializerOptions CreateSystemTextOptions()
    {
        var options = new JsonSerializerOptions()
        {
            IncludeFields = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>
    /// Options matching <see cref="CreateSystemTextOptions"/>, but backed by the
    /// source-generated <see cref="ComplexObjectJsonContext"/> instead of reflection-based
    /// metadata, so the code-gen serialization path can be compared against it.
    /// </summary>
    public static JsonSerializerOptions CreateSystemTextSourceGenOptions()
    {
        var options = new JsonSerializerOptions()
        {
            IncludeFields = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.TypeInfoResolver = ComplexObjectJsonContext.Default;
        return options;
    }

    /// <summary>
    /// Creates a deserializer with default settings for the value-type benchmarks. The
    /// byte array reader auto-detects base64 vs. number-array input, so a single instance
    /// handles both byte array representations.
    /// </summary>
    public static JsonDeserializer CreateFeatureDeserializer() => new JsonDeserializer();

#if NET6_0_OR_GREATER
    /// <summary>
    /// Serializes with SpanJson using the include-nulls resolver, so its output matches
    /// the other serializers.
    /// </summary>
    public static void SerializeWithSpanJson<T>(T value, Stream stream) =>
        SpanJson.JsonSerializer.Generic.Utf8.SerializeAsync<T, SpanJson.Resolvers.IncludeNullsOriginalCaseResolver<byte>>(value, stream).GetAwaiter().GetResult();

    /// <summary>
    /// Deserializes with SpanJson using the include-nulls resolver, matching
    /// <see cref="SerializeWithSpanJson{T}"/>.
    /// </summary>
    public static T DeserializeWithSpanJson<T>(Stream stream) =>
        SpanJson.JsonSerializer.Generic.Utf8.DeserializeAsync<T, SpanJson.Resolvers.IncludeNullsOriginalCaseResolver<byte>>(stream).GetAwaiter().GetResult();
#endif

    /// <summary>
    /// Variant that writes byte arrays as a JSON number array instead of a base64 string,
    /// so the byte array benchmark can compare both formats against SpanJson.
    /// </summary>
    public static Serialization.JsonSerializer CreateFeatureSerializerWithByteArrayAsNumbers()
    {
        var settings = new Serialization.JsonSerializer.Settings()
        {
            typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
            enumAsString = true,
            writeByteArrayAsBase64String = false,
        };
        return new Serialization.JsonSerializer(settings);
    }

    /// <summary>
    /// Variant that writes byte arrays as a JSON number array. System.Text.Json has no
    /// built-in switch for this, so a converter is used.
    /// </summary>
    public static JsonSerializerOptions CreateSystemTextOptionsWithByteArrayAsNumbers()
    {
        var options = CreateSystemTextOptions();
        options.Converters.Add(new ByteArrayAsNumbersConverter());
        return options;
    }

    private sealed class ByteArrayAsNumbersConverter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var bytes = new List<byte>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) bytes.Add(reader.GetByte());
            return bytes.ToArray();
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            for (int i = 0; i < value.Length; i++) writer.WriteNumberValue(value[i]);
            writer.WriteEndArray();
        }
    }
}
