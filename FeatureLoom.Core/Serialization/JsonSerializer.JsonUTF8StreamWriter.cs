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

        /// <summary>Maximum number of bytes a single GUID value can occupy: 32 hex chars + 4 hyphens + 2 quotes.</summary>
        public const int GUID_MAX_BYTES = 38;

        // The number writers copy whole 4-digit chunks (including padding/leading zeros) into the
        // buffer, even if fewer bytes are counted. The reservation sizes therefore reflect the
        // physically written bytes, not the number of digits.
        /// <summary>Maximum number of bytes physically written for a byte value.</summary>
        public const int BYTE_MAX_BYTES = 4;
        /// <summary>Maximum number of bytes physically written for an sbyte value.</summary>
        public const int SBYTE_MAX_BYTES = 5;
        /// <summary>Maximum number of bytes physically written for a ushort value.</summary>
        public const int UINT16_MAX_BYTES = 8;
        /// <summary>Maximum number of bytes physically written for a short value.</summary>
        public const int INT16_MAX_BYTES = 9;
        /// <summary>Maximum number of bytes physically written for a uint value.</summary>
        public const int UINT32_MAX_BYTES = 12;
        /// <summary>Maximum number of bytes physically written for an int value.</summary>
        public const int INT32_MAX_BYTES = 13;
        /// <summary>Maximum number of bytes physically written for a ulong value.</summary>
        public const int UINT64_MAX_BYTES = 20;
        /// <summary>Maximum number of bytes physically written for a long value.</summary>
        public const int INT64_MAX_BYTES = 21;
        /// <summary>Maximum number of bytes written for a bool value ("false").</summary>
        public const int BOOL_MAX_BYTES = 5;

        /// <summary>
        /// Prepares a batch of fixed size elements and returns how many of them are guaranteed to
        /// fit into the remaining buffer space. Only flushes if not even one element fits, so the
        /// buffer is always filled completely before it is written to the stream. Always >= 1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int BeginFixedSizeBatch(int bytesPerElement)
        {
            int free = mainBufferLimit - mainBufferCount;
            if (free <= bytesPerElement)
            {
                EnsureFreeBufferSpace(bytesPerElement);
                free = mainBufferLimit - mainBufferCount;
            }
            return free / bytesPerElement;
        }

        /// <summary>
        /// True if the writer emits indentation. Batched writing is only valid without indentation,
        /// because indentation adds a variable number of bytes per separator.
        /// </summary>
        public bool IsIndenting => indent;

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
            // mainBufferLimit is always mainBuffer.Length - BUFFER_LIMIT_MARGIN, so as long as the
            // limit is not reached, the buffer length check cannot trigger either. That makes the
            // common case a single comparison and keeps the rare handling out of the inlined body.
            if (mainBufferCount + freeBytes < mainBufferLimit) return;
            EnsureFreeBufferSpaceSlow(freeBytes);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureFreeBufferSpaceSlow(int freeBytes)
        {
            WriteBufferToStream();
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
            WriteDecimal(value);
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

        // Unchecked variants of the fixed size value writers. The caller must have reserved the
        // corresponding *_MAX_BYTES, e.g. via BeginFixedSizeBatch in an array loop.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteLongValueWithoutCheck(long value) => WriteSignedIntegerWithoutCheck(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUlongValueWithoutCheck(ulong value) => WriteUnsignedInteger64WithoutCheck(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteIntValueWithoutCheck(int value) => WriteSignedIntegerWithoutCheck(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUintValueWithoutCheck(uint value) => WriteUnsignedInteger32WithoutCheck(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteShortValueWithoutCheck(short value) => WriteSignedIntegerWithoutCheck((int)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteUshortValueWithoutCheck(ushort value) => WriteUnsignedInteger32WithoutCheck(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByteValueWithoutCheck(byte value) => WriteFromNumberLookupWithoutCheck(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteSbyteValueWithoutCheck(sbyte value) => WriteSignedIntegerWithoutCheck((int)value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBoolValueWithoutCheck(bool value) => WriteToBufferWithoutCheck(value ? BOOLVALUE_TRUE : BOOLVALUE_FALSE);

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
        static readonly byte[] EMPTY_STRING = new byte[] { (byte)'\"', (byte)'\"' };
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
        /// Writes the two base64 characters of a 12-bit group as one 2-byte block into the
        /// given buffer and returns the new position. Works on locals only, so the caller can
        /// keep the buffer position in a register across a whole encoding loop. The caller
        /// must have ensured the free space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteBase64Pair(byte[] buffer, int pos, byte[] lookup, int group12Bit)
        {
            int offset = group12Bit * 2;
#if NET7_0_OR_GREATER
            ushort chunk = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lookup), offset));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), pos), chunk);
#else
            buffer[pos] = lookup[offset];
            buffer[pos + 1] = lookup[offset + 1];
#endif
            return pos + 2;
        }

        private void WriteBase64(ByteSegment value)
        {
            int numInputBytes = value.Count;
            int fullBlocks = numInputBytes / 3;
            int bytesToReserve = 2 + (fullBlocks + 1) * 4;
            EnsureFreeBufferSpace(bytesToReserve);

            // An empty payload is a common case (e.g. an empty byte array member) and must not
            // pay for the setup of the encoding paths below.
            if (numInputBytes == 0)
            {
                WriteToBufferWithoutCheck((byte)'"');
                WriteToBufferWithoutCheck((byte)'"');
                return;
            }

#if !NETSTANDARD2_0
            // The runtime encoder is vectorized and clearly beats the scalar lookup loop for
            // larger payloads. For short inputs the call overhead dominates, so the lookup loop
            // is kept for those. The threshold was determined by measurement.
            if (numInputBytes >= 64 && TryWriteBase64Vectorized(value)) return;

            var bytes = value.AsSpan();
#else
            var bytes = value;
#endif

            WriteToBufferWithoutCheck((byte)'"');
            var buffer = mainBuffer;
            var lookup = Base64PairLookup;
            int pos = mainBufferCount;
            int inputIndex = 0;
            for (int i = 0; i < fullBlocks; i++)
            {
                int bufferValue = (bytes[inputIndex++] << 16) & 0xFFFFFF;
                bufferValue |= (bytes[inputIndex++] << 8);
                bufferValue |= bytes[inputIndex++];
                pos = WriteBase64Pair(buffer, pos, lookup, (bufferValue >> 12) & 0xFFF);
                pos = WriteBase64Pair(buffer, pos, lookup, bufferValue & 0xFFF);
            }

            int remainingBytes = numInputBytes - inputIndex;
            if (remainingBytes == 1)
            {
                int bufferValue = (bytes[inputIndex++] << 16) & 0xFFFFFF;
                pos = WriteBase64Pair(buffer, pos, lookup, (bufferValue >> 12) & 0xFFF);
                buffer[pos++] = (byte)'=';
                buffer[pos++] = (byte)'=';
            }
            else if(remainingBytes == 2)
            {
                int bufferValue = (bytes[inputIndex++] << 16) & 0xFFFFFF;
                bufferValue |= (bytes[inputIndex++] << 8);
                pos = WriteBase64Pair(buffer, pos, lookup, (bufferValue >> 12) & 0xFFF);
                buffer[pos++] = Base64Chars[(bufferValue >> 6) & 0x3F];
                buffer[pos++] = (byte)'=';
            }

            buffer[pos++] = (byte)'"';
            mainBufferCount = pos;
        }

