using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using System;

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{
    /// <summary>
    /// Provides a temporary, non-owning view into the deserializer's current input buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="BufferSegment"/> and the data exposed by <see cref="Bytes"/> or <see cref="Span"/>
    /// are valid only for the duration of the callback receiving it. Do not retain, store, or use
    /// the segment or its underlying data after that callback returns.
    /// </para>
    /// <para>
    /// For JSON strings, the bytes exclude the surrounding quotation marks and retain JSON escape
    /// sequences. <see cref="AsString"/> decodes UTF-8 and JSON escape sequences on demand.
    /// </para>
    /// </remarks>
    public readonly struct BufferSegment
    {
        readonly ByteSegment bytes;

        internal BufferSegment(ByteSegment bytes)
        {
            this.bytes = bytes;
        }

        /// <summary>
        /// Gets the temporary buffer region without allocating.
        /// </summary>
        /// <remarks>
        /// The returned segment references the deserializer's reusable input buffer and must not be
        /// retained or used after the callback receiving this <see cref="BufferSegment"/> returns.
        /// </remarks>
        public ByteSegment Bytes => bytes;

#if !NETSTANDARD2_0
        /// <summary>
        /// Gets the temporary buffer region as a read-only span without allocating.
        /// </summary>
        /// <remarks>
        /// The returned span is valid only until the callback receiving this <see cref="BufferSegment"/> returns.
        /// </remarks>
        public ReadOnlySpan<byte> Span => bytes.AsSpan();
#endif

        /// <summary>
        /// Decodes the buffered UTF-8 JSON string content, including JSON escape sequences, into a new string.
        /// </summary>
        public string AsString() => Utf8Converter.DecodeUtf8ToString(bytes);

        /// <summary>Returns the decoded string content.</summary>
        public override string ToString() => AsString();
    }
}
