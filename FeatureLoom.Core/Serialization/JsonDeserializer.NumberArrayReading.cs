using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Serialization;

// Conditional compilation in this file:
// - NETSTANDARD2_0 has no ReadOnlySpan<byte> support here, so the bulk fast path is omitted entirely
//   and only the general per-element path is compiled.
// - NET5_0_OR_GREATER supports static abstract interface members, so the parser policies are static
//   and get fully devirtualized/inlined per instantiation. Older targets use instance members that
//   are called on default(TParser).
// NET5_0_OR_GREATER and NETSTANDARD2_0 are mutually exclusive, so they are never nested.

public sealed partial class JsonDeserializer
{
    /// <summary>
    /// Parses a single numeric element of a JSON number array. Implementations are structs, so the
    /// generic sequence reader gets a dedicated, fully inlined instantiation per element type
    /// instead of a delegate call per element.
    /// </summary>
    interface INumberElementParser<T>
    {
#if NET5_0_OR_GREATER
        /// <summary>
        /// Tries to parse a compact (plain, fully buffered, in-range) integer starting at
        /// <paramref name="pos"/>. On success <paramref name="pos"/> is advanced past the digits,
        /// otherwise it stays unchanged and the caller must fall back to the general path.
        /// </summary>
        static abstract bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out T value);

        static abstract T ReadGeneral(JsonDeserializer deserializer);
#elif NETSTANDARD2_0
        T ReadGeneral(JsonDeserializer deserializer);
#else
        bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out T value);

        T ReadGeneral(JsonDeserializer deserializer);
#endif
    }

#if !NETSTANDARD2_0
    /// <summary>
    /// Shared compact parser for all signed integer element types. Rejects everything that is not a
    /// plain, fully buffered, in-range integer so the general path can handle (and validate) it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseCompactSigned(ReadOnlySpan<byte> span, ref int pos, long min, long max, out long value)
    {
        value = 0;
        int p = pos;
        bool isNegative = span[p] == (byte)'-';
        if (isNegative) p++;

        int digitStart = p;
        ulong uValue = 0;
        while (p < span.Length)
        {
            uint digit = (uint)(span[p] - (byte)'0');
            if (digit > 9u) break;
            unchecked { uValue = uValue * 10 + digit; }
            p++;
        }

        int digitCount = p - digitStart;
        if (digitCount == 0) return false;      // not a plain integer
        if (p == span.Length) return false;     // element may be truncated by the buffer window
        if (digitCount > 19) return false;      // may exceed ulong -> general path reports it

        // Up to 19 digits always fit into ulong, so the accumulation above cannot have wrapped and
        // the conversion to long only needs an explicit range check.
        const ulong maxPos = (ulong)long.MaxValue;
        const ulong maxNegAbs = 1UL + (ulong)long.MaxValue; // abs(long.MinValue)
        long parsed;
        if (isNegative)
        {
            if (uValue > maxNegAbs) return false;
            parsed = uValue == maxNegAbs ? long.MinValue : -(long)uValue;
        }
        else
        {
            if (uValue > maxPos) return false;
            parsed = (long)uValue;
        }

        if (parsed < min || parsed > max) return false;

        pos = p;
        value = parsed;
        return true;
    }

    /// <summary>
    /// Shared compact parser for all unsigned integer element types.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseCompactUnsigned(ReadOnlySpan<byte> span, ref int pos, ulong max, out ulong value)
    {
        value = 0;
        int p = pos;
        if (span[p] == (byte)'-') return false;

        ulong uValue = 0;
        while (p < span.Length)
        {
            uint digit = (uint)(span[p] - (byte)'0');
            if (digit > 9u) break;
            unchecked { uValue = uValue * 10 + digit; }
            p++;
        }

        int digitCount = p - pos;
        if (digitCount == 0) return false;
        if (p == span.Length) return false;
        if (digitCount > 19) return false;      // 20 digits may already overflow ulong
        if (uValue > max) return false;

        pos = p;
        value = uValue;
        return true;
    }

    /// <summary>
    /// Shared compact parser for the floating point element types. It accepts the common shape
    /// "[-]digits[.digits][(e|E)[+|-]digits]" as long as the value can be reconstructed exactly by a
    /// single scaling operation, i.e. the mantissa is exactly representable as a double and the net
    /// decimal exponent is covered by <see cref="exponentFactorMap"/>. Under those conditions the
    /// IEEE result is correctly rounded by definition, because both operands and the single
    /// multiply/divide are exact. Everything else (overly long mantissas, large exponents,
    /// subnormals, NaN/Infinity, truncated tails) is rejected and handled by the general path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseCompactFloating(ReadOnlySpan<byte> span, ref int pos, out double value)
    {
        value = 0;
        int p = pos;
        bool isNegative = span[p] == (byte)'-';
        if (isNegative) p++;

        ulong mantissa = 0;
        int digitCount = 0;
        while (p < span.Length)
        {
            uint digit = (uint)(span[p] - (byte)'0');
            if (digit > 9u) break;
            unchecked { mantissa = mantissa * 10 + digit; }
            digitCount++;
            p++;
        }

        if (digitCount == 0) return false;      // not a plain number (could be NaN/Infinity string)

        int fractionDigits = 0;
        if (p < span.Length && span[p] == (byte)'.')
        {
            p++;
            while (p < span.Length)
            {
                uint digit = (uint)(span[p] - (byte)'0');
                if (digit > 9u) break;
                unchecked { mantissa = mantissa * 10 + digit; }
                digitCount++;
                fractionDigits++;
                p++;
            }
        }

        if (p == span.Length) return false;     // element may be truncated by the buffer window
        if (digitCount > 19) return false;      // mantissa may have wrapped -> general path

        // Only an exactly representable mantissa keeps the single scaling step correctly rounded.
        const ulong maxExactMantissa = 1UL << 53;
        if (mantissa > maxExactMantissa) return false;

        int exponent = 0;
        byte next = span[p];
        if (next == (byte)'e' || next == (byte)'E')
        {
            p++;
            if (p == span.Length) return false;

            bool isExponentNegative = false;
            byte sign = span[p];
            if (sign == (byte)'-')
            {
                isExponentNegative = true;
                p++;
            }
            else if (sign == (byte)'+') p++;

            int exponentDigits = 0;
            while (p < span.Length)
            {
                uint digit = (uint)(span[p] - (byte)'0');
                if (digit > 9u) break;
                exponent = exponent * 10 + (int)digit;
                exponentDigits++;
                if (exponentDigits > 3) return false;   // far outside the exact-scaling range
                p++;
            }

            if (exponentDigits == 0) return false;
            if (p == span.Length) return false;         // exponent may be truncated by the window
            if (isExponentNegative) exponent = -exponent;
        }

        // Net decimal exponent still to apply after the digits were folded into the mantissa.
        int netExponent = exponent - fractionDigits;
        int maxFactor = exponentFactorMap.Length - 1;
        if (netExponent > maxFactor || netExponent < -maxFactor) return false;

        double parsed = mantissa;
        if (netExponent > 0) parsed *= exponentFactorMap[netExponent];
        else if (netExponent < 0) parsed /= exponentFactorMap[-netExponent];
        if (isNegative) parsed = -parsed;

        pos = p;
        value = parsed;
        return true;
    }
