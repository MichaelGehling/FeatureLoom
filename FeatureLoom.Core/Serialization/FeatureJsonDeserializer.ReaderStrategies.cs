using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Serialization;

public partial class FeatureJsonDeserializer
{

    private Func<C, C> CreateValueFieldWriterViaStrategy<T, V, C, S, SV>(Func<T, V, T> setValueAndReturn, CachedTypeReader cachedTypeReader) where S : struct, IReaderStrategy<SV> where T : C
    {
        return parentItem =>
        {
#if NET8_0_OR_GREATER
            var v = S.Read(cachedTypeReader);
#else
            var v = default(S).Read(cachedTypeReader);
#endif
            V fieldValue = Unsafe.As<SV, V>(ref v);
            parentItem = setValueAndReturn((T)parentItem, fieldValue);
            return parentItem;
        };
    }

    private Func<C, C> CreateObjFieldWriterViaStrategy<T, V, C, S, SV>(Action<T, V> setValue, CachedTypeReader cachedTypeReader) where S : struct, IReaderStrategy<SV> where T : C
    {
        return parentItem =>
        {
#if NET8_0_OR_GREATER
            var v = S.Read(cachedTypeReader);
#else
            var v = default(S).Read(cachedTypeReader);
#endif
            V fieldValue = Unsafe.As<SV, V>(ref v);
            setValue((T)parentItem, fieldValue);
            return parentItem;
        };
    }

    private TypeReaderInitializer CreateGenericEnumerableTypeReaderViaStrategy<T, E, S, SV>(CachedTypeReader cachedTypeReader, Func<IEnumerable<E>, T> constructor, Pool<List<E>> bufferPool) where S : struct, IReaderStrategy<SV>
    {
        var r = () =>
        {
            if (TryReadNullValue()) return default;
            var b = buffer.CurrentByte;
            if (b != '[') throw new Exception("Failed reading Array");
            if (!buffer.TryNextByte()) throw new Exception("Failed reading Array");
            List<E> elementBuffer = bufferPool.Take();
            while (true)
            {
                b = SkipWhiteSpaces();
                if (b == ']') break;
#if NET8_0_OR_GREATER
                SV v = S.Read(cachedTypeReader);
#else
                SV v = default(S).Read(cachedTypeReader);
#endif                
                E value = Unsafe.As<SV, E>(ref v);
                elementBuffer.Add(value);
                b = SkipWhiteSpaces();
                if (b == ',') buffer.TryNextByte();
                else if (b != ']') throw new Exception("Failed reading Array");
            }
            T item = constructor(elementBuffer);
            bufferPool.Return(elementBuffer);
            if (buffer.CurrentByte != ']') throw new Exception("Failed reading Array");
            buffer.TryNextByte();
            return item;
        };
        return TypeReaderInitializer.Create(this, r, null, true);
    }

    private TypeReaderInitializer CreateGenericArrayTypeReaderViaStrategy<E, S, SV>(CachedTypeReader cachedTypeReader, Pool<List<E>> bufferPool) where S : struct, IReaderStrategy<SV>
    {
        var stringArrayReader = () =>
        {
            byte b = SkipWhiteSpaces();
            if (b != '[') throw new Exception("Failed reading Array");
            if (!buffer.TryNextByte()) throw new Exception("Failed reading Array");
            List<E> elementBuffer = bufferPool.Take();
            while (true)
            {
                b = SkipWhiteSpaces();
                if (b == ']') break;
#if NET8_0_OR_GREATER
                SV v = S.Read(cachedTypeReader);
#else
                SV v = default(S).Read(cachedTypeReader);
#endif                
                E value = Unsafe.As<SV, E>(ref v);
                elementBuffer.Add(value);
                b = SkipWhiteSpaces();
                if (b == ',') buffer.TryNextByte();
                else if (b != ']') throw new Exception("Failed reading Array");
            }
            E[] item = elementBuffer.ToArray();
            if (settings.enableReferenceResolution) SetItemRefInCurrentItemInfo(item);
            bufferPool.Return(elementBuffer);
            buffer.TryNextByte();
            return item;
        };
        return TypeReaderInitializer.Create(this, stringArrayReader, null, true);
    }


