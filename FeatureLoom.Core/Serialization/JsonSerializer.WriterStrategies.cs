using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization;

public sealed partial class JsonSerializer
{
    /// <summary>
    /// Mirrors <c>JsonDeserializer.IReaderStrategy</c> on the write side. Implementations are
    /// structs, so a generic method constrained to <c>struct, IWriterStrategy&lt;TValue&gt;</c> gets
    /// its own JIT instantiation and the Write call is inlined instead of going through a delegate.
    /// </summary>
    interface IWriterStrategy<TValue>
    {
#if NET5_0_OR_GREATER
        /// <summary>
        /// Upper bound of bytes a single value can occupy, or 0 if the size is unbounded/variable.
        /// Only strategies with a positive bound can be written in a reserved batch.
        /// </summary>
        static abstract int MaxBytesPerValue { get; }

        static abstract void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, TValue value);

        /// <summary>
        /// Writes the value without ensuring buffer space. Only called when the caller reserved
        /// at least <see cref="MaxBytesPerValue"/> bytes.
        /// </summary>
        static abstract void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, TValue value);
#else
        void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, TValue value);
#endif
    }

    /// <summary>
    /// Fallback strategy that keeps the previous behaviour: it delegates to the element's
    /// CachedTypeWriter, including the null check and the type deviation check.
    /// </summary>
    struct GenericWriterStrategy<E> : IWriterStrategy<E>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, E value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, E value) => typeWriter.WriteItem(value, default);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, E value) => typeWriter.WriteItem(value, default);
#endif
    }

    /// <summary>
    /// Writes a byte array as a base64 string, bypassing the CachedTypeWriter delegate.
    /// Only selected when the serializer is configured for base64 output.
    /// </summary>
    struct ByteArrayBase64WriterStrategy : IWriterStrategy<byte[]>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, byte[] value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, byte[] value)
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, byte[] value)
#endif
        {
            if (value == null) writer.WriteNullValue();
            else writer.WriteBytesAsBase64(value);
        }
    }

    /// <summary>
    /// Writes a byte array as a JSON number array, bypassing the CachedTypeWriter delegate.
    /// Only selected when the serializer is configured for number array output.
    /// </summary>
    struct ByteArrayNumbersWriterStrategy : IWriterStrategy<byte[]>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, byte[] value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, byte[] value)
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, byte[] value)
#endif
        {
            if (value == null) writer.WriteNullValue();
            else writer.WriteBytesAsArray(value);
        }
    }

    struct GuidWriterStrategy : IWriterStrategy<Guid>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.GUID_MAX_BYTES;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, Guid value) => writer.WriteGuidValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, Guid value) => writer.WriteGuidValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, Guid value) => writer.WriteGuidValue(value);
#endif
    }

    struct DateTimeWriterStrategy : IWriterStrategy<DateTime>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, DateTime value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, DateTime value) => writer.WriteDateTimeValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, DateTime value) => writer.WriteDateTimeValue(value);
#endif
    }

    struct TimeSpanWriterStrategy : IWriterStrategy<TimeSpan>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, TimeSpan value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, TimeSpan value) => writer.WriteTimeSpanValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, TimeSpan value) => writer.WriteTimeSpanValue(value);
#endif
    }

#if NET6_0_OR_GREATER
    struct DateOnlyWriterStrategy : IWriterStrategy<DateOnly>
    {
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, DateOnly value) => Write(writer, typeWriter, value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, DateOnly value) => writer.WriteDateOnlyValue(value);
    }

    struct TimeOnlyWriterStrategy : IWriterStrategy<TimeOnly>
    {
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, TimeOnly value) => Write(writer, typeWriter, value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, TimeOnly value) => writer.WriteTimeOnlyValue(value);
    }
#endif

    struct StringWriterStrategy : IWriterStrategy<string>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, string value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, string value)
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, string value)
#endif
        {
            if (value == null) writer.WriteNullValue();
            else writer.WriteStringValue(value);
        }
    }

    struct SByteWriterStrategy : IWriterStrategy<sbyte>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.SBYTE_MAX_BYTES;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, sbyte value) => writer.WriteSbyteValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, sbyte value) => writer.WriteSbyteValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, sbyte value) => writer.WriteSbyteValue(value);
