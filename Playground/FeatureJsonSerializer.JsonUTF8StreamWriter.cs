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
    public sealed partial class FeatureJsonSerializer
    {
        private sealed class JsonUTF8StreamWriter
        {
            public Stream stream;
            private byte[] localBuffer;                 
            private byte[] mainBuffer;
            private int mainBufferSize;
            private int mainBufferCount;
            private SlicedBuffer<byte> tempSlicedBuffer;

            public JsonUTF8StreamWriter(int mainBufferSize, int tempBufferSize)
            {
                localBuffer = new byte[64];
                this.mainBufferSize = mainBufferSize;
                // We give some extra bytes in order to not always check remaining space
                mainBuffer = new byte[mainBufferSize + 64];
                // Used for temporarily needed names e.g. Dictionary
                tempSlicedBuffer = new SlicedBuffer<byte>(tempBufferSize, 128);
            }

            public override string ToString()
            {
                if (stream.Length > 0)
                {
                    WriteBufferToStream();
                    return stream.ReadToString();
                }
                else
                {
                    return Encoding.UTF8.GetString(mainBuffer, 0, mainBufferCount);
                }
            }

            public byte[] Buffer => mainBuffer;
            public int BufferCount => mainBufferCount;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteBufferToStream()
            {
                stream.Write(mainBuffer, 0, mainBufferCount);
                mainBufferCount = 0;                
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ResetBuffer()
            {
                mainBufferCount = 0;
                tempSlicedBuffer.Reset(true);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteToBuffer(byte[] data, int offset, int count)
            {
                if (mainBufferCount + count > mainBuffer.Length) WriteBufferToStream();
                Array.Copy(data, offset, mainBuffer, mainBufferCount, count);
                mainBufferCount += count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteToBuffer(byte[] data, int count)
            {
                WriteToBuffer(data, 0, count);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteToBuffer(byte[] data)
            {
                WriteToBuffer(data, 0, data.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteToBuffer(ArraySegment<byte> data)
            {
                if (mainBufferCount + data.Count > mainBuffer.Length) WriteBufferToStream();
                data.CopyTo(mainBuffer, mainBufferCount);
                mainBufferCount += data.Count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteToBuffer(byte data)
            {
                if (mainBufferCount >= mainBuffer.Length) WriteBufferToStream();
                mainBuffer[mainBufferCount++] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteToBufferWithoutCheck(byte[] data, int offset, int count)
            {
                Array.Copy(data, offset, mainBuffer, mainBufferCount, count);
                mainBufferCount += count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteToBufferWithoutCheck(byte[] data, int count)
            {
                WriteToBufferWithoutCheck(data, 0, count);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteToBufferWithoutCheck(byte[] data)
            {
                WriteToBufferWithoutCheck(data, 0, data.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteToBufferWithoutCheck(byte data)
            {
                mainBuffer[mainBufferCount++] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void EnsureFreeBufferSpace(int freeBytes)
            {
                if (mainBufferCount + freeBytes >= mainBuffer.Length) WriteBufferToStream();
            }

            static readonly byte[] NULL = "null".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteNullValue() => WriteToBuffer(NULL);

            static readonly byte OPEN_OBJECT = (byte)'{';
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenObject() => WriteToBufferWithoutCheck(OPEN_OBJECT);

            static readonly byte CLOSE_OBJECT = (byte)'}';
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseObject() => WriteToBufferWithoutCheck(CLOSE_OBJECT);

            static readonly byte[] TYPEINFO_PRE = "\"$type\":\"".ToByteArray();
            static readonly byte TYPEINFO_POST = (byte)'\"';
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteTypeInfo(string typeName)
            {
                WriteToBuffer(TYPEINFO_PRE);
                WriteString(typeName);
                WriteToBufferWithoutCheck(TYPEINFO_POST);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareTypeInfo(string typeName)
            {
                return $"\"$type\":\"{typeName}\"".ToByteArray();
            }

            static readonly byte[] VALUEFIELDNAME = "\"$value\":".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteValueFieldName() => WriteToBuffer(VALUEFIELDNAME);

            static readonly byte FIELDNAME_PRE = (byte)'\"';
            static readonly byte[] FIELDNAME_POST = "\":".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteFieldName(string fieldName)
            {
                WriteToBufferWithoutCheck(FIELDNAME_PRE);
                WriteString(fieldName);
                WriteToBufferWithoutCheck(FIELDNAME_POST);
            }


            // Fallback for non specialized methods
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue<T>(T value)
            {                
                WriteString(value.ToString());
            }

            // Fallback for non specialized methods
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy<T>(T value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(1024);
                var countBefore = mainBufferCount;
                
                WriteString(value.ToString());

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);                
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);
                
                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            // Fallback for non specialized methods
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString<T>(T value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteString(value.ToString());
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static byte[] PreparePrimitiveToBytes<T>(T value)
            {
                return Encoding.UTF8.GetBytes(value.ToString()); // TODO optimize
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(long value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(long value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteSignedInteger(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(long value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteSignedInteger(value);
                WriteToBufferWithoutCheck(QUOTES);
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(ulong value)
            {
                WriteUnsignedInteger((long)value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(ulong value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteUnsignedInteger((long)value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(ulong value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteUnsignedInteger((long)value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(int value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(int value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteSignedInteger(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(int value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteSignedInteger(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(uint value)
            {
                WriteUnsignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(uint value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteUnsignedInteger(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(uint value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteUnsignedInteger(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(byte value)
            {
                WriteUnsignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(byte value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteUnsignedInteger(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(byte value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteUnsignedInteger(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(sbyte value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(sbyte value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteSignedInteger(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(sbyte value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteSignedInteger(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(short value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(short value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteSignedInteger(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(short value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteSignedInteger(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(ushort value)
            {
                WriteUnsignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(ushort value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteUnsignedInteger(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(ushort value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteUnsignedInteger(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(float value)
            {
                WriteFloat(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(float value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteFloat(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(float value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteFloat(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(double value)
            {
                WriteDouble(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(decimal value)
            {
                WriteDouble((double)value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(double value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteDouble(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(double value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteDouble(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            static readonly byte[] BOOLVALUE_TRUE = "true".ToByteArray();
            static readonly byte[] BOOLVALUE_FALSE = "false".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(bool value)
            {
                var bytes = value ? BOOLVALUE_TRUE : BOOLVALUE_FALSE;
                WriteToBuffer(bytes);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(bool value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                var bytes = value ? BOOLVALUE_TRUE : BOOLVALUE_FALSE;
                WriteToBuffer(bytes);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(bool value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                var bytes = value ? BOOLVALUE_TRUE : BOOLVALUE_FALSE;
                WriteToBuffer(bytes);
                WriteToBufferWithoutCheck(QUOTES);
            }

            static readonly byte QUOTES = (byte)'\"';
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(string str)
            {
                if (str != null)
                {
                    WriteToBufferWithoutCheck(QUOTES);
                    WriteEscapedString(str);
                    WriteToBufferWithoutCheck(QUOTES);
                }
                else WriteNullValue();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(string str)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WriteEscapedString(str);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(string str)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteEscapedString(str);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(char value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteChar(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(char value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                WritePrimitiveValue(value);

                var writtenBytes = mainBufferCount - countBefore;
                var slice = tempSlicedBuffer.GetSlice(writtenBytes);
                slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

                WriteToBufferWithoutCheck(QUOTES);
                return slice;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(char value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WritePrimitiveValue(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            static readonly byte[] REFOBJECT_PRE = "{\"$ref\":\"".ToByteArray();
            static readonly byte[] REFOBJECT_POST = "\"}".ToByteArray();            
            Stack<ItemInfo> reverseItemInfoStack = new Stack<ItemInfo>();
            public void WriteRefObject(ItemInfo itemInfo)
            {
                while (itemInfo != null)
                {
                    reverseItemInfoStack.Push(itemInfo);
                    itemInfo = itemInfo.parentInfo;
                }

                WriteToBuffer(REFOBJECT_PRE);

                if (reverseItemInfoStack.TryPop(out itemInfo))
                {
                    var name = itemInfo.ItemName;
                    WriteToBuffer(itemInfo.ItemName);
                }

                while (reverseItemInfoStack.TryPop(out itemInfo))
                {
                    var name = itemInfo.ItemName;
                    if (name[0] != OPENCOLLECTION) WriteDot();
                    WriteToBuffer(name);                    
                }
                WriteToBuffer(REFOBJECT_POST);
            }
        
            static readonly byte OPENCOLLECTION = (byte)'[';
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenCollection() => WriteToBufferWithoutCheck(OPENCOLLECTION);

            static readonly byte CLOSECOLLECTION = (byte)']';
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseCollection() => WriteToBufferWithoutCheck(CLOSECOLLECTION);

            static readonly byte COMMA = (byte)',';
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteComma() => WriteToBufferWithoutCheck(COMMA);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveTrailingComma()
            {
                if (mainBuffer[mainBufferCount - 1] == COMMA) mainBufferCount--;
            }

            static readonly byte DOT = (byte)'.';
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteDot() => WriteToBufferWithoutCheck(DOT);

            static readonly byte COLON = (byte)':';
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteColon() => WriteToBufferWithoutCheck(COLON);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePreparedByteString(byte[] bytes) => WriteToBuffer(bytes);

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

            public static readonly byte[] ROOT = "$".ToByteArray();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareRootName() => ROOT;

            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareEnumTextToBytes(string enumText)
            {
                return Encoding.UTF8.GetBytes($"\"{enumText}\"");
            }

            List<ArraySegment<byte>> indexNameList = new List<ArraySegment<byte>>();            
            public ArraySegment<byte> PrepareCollectionIndexName(int index)
            {
                if (index >= indexNameList.Count)
                {                    
                    for(int i = indexNameList.Count; i <= index; i++)
                    {
                        indexNameList.Add(default);
                    }
                }
                if (indexNameList[index].Array == null) indexNameList[index] = new ArraySegment<byte>($"[{index}]".ToByteArray());
                return indexNameList[index];
            }

            private static readonly byte[][] PositiveNumberBytesLookup = InitNumberBytesLookup(false, 256);
            private static readonly byte[][] NegativeNumberBytesLookup = InitNumberBytesLookup(true, 128);
            private static byte[][] InitNumberBytesLookup(bool negative, int size)
            {
                byte[][] lookup = new byte[size][];
                int factor = negative ? -1 : 1;

                for (int i = 0; i < size; i++)
                {
                    lookup[i] = Encoding.ASCII.GetBytes((i*factor).ToString());
                }

                return lookup;
            }

            private static readonly byte[] BackSlashEscapeBytes = "\\\\".ToByteArray();
            private static readonly byte[][] EscapeByteLookup = InitEscapeByteLookup();
            private static byte[][] InitEscapeByteLookup()
            {
                byte[][] lookup = new byte[35][]; // '\\' is the highest escape char
                string escapeChars = "\"\b\f\n\r\t"; ; //  '\\' Is checked extra
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static byte[] GetEscapeBytes(char c)
            {
                if (c == '\\') return BackSlashEscapeBytes;
                if (c < EscapeByteLookup.Length) return EscapeByteLookup[c];
                return null;
            }

            private void WriteChar(char c)
            {
                // Check if the character is in the EscapeByteLookup table
                byte[] escapeBytes = GetEscapeBytes(c);                
                if (escapeBytes != null)
                {
                    WriteToBuffer(escapeBytes, 0, escapeBytes.Length);
                    return;
                }

                int codepoint = c;

                if (codepoint <= 0x7F)
                {
                    // 1-byte sequence
                    WriteToBuffer((byte)codepoint);
                }
                else if (codepoint <= 0x7FF)
                {
                    // 2-byte sequence
                    EnsureFreeBufferSpace(2);
                    WriteToBufferWithoutCheck((byte)(((codepoint >> 6) & 0x1F) | 0xC0));
                    WriteToBufferWithoutCheck((byte)((codepoint & 0x3F) | 0x80));                    
                }
                else if (!char.IsSurrogate(c))
                {
                    // 3-byte sequence
                    EnsureFreeBufferSpace(3);
                    WriteToBufferWithoutCheck((byte)(((codepoint >> 12) & 0x0F) | 0xE0));
                    WriteToBufferWithoutCheck((byte)(((codepoint >> 6) & 0x3F) | 0x80));
                    WriteToBufferWithoutCheck((byte)((codepoint & 0x3F) | 0x80));
                }
                else
                {
                    // Handle surrogate by writing it as a Unicode escape sequence
                    WriteString("\\u" + ((int)c).ToString("X4"));
                }
            }

            private void WriteEscapedString(string str)
            {                
                int charIndex = 0;
                const int MAX_CHAR_LENGTH = 6; // Escaped characters may have up to 6 Bytes
               
                while (charIndex < str.Length)
                {
                    EnsureFreeBufferSpace((str.Length - charIndex) * MAX_CHAR_LENGTH);
                    int guaranteedCharSpace = (mainBuffer.Length - mainBufferCount) / MAX_CHAR_LENGTH;
                    int charIndexLimit = Math.Min(str.Length, charIndex + guaranteedCharSpace);

                    for (; charIndex < charIndexLimit; charIndex++)
                    {
                        var c = str[charIndex];

                        // Handle escaped chars and control chars
                        byte[] escapeBytes = GetEscapeBytes(c);
                        if (escapeBytes != null)
                        { 
                            WriteToBufferWithoutCheck(escapeBytes);                            
                            continue;
                        }

                        int codepoint = c;

                        if (codepoint <= 0x7F)
                        {
                            // 1-byte sequence
                            WriteToBufferWithoutCheck((byte)codepoint);
                        }
                        else if (codepoint <= 0x7FF)
                        {
                            // 2-byte sequence
                            WriteToBufferWithoutCheck((byte)(((codepoint >> 6) & 0x1F) | 0xC0));
                            WriteToBufferWithoutCheck((byte)((codepoint & 0x3F) | 0x80));
                        }
                        else if (!char.IsSurrogate(c))
                        {
                            // 3-byte sequence
                            WriteToBufferWithoutCheck((byte)(((codepoint >> 12) & 0x0F) | 0xE0));
                            WriteToBufferWithoutCheck((byte)(((codepoint >> 6) & 0x3F) | 0x80));
                            WriteToBufferWithoutCheck((byte)((codepoint & 0x3F) | 0x80));
                        }
                        else
                        {
                            // Handle surrogate pairs
                            if (char.IsHighSurrogate(c) && charIndex + 1 < str.Length && char.IsLowSurrogate(str[charIndex + 1]))
                            {
                                int highSurrogate = c;
                                int lowSurrogate = str[charIndex + 1];
                                int surrogateCodePoint = 0x10000 + ((highSurrogate - 0xD800) << 10) + (lowSurrogate - 0xDC00);

                                WriteToBufferWithoutCheck((byte)((surrogateCodePoint >> 18) | 0xF0));
                                WriteToBufferWithoutCheck((byte)(((surrogateCodePoint >> 12) & 0x3F) | 0x80));
                                WriteToBufferWithoutCheck((byte)(((surrogateCodePoint >> 6) & 0x3F) | 0x80));
                                WriteToBufferWithoutCheck((byte)((surrogateCodePoint & 0x3F) | 0x80));

                                charIndex++; // Skip next character, it was part of the surrogate pair
                            }
                            else
                            {
                                throw new ArgumentException("Invalid surrogate pair in string.");
                            }
                        }
                    }
                }
            }

            private void WriteString(string str)
            {
                int charIndex = 0;
                const int MAX_CHAR_LENGTH = 4;                
                while (charIndex < str.Length)
                {
                    EnsureFreeBufferSpace((str.Length - charIndex) * MAX_CHAR_LENGTH);
                    int guaranteedCharSpace = (mainBuffer.Length - mainBufferCount) / MAX_CHAR_LENGTH;
                    int charIndexLimit = Math.Min(str.Length, charIndex + guaranteedCharSpace);

                    for (; charIndex < charIndexLimit; charIndex++)
                    {
                        var c = str[charIndex];
                        int codepoint = c;

                        if (codepoint <= 0x7F)
                        {
                            // 1-byte sequence
                            WriteToBufferWithoutCheck((byte)codepoint);
                        }
                        else if (codepoint <= 0x7FF)
                        {
                            // 2-byte sequence
                            WriteToBufferWithoutCheck((byte)(((codepoint >> 6) & 0x1F) | 0xC0));
                            WriteToBufferWithoutCheck((byte)((codepoint & 0x3F) | 0x80));
                        }
                        else if (!char.IsSurrogate(c))
                        {
                            // 3-byte sequence
                            WriteToBufferWithoutCheck((byte)(((codepoint >> 12) & 0x0F) | 0xE0));
                            WriteToBufferWithoutCheck((byte)(((codepoint >> 6) & 0x3F) | 0x80));
                            WriteToBufferWithoutCheck((byte)((codepoint & 0x3F) | 0x80));
                        }
                        else
                        {
                            // Handle surrogate pairs
                            if (char.IsHighSurrogate(c) && charIndex + 1 < str.Length && char.IsLowSurrogate(str[charIndex + 1]))
                            {
                                int highSurrogate = c;
                                int lowSurrogate = str[charIndex + 1];
                                int surrogateCodePoint = 0x10000 + ((highSurrogate - 0xD800) << 10) + (lowSurrogate - 0xDC00);

                                WriteToBufferWithoutCheck((byte)((surrogateCodePoint >> 18) | 0xF0));
                                WriteToBufferWithoutCheck((byte)(((surrogateCodePoint >> 12) & 0x3F) | 0x80));
                                WriteToBufferWithoutCheck((byte)(((surrogateCodePoint >> 6) & 0x3F) | 0x80));
                                WriteToBufferWithoutCheck((byte)((surrogateCodePoint & 0x3F) | 0x80));

                                charIndex++; // Skip next character, it was part of the surrogate pair
                            }
                            else
                            {
                                throw new ArgumentException("Invalid surrogate pair in string.");
                            }
                        }
                    }
                }
            }

            private void WriteSignedInteger(long value)
            {                
                bool isNegative = value < 0;
                if (isNegative)
                {
                    value = -value;
                    if (value < NegativeNumberBytesLookup.Length)
                    {
                        var bytes = NegativeNumberBytesLookup[value];
                        WriteToBuffer(bytes);
                        return;
                    }
                }
                if (value < PositiveNumberBytesLookup.Length)
                {
                    var bytes = PositiveNumberBytesLookup[value];
                    WriteToBuffer(bytes);
                    return;
                }

                const int maxDigits = 20;
                int index = maxDigits;
                while(value >= 10)
                {
                    value = Math.DivRem(value, 10, out long digit);
                    localBuffer[index--] = (byte)('0' + digit);
                }
                if (value > 0) localBuffer[index--] = (byte)('0' + value);
                if (isNegative) localBuffer[index--] = (byte)'-';

                WriteToBuffer(localBuffer, index+1, maxDigits - index);
            }

           /* private byte[] SignedIntegerToBytes(long value)
            {
                if (value == 0) return ZERO;
            }*/

            private void WriteSignedInteger(int value)
            {
                bool isNegative = value < 0;
                if (isNegative)
                {
                    value = -value;
                    if (value < NegativeNumberBytesLookup.Length)
                    {
                        var bytes = NegativeNumberBytesLookup[value];
                        WriteToBuffer(bytes);
                        return;
                    }
                }
                if (value < PositiveNumberBytesLookup.Length)
                {
                    var bytes = PositiveNumberBytesLookup[value];
                    WriteToBuffer(bytes);
                    return;
                }

                const int maxDigits = 20;
                int index = maxDigits;
                while (value >= 10)
                {
                    value = Math.DivRem(value, 10, out int digit);
                    localBuffer[index--] = (byte)('0' + digit);
                }
                if (value > 0) localBuffer[index--] = (byte)('0' + value);
                if (isNegative) localBuffer[index--] = (byte)'-';

                WriteToBuffer(localBuffer, index + 1, maxDigits - index);
            }

            private void WriteUnsignedInteger(long value)
            {
                if (value < PositiveNumberBytesLookup.Length)
                {
                    var bytes = PositiveNumberBytesLookup[value];
                    WriteToBuffer(bytes, 0, bytes.Length);
                    return;
                }

                const int maxDigits = 20;
                int index = maxDigits;
                while (value >= 10)
                {
                    value = Math.DivRem(value, 10, out long digit);
                    localBuffer[index--] = (byte)('0' + digit);
                }
                if (value > 0) localBuffer[index--] = (byte)('0' + value);

                WriteToBuffer(localBuffer, index + 1, maxDigits - index);
            }

            static readonly byte[] ZERO_FLOAT = "0.0".ToByteArray();
            static readonly byte[] NAN = "\"NaN\"".ToByteArray();
            static readonly byte[] POS_INFINITY = "\"Infinity\"".ToByteArray();
            static readonly byte[] NEG_INFINITY = "\"-Infinity\"".ToByteArray();


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsSpecial(double value)
            {
                if (Double.IsNaN(value)) return true;   // NaN
                long bits = BitConverter.DoubleToInt64Bits(value);
                const long mask = 0x7FF0000000000000L;  // Mask to isolate the exponent bits for double
                long maskedBits = bits & mask;
                if (maskedBits == 0) return true;       // Subnormal
                if (maskedBits == mask) return true;    // Infinity
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsSpecial(float value)
            {
                if (Single.IsNaN(value)) return true;   // NaN
                int bits = BitConverter.SingleToInt32Bits(value);
                const int mask = 0x7F800000;            // Mask to isolate the exponent bits for float
                int maskedBits = bits & mask;
                if (maskedBits == 0) return true;       // Subnormal
                if (maskedBits == mask) return true;    // Infinity
                return false;
            }

            private void WriteFloat(float value)
            {
                if (HandleSpecialCases(value)) return;

                EnsureFreeBufferSpace(100);

                if (value < 0)
                {
                    value = -value;
                    WriteToBufferWithoutCheck((byte)'-');
                }
                
                value = CalculateNumDigits(value, out int  exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent);

                float integralPart = (float)Math.Floor(value);
                float fractionalPart = value - integralPart;

                WriteIntegralPart(numIntegralDigits, integralPart);

                WriteToBufferWithoutCheck((byte)'.');

                WriteFractionalPart(numFractionalDigits, fractionalPart);

                if (printExponent)
                {
                    WriteToBuffer((byte)'E');
                    WriteSignedInteger(exponent);
                }

                // Local Functions

                bool HandleSpecialCases(float value)
                {
                    if (value == 0)
                    {
                        WriteToBuffer(ZERO_FLOAT);
                        return true;
                    }
                    if (IsSpecial(value))
                    {
                        if (Single.IsNaN(value)) WriteToBuffer(NAN);
                        if (Single.IsNegativeInfinity(value)) WriteToBuffer(NEG_INFINITY);
                        if (Single.IsPositiveInfinity(value)) WriteToBuffer(POS_INFINITY);
                        if (Single.IsSubnormal(value)) WriteString(value.ToString());
                        return true;
                    }

                    return false;
                }

                static float CalculateNumDigits(float value, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent)
                {
                    const int MAX_SIGNIFICANT_DIGITS = 7;
                    const int POS_EXPONENT_LIMIT = 7;
                    const int NEG_EXPONENT_LIMIT = -5;

                    int bits = BitConverter.SingleToInt32Bits(value);
                    int binaryExponent = ((bits >> 23) & 0xFF) - 127;
                    exponent = (int)(binaryExponent * 0.34f);
                    numIntegralDigits = Math.Max(0, exponent + 1);
                    numFractionalDigits = Math.Max(0, MAX_SIGNIFICANT_DIGITS - numIntegralDigits);
                    printExponent = false;


                    if (exponent < NEG_EXPONENT_LIMIT || exponent > POS_EXPONENT_LIMIT)
                    {
                        printExponent = true;
                        value = (float)(value * Math.Pow(10, -exponent));
                        while (value < 1)
                        {
                            value *= 10;
                            exponent -= 1;
                        }
                        while (value >= 10)
                        {
                            value /= 10;
                            exponent += 1;
                        }
                        numIntegralDigits = 1;
                        numFractionalDigits = MAX_SIGNIFICANT_DIGITS - 2;
                    }

                    return value;
                }

                void WriteIntegralPart(int numIntegralDigits, float integralPart)
                {
                    if (integralPart == 0)
                    {
                        WriteToBufferWithoutCheck((byte)'0');
                    }
                    else
                    {

                        int integralInt = (int)integralPart;
                        int index = numIntegralDigits;
                        int numLeadingZeros = 0;

                        for (int i = 0; i < numIntegralDigits; i++)
                        {
                            integralInt = (int)Math.DivRem(integralInt, 10, out long digitLong);
                            localBuffer[index--] = (byte)('0' + (byte)digitLong);
                            if (digitLong == 0) numLeadingZeros++;
                            else numLeadingZeros = 0;
                        }
                        index += numLeadingZeros;
                        WriteToBufferWithoutCheck(localBuffer, index + 1, numIntegralDigits - index);
                    }
                }

                void WriteFractionalPart(int numFractionalDigits, float fractionalPart)
                {
                    if (fractionalPart == 0)
                    {
                        WriteToBufferWithoutCheck((byte)'0');
                        return;
                    }
                    if (numFractionalDigits == 0)
                    {
                        if (fractionalPart >= 0.5f) WriteToBufferWithoutCheck((byte)'1');
                        else WriteToBufferWithoutCheck((byte)'0');
                        return;
                    }

                    int firstFractionalDigitIndex = mainBufferCount;
                    for (int i = 0; i <= numFractionalDigits; i++)
                    {
                        fractionalPart *= 10;
                        byte digit = (byte)fractionalPart;
                        fractionalPart -= digit;
                        WriteToBufferWithoutCheck((byte)('0' + digit));
                    }

                    int correctionIndex = mainBufferCount - 1;
                    while (mainBuffer[correctionIndex] == '0' && correctionIndex > firstFractionalDigitIndex)
                    {
                        correctionIndex--;
                    }
                    int oldMainBufferCount = mainBufferCount;
                    mainBufferCount = correctionIndex + 1;
                    if (oldMainBufferCount != mainBufferCount)
                    {
                        return;
                    }

                    while (mainBuffer[correctionIndex] == '9' && correctionIndex > firstFractionalDigitIndex)
                    {
                        correctionIndex--;
                    }

                    if (mainBuffer[correctionIndex] < '9')
                    {
                        mainBuffer[correctionIndex] += 1;
                        mainBufferCount = correctionIndex + 1;
                        return;
                    }
                }

            }

            private void WriteDouble(double value)
            {
                if (HandleSpecialCases(value)) return;

                EnsureFreeBufferSpace(100);

                if (value < 0)
                {
                    value = -value;
                    WriteToBufferWithoutCheck((byte)'-');
                }

                value = CalculateNumDigits(value, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent);

                double integralPart = Math.Floor(value);
                double fractionalPart = value - integralPart;

                WriteIntegralPart(numIntegralDigits, integralPart);

                WriteToBufferWithoutCheck((byte)'.');

                WriteFractionalPart(numFractionalDigits, fractionalPart);

                if (printExponent)
                {
                    WriteToBuffer((byte)'E');
                    WriteSignedInteger(exponent);
                }

                // Local Functions

                bool HandleSpecialCases(double value)
                {
                    if (value == 0)
                    {
                        WriteToBuffer(ZERO_FLOAT);
                        return true;
                    }
                    if (IsSpecial(value))
                    {
                        if (Double.IsNaN(value)) WriteToBuffer(NAN);
                        if (Double.IsNegativeInfinity(value)) WriteToBuffer(NEG_INFINITY);
                        if (Double.IsPositiveInfinity(value)) WriteToBuffer(POS_INFINITY);
                        if (Double.IsSubnormal(value)) WriteString(value.ToString());
                        return true;
                    }

                    return false;
                }

                static double CalculateNumDigits(double value, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent)
                {
                    const int MAX_SIGNIFICANT_DIGITS = 16;
                    const int POS_EXPONENT_LIMIT = 7;
                    const int NEG_EXPONENT_LIMIT = -5;

                    long bits = BitConverter.DoubleToInt64Bits(value);
                    int binaryExponent = (int)((bits >> 52) & 0x7FF) - 1023;
                    exponent = (int)(binaryExponent * 0.34f);
                    numIntegralDigits = Math.Max(0, exponent + 1);
                    numFractionalDigits = Math.Max(0, MAX_SIGNIFICANT_DIGITS - numIntegralDigits);
                    printExponent = false;

                    if (exponent < NEG_EXPONENT_LIMIT || exponent > POS_EXPONENT_LIMIT)
                    {
                        printExponent = true;
                        value = (value * Math.Pow(10, -exponent));
                        while (value < 1)
                        {
                            value *= 10;
                            exponent -= 1;
                        }
                        while (value >= 10)
                        {
                            value /= 10;
                            exponent += 1;
                        }
                        numIntegralDigits = 1;
                        numFractionalDigits = MAX_SIGNIFICANT_DIGITS - 3;
                    }

                    return value;
                }

                void WriteIntegralPart(int numIntegralDigits, double integralPart)
                {
                    if (integralPart == 0)
                    {
                        WriteToBufferWithoutCheck((byte)'0');
                    }
                    else
                    {
                        long integralInt = (long)integralPart;
                        int index = numIntegralDigits;
                        int numLeadingZeros = 0;

                        for (int i = 0; i < numIntegralDigits; i++)
                        {
                            integralInt = Math.DivRem(integralInt, 10, out long digitLong);
                            localBuffer[index--] = (byte)('0' + (byte)digitLong);
                            if (digitLong == 0) numLeadingZeros++;
                            else numLeadingZeros = 0;
                        }
                        index += numLeadingZeros;
                        WriteToBufferWithoutCheck(localBuffer, index + 1, numIntegralDigits - index);
                    }
                }

                void WriteFractionalPart(int numFractionalDigits, double fractionalPart)
                {
                    if (fractionalPart == 0)
                    {
                        WriteToBufferWithoutCheck((byte)'0');
                        return;
                    }
                    if (numFractionalDigits == 0)
                    {
                        if (fractionalPart >= 0.5f) WriteToBufferWithoutCheck((byte)'1');
                        else WriteToBufferWithoutCheck((byte)'0');
                        return;
                    }

                    int firstFractionalDigitIndex = mainBufferCount;
                    for (int i = 0; i <= numFractionalDigits; i++)
                    {
                        fractionalPart *= 10;
                        byte digit = (byte)fractionalPart;
                        fractionalPart -= digit;
                        WriteToBufferWithoutCheck((byte)('0' + digit));
                    }

                    int correctionIndex = mainBufferCount - 1;
                    while (mainBuffer[correctionIndex] == '0' && correctionIndex > firstFractionalDigitIndex)
                    {
                        correctionIndex--;
                    }
                    int oldMainBufferCount = mainBufferCount;
                    mainBufferCount = correctionIndex + 1;
                    if (oldMainBufferCount != mainBufferCount)
                    {
                        return;
                    }

                    while (mainBuffer[correctionIndex] == '9' && correctionIndex > firstFractionalDigitIndex)
                    {
                        correctionIndex--;
                    }

                    if (mainBuffer[correctionIndex] < '9')
                    {
                        mainBuffer[correctionIndex] += 1;
                        mainBufferCount = correctionIndex + 1;
                        return;
                    }
                }

            }


        }
            
    }
}