    interface IReaderStrategy<TValue>
    {
#if NET8_0_OR_GREATER
        public static abstract TValue Read(CachedTypeReader reader);
#else
        public TValue Read(CachedTypeReader reader);
#endif
    }

    struct GenericReaderStrategy<T> : IReaderStrategy<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static T Read(CachedTypeReader reader) => reader.ReadValue_NoCheck<T>();
#else
        public T Read(CachedTypeReader reader) => reader.ReadValue_NoCheck<T>();
#endif
    }

    struct StringReaderStrategy : IReaderStrategy<string>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static string Read(CachedTypeReader reader) => reader.Parent.ReadStringValueOrNull();
#else
        public string Read(CachedTypeReader reader) => reader.Parent.ReadStringValueOrNull();
#endif
    }

    struct CharReaderStrategy : IReaderStrategy<char>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static char Read(CachedTypeReader reader) => reader.Parent.ReadCharValue();
#else
        public char Read(CachedTypeReader reader) => reader.Parent.ReadCharValue();
#endif
    }

    struct SByteReaderStrategy : IReaderStrategy<sbyte>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static sbyte Read(CachedTypeReader reader) => reader.Parent.ReadSbyteValue();
#else
        public sbyte Read(CachedTypeReader reader) => reader.Parent.ReadSbyteValue();
#endif
    }

    struct ByteReaderStrategy : IReaderStrategy<byte>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static byte Read(CachedTypeReader reader) => reader.Parent.ReadByteValue();
#else
        public byte Read(CachedTypeReader reader) => reader.Parent.ReadByteValue();
#endif
    }

    struct Int16ReaderStrategy : IReaderStrategy<short>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static short Read(CachedTypeReader reader) => reader.Parent.ReadShortValue();
#else
        public short Read(CachedTypeReader reader) => reader.Parent.ReadShortValue();
#endif
    }

    struct UInt16ReaderStrategy : IReaderStrategy<ushort>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static ushort Read(CachedTypeReader reader) => reader.Parent.ReadUshortValue();
#else
        public ushort Read(CachedTypeReader reader) => reader.Parent.ReadUshortValue();
#endif
    }

    struct Int32ReaderStrategy : IReaderStrategy<int>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static int Read(CachedTypeReader reader) => reader.Parent.ReadIntValue();
#else
        public int Read(CachedTypeReader reader) => reader.Parent.ReadIntValue();
#endif
    }

    struct UInt32ReaderStrategy : IReaderStrategy<uint>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static uint Read(CachedTypeReader reader) => reader.Parent.ReadUintValue();
#else
        public uint Read(CachedTypeReader reader) => reader.Parent.ReadUintValue();
#endif
    }

    struct Int64ReaderStrategy : IReaderStrategy<long>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static long Read(CachedTypeReader reader) => reader.Parent.ReadLongValue();
#else
        public long Read(CachedTypeReader reader) => reader.Parent.ReadLongValue();
#endif
    }

    struct UInt64ReaderStrategy : IReaderStrategy<ulong>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static ulong Read(CachedTypeReader reader) => reader.Parent.ReadUlongValue();
#else
        public ulong Read(CachedTypeReader reader) => reader.Parent.ReadUlongValue();
#endif
    }


    struct BoolReaderStrategy : IReaderStrategy<bool>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static bool Read(CachedTypeReader reader) => reader.Parent.ReadBoolValue();
#else
        public bool Read(CachedTypeReader reader) => reader.Parent.ReadBoolValue();
#endif
    }

    struct FloatReaderStrategy : IReaderStrategy<float>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static float Read(CachedTypeReader reader) => reader.Parent.ReadFloatValue();
#else
        public float Read(CachedTypeReader reader) => reader.Parent.ReadFloatValue();
#endif
    }

    struct DoubleReaderStrategy : IReaderStrategy<double>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET8_0_OR_GREATER
        public static double Read(CachedTypeReader reader) => reader.Parent.ReadDoubleValue();
#else
        public double Read(CachedTypeReader reader) => reader.Parent.ReadDoubleValue(); 
#endif
    }
}