#endif
    }

    struct ByteWriterStrategy : IWriterStrategy<byte>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.BYTE_MAX_BYTES;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, byte value) => writer.WriteByteValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, byte value) => writer.WriteByteValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, byte value) => writer.WriteByteValue(value);
#endif
    }

    struct Int16WriterStrategy : IWriterStrategy<short>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.INT16_MAX_BYTES;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, short value) => writer.WriteShortValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, short value) => writer.WriteShortValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, short value) => writer.WriteShortValue(value);
#endif
    }

    struct UInt16WriterStrategy : IWriterStrategy<ushort>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.UINT16_MAX_BYTES;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, ushort value) => writer.WriteUshortValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, ushort value) => writer.WriteUshortValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, ushort value) => writer.WriteUshortValue(value);
#endif
    }

    struct Int32WriterStrategy : IWriterStrategy<int>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.INT32_MAX_BYTES;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, int value) => writer.WriteIntValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, int value) => writer.WriteIntValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, int value) => writer.WriteIntValue(value);
#endif
    }

    struct UInt32WriterStrategy : IWriterStrategy<uint>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.UINT32_MAX_BYTES;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, uint value) => writer.WriteUintValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, uint value) => writer.WriteUintValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, uint value) => writer.WriteUintValue(value);
#endif
    }

    struct Int64WriterStrategy : IWriterStrategy<long>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.INT64_MAX_BYTES;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, long value) => writer.WriteLongValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, long value) => writer.WriteLongValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, long value) => writer.WriteLongValue(value);
#endif
    }

    struct UInt64WriterStrategy : IWriterStrategy<ulong>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.UINT64_MAX_BYTES;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, ulong value) => writer.WriteUlongValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, ulong value) => writer.WriteUlongValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, ulong value) => writer.WriteUlongValue(value);
#endif
    }

    struct FloatWriterStrategy : IWriterStrategy<float>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, float value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, float value) => writer.WriteFloatValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, float value) => writer.WriteFloatValue(value);
#endif
    }

    struct DoubleWriterStrategy : IWriterStrategy<double>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, double value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, double value) => writer.WriteDoubleValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, double value) => writer.WriteDoubleValue(value);
#endif
    }

    struct DecimalWriterStrategy : IWriterStrategy<decimal>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, decimal value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, decimal value) => writer.WriteDecimalValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, decimal value) => writer.WriteDecimalValue(value);
#endif
    }

    struct BoolWriterStrategy : IWriterStrategy<bool>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => JsonUTF8StreamWriter.BOOL_MAX_BYTES;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, bool value) => writer.WriteBoolValueWithoutCheck(value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, bool value) => writer.WriteBoolValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, bool value) => writer.WriteBoolValue(value);
#endif
    }

    struct CharWriterStrategy : IWriterStrategy<char>
    {
#if NET5_0_OR_GREATER
        public static int MaxBytesPerValue => 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteWithoutCheck(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, char value) => Write(writer, typeWriter, value);
#endif
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, char value) => writer.WriteCharValue(value);
#else
        public void Write(JsonUTF8StreamWriter writer, CachedTypeWriter typeWriter, char value) => writer.WriteCharValue(value);
#endif
    }

    /// <summary>
    /// Abstracts indexed element access, so the element loop only has to be written once and is
    /// still specialized (and inlined) per container shape, avoiding interface dispatch per element.
    /// </summary>
    interface IIndexedAccessor<T, E>
    {
#if NET5_0_OR_GREATER
        static abstract int GetCount(T collection);
        static abstract E GetElement(T collection, int index);
#else
        int GetCount(T collection);
        E GetElement(T collection, int index);
#endif
    }

    struct ArrayAccessor<E> : IIndexedAccessor<E[], E>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static int GetCount(E[] collection) => collection.Length;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static E GetElement(E[] collection, int index) => collection[index];