#if !NETSTANDARD2_0
        /// <summary>
        /// Writes the quoted base64 representation using the vectorized runtime encoder.
        /// Returns false without changing the buffer if the remaining space is not sufficient,
        /// so the caller can fall back to the scalar loop.
        /// </summary>
        private bool TryWriteBase64Vectorized(ByteSegment value)
        {
            int pos = mainBufferCount;
            var destination = new Span<byte>(mainBuffer, pos + 1, mainBuffer.Length - pos - 1);
            if (Base64.EncodeToUtf8(value.AsSpan(), destination, out _, out int written) != System.Buffers.OperationStatus.Done) return false;

            mainBuffer[pos] = (byte)'"';
            pos += 1 + written;
            mainBuffer[pos] = (byte)'"';
            mainBufferCount = pos + 1;
            return true;
        }
#endif

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

        /// <summary>
        /// Writes the separating comma without any buffer check and without indentation.
        /// Only valid inside a reserved batch, which is only used when indentation is off.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteCommaWithoutCheck() => WriteToBufferWithoutCheck(COMMA);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUnsignedInteger32(uint value)
        {
            // Single digits are the most common case in typical payloads. Writing them directly
            // avoids the 4-byte chunk copy and the digit-count derivation of the lookup path.
            // The sign was already stripped by the callers, so this cannot hit negative values.
            if (value < 10) WriteToBuffer((byte)('0' + value));
            else WriteUnsignedInteger32Slow(value);
        }

        private void WriteUnsignedInteger32Slow(uint value)
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
        /// Same as <see cref="WriteUnsignedInteger32(uint)"/>, but the caller must have reserved
        /// <see cref="UINT32_MAX_BYTES"/> bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUnsignedInteger32WithoutCheck(uint value)
        {
            if (value < 10) WriteToBufferWithoutCheck((byte)('0' + value));
            else WriteUnsignedInteger32WithoutCheckSlow(value);
        }

        private void WriteUnsignedInteger32WithoutCheckSlow(uint value)
        {
            if (value < NUMBER_LOOKUP_LIMIT)
            {
                WriteFromNumberLookupWithoutCheck(value);
                return;
            }

            if (value < 100000000u)
            {
                uint high = value / 10000;
                WriteFromNumberLookupWithoutCheck(high);
                WriteFullNumberChunk(value - high * 10000);
            }
            else
            {
                uint high = value / 100000000u;
                uint rest = value - high * 100000000u;
                WriteFromNumberLookupWithoutCheck(high);
                WriteFullNumberChunk8(rest);
            }
        }

        /// <summary>
        /// Writes a value in groups of 4 digits, splitting off 8 digits at a time to stay in
        /// the cheaper 32 bit arithmetic for the groups.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUnsignedInteger64(ulong value)
        {
            if (value < 10) WriteToBuffer((byte)('0' + (uint)value));
            else WriteUnsignedInteger64Slow(value);
        }

        private void WriteUnsignedInteger64Slow(ulong value)
        {
            if (value <= uint.MaxValue)
            {
                WriteUnsignedInteger32Slow((uint)value);
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

        /// <summary>
        /// Same as <see cref="WriteUnsignedInteger64(ulong)"/>, but the caller must have reserved
        /// <see cref="UINT64_MAX_BYTES"/> bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUnsignedInteger64WithoutCheck(ulong value)
        {
            if (value < 10) WriteToBufferWithoutCheck((byte)('0' + (uint)value));
            else WriteUnsignedInteger64WithoutCheckSlow(value);
        }

        private void WriteUnsignedInteger64WithoutCheckSlow(ulong value)
        {
            if (value <= uint.MaxValue)
            {
                WriteUnsignedInteger32WithoutCheckSlow((uint)value);
                return;
            }

            ulong rest = value / 100000000UL;
            uint low8 = (uint)(value - rest * 100000000UL);
            if (rest < 100000000UL)
            {
                WriteUnsignedInteger32WithoutCheck((uint)rest);
            }
            else
            {
                ulong high = rest / 100000000UL;
                uint mid8 = (uint)(rest - high * 100000000UL);
                WriteFromNumberLookupWithoutCheck((uint)high); // At most 4 digits for ulong.MaxValue
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

#if NET7_0_OR_GREATER
        /// <summary>
        /// Exclusive upper bound of the characters covered by the escape lookups.
        /// '\\' (92) is the highest character that needs escaping.
        /// </summary>
        private const int ESCAPE_LOOKUP_LIMIT = 93;

        /// <summary>
        /// Width of one chunk in <see cref="EscapeFlatLookup"/>. The longest escape is the
        /// 6-byte \uXXXX form, but the chunks are padded to 8 so that one escape can be
        /// written as a single unaligned 64-bit store instead of a variable length copy.
        /// </summary>
        private const int ESCAPE_CHUNK_SIZE = 8;

        /// <summary>
        /// Escape sequences of all characters below <see cref="ESCAPE_LOOKUP_LIMIT"/> in flat
        /// 8-byte chunks. A single array avoids the per-character array objects and the null
        /// check of the jagged lookup, which dominate the cost for escape dense strings.
        /// </summary>
        private static readonly byte[] EscapeFlatLookup = InitEscapeFlatLookup();

        /// <summary>
        /// Length of the escape sequence of each character, or 0 if it needs no escaping.
        /// </summary>
        private static readonly byte[] EscapeLengthLookup = InitEscapeLengthLookup();

        private static byte[] InitEscapeFlatLookup()
        {
            byte[] lookup = new byte[ESCAPE_LOOKUP_LIMIT * ESCAPE_CHUNK_SIZE];
            for (int i = 0; i < ESCAPE_LOOKUP_LIMIT; i++)
            {
                byte[] escapeBytes = GetEscapeBytes((char)i);
                if (escapeBytes == null) continue;
                Array.Copy(escapeBytes, 0, lookup, i * ESCAPE_CHUNK_SIZE, escapeBytes.Length);
            }
            return lookup;
        }

        private static byte[] InitEscapeLengthLookup()
        {
            byte[] lookup = new byte[ESCAPE_LOOKUP_LIMIT];
            for (int i = 0; i < ESCAPE_LOOKUP_LIMIT; i++)
            {
                lookup[i] = (byte)(GetEscapeBytes((char)i)?.Length ?? 0);
            }
            return lookup;
        }

        /// <summary>
        /// Returns the length of the escape sequence of the given character, or 0 if it needs
        /// no escaping. Only a bounds compare and a byte load, so it replaces the array deref
        /// and null check of <see cref="GetEscapeBytes"/> in the hot escaping loop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetEscapeLength(char c)
        {
            return c < ESCAPE_LOOKUP_LIMIT ? EscapeLengthLookup[c] : 0;
        }
#endif

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

        /// <summary>
        /// Maximum length of a known ASCII run that is narrowed inline instead of being
        /// passed to the bulk UTF-8 encoder. The encoder's ASCII narrowing is unrolled and
        /// better tuned, so it wins for longer runs despite scanning the run a second time.
        /// Below that its call overhead dominates, so narrowing inline is faster.
        /// </summary>
        const int MAX_ASCII_NARROW_LENGTH = 128;

#if NET7_0_OR_GREATER
        /// <summary>
        /// Returns the index of the first character that needs special handling
        /// (control char, quote, backslash or surrogate) or the length if there is none.
        /// Uses SIMD to scan multiple characters at once, which dominates the cost for longer strings.
        /// Also reports whether the skipped run consists of ASCII characters only, so that the
        /// caller can transcode it by simply narrowing the chars instead of calling the encoder.
        /// </summary>
        private static int FindNextSpecialChar(ReadOnlySpan<char> chars, out bool allAscii)
        {
            allAscii = true;
            int i = 0;
            ref ushort start = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(chars));
            if (Vector256.IsHardwareAccelerated && chars.Length >= Vector256<ushort>.Count)
            {
                var space = Vector256.Create((ushort)' ');
                var quote = Vector256.Create((ushort)'"');
                var backslash = Vector256.Create((ushort)'\\');
                var surrogateStart = Vector256.Create((ushort)0xD800);
                var surrogateRange = Vector256.Create((ushort)0x0800);
                var maxAscii = Vector256.Create((ushort)0x7F);
                int limit = chars.Length - Vector256<ushort>.Count;
                for (; i <= limit; i += Vector256<ushort>.Count)
                {
                    var v = Vector256.LoadUnsafe(ref start, (nuint)i);
                    var escapes = Vector256.LessThan(v, space)
                                | Vector256.Equals(v, quote)
                                | Vector256.Equals(v, backslash);
                    var nonAscii = Vector256.GreaterThan(v, maxAscii);
                    // Plain ASCII without anything to escape is by far the most common case,
                    // so the loop is kept down to a single mask extraction for it. Only when
                    // something was found the individual masks are extracted to tell apart
                    // what it was.
                    if ((escapes | nonAscii).ExtractMostSignificantBits() == 0) continue;

                    // A surrogate is never ASCII, so it only has to be tested here.
                    uint mask = (escapes | Vector256.LessThan(v - surrogateStart, surrogateRange)).ExtractMostSignificantBits();
                    uint nonAsciiMask = nonAscii.ExtractMostSignificantBits();
                    if (mask != 0)
                    {
                        int index = BitOperations.TrailingZeroCount(mask);
                        // Only the characters before the stop index belong to the run.
                        if ((nonAsciiMask & ((1u << index) - 1)) != 0) allAscii = false;
                        return i + index;
                    }
                    // Nothing to escape was found, so the hit came from a non ASCII char.
                    allAscii = false;
                }
            }
            if (Vector128.IsHardwareAccelerated && chars.Length - i >= Vector128<ushort>.Count)
            {
                var space = Vector128.Create((ushort)' ');
                var quote = Vector128.Create((ushort)'"');
                var backslash = Vector128.Create((ushort)'\\');
                var surrogateStart = Vector128.Create((ushort)0xD800);
                var surrogateRange = Vector128.Create((ushort)0x0800);
                var maxAscii = Vector128.Create((ushort)0x7F);
                int limit = chars.Length - Vector128<ushort>.Count;
                for (; i <= limit; i += Vector128<ushort>.Count)
                {
                    var v = Vector128.LoadUnsafe(ref start, (nuint)i);
                    var escapes = Vector128.LessThan(v, space)
                                | Vector128.Equals(v, quote)
                                | Vector128.Equals(v, backslash);
                    var nonAscii = Vector128.GreaterThan(v, maxAscii);
                    if ((escapes | nonAscii).ExtractMostSignificantBits() == 0) continue;

                    uint mask = (escapes | Vector128.LessThan(v - surrogateStart, surrogateRange)).ExtractMostSignificantBits();
                    uint nonAsciiMask = nonAscii.ExtractMostSignificantBits();
                    if (mask != 0)
                    {
                        int index = BitOperations.TrailingZeroCount(mask);
                        if ((nonAsciiMask & ((1u << index) - 1)) != 0) allAscii = false;
                        return i + index;
                    }
                    allAscii = false;
                }
            }
            for (; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c < ' ' || c == '"' || c == '\\' || char.IsSurrogate(c)) return i;
                if (c > 0x7F) allAscii = false;
            }
            return chars.Length;
        }

        /// <summary>
        /// Transcodes a run of known ASCII characters by narrowing them directly into the
        /// buffer. Saves the call into the UTF8 encoder, which would scan the run a second
        /// time to discover what is already known here.
        /// The caller must have reserved space for the whole run.
        /// </summary>
        private void WriteAsciiRun(ReadOnlySpan<char> chars)
        {
            // The destination offset is kept in a local instead of the mainBufferCount field,
            // because updating the field inside the loop forces a store through "this" on
            // every iteration and prevents the JIT from keeping the offset in a register.
            int pos = mainBufferCount;
            int i = 0;
            int length = chars.Length;
            // Taking refs once lets all accesses below use unchecked offsets, so the bounds
            // checks of span and array indexing disappear from the loops.
            ref ushort src = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(chars));
            ref byte dst = ref MemoryMarshal.GetArrayDataReference(mainBuffer);
            if (length >= Vector128<ushort>.Count)
            {
                if (Vector256.IsHardwareAccelerated && length >= Vector256<ushort>.Count)
                {
                    int wideLimit = length - Vector256<ushort>.Count;
                    for (; i <= wideLimit; i += Vector256<ushort>.Count)
                    {
                        var v = Vector256.LoadUnsafe(ref src, (nuint)i);
                        // Narrow duplicates the input, so only the lower half is the result.
                        Vector256.Narrow(v, v).GetLower().StoreUnsafe(ref dst, (nuint)pos);
                        pos += Vector256<ushort>.Count;
                    }
                }
                int limit = length - Vector128<ushort>.Count;
                for (; i <= limit; i += Vector128<ushort>.Count)
                {
                    var v = Vector128.LoadUnsafe(ref src, (nuint)i);
                    Vector128.Narrow(v, v).GetLower().StoreUnsafe(ref dst, (nuint)pos);
                    pos += Vector128<ushort>.Count;
                }
                if (i < length)
                {
                    // The remaining 1..7 chars are written by one more vector store that
                    // overlaps the previous one instead of a scalar loop. Rewriting a few
                    // bytes with the same values is cheaper than the leftover iterations,
                    // and it stays in bounds because the run is at least a vector long.
                    int tailStart = length - Vector128<ushort>.Count;
                    var v = Vector128.LoadUnsafe(ref src, (nuint)tailStart);
                    Vector128.Narrow(v, v).GetLower().StoreUnsafe(ref dst, (nuint)(pos - (i - tailStart)));
                    pos += length - i;
                }
            }
            else
            {
                // Runs shorter than a vector are rare and at most 7 chars long.
                for (; i < length; i++)
                {
                    Unsafe.Add(ref dst, pos++) = (byte)Unsafe.Add(ref src, i);
                }
            }
            mainBufferCount = pos;
        }

        /// <summary>
        /// Transcodes a run that is already known to be ASCII only.
        /// Short runs are narrowed inline, because there the call overhead of the bulk
        /// routines dominates. Longer runs are handed to the runtime's ASCII narrowing,
        /// which is more aggressively unrolled than the loop above. The general UTF-8
        /// encoder is avoided, because it would re-derive that the run is ASCII and carry
        /// the transcoding and validation machinery that is not needed here.
        /// The caller must have reserved one byte per character.
        /// </summary>
        private void WriteKnownAsciiRun(ReadOnlySpan<char> chars)
        {
            if (chars.Length < MAX_ASCII_NARROW_LENGTH)
            {
                WriteAsciiRun(chars);
                return;
            }
            var destination = new Span<byte>(mainBuffer, mainBufferCount, mainBuffer.Length - mainBufferCount);
#if NET8_0_OR_GREATER
            Ascii.FromUtf16(chars, destination, out int written);
            mainBufferCount += written;
#else
            mainBufferCount += Encoding.UTF8.GetBytes(chars, destination);
#endif
        }

        /// <summary>
        /// Writes the quoted string in a single fused pass that scans and narrows each vector
        /// in one go, so plain ASCII content is touched only once instead of being scanned for
        /// the reservation and then transcoded again.
        /// Returns false without changing the buffer as soon as a character is found that needs
        /// escaping or transcoding, so the caller can fall back to the scanning path.
        /// Every character it writes becomes exactly one byte, so the caller only has to ensure
        /// that one byte per character plus the two quotes fit. That keeps the buffer from being
        /// flushed in between, so the written bytes stay contiguous for callers that read them back.
        /// </summary>
        private bool TryWriteFusedAsciiWithQuotes(ReadOnlySpan<char> chars, int startIndex, int endIndex)
        {
            int pos = mainBufferCount;
            ref ushort src = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(chars));
            ref byte dst = ref MemoryMarshal.GetArrayDataReference(mainBuffer);
            Unsafe.Add(ref dst, pos++) = (byte)'"';

            int i = startIndex;
            // Anything below the space, the quote, the backslash and everything above
            // plain ASCII needs special handling. Checking against the highest ASCII char
            // covers surrogates and multi byte chars in the same compare.
            if (Vector256.IsHardwareAccelerated && endIndex - i >= Vector256<ushort>.Count)
            {
                var space = Vector256.Create((ushort)' ');
                var quote = Vector256.Create((ushort)'"');
                var backslash = Vector256.Create((ushort)'\\');
                var maxAscii = Vector256.Create((ushort)0x7F);
                int limit = endIndex - Vector256<ushort>.Count;
                for (; i <= limit; i += Vector256<ushort>.Count)
                {
                    var v = Vector256.LoadUnsafe(ref src, (nuint)i);
                    var special = Vector256.LessThan(v, space)
                                | Vector256.Equals(v, quote)
                                | Vector256.Equals(v, backslash)
                                | Vector256.GreaterThan(v, maxAscii);
                    if (special.ExtractMostSignificantBits() != 0) return false;
                    // The vector is known to be plain ASCII, so it can be narrowed and stored
                    // right away, without loading it a second time in a separate write pass.
                    Vector256.Narrow(v, v).GetLower().StoreUnsafe(ref dst, (nuint)pos);
                    pos += Vector256<ushort>.Count;
                }
            }
            if (Vector128.IsHardwareAccelerated)
            {
                var space = Vector128.Create((ushort)' ');
                var quote = Vector128.Create((ushort)'"');
                var backslash = Vector128.Create((ushort)'\\');
                var maxAscii = Vector128.Create((ushort)0x7F);
                int limit = endIndex - Vector128<ushort>.Count;
                for (; i <= limit; i += Vector128<ushort>.Count)
                {
                    var v = Vector128.LoadUnsafe(ref src, (nuint)i);
                    var special = Vector128.LessThan(v, space)
                                | Vector128.Equals(v, quote)
                                | Vector128.Equals(v, backslash)
                                | Vector128.GreaterThan(v, maxAscii);
                    if (special.ExtractMostSignificantBits() != 0) return false;
                    Vector128.Narrow(v, v).GetLower().StoreUnsafe(ref dst, (nuint)pos);
                    pos += Vector128<ushort>.Count;
                }
            }
            for (; i < endIndex; i++)
            {
                char c = (char)Unsafe.Add(ref src, i);
                if (c < ' ' || c == '"' || c == '\\' || c > 0x7F) return false;
                Unsafe.Add(ref dst, pos++) = (byte)c;
            }

            Unsafe.Add(ref dst, pos++) = (byte)'"';
            mainBufferCount = pos;
            return true;
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
#if !NETSTANDARD2_0
            // Using a span lets the JIT drop the repeated bounds checks of the scan loop,
            // which dominates the cost for longer strings.
            ReadOnlySpan<char> chars = str.AsSpan();
#else
            string chars = str;
#endif
            // Empty strings are common enough to be worth skipping the whole machinery below.
            if (numChars == 0)
            {
                WriteToBuffer(EMPTY_STRING);
                return;
            }

            // The reservation has to cover the whole string, because the buffer must not be
            // flushed while writing: callers like WriteStringValueAsStringWithCopy read the
            // written bytes back and rely on them being contiguous.
            // Reserving the worst case of 6 bytes per char would demand six times the space
            // an ASCII string actually needs, which forces premature flushes and buffer
            // resizes for longer strings. So the first special char is located up front and
            // the reservation is sized by what was actually found.
            const int MAX_CHAR_LENGTH = 6; // A control char escape (\uXXXX) is the longest
#if NET7_0_OR_GREATER
            // Plain ASCII without anything to escape is the most common case by far, so it is
            // attempted first in a fused pass that scans and narrows in one go, touching the
            // data only once instead of scanning it for the reservation and transcoding it
            // afterwards.
            // The fused path narrows every character to a single byte and bails out before it
            // writes anything that would need more, so it only needs one byte per character
            // instead of the worst case. That keeps it reachable for long strings, where the
            // saved second pass matters most. A failed attempt writes through a local position
            // and only commits it on success, so it leaves the buffer untouched.
            if (mainBufferCount + numChars + 2 < mainBufferLimit)
            {
                if (TryWriteFusedAsciiWithQuotes(chars, startIndex, endIndex)) return;
            }

            int firstSpecial = FindNextSpecialChar(chars.Slice(startIndex, numChars), out bool firstRunIsAscii);
            bool plainAscii = firstRunIsAscii && firstSpecial == numChars;
            // An all ASCII string without any special char needs exactly one byte per char.
            EnsureFreeBufferSpace(plainAscii ? numChars + 2 : numChars * MAX_CHAR_LENGTH + 2);
            if (plainAscii)
            {
                // Nothing to escape and nothing to transcode: the scan above already learned
                // everything, so the whole string can be written in one go without entering
                // the run loop.
                WriteToBufferWithoutCheck((byte)'"');
                WriteKnownAsciiRun(chars.Slice(startIndex, numChars));
                WriteToBufferWithoutCheck((byte)'"');
                return;
            }
#else
            EnsureFreeBufferSpace(numChars * MAX_CHAR_LENGTH + 2); // +2 for the surrounding quotes
#endif
            WriteToBufferWithoutCheck((byte)'"');
#if NET7_0_OR_GREATER
            int nextSpecial = firstSpecial;
            bool nextRunIsAscii = firstRunIsAscii;
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
                // The scan result is carried in locals rather than recomputed here, so that
                // the initial scan done for the reservation can be reused without testing for
                // the first iteration inside this loop, which runs once per escaped char.
                charIndex = runStart + nextSpecial;
                bool runIsAscii = nextRunIsAscii;
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
#if NET7_0_OR_GREATER
                    if (runIsAscii)
                    {
                        WriteKnownAsciiRun(chars.Slice(runStart, runLength));
                    }
                    else
#endif
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

                // Handle escaped chars and control chars. Consecutive ones are consumed by
                // this tight loop instead of returning to the scan above, because that scan
                // sets up its vector constants on every call and would immediately report
                // the very next character again. For escape dense strings that setup, not
                // the escaping itself, dominates the runtime.
#if NET7_0_OR_GREATER
                int escapeLength = GetEscapeLength(c);
                if (escapeLength != 0)
                {
                    // Each escape is written as one unaligned 8-byte store from the flat
                    // lookup and the position is then advanced by the real escape length.
                    // The padding bytes beyond it are overwritten by the next write, which
                    // is cheaper than a variable length copy per character. The reservation
                    // of 6 bytes per char plus the closing quote covers the overhang.
                    ref byte lookup = ref MemoryMarshal.GetArrayDataReference(EscapeFlatLookup);
                    ref byte dst = ref MemoryMarshal.GetArrayDataReference(mainBuffer);
                    int pos = mainBufferCount;
                    do
                    {
                        ulong chunk = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref lookup, c * ESCAPE_CHUNK_SIZE));
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, pos), chunk);
                        pos += escapeLength;
                        charIndex++;
                        if (charIndex >= endIndex) break;
                        c = chars[charIndex];
                        escapeLength = GetEscapeLength(c);
                    } while (escapeLength != 0);
                    mainBufferCount = pos;
                    if (charIndex < endIndex) nextSpecial = FindNextSpecialChar(chars.Slice(charIndex, endIndex - charIndex), out nextRunIsAscii);
                    continue;
                }
