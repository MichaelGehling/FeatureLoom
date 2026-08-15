using FeatureLoom.Collections;
using System;
using System.Text;
#if !(NETSTANDARD2_0 || NETFRAMEWORK)
using System.Buffers;
using System.Runtime.InteropServices;
#endif
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif

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

#if NET7_0_OR_GREATER
    // Bytes that end a plain ASCII run: the escape marker and every byte that starts a
    // multi-byte UTF-8 sequence (0x80-0xFF). Enables a vectorized scan for the run length.
    static readonly System.Buffers.SearchValues<byte> plainAsciiStoppers = System.Buffers.SearchValues.Create(CreatePlainAsciiStoppers());

    static byte[] CreatePlainAsciiStoppers()
    {
        var stoppers = new byte[129];
        stoppers[0] = (byte)'\\';
        for (int i = 0; i < 128; i++) stoppers[i + 1] = (byte)(0x80 + i);
        return stoppers;
    }
#endif

#if !(NETSTANDARD2_0 || NETFRAMEWORK)
    // Chunk size for stack-based ASCII widening. Keeps the stack usage bounded (512 bytes)
    // while still amortizing the per-append overhead over many characters.
    const int AsciiChunkSize = 256;
    // Above this length the decode target is rented from the array pool instead of the stack.
    const int MaxStackDecodeChars = 512;