#endif

    struct SByteElementParser : INumberElementParser<sbyte>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out sbyte value)
        {
            bool ok = TryParseCompactSigned(span, ref pos, sbyte.MinValue, sbyte.MaxValue, out long v);
            value = (sbyte)v;
            return ok;
        }

        public static sbyte ReadGeneral(JsonDeserializer d) => d.ReadSbyteValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out sbyte value)
        {
            bool ok = TryParseCompactSigned(span, ref pos, sbyte.MinValue, sbyte.MaxValue, out long v);
            value = (sbyte)v;
            return ok;
        }
#endif
        public sbyte ReadGeneral(JsonDeserializer d) => d.ReadSbyteValue();
#endif
    }

    struct Int16ElementParser : INumberElementParser<short>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out short value)
        {
            bool ok = TryParseCompactSigned(span, ref pos, short.MinValue, short.MaxValue, out long v);
            value = (short)v;
            return ok;
        }

        public static short ReadGeneral(JsonDeserializer d) => d.ReadShortValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out short value)
        {
            bool ok = TryParseCompactSigned(span, ref pos, short.MinValue, short.MaxValue, out long v);
            value = (short)v;
            return ok;
        }
#endif
        public short ReadGeneral(JsonDeserializer d) => d.ReadShortValue();
#endif
    }

    struct Int32ElementParser : INumberElementParser<int>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out int value)
        {
            bool ok = TryParseCompactSigned(span, ref pos, int.MinValue, int.MaxValue, out long v);
            value = (int)v;
            return ok;
        }

        public static int ReadGeneral(JsonDeserializer d) => d.ReadIntValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out int value)
        {
            bool ok = TryParseCompactSigned(span, ref pos, int.MinValue, int.MaxValue, out long v);
            value = (int)v;
            return ok;
        }
#endif
        public int ReadGeneral(JsonDeserializer d) => d.ReadIntValue();
#endif
    }

    struct Int64ElementParser : INumberElementParser<long>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out long value)
            => TryParseCompactSigned(span, ref pos, long.MinValue, long.MaxValue, out value);

        public static long ReadGeneral(JsonDeserializer d) => d.ReadLongValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out long value)
            => TryParseCompactSigned(span, ref pos, long.MinValue, long.MaxValue, out value);
