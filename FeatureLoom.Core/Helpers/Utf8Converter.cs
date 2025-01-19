using FeatureLoom.Collections;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.Helpers;

public static class Utf8Converter
{
    static SlicedBuffer<char> sharedSlicedBuffer = new SlicedBuffer<char>(1024, 1024);
    static MicroLock sharedBufferLock = new MicroLock();
    static Pool<StringBuilder> stringBuilderPool = new Pool<StringBuilder>(() => new StringBuilder(1024), sb => sb.Clear());

    public static void DecodeUtf8ToStringBuilder(ArraySegment<byte> bytes, StringBuilder stringBuilder)
    {
        stringBuilder.EnsureCapacity(bytes.Count);

        int i = bytes.Offset;
        int end = bytes.Offset + bytes.Count;
        var buffer = bytes.Array;

        while (i < end)
        {
            byte b = buffer[i++];

            if (b == '\\' && i < end)
            {
                b = buffer[i++];

                if (b == 'b') stringBuilder.Append('\b');
                else if (b == 'f') stringBuilder.Append('\f');
                else if (b == 'n') stringBuilder.Append('\n');
                else if (b == 'r') stringBuilder.Append('\r');
                else if (b == 't') stringBuilder.Append('\t');
                else if (b == 'u')
                {
                    if (i + 4 > end) stringBuilder.Append((char)b);
                    else
                    {
                        byte b1 = buffer[i++];
                        byte b2 = buffer[i++];
                        byte b3 = buffer[i++];
                        byte b4 = buffer[i++];
                        int codepoint = ((b1 & 0x07) << 18) | ((b2 & 0x3F) << 12) | ((b3 & 0x3F) << 6) | (b4 & 0x3F);
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
                else stringBuilder.Append((char)b);
            }
            else if (b < 0x80)
            {
                stringBuilder.Append((char)b);
            }
            else if (b < 0xE0)
            {
                byte b2 = buffer[i++];
                stringBuilder.Append((char)(((b & 0x1F) << 6) | (b2 & 0x3F)));
            }
            else if (b < 0xF0)
            {
                byte b2 = buffer[i++];
                byte b3 = buffer[i++];
                stringBuilder.Append((char)(((b & 0x0F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F)));
            }
            else
            {
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

    public static string DecodeUtf8ToString(ArraySegment<byte> bytes, StringBuilder stringBuilder = null)
    {
        if (stringBuilder == null) stringBuilder = new StringBuilder(bytes.Count);        
        DecodeUtf8ToStringBuilder(bytes, stringBuilder);
        return stringBuilder.ToString();        
    }

    public static ArraySegment<char> DecodeUtf8ToChars(ArraySegment<byte> bytes, StringBuilder stringBuilder = null, SlicedBuffer<char> slicedBuffer = null)
    {
        if (stringBuilder == null) stringBuilder = stringBuilderPool.Take();        
        
        DecodeUtf8ToStringBuilder(bytes, stringBuilder);
                
        ArraySegment<char> chars;
        if (slicedBuffer == null)
        {
            using (sharedBufferLock.Lock())
            {
                chars = sharedSlicedBuffer.GetSlice(stringBuilder.Length);
            }
        }
        else
        {
            chars = slicedBuffer.GetSlice(stringBuilder.Length);
        }
        stringBuilder.CopyTo(0, chars.Array, chars.Offset, stringBuilder.Length);
        return chars;
    }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
    public static ReadOnlySpan<char> DecodeUtf8ToSpanOfChars(ArraySegment<byte> bytes, StringBuilder stringBuilder = null, SlicedBuffer<char> slicedBuffer = null)
    {
        return DecodeUtf8ToChars(bytes, stringBuilder, slicedBuffer);
    }
#endif
}