#endif

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
        var segment = bytes.AsArraySegment;
        byte[] buffer = segment.Array;
        int count = bytes.Count;

        if (count == 0) return string.Empty;

        if (buffer != null)
        {
            int offset = segment.Offset;
            // Scan for the first byte that needs real decoding work. Mirrors the serializer's
            // run-based strategy: the scan result is never thrown away, it either completes the
            // whole string or marks where the general decoder has to take over.
            int asciiRun = CountPlainAsciiRun(buffer, offset, count);

            if (asciiRun == count) return CreateStringFromAscii(buffer, offset, count);

#if !(NETSTANDARD2_0 || NETFRAMEWORK)
            // Decoding never produces more chars than input bytes, so the output size is known
            // up front. Decode straight into a char span and build the string in a single copy,
            // avoiding the StringBuilder growth (ExpandByABlock) and its extra ToString() copy.
            char[] rented = count > MaxStackDecodeChars ? ArrayPool<char>.Shared.Rent(count) : null;
            try
            {
                Span<char> chars = rented != null
                    ? rented.AsSpan(0, count)
                    : stackalloc char[count];

                int written = DecodeUtf8ToChars(buffer, offset, count, asciiRun, chars);
                return new string(chars.Slice(0, written));
            }
            finally
            {
                if (rented != null) ArrayPool<char>.Shared.Return(rented);
            }
#else
            StringBuilder fastSb;
            if (stringBuilder == null) fastSb = stringBuilderPool.Take();
            else
            {
                stringBuilder.Clear();
                fastSb = stringBuilder;
            }

            fastSb.EnsureCapacity(count);
            // Bulk-append the already scanned ASCII prefix, then decode only the remainder,
            // so no byte is ever inspected twice.
            AppendAsciiRun(fastSb, buffer, offset, asciiRun);
            DecodeUtf8ToStringBuilder(new ByteSegment(buffer, offset + asciiRun, count - asciiRun), fastSb);

            string fastStr = fastSb.ToString();
            if (stringBuilder == null) stringBuilderPool.Return(fastSb);
            else fastSb.Clear();
            return fastStr;
#endif
        }

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

    #if !(NETSTANDARD2_0 || NETFRAMEWORK)
    /// <summary>
    /// Decodes UTF-8 bytes directly into a char span, handling escape sequences.
    /// The already-scanned plain ASCII prefix is bulk-widened, so no byte is inspected twice.
    /// </summary>
    /// <returns>The number of chars written to <paramref name="destination"/>.</returns>
    private static int DecodeUtf8ToChars(byte[] buffer, int offset, int count, int asciiRun, Span<char> destination)
    {
        WidenAsciiToChars(new ReadOnlySpan<byte>(buffer, offset, asciiRun), destination.Slice(0, asciiRun));

        int w = asciiRun;
        int i = offset + asciiRun;
        int end = offset + count;

        while (i < end)
        {
            byte b = buffer[i++];

            if (b == '\\')
            {
                i = HandleEscapeSequence(buffer, i, end, destination, ref w);
            }
            else if (b < 0x80)
            {
                destination[w++] = (char)b;

                // A single non-ASCII byte usually does not mean the rest is non-ASCII too,
                // so re-scan for the next stopper and bulk-widen the run that follows.
                int run = CountPlainAsciiRun(buffer, i, end - i);
                if (run > 0)
                {
                    WidenAsciiToChars(new ReadOnlySpan<byte>(buffer, i, run), destination.Slice(w, run));
                    w += run;
                    i += run;
                }
            }
            else if (b < 0xE0)
            {
                if (i >= end) break;
                byte b2 = buffer[i++];
                destination[w++] = (char)(((b & 0x1F) << 6) | (b2 & 0x3F));
            }
            else if (b < 0xF0)
            {
                if (i + 2 > end) break;
                byte b2 = buffer[i++];
                byte b3 = buffer[i++];
                destination[w++] = (char)(((b & 0x0F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F));
            }
            else
            {
                if (i + 3 > end) break;
                byte b2 = buffer[i++];
                byte b3 = buffer[i++];
                byte b4 = buffer[i++];
                int codepoint = ((b & 0x07) << 18) | ((b2 & 0x3F) << 12) | ((b3 & 0x3F) << 6) | (b4 & 0x3F);

                if (codepoint > 0xFFFF)
                {
                    codepoint -= 0x10000;
                    destination[w++] = (char)(0xD800 | (codepoint >> 10));
                    destination[w++] = (char)(0xDC00 | (codepoint & 0x3FF));
                }
                else
                {
                    destination[w++] = (char)codepoint;
                }
            }
        }

        return w;
    }

    /// <summary>
    /// Span-based counterpart of the StringBuilder escape handling. Writes the decoded
    /// character(s) at <paramref name="w"/> and returns the updated buffer index.
    /// </summary>
    private static int HandleEscapeSequence(byte[] buffer, int i, int end, Span<char> destination, ref int w)
    {
        if (i >= end)
        {
            destination[w++] = '\\';
            return i;
        }

        byte b = buffer[i++];

        switch (b)
        {
            case (byte)'\\': destination[w++] = '\\'; break;
            case (byte)'b': destination[w++] = '\b'; break;
            case (byte)'f': destination[w++] = '\f'; break;
            case (byte)'n': destination[w++] = '\n'; break;
            case (byte)'r': destination[w++] = '\r'; break;
            case (byte)'t': destination[w++] = '\t'; break;
            case (byte)'u':
                if (i + 4 > end)
                {
                    destination[w++] = '\\';
                    destination[w++] = 'u';
                    while (i < end) destination[w++] = (char)buffer[i++];
                    return end;
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
                    destination[w++] = '\\';
                    destination[w++] = 'u';
                    for (int j = 0; j < 4; j++) destination[w++] = (char)buffer[start + j];
                }
                else if (codepoint >= 0xD800 &&
                         codepoint <= 0xDBFF &&
                         i + 6 <= end &&
                         buffer[i] == '\\' &&
                         buffer[i + 1] == 'u')
                {
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
                        destination[w++] = (char)codepoint;
                        destination[w++] = (char)lowSurrogate;
                    }
                    else
                    {
                        destination[w++] = (char)codepoint;
                        destination[w++] = '\\';
                        destination[w++] = 'u';
                        for (int j = 0; j < 4; j++) destination[w++] = (char)buffer[lowStart + j];
                    }
                    i += 6;
                }
                else
                {
                    destination[w++] = (char)codepoint;
                }
                break;
            default:
                destination[w++] = (char)b;
                break;
        }
        return i;
    }
