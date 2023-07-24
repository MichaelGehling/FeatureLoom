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
                stream.Write(STRINGVALUE_PRE, 0,  STRINGVALUE_PRE.Length);
                WriteString(str, true);
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
            public void WritePreparedString(string str) => WriteString(str);


            static readonly byte[] ESCAPE_QUOTE = "\\\"".ToByteArray();
            static readonly byte[] ESCAPE_BACKSLASH = "\\\\".ToByteArray();
            static readonly byte[] ESCAPE_B = "\\b".ToByteArray();
            static readonly byte[] ESCAPE_F = "\\f".ToByteArray();
            static readonly byte[] ESCAPE_N = "\\n".ToByteArray();
            static readonly byte[] ESCAPE_R = "\\r".ToByteArray();
            static readonly byte[] ESCAPE_T = "\\t".ToByteArray();
            private void WriteString(string str, bool escape = false)
            {
                int bufferIndex = 0;

                for (var i = 0; i < str.Length; i++)
                {
                    var c = str[i];

                    if (!char.IsSurrogate(c))
                    {
                        // Ensure we have enough room in the buffer
                        if (buffer.Length - bufferIndex < 20)
                        {
                            stream.Write(buffer, 0, bufferIndex);
                            bufferIndex = 0;
                        }

                        
                        if (escape)
                        {
                            bool next = true;
                            switch (c)
                            {
                                case '\"': Buffer.BlockCopy(ESCAPE_QUOTE, 0, buffer, bufferIndex, ESCAPE_QUOTE.Length); bufferIndex += ESCAPE_QUOTE.Length; break;
                                case '\\': Buffer.BlockCopy(ESCAPE_BACKSLASH, 0, buffer, bufferIndex, ESCAPE_BACKSLASH.Length); bufferIndex += ESCAPE_BACKSLASH.Length; break;
                                case '\b': Buffer.BlockCopy(ESCAPE_B, 0, buffer, bufferIndex, ESCAPE_B.Length); bufferIndex += ESCAPE_B.Length; break;
                                case '\f': Buffer.BlockCopy(ESCAPE_F, 0, buffer, bufferIndex, ESCAPE_F.Length); bufferIndex += ESCAPE_F.Length; break;
                                case '\n': Buffer.BlockCopy(ESCAPE_N, 0, buffer, bufferIndex, ESCAPE_N.Length); bufferIndex += ESCAPE_N.Length; break;
                                case '\r': Buffer.BlockCopy(ESCAPE_R, 0, buffer, bufferIndex, ESCAPE_R.Length); bufferIndex += ESCAPE_R.Length; break;
                                case '\t': Buffer.BlockCopy(ESCAPE_T, 0, buffer, bufferIndex, ESCAPE_T.Length); bufferIndex += ESCAPE_T.Length; break;
                                default: next = false; break;
                            }
                            if (next) continue;
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
                        else
                        {
                            // 3-byte sequence
                            buffer[bufferIndex++] = (byte)(((codepoint >> 12) & 0x0F) | 0xE0);
                            buffer[bufferIndex++] = (byte)(((codepoint >> 6) & 0x3F) | 0x80);
                            buffer[bufferIndex++] = (byte)((codepoint & 0x3F) | 0x80);
                        }
                    }
                    else
                    {
                        // Handle surrogate pairs
                        if (char.IsHighSurrogate(c) && i + 1 < str.Length && char.IsLowSurrogate(str[i + 1]))
                        {
                            var bytesUsed = encoder.GetBytes(str.ToCharArray(i, 2), 0, 2, buffer, bufferIndex, false);
                            i++; // Skip next character, it was part of the surrogate pair
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
                }
            }

        }

    }
}