#else
                byte[] escapeBytes = GetEscapeBytes(c);
                if (escapeBytes != null)
                {
                    do
                    {
                        WriteToBufferWithoutCheck(escapeBytes);
                        charIndex++;
                        if (charIndex >= endIndex) break;
                        escapeBytes = GetEscapeBytes(chars[charIndex]);
                    } while (escapeBytes != null);
                    continue;
                }
#endif

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
#if NET7_0_OR_GREATER
                    if (charIndex < endIndex) nextSpecial = FindNextSpecialChar(chars.Slice(charIndex, endIndex - charIndex), out nextRunIsAscii);
#endif
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

        /// <summary>
        /// Only the single digit case is inlined into the caller. Everything else lives in a
        /// separate non-inlined method, so the call sites stay small and the hot path is a
        /// single compare plus a byte store. Negative values wrap to a huge unsigned value by
        /// the cast, so they fall through to the slow path.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteSignedInteger(long inputValue)
        {
            if ((ulong)inputValue < 10) WriteToBuffer((byte)('0' + (uint)inputValue));
            else WriteSignedIntegerSlow(inputValue);
        }

        private void WriteSignedIntegerSlow(long inputValue)
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

        /// <summary>
        /// Same as <see cref="WriteSignedInteger(long)"/>, but the caller must have reserved
        /// <see cref="INT64_MAX_BYTES"/> bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteSignedIntegerWithoutCheck(long inputValue)
        {
            if ((ulong)inputValue < 10) WriteToBufferWithoutCheck((byte)('0' + (uint)inputValue));
            else WriteSignedIntegerWithoutCheckSlow(inputValue);
        }

        private void WriteSignedIntegerWithoutCheckSlow(long inputValue)
        {
            var value = inputValue;
            if (value < 0)
            {
                if (value == long.MinValue)
                {
                    WriteToBufferWithoutCheck(Int64MinValueBytes);
                    return;
                }
                value = -value;
                WriteToBufferWithoutCheck((byte)'-');
            }

            WriteUnsignedInteger64WithoutCheck((ulong)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteByte(byte inputValue)
        {
            WriteFromNumberLookup(inputValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteSignedInteger(int inputValue)
        {
            if ((uint)inputValue < 10) WriteToBuffer((byte)('0' + (uint)inputValue));
            else WriteSignedIntegerSlow(inputValue);
        }

        private void WriteSignedIntegerSlow(int inputValue)
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

        /// <summary>
        /// Same as <see cref="WriteSignedInteger(int)"/>, but the caller must have reserved
        /// <see cref="INT32_MAX_BYTES"/> bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteSignedIntegerWithoutCheck(int inputValue)
        {
            if ((uint)inputValue < 10) WriteToBufferWithoutCheck((byte)('0' + (uint)inputValue));
            else WriteSignedIntegerWithoutCheckSlow(inputValue);
        }

        private void WriteSignedIntegerWithoutCheckSlow(int inputValue)
        {
            var value = inputValue;
            if (value < 0)
            {
                if (value == int.MinValue)
                {
                    WriteToBufferWithoutCheck(Int32MinValueBytes);
                    return;
                }
                value = -value;
                WriteToBufferWithoutCheck((byte)'-');
            }

            WriteUnsignedInteger32WithoutCheck((uint)value);
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
                WriteExponent(exponent);
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
        /// framework formatter. This is the regular path for fractional doubles and the
        /// fallback whenever a faster path cannot produce an exact result.
        /// Avoids string allocation where possible.
        /// The caller must have reserved at least 32 free bytes, so that this hot path
        /// does not repeat the buffer check for every single value.
        /// </summary>
        private void WriteShortestRoundTrippable(double value)
        {
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

            // Fractional values are formatted by the framework, which produces the
            // shortest round-trippable representation in a single pass. The former
            // hand-rolled digit extraction was limited to 16 significant digits and had
            // to verify its result by parsing it back; on realistic payload data about
            // 60% of the fractional values failed that check and were formatted twice.
            // Only NETSTANDARD2_0 keeps the extraction, because Utf8Formatter is not
            // available there and the string based fallback would allocate.
#if !NETSTANDARD2_0
            if (isNegative) WriteToBufferWithoutCheck((byte)'-');
            WriteShortestRoundTrippable(absValue);
#else
            bool bodyFailed = false;
            bool needsFallback = false;
            bool digitsAreExact = false;
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
                WriteShortestRoundTrippable(inputValue);
            }
#endif
            return;

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
                    else if (Double.IsNegativeInfinity(value)) WriteToBuffer(NEG_INFINITY);
                    else if (Double.IsPositiveInfinity(value)) WriteToBuffer(POS_INFINITY);
                    else
                    {
                        // Reached before the reservation below, so it has to reserve itself.
                        EnsureFreeBufferSpace(32);
                        WriteShortestRoundTrippable(value); // Then it must be subnormal
                    }
                    return true;
                }

                return false;
            }

#if NETSTANDARD2_0

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
                    WriteExponent(exponent);
                }
            }

            bool RoundTrips(int start, int end, double original)
            {
                int length = end - start;
                string text = Encoding.ASCII.GetString(mainBuffer, start, length);
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    return parsed == original;
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
#endif

        }

        private static readonly double[] POW10 = new double[]
        {
            1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9,
            1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18,
            // Values below 1 extend the fractional digit budget by up to
            // -NEG_EXPONENT_LIMIT - 1 leading zeros, so the tail-is-zero probe can index
            // beyond 1e18.
            1e19, 1e20, 1e21, 1e22
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

            // Index of the lowest significant digit: everything below it is a trailing zero.
            int lastSignificant = 0;
            while (lastSignificant < exponent && tempBuffer[lastSignificant] == '0') lastSignificant++;

            // Plain notation is used while the exponent stays within the limit, and also when
            // the value has no trailing zeros at all. In the latter case scientific notation
            // would not be shorter, and the runtime (and other JSON writers) keep such values
            // in plain notation as well, e.g. 1234567890123456 instead of 1.234567890123456E15.
            if (exponent <= posExponentLimit || lastSignificant == 0)
            {
                for (int i = digitCount - 1; i >= 0; i--) WriteToBufferWithoutCheck(tempBuffer[i]);
                return;
            }

            // Scientific notation: a single leading digit, then the remaining significant
            // digits without their trailing zeros.

            WriteToBufferWithoutCheck(tempBuffer[exponent]);
            if (lastSignificant < exponent)
            {
                WriteToBufferWithoutCheck((byte)'.');
                for (int i = exponent - 1; i >= lastSignificant; i--) WriteToBufferWithoutCheck(tempBuffer[i]);
            }
            WriteToBufferWithoutCheck((byte)'E');
            WriteExponent(exponent);
        }

        /// <summary>
        /// Writes the exponent part of a scientific notation number using the same convention
        /// as .NET's round-trip formatting: an explicit sign followed by at least two digits
        /// (e.g. "+15", "-05", "+308").
        /// </summary>
        private void WriteExponent(int exponent)
        {
            uint magnitude;
            if (exponent < 0)
            {
                WriteToBuffer((byte)'-');
                magnitude = (uint)(-(long)exponent);
            }
            else
            {
                WriteToBuffer((byte)'+');
                magnitude = (uint)exponent;
            }

            if (magnitude < 10) WriteToBuffer((byte)'0');
            WriteUnsignedInteger32(magnitude);
        }

        // Longest decimal output: sign + "0." + 28 fractional digits, or 29 integral digits.
        private const int DECIMAL_MAX_BYTES = 32;

        /// <summary>
        /// Writes a decimal exactly, in plain notation and preserving the value's scale
        /// (e.g. 1.250 stays "1.250"), matching decimal.ToString(InvariantCulture) and the
        /// output of System.Text.Json. Decimal is a base-10 type, so its digits can be
        /// emitted directly from the 96-bit mantissa without any rounding, round-trip
        /// verification, or scientific notation.
        /// </summary>
        private void WriteDecimal(decimal value)
        {
            EnsureFreeBufferSpace(DECIMAL_MAX_BYTES);

            // Bits 16-23 of the flags hold the scale (0-28), bit 31 holds the sign.
            int[] bits = decimal.GetBits(value);
            int flags = bits[3];
            int scale = (flags >> 16) & 0xFF;

            // Render the 96-bit mantissa as decimal digits, least significant first.
            uint lo = (uint)bits[0];
            uint mid = (uint)bits[1];
            uint hi = (uint)bits[2];

            if ((lo | mid | hi) == 0)
            {
                // Plain zero is by far the most common decimal value, so it skips the
                // division loop entirely. A zero mantissa never carries a sign, so even
                // decimal.Negate(0m) is written as "0", matching decimal.ToString().
                if (scale == 0)
                {
                    WriteToBufferWithoutCheck((byte)'0');
                    return;
                }

                // A scaled zero still keeps its trailing zeros, e.g. 0.00m stays "0.00".
                WriteToBufferWithoutCheck((byte)'0');
                WriteToBufferWithoutCheck((byte)'.');
                for (int i = scale; i > 0; i--) WriteToBufferWithoutCheck((byte)'0');
                return;
            }

            bool isNegative = flags < 0;

            int digitCount = 0;
            do
            {
                // Long division of the 96-bit mantissa by 10, from the most significant word down.
                ulong rest = hi;
                hi = (uint)(rest / 10);
                rest = ((rest - hi * 10) << 32) | mid;
                mid = (uint)(rest / 10);
                rest = ((rest - mid * 10) << 32) | lo;
                lo = (uint)(rest / 10);
                tempBuffer[digitCount++] = (byte)('0' + (rest - lo * 10));
            }
            while ((lo | mid | hi) != 0);

            if (isNegative) WriteToBufferWithoutCheck((byte)'-');

            if (scale == 0)
            {
                for (int i = digitCount - 1; i >= 0; i--) WriteToBufferWithoutCheck(tempBuffer[i]);
                return;
            }

            if (digitCount > scale)
            {
                for (int i = digitCount - 1; i >= scale; i--) WriteToBufferWithoutCheck(tempBuffer[i]);
            }
            else
            {
                // Value is below 1, so the integral part is a single zero and the fractional
                // part needs leading zeros to reach the scale.
                WriteToBufferWithoutCheck((byte)'0');
            }

            WriteToBufferWithoutCheck((byte)'.');
            for (int i = scale - digitCount; i > 0; i--) WriteToBufferWithoutCheck((byte)'0');
            for (int i = Math.Min(scale, digitCount) - 1; i >= 0; i--) WriteToBufferWithoutCheck(tempBuffer[i]);
        }

        double CalculateNumDigits(double value, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent, out bool failed, int MAX_SIGNIFICANT_DIGITS)
        {
            const int NEG_EXPONENT_LIMIT = -5;

            failed = false;
            long bits = BitConverter.DoubleToInt64Bits(value);
            int binaryExponent = (int)((bits >> 52) & 0x7FF) - 1023;
            exponent = (int)(binaryExponent * 0.34f);
            numIntegralDigits = Math.Max(0, exponent + 1);
            // For values below 1 the leading zeros right after the decimal point occupy
            // fractional digit slots without carrying any significant digit. They must be
            // added on top of the budget, otherwise such values receive fewer significant
            // digits than intended and the fast path falls back on values it could have
            // represented exactly (e.g. 0.0001589 got only 12 of the 16 digits).
            int numLeadingFractionalZeros = numIntegralDigits == 0 ? -exponent - 1 : 0;
            numFractionalDigits = Math.Max(0, MAX_SIGNIFICANT_DIGITS - numIntegralDigits + numLeadingFractionalZeros);
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
        {            EnsureFreeBufferSpace(GUID_MAX_BYTES);  // GUID string length + 4 hyphens + 2 "
            WriteGuidValueWithoutCheck(guid);
        }

        /// <summary>
        /// Writes a GUID without ensuring buffer space. The caller must have reserved
        /// <see cref="GUID_MAX_BYTES"/> bytes, e.g. via a batched reservation in an array loop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteGuidValueWithoutCheck(Guid guid)
        {
#if NET7_0_OR_GREATER
            // The Guid is read directly by reference, so the detour through tempBuffer and the
            // bounds checked array loads disappear. The destination reference and the lookup
            // reference are computed once and all 38 positions are constant offsets, which lets
            // the JIT emit plain stores without any position arithmetic.
            if (BitConverter.IsLittleEndian)
            {
                ref byte src = ref Unsafe.As<Guid, byte>(ref guid);
                ref byte lut = ref MemoryMarshal.GetArrayDataReference(HexPairLookup);
                ref byte dst = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(mainBuffer), mainBufferCount);

                dst = (byte)'"';
                WriteHexPairAt(ref dst, 1, ref lut, Unsafe.Add(ref src, 3));
                WriteHexPairAt(ref dst, 3, ref lut, Unsafe.Add(ref src, 2));
                WriteHexPairAt(ref dst, 5, ref lut, Unsafe.Add(ref src, 1));
                WriteHexPairAt(ref dst, 7, ref lut, src);
                Unsafe.Add(ref dst, 9) = (byte)'-';
                WriteHexPairAt(ref dst, 10, ref lut, Unsafe.Add(ref src, 5));
                WriteHexPairAt(ref dst, 12, ref lut, Unsafe.Add(ref src, 4));
                Unsafe.Add(ref dst, 14) = (byte)'-';
                WriteHexPairAt(ref dst, 15, ref lut, Unsafe.Add(ref src, 7));
                WriteHexPairAt(ref dst, 17, ref lut, Unsafe.Add(ref src, 6));
                Unsafe.Add(ref dst, 19) = (byte)'-';
                WriteHexPairAt(ref dst, 20, ref lut, Unsafe.Add(ref src, 8));
                WriteHexPairAt(ref dst, 22, ref lut, Unsafe.Add(ref src, 9));
                Unsafe.Add(ref dst, 24) = (byte)'-';
                WriteHexPairAt(ref dst, 25, ref lut, Unsafe.Add(ref src, 10));
                WriteHexPairAt(ref dst, 27, ref lut, Unsafe.Add(ref src, 11));
                WriteHexPairAt(ref dst, 29, ref lut, Unsafe.Add(ref src, 12));
                WriteHexPairAt(ref dst, 31, ref lut, Unsafe.Add(ref src, 13));
                WriteHexPairAt(ref dst, 33, ref lut, Unsafe.Add(ref src, 14));
                WriteHexPairAt(ref dst, 35, ref lut, Unsafe.Add(ref src, 15));
                Unsafe.Add(ref dst, 37) = (byte)'"';

                mainBufferCount += 38;
                return;
            }
#endif
            WriteGuidValuePortable(guid);
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// Writes the two hex characters of a byte at a constant offset from the destination.
        /// Both characters are moved as a single 2-byte load/store. Free space was ensured by the caller.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteHexPairAt(ref byte dst, int offset, ref byte lookup, byte value)
        {
            ushort chunk = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref lookup, value * 2));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, offset), chunk);
        }
