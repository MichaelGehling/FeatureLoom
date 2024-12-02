using System.Text;
using FeatureLoom.Extensions;
using System.IO;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using FeatureLoom.Helpers;
using System.Reflection;
using System.Linq;
using System.Globalization;

namespace FeatureLoom.Serialization
{
    public sealed partial class FeatureJsonSerializer
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
            ArraySegment<byte> GetCollectionIndexName(int index);
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
            ArraySegment<byte> WriteBoolValueAsStringWithCopy(bool value);
            ArraySegment<byte> WriteByteValueAsStringWithCopy(byte value);
            ArraySegment<byte> WriteCharValueAsStringWithCopy(char value);
            ArraySegment<byte> WriteDoubleValueAsStringWithCopy(double value);
            ArraySegment<byte> WriteFloatValueAsStringWithCopy(float value);
            ArraySegment<byte> WriteIntValueAsStringWithCopy(int value);
            ArraySegment<byte> WriteLongValueAsStringWithCopy(long value);
            ArraySegment<byte> WriteSbyteValueAsStringWithCopy(sbyte value);
            ArraySegment<byte> WriteShortValueAsStringWithCopy(short value);
            ArraySegment<byte> WriteStringValueAsStringWithCopy(string str);
            ArraySegment<byte> WriteUintValueAsStringWithCopy(uint value);
            ArraySegment<byte> WriteUlongValueAsStringWithCopy(ulong value);
            ArraySegment<byte> WriteUshortValueAsStringWithCopy(ushort value);
            void WriteToBuffer(ArraySegment<byte> data);
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
        }

        private sealed class JsonUTF8StreamWriter : IWriter
        {
            public Stream stream;
            private byte[] localBuffer;
            private byte[] mainBuffer;
            private int mainBufferCount;
            private int mainBufferLimit;
            private SlicedBuffer<byte> tempSlicedBuffer;
            private CompiledSettings settings;
            private readonly bool indent;
            private int currentIndentionDepth = 0;
            private readonly int maxIndentationDepth;
            private readonly byte[][] indentationLookup;

            public JsonUTF8StreamWriter(CompiledSettings settings)
            {
                mainBufferLimit = settings.writeBufferChunkSize;
                localBuffer = new byte[128];
                // We give some extra bytes in order to not always check remaining space
                mainBuffer = new byte[mainBufferLimit + 64];
                // Used for temporarily needed names e.g. Dictionary
                tempSlicedBuffer = new SlicedBuffer<byte>(settings.tempBufferSize, 128);
                this.settings = settings;

                indent = settings.indent;
                maxIndentationDepth = settings.maxIndentationDepth;
                indentationLookup = new byte[maxIndentationDepth][];
                InitIndentationLookup();
            }
           
            private void InitIndentationLookup()
            {                
                if (!indent) return;                

                List<byte> indentationBytes = new List<byte>();
                indentationBytes.Add((byte)'\n');
                for (int i = 0; i < settings.maxIndentationDepth; i++)
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            public void ResetBuffer()
            {
                mainBufferCount = 0;
                tempSlicedBuffer.Reset(true);
                currentIndentionDepth = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBuffer(byte[] data, int offset, int count)
            {
                if (mainBufferCount + count > mainBufferLimit) WriteBufferToStream();
                Array.Copy(data, offset, mainBuffer, mainBufferCount, count);
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
                WriteToBuffer(data, 0, data.Length);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBuffer(ArraySegment<byte> data)
            {
                if (mainBufferCount + data.Count > mainBufferLimit) WriteBufferToStream();
                data.CopyTo(mainBuffer, mainBufferCount);
                mainBufferCount += data.Count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBuffer(byte data)
            {
                if (mainBufferCount >= mainBufferLimit) WriteBufferToStream();
                mainBuffer[mainBufferCount++] = data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteToBufferWithoutCheck(byte[] data, int offset, int count)
            {
                Array.Copy(data, offset, mainBuffer, mainBufferCount, count);
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
                WriteToBufferWithoutCheck(data, 0, data.Length);
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
                if ((long)value <= long.MaxValue) WriteUnsignedInteger((long)value);
                else WriteString(value.ToString());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WriteLongValueAsStringWithCopy(long value)
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
                if (value <= long.MaxValue) WriteUnsignedInteger((long)value);
                else WriteString(value.ToString());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WriteUlongValueAsStringWithCopy(ulong value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                EnsureFreeBufferSpace(64);
                var countBefore = mainBufferCount;

                if (value <= long.MaxValue) WriteUnsignedInteger((long)value);
                else WriteString(value.ToString());

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
                if (value <= long.MaxValue) WriteUnsignedInteger((long)value);
                else WriteString(value.ToString());
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteIntValue(int value)
            {
                WriteSignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WriteIntValueAsStringWithCopy(int value)
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
            public ArraySegment<byte> WriteUintValueAsStringWithCopy(uint value)
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
                WriteUnsignedInteger(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WriteByteValueAsStringWithCopy(byte value)
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
            public ArraySegment<byte> WriteSbyteValueAsStringWithCopy(sbyte value)
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
            public ArraySegment<byte> WriteShortValueAsStringWithCopy(short value)
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
            public ArraySegment<byte> WriteUshortValueAsStringWithCopy(ushort value)
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
            public ArraySegment<byte> WriteFloatValueAsStringWithCopy(float value)
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
            public ArraySegment<byte> WriteDoubleValueAsStringWithCopy(double value)
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
            public ArraySegment<byte> WriteBoolValueAsStringWithCopy(bool value)
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
                    WriteToBufferWithoutCheck(QUOTES);
                    WriteEscapedString(str);
                    WriteToBufferWithoutCheck(QUOTES);
                }
                else WriteNullValue();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WriteStringValueAsStringWithCopy(string str)
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
            public void WriteCharValue(char value)
            {
                WriteToBufferWithoutCheck(QUOTES);
                WriteChar(value);
                WriteToBufferWithoutCheck(QUOTES);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySegment<byte> WriteCharValueAsStringWithCopy(char value)
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

            List<ArraySegment<byte>> indexNameList = new List<ArraySegment<byte>>();
            public ArraySegment<byte> GetCollectionIndexName(int index)
            {
                if (!settings.requiresItemNames) return default;

                if (index >= indexNameList.Count)
                {
                    for (int i = indexNameList.Count; i <= index; i++)
                    {
                        indexNameList.Add(default);
                    }
                }
                if (indexNameList[index].Array == null) indexNameList[index] = new ArraySegment<byte>($"[{index}]".ToByteArray());
                return indexNameList[index];
            }

            private static readonly byte[][] PositiveNumberBytesLookup = InitNumberBytesLookup(false, 256);
            private static readonly byte[][] NegativeNumberBytesLookup = InitNumberBytesLookup(true, 128);
            private static readonly byte[] Int32MinValueBytes = int.MinValue.ToString().ToByteArray();
            private static readonly byte[] Int64MinValueBytes = long.MinValue.ToString().ToByteArray();

            private static byte[][] InitNumberBytesLookup(bool negative, int size)
            {
                byte[][] lookup = new byte[size][];
                int factor = negative ? -1 : 1;

                for (int i = 0; i < size; i++)
                {
                    lookup[i] = Encoding.ASCII.GetBytes((i * factor).ToString());
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
                    int guaranteedCharSpace = (mainBufferLimit - mainBufferCount) / MAX_CHAR_LENGTH;
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
                    int guaranteedCharSpace = (mainBufferLimit - mainBufferCount) / MAX_CHAR_LENGTH;
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

            private void WriteSignedInteger(long inputValue)
            {
                var value = inputValue;
                bool isNegative = value < 0;
                if (isNegative)
                {
                    value = -value;
                    if (value < NegativeNumberBytesLookup.Length)
                    {
                        // If the value was long.MinValue negating it will cause an overflow and resulting again in long.MinValue,
                        // so we handle it as a special number
                        if (value == long.MinValue)
                        {
                            WriteToBuffer(Int64MinValueBytes);
                            return;
                        }
                        else
                        {
                            var bytes = NegativeNumberBytesLookup[value];
                            WriteToBuffer(bytes);
                            return;
                        }
                    }
                }
                if (value < PositiveNumberBytesLookup.Length)
                {
                    var bytes = PositiveNumberBytesLookup[value];
                    WriteToBuffer(bytes);
                    return;
                }

                const int maxDigits = 25;
                int index = maxDigits;
                while (value >= 10)
                {
                    value = Math.DivRem(value, 10, out long digit);
                    localBuffer[index--] = (byte)('0' + digit);
                }
                if (value > 0) localBuffer[index--] = (byte)('0' + value);
                if (isNegative) localBuffer[index--] = (byte)'-';

                WriteToBuffer(localBuffer, index + 1, maxDigits - index);
            }


            private void WriteSignedInteger(int inputValue)
            {
                var value = inputValue;
                bool isNegative = value < 0;
                if (isNegative)
                {
                    value = -value;
                    if (value < NegativeNumberBytesLookup.Length)
                    {
                        // If the value was int.MinValue negating it will cause an overflow and resulting again in int.MinValue,
                        // so we handle it as a special number
                        if (value == int.MinValue)
                        {
                            WriteToBuffer(Int32MinValueBytes);
                            return;
                        }
                        else
                        {
                            var bytes = NegativeNumberBytesLookup[value];
                            WriteToBuffer(bytes);
                            return;
                        }
                    }
                }
                else if (value < PositiveNumberBytesLookup.Length)
                {
                    var bytes = PositiveNumberBytesLookup[value];
                    WriteToBuffer(bytes);
                    return;
                }

                const int maxDigits = 25;
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

            private void WriteUnsignedInteger(long inputValue)
            {
                var value = inputValue;
                if (value < PositiveNumberBytesLookup.Length)
                {
                    var bytes = PositiveNumberBytesLookup[value];
                    WriteToBuffer(bytes, 0, bytes.Length);
                    return;
                }

                const int maxDigits = 25;
                int index = maxDigits;
                while (value >= 10)
                {
                    value = Math.DivRem(value, 10, out long digit);
                    localBuffer[index--] = (byte)('0' + digit);
                }
                if (value > 0) localBuffer[index--] = (byte)('0' + value);

                WriteToBuffer(localBuffer, index + 1, maxDigits - index);
            }

            //static readonly byte[] ZERO_FLOAT = "0.0".ToByteArray();
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
                return (float)CalculateNumDigits((double)value, out exponent, out numIntegralDigits, out numFractionalDigits, out printExponent, out failed);
#else
                const int MAX_SIGNIFICANT_DIGITS = 7;
                const int POS_EXPONENT_LIMIT = 7;
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

            private void WriteDouble(double inputValue)
            {
                var value = inputValue;
                if (HandleSpecialCases(value)) return;

                EnsureFreeBufferSpace(100);

                bool isNegative = value < 0;
                if (isNegative) value = -value;                

                value = CalculateNumDigits(value, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent, out bool failed);
                if (failed)
                {
                    WriteString(inputValue.ToString(CultureInfo.InvariantCulture));
                    return;
                }

                if (isNegative) WriteToBufferWithoutCheck((byte)'-');

                double integralPart = Math.Floor(value);
                double fractionalPart = value - integralPart;

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
                        else WriteString(value.ToString(CultureInfo.InvariantCulture)); // Then it must be subnormal
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

            double CalculateNumDigits(double value, out int exponent, out int numIntegralDigits, out int numFractionalDigits, out bool printExponent, out bool failed)
            {
                const int MAX_SIGNIFICANT_DIGITS = 16;
                const int POS_EXPONENT_LIMIT = 13;
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

            /*
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool IsSubnormal(double value)
            {
#if NETSTANDARD2_0
                long bits = BitConverter.DoubleToInt64Bits(value);
                const long exponentMask = 0x7FF0000000000000L;  // Exponent bits
                const long fractionMask = 0x000FFFFFFFFFFFFFL;  // Fraction bits

                long exponent = bits & exponentMask;
                long fraction = bits & fractionMask;

                // Subnormal numbers have an exponent of 0 but a non-zero fraction
                return exponent == 0 && fraction != 0;
#else
                return Double.IsSubnormal(value);                
#endif
            }
            */


            private static readonly byte[] HexMap = System.Text.Encoding.UTF8.GetBytes("0123456789abcdef");
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteByteAsHexWithoutCheck(byte value)
            {
                WriteToBufferWithoutCheck(HexMap[value >> 4]);
                WriteToBufferWithoutCheck(HexMap[value & 0xF]);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteByteAsHex(byte value)
            {
                EnsureFreeBufferSpace(2);
                WriteToBufferWithoutCheck(HexMap[value >> 4]);
                WriteToBufferWithoutCheck(HexMap[value & 0xF]);
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
                Span<byte> guidBytesSpan = new Span<byte>(localBuffer, 0, 16);
                guid.TryWriteBytes(guidBytesSpan); // loacalBuffer is always bigger than 16 bytes
                byte[] guidBytes = localBuffer;
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

            public void WriteDateTimeValue(DateTime dateTime)
            {
                EnsureFreeBufferSpace(32);

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
                WriteToBufferWithoutCheck((byte)'.');
                // Write Fractional second
                Write7Digits((int)(dateTime.Ticks % TimeSpan.TicksPerSecond));

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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]

            private void Write4Digits(int value)
            {
                int temp;
                temp = Math.DivRem(value, 1000, out value);
                WriteDigit(temp);

                temp = Math.DivRem(value, 100, out value);
                WriteDigit(temp);

                temp = Math.DivRem(value, 10, out value);
                WriteDigit(temp);
                WriteDigit(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void Write2Digits(int value)
            {
                int div;
                div = Math.DivRem(value, 10, out value);
                WriteDigit(div);
                WriteDigit(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void Write7Digits(int value)
            {
                // First digit
                int digit = Math.DivRem(value, 1000000, out value);
                WriteDigit(digit);

                // Second digit
                digit = Math.DivRem(value, 100000, out value);
                WriteDigit(digit);

                // Third digit
                digit = Math.DivRem(value, 10000, out value);
                WriteDigit(digit);

                // Fourth digit
                digit = Math.DivRem(value, 1000, out value);
                WriteDigit(digit);

                // Fifth digit
                digit = Math.DivRem(value, 100, out value);
                WriteDigit(digit);

                // Sixth digit
                digit = Math.DivRem(value, 10, out value);
                WriteDigit(digit);

                // Seventh digit
                WriteDigit(value);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void WriteDigit(int value)
            {
                WriteToBufferWithoutCheck((byte)('0' + value));
            }
        }

    }
}

