using System.Text;
using FeatureLoom.Extensions;
using System.IO;
using System.Runtime.CompilerServices;
using System;
#if !NETSTANDARD2_0
using System.Buffers.Text;
#endif
#if NET7_0_OR_GREATER
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
#endif
using System.Collections.Generic;
using FeatureLoom.Helpers;
using System.Reflection;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using FeatureLoom.Collections;
using FeatureLoom.Synchronization;
using System.Xml;

namespace FeatureLoom.Serialization;

public sealed partial class JsonSerializer
{
    public interface IWriter
    {
        byte[] Buffer { get; }
        int BufferCount { get; }

            void CloseArray();
            void CloseObject();
            void EnsureFreeBufferSpace(int freeBytes);
            void OpenArray();
            void OpenObject();
            ByteSegment GetCollectionIndexName(int index);
            byte[] PrepareFieldNameBytes(string fieldname);
            byte[] PrepareRootName();
            byte[] PrepareStringToBytes(string str);
            byte[] PrepareTextToBytes(string enumText);
            byte[] PrepareTypeInfo(string typeName);
            void ResetBuffer();
            void WriteBufferToStream();
            void WriteColon();
            void WriteComma();
            void WriteDot();
            void WriteFieldName(string fieldName);
            void WriteNullValue();
            void WriteBoolValue(bool value);
            void WriteByteValue(byte value);
            void WriteCharValue(char value);
            void WriteDecimalValue(decimal value);
            void WriteDoubleValue(double value);
            void WriteFloatValue(float value);
            void WriteIntValue(int value);
            void WriteLongValue(long value);
            void WriteSbyteValue(sbyte value);
            void WriteShortValue(short value);
            void WriteStringValue(string str);
            void WriteUintValue(uint value);
            void WriteUlongValue(ulong value);
            void WriteUshortValue(ushort value);
            void WriteGuidValue(Guid value);
            void WriteDateTimeValue(DateTime value);
            void WriteUriValue(Uri value);
            void WriteBoolAsStringValue(bool value);
            void WriteByteAsStringValue(byte value);
            void WriteCharValueAsString(char value);
            void WriteDoubleValueAsString(double value);
            void WriteFloatValueAsString(float value);
            void WriteIntValueAsString(int value);
            void WriteLongValueAsString(long value);
            void WriteSbyteValueAsString(sbyte value);
            void WriteShortValueAsString(short value);
            void WriteUintValueAsString(uint value);
            void WriteUlongValueAsString(ulong value);
            void WriteUshortValueAsString(ushort value);
            ByteSegment WriteBoolValueAsStringWithCopy(bool value);
            ByteSegment WriteByteValueAsStringWithCopy(byte value);
            ByteSegment WriteCharValueAsStringWithCopy(char value);
            ByteSegment WriteDoubleValueAsStringWithCopy(double value);
            ByteSegment WriteFloatValueAsStringWithCopy(float value);
            ByteSegment WriteIntValueAsStringWithCopy(int value);
            ByteSegment WriteLongValueAsStringWithCopy(long value);
            ByteSegment WriteSbyteValueAsStringWithCopy(sbyte value);
            ByteSegment WriteShortValueAsStringWithCopy(short value);
            ByteSegment WriteStringValueAsStringWithCopy(string str);
            ByteSegment WriteUintValueAsStringWithCopy(uint value);
            ByteSegment WriteUlongValueAsStringWithCopy(ulong value);
            ByteSegment WriteUshortValueAsStringWithCopy(ushort value);
            void WriteToBuffer(ByteSegment data);
            void WriteToBuffer(byte data);
            void WriteToBuffer(byte[] data);
            void WriteToBuffer(byte[] data, int count);
            void WriteToBuffer(byte[] data, int offset, int count);
            void WriteToBufferWithoutCheck(byte data);
            void WriteToBufferWithoutCheck(byte[] data);
            void WriteToBufferWithoutCheck(byte[] data, int count);
            void WriteToBufferWithoutCheck(byte[] data, int offset, int count);
            void WriteTypeInfo(string typeName);
            void WriteValueFieldName();
            bool TryPreparePrimitiveWriteDelegate<T>(out Action<T> primitiveWriteDelegate);
            void WriteRawJsonFragment(string json);
        }

    private sealed class JsonUTF8StreamWriter : IWriter
    {
        public Stream stream;
        private byte[] tempBuffer;
        private byte[] mainBuffer;
        private int mainBufferCount;
        private int mainBufferLimit;
        private SlicedBuffer<byte> tempSlicedBuffer;
        private CompiledSettings settings;
        private readonly bool indent;
        private int currentIndentionDepth = 0;
        private readonly int maxIndentationDepth;
        private readonly byte[][] indentationLookup;
        const int BUFFER_LIMIT_MARGIN = 64; // Margin to avoid frequent buffer checks

        public JsonUTF8StreamWriter(CompiledSettings settings)
        {
            // We lower the limit by a small margin in order to not always check remaining space
            mainBufferLimit = settings.writeBufferChunkSize - BUFFER_LIMIT_MARGIN;            
            mainBuffer = new byte[settings.writeBufferChunkSize];
            // Small temporary buffer for writing primitive values
            tempBuffer = new byte[128];
            // Used for temporarily needed names e.g. Dictionary
            tempSlicedBuffer = new SlicedBuffer<byte>(settings.tempBufferSize, settings.tempBufferSize * 64, 4, true, false);
            this.settings = settings;

            indent = settings.indent;
            maxIndentationDepth = settings.maxIndentationDepth;
            indentationLookup = new byte[maxIndentationDepth+1][];
            InitIndentationLookup();
        }

        private void ExtendBufferLimit(int newLimit)
        {
            if (newLimit <= mainBufferLimit) return; // No need to extend

            mainBufferLimit = newLimit;

            int newSize= newLimit + BUFFER_LIMIT_MARGIN;            
            if (mainBufferCount == 0) mainBuffer = new byte[newSize];
            else Array.Resize(ref mainBuffer, newLimit + BUFFER_LIMIT_MARGIN);            
        }

        private void InitIndentationLookup()
        {                
            if (!indent) return;                

            List<byte> indentationBytes = new List<byte>();
            indentationBytes.Add((byte)'\n');
            for (int i = 0; i <= settings.maxIndentationDepth; i++)
            {                                        
                indentationLookup[i] = indentationBytes.ToArray();
                for (int j = 0; j < settings.indentationFactor; j++) indentationBytes.Add((byte)' ');
            }
        }

        private void WriteNextLine()
        {
            var indentationBytes = indentationLookup[Math.Min(currentIndentionDepth,maxIndentationDepth)];
            WriteToBuffer(indentationBytes);
        }


        public byte[] Buffer => mainBuffer;
        public int BufferCount
        {
            get => mainBufferCount;
            set => mainBufferCount = value;
        }