#endif

        private void WriteGuidValuePortable(Guid guid)
        {
#if NETSTANDARD2_0
            // Fallback for .NET Standard 2.0 using ToByteArray and manual byte processing
            byte[] guidBytes = guid.ToByteArray();
#else
            // Default case for .NET Standard 2.1+ and other frameworks supporting Span<T>
            Span<byte> guidBytesSpan = new Span<byte>(tempBuffer, 0, 16);
            guid.TryWriteBytes(guidBytesSpan); // loacalBuffer is always bigger than 16 bytes
            byte[] guidBytes = tempBuffer;
#endif
            // The whole GUID is written through locals and the write position is only stored
            // back once. Going through the fields for each of the 21 chunks was the dominant
            // cost here, because the JIT cannot keep the position in a register across the
            // field accesses.
            var lookup = HexPairLookup;
            var buffer = mainBuffer;
            int pos = mainBufferCount;

            buffer[pos++] = (byte)'"';
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[3]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[2]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[1]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[0]);
            buffer[pos++] = (byte)'-';
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[5]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[4]);
            buffer[pos++] = (byte)'-';
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[7]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[6]);
            buffer[pos++] = (byte)'-';
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[8]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[9]);
            buffer[pos++] = (byte)'-';
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[10]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[11]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[12]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[13]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[14]);
            pos = WriteHexPair(buffer, pos, lookup, guidBytes[15]);
            buffer[pos++] = (byte)'"';

            mainBufferCount = pos;
        }

        /// <summary>
        /// Writes the two hex characters of a byte at the given position and returns the new
        /// position. Works on locals only, so no field access happens per chunk.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteHexPair(byte[] buffer, int pos, byte[] lookup, byte value)
        {
            int offset = value * 2;
#if NET7_0_OR_GREATER
            // Both hex characters are moved as a single 2-byte load/store, which also removes
            // the bounds checks of the byte-wise accesses. The free space was ensured by the caller.
            ushort chunk = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(lookup), offset));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), pos), chunk);
