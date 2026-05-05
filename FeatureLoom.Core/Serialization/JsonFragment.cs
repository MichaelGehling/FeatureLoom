using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Serialization;

/// <summary>
/// Represents a JSON fragment that can be stored as either a UTF-16 <see cref="string"/>
/// or UTF-8 encoded <see cref="byte"/> array and converted on demand.
/// </summary>
/// <remarks>
/// <para>
/// This type keeps either string or UTF-8 data and may lazily convert between representations
/// for efficient access.
/// </para>
/// <para>
/// <b>Warning:</b> If constructed from a <see cref="byte"/> array, the underlying array reference
/// may be exposed via <see cref="JsonUtf8"/>. Mutating that array after construction can change
/// equality behavior and invalidate cached hash assumptions.
/// </para>
/// <para>
/// Treat externally provided or retrieved UTF-8 arrays as immutable while a <see cref="JsonFragment"/>
/// instance is in use (especially as a key in hash-based collections).
/// </para>
/// </remarks>
public struct JsonFragment : IEquatable<JsonFragment>
{
    /// <summary>
    /// Initializes a new instance from a JSON string.
    /// </summary>
    /// <param name="jsonString">The JSON content as a string.</param>
    public JsonFragment(string jsonString)
    {
        jsonData = jsonString;
    }

    /// <summary>
    /// Initializes a new instance from UTF-8 encoded JSON bytes.
    /// </summary>
    /// <param name="jsonUtf8">The JSON content encoded as UTF-8 bytes.</param>
    public JsonFragment(byte[] jsonUtf8)
    {
        jsonData = jsonUtf8;
    }

    object jsonData;
    int? hash;

    /// <summary>
    /// Gets a value indicating whether the fragment is currently stored as UTF-8 bytes.
    /// </summary>
    public bool IsUtf8 => jsonData is byte[];

    /// <summary>
    /// Gets a value indicating whether the fragment is currently stored as a string.
    /// </summary>
    public bool IsString => jsonData is string;

    /// <summary>
    /// Gets a value indicating whether the fragment contains data.
    /// </summary>
    public bool IsValid => jsonData != null;

    /// <summary>
    /// Gets the JSON fragment as a string.
    /// When the current storage is UTF-8 bytes, it is decoded and cached as a string.
    /// </summary>
    public string JsonString
    {
        get
        {
            if (jsonData is string str) return str;
            if (jsonData is byte[] bytes)
            {
                str = Utf8Converter.DecodeUtf8ToString(bytes);
                jsonData = str;
                return str;
            }
            return "";
        }
    }

    /// <summary>
    /// Gets the JSON fragment as UTF-8 bytes.
    /// When the current storage is a string, it is encoded and cached as bytes.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> This returns the underlying array reference when data is already stored as UTF-8.
    /// Mutating the returned array may break equality/hash stability for this value.
    /// </remarks>
    public byte[] JsonUtf8
    {
        get
        {
            if (jsonData is byte[] bytes) return bytes;
            if (jsonData is string str)
            {
                bytes = Encoding.UTF8.GetBytes(str);
                jsonData = bytes;
                return bytes;
            }
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    /// Determines whether this instance and another <see cref="JsonFragment"/> are equal.
    /// </summary>
    /// <param name="other">The fragment to compare with this instance.</param>
    /// <returns>
    /// <see langword="true"/> if both fragments are invalid, or both are valid and contain equal JSON bytes;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(JsonFragment other)
    {
        if (!IsValid && !other.IsValid) return true;
        if (!IsValid || !other.IsValid) return false;
        if (IsString && other.IsString) return JsonString == other.JsonString;

        ByteSegment myBytes = IsUtf8 ? JsonUtf8 : Utf8Converter.EncodeToUtf8(JsonString);
        ByteSegment otherBytes = other.IsUtf8 ? other.JsonUtf8 : Utf8Converter.EncodeToUtf8(other.JsonString);
        bool equal = myBytes.Equals(otherBytes);

        // Return pooled byte arrays if we had to encode them for comparison
        if (!other.IsUtf8) Utf8Converter.ReturnBytesToPool(ref otherBytes);
        if (!IsUtf8) Utf8Converter.ReturnBytesToPool(ref myBytes);

        return equal;
    }

    /// <summary>
    /// Determines whether this instance and the specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns><see langword="true"/> if the specified object is an equal <see cref="JsonFragment"/>; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object obj) => obj is JsonFragment other && Equals(other);

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(JsonFragment)"/>.
    /// </summary>
    /// <returns>A hash code for the current fragment.</returns>
    /// <remarks>
    /// The hash is normalized over UTF-8 content and cached after first computation.
    /// <b>Warning:</b> mutating externally shared UTF-8 arrays after hash computation can make the cached
    /// hash inconsistent with current byte content.
    /// </remarks>
    public override int GetHashCode()
    {
        if (!IsValid) return 0;
        if (this.hash.HasValue) return this.hash.Value;

        ByteSegment bytes = IsUtf8 ? JsonUtf8 : Utf8Converter.EncodeToUtf8(JsonString);
        hash = bytes.GetHashCode();

        if (!IsUtf8) Utf8Converter.ReturnBytesToPool(ref bytes);

        return hash.Value;
    }

    /// <summary>
    /// Returns the JSON fragment as a string.
    /// </summary>
    /// <returns>The JSON content as a string.</returns>
    public override string ToString() => JsonString;

    /// <summary>
    /// Compares two <see cref="JsonFragment"/> values for equality.
    /// </summary>
    public static bool operator ==(JsonFragment left, JsonFragment right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="JsonFragment"/> values for inequality.
    /// </summary>
    public static bool operator !=(JsonFragment left, JsonFragment right) => !left.Equals(right);

    /// <summary>
    /// Converts a <see cref="JsonFragment"/> to its string representation.
    /// </summary>
    /// <param name="fragment">The JSON fragment.</param>
    public static implicit operator string(JsonFragment fragment) => fragment.JsonString;

    /// <summary>
    /// Converts a <see cref="JsonFragment"/> to its UTF-8 byte representation.
    /// </summary>
    /// <param name="fragment">The JSON fragment.</param>
    /// <remarks>
    /// <b>Warning:</b> The returned array may be the internal backing array. Do not mutate it unless
    /// mutation side effects are explicitly intended.
    /// </remarks>
    public static implicit operator byte[](JsonFragment fragment) => fragment.JsonUtf8;

    /// <summary>
    /// Converts a JSON string to a <see cref="JsonFragment"/>.
    /// </summary>
    /// <param name="jsonString">The JSON content as a string.</param>
    public static implicit operator JsonFragment(string jsonString) => new JsonFragment(jsonString);

    /// <summary>
    /// Converts UTF-8 JSON bytes to a <see cref="JsonFragment"/>.
    /// </summary>
    /// <param name="jsonUtf8">The JSON content encoded as UTF-8 bytes.</param>
    public static implicit operator JsonFragment(byte[] jsonUtf8) => new JsonFragment(jsonUtf8);
}
