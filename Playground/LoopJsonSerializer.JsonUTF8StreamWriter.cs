using System.Text;
using FeatureLoom.Extensions;
using System.IO;
using System.Runtime.CompilerServices;
using System;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using FeatureLoom.Helpers;
using System.Reflection;

namespace Playground
{
    public sealed partial class LoopJsonSerializer
    {
        private sealed class JsonUTF8StreamWriter
        {
            public Stream stream;
            private Encoder encoder;
            private static byte[] buffer;

            public JsonUTF8StreamWriter()
            {
                this.encoder = Encoding.UTF8.GetEncoder();
                buffer = new byte[1024];
            }

            public override string ToString() => stream.ReadToString();


            static readonly byte[] NULL = "null".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteNullValue() => stream.Write(NULL, 0, NULL.Length);

            static readonly byte[] OPEN_OBJECT = "{".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenObject() => stream.Write(OPEN_OBJECT, 0, OPEN_OBJECT.Length);

            static readonly byte[] CLOSE_OBJECT = "}".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseObject() => stream.Write(CLOSE_OBJECT, 0, CLOSE_OBJECT.Length);

            static readonly byte[] TYPEINFO_PRE = "\"$type\":\"".ToByteArray();
            static readonly byte[] TYPEINFO_POST = "\"".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteTypeInfo(string typeName)
            {
                stream.Write(TYPEINFO_PRE, 0, TYPEINFO_PRE.Length);
                WriteString(typeName);
                stream.Write(TYPEINFO_POST, 0, TYPEINFO_POST.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareTypeInfo(string typeName)
            {
                return $"\"$type\":\"{typeName}\"".ToByteArray();
            }

            static readonly byte[] VALUEFIELDNAME = "\"$value\":".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteValueFieldName() => stream.Write(VALUEFIELDNAME, 0, VALUEFIELDNAME.Length);

            static readonly byte[] FIELDNAME_PRE = "\"".ToByteArray();
            static readonly byte[] FIELDNAME_POST = "\":".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteFieldName(string fieldName)
            {
                stream.Write(FIELDNAME_PRE, 0, FIELDNAME_PRE.Length);
                WriteString(fieldName);
                stream.Write(FIELDNAME_POST, 0, FIELDNAME_POST.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue<T>(T value)
            {                
                WriteString(value.ToString());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString<T>(T value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteString(value.ToString());
                stream.Write(QUOTES, 0, QUOTES.Length);
            }
           
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(long value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(long value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteSignedInteger(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(ulong value)
            {
                WriteUnsignedInteger((long)value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(ulong value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteUnsignedInteger((long)value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(int value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(int value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteSignedInteger(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(uint value)
            {
                WriteUnsignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(uint value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteUnsignedInteger(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(byte value)
            {
                WriteUnsignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(byte value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteUnsignedInteger(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(sbyte value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(sbyte value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteSignedInteger(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(short value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(short value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteSignedInteger(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(ushort value)
            {
                WriteUnsignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(ushort value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteUnsignedInteger(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(float value)
            {
                WriteFloat(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(float value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteFloat(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(double value)
            {
                WriteFloat(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(double value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteFloat(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            static readonly byte[] BOOLVALUE_TRUE = "true".ToByteArray();
            static readonly byte[] BOOLVALUE_FALSE = "false".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(bool value)
            {
                stream.Write(value ? BOOLVALUE_TRUE : BOOLVALUE_FALSE);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(bool value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                stream.Write(value ? BOOLVALUE_TRUE : BOOLVALUE_FALSE);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            static readonly byte[] QUOTES = "\"".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(string str)
            {
                if (str != null)
                {
                    stream.Write(QUOTES, 0, QUOTES.Length);
                    WriteEscapedString(str);
                    stream.Write(QUOTES, 0, QUOTES.Length);
                }
                else WriteNullValue();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(string str)
            {
                WritePrimitiveValue(str);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(char value)
            {
                stream.Write(QUOTES, 0, QUOTES.Length);
                WriteChar(value);
                stream.Write(QUOTES, 0, QUOTES.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(char value)
            {
                WritePrimitiveValue(value);
            }

            static readonly byte[] REFOBJECT_PRE = "{\"$ref\":\"".ToByteArray();
            static readonly byte[] REFOBJECT_POST = "\"}".ToByteArray();            
            Stack<BaseJob> reverseJobStack = new Stack<BaseJob>();
            public void WriteRefObject(BaseJob job)
            {
                while (job != null)
                {
                    reverseJobStack.Push(job);
                    job = job.parentJob;
                }

                stream.Write(REFOBJECT_PRE, 0, REFOBJECT_PRE.Length);
                bool first = true;
                while(reverseJobStack.TryPop(out job))
                {
                    if (first) first = false;
                    else if (job.itemName[0] != OPENCOLLECTION[0]) WriteDot();
                    stream.Write(job.itemName);                    
                }
                stream.Write(REFOBJECT_POST, 0, REFOBJECT_POST.Length);
            }

            static readonly byte[] OPENCOLLECTION = "[".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenCollection() => stream.Write(OPENCOLLECTION, 0, OPENCOLLECTION.Length);

            static readonly byte[] CLOSECOLLECTION = "]".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseCollection() => stream.Write(CLOSECOLLECTION, 0, CLOSECOLLECTION.Length);

            static readonly byte[] COMMA = ",".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteComma() => stream.Write(COMMA, 0, COMMA.Length);

            static readonly byte[] DOT = ".".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteDot() => stream.Write(DOT, 0, DOT.Length);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePreparedByteString(byte[] bytes) => stream.Write(bytes);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareFieldNameBytes(string fieldname)
            {
                return Encoding.UTF8.GetBytes($"\"{fieldname}\":");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareStringToBytes(string str)
            {
                return Encoding.UTF8.GetBytes(str);
            }

            static readonly byte[] ROOT = "$".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareRootName() => ROOT;

            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareEnumTextToBytes(string enumText)
            {
                return Encoding.UTF8.GetBytes($"\"{enumText}\"");
            }

            List<byte[]> indexNameList = new List<byte[]>();
            public byte[] PrepareCollectionIndexName(BaseJob parentJob)
            {
                int index = parentJob.currentIndex;
                for (int i = 0; i <= index; i++)
                {
                    if (indexNameList.Count <= i) indexNameList.Add(null);
                    if (index == i)
                    {
                        if (indexNameList[i] == null) indexNameList[i] = $"[{index}]".ToByteArray();                        
                    }
                }
                return indexNameList[index];
            }

            private static readonly byte[][] EscapeByteLookup = InitEscapeByteLookup();
            private static byte[][] InitEscapeByteLookup()
            {
                byte[][] lookup = new byte[128][];
                string escapeChars = "\\\"\b\f\n\r\t";
                for (int i = 0; i < escapeChars.Length; i++)
                {
                    char c = escapeChars[i];
                    lookup[c] = new byte[] { (byte)'\\', (byte)escapeChars[i] };
                }

                // Special handling for characters that don't map directly to their escape sequence
                lookup['\b'] = new byte[] { (byte)'\\', (byte)'b' };
                lookup['\f'] = new byte[] { (byte)'\\', (byte)'f' };
                lookup['\n'] = new byte[] { (byte)'\\', (byte)'n' };
                lookup['\r'] = new byte[] { (byte)'\\', (byte)'r' };
                lookup['\t'] = new byte[] { (byte)'\\', (byte)'t' };

                // Handling for control characters
                for (int i = 0; i < 0x20; i++)
                {
                    if (lookup[i] == null) // If not already set by the escape sequences above
                    {
                        string unicodeEscape = "\\u" + i.ToString("X4");
                        lookup[i] = Encoding.ASCII.GetBytes(unicodeEscape);
                    }
                }

                return lookup;
            }

            private void WriteChar(char c)
            {
                // Check if the character is in the EscapeByteLookup table
                if (c < EscapeByteLookup.Length && EscapeByteLookup[c] != null)
                {
                    byte[] escapeBytes = EscapeByteLookup[c];
                    stream.Write(escapeBytes, 0, escapeBytes.Length);
                    return;
                }

                int codepoint = c;

                if (codepoint <= 0x7F)
                {
                    // 1-byte sequence
                    stream.WriteByte((byte)codepoint);
                }
                else if (codepoint <= 0x7FF)
                {
                    // 2-byte sequence
                    stream.WriteByte((byte)(((codepoint >> 6) & 0x1F) | 0xC0));
                    stream.WriteByte((byte)((codepoint & 0x3F) | 0x80));
                }
                else if (!char.IsSurrogate(c))
                {
                    // 3-byte sequence
                    stream.WriteByte((byte)(((codepoint >> 12) & 0x0F) | 0xE0));
                    stream.WriteByte((byte)(((codepoint >> 6) & 0x3F) | 0x80));
                    stream.WriteByte((byte)((codepoint & 0x3F) | 0x80));
                }
                else
                {
                    // Handle surrogate by writing it as a Unicode escape sequence
                    WriteString("\\u" + ((int)c).ToString("X4"));
                }
            }

            private void WriteEscapedString(string str)
            {
                int bufferIndex = 0;

                int charIndex = 0;
                int minCharSpace = buffer.Length / 6;
                while (charIndex < str.Length)
                {
                    int charIndexLimit = Math.Min(str.Length, charIndex + minCharSpace);

                    for (; charIndex < charIndexLimit; charIndex++)
                    {
                        var c = str[charIndex];

                        // Handle escaped chars and control chars
                        if (c < EscapeByteLookup.Length)
                        {
                            byte[] escapeBytes = EscapeByteLookup[c];
                            if (escapeBytes != null)
                            {
                                Buffer.BlockCopy(escapeBytes, 0, buffer, bufferIndex, escapeBytes.Length);
                                bufferIndex += escapeBytes.Length;
                                continue;
                            }
                        }

                        int codepoint = c;

                        if (codepoint <= 0x7F)
                        {
                            // 1-byte sequence
                            buffer[bufferIndex++] = (byte)codepoint;
                        }
                        else if (codepoint <= 0x7FF)
                        {
                            // 2-byte sequence
                            buffer[bufferIndex++] = (byte)(((codepoint >> 6) & 0x1F) | 0xC0);
                            buffer[bufferIndex++] = (byte)((codepoint & 0x3F) | 0x80);
                        }
                        else if (!char.IsSurrogate(c))
                        {
                            // 3-byte sequence
                            buffer[bufferIndex++] = (byte)(((codepoint >> 12) & 0x0F) | 0xE0);
                            buffer[bufferIndex++] = (byte)(((codepoint >> 6) & 0x3F) | 0x80);
                            buffer[bufferIndex++] = (byte)((codepoint & 0x3F) | 0x80);
                        }
                        else
                        {
                            // Handle surrogate pairs
                            if (char.IsHighSurrogate(c) && charIndex + 1 < str.Length && char.IsLowSurrogate(str[charIndex + 1]))
                            {
                                int highSurrogate = c;
                                int lowSurrogate = str[charIndex + 1];
                                int surrogateCodePoint = 0x10000 + ((highSurrogate - 0xD800) << 10) + (lowSurrogate - 0xDC00);

                                buffer[bufferIndex++] = (byte)((surrogateCodePoint >> 18) | 0xF0);
                                buffer[bufferIndex++] = (byte)(((surrogateCodePoint >> 12) & 0x3F) | 0x80);
                                buffer[bufferIndex++] = (byte)(((surrogateCodePoint >> 6) & 0x3F) | 0x80);
                                buffer[bufferIndex++] = (byte)((surrogateCodePoint & 0x3F) | 0x80);

                                charIndex++; // Skip next character, it was part of the surrogate pair
                            }
                            else
                            {
                                throw new ArgumentException("Invalid surrogate pair in string.");
                            }
                        }
                    }

                    // Flush any remaining bytes in the buffer to the stream
                    if (bufferIndex > 0)
                    {
                        stream.Write(buffer, 0, bufferIndex);
                        bufferIndex = 0;
                    }
                }
            }

            private void WriteString(string str)
            {
                int bufferIndex = 0;

                int charIndex = 0;
                int minCharSpace = buffer.Length / 4;
                while (charIndex < str.Length)
                {                    
                    int charIndexLimit = Math.Min(str.Length, charIndex + minCharSpace);

                    for (; charIndex < charIndexLimit; charIndex++)
                    {
                        var c = str[charIndex];                        
                        int codepoint = c;

                        if (codepoint <= 0x7F)
                        {
                            // 1-byte sequence
                            buffer[bufferIndex++] = (byte)codepoint;
                        }
                        else if (codepoint <= 0x7FF)
                        {
                            // 2-byte sequence
                            buffer[bufferIndex++] = (byte)(((codepoint >> 6) & 0x1F) | 0xC0);
                            buffer[bufferIndex++] = (byte)((codepoint & 0x3F) | 0x80);
                        }
                        else if (!char.IsSurrogate(c))
                        {
                            // 3-byte sequence
                            buffer[bufferIndex++] = (byte)(((codepoint >> 12) & 0x0F) | 0xE0);
                            buffer[bufferIndex++] = (byte)(((codepoint >> 6) & 0x3F) | 0x80);
                            buffer[bufferIndex++] = (byte)((codepoint & 0x3F) | 0x80);
                        }
                        else
                        {
                            // Handle surrogate pairs
                            if (char.IsHighSurrogate(c) && charIndex + 1 < str.Length && char.IsLowSurrogate(str[charIndex + 1]))
                            {
                                int highSurrogate = c;
                                int lowSurrogate = str[charIndex + 1];
                                int surrogateCodePoint = 0x10000 + ((highSurrogate - 0xD800) << 10) + (lowSurrogate - 0xDC00);

                                buffer[bufferIndex++] = (byte)((surrogateCodePoint >> 18) | 0xF0);
                                buffer[bufferIndex++] = (byte)(((surrogateCodePoint >> 12) & 0x3F) | 0x80);
                                buffer[bufferIndex++] = (byte)(((surrogateCodePoint >> 6) & 0x3F) | 0x80);
                                buffer[bufferIndex++] = (byte)((surrogateCodePoint & 0x3F) | 0x80);

                                charIndex++; // Skip next character, it was part of the surrogate pair
                            }
                            else
                            {
                                throw new ArgumentException("Invalid surrogate pair in string.");
                            }
                        }
                    }

                    // Flush any remaining bytes in the buffer to the stream
                    if (bufferIndex > 0)
                    {
                        stream.Write(buffer, 0, bufferIndex);
                        bufferIndex = 0;
                    }
                }
            }

            private void WriteSignedInteger(long value)
            {
                if (value == 0)
                {
                    stream.WriteByte((byte)'0');
                    return;
                }

                bool isNegative = value < 0;
                if (isNegative) value = -value;                

                const int maxDigits = 20;
                int index = maxDigits;
                while(value >= 10)
                {
                    value = Math.DivRem(value, 10, out long digit);
                    buffer[index--] = (byte)('0' + digit);
                }
                if (value > 0) buffer[index--] = (byte)('0' + value);
                if (isNegative) buffer[index--] = (byte)'-';

                stream.Write(buffer, index+1, maxDigits - index);
            }

            private void WriteSignedInteger(int value)
            {
                if (value == 0)
                {
                    stream.WriteByte((byte)'0');
                    return;
                }

                bool isNegative = value < 0;
                if (isNegative) value = -value;

                const int maxDigits = 20;
                int index = maxDigits;
                while (value >= 10)
                {
                    value = Math.DivRem(value, 10, out int digit);
                    buffer[index--] = (byte)('0' + digit);
                }
                if (value > 0) buffer[index--] = (byte)('0' + value);
                if (isNegative) buffer[index--] = (byte)'-';

                stream.Write(buffer, index + 1, maxDigits - index);
            }

            private void WriteUnsignedInteger(long value)
            {
                if (value == 0)
                {
                    stream.WriteByte((byte)'0');
                    return;
                }

                const int maxDigits = 20;
                int index = maxDigits;
                while (value >= 10)
                {
                    value = Math.DivRem(value, 10, out long digit);
                    buffer[index--] = (byte)('0' + digit);
                }
                if (value > 0) buffer[index--] = (byte)('0' + value);

                stream.Write(buffer, index + 1, maxDigits - index);
            }

            static readonly byte[] ZERO_FLOAT = "0.0".ToByteArray();
            private void WriteFloat(double value)
            {
                if (value == 0.0)
                {
                    stream.Write(ZERO_FLOAT);
                    return;
                }

                int index = 0;
                if (value < 0)
                {
                    value = -value;
                    buffer[index++] = (byte)'-';
                }


                double integralPart = Math.Floor(value);
                double fractionalPart = value - integralPart;
                byte digit;
                if (integralPart == 0)
                {
                    buffer[index++] = (byte)'0';
                }
                else
                {
                    int integralDigits = 0;
                    while (integralPart >= 1)
                    {
                        integralDigits += 4;
                        integralPart /= 10000;
                    }

                    int i = 0;
                    for (; i < integralDigits; i++)
                    {
                        integralPart *= 10;
                        digit = (byte)integralPart;
                        if (digit != 0)
                        {
                            buffer[index++] = (byte)('0' + digit);
                            integralPart -= digit;
                            i++;
                            break;
                        }                        
                    }

                    for (; i < integralDigits; i++)
                    {
                        integralPart *= 10;
                        digit = (byte)integralPart;
                        buffer[index++] = (byte)('0' + digit);
                        integralPart -= digit;                        
                    }
                    if (integralPart > 0.5)
                    {
                        int correctionIndex = index - 1;
                        while (buffer[correctionIndex] == (byte)'9')
                        {
                            buffer[correctionIndex] = (byte)'0';
                            correctionIndex--;
                        }
                        buffer[correctionIndex] += 1;
                    }
                }

                buffer[index++] = (byte)'.';

                if (fractionalPart == 0)
                {
                    buffer[index++] = (byte)'0';
                }
                else
                {                    
                    int trailingNines = 0;
                    int trailingZeros = 0;
                    const int trailingLimit = 4;
                    while (fractionalPart > 0)
                    {
                        fractionalPart *= 10;
                        digit = (byte)fractionalPart;
                        buffer[index++] = (byte)('0' + digit);
                        fractionalPart -= digit;
                        if (digit == 0)
                        {
                            trailingZeros++;
                            if (trailingZeros >= trailingLimit)
                            {
                                index -= trailingZeros;
                                break;
                            }
                            trailingNines = 0;
                        }
                        else if (digit == 9)
                        {
                            trailingNines++;
                            if (trailingNines >= trailingLimit)
                            {
                                index -= trailingNines;
                                buffer[index - 1] += 1;
                                break;
                            }
                            trailingZeros = 0;
                        }
                        else
                        {
                            trailingZeros = 0;
                            trailingNines = 0;
                        }
                    }
                }
                stream.Write(buffer, 0, index);
            }
      
        }
            
    }
}