        public void WriteBufferToStream()
        {
            try
            {
                stream.Write(mainBuffer, 0, mainBufferCount);
                mainBufferCount = 0;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed writing to stream", ex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task WriteBufferToStreamAsync()
        {
            try
            {
                await stream.WriteAsync(mainBuffer, 0, mainBufferCount).ConfiguredAwait();
                mainBufferCount = 0;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed writing to stream", ex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetBuffer()
        {
            mainBufferCount = 0;
            tempSlicedBuffer.Reset(true);
            currentIndentionDepth = 0;
        }

        /// <summary>
        /// Copies a byte range into the target buffer. Delegates to the runtime's memmove,
        /// which already handles short blobs with a couple of overlapping wide moves and needs
        /// only a single length check, so a hand-written loop with its per-element bounds
        /// checks cannot beat it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyBytes(byte[] source, int sourceOffset, byte[] target, int targetOffset, int count)
        {
#if !NETSTANDARD2_0
            new ReadOnlySpan<byte>(source, sourceOffset, count).CopyTo(new Span<byte>(target, targetOffset, count));
#else
            System.Buffer.BlockCopy(source, sourceOffset, target, targetOffset, count);
#endif
        }

        /// <summary>
        /// Copies a complete array into the target buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyBytes(byte[] source, byte[] target, int targetOffset)
        {
#if !NETSTANDARD2_0
            new ReadOnlySpan<byte>(source).CopyTo(new Span<byte>(target, targetOffset, source.Length));
#else
            System.Buffer.BlockCopy(source, 0, target, targetOffset, source.Length);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBuffer(byte[] data, int offset, int count)
        {
            if (mainBufferCount + count > mainBufferLimit) WriteBufferToStream();
            CopyBytes(data, offset, mainBuffer, mainBufferCount, count);
            mainBufferCount += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBuffer(byte[] data, int count)
        {
            WriteToBuffer(data, 0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBuffer(byte[] data)
        {
            if (mainBufferCount + data.Length > mainBufferLimit) WriteBufferToStream();
            CopyBytes(data, mainBuffer, mainBufferCount);
            mainBufferCount += data.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBuffer(ByteSegment data)
        {
            var segment = data.AsArraySegment;
            int count = segment.Count;
            if (mainBufferCount + count > mainBufferLimit) WriteBufferToStream();
            CopyBytes(segment.Array, segment.Offset, mainBuffer, mainBufferCount, count);
            mainBufferCount += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBuffer(byte data)
        {
            if (mainBufferCount >= mainBufferLimit) WriteBufferToStream();
            mainBuffer[mainBufferCount++] = data;
        }

        /// <summary>
        /// Writes a short, pre-encoded byte sequence (e.g. a field name incl. quotes, colon and
        /// optional leading comma).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePreparedBytes(byte[] data)
        {
            WriteToBuffer(data);
        }

        public static readonly MethodInfo WritePreparedBytesMethod = typeof(JsonUTF8StreamWriter).GetMethod(nameof(WritePreparedBytes), BindingFlags.Public | BindingFlags.Instance);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBufferWithoutCheck(byte[] data, int offset, int count)
        {
            CopyBytes(data, offset, mainBuffer, mainBufferCount, count);
            mainBufferCount += count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBufferWithoutCheck(byte[] data, int count)
        {
            WriteToBufferWithoutCheck(data, 0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBufferWithoutCheck(byte[] data)
        {
            CopyBytes(data, mainBuffer, mainBufferCount);
            mainBufferCount += data.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToBufferWithoutCheck(byte data)
        {
            mainBuffer[mainBufferCount++] = data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureFreeBufferSpace(int freeBytes)
        {
            if (mainBufferCount + freeBytes >= mainBufferLimit) WriteBufferToStream();
            if (mainBufferCount + freeBytes >= mainBuffer.Length) ExtendBufferLimit(mainBufferCount + freeBytes);
        }

        static Dictionary<Type, string> typeMap = new Dictionary<Type, string>
        {
            { typeof(sbyte), "WriteSbyteValue" },
            { typeof(byte), "WriteByteValue" },
            { typeof(short), "WriteShortValue" },
            { typeof(ushort), "WriteUshortValue" },
            { typeof(int), "WriteIntValue" },
            { typeof(uint), "WriteUintValue" },
            { typeof(long), "WriteLongValue" },
            { typeof(ulong), "WriteUlongValue" },
            { typeof(float), "WriteFloatValue" },
            { typeof(double), "WriteDoubleValue" },
            { typeof(decimal), "WriteDecimalValue" },
            { typeof(char), "WriteCharValue" },
            { typeof(bool), "WriteBoolValue" },
            { typeof(string), "WriteStringValue" }
        };

        public bool TryPreparePrimitiveWriteDelegate<T>(out Action<T> primitiveWriteDelegate)
        {
            Type type = typeof(T);
            primitiveWriteDelegate = null;
            if (settings.itemHandlerCreators.Any(creator => creator.SupportsType(type))) return false;
            

            if (typeMap.TryGetValue(type, out string methodName))
            {
                MethodInfo methodInfo = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

                if (methodInfo != null)
                {
                    try
                    {
                        primitiveWriteDelegate = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), this, methodInfo);
                        return true;
                    }
                    catch (ArgumentException)
                    {
                        // This catch block is here in case the delegate creation fails due to a mismatch,
                        // which should not happen if the methods are correctly defined and matched.
                        throw new Exception($"Method {methodName} not found!");
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Like <see cref="TryPreparePrimitiveWriteDelegate{T}"/>, but returns the MethodInfo instead of a
        /// bound delegate, so callers can emit a direct call into a compiled expression tree.
        /// </summary>
        public bool TryGetPrimitiveWriteMethod<T>(out MethodInfo methodInfo)
        {
            Type type = typeof(T);
            methodInfo = null;
            if (settings.itemHandlerCreators.Any(creator => creator.SupportsType(type))) return false;
            if (!typeMap.TryGetValue(type, out string methodName)) return false;
            methodInfo = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            return methodInfo != null;
        }

        static readonly byte[] NULL = "null".ToByteArray();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteNullValue() => WriteToBuffer(NULL);

        static readonly byte OPEN_OBJECT = (byte)'{';
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OpenObject()
        {
            WriteToBufferWithoutCheck(OPEN_OBJECT);
            if (indent)
            {
                currentIndentionDepth++;
                WriteNextLine();
            }
        }

        static readonly byte CLOSE_OBJECT = (byte)'}';
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CloseObject()
        {
            if (indent)
            {
                currentIndentionDepth--;
                WriteNextLine();
            }
            WriteToBufferWithoutCheck(CLOSE_OBJECT);
        }

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


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] PreparePrimitiveToBytes<T>(T value)
        {
            return Encoding.UTF8.GetBytes(value.ToString()); // TODO optimize
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteLongValue(long value)
        {
            WriteSignedInteger(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteIntPtrValue(IntPtr value)
        {
            WriteSignedInteger((long)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUintPtrValue(UIntPtr value)
        {
            WriteUnsignedInteger((ulong)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteLongValueAsStringWithCopy(long value)
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
        public void WriteLongValueAsString(long value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteSignedInteger(value);
            WriteToBufferWithoutCheck(QUOTES);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUlongValue(ulong value)
        {
            WriteUnsignedInteger(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteUlongValueAsStringWithCopy(ulong value)
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
        public void WriteUlongValueAsString(ulong value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteUnsignedInteger(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteIntValue(int value)
        {
            WriteSignedInteger(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteIntValueAsStringWithCopy(int value)
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
        public void WriteIntValueAsString(int value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteSignedInteger(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUintValue(uint value)
        {
            WriteUnsignedInteger(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteUintValueAsStringWithCopy(uint value)
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
        public void WriteUintValueAsString(uint value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteUnsignedInteger(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteValue(byte value)
        {
            WriteByte(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteByteValueAsStringWithCopy(byte value)
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
        public void WriteByteAsStringValue(byte value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteUnsignedInteger(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSbyteValue(sbyte value)
        {
            WriteSignedInteger(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteSbyteValueAsStringWithCopy(sbyte value)
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
        public void WriteSbyteValueAsString(sbyte value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteSignedInteger(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteShortValue(short value)
        {
            WriteSignedInteger(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteShortValueAsStringWithCopy(short value)
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
        public void WriteShortValueAsString(short value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteSignedInteger(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUshortValue(ushort value)
        {
            WriteUnsignedInteger(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteUshortValueAsStringWithCopy(ushort value)
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
        public void WriteUshortValueAsString(ushort value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteUnsignedInteger(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteFloatValue(float value)
        {
            WriteFloat(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteFloatValueAsStringWithCopy(float value)
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
        public void WriteFloatValueAsString(float value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteFloat(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDoubleValue(double value)
        {
            WriteDouble(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDecimalValue(decimal value)
        {
            WriteDouble((double)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteDoubleValueAsStringWithCopy(double value)
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
        public void WriteDoubleValueAsString(double value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteDouble(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        static readonly byte[] BOOLVALUE_TRUE = "true".ToByteArray();
        static readonly byte[] BOOLVALUE_FALSE = "false".ToByteArray();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBoolValue(bool value)
        {
            var bytes = value ? BOOLVALUE_TRUE : BOOLVALUE_FALSE;
            WriteToBuffer(bytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteBoolValueAsStringWithCopy(bool value)
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
        public void WriteBoolAsStringValue(bool value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            var bytes = value ? BOOLVALUE_TRUE : BOOLVALUE_FALSE;
            WriteToBuffer(bytes);
            WriteToBufferWithoutCheck(QUOTES);
        }

        static readonly byte QUOTES = (byte)'\"';
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteStringValue(string str)
        {
            if (str != null)
            {
                WriteEscapedStringWithQuotes(str);
            }
            else WriteNullValue();
        }

        /// <summary>
        /// Writes a pre-encoded UTF-8 string value, wrapping it in quotes without escaping.
        /// Only valid for content known to be free of characters requiring escaping,
        /// e.g. enum names, which are always valid C# identifiers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePreparedStringValue(byte[] utf8Bytes)
        {
            if (mainBufferCount + utf8Bytes.Length + 2 > mainBufferLimit) WriteBufferToStream();
            mainBuffer[mainBufferCount++] = QUOTES;
            CopyBytes(utf8Bytes, mainBuffer, mainBufferCount);
            mainBufferCount += utf8Bytes.Length;
            mainBuffer[mainBufferCount++] = QUOTES;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteTextSegmentValue(TextSegment textSegment)
        {
            if (textSegment.IsValid)
            {
                WriteEscapedStringWithQuotes(textSegment.UnderlyingString, textSegment.Offset, textSegment.Count);
            }
            else WriteNullValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteJsonFragmentValue(JsonFragment json)
        {
            if (json.IsString) WriteString(json.JsonString);
            else if (json.IsUtf8) WriteToBuffer(json.JsonUtf8);
            else WriteNullValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteStringValueAsStringWithCopy(string str)
        {

            var countBefore = mainBufferCount;

            WriteEscapedStringWithQuotes(str);

            // Quotes must be removed from string
            var writtenBytes = mainBufferCount - countBefore - 2;
            var slice = tempSlicedBuffer.GetSlice(writtenBytes - 2);
            slice.CopyFrom(mainBuffer, countBefore+1, writtenBytes);
            return slice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePrimitiveValueAsString(string str)
        {
            WriteEscapedStringWithQuotes(str);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCharValue(char value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteChar(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment WriteCharValueAsStringWithCopy(char value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            EnsureFreeBufferSpace(64);
            var countBefore = mainBufferCount;

            WriteChar(value);

            var writtenBytes = mainBufferCount - countBefore;
            var slice = tempSlicedBuffer.GetSlice(writtenBytes);
            slice.CopyFrom(mainBuffer, countBefore, writtenBytes);

            WriteToBufferWithoutCheck(QUOTES);
            return slice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCharValueAsString(char value)
        {
            WriteToBufferWithoutCheck(QUOTES);
            WriteChar(value);
            WriteToBufferWithoutCheck(QUOTES);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesAsBase64(ByteSegment value)
        {
            if (value.IsValid) WriteBase64(value);
            else WriteNullValue();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesAsArray(ByteSegment value)
        {
            if (value.IsValid) WriteByteArray(value);
            else WriteNullValue();
        }

        private void WriteByteArray(ByteSegment value)
        {
            int numInputBytes = value.Count;
            // Each element needs at most 3 digits plus a separator, and the lookup always
            // writes a full 4-byte chunk, so a little extra slack is reserved for the last one.
            int bytesToReserve = 2 + numInputBytes * 4 + 4;
            EnsureFreeBufferSpace(bytesToReserve);
#if !NETSTANDARD2_0
            var bytes = value.AsSpan();
#else
            var bytes = value;
#endif            
            if (numInputBytes == 0)
            {
                WriteToBufferWithoutCheck((byte)'[');
                WriteToBufferWithoutCheck((byte)']');
                return;
            }

            WriteToBufferWithoutCheck((byte)'[');
            WriteFromNumberLookupWithoutCheck(bytes[0]);
            for (int i = 1; i < bytes.Length; i++)
            {
                WriteToBufferWithoutCheck((byte)',');
                WriteFromNumberLookupWithoutCheck(bytes[i]);
            }
            WriteToBufferWithoutCheck((byte)']');
        }

        private static readonly byte[] Base64Chars = System.Text.Encoding.ASCII.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");

        /// <summary>
        /// Contains the two base64 characters for every 12-bit group, stored in 2-byte chunks,
        /// so a 3-byte input block can be written as two 2-byte blocks.
        /// </summary>
        private static readonly byte[] Base64PairLookup = CreateBase64PairLookup();

        private static byte[] CreateBase64PairLookup()
        {
            var lookup = new byte[4096 * 2];
            for (int i = 0; i < 4096; i++)
            {
                lookup[i * 2] = Base64Chars[(i >> 6) & 0x3F];
                lookup[i * 2 + 1] = Base64Chars[i & 0x3F];
            }
            return lookup;
        }

        /// <summary>
        /// Writes the two base64 characters of a 12-bit group as one 2-byte block. The caller
        /// must have ensured the free space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteBase64Pair(int group12Bit)
        {
            var lookup = Base64PairLookup;
            var buffer = mainBuffer;
            int offset = group12Bit * 2;
            int pos = mainBufferCount;
#if NET7_0_OR_GREATER
            ushort chunk = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lookup), offset));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), pos), chunk);
#else
            buffer[pos] = lookup[offset];
            buffer[pos + 1] = lookup[offset + 1];
#endif
            mainBufferCount = pos + 2;
        }

        private void WriteBase64(ByteSegment value)
        {
            int numInputBytes = value.Count;            
            int fullBlocks = numInputBytes / 3;                        
            int bytesToReserve = 2 + (fullBlocks+1) * 4;
            EnsureFreeBufferSpace(bytesToReserve);

#if !NETSTANDARD2_0
            var bytes = value.AsSpan();
#else
            var bytes = value;
#endif

            WriteToBufferWithoutCheck((byte)'"');
            int inputIndex = 0;            
            for (int i = 0; i < fullBlocks; i++)
            {
                int bufferValue = (bytes[inputIndex++] << 16) & 0xFFFFFF;
                bufferValue |= (bytes[inputIndex++] << 8);
                bufferValue |= bytes[inputIndex++];
                WriteBase64Pair((bufferValue >> 12) & 0xFFF);
                WriteBase64Pair(bufferValue & 0xFFF);
            }

            int remainingBytes = numInputBytes - inputIndex;
            if (remainingBytes == 1)
            {
                int bufferValue = (bytes[inputIndex++] << 16) & 0xFFFFFF;
                WriteBase64Pair((bufferValue >> 12) & 0xFFF);
                WriteToBufferWithoutCheck((byte)'=');
                WriteToBufferWithoutCheck((byte)'=');
            }
            else if(remainingBytes == 2)
            {
                int bufferValue = (bytes[inputIndex++] << 16) & 0xFFFFFF;
                bufferValue |= (bytes[inputIndex++] << 8);
                WriteBase64Pair((bufferValue >> 12) & 0xFFF);
                WriteToBufferWithoutCheck(Base64Chars[(bufferValue >> 6) & 0x3F]);
                WriteToBufferWithoutCheck((byte)'=');
            }

            WriteToBufferWithoutCheck((byte)'"');
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
                if (name.Get(0) != OPENARRAY) WriteDot();
                WriteToBuffer(name);
            }
            WriteToBuffer(REFOBJECT_POST);
        }

        static readonly byte OPENARRAY = (byte)'[';
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OpenArray()
        {
            WriteToBufferWithoutCheck(OPENARRAY);
            if (indent)
            {
                currentIndentionDepth++;
                WriteNextLine();
            }
        }

        static readonly byte CLOSEARRAY = (byte)']';
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CloseArray()
        {
            if (indent)
            {
                currentIndentionDepth--;
                WriteNextLine();
            }
            WriteToBufferWithoutCheck(CLOSEARRAY);
        }

        static readonly byte COMMA = (byte)',';
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteComma()
        {
            WriteToBufferWithoutCheck(COMMA);
            if (indent) WriteNextLine();                
        }

        static readonly byte DOT = (byte)'.';
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDot() => WriteToBufferWithoutCheck(DOT);

        static readonly byte COLON = (byte)':';
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColon() => WriteToBufferWithoutCheck(COLON);

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
        public byte[] PrepareTextToBytes(string enumText)
        {
            return Encoding.UTF8.GetBytes($"\"{enumText}\"");
        }

        List<ByteSegment> indexNameList = new List<ByteSegment>();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteSegment GetCollectionIndexName(int index)
        {
            if (!settings.requiresItemNames) return default;

            if (index >= indexNameList.Count)
            {
                for (int i = indexNameList.Count; i <= index; i++)
                {
                    indexNameList.Add(default);
                }
            }
            if (!indexNameList[index].IsValid) indexNameList[index] = new ByteSegment($"[{index}]",true);
            return indexNameList[index];
        }

        /// <summary>
        /// Exclusive upper bound of the values covered by <see cref="NumberLookup"/>.
        /// </summary>
        private const int NUMBER_LOOKUP_LIMIT = 10000;

        /// <summary>
        /// Fill byte of the unused trailing bytes of a lookup chunk. It is below any ASCII
        /// digit, so it marks the end of the digits without needing their count.
        /// </summary>
        private const byte NUMBER_LOOKUP_PADDING = 0;

        /// <summary>
        /// Single flat table holding the ASCII digits of all values from 0 to 9999 in
        /// left-aligned 4-byte chunks (unused trailing bytes hold <see cref="NUMBER_LOOKUP_PADDING"/>).
        /// A single array avoids the per-value array objects and the extra indirection of a
        /// jagged lookup, and the left alignment makes the chunk start a plain value*4, so a
        /// number is written by copying a fixed 4-byte block and advancing only by its digit count.
        /// </summary>
        private static readonly byte[] NumberLookup = InitNumberLookup();

        /// <summary>
        /// Same layout as <see cref="NumberLookup"/>, but the chunks are right-aligned and
        /// filled with '0'. Used for the trailing 4-digit groups of larger numbers, where the
        /// leading zeros are significant, so a group is written as one unconditional 4-byte block.
        /// </summary>
        private static readonly byte[] NumberLookupZeroPadded = InitNumberLookupZeroPadded();

        private static readonly byte[] Int32MinValueBytes = int.MinValue.ToString().ToByteArray();
        private static readonly byte[] Int64MinValueBytes = long.MinValue.ToString().ToByteArray();

        private static byte[] InitNumberLookup()
        {
            byte[] lookup = new byte[NUMBER_LOOKUP_LIMIT * 4];
            for (int i = 0; i < NUMBER_LOOKUP_LIMIT; i++)
            {
                int digits = CountDigits((uint)i);
                int chunkStart = i * 4;
                int pos = chunkStart + digits;
                for (int p = pos; p < chunkStart + 4; p++) lookup[p] = NUMBER_LOOKUP_PADDING;
                int value = i;
                do
                {
                    lookup[--pos] = (byte)('0' + (value % 10));
                    value /= 10;
                }
                while (value > 0);
            }
            return lookup;
        }

        private static byte[] InitNumberLookupZeroPadded()
        {
            byte[] lookup = new byte[NUMBER_LOOKUP_LIMIT * 4];
            for (int i = 0; i < NUMBER_LOOKUP_LIMIT; i++)
            {
                int pos = i * 4 + 4;
                int value = i;
                for (int d = 0; d < 4; d++)
                {
                    lookup[--pos] = (byte)('0' + (value % 10));
                    value /= 10;
                }
            }
            return lookup;
        }

        /// <summary>
        /// Writes a value below <see cref="NUMBER_LOOKUP_LIMIT"/> by copying its 4-byte chunk
        /// out of <see cref="NumberLookup"/>. The trailing padding bytes are written too, but
        /// are not counted and are overwritten by the next write. The digit count is derived
        /// from the already loaded padding bytes, so no separate digit calculation is needed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFromNumberLookup(uint value)
        {
            if (mainBufferCount + 4 > mainBufferLimit) WriteBufferToStream();
            WriteFromNumberLookupWithoutCheck(value);
        }

        /// <summary>
        /// Same as <see cref="WriteFromNumberLookup(uint)"/>, but the caller must have ensured
        /// space for 4 bytes, even if fewer digits are counted.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFromNumberLookupWithoutCheck(uint value)
        {
            var lookup = NumberLookup;
            var buffer = mainBuffer;
            int offset = (int)value * 4;
            int pos = mainBufferCount;
#if NET7_0_OR_GREATER
            // The whole chunk is moved as a single 4-byte load/store, which also removes the
            // bounds checks of the byte-wise accesses. The free space was ensured above.
            uint chunk = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lookup), offset));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), pos), chunk);

            // The padding bytes are zero, so the digit count follows from the position of the
            // highest non-zero byte. The chunk is never zero, because the first digit is always
            // written.
            int digits = BitConverter.IsLittleEndian
                ? 4 - (BitOperations.LeadingZeroCount(chunk) >> 3)
                : 4 - (BitOperations.TrailingZeroCount(chunk) >> 3);
#else
            byte b0 = lookup[offset];
            byte b1 = lookup[offset + 1];
            byte b2 = lookup[offset + 2];
            byte b3 = lookup[offset + 3];
            buffer[pos] = b0;
            buffer[pos + 1] = b1;
            buffer[pos + 2] = b2;
            buffer[pos + 3] = b3;

            // Copying and counting are kept branchless on purpose: the digit count is
            // data-dependent, so branches on it would mispredict often, which costs far
            // more than the few extra padding-byte stores they could save.
            int digits = 1;
            if (b1 != NUMBER_LOOKUP_PADDING) digits++;
            if (b2 != NUMBER_LOOKUP_PADDING) digits++;
            if (b3 != NUMBER_LOOKUP_PADDING) digits++;
#endif
            mainBufferCount = pos + digits;
        }

        /// <summary>
        /// Writes a 4-digit group including its leading zeros as one unconditional 4-byte
        /// block. The caller must have ensured the free space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFullNumberChunk(uint value)
        {
            var lookup = NumberLookupZeroPadded;
            var buffer = mainBuffer;
            int offset = (int)value * 4;
            int pos = mainBufferCount;
#if NET7_0_OR_GREATER
            uint chunk = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lookup), offset));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), pos), chunk);
#else
            buffer[pos] = lookup[offset];
            buffer[pos + 1] = lookup[offset + 1];
            buffer[pos + 2] = lookup[offset + 2];
            buffer[pos + 3] = lookup[offset + 3];
#endif
            mainBufferCount = pos + 4;
        }

        /// <summary>
        /// Writes an 8-digit group including its leading zeros. The caller must have ensured
        /// the free space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFullNumberChunk8(uint value)
        {
            uint high = value / 10000;
            WriteFullNumberChunk(high);
            WriteFullNumberChunk(value - high * 10000);
        }

        /// <summary>
        /// Writes a value in groups of 4 digits, each taken from the lookup tables. This
        /// replaces the digit-by-digit division loop by a few divisions and a handful of
        /// 4-byte block writes.
        /// </summary>
        private void WriteUnsignedInteger32(uint value)
        {
            if (value < NUMBER_LOOKUP_LIMIT)
            {
                WriteFromNumberLookup(value);
                return;
            }

            // Worst case: leading group writes 4 bytes (incl. padding) plus two full groups.
            if (mainBufferCount + 12 > mainBufferLimit) WriteBufferToStream();

            if (value < 100000000u)
            {
                uint high = value / 10000;
                WriteFromNumberLookup(high);
                WriteFullNumberChunk(value - high * 10000);
            }
            else
            {
                uint high = value / 100000000u;
                uint rest = value - high * 100000000u;
                WriteFromNumberLookup(high);
                WriteFullNumberChunk8(rest);
            }
        }

        /// <summary>
        /// Writes a value in groups of 4 digits, splitting off 8 digits at a time to stay in
        /// the cheaper 32 bit arithmetic for the groups.
        /// </summary>
        private void WriteUnsignedInteger64(ulong value)
        {
            if (value <= uint.MaxValue)
            {
                WriteUnsignedInteger32((uint)value);
                return;
            }

            // Worst case: leading group writes 4 bytes (incl. padding) plus four full groups.
            if (mainBufferCount + 20 > mainBufferLimit) WriteBufferToStream();

            ulong rest = value / 100000000UL;
            uint low8 = (uint)(value - rest * 100000000UL);
            if (rest < 100000000UL)
            {
                WriteUnsignedInteger32((uint)rest);
            }
            else
            {
                ulong high = rest / 100000000UL;
                uint mid8 = (uint)(rest - high * 100000000UL);
                WriteFromNumberLookup((uint)high); // At most 4 digits for ulong.MaxValue
                WriteFullNumberChunk8(mid8);
            }
            WriteFullNumberChunk8(low8);
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

        /// <summary>
        /// Minimum length of an unescaped character run for which the bulk UTF-8 encoder
        /// is used. Below that, its call overhead outweighs its vectorization benefit.
        /// </summary>
        const int MIN_BULK_ENCODE_LENGTH = 16;

#if NET7_0_OR_GREATER
        /// <summary>
        /// Returns the index of the first character that needs special handling
        /// (control char, quote, backslash or surrogate) or the length if there is none.
        /// Uses SIMD to scan multiple characters at once, which dominates the cost for longer strings.
        /// </summary>
        private static int FindNextSpecialChar(ReadOnlySpan<char> chars)
        {
            int i = 0;
            if (Vector128.IsHardwareAccelerated && chars.Length >= Vector128<ushort>.Count)
            {
                var space = Vector128.Create((ushort)' ');
                var quote = Vector128.Create((ushort)'"');
                var backslash = Vector128.Create((ushort)'\\');
                var surrogateStart = Vector128.Create((ushort)0xD800);
                var surrogateRange = Vector128.Create((ushort)0x0800);
                ref ushort start = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(chars));
                int limit = chars.Length - Vector128<ushort>.Count;
                for (; i <= limit; i += Vector128<ushort>.Count)
                {
                    var v = Vector128.LoadUnsafe(ref start, (nuint)i);
                    var special = Vector128.LessThan(v, space)
                                | Vector128.Equals(v, quote)
                                | Vector128.Equals(v, backslash)
                                | Vector128.LessThan(v - surrogateStart, surrogateRange);
                    uint mask = special.ExtractMostSignificantBits();
                    if (mask != 0) return i + BitOperations.TrailingZeroCount(mask);
                }
            }
            for (; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c < ' ' || c == '"' || c == '\\' || char.IsSurrogate(c)) return i;
            }
            return chars.Length;
        }

        /// <summary>
        /// Returns the index of the first surrogate character or the length if there is none.
        /// Uses SIMD to scan multiple characters at once.
        /// </summary>
        private static int FindNextSurrogate(ReadOnlySpan<char> chars)
        {
            int i = 0;
            if (Vector128.IsHardwareAccelerated && chars.Length >= Vector128<ushort>.Count)
            {
                var surrogateStart = Vector128.Create((ushort)0xD800);
                var surrogateRange = Vector128.Create((ushort)0x0800);
                ref ushort start = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(chars));
                int limit = chars.Length - Vector128<ushort>.Count;
                for (; i <= limit; i += Vector128<ushort>.Count)
                {
                    var v = Vector128.LoadUnsafe(ref start, (nuint)i);
                    uint mask = Vector128.LessThan(v - surrogateStart, surrogateRange).ExtractMostSignificantBits();
                    if (mask != 0) return i + BitOperations.TrailingZeroCount(mask);
                }
            }
            for (; i < chars.Length; i++)
            {
                if (char.IsSurrogate(chars[i])) return i;
            }
            return chars.Length;
        }
#endif

        private void WriteEscapedStringWithQuotes(string str, int startIndex = 0, int length = -1)
        {
            int charIndex = startIndex;
            int numChars = length >= 0 ? length : str.Length - startIndex;
            int endIndex = startIndex + numChars;
            const int MAX_CHAR_LENGTH = 6; // Escaped characters may have up to 6 Bytes
            EnsureFreeBufferSpace((endIndex - charIndex) * MAX_CHAR_LENGTH + 2); // +2 for the surrounding quotes            
            WriteToBufferWithoutCheck((byte)'"');
#if !NETSTANDARD2_0
            // Using a span lets the JIT drop the repeated bounds checks of the scan loop,
            // which dominates the cost for longer strings.
            ReadOnlySpan<char> chars = str.AsSpan();
#else
            string chars = str;
#endif
            while (charIndex < endIndex)
            {
                // Fast path: find the longest run of characters that need no escaping and
                // are no surrogates, then transcode the whole run in one vectorized call.
                // Writing byte by byte through the general path costs several branches, a
                // lookup and a call per character, which dominates the runtime for longer
                // strings.
                int runStart = charIndex;
#if NET7_0_OR_GREATER
                charIndex = runStart + FindNextSpecialChar(chars.Slice(runStart, endIndex - runStart));
#else
                while (charIndex < endIndex)
                {
                    char runChar = chars[charIndex];
                    if (runChar < ' ' || runChar == '"' || runChar == '\\' || char.IsSurrogate(runChar)) break;
                    charIndex++;
                }
#endif

                int runLength = charIndex - runStart;
                if (runLength > 0)
                {
                    if (runLength < MIN_BULK_ENCODE_LENGTH)
                    {
                        // For short runs the encoder call overhead outweighs its benefit.
                        for (int i = runStart; i < charIndex; i++)
                        {
                            char runChar = chars[i];
                            if (runChar <= 0x7F)
                            {
                                WriteToBufferWithoutCheck((byte)runChar);
                            }
                            else if (runChar <= 0x7FF)
                            {
                                WriteToBufferWithoutCheck((byte)(((runChar >> 6) & 0x1F) | 0xC0));
                                WriteToBufferWithoutCheck((byte)((runChar & 0x3F) | 0x80));
                            }
                            else
                            {
                                WriteToBufferWithoutCheck((byte)(((runChar >> 12) & 0x0F) | 0xE0));
                                WriteToBufferWithoutCheck((byte)(((runChar >> 6) & 0x3F) | 0x80));
                                WriteToBufferWithoutCheck((byte)((runChar & 0x3F) | 0x80));
                            }
                        }
                    }
                    else
                    {
#if !NETSTANDARD2_0
                        mainBufferCount += Encoding.UTF8.GetBytes(chars.Slice(runStart, runLength), new Span<byte>(mainBuffer, mainBufferCount, mainBuffer.Length - mainBufferCount));
#else
                        mainBufferCount += Encoding.UTF8.GetBytes(str, runStart, runLength, mainBuffer, mainBufferCount);
#endif
                    }

                    if (charIndex >= endIndex) break;
                }

                char c = chars[charIndex];

                // Handle escaped chars and control chars
                byte[] escapeBytes = GetEscapeBytes(c); 
                if (escapeBytes != null)
                {
                    WriteToBufferWithoutCheck(escapeBytes);
                    charIndex++;
                    continue;
                }

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

                    charIndex += 2; // The next character was part of the surrogate pair
                    continue;
                }

                throw new ArgumentException("Invalid surrogate pair in string.");
            }
            WriteToBufferWithoutCheck((byte)'"');
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRawJsonFragment(string json)
        {
            if (json != null) WriteString(json);
            else WriteNullValue();
        }

        private void WriteString(string str)
        {            
            const int MAX_CHAR_LENGTH = 6;
            EnsureFreeBufferSpace(str.Length * MAX_CHAR_LENGTH);
            int charIndex = 0;
            int endIndex = str.Length;
#if !NETSTANDARD2_0
            // Using a span lets the JIT drop the repeated bounds checks of the scan loop,
            // which dominates the cost for longer strings.
            ReadOnlySpan<char> chars = str.AsSpan();
#else
            string chars = str;
#endif
            while (charIndex < endIndex)
            {
                // Fast path: find the longest run of non-surrogate characters and transcode
                // it in one vectorized call instead of writing byte by byte.
                int runStart = charIndex;
#if NET7_0_OR_GREATER
                charIndex = runStart + FindNextSurrogate(chars.Slice(runStart, endIndex - runStart));
#else
                while (charIndex < endIndex && !char.IsSurrogate(chars[charIndex])) charIndex++;
#endif

                int runLength = charIndex - runStart;
                if (runLength > 0)
                {
                    if (runLength < MIN_BULK_ENCODE_LENGTH)
                    {
                        // For short runs the encoder call overhead outweighs its benefit.
                        for (int i = runStart; i < charIndex; i++)
                        {
                            char runChar = chars[i];
                            if (runChar <= 0x7F)
                            {
                                WriteToBufferWithoutCheck((byte)runChar);
                            }
                            else if (runChar <= 0x7FF)
                            {
                                WriteToBufferWithoutCheck((byte)(((runChar >> 6) & 0x1F) | 0xC0));
                                WriteToBufferWithoutCheck((byte)((runChar & 0x3F) | 0x80));
                            }
                            else
                            {
                                WriteToBufferWithoutCheck((byte)(((runChar >> 12) & 0x0F) | 0xE0));
                                WriteToBufferWithoutCheck((byte)(((runChar >> 6) & 0x3F) | 0x80));
                                WriteToBufferWithoutCheck((byte)((runChar & 0x3F) | 0x80));
                            }
                        }
                    }
                    else
                    {
#if !NETSTANDARD2_0
                        mainBufferCount += Encoding.UTF8.GetBytes(chars.Slice(runStart, runLength), new Span<byte>(mainBuffer, mainBufferCount, mainBuffer.Length - mainBufferCount));
#else
                        mainBufferCount += Encoding.UTF8.GetBytes(str, runStart, runLength, mainBuffer, mainBufferCount);
#endif
                    }

                    if (charIndex >= endIndex) break;
                }

                char c = chars[charIndex];

                // Handle surrogate pairs
                if (char.IsHighSurrogate(c) && charIndex + 1 < endIndex && char.IsLowSurrogate(str[charIndex + 1]))
                {
                    int highSurrogate = c;
                    int lowSurrogate = str[charIndex + 1];
                    int surrogateCodePoint = 0x10000 + ((highSurrogate - 0xD800) << 10) + (lowSurrogate - 0xDC00);

                    WriteToBufferWithoutCheck((byte)((surrogateCodePoint >> 18) | 0xF0));
                    WriteToBufferWithoutCheck((byte)(((surrogateCodePoint >> 12) & 0x3F) | 0x80));
                    WriteToBufferWithoutCheck((byte)(((surrogateCodePoint >> 6) & 0x3F) | 0x80));
                    WriteToBufferWithoutCheck((byte)((surrogateCodePoint & 0x3F) | 0x80));

                    charIndex += 2; // The next character was part of the surrogate pair
                    continue;
                }

                throw new ArgumentException("Invalid surrogate pair in string.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountDigits(uint value)
        {
            if (value < 10U) return 1;
            if (value < 100U) return 2;
            if (value < 1000U) return 3;
            if (value < 10000U) return 4;
            if (value < 100000U) return 5;
            if (value < 1000000U) return 6;
            if (value < 10000000U) return 7;
            if (value < 100000000U) return 8;
            if (value < 1000000000U) return 9;
            return 10;
        }

        private void WriteSignedInteger(long inputValue)
        {
            var value = inputValue;
            if (value < 0)
            {
                // If the value was long.MinValue negating it will cause an overflow and resulting again in long.MinValue,
                // so we handle it as a special number
                if (value == long.MinValue)
                {
                    WriteToBuffer(Int64MinValueBytes);
                    return;
                }
                value = -value;
                WriteToBuffer((byte)'-');
            }

            WriteUnsignedInteger64((ulong)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteByte(byte inputValue)
        {
            WriteFromNumberLookup(inputValue);
        }

        private void WriteSignedInteger(int inputValue)
        {
            var value = inputValue;
            if (value < 0)
            {
                // If the value was int.MinValue negating it will cause an overflow and resulting again in int.MinValue,
                // so we handle it as a special number
                if (value == int.MinValue)
                {
                    WriteToBuffer(Int32MinValueBytes);
                    return;
                }
                value = -value;
                WriteToBuffer((byte)'-');
            }

            WriteUnsignedInteger32((uint)value);
        }

        private void WriteUnsignedInteger(long inputValue)
        {
            WriteUnsignedInteger64((ulong)inputValue);
        }

        private void WriteUnsignedInteger(ulong value)
        {
            WriteUnsignedInteger64(value);
        }
        static readonly byte ZERO_FLOAT = (byte)'0';
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
#if NETSTANDARD2_0
            return IsSpecial((double)value);
#else
            if (Single.IsNaN(value)) return true;   // NaN
            int bits = BitConverter.SingleToInt32Bits(value);
            const int mask = 0x7F800000;            // Mask to isolate the exponent bits for float
            int maskedBits = bits & mask;
            if (maskedBits == 0) return true;       // Subnormal
            if (maskedBits == mask) return true;    // Infinity
            return false;
#endif
        }

        private void WriteFloat(float inputValue)
        {
            var value = inputValue;
            if (HandleSpecialCases(value)) return;

            EnsureFreeBufferSpace(100);

            bool isNegative = value < 0;
            if (isNegative) value = -value;

            // Fastest path: values without a fractional part below 2^24 are exactly
            // representable as int, so their decimal digits can be produced without any
            // scaling, remainder extraction or rounding correction. Their
            // trailing-zero-trimmed digits are also the shortest round-trippable
            // representation, because below 2^24 every integer maps to its own float.
            if (value < MAX_EXACT_INTEGRAL_FLOAT && (float)Math.Floor(value) == value)
            {
                if (isNegative) WriteToBufferWithoutCheck((byte)'-');
                WriteExactIntegral((int)value, FLOAT_POS_EXPONENT_LIMIT);
                return;
            }

            value = CalculateNumDigits(value, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent, out bool failed);
            if (failed)
            {
                WriteString(inputValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (isNegative) WriteToBufferWithoutCheck((byte)'-');

            float integralPart = (float)Math.Floor(value);
            float fractionalPart = value - integralPart;

            WriteIntegralPart(numIntegralDigits, integralPart);

            if (fractionalPart > 0)
            {
                WriteToBufferWithoutCheck((byte)'.');
                WriteFractionalPart(numFractionalDigits, fractionalPart);
            }

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
                    else if (Single.IsNegativeInfinity(value)) WriteToBuffer(NEG_INFINITY);
                    else if (Single.IsPositiveInfinity(value)) WriteToBuffer(POS_INFINITY);
                    else WriteString(value.ToString(CultureInfo.InvariantCulture)); // Then it must be subnormal
                    return true;
                }

                return false;
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
                        tempBuffer[index--] = (byte)('0' + (byte)digitLong);
                        if (digitLong == 0) numLeadingZeros++;
                        else numLeadingZeros = 0;
                    }
                    index += numLeadingZeros;
                    WriteToBufferWithoutCheck(tempBuffer, index + 1, numIntegralDigits - index);
                }
            }

            void WriteFractionalPart(int numFractionalDigits, float fractionalPart)
            {
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

        float CalculateNumDigits(float value, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent, out bool failed)
        {

#if NETSTANDARD2_0
            return (float)CalculateNumDigits((double)value, out exponent, out numIntegralDigits, out numFractionalDigits, out printExponent, out failed, 16);
#else
            const int MAX_SIGNIFICANT_DIGITS = 7;
            const int POS_EXPONENT_LIMIT = FLOAT_POS_EXPONENT_LIMIT;
            const int NEG_EXPONENT_LIMIT = -5;

            int bits = BitConverter.SingleToInt32Bits(value);
            int binaryExponent = ((bits >> 23) & 0xFF) - 127;
            exponent = (int)(binaryExponent * 0.34f);
            numIntegralDigits = Math.Max(0, exponent + 1);
            numFractionalDigits = Math.Max(0, MAX_SIGNIFICANT_DIGITS - numIntegralDigits);
            printExponent = false;

            failed = false;
            if (exponent < NEG_EXPONENT_LIMIT || exponent > POS_EXPONENT_LIMIT)
            {
                printExponent = true;
                value = (float)(value * Math.Pow(10, -exponent));

                if (value == 0 || IsSpecial(value))
                {
                    // In extreme cases, we can't calculate the digits properly.
                    failed = true;
                    return value;
                }

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
#endif
        }

        /// <summary>
        /// Writes the shortest round-trippable representation of the value using the
        /// framework formatter. Used as fallback when the fast digit extraction cannot
        /// produce an exact result. Avoids string allocation where possible.
        /// </summary>
        private void WriteRoundTripFallback(double value)
        {
            EnsureFreeBufferSpace(32);
#if NETSTANDARD2_0
            WriteString(value.ToString("R", CultureInfo.InvariantCulture));
#else
            if (Utf8Formatter.TryFormat(value, new Span<byte>(mainBuffer, mainBufferCount, mainBuffer.Length - mainBufferCount), out int written))
            {
                mainBufferCount += written;
            }
            else
            {
                WriteString(value.ToString("R", CultureInfo.InvariantCulture));
            }
#endif
        }

        private void WriteDouble(double inputValue)
        {
            var value = inputValue;
            if (HandleSpecialCases(value)) return;

            EnsureFreeBufferSpace(100);

            bool isNegative = value < 0;
            if (isNegative) value = -value;

            double absValue = value;
            bool bodyFailed = false;
            bool needsFallback = false;
            bool digitsAreExact = false;

            // Fastest path: values without a fractional part below 2^53 are exactly
            // representable as long, so their decimal digits can be produced without any
            // scaling, remainder extraction or verification. Their trailing-zero-trimmed
            // digits are also the shortest round-trippable representation, because below
            // 2^53 every integer maps to its own double.
            if (absValue < MAX_EXACT_INTEGRAL_DOUBLE && Math.Floor(absValue) == absValue)
            {
                if (isNegative) WriteToBufferWithoutCheck((byte)'-');
                WriteExactIntegral((long)absValue, POS_EXPONENT_LIMIT);
                return;
            }

            int numberStart = mainBufferCount;

            // Fast path: extract the digits directly. It is limited to 16 significant
            // digits and the extraction accumulates rounding errors in its last digits,
            // so a truncated value can look valid (its dropped tail may even be zeros).
            // Therefore the result is verified by parsing it back, unless the extraction
            // consumed the value without any remainder (digitsAreExact). Whenever the
            // fast path is not exact, the framework formatter produces the exact
            // shortest representation.
            WriteNumberBody(16);

            if (bodyFailed || needsFallback || (!digitsAreExact && !RoundTrips(numberStart, mainBufferCount, inputValue)))
            {
                mainBufferCount = numberStart;
                WriteRoundTripFallback(inputValue);
            }
            return;

            // Local Functions

            void WriteNumberBody(int maxSignificantDigits)
            {
                needsFallback = false;
                double bodyValue = CalculateNumDigits(absValue, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent, out bool failed, maxSignificantDigits);
                if (failed)
                {
                    bodyFailed = true;
                    return;
                }

                if (isNegative) WriteToBufferWithoutCheck((byte)'-');

                double integralPart = Math.Floor(bodyValue);
                double fractionalPart = bodyValue - integralPart;

                WriteIntegralPart(numIntegralDigits, integralPart);

                // Without a fractional part and without scaling, the written digits
                // represent the value exactly, so no verification is needed.
                digitsAreExact = !printExponent;

                if (fractionalPart > 0)
                {
                    WriteToBufferWithoutCheck((byte)'.');
                    WriteFractionalPart(numFractionalDigits, fractionalPart);
                    // The written digits are discarded by the caller, so skip the rest.
                    if (needsFallback) return;
                }

                if (printExponent)
                {
                    WriteToBuffer((byte)'E');
                    WriteSignedInteger(exponent);
                }
            }

            bool RoundTrips(int start, int end, double original)
            {
                int length = end - start;
#if NETSTANDARD2_0
                string text = Encoding.ASCII.GetString(mainBuffer, start, length);
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    return parsed == original;
                }
                return false;
#else
                var span = new ReadOnlySpan<byte>(mainBuffer, start, length);
                if (Utf8Parser.TryParse(span, out double parsed, out int consumed) && consumed == length)
                {
                    return parsed == original;
                }
                return false;
#endif
            }

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
                    else if (Double.IsNegativeInfinity(value)) WriteToBuffer(NEG_INFINITY);
                    else if (Double.IsPositiveInfinity(value)) WriteToBuffer(POS_INFINITY);
                    else WriteRoundTripFallback(value); // Then it must be subnormal
                    return true;
                }

                return false;
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
                        tempBuffer[index--] = (byte)('0' + (byte)digitLong);
                        if (digitLong == 0) numLeadingZeros++;
                        else numLeadingZeros = 0;
                    }
                    index += numLeadingZeros;
                    WriteToBufferWithoutCheck(tempBuffer, index + 1, numIntegralDigits - index);
                }
            }

            void WriteFractionalPart(int numFractionalDigits, double fractionalPart)
            {
                if (numFractionalDigits == 0)
                {
                    needsFallback = true;
                    if (fractionalPart >= 0.5f) WriteToBufferWithoutCheck((byte)'1');
                    else WriteToBufferWithoutCheck((byte)'0');
                    return;
                }

                int firstFractionalDigitIndex = mainBufferCount;
                bool tailIsAllZeros = false;
                for (int i = 0; i <= numFractionalDigits; i++)
                {
                    fractionalPart *= 10;
                    byte digit = (byte)fractionalPart;
                    fractionalPart -= digit;
                    WriteToBufferWithoutCheck((byte)('0' + digit));

                    // If the scaled remainder is below 1, no remaining digit can become
                    // non-zero anymore, so all of them would be '0' and would be trimmed
                    // away again. Skipping them saves the bulk of the work for typical
                    // short decimal values whose remainder keeps a tiny but non-zero
                    // residue (e.g. 3.33 -> 0.33000000000000007).
                    int remainingDigits = numFractionalDigits - i;
                    if (remainingDigits > 0 && fractionalPart * POW10[remainingDigits] < 1.0)
                    {
                        tailIsAllZeros = true;
                        break;
                    }
                }

                // Only a remainder of exactly zero proves that the digits represent the
                // value without any loss. Any other remainder is too inaccurate to decide
                // it, because the extraction amplifies its rounding error by 10 per digit.
                digitsAreExact &= fractionalPart == 0;

                int correctionIndex = mainBufferCount - 1;
                while (mainBuffer[correctionIndex] == '0' && correctionIndex > firstFractionalDigitIndex)
                {
                    correctionIndex--;
                }
                int oldMainBufferCount = mainBufferCount;
                mainBufferCount = correctionIndex + 1;
                if (oldMainBufferCount != mainBufferCount || tailIsAllZeros)
                {
                    // Trailing zeros were dropped (either written or skipped), so the
                    // digit budget was not fully consumed. The digits may still be a
                    // truncation, which the caller detects by parsing them back.
                    return;
                }

                // No trailing zeros were trimmed: the full digit budget was consumed,
                // so this value may need more precision to round-trip. Flag it, so the
                // caller discards the digits and uses the exact framework formatter.
                needsFallback = true;
            }

        }

        private static readonly double[] POW10 = new double[]
        {
            1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9,
            1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18
        };

        /// <summary>
        /// Highest decimal exponent that is still written in plain notation.
        /// </summary>
        private const int POS_EXPONENT_LIMIT = 13;

        /// <summary>
        /// Highest decimal exponent that is still written in plain notation for float.
        /// On NETSTANDARD2_0 the float path delegates to the double digit calculation,
        /// so it uses the same limit there.
        /// </summary>
#if NETSTANDARD2_0
        private const int FLOAT_POS_EXPONENT_LIMIT = POS_EXPONENT_LIMIT;
#else
        private const int FLOAT_POS_EXPONENT_LIMIT = 7;
#endif

        /// <summary>
        /// 2^53: below that, every integral double is exactly representable as long and
        /// no two integers share the same double.
        /// </summary>
        private const double MAX_EXACT_INTEGRAL_DOUBLE = 9007199254740992d;

        /// <summary>
        /// 2^24: below that, every integral float is exactly representable as int and
        /// no two integers share the same float.
        /// </summary>
        private const float MAX_EXACT_INTEGRAL_FLOAT = 16777216f;

        /// <summary>
        /// Writes an integral value that is exactly representable as long. The digits are
        /// taken from the integer directly, so they are exact and need neither rounding
        /// correction nor round-trip verification. The notation matches the one used by
        /// the generic digit extraction path.
        /// </summary>
        private void WriteExactIntegral(long integralValue, int posExponentLimit)
        {
            int digitCount = 0;
            do
            {
                integralValue = Math.DivRem(integralValue, 10, out long digit);
                tempBuffer[digitCount++] = (byte)('0' + (byte)digit);
            }
            while (integralValue > 0);

            int exponent = digitCount - 1;
            if (exponent <= posExponentLimit)
            {
                for (int i = digitCount - 1; i >= 0; i--) WriteToBufferWithoutCheck(tempBuffer[i]);
                return;
            }

            // Scientific notation: a single leading digit, then the remaining significant
            // digits without their trailing zeros.
            int lastSignificant = 0;
            while (lastSignificant < exponent && tempBuffer[lastSignificant] == '0') lastSignificant++;

            WriteToBufferWithoutCheck(tempBuffer[exponent]);
            if (lastSignificant < exponent)
            {
                WriteToBufferWithoutCheck((byte)'.');
                for (int i = exponent - 1; i >= lastSignificant; i--) WriteToBufferWithoutCheck(tempBuffer[i]);
            }
            WriteToBufferWithoutCheck((byte)'E');
            WriteSignedInteger(exponent);
        }

        double CalculateNumDigits(double value, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent, out bool failed, int MAX_SIGNIFICANT_DIGITS)
        {
            const int NEG_EXPONENT_LIMIT = -5;

            failed = false;
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

                if (value == 0 || IsSpecial(value))
                {
                    // In extreme cases, we can't calculate the digits properly.
                    failed = true;
                    return value;
                }

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




        private static readonly byte[] HexMap = System.Text.Encoding.UTF8.GetBytes("0123456789abcdef");

        /// <summary>
        /// Contains the two lowercase hex characters for every byte value, stored in 2-byte
        /// chunks, so a byte can be written as a single 2-byte block.
        /// </summary>
        private static readonly byte[] HexPairLookup = CreateHexPairLookup();

        private static byte[] CreateHexPairLookup()
        {
            var lookup = new byte[256 * 2];
            for (int i = 0; i < 256; i++)
            {
                lookup[i * 2] = HexMap[i >> 4];
                lookup[i * 2 + 1] = HexMap[i & 0xF];
            }
            return lookup;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteAsHexWithoutCheck(byte value)
        {
            var lookup = HexPairLookup;
            var buffer = mainBuffer;
            int offset = value * 2;
            int pos = mainBufferCount;
#if NET7_0_OR_GREATER
            // Both hex characters are moved as a single 2-byte load/store, which also removes
            // the bounds checks of the byte-wise accesses. The free space was ensured by the caller.
            ushort chunk = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lookup), offset));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), pos), chunk);
#else
            buffer[pos] = lookup[offset];
            buffer[pos + 1] = lookup[offset + 1];
#endif
            mainBufferCount = pos + 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteAsHex(byte value)
        {
            EnsureFreeBufferSpace(2);
            WriteByteAsHexWithoutCheck(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGuidValue(Guid guid)
        {
            EnsureFreeBufferSpace(38);  // GUID string length + 4 hyphens + 2 "
#if NETSTANDARD2_0
            // Fallback for .NET Standard 2.0 using ToByteArray and manual byte processing
            byte[] guidBytes = guid.ToByteArray();
#else
            // Default case for .NET Standard 2.1+ and other frameworks supporting Span<T>
            Span<byte> guidBytesSpan = new Span<byte>(tempBuffer, 0, 16);
            guid.TryWriteBytes(guidBytesSpan); // loacalBuffer is always bigger than 16 bytes
            byte[] guidBytes = tempBuffer;
#endif
            WriteToBufferWithoutCheck((byte)'"');
            WriteByteAsHexWithoutCheck(guidBytes[3]);
            WriteByteAsHexWithoutCheck(guidBytes[2]);
            WriteByteAsHexWithoutCheck(guidBytes[1]);
            WriteByteAsHexWithoutCheck(guidBytes[0]);
            WriteToBufferWithoutCheck((byte)'-');
            WriteByteAsHexWithoutCheck(guidBytes[5]);
            WriteByteAsHexWithoutCheck(guidBytes[4]);
            WriteToBufferWithoutCheck((byte)'-');
            WriteByteAsHexWithoutCheck(guidBytes[7]);
            WriteByteAsHexWithoutCheck(guidBytes[6]);
            WriteToBufferWithoutCheck((byte)'-');
            WriteByteAsHexWithoutCheck(guidBytes[8]);
            WriteByteAsHexWithoutCheck(guidBytes[9]);
            WriteToBufferWithoutCheck((byte)'-');
            WriteByteAsHexWithoutCheck(guidBytes[10]);
            WriteByteAsHexWithoutCheck(guidBytes[11]);
            WriteByteAsHexWithoutCheck(guidBytes[12]);
            WriteByteAsHexWithoutCheck(guidBytes[13]);
            WriteByteAsHexWithoutCheck(guidBytes[14]);
            WriteByteAsHexWithoutCheck(guidBytes[15]);
            WriteToBufferWithoutCheck((byte)'"');
        }

        private static readonly byte[] zeroDateTimeBytes = System.Text.Encoding.UTF8.GetBytes("\"0001-01-01T00:00:00\"");
        public void WriteDateTimeValue(DateTime dateTime)
        {
            if (dateTime == default)
            {
                WriteToBuffer(zeroDateTimeBytes);
                return;
            }

            int fractualSeconds = (int)(dateTime.Ticks % TimeSpan.TicksPerSecond);
            int bytesToReserve = zeroDateTimeBytes.Length;
            if (fractualSeconds > 0) bytesToReserve += 8; // .fffffff
            if (dateTime.Kind == DateTimeKind.Utc) bytesToReserve += 1; // Z
            else if (dateTime.Kind == DateTimeKind.Local) bytesToReserve += 6; // e.g. +01:00
            EnsureFreeBufferSpace(bytesToReserve);

            WriteToBufferWithoutCheck((byte)'"');
            // Write Year
            Write4Digits(dateTime.Year);
            WriteToBufferWithoutCheck((byte)'-');
            // Write Month
            Write2Digits(dateTime.Month);
            WriteToBufferWithoutCheck((byte)'-');
            // Write Day
            Write2Digits(dateTime.Day);
            WriteToBufferWithoutCheck((byte)'T');
            // Write Hour
            Write2Digits(dateTime.Hour);
            WriteToBufferWithoutCheck((byte)':');
            // Write Minute
            Write2Digits(dateTime.Minute);
            WriteToBufferWithoutCheck((byte)':');
            // Write Second
            Write2Digits(dateTime.Second);

            // Write Fractional second                
            if (fractualSeconds > 0)
            {
                WriteToBufferWithoutCheck((byte)'.');
                Write7Digits(fractualSeconds);
            }

            if (dateTime.Kind == DateTimeKind.Utc)
            {
                WriteToBufferWithoutCheck((byte)'Z');
            }
            else if (dateTime.Kind == DateTimeKind.Local)
            {
                TimeSpan offsetSpan = TimeZoneInfo.Local.GetUtcOffset(dateTime);
                bool isNegative = offsetSpan.Ticks < 0;
                WriteToBufferWithoutCheck((byte)(isNegative ? '-' : '+'));
                Write2Digits(Math.Abs(offsetSpan.Hours));
                WriteToBufferWithoutCheck((byte)':');
                Write2Digits(Math.Abs(offsetSpan.Minutes));
            }
            WriteToBufferWithoutCheck((byte)'"');
        }

        private static readonly byte[] zeroDateTimeOffsetBytes = System.Text.Encoding.UTF8.GetBytes("\"0001-01-01T00:00:00+00:00\"");
        public void WriteDateTimeOffsetValue(DateTimeOffset dateTimeOffset)
        {
            if (dateTimeOffset == default)
            {
                WriteToBuffer(zeroDateTimeOffsetBytes);
                return;
            }

            int fractualSeconds = (int)(dateTimeOffset.Ticks % TimeSpan.TicksPerSecond);
            int bytesToReserve = zeroDateTimeOffsetBytes.Length;
            if (fractualSeconds > 0) bytesToReserve += 8; // .fffffff
            EnsureFreeBufferSpace(bytesToReserve);

            WriteToBufferWithoutCheck((byte)'"');
            // Write Year
            Write4Digits(dateTimeOffset.Year);
            WriteToBufferWithoutCheck((byte)'-');
            // Write Month
            Write2Digits(dateTimeOffset.Month);
            WriteToBufferWithoutCheck((byte)'-');
            // Write Day
            Write2Digits(dateTimeOffset.Day);
            WriteToBufferWithoutCheck((byte)'T');
            // Write Hour
            Write2Digits(dateTimeOffset.Hour);
            WriteToBufferWithoutCheck((byte)':');
            // Write Minute
            Write2Digits(dateTimeOffset.Minute);
            WriteToBufferWithoutCheck((byte)':');
            // Write Second
            Write2Digits(dateTimeOffset.Second);

            // Write Fractional second
            if (fractualSeconds > 0)
            {
                WriteToBufferWithoutCheck((byte)'.');
                Write7Digits(fractualSeconds);
            }

            TimeSpan offsetSpan = dateTimeOffset.Offset;
            bool isNegative = offsetSpan.Ticks < 0;
            WriteToBufferWithoutCheck((byte)(isNegative ? '-' : '+'));
            Write2Digits(Math.Abs(offsetSpan.Hours));
            WriteToBufferWithoutCheck((byte)':');
            Write2Digits(Math.Abs(offsetSpan.Minutes));

            WriteToBufferWithoutCheck((byte)'"');
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUriValue(Uri value)
        {
            if (value != null) WriteEscapedStringWithQuotes(value.OriginalString);
            else WriteNullValue();
        }

        private static readonly byte[] zeroTimespanBytes = System.Text.Encoding.UTF8.GetBytes("\"00:00:00\"");
        public void WriteTimeSpanValue(TimeSpan value)
        {
            if (value == default)
            {
                WriteToBuffer(zeroTimespanBytes);
                return;
            }

            bool isNegative = value.Ticks < 0;
            if (isNegative) value = value.Negate(); // Make the TimeSpan positive for easier formatting

            int numDays = value.Days;
            int numFractualSeconds = (int)(value.Ticks % TimeSpan.TicksPerSecond);

            int bytesToReserve = zeroTimespanBytes.Length; // "hh:mm:ss"
            if (isNegative) bytesToReserve += 1; // '-' sign
            if (numDays > 0) bytesToReserve += 11; // e.g. ddd.(max 10 digits + dot)
            if (numFractualSeconds > 0) bytesToReserve += 8; // .fffffff

            EnsureFreeBufferSpace(bytesToReserve);
            WriteToBufferWithoutCheck((byte)'"');

            if (isNegative) WriteToBufferWithoutCheck((byte)'-');

            if (numDays > 0)
            {
                WriteSignedInteger(numDays);
                WriteToBufferWithoutCheck((byte)'.');
            }

            Write2Digits(value.Hours);
            WriteToBufferWithoutCheck((byte)':');
            Write2Digits(value.Minutes);
            WriteToBufferWithoutCheck((byte)':');
            Write2Digits(value.Seconds);

            if (numFractualSeconds > 0)
            {
                WriteToBufferWithoutCheck((byte)'.');
                Write7Digits(numFractualSeconds);
            }

            WriteToBufferWithoutCheck((byte)'"');
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write4Digits(int value)
        {
            WriteFullNumberChunk((uint)value);
        }

        /// <summary>
        /// Writes the last <paramref name="digits"/> characters of the zero-padded 4-digit
        /// representation of <paramref name="value"/>. The caller must have ensured the free space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNumberChunkTail(uint value, int digits)
        {
            var lookup = NumberLookupZeroPadded;
            var buffer = mainBuffer;
            int offset = (int)value * 4 + (4 - digits);
            int pos = mainBufferCount;
            for (int i = 0; i < digits; i++) buffer[pos + i] = lookup[offset + i];
            mainBufferCount = pos + digits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write2Digits(int value)
        {
            var lookup = NumberLookupZeroPadded;
            var buffer = mainBuffer;
            int offset = value * 4 + 2;
            int pos = mainBufferCount;
#if NET7_0_OR_GREATER
            // Both digits are moved as a single 2-byte load/store, which also removes the
            // bounds checks of the byte-wise accesses. The free space was ensured by the caller.
            ushort chunk = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lookup), offset));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), pos), chunk);
#else
            buffer[pos] = lookup[offset];
            buffer[pos + 1] = lookup[offset + 1];
#endif
            mainBufferCount = pos + 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Write7Digits(int value)
        {
            // Split into the leading 3 digits and a full 4-digit group, so only two lookups
            // are needed instead of seven divisions.
            uint high = (uint)value / 10000;
            uint low = (uint)value % 10000;
            WriteNumberChunkTail(high, 3);
            WriteFullNumberChunk(low);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteDigit(int value)
        {
            WriteToBufferWithoutCheck((byte)('0' + value));
        }
    }

}