#endif
        public long ReadGeneral(JsonDeserializer d) => d.ReadLongValue();
#endif
    }

    struct ByteElementParser : INumberElementParser<byte>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out byte value)
        {
            bool ok = TryParseCompactUnsigned(span, ref pos, byte.MaxValue, out ulong v);
            value = (byte)v;
            return ok;
        }

        public static byte ReadGeneral(JsonDeserializer d) => d.ReadByteValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out byte value)
        {
            bool ok = TryParseCompactUnsigned(span, ref pos, byte.MaxValue, out ulong v);
            value = (byte)v;
            return ok;
        }
#endif
        public byte ReadGeneral(JsonDeserializer d) => d.ReadByteValue();
#endif
    }

    struct UInt16ElementParser : INumberElementParser<ushort>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out ushort value)
        {
            bool ok = TryParseCompactUnsigned(span, ref pos, ushort.MaxValue, out ulong v);
            value = (ushort)v;
            return ok;
        }

        public static ushort ReadGeneral(JsonDeserializer d) => d.ReadUshortValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out ushort value)
        {
            bool ok = TryParseCompactUnsigned(span, ref pos, ushort.MaxValue, out ulong v);
            value = (ushort)v;
            return ok;
        }
#endif
        public ushort ReadGeneral(JsonDeserializer d) => d.ReadUshortValue();
#endif
    }

    struct UInt32ElementParser : INumberElementParser<uint>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out uint value)
        {
            bool ok = TryParseCompactUnsigned(span, ref pos, uint.MaxValue, out ulong v);
            value = (uint)v;
            return ok;
        }

        public static uint ReadGeneral(JsonDeserializer d) => d.ReadUintValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out uint value)
        {
            bool ok = TryParseCompactUnsigned(span, ref pos, uint.MaxValue, out ulong v);
            value = (uint)v;
            return ok;
        }
#endif
        public uint ReadGeneral(JsonDeserializer d) => d.ReadUintValue();
#endif
    }

    struct UInt64ElementParser : INumberElementParser<ulong>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out ulong value)
            => TryParseCompactUnsigned(span, ref pos, ulong.MaxValue, out value);

        public static ulong ReadGeneral(JsonDeserializer d) => d.ReadUlongValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out ulong value)
            => TryParseCompactUnsigned(span, ref pos, ulong.MaxValue, out value);
#endif
        public ulong ReadGeneral(JsonDeserializer d) => d.ReadUlongValue();
#endif
    }

    struct DoubleElementParser : INumberElementParser<double>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out double value)
            => TryParseCompactFloating(span, ref pos, out value);

        public static double ReadGeneral(JsonDeserializer d) => d.ReadDoubleValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out double value)
            => TryParseCompactFloating(span, ref pos, out value);
#endif
        public double ReadGeneral(JsonDeserializer d) => d.ReadDoubleValue();
#endif
    }

    struct SingleElementParser : INumberElementParser<float>
    {
#if NET5_0_OR_GREATER
        public static bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out float value)
        {
            bool ok = TryParseCompactFloating(span, ref pos, out double v);
            value = (float)v;
            return ok;
        }

        public static float ReadGeneral(JsonDeserializer d) => d.ReadFloatValue();
#else
#if !NETSTANDARD2_0
        public bool TryParseCompact(ReadOnlySpan<byte> span, ref int pos, out float value)
        {
            bool ok = TryParseCompactFloating(span, ref pos, out double v);
            value = (float)v;
            return ok;
        }
#endif
        public float ReadGeneral(JsonDeserializer d) => d.ReadFloatValue();
