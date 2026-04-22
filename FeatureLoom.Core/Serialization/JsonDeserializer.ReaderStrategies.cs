using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using static FeatureLoom.Serialization.JsonDeserializer;

namespace FeatureLoom.Serialization;

public partial class JsonDeserializer
{

    private Func<C, C> CreateValueFieldWriterViaStrategy<T, V, C, S, SV>(Func<T, V, T> setValueAndReturn, CachedTypeReader cachedTypeReader) where S : struct, IReaderStrategy<SV> where T : C
    {
        return parentItem =>
        {
#if NET5_0_OR_GREATER
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
#if NET5_0_OR_GREATER
            var v = S.Read(cachedTypeReader);
#else
            var v = default(S).Read(cachedTypeReader);
#endif
            V fieldValue = Unsafe.As<SV, V>(ref v);
            setValue((T)parentItem, fieldValue);
            return parentItem;
        };
    }

    private Func<C, C> CreateInitOnlyValueFieldWriterViaStrategy<T, V, C, S, SV>(FieldInfo fieldInfo, CachedTypeReader cachedTypeReader) where S : struct, IReaderStrategy<SV> where T : C
    {
        return parentItem =>
        {
#if NET5_0_OR_GREATER
            var v = S.Read(cachedTypeReader);
#else
            var v = default(S).Read(cachedTypeReader);
#endif
            V fieldValue = Unsafe.As<SV, V>(ref v);
            var boxedItem = (object)parentItem;
            fieldInfo.SetValue(boxedItem, fieldValue);
            parentItem = (T)boxedItem;
            return parentItem;
        };
    }

    private Func<C, C> CreateInitOnlyObjFieldWriterViaStrategy<T, V, C, S, SV>(FieldInfo fieldInfo, CachedTypeReader cachedTypeReader) where S : struct, IReaderStrategy<SV> where T : C
    {
        return parentItem =>
        {
#if NET5_0_OR_GREATER
            var v = S.Read(cachedTypeReader);
#else
            var v = default(S).Read(cachedTypeReader);
#endif
            V fieldValue = Unsafe.As<SV, V>(ref v);
            fieldInfo.SetValue(parentItem, fieldValue);
            return parentItem;
        };
    }

    private TypeReaderInitializer CreateGenericEnumerableTypeReaderViaStrategy<T, E, S, SV>(CachedTypeReader elementTypeReader, Func<IEnumerable<E>, T> constructor, Pool<List<E>> bufferPool, BaseTypeSettings typeSettings) where S : struct, IReaderStrategy<SV>
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
#if NET5_0_OR_GREATER
                SV v = S.Read(elementTypeReader);
#else
                SV v = default(S).Read(elementTypeReader);
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
        return TypeReaderInitializer.Create(this, r, null, elementTypeReader.WriteRefPath, typeSettings);
    }

    private TypeReaderInitializer CreateGenericArrayTypeReaderViaStrategy<E, S, SV>(CachedTypeReader elementTypeReader, Pool<List<E>> bufferPool, BaseTypeSettings typeSettings) where S : struct, IReaderStrategy<SV>
    {
        bool setItemRef = elementTypeReader.ResolveRefPath;
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
#if NET5_0_OR_GREATER
                SV v = S.Read(elementTypeReader);
#else
                SV v = default(S).Read(elementTypeReader);
#endif                
                E value = Unsafe.As<SV, E>(ref v);
                elementBuffer.Add(value);
                b = SkipWhiteSpaces();
                if (b == ',') buffer.TryNextByte();
                else if (b != ']') throw new Exception("Failed reading Array");
            }
            E[] item = elementBuffer.ToArray();
            if (setItemRef) SetItemRefInCurrentItemInfo(item);
            bufferPool.Return(elementBuffer);
            buffer.TryNextByte();
            return item;
        };
        return TypeReaderInitializer.Create(this, stringArrayReader, null, elementTypeReader.WriteRefPath, typeSettings);
    }


    interface IReaderStrategy<TValue>
    {
#if NET5_0_OR_GREATER
        public static abstract TValue Read(CachedTypeReader reader);
#else
        public TValue Read(CachedTypeReader reader);
#endif
    }

    struct GenericReaderStrategy<T> : IReaderStrategy<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static T Read(CachedTypeReader reader) => reader.ReadValue_NoCheck<T>();