#else
            buffer[pos] = lookup[offset];
            buffer[pos + 1] = lookup[offset + 1];
#endif
            return pos + 2;
        }

        private static readonly byte[] zeroDateTimeBytes = System.Text.Encoding.UTF8.GetBytes("\"0001-01-01T00:00:00\"");

        // Caches the already encoded "yyyy-MM-dd" bytes of the last written day number.
        // Serialized date collections usually share the same or only a few distinct days,
        // so the calendar decomposition and the digit lookups are done once per day instead
        // of once per value. The mapping is constant, so the cache never needs invalidation.
        private int cachedDateDayNumber = -1;
        private ulong cachedDateChunkLow;   // "yyyy-MM-" 
        private ushort cachedDateChunkHigh; // "dd"

        /// <summary>
        /// Splits the days since 0001-01-01 into year, month and day in a single pass.
        /// DateTime.Year, .Month and .Day each redo the whole calendar decomposition from the
        /// ticks, so asking for all three costs three times the divisions this needs.
        /// Uses the shifted-era approach, which moves the leap day to the end of the year and
        /// thereby removes the case distinctions of a calendar based calculation.
        /// </summary>
        private static void GetDateParts(int days, out int year, out int month, out int day)
        {
            // Shift the epoch from 0001-01-01 to 0000-03-01, so that the leap day becomes the
            // last day of the year and the month lengths follow a regular pattern.
            days += 306;
            int era = days / 146097;        // 146097 days per 400 years
            int dayOfEra = days - era * 146097;
            // Subtracting the leap days of the century and 4-year cycles yields the year of the era.
            int yearOfEra = (dayOfEra - dayOfEra / 1460 + dayOfEra / 36524 - dayOfEra / 146096) / 365;
            int dayOfYear = dayOfEra - (365 * yearOfEra + yearOfEra / 4 - yearOfEra / 100);
            // The regular month pattern of the shifted year allows a direct calculation.
            int shiftedMonth = (5 * dayOfYear + 2) / 153;
            day = dayOfYear - (153 * shiftedMonth + 2) / 5 + 1;
            // Shift March..February back to January..December.
            month = shiftedMonth < 10 ? shiftedMonth + 3 : shiftedMonth - 9;
            year = yearOfEra + era * 400 + (shiftedMonth < 10 ? 0 : 1);
        }

        public void WriteDateTimeValue(DateTime dateTime)
        {
            if (dateTime == default)
            {
                WriteToBuffer(zeroDateTimeBytes);
                return;
            }

            long ticks = dateTime.Ticks;
            int fractualSeconds = (int)(ticks % TimeSpan.TicksPerSecond);
            var kind = dateTime.Kind;
            int bytesToReserve = zeroDateTimeBytes.Length;
            if (fractualSeconds > 0) bytesToReserve += 8; // .fffffff
            if (kind == DateTimeKind.Utc) bytesToReserve += 1; // Z
            else if (kind == DateTimeKind.Local) bytesToReserve += 6; // e.g. +01:00
            EnsureFreeBufferSpace(bytesToReserve);

            WriteDateAndTime(ticks);

            // Write Fractional second                
            if (fractualSeconds > 0) WriteFractionalSeconds(fractualSeconds);

            if (kind == DateTimeKind.Utc)
            {
                WriteToBufferWithoutCheck((byte)'Z');
            }
            else if (kind == DateTimeKind.Local)
            {
                TimeSpan offsetSpan = TimeZoneInfo.Local.GetUtcOffset(dateTime);
                WriteUtcOffset(offsetSpan);
            }
            WriteToBufferWithoutCheck((byte)'"');
        }

        /// <summary>
        /// Writes the opening quote and the "yyyy-MM-ddTHH:mm:ss" part of the given ticks.
        /// The whole block has a fixed size, so it is composed in one go and the write position
        /// is only stored back once instead of once per chunk. The free space was ensured by the caller.
        /// </summary>
        private void WriteDateAndTime(long ticks)
        {
            long totalDays = ticks / TimeSpan.TicksPerDay;
            int timeOfDay = (int)((ticks - totalDays * TimeSpan.TicksPerDay) / TimeSpan.TicksPerSecond);
            // A single division yields both the hour and the remaining seconds of the hour.
            int hour = timeOfDay / 3600;
            int secondOfHour = timeOfDay - hour * 3600;
            int minute = secondOfHour / 60;
            int second = secondOfHour - minute * 60;

            int pos = mainBufferCount;
#if NET7_0_OR_GREATER
            int dayNumber = (int)totalDays;
            if (dayNumber != cachedDateDayNumber) UpdateDateCache(dayNumber);

            ref byte dst = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(mainBuffer), pos);
            dst = (byte)'"';
            // The whole "yyyy-MM-dd" comes from the cache as one 8-byte and one 2-byte store.
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 1), cachedDateChunkLow);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 9), cachedDateChunkHigh);
            Unsafe.Add(ref dst, 11) = (byte)'T';
            mainBufferCount = pos + 12;
            WriteTimeOfDay(hour, minute, second);
