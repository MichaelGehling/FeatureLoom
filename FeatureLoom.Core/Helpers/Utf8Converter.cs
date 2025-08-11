using FeatureLoom.Collections;
using System;
using System.Text;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides static methods for efficient UTF-8 encoding and decoding between byte segments, strings, and char arrays,
/// including robust handling of escape sequences (e.g., JSON-style \uXXXX, \n, \t, etc.).
/// Utilizes pooled buffers and StringBuilders for high performance and low allocation overhead.
/// </summary>
public static class Utf8Converter
{
    // Shared buffer for pooled char slices to reduce allocations.
    static SlicedBuffer<char> sharedSlicedCharBuffer = new SlicedBuffer<char>(1024, 1024 * 40, 4, true, true);
    // Shared buffer for pooled byte slices to reduce allocations.
    static SlicedBuffer<byte> sharedSlicedByteBuffer = new SlicedBuffer<byte>(1024, 1024 * 80, 4, true, true);
    // Pool for reusing StringBuilder instances.
    static Pool<StringBuilder> stringBuilderPool = new Pool<StringBuilder>(() => new StringBuilder(1024), sb => sb.Clear());

    /// <summary>
    /// Decodes a UTF-8 encoded <see cref="ByteSegment"/> into a <see cref="StringBuilder"/>, handling escape sequences.
    /// </summary>
    /// <param name="bytes">The byte segment containing UTF-8 encoded data.</param>
    /// <param name="stringBuilder">The StringBuilder to append decoded characters to.</param>
    public static void DecodeUtf8ToStringBuilder(this ByteSegment bytes, StringBuilder stringBuilder)
    {
        stringBuilder.EnsureCapacity(bytes.Count);

        int i = bytes.AsArraySegment.Offset;
        int end = bytes.AsArraySegment.Offset + bytes.Count;
        var buffer = bytes.AsArraySegment.Array;

        while (i < end)
        {
            byte b = buffer[i++];

            // Handle escape sequences (e.g., \n, \uXXXX, etc.)
            if (b == '\\')
            {
                i = HandleEscapeSequence(buffer, i, end, stringBuilder);
            }
            // ASCII fast path
            else if (b < 0x80)
            {
                stringBuilder.Append((char)b);
            }
            // 2-byte UTF-8 sequence
            else if (b < 0xE0)
            {
                if (i >= end) break; // Prevent overrun
                byte b2 = buffer[i++];
                stringBuilder.Append((char)(((b & 0x1F) << 6) | (b2 & 0x3F)));
            }
            // 3-byte UTF-8 sequence
            else if (b < 0xF0)
            {
                if (i + 2 > end) break; // Prevent overrun
                byte b2 = buffer[i++];
                byte b3 = buffer[i++];
                stringBuilder.Append((char)(((b & 0x0F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F)));
            }
            // 4-byte UTF-8 sequence (produces surrogate pair)
            else
            {
                if (i + 3 > end) break; // Prevent overrun
                byte b2 = buffer[i++];
                byte b3 = buffer[i++];
                byte b4 = buffer[i++];
                int codepoint = ((b & 0x07) << 18) | ((b2 & 0x3F) << 12) | ((b3 & 0x3F) << 6) | (b4 & 0x3F);

                if (codepoint > 0xFFFF)
                {
                    codepoint -= 0x10000;
                    stringBuilder.Append((char)(0xD800 | (codepoint >> 10)));
                    stringBuilder.Append((char)(0xDC00 | (codepoint & 0x3FF)));
                }
                else
                {
                    stringBuilder.Append((char)codepoint);
                }
            }
        }
    }

    /// <summary>
    /// Handles escape sequences (e.g., \n, \t, \uXXXX, etc.) in the UTF-8 decoding process.
    /// Advances the buffer index as needed and appends the decoded character(s) to the StringBuilder.
    /// </summary>
    /// <param name="buffer">The byte buffer being decoded.</param>
    /// <param name="i">The current index in the buffer (points to the character after the backslash).</param>
    /// <param name="end">The end index of the buffer.</param>
    /// <param name="stringBuilder">The StringBuilder to append decoded characters to.</param>
    /// <returns>The updated index after processing the escape sequence.</returns>
    private static int HandleEscapeSequence(byte[] buffer, int i, int end, StringBuilder stringBuilder)
    {
        if (i >= end)
        {
            // Lone backslash at end of input: treat as literal backslash.
            stringBuilder.Append('\\');
            return i;
        }

        byte b = buffer[i++];

        switch (b)
        {
            case (byte)'\\':
                stringBuilder.Append('\\');
                break;
            case (byte)'b':
                stringBuilder.Append('\b');
                break;
            case (byte)'f':
                stringBuilder.Append('\f');
                break;
            case (byte)'n':
                stringBuilder.Append('\n');
                break;
            case (byte)'r':
                stringBuilder.Append('\r');
                break;
            case (byte)'t':
                stringBuilder.Append('\t');
                break;
            case (byte)'u':
                // Unicode escape sequence: \uXXXX
                if (i + 4 > end)
                {
                    // Not enough bytes for a full escape, append as literal
                    stringBuilder.Append("\\u");
                    while (i < end) stringBuilder.Append((char)buffer[i++]);
                    return end; // Consumed all remaining bytes
                }
                int codepoint = 0;
                int start = i;
                int invalidAt = -1;
                for (int j = 0; j < 4; j++)
                {
                    byte hex = buffer[i++];
                    codepoint <<= 4;
                    if (hex >= '0' && hex <= '9') codepoint |= (hex - '0');
                    else if (hex >= 'A' && hex <= 'F') codepoint |= (hex - 'A' + 10);
                    else if (hex >= 'a' && hex <= 'f') codepoint |= (hex - 'a' + 10);
                    else if (invalidAt == -1) invalidAt = j;
                }
                if (invalidAt != -1)
                {
                    // At least one invalid digit: write \u and all 4 bytes as chars
                    stringBuilder.Append("\\u");
                    for (int j = 0; j < 4; j++) stringBuilder.Append((char)buffer[start + j]);
                }
                else
                {
                    // Valid codepoint, handle surrogate pairs as before
                    if (codepoint >= 0xD800 &&
                        codepoint <= 0xDBFF &&
                        i + 6 <= end &&
                        buffer[i] == '\\' &&
                        buffer[i + 1] == 'u')
                    {
                        // Try to decode the low surrogate
                        int lowSurrogate = 0;
                        int lowStart = i + 2;
                        bool lowValid = true;
                        for (int j = 0; j < 4; j++)
                        {
                            byte hex = buffer[lowStart + j];
                            lowSurrogate <<= 4;
                            if (hex >= '0' && hex <= '9') lowSurrogate |= (hex - '0');
                            else if (hex >= 'A' && hex <= 'F') lowSurrogate |= (hex - 'A' + 10);
                            else if (hex >= 'a' && hex <= 'f') lowSurrogate |= (hex - 'a' + 10);
                            else { lowValid = false; break; }
                        }
                        if (lowValid && lowSurrogate >= 0xDC00 && lowSurrogate <= 0xDFFF)
                        {
                            // Valid surrogate pair: append as a single Unicode character
                            int fullCodepoint = 0x10000 + ((codepoint - 0xD800) << 10) + (lowSurrogate - 0xDC00);
                            stringBuilder.Append(char.ConvertFromUtf32(fullCodepoint));
                            i += 6; // Skip over the low surrogate
                        }
                        else
                        {
                            // Not a valid surrogate pair: append high surrogate as char,
                            // then append the literal \uXXXX for the low surrogate and skip it
                            stringBuilder.Append((char)codepoint);
                            stringBuilder.Append("\\u");
                            for (int j = 0; j < 4; j++) stringBuilder.Append((char)buffer[lowStart + j]);
                            i += 6; // Skip over the low surrogate
                        }
                    }
                    else
                    {
                        stringBuilder.Append((char)codepoint);
                    }
                }
                break;
            default:
                // Unknown escape: treat as literal character
                stringBuilder.Append((char)b);
                break;
        }
        return i;
    }

    /// <summary>
    /// Decodes a UTF-8 encoded <see cref="ByteSegment"/> into a string, handling escape sequences.
    /// </summary>
    /// <param name="bytes">The byte segment containing UTF-8 encoded data.</param>
    /// <param name="stringBuilder">Optional StringBuilder to use for decoding (for pooling/reuse). Will be cleared before and after.</param>
    /// <returns>The decoded string.</returns>
    public static string DecodeUtf8ToString(this ByteSegment bytes, StringBuilder stringBuilder = null)
    {
        StringBuilder sb;
        if (stringBuilder == null) sb = stringBuilderPool.Take();
        else
        {
            stringBuilder.Clear();
            sb = stringBuilder;
        }

        DecodeUtf8ToStringBuilder(bytes, sb);
        string str = sb.ToString();

        if (stringBuilder == null) stringBuilderPool.Return(sb);
        else sb.Clear();

        return str;
    }

    /// <summary>
    /// Decodes a UTF-8 encoded <see cref="ByteSegment"/> into a pooled char array segment, handling escape sequences.
    /// </summary>
    /// <param name="bytes">The byte segment containing UTF-8 encoded data.</param>
    /// <param name="stringBuilder">Optional StringBuilder to use for decoding (for pooling/reuse). Will be cleared before and after.</param>
    /// <param name="slicedBuffer">Optional SlicedBuffer to use for char array allocation.</param>
    /// <returns>An ArraySegment of chars containing the decoded characters.</returns>
    public static ArraySegment<char> DecodeUtf8ToChars(this ByteSegment bytes, StringBuilder stringBuilder = null, SlicedBuffer<char> slicedBuffer = null)
    {
        StringBuilder sb;
        if (stringBuilder == null) sb = stringBuilderPool.Take();
        else
        {
            stringBuilder.Clear();
            sb = stringBuilder;
        }

        DecodeUtf8ToStringBuilder(bytes, sb);

        ArraySegment<char> chars;
        if (slicedBuffer == null)
        {
            chars = sharedSlicedCharBuffer.GetSlice(sb.Length);
        }
        else
        {
            chars = slicedBuffer.GetSlice(sb.Length);
        }
        sb.CopyTo(0, chars.Array, chars.Offset, sb.Length);

        if (stringBuilder == null) stringBuilderPool.Return(sb);
        else sb.Clear();

        return chars;
    }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    /// <summary>
    /// Decodes a UTF-8 encoded <see cref="ByteSegment"/> into a ReadOnlySpan of chars, handling escape sequences.
    /// </summary>
    /// <param name="bytes">The byte segment containing UTF-8 encoded data.</param>
    /// <param name="stringBuilder">Optional StringBuilder to use for decoding (for pooling/reuse). Will be cleared before and after.</param>
    /// <param name="slicedBuffer">Optional SlicedBuffer to use for char array allocation.</param>
    /// <returns>A ReadOnlySpan of chars containing the decoded characters.</returns>
    public static ReadOnlySpan<char> DecodeUtf8ToSpanOfChars(this ByteSegment bytes, StringBuilder stringBuilder = null, SlicedBuffer<char> slicedBuffer = null)
    {
        return DecodeUtf8ToChars(bytes, stringBuilder, slicedBuffer);
    }
#endif

    /// <summary>
    /// Encodes a string as UTF-8 into a pooled byte array segment.
    /// </summary>
    /// <param name="str">The string to encode.</param>
    /// <param name="slicedBuffer">Optional SlicedBuffer to use for byte array allocation.</param>
    /// <returns>An ArraySegment of bytes containing the UTF-8 encoded data.</returns>
    public static ByteSegment EncodeToUtf8(this string str, SlicedBuffer<byte> slicedBuffer = null)
    {
        if (slicedBuffer == null) slicedBuffer = sharedSlicedByteBuffer;
        ArraySegment<byte> bytes = slicedBuffer.GetSlice(str.Length);
        int bytesCount = Encoding.UTF8.GetBytes(str, 0, str.Length, bytes.Array, bytes.Offset);
        slicedBuffer.ResizeSlice(ref bytes, bytesCount);
        return bytes;
    }

    /// <summary>
    /// Encodes a <see cref="TextSegment"/> as UTF-8 into a pooled byte array segment.
    /// </summary>
    /// <param name="text">The text segment to encode.</param>
    /// <param name="slicedBuffer">Optional SlicedBuffer to use for byte array allocation.</param>
    /// <returns>An ArraySegment of bytes containing the UTF-8 encoded data.</returns>
    public static ByteSegment EncodeToUtf8(this TextSegment text, SlicedBuffer<byte> slicedBuffer = null)
    {
        if (slicedBuffer == null) slicedBuffer = sharedSlicedByteBuffer;
        ArraySegment<byte> bytes = slicedBuffer.GetSlice(text.Count);
        int bytesCount = Encoding.UTF8.GetBytes(text.UnderlyingString, text.Offset, text.Count, bytes.Array, bytes.Offset);
        slicedBuffer.ResizeSlice(ref bytes, bytesCount);
        return bytes;
    }
}