#endif

    /// <summary>
    /// Returns the number of leading bytes that are plain ASCII, i.e. neither part of a
    /// multi-byte sequence nor the start of an escape sequence.
    /// </summary>
    private static int CountPlainAsciiRun(byte[] buffer, int offset, int count)
    {
#if NET7_0_OR_GREATER
        var span = new ReadOnlySpan<byte>(buffer, offset, count);
        int index = span.IndexOfAny(plainAsciiStoppers);
        return index < 0 ? count : index;
#else
        for (int i = 0; i < count; i++)
        {
            byte b = buffer[offset + i];
            if (b >= 0x80 || b == (byte)'\\') return i;
        }
        return count;
#endif
    }

    /// <summary>
    /// Creates a string from a range of bytes that is known to contain only plain ASCII.
    /// </summary>
    private static string CreateStringFromAscii(byte[] buffer, int offset, int count)
    {
#if NETSTANDARD2_0 || NETFRAMEWORK
        return Encoding.ASCII.GetString(buffer, offset, count);
#else
        return string.Create(count, (buffer, offset), static (chars, state) =>
        {
            var (src, srcOffset) = state;
            WidenAsciiToChars(new ReadOnlySpan<byte>(src, srcOffset, chars.Length), chars);
        });
#endif
    }

    /// <summary>
    /// Appends a range of bytes that is known to contain only plain ASCII to a StringBuilder.
    /// </summary>
    private static void AppendAsciiRun(StringBuilder stringBuilder, byte[] buffer, int offset, int count)
    {
#if NETSTANDARD2_0 || NETFRAMEWORK
        for (int i = 0; i < count; i++) stringBuilder.Append((char)buffer[offset + i]);
#else
        // Widen through a small stack chunk, so the run is appended with a few span appends
        // instead of one virtual Append call per character, without allocating a scratch buffer.
        Span<char> chunk = stackalloc char[AsciiChunkSize];
        int done = 0;
        while (done < count)
        {
            int length = Math.Min(AsciiChunkSize, count - done);
            Span<char> target = chunk.Slice(0, length);
            WidenAsciiToChars(new ReadOnlySpan<byte>(buffer, offset + done, length), target);
            stringBuilder.Append(target);
            done += length;
        }
#endif
    }

#if !(NETSTANDARD2_0 || NETFRAMEWORK)
    /// <summary>
    /// Widens a span of ASCII bytes into chars, using the widest available SIMD path.
    /// </summary>
    private static void WidenAsciiToChars(ReadOnlySpan<byte> source, Span<char> destination)
    {
        int i = 0;

#if NET8_0_OR_GREATER
        if (Vector256.IsHardwareAccelerated && source.Length >= Vector256<byte>.Count)
        {
            int limit = source.Length - Vector256<byte>.Count;
            for (; i <= limit; i += Vector256<byte>.Count)
            {
                Vector256<byte> block = Vector256.Create(source.Slice(i, Vector256<byte>.Count));
                // Each 256-bit byte block widens into two 256-bit char blocks.
                (Vector256<ushort> lower, Vector256<ushort> upper) = Vector256.Widen(block);
                lower.CopyTo(MemoryMarshal.Cast<char, ushort>(destination.Slice(i, Vector256<ushort>.Count)));
                upper.CopyTo(MemoryMarshal.Cast<char, ushort>(destination.Slice(i + Vector256<ushort>.Count, Vector256<ushort>.Count)));
            }
        }
        else if (Vector128.IsHardwareAccelerated && source.Length >= Vector128<byte>.Count)
        {
            int limit = source.Length - Vector128<byte>.Count;
            for (; i <= limit; i += Vector128<byte>.Count)
            {
                Vector128<byte> block = Vector128.Create(source.Slice(i, Vector128<byte>.Count));
                (Vector128<ushort> lower, Vector128<ushort> upper) = Vector128.Widen(block);
                lower.CopyTo(MemoryMarshal.Cast<char, ushort>(destination.Slice(i, Vector128<ushort>.Count)));
                upper.CopyTo(MemoryMarshal.Cast<char, ushort>(destination.Slice(i + Vector128<ushort>.Count, Vector128<ushort>.Count)));
            }
        }
#endif

        for (; i < source.Length; i++) destination[i] = (char)source[i];
    }
#endif

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

    /// <summary>
    /// Returns a byte segment obtained from EncodeToUtf8 back to the pool for reuse.
    /// The byte segment will be cleared and set to empty after returning to prevent accidental reuse.
    /// </summary>
    /// <param name="byteSegment">The byte segment to return to the pool.</param>
    public static void ReturnBytesToPool(ref ByteSegment byteSegment)
    {
        ArraySegment<byte> bytes = byteSegment.AsArraySegment;
        sharedSlicedByteBuffer.FreeSlice(ref bytes);
        byteSegment = ByteSegment.Empty;
    }
}