#else
            GetDateParts((int)totalDays, out int year, out int month, out int day);
            mainBuffer[pos] = (byte)'"';
            mainBufferCount = pos + 1;
            Write4Digits(year);
            WriteToBufferWithoutCheck((byte)'-');
            Write2Digits(month);
            WriteToBufferWithoutCheck((byte)'-');
            Write2Digits(day);
            WriteToBufferWithoutCheck((byte)'T');
            Write2Digits(hour);
            WriteToBufferWithoutCheck((byte)':');
            Write2Digits(minute);
            WriteToBufferWithoutCheck((byte)':');
            Write2Digits(second);
#endif
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// Decomposes the given day number and encodes it as the "yyyy-MM-dd" byte chunks of the cache.
        /// Only called when a day differs from the previously written one.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UpdateDateCache(int dayNumber)
        {
            GetDateParts(dayNumber, out int year, out int month, out int day);
            Span<byte> tmp = stackalloc byte[16];
            ref byte tmpRef = ref MemoryMarshal.GetReference(tmp);
            ref byte lut = ref MemoryMarshal.GetArrayDataReference(NumberLookupZeroPadded);
            // The year is a full 4-digit group, month and day are the last 2 digits of one.
            Write4DigitsAt(ref tmpRef, 0, ref lut, year);
            Unsafe.Add(ref tmpRef, 4) = (byte)'-';
            Write2DigitsAt(ref tmpRef, 5, ref lut, month);
            Unsafe.Add(ref tmpRef, 7) = (byte)'-';
            Write2DigitsAt(ref tmpRef, 8, ref lut, day);
            cachedDateChunkLow = Unsafe.ReadUnaligned<ulong>(ref tmpRef);
            cachedDateChunkHigh = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref tmpRef, 8));
            cachedDateDayNumber = dayNumber;
        }
