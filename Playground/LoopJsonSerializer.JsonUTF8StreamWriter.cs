using System.Text;
using FeatureLoom.Extensions;
using System.IO;
using System.Runtime.CompilerServices;
using System;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Playground
{
    public sealed partial class LoopJsonSerializer
    {
        private sealed class JsonUTF8StreamWriter
        {
            public Stream stream;
            private StreamWriter stringWriter;
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
            public void WritePrimitiveValue<T>(T value) => WriteString(value.ToString());

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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteRefObject(string refPath)
            {
                stream.Write(REFOBJECT_PRE, 0, REFOBJECT_PRE.Length);
                WriteString(refPath);
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

        }
    }
}
