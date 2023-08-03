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
            public void WriteSignedIntValue(long value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteUnsignedIntValue(ulong value)
            {
                WriteUnsignedInteger((long)value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteSignedIntValue(int value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteUnsignedIntValue(uint value)
            {
                WriteUnsignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteFloatValue(float value)
            {
                WriteFloat(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteFloatValue(double value)
            {
                WriteFloat(value);
            }

            static readonly byte[] BOOLVALUE_TRUE = "true".ToByteArray();
            static readonly byte[] BOOLVALUE_FALSE = "false".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBoolValue(bool value)
            {
                stream.Write(value ? BOOLVALUE_TRUE : BOOLVALUE_FALSE);
            }            

            static readonly byte[] STRINGVALUE_PRE = "\"".ToByteArray();
            static readonly byte[] STRINGVALUE_POST = "\"".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteStringValue(string str)
            {
                stream.Write(STRINGVALUE_PRE, 0, STRINGVALUE_PRE.Length);
                WriteEscapedString(str);
                stream.Write(STRINGVALUE_POST, 0, STRINGVALUE_POST.Length);
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
            public byte[] PrepareCollectionIndexName(CollectionJob parentJob)
            {
                int index = parentJob.index;
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

            static readonly byte[] ESCAPE_SEQUENCES = "\\\\\\\"\\b\\f\\n\\r\\t".ToByteArray();
            private void WriteEscapedString(string str)
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

                        
                        int escapeIndex = "\\\"\b\f\n\r\t".IndexOf(c);
                        if (escapeIndex != -1)
                        {
                            Buffer.BlockCopy(ESCAPE_SEQUENCES, escapeIndex * 2, buffer, bufferIndex, 2);
                            bufferIndex += 2;
                            continue;
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
                                var bytesUsed = encoder.GetBytes(str.ToCharArray(charIndex, 2), 0, 2, buffer, bufferIndex, false);
                                charIndex++; // Skip next character, it was part of the surrogate pair
                                bufferIndex += bytesUsed;
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
                                var bytesUsed = encoder.GetBytes(str.ToCharArray(charIndex, 2), 0, 2, buffer, bufferIndex, false);
                                charIndex++; // Skip next character, it was part of the surrogate pair
                                bufferIndex += bytesUsed;
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
            /*private void WriteFloat(double value)
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

                int integralDigits = 0;
                while (value >= 1)
                {
                    integralDigits++;
                    value /= 10;
                }

                byte digit;                
                for (int i = 0; i < integralDigits; i++)
                {
                    value *= 10;
                    digit = (byte)value;
                    buffer[index++] = (byte)('0' + digit);
                    value -= digit;
                }

                if (value == 0) return;

                WriteDot();
                               
                while(value > 0)
                {
                    value *= 10;
                    digit = (byte)value;
                    buffer[index++] = (byte)('0' + digit);
                    value -= digit;
                }
                stream.Write(buffer, 0, index);
            }*/

            /*
            private void WriteFloat(float value)
            {
                if (value == 0.0)
                {
                    stream.Write(ZERO_FLOAT);
                    return;
                }

                long fullValue = (long)value;
                WriteSignedInteger(fullValue);

                WriteDot();

                float fracturedValue = value - fullValue;
                if (fracturedValue == 0.0)
                {
                    stream.WriteByte((byte)'0');
                    return;
                }
                if (fracturedValue < 0) fracturedValue = -fracturedValue;
                byte digit;
                int index = 0;
                while (fracturedValue > 0)
                {
                    fracturedValue *= 10;
                    digit = (byte)fracturedValue;
                    buffer[index++] = (byte)('0' + digit);
                    fracturedValue -= digit;
                }
                stream.Write(buffer, 0, index);
            }
            */
        }
            
    }
}