#else
        public int GetCount(E[] collection) => collection.Length;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E GetElement(E[] collection, int index) => collection[index];
#endif
    }

    struct ListAccessor<E> : IIndexedAccessor<List<E>, E>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static int GetCount(List<E> collection) => collection.Count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static E GetElement(List<E> collection, int index) => collection[index];
#else
        public int GetCount(List<E> collection) => collection.Count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E GetElement(List<E> collection, int index) => collection[index];
#endif
    }

    struct IListAccessor<T, E> : IIndexedAccessor<T, E> where T : IList<E>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static int GetCount(T collection) => collection.Count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static E GetElement(T collection, int index) => collection[index];
#else
        public int GetCount(T collection) => collection.Count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E GetElement(T collection, int index) => collection[index];
#endif
    }

    struct IReadOnlyListAccessor<T, E> : IIndexedAccessor<T, E> where T : IReadOnlyList<E>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static int GetCount(T collection) => collection.Count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static E GetElement(T collection, int index) => collection[index];
#else
        public int GetCount(T collection) => collection.Count;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E GetElement(T collection, int index) => collection[index];
#endif
    }

    /// <summary>
    /// Decides whether the element can be written by a direct writer strategy, bypassing the
    /// CachedTypeWriter delegate indirection. Only allowed if nothing may wrap or redirect the
    /// element output (item infos, always-on type info, custom handlers, polymorphic elements).
    /// </summary>
    private bool CanUseDirectValueStrategy(CachedTypeWriter elementHandler, Type elementType)
    {
        if (settings.requiresItemInfos) return false;
        if (settings.typeInfoHandling == TypeInfoHandling.AddAllTypeInfo) return false;
        if (elementHandler.HandlerType != elementType) return false;
        if (!elementType.IsValueType && !elementType.IsSealed) return false;

        foreach (var creator in settings.itemHandlerCreators)
        {
            if (creator.SupportsType(elementType)) return false;
        }

        return true;
    }

    /// <summary>
    /// Reference element types only bypass the CachedTypeWriter path if a dedicated,
    /// null-safe writer strategy exists for them (the generic fallback strategy does not
    /// handle nulls itself).
    /// </summary>
    private bool CanUseDirectReferenceStrategy(CachedTypeWriter elementHandler, Type elementType)
    {
        if (elementType != typeof(string) && elementType != typeof(byte[])) return false;
        return CanUseDirectValueStrategy(elementHandler, elementType);
    }

    /// <summary>
    /// Shared entry point for all indexed container shapes. Selects the element writer strategy
    /// once at handler creation time and hands it to the single, shape-agnostic element loop.
    /// </summary>
    private void CreateIndexedItemHandler<T, E, ACC>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler)
        where ACC : struct, IIndexedAccessor<T, E>
    {
        if (CanUseDirectValueStrategy(elementHandler, typeof(E)))
        {
            if (typeof(E) == typeof(int)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, Int32WriterStrategy, int>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(long)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, Int64WriterStrategy, long>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(double)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, DoubleWriterStrategy, double>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(string)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, StringWriterStrategy, string>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(bool)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, BoolWriterStrategy, bool>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(float)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, FloatWriterStrategy, float>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(decimal)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, DecimalWriterStrategy, decimal>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(uint)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, UInt32WriterStrategy, uint>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(ulong)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, UInt64WriterStrategy, ulong>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(short)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, Int16WriterStrategy, short>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(ushort)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, UInt16WriterStrategy, ushort>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(sbyte)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, SByteWriterStrategy, sbyte>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(byte)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, ByteWriterStrategy, byte>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(char)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, CharWriterStrategy, char>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(Guid)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, GuidWriterStrategy, Guid>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(DateTime)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, DateTimeWriterStrategy, DateTime>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(TimeSpan)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, TimeSpanWriterStrategy, TimeSpan>(typeHandler, elementHandler); return; }
#if NET6_0_OR_GREATER
            if (typeof(E) == typeof(DateOnly)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, DateOnlyWriterStrategy, DateOnly>(typeHandler, elementHandler); return; }
            if (typeof(E) == typeof(TimeOnly)) { CreateIndexedItemHandlerViaStrategy<T, E, ACC, TimeOnlyWriterStrategy, TimeOnly>(typeHandler, elementHandler); return; }
#endif
            if (typeof(E) == typeof(byte[]))
            {
                // The output format is fixed by the settings, so the strategy can be selected here
                // and the per-element format branch disappears completely. Resolved from the
                // element writer's own settings, so a locally overridden element format is honored.
                if (settings.ResolveWriteByteArrayAsBase64String(elementHandler.TypeSettings)) CreateIndexedItemHandlerViaStrategy<T, E, ACC, ByteArrayBase64WriterStrategy, byte[]>(typeHandler, elementHandler);
                else CreateIndexedItemHandlerViaStrategy<T, E, ACC, ByteArrayNumbersWriterStrategy, byte[]>(typeHandler, elementHandler);
                return;
            }
        }

        CreateIndexedItemHandlerViaStrategy<T, E, ACC, GenericWriterStrategy<E>, E>(typeHandler, elementHandler);
    }

    /// <summary>
    /// The one and only element loop for indexed containers. Both the element access (ACC) and the
    /// element writing (S) are struct type parameters, so the JIT specializes and inlines them.
    /// </summary>
    private void CreateIndexedItemHandlerViaStrategy<T, E, ACC, S, SV>(CachedTypeWriter typeHandler, CachedTypeWriter elementHandler)
        where ACC : struct, IIndexedAccessor<T, E>
        where S : struct, IWriterStrategy<SV>
    {
        Action<T> itemHandler = (collection) =>
        {
#if NET5_0_OR_GREATER
            int count = ACC.GetCount(collection);
#else
            int count = default(ACC).GetCount(collection);
#endif
            if (count == 0) return;

            // The writer and the element handler live in closure fields. Loading them once keeps
            // them in registers for the whole loop instead of re-reading the closure per element.
            var w = writer;
            var eh = elementHandler;

#if NET5_0_OR_GREATER
            // Strategies with a known maximum size can reserve space for many elements at once,
            // so the per-element buffer check disappears. MaxBytesPerValue is a constant of the
            // struct type parameter, so the JIT folds this branch away for the other strategies.
            // Indentation is excluded because it emits a variable number of bytes per separator.
            if (S.MaxBytesPerValue > 0 && !w.IsIndenting)
            {
                int perElement = S.MaxBytesPerValue + 1; // + 1 for the separating comma

                // The very first element has no leading comma. Writing it outside keeps the
                // inner loop free of any per-element index test.
                w.EnsureFreeBufferSpace(S.MaxBytesPerValue);
                {
                    E first = ACC.GetElement(collection, 0);
                    S.WriteWithoutCheck(w, eh, Unsafe.As<E, SV>(ref first));
                }

                int i = 1;
                while (i < count)
                {
                    // The batch is sized from the space that is actually left, so the buffer still
                    // fills up completely before it is flushed.
                    int end = Math.Min(i + w.BeginFixedSizeBatch(perElement), count);
                    for (; i < end; i++)
                    {
                        w.WriteCommaWithoutCheck();
                        E element = ACC.GetElement(collection, i);
                        S.WriteWithoutCheck(w, eh, Unsafe.As<E, SV>(ref element));
                    }
                }
                return;
            }
#endif

            WriteElement(w, eh, collection, 0);
            for (int i = 1; i < count; i++)
            {
                w.WriteComma();
                WriteElement(w, eh, collection, i);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void WriteElement(JsonUTF8StreamWriter w, CachedTypeWriter eh, T c, int index)
            {
#if NET5_0_OR_GREATER
                E element = ACC.GetElement(c, index);
                S.Write(w, eh, Unsafe.As<E, SV>(ref element));
#else
                E element = default(ACC).GetElement(c, index);
                default(S).Write(w, eh, Unsafe.As<E, SV>(ref element));
#endif
            }
        };

        typeHandler.SetItemWriter(CreateArrayItemWriter(typeHandler, itemHandler), false);
    }
}