#endif
    }

    /// <summary>
    /// Holds the reusable scratch buffer of a number-array reader. Each created type reader owns its
    /// own holder, so no shared per-type fields are needed.
    /// </summary>
    sealed class NumberScratch<T> where T : struct
    {
        public T[] buffer;
    }

    /// <summary>
    /// Reads a JSON number array into a reusable scratch buffer. This is the single shared loop for
    /// every numeric element type and every target container: it avoids the generic array reader's
    /// per-element delegate indirection and the pooled <see cref="List{T}"/> intermediate.
    /// </summary>
    private void ReadNumbersIntoScratch<T, TParser>(ref T[] scratch, out int count)
        where T : struct
        where TParser : struct, INumberElementParser<T>
    {
        byte start = buffer.CurrentByte;
        if (start != '[') throw new Exception($"Failed reading Array: Array didn't start with '[', but with '{(char)start}'");
        if (!buffer.TryNextByte()) throw new Exception("Failed reading Array: Unexpected end of input");

        T[] target = scratch ?? new T[128];
        count = 0;
        while (true)
        {
            byte b = SkipWhiteSpaces();
            if (b == ']') break;

#if !NETSTANDARD2_0
            // Bulk fast path: consume as many compact elements as are fully contained in the current
            // buffer window, using a single span acquisition instead of paying
            // SkipWhiteSpaces/GetRemainingSpan per element.
            if (BulkReadElements<T, TParser>(ref target, ref count, out bool reachedArrayEnd))
            {
                if (reachedArrayEnd) break;
                continue;
            }
#endif

#if NET5_0_OR_GREATER
            T value = TParser.ReadGeneral(this);
#else
            T value = default(TParser).ReadGeneral(this);
#endif
            if (count == target.Length) Array.Resize(ref target, target.Length * 2);
            target[count++] = value;

            b = SkipWhiteSpaces();
            if (b == ',') buffer.TryNextByte();
            else if (b != ']') throw new Exception($"Failed reading Array: Unexpected character encountered '{(char)b}'");
        }
        buffer.TryNextByte();
        // No try/finally: the method never re-enters itself, and on a parsing exception the
        // deserializer state is invalid anyway, so preserving the scratch buffer is pointless.
        scratch = target;
    }

    /// <summary>
    /// Reads a JSON number array into an exactly sized array.
    /// </summary>
    private T[] ReadArrayFromNumbers<T, TParser>(ref T[] scratch)
        where T : struct
        where TParser : struct, INumberElementParser<T>
    {
        ReadNumbersIntoScratch<T, TParser>(ref scratch, out int count);
        if (count == 0) return Array.Empty<T>();
        T[] result = new T[count];
        Array.Copy(scratch, 0, result, 0, count);
        return result;
    }

    /// <summary>
    /// Reads a JSON number array into an exactly sized <see cref="List{T}"/>, so the same bulk
    /// parsing is available for list-shaped targets and not only for arrays.
    /// </summary>
    private List<T> ReadListFromNumbers<T, TParser>(ref T[] scratch)
        where T : struct
        where TParser : struct, INumberElementParser<T>
    {
        ReadNumbersIntoScratch<T, TParser>(ref scratch, out int count);
        var result = new List<T>(count);
        T[] source = scratch;
        for (int i = 0; i < count; i++) result.Add(source[i]);
        return result;
    }

    private T[] ReadArrayFromNumbers<T, TParser>(NumberScratch<T> scratch)
        where T : struct
        where TParser : struct, INumberElementParser<T>
        => ReadArrayFromNumbers<T, TParser>(ref scratch.buffer);

    private List<T> ReadListFromNumbers<T, TParser>(NumberScratch<T> scratch)
        where T : struct
        where TParser : struct, INumberElementParser<T>
        => ReadListFromNumbers<T, TParser>(ref scratch.buffer);

#if !NETSTANDARD2_0
    /// <summary>
    /// Consumes as many complete numeric elements as are fully contained in the currently buffered
    /// window, using one span acquisition for all of them. Returns false if nothing could be
    /// consumed safely, in which case the caller must fall back to the general per-element path.
    /// </summary>
    private bool BulkReadElements<T, TParser>(ref T[] scratch, ref int count, out bool reachedArrayEnd)
        where T : struct
        where TParser : struct, INumberElementParser<T>
    {
        reachedArrayEnd = false;
        ReadOnlySpan<byte> span = buffer.GetRemainingSpan();
        if (span.Length < 2) return false;

        int pos = 0;
        T[] target = scratch;
        int written = count;

        // Only positions guaranteed to be a valid, fully parsed element boundary inside the current
        // window are committed, so a truncated tail always falls back to the general path.
        int commitPos = -1;
        int commitWritten = count;
        bool commitIsArrayEnd = false;

        while (true)
        {
#if NET5_0_OR_GREATER
            if (!TParser.TryParseCompact(span, ref pos, out T value)) break;
#else
            if (!default(TParser).TryParseCompact(span, ref pos, out T value)) break;
#endif

            // optional whitespace before the separator
            while (pos < span.Length && IsWhiteSpace(span[pos])) pos++;
            if (pos == span.Length) break;

            byte sep = span[pos];
            if (sep != (byte)',' && sep != (byte)']') break;

            if (written == target.Length) Array.Resize(ref target, target.Length * 2);
            target[written++] = value;

            if (sep == (byte)']')
            {
                commitPos = pos;                        // leave position on ']' for the caller
                commitWritten = written;
                commitIsArrayEnd = true;
                break;
            }

            pos++;                                      // consume ','
            while (pos < span.Length && IsWhiteSpace(span[pos])) pos++;
            if (pos == span.Length) break;

            commitPos = pos;                            // start of the next element
            commitWritten = written;
        }

        if (commitPos < 0) return false;

        scratch = target;
        count = commitWritten;
        reachedArrayEnd = commitIsArrayEnd;
        buffer.BufferPos += commitPos;
        return true;
    }
#endif
}