#else
        public T Read(CachedTypeReader reader) => reader.ReadValue_NoCheck<T>();
#endif
    }

    struct StringReaderStrategy : IReaderStrategy<string>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static string Read(CachedTypeReader reader) => reader.Parent.ReadStringValueOrNull();
#else
        public string Read(CachedTypeReader reader) => reader.Parent.ReadStringValueOrNull();
#endif
    }

    struct StringReader_WithoutStringCache_Strategy : IReaderStrategy<string>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static string Read(CachedTypeReader reader) => reader.Parent.ReadStringValueOrNull_WithoutStringCache();
#else
        public string Read(CachedTypeReader reader) => reader.Parent.ReadStringValueOrNull_WithoutStringCache();
#endif
    }

    struct StringReader_WithStringCache_Strategy : IReaderStrategy<string>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static string Read(CachedTypeReader reader) => reader.Parent.ReadStringValueOrNull_WithStringCache();
#else
        public string Read(CachedTypeReader reader) => reader.Parent.ReadStringValueOrNull_WithStringCache();
#endif
    }

    struct CharReaderStrategy : IReaderStrategy<char>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static char Read(CachedTypeReader reader) => reader.Parent.ReadCharValue();
#else
        public char Read(CachedTypeReader reader) => reader.Parent.ReadCharValue();
#endif
    }

    struct SByteReaderStrategy : IReaderStrategy<sbyte>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static sbyte Read(CachedTypeReader reader) => reader.Parent.ReadSbyteValue();
#else
        public sbyte Read(CachedTypeReader reader) => reader.Parent.ReadSbyteValue();
#endif
    }

    struct ByteReaderStrategy : IReaderStrategy<byte>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static byte Read(CachedTypeReader reader) => reader.Parent.ReadByteValue();
#else
        public byte Read(CachedTypeReader reader) => reader.Parent.ReadByteValue();
#endif
    }

    struct Int16ReaderStrategy : IReaderStrategy<short>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static short Read(CachedTypeReader reader) => reader.Parent.ReadShortValue();
#else
        public short Read(CachedTypeReader reader) => reader.Parent.ReadShortValue();
#endif
    }

    struct UInt16ReaderStrategy : IReaderStrategy<ushort>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static ushort Read(CachedTypeReader reader) => reader.Parent.ReadUshortValue();
#else
        public ushort Read(CachedTypeReader reader) => reader.Parent.ReadUshortValue();
#endif
    }

    struct Int32ReaderStrategy : IReaderStrategy<int>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static int Read(CachedTypeReader reader) => reader.Parent.ReadIntValue();
#else
        public int Read(CachedTypeReader reader) => reader.Parent.ReadIntValue();
#endif
    }

    struct UInt32ReaderStrategy : IReaderStrategy<uint>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static uint Read(CachedTypeReader reader) => reader.Parent.ReadUintValue();
#else
        public uint Read(CachedTypeReader reader) => reader.Parent.ReadUintValue();
#endif
    }

    struct Int64ReaderStrategy : IReaderStrategy<long>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static long Read(CachedTypeReader reader) => reader.Parent.ReadLongValue();
#else
        public long Read(CachedTypeReader reader) => reader.Parent.ReadLongValue();
#endif
    }

    struct UInt64ReaderStrategy : IReaderStrategy<ulong>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static ulong Read(CachedTypeReader reader) => reader.Parent.ReadUlongValue();
#else
        public ulong Read(CachedTypeReader reader) => reader.Parent.ReadUlongValue();
#endif
    }


    struct BoolReaderStrategy : IReaderStrategy<bool>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static bool Read(CachedTypeReader reader) => reader.Parent.ReadBoolValue();
#else
        public bool Read(CachedTypeReader reader) => reader.Parent.ReadBoolValue();
#endif
    }

    struct FloatReaderStrategy : IReaderStrategy<float>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static float Read(CachedTypeReader reader) => reader.Parent.ReadFloatValue();
#else
        public float Read(CachedTypeReader reader) => reader.Parent.ReadFloatValue();
#endif
    }

    struct DoubleReaderStrategy : IReaderStrategy<double>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
        public static double Read(CachedTypeReader reader) => reader.Parent.ReadDoubleValue();
#else
        public double Read(CachedTypeReader reader) => reader.Parent.ReadDoubleValue(); 
#endif
    }
}