#endif

        /// <summary>
        /// Writes the "+HH:mm" / "-HH:mm" suffix of a local time or a DateTimeOffset.
        /// The free space was ensured by the caller.
        /// </summary>
        private void WriteUtcOffset(TimeSpan offsetSpan)
        {
            // A single decomposition of the total minutes replaces the separate .Hours/.Minutes
            // properties, which each redo the division chain from the ticks.
            int offsetMinutes = (int)(offsetSpan.Ticks / TimeSpan.TicksPerMinute);
            bool isNegative = offsetMinutes < 0;
            if (isNegative) offsetMinutes = -offsetMinutes;
            int offsetHour = offsetMinutes / 60;
            int offsetMinute = offsetMinutes - offsetHour * 60;

            int pos = mainBufferCount;
#if NET7_0_OR_GREATER
            if (BitConverter.IsLittleEndian)
            {
                ref byte lut = ref MemoryMarshal.GetArrayDataReference(NumberLookupZeroPadded);
                uint h = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref lut, offsetHour * 4 + 2));
                uint m = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref lut, offsetMinute * 4 + 2));
                // "+HH:" and "mm" as one 4-byte and one 2-byte store.
                uint head = (uint)(isNegative ? '-' : '+') | (h << 8) | ((uint)':' << 24);
                ref byte dst = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(mainBuffer), pos);
                Unsafe.WriteUnaligned(ref dst, head);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 4), (ushort)m);
                mainBufferCount = pos + 6;
                return;
            }
