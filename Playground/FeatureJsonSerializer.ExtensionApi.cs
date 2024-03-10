using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Playground
{
    public sealed partial class FeatureJsonSerializer
    {
        public sealed class ExtensionApi
        {
            FeatureJsonSerializer s;
            JsonUTF8StreamWriter w;

            public ExtensionApi(FeatureJsonSerializer serializer)
            {
                this.s = serializer;
                this.w = serializer.writer;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public CachedTypeHandler GetCachedTypeHandler(Type type) => s.GetCachedTypeHandler(type);


            public byte[] Buffer => w.Buffer;
            public int BufferCount => w.BufferCount;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ResetBuffer() => w.ResetBuffer();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBuffer(byte[] data, int offset, int count) => w.WriteToBuffer(data, offset, count);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBuffer(byte[] data, int count) => w.WriteToBuffer(data, count);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBuffer(byte[] data) => w.WriteToBuffer(data);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBuffer(ArraySegment<byte> data) => w.WriteToBuffer(data);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBuffer(byte data) => w.WriteToBuffer(data);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBufferWithoutCheck(byte[] data, int offset, int count) => w.WriteToBufferWithoutCheck(data, offset, count);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBufferWithoutCheck(byte[] data, int count) => w.WriteToBufferWithoutCheck(data, count);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBufferWithoutCheck(byte[] data) => w.WriteToBufferWithoutCheck(data);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBufferWithoutCheck(byte data) => w.WriteToBufferWithoutCheck(data);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EnsureFreeBufferSpace(int freeBytes) => w.EnsureFreeBufferSpace(freeBytes);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteNullValue() => w.WriteNullValue();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenObject() => w.OpenObject();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseObject() => w.CloseObject();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteTypeInfo(string typeName) => w.WriteTypeInfo(typeName);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareTypeInfo(string typeName) => w.PrepareTypeInfo(typeName);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteValueFieldName() => w.WriteValueFieldName();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteFieldName(string fieldName) => w.WriteFieldName(fieldName);            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy<T>(T value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString<T>(T value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(long value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(long value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(long value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(ulong value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(ulong value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(ulong value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(int value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(int value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(int value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(uint value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(uint value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(uint value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(byte value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(byte value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(byte value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(sbyte value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(sbyte value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(sbyte value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(short value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(short value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(short value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(ushort value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(ushort value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(ushort value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(float value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(float value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(float value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(double value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(decimal value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(double value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(double value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(bool value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(bool value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(bool value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(string str) => w.WritePrimitiveValue(str);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(string str) => w.WritePrimitiveValueAsStringWithCopy(str);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(string str) => w.WritePrimitiveValueAsString(str);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValue(char value) => w.WritePrimitiveValue(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WritePrimitiveValueAsStringWithCopy(char value) => w.WritePrimitiveValueAsStringWithCopy(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WritePrimitiveValueAsString(char value) => w.WritePrimitiveValueAsString(value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void OpenCollection() => w.OpenCollection();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CloseCollection() => w.CloseCollection();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteComma() => w.WriteComma();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RemoveTrailingComma() => w.RemoveTrailingComma();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteDot() => w.WriteDot();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteColon() => w.WriteColon();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareFieldNameBytes(string fieldname) => w.PrepareFieldNameBytes(fieldname);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareStringToBytes(string str) => w.PrepareStringToBytes(str);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareRootName() => w.PrepareRootName();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public byte[] PrepareTextToBytes(string enumText) => w.PrepareTextToBytes(enumText);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> PrepareCollectionIndexName(int index) => w.PrepareCollectionIndexName(index);            
        }

    }
}