#endif
            WriteToBufferWithoutCheck((byte)(isNegative ? '-' : '+'));
            Write2Digits(offsetHour);
            WriteToBufferWithoutCheck((byte)':');
            Write2Digits(offsetMinute);
        }

#if NET7_0_OR_GREATER
        /// <summary>
        /// Writes the last two digits of the zero-padded 4-digit representation of the value at a
        /// constant offset from the destination, as a single 2-byte load/store.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write2DigitsAt(ref byte dst, int offset, ref byte lookup, int value)
        {
            ushort chunk = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref lookup, value * 4 + 2));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, offset), chunk);
        }

        /// <summary>
        /// Writes the full zero-padded 4-digit representation of the value at a constant offset
        /// from the destination, as a single 4-byte load/store.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write4DigitsAt(ref byte dst, int offset, ref byte lookup, int value)
        {
            uint chunk = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref lookup, value * 4));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, offset), chunk);
        }
#endif

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

            WriteDateAndTime(dateTimeOffset.Ticks);

            // Write Fractional second
            if (fractualSeconds > 0) WriteFractionalSeconds(fractualSeconds);

            WriteUtcOffset(dateTimeOffset.Offset);

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
            long ticks = isNegative ? -value.Ticks : value.Ticks; // Make it positive for easier formatting

            // A single decomposition yields all parts. TimeSpan.Days, .Hours, .Minutes and .Seconds
            // each redo the division chain from the ticks, so asking for all four costs a multiple.
            int numDays = (int)(ticks / TimeSpan.TicksPerDay);
            long restOfDay = ticks - numDays * TimeSpan.TicksPerDay;
            int secondOfDay = (int)(restOfDay / TimeSpan.TicksPerSecond);
            int numFractualSeconds = (int)(restOfDay - secondOfDay * TimeSpan.TicksPerSecond);
            int hour = secondOfDay / 3600;
            int secondOfHour = secondOfDay - hour * 3600;
            int minute = secondOfHour / 60;
            int second = secondOfHour - minute * 60;

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

            WriteTimeOfDay(hour, minute, second);

            if (numFractualSeconds > 0) WriteFractionalSeconds(numFractualSeconds);

            WriteToBufferWithoutCheck((byte)'"');
        }

        /// <summary>
        /// Writes the "hh:mm:ss" part. The block has a fixed size of exactly 8 bytes, so it is
        /// composed in one go and stored as a single 8-byte write instead of three lookups plus
        /// two separator writes. The free space was ensured by the caller.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteTimeOfDay(int hour, int minute, int second)
        {
#if NET7_0_OR_GREATER
            if (BitConverter.IsLittleEndian)
            {
                ref byte lut = ref MemoryMarshal.GetArrayDataReference(NumberLookupZeroPadded);
                ulong h = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref lut, hour * 4 + 2));
                ulong m = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref lut, minute * 4 + 2));
                ulong s = Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref lut, second * 4 + 2));
                ulong block = h | ((ulong)':' << 16) | (m << 24) | ((ulong)':' << 40) | (s << 48);
                int pos = mainBufferCount;
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(mainBuffer), pos), block);
                mainBufferCount = pos + 8;
                return;
            }
#endif
            Write2Digits(hour);
            WriteToBufferWithoutCheck((byte)':');
            Write2Digits(minute);
            WriteToBufferWithoutCheck((byte)':');
            Write2Digits(second);
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

        /// <summary>
        /// Writes the ".fffffff" fractional second part. The whole block has a fixed size, so it is
        /// composed in one go and stored as a single 8-byte write instead of a byte-wise loop.
        /// The free space was ensured by the caller.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFractionalSeconds(int fractionalSeconds)
        {
#if NET7_0_OR_GREATER
            if (BitConverter.IsLittleEndian)
            {
                uint high = (uint)fractionalSeconds / 10000;
                uint low = (uint)fractionalSeconds % 10000;
                ref byte lut = ref MemoryMarshal.GetArrayDataReference(NumberLookupZeroPadded);
                // The 4-digit chunk of the 3-digit high part starts with a padding zero,
                // which is exactly the slot the decimal point has to occupy.
                uint highChunk = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref lut, (int)high * 4));
                uint lowChunk = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref lut, (int)low * 4));
                ulong block = ((ulong)lowChunk << 32) | (highChunk & 0xFFFFFF00u) | (byte)'.';
                int pos = mainBufferCount;
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(mainBuffer), pos), block);
                mainBufferCount = pos + 8;
                return;
            }
#endif
            WriteToBufferWithoutCheck((byte)'.');
            Write7Digits(fractionalSeconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteDigit(int value)
        {
            WriteToBufferWithoutCheck((byte)('0' + value));
        }
    }

}

