using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Serialization;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Text;
using static FeatureLoom.Serialization.JsonSerializer;

#if !NETSTANDARD2_0
using System.Buffers.Text;
using System.Buffers;
#endif

namespace FeatureLoom.Serialization;

public sealed partial class JsonDeserializer
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte[] ReadByteArray(CachedTypeReader byteArrayReader, bool useFastNumberArrayReader = false)
    {
        //SkipWhiteSpaces(); //Whitespaces are already skipped by the caller, so we can expect to be exactly at the start of the value
        byte b = buffer.CurrentByte;
        if (b == '"')
        {
            var base64Uft8 = ReadStringBytes();
#if NETSTANDARD2_0
            string base64String = Utf8Converter.DecodeUtf8ToString(base64Uft8, stringBuilder);
            return Convert.FromBase64String(base64String);
#else
            ReadOnlySpan<byte> utf8Base64 = base64Uft8.AsArraySegment.AsSpan();
            int encodedLength = utf8Base64.Length;
            if (encodedLength == 0) return Array.Empty<byte>();

            byte[] bytes;
            if ((encodedLength & 3) == 0)
            {
                // Well-formed base64: the exact decoded length is known upfront,
                // so a single right-sized array can be allocated without a resize/copy afterwards.
                int padding = 0;
                if (utf8Base64[encodedLength - 1] == (byte)'=')
                {
                    padding = utf8Base64[encodedLength - 2] == (byte)'=' ? 2 : 1;
                }
                bytes = new byte[(encodedLength / 4) * 3 - padding];
            }
            else
            {
                bytes = new byte[Base64.GetMaxDecodedFromUtf8Length(encodedLength)];
            }

            OperationStatus status = Base64.DecodeFromUtf8(utf8Base64, bytes, out int bytesConsumed, out int bytesWritten);
            if (status != OperationStatus.Done) throw new FormatException($"Invalid Base64 sequence (status = {status}).");
            if (bytesWritten != bytes.Length) Array.Resize(ref bytes, bytesWritten);
            return bytes;
#endif
        }
        else if (b == '[')
        {
            if (useFastNumberArrayReader) return ReadByteArrayFromNumbers();
            return byteArrayReader.ReadValue_NoCheck<byte[]>();
        }

        throw new Exception("Expected byte array, but didn't got an array nor an Base64 string");
    }

    private byte[] numberArrayScratch;

    /// <summary>
    /// Reads a JSON number array of <see cref="byte"/> values using the shared integer bulk reader.
    /// </summary>
    private byte[] ReadByteArrayFromNumbers()
    {
        return ReadArrayFromNumbers<byte, ByteElementParser>(ref numberArrayScratch);
    }


    private long[] longArrayScratch;

    /// <summary>
    /// Reads a JSON number array of <see cref="long"/> values using the shared integer bulk reader.
    /// </summary>
    private long[] ReadLongArrayFromNumbers()
    {
        return ReadArrayFromNumbers<long, Int64ElementParser>(ref longArrayScratch);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object ReadUnknownValue()
    {
        var valueType = Lookup(map_TypeStart, buffer.CurrentByte);
        if (valueType == TypeResult.Whitespace)
        {
            byte b = SkipWhiteSpaces();
            valueType = Lookup(map_TypeStart, b);
        }

        switch (valueType)
        {
            case TypeResult.String: return ReadStringValue();
            case TypeResult.Object: return ReadObjectValueAsDictionary();
            case TypeResult.Bool: return ReadBoolValue();
            case TypeResult.Null: return ReadNullValue();
            case TypeResult.Array: return ReadArrayValue();
            case TypeResult.Number: return ReadNumberValueAsObject();
            default: throw new Exception("Invalid character for determining value");
        }
    }

    CachedTypeReader cachedStringObjectDictionaryReader = null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Dictionary<string, object> ReadObjectValueAsDictionary()
    {
        if (cachedStringObjectDictionaryReader == null) cachedStringObjectDictionaryReader = CreateCachedTypeReader(typeof(Dictionary<string, object>));
        return cachedStringObjectDictionaryReader.ReadValue_NoCheck<Dictionary<string, object>>();
    }

    CollectionCaster collectionCaster = new CollectionCaster();
    CachedTypeReader cachedObjectArrayReader = null;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object ReadArrayValue()
    {
        if (cachedObjectArrayReader == null) cachedObjectArrayReader = CreateCachedTypeReader(typeof(object[]));
        var objectsArray = cachedObjectArrayReader.ReadValue_NoCheck<object[]>();
        if (!settings.castObjectArrayToCommonTypeArray || objectsArray.Length == 0) return objectsArray;

        var castedArray = collectionCaster.CastToCommonTypeArray(objectsArray, out _);
        return castedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadObjectValue<T>(out T value, ByteSegment itemName)
    {
        value = default;
        try
        {
            var typeReader = GetCachedTypeReader(typeof(T));
            if (itemName.IsEmptyOrInvalid) value = typeReader.ReadValue_CheckProposed<T>();
            else value = typeReader.ReadFieldValue<T>(itemName);
        }
        catch
        {
            return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadArrayValue<T>(out T value, ByteSegment itemName) where T : IEnumerable
    {
        value = default;
        try
        {
            var typeReader = GetCachedTypeReader(typeof(T));
            if (itemName.IsEmptyOrInvalid) value = typeReader.ReadValue_CheckProposed<T>();
            else value = typeReader.ReadFieldValue<T>(itemName);
        }
        catch
        {
            return false;
        }
        return true;
    }

    readonly Utf8StringCache stringCache;
    readonly bool useStringCache;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ReadStringValue()
    {
        var stringBytes = ReadStringBytes();
        string result;

        if (useStringCache) result = stringCache.GetOrCreate(stringBytes, stringBuilder);
        else result = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);

        stringBuilder.Clear();
        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ReadStringValueOrNull()
    {
        if (TryReadNullValue()) return null;

        var stringBytes = ReadStringBytes();
        string result;

        if (useStringCache) result = stringCache.GetOrCreate(stringBytes, stringBuilder);
        else result = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);

        stringBuilder.Clear();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ReadStringValue_WithoutStringCache()
    {
        var stringBytes = ReadStringBytes();
        string result;

        result = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);

        stringBuilder.Clear();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ReadStringValue_WithStringCache()
    {
        var stringBytes = ReadStringBytes();
        string result;

        result = stringCache.GetOrCreate(stringBytes, stringBuilder);

        stringBuilder.Clear();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ReadStringValueOrNull_WithoutStringCache()
    {
        if (TryReadNullValue()) return null;

        var stringBytes = ReadStringBytes();
        string result;

        result = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);

        stringBuilder.Clear();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ReadStringValueOrNull_WithStringCache()
    {
        if (TryReadNullValue()) return null;
        var stringBytes = ReadStringBytes();
        string result;
        result = stringCache.GetOrCreate(stringBytes, stringBuilder);
        stringBuilder.Clear();
        return result;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadStringValueOrNull(out string value)
    {
        value = null;
        if (!TryReadStringBytesOrNull(out var stringBytes, out var isNull)) return false;
        if (isNull)
        {
            value = null;
            return true;
        }
        value = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);
        stringBuilder.Clear();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char ReadCharValue()
    {
        var stringBytes = ReadStringBytes();
        Utf8Converter.DecodeUtf8ToStringBuilder(stringBytes, stringBuilder);
        if (stringBuilder.Length == 0) throw new Exception("string for reading char is empty");
        char c = stringBuilder[0];
        stringBuilder.Clear();
        return c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private char? ReadNullableCharValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadCharValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DateTime ReadDateTimeValue()
    {
        var stringBytes = ReadStringBytes();

        if (stringBytes.Count == 0 && !settings.strict)
        {
            return default;
        }

        // Fast path: the overwhelming majority of JSON date-times are plain ASCII ISO-8601 of a
        // fixed layout. Parsing those directly from the UTF-8 bytes avoids both the UTF-8 decode
        // and System.DateTimeParse, which is a format-flexible state machine that repeatedly
        // consults DateTimeFormatInfo (separators, designators, era data) that cannot apply here.
        if (TryParseIso8601DateTime(stringBytes, out DateTime fastResult)) return fastResult;

        DateTime result;
#if NET5_0_OR_GREATER
        Utf8Converter.DecodeUtf8ToStringBuilder(stringBytes, stringBuilder);
        ReadOnlySpan<char> span = new ReadOnlySpan<char>();
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            if (span.IsEmpty) span = chunk.Span; // First chunk, that is good
            else
            {
                // second chunk is bad and we need to reset to fall back to copying (This is very unlikely for a DateTime string)
                span = new ReadOnlySpan<char>();
                break;
            }
        }
        if (span.IsEmpty)
        {
            var chars = charSlicedBuffer.GetSlice(stringBuilder.Length);
            stringBuilder.CopyTo(0, chars.Array, chars.Offset, stringBuilder.Length);
            span = chars;
            charSlicedBuffer.Reset(true); // We reset early, though the slice/span was not used yet. That works because the underlying array is not erased.
        }
        result = DateTime.Parse(span, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
#elif NETSTANDARD2_1_OR_GREATER
        ReadOnlySpan<char> span = Utf8Converter.DecodeUtf8ToSpanOfChars(stringBytes, stringBuilder, charSlicedBuffer);            
        result = DateTime.Parse(span, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);            
        charSlicedBuffer.Reset(true);
#else
        string str = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);            
        result = DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);                        
#endif
        stringBuilder.Clear();
        return result;
    }

    /// <summary>
    /// Parses the strict ISO-8601 layouts that JSON date-times almost always use, directly from
    /// the UTF-8 bytes:
    /// <c>yyyy-MM-dd</c>, optionally followed by <c>THH:mm</c>, <c>:ss</c>, a fractional part and
    /// a <c>Z</c> / <c>+HH:mm</c> / <c>-HH:mm</c> offset.
    /// Returns false for anything that deviates even slightly, so the caller falls back to the
    /// culture-aware <see cref="DateTime.Parse(string, IFormatProvider, DateTimeStyles)"/> and
    /// behaviour is preserved for all other inputs.
    /// </summary>
    private static bool TryParseIso8601DateTime(ByteSegment bytes, out DateTime result)
    {
        result = default;
        if (!TryParseIso8601Core(bytes, out long ticks, out DateTimeKind kind, out int offsetMinutes)) return false;

        if (kind == DateTimeKind.Local)
        {
            // Matches DateTimeStyles.RoundtripKind: an explicit offset is normalised to UTC and
            // then converted to local time.
            long utcTicks = ticks - offsetMinutes * TimeSpan.TicksPerMinute;
            if ((ulong)utcTicks > MaxTicks) return false;
            result = new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
        }
        else
        {
            result = new DateTime(ticks, kind);
        }

        return true;
    }

    /// <summary>
    /// Shared scanner for the strict ISO-8601 layouts. Produces the wall clock tick count together
    /// with the kind implied by the trailing designator: <see cref="DateTimeKind.Utc"/> for
    /// <c>Z</c>, <see cref="DateTimeKind.Local"/> plus <paramref name="offsetMinutes"/> for an
    /// explicit numeric offset and <see cref="DateTimeKind.Unspecified"/> when no designator is
    /// present.
    /// </summary>
    private static bool TryParseIso8601Core(ByteSegment bytes, out long resultTicks, out DateTimeKind resultKind, out int resultOffsetMinutes)
    {
        resultTicks = 0;
        resultKind = DateTimeKind.Unspecified;
        resultOffsetMinutes = 0;
        int len = bytes.Count;
        // "yyyy-MM-dd" is the shortest accepted form, the longest handled here is
        // "yyyy-MM-ddTHH:mm:ss.fffffff+HH:mm".
        if (len < 10 || len > 33) return false;

        var seg = bytes.AsArraySegment;
        byte[] a = seg.Array;
        int o = seg.Offset;

        if (a[o + 4] != (byte)'-' || a[o + 7] != (byte)'-') return false;

        if (!TryRead4Digits(a, o, out int year)) return false;
        if (!TryRead2Digits(a, o + 5, out int month)) return false;
        if (!TryRead2Digits(a, o + 8, out int day)) return false;

        int hour = 0, minute = 0, second = 0;
        long subTicks = 0;
        int pos = 10;

        if (pos < len)
        {
            byte sep = a[o + pos];
            if (sep != (byte)'T' && sep != (byte)' ') return false;
            // A time part requires at least "HH:mm".
            if (pos + 6 > len) return false;
            if (a[o + pos + 3] != (byte)':') return false;
            if (!TryRead2Digits(a, o + pos + 1, out hour)) return false;
            if (!TryRead2Digits(a, o + pos + 4, out minute)) return false;
            pos += 6;

            if (pos < len && a[o + pos] == (byte)':')
            {
                if (pos + 3 > len) return false;
                if (!TryRead2Digits(a, o + pos + 1, out second)) return false;
                pos += 3;

                if (pos < len && a[o + pos] == (byte)'.')
                {
                    pos++;
                    int fracStart = pos;
                    // Accumulate up to 7 fractional digits (100ns tick resolution); any further
                    // digits are valid ISO-8601 but cannot be represented, so they are skipped.
                    while (pos < len)
                    {
                        byte d = a[o + pos];
                        if (d < (byte)'0' || d > (byte)'9') break;
                        if (pos - fracStart < 7) subTicks = subTicks * 10 + (d - (byte)'0');
                        pos++;
                    }
                    int digits = pos - fracStart;
                    if (digits == 0) return false;
                    for (int i = digits; i < 7; i++) subTicks *= 10;
                }
            }
        }

        DateTimeKind kind = DateTimeKind.Unspecified;
        int offsetMinutes = 0;
        if (pos < len)
        {
            byte c = a[o + pos];
            if (c == (byte)'Z' || c == (byte)'z')
            {
                if (pos + 1 != len) return false;
                kind = DateTimeKind.Utc;
                pos = len;
            }
            else if (c == (byte)'+' || c == (byte)'-')
            {
                // Only "+HH:mm" / "-HH:mm" is handled; other offset spellings fall back.
                if (pos + 6 != len || a[o + pos + 3] != (byte)':') return false;
                if (!TryRead2Digits(a, o + pos + 1, out int offHour)) return false;
                if (!TryRead2Digits(a, o + pos + 4, out int offMinute)) return false;
                if (offHour > 14 || offMinute > 59) return false;
                offsetMinutes = offHour * 60 + offMinute;
                if (c == (byte)'-') offsetMinutes = -offsetMinutes;
                kind = DateTimeKind.Local;
                pos = len;
            }
            else return false;
        }
        if (pos != len) return false;

        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1) return false;
        // A leap second (or 24:00) is legal ISO-8601 but not representable, so fall back.
        if (hour > 23 || minute > 59 || second > 59) return false;

        // Days in month without a calendar lookup. February is corrected via the leap year rule.
        int maxDay = DaysInMonthTable[month];
        if (month == 2 && (year & 3) == 0 && (year % 100 != 0 || year % 400 == 0)) maxDay = 29;
        if (day > maxDay) return false;

        // Compute the tick count directly instead of using the DateTime(y,m,d,...) constructor,
        // which re-derives the day number through the calendar and throws on invalid input. All
        // components are already validated above, so the result is guaranteed to be in range and
        // no exception handling is needed on this path.
        int era = (month <= 2 ? year - 1 : year);
        long days = era * 365L + era / 4 - era / 100 + era / 400 + DayOfYearTable[month] + day - 1;
        long ticks = (days - DaysToEpoch) * TimeSpan.TicksPerDay
                   + hour * TimeSpan.TicksPerHour
                   + minute * TimeSpan.TicksPerMinute
                   + second * TimeSpan.TicksPerSecond
                   + subTicks;

        resultTicks = ticks;
        resultKind = kind;
        resultOffsetMinutes = offsetMinutes;
        return true;
    }

    /// <summary>
    /// Parses the strict ISO-8601 layouts directly from the UTF-8 bytes into a
    /// <see cref="DateTimeOffset"/>, avoiding both the UTF-8 decode and the culture-aware parser.
    /// Returns false for anything that deviates, so the caller falls back and behaviour is
    /// preserved for all other inputs.
    /// </summary>
    private static bool TryParseIso8601DateTimeOffset(ByteSegment bytes, out DateTimeOffset result)
    {
        result = default;
        if (!TryParseIso8601Core(bytes, out long ticks, out DateTimeKind kind, out int offsetMinutes)) return false;

        if (kind == DateTimeKind.Unspecified)
        {
            // Without a designator the local offset of that very instant applies. Determining it
            // needs the time zone rules, so only the safely convertible range is handled here.
            if (ticks < TimeSpan.TicksPerDay || (ulong)(ticks + TimeSpan.TicksPerDay) > MaxTicks) return false;
            result = new DateTimeOffset(new DateTime(ticks, DateTimeKind.Unspecified));
            return true;
        }

        if (kind == DateTimeKind.Utc) offsetMinutes = 0;

        long utcTicks = ticks - offsetMinutes * TimeSpan.TicksPerMinute;
        if ((ulong)utcTicks > MaxTicks) return false;
        result = new DateTimeOffset(ticks, new TimeSpan(offsetMinutes * TimeSpan.TicksPerMinute));
        return true;
    }

    /// <summary>
    /// Parses the invariant <c>[-][d.]hh:mm[:ss[.fffffff]]</c> layouts (and the bare day count)
    /// directly from the UTF-8 bytes. Anything else falls back to <see cref="TimeSpan.Parse(string, IFormatProvider)"/>,
    /// whose format-flexible matching dominates the deserialization cost.
    /// </summary>
    private static bool TryParseTimeSpan(ByteSegment bytes, out TimeSpan result)
    {
        result = default;
        int len = bytes.Count;
        // The longest accepted form is "-10675199.02:48:05.4775807".
        if (len < 1 || len > 26) return false;

        var seg = bytes.AsArraySegment;
        byte[] a = seg.Array;
        int o = seg.Offset;

        int pos = 0;
        bool negative = a[o] == (byte)'-';
        if (negative) pos = 1;

        if (!TryReadDigitRun(a, o, ref pos, len, 8, out long first)) return false;

        long days = 0, hours = 0, minutes = 0, seconds = 0, subTicks = 0;

        if (pos == len)
        {
            days = first; // A lone number is a day count.
        }
        else
        {
            if (a[o + pos] == (byte)'.')
            {
                days = first;
                pos++;
                if (!TryReadDigitRun(a, o, ref pos, len, 2, out hours)) return false;
                if (pos >= len || a[o + pos] != (byte)':') return false;
            }
            else if (a[o + pos] == (byte)':')
            {
                hours = first;
            }
            else return false;

            pos++; // consume the ':' before the minutes
            if (!TryReadDigitRun(a, o, ref pos, len, 2, out minutes)) return false;

            if (pos < len && a[o + pos] == (byte)':')
            {
                pos++;
                if (!TryReadDigitRun(a, o, ref pos, len, 2, out seconds)) return false;

                if (pos < len && a[o + pos] == (byte)'.')
                {
                    pos++;
                    int fracStart = pos;
                    while (pos < len)
                    {
                        byte d = a[o + pos];
                        if (d < (byte)'0' || d > (byte)'9') break;
                        subTicks = subTicks * 10 + (d - (byte)'0');
                        pos++;
                    }
                    int digits = pos - fracStart;
                    // More than tick resolution is rejected, exactly like the framework parser.
                    if (digits == 0 || digits > 7) return false;
                    for (int i = digits; i < 7; i++) subTicks *= 10;
                }
            }
        }

        if (pos != len) return false;
        if (hours > 23 || minutes > 59 || seconds > 59) return false;
        if (days > 10675199) return false;

        long ticks = days * TimeSpan.TicksPerDay
                   + hours * TimeSpan.TicksPerHour
                   + minutes * TimeSpan.TicksPerMinute
                   + seconds * TimeSpan.TicksPerSecond
                   + subTicks;
        if (ticks < 0) return false;

        result = new TimeSpan(negative ? -ticks : ticks);
        return true;
    }

    /// <summary>
    /// Reads a run of at least one and at most <paramref name="maxDigits"/> ASCII digits.
    /// </summary>
    private static bool TryReadDigitRun(byte[] a, int o, ref int pos, int len, int maxDigits, out long value)
    {
        value = 0;
        int start = pos;
        while (pos < len)
        {
            byte d = a[o + pos];
            if (d < (byte)'0' || d > (byte)'9') break;
            value = value * 10 + (d - (byte)'0');
            pos++;
            if (pos - start > maxDigits) return false;
        }
        return pos > start;
    }

    // Days from the March-based day number
    // maps to tick 0. January and February are treated as months 13/14 of the previous year, which
    // is why 0001-01-01 starts at day 306 in this numbering.
    private const long DaysToEpoch = 306;
    private const ulong MaxTicks = 3155378975999999999UL;

    private static readonly int[] DaysInMonthTable = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
    // Cumulative days before each month for a March-based year, matching the era shift above.
    private static readonly int[] DayOfYearTable = { 0, 306, 337, 0, 31, 61, 92, 122, 153, 184, 214, 245, 275 };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryRead2Digits(byte[] a, int i, out int value)
    {
        // Both digits are read as one 16 bit load. A lane is a digit exactly when its high nibble
        // is 3 and adding 6 does not carry into that nibble, so two masked tests cover both lanes.
        ushort pair = Unsafe.ReadUnaligned<ushort>(ref a[i]);
        bool valid = ((pair & 0xF0F0) == 0x3030) && (((pair + 0x0606) & 0xF0F0) == 0x3030);
        uint d = (uint)(pair - 0x3030);
        value = (int)(((d & 0xFF) * 10) + ((d >> 8) & 0xFF));
        return valid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryRead4Digits(byte[] a, int i, out int value)
    {
        // All four digits are read as one 32 bit load. Each lane must be in '0'..'9', which is
        // checked with two masked comparisons instead of four separate range tests.
        uint quad = Unsafe.ReadUnaligned<uint>(ref a[i]);
        bool valid = ((quad & 0xF0F0F0F0) == 0x30303030) && (((quad + 0x06060606) & 0xF0F0F0F0) == 0x30303030);
        uint d = quad - 0x30303030;
        value = (int)(((d & 0xFF) * 1000) + (((d >> 8) & 0xFF) * 100) + (((d >> 16) & 0xFF) * 10) + ((d >> 24) & 0xFF));
        return valid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DateTime? ReadNullableDateTimeValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadDateTimeValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DateTimeOffset ReadDateTimeOffsetValue()
    {
        var stringBytes = ReadStringBytes();

        if (stringBytes.Count == 0 && !settings.strict)
        {
            return default;
        }

        if (TryParseIso8601DateTimeOffset(stringBytes, out DateTimeOffset fastResult)) return fastResult;

        DateTimeOffset result;
#if NET5_0_OR_GREATER
        Utf8Converter.DecodeUtf8ToStringBuilder(stringBytes, stringBuilder);
        ReadOnlySpan<char> span = new ReadOnlySpan<char>();
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            if (span.IsEmpty) span = chunk.Span; // First chunk, that is good
            else
            {
                // second chunk is bad and we need to reset to fall back to copying (This is very unlikely for a DateTimeOffset string)
                span = new ReadOnlySpan<char>();
                break;
            }
        }
        if (span.IsEmpty)
        {
            var chars = charSlicedBuffer.GetSlice(stringBuilder.Length);
            stringBuilder.CopyTo(0, chars.Array, chars.Offset, stringBuilder.Length);
            span = chars;
            charSlicedBuffer.Reset(true); // We reset early, though the slice/span was not used yet. That works because the underlying array is not erased.
        }
        result = DateTimeOffset.Parse(span, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
#elif NETSTANDARD2_1_OR_GREATER
        ReadOnlySpan<char> span = Utf8Converter.DecodeUtf8ToSpanOfChars(stringBytes, stringBuilder, charSlicedBuffer);
        result = DateTimeOffset.Parse(span, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        charSlicedBuffer.Reset(true);
#else
        string str = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);
        result = DateTimeOffset.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
#endif
        stringBuilder.Clear();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private DateTimeOffset? ReadNullableDateTimeOffsetValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadDateTimeOffsetValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TimeSpan ReadTimeSpanValue()
    {
        var stringBytes = ReadStringBytes();

        if (stringBytes.Count == 0 && !settings.strict)
        {
            return default;
        }

        if (TryParseTimeSpan(stringBytes, out TimeSpan fastResult)) return fastResult;

        TimeSpan result;
#if NET5_0_OR_GREATER
        Utf8Converter.DecodeUtf8ToStringBuilder(stringBytes, stringBuilder);
        ReadOnlySpan<char> span = new ReadOnlySpan<char>();
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            if (span.IsEmpty) span = chunk.Span; // First chunk, that is good
            else
            {
                // second chunk is bad and we need to reset to fall back to copying (This is very unlikely for a TimeSpan string)
                span = new ReadOnlySpan<char>();
                break;
            }
        }
        if (span.IsEmpty)
        {
            var chars = charSlicedBuffer.GetSlice(stringBuilder.Length);
            stringBuilder.CopyTo(0, chars.Array, chars.Offset, stringBuilder.Length);
            span = chars;
            charSlicedBuffer.Reset(true); // We reset early, though the slice/span was not used yet. That works because the underlying array is not erased.
        }
        result = TimeSpan.Parse(span, CultureInfo.InvariantCulture);
#elif NETSTANDARD2_1_OR_GREATER
        ReadOnlySpan<char> span = Utf8Converter.DecodeUtf8ToSpanOfChars(stringBytes, stringBuilder, charSlicedBuffer);            
        result = TimeSpan.Parse(span, CultureInfo.InvariantCulture);            
        charSlicedBuffer.Reset(true);
#else
        string str = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);
        result = TimeSpan.Parse(str, CultureInfo.InvariantCulture);
#endif
        stringBuilder.Clear();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TimeSpan? ReadNullableTimeSpanValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadTimeSpanValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Guid ReadGuidValue()
    {
        var stringBytes = ReadStringBytes();

        // A GUID is plain ASCII hex in a fixed layout, so the canonical forms can be decoded
        // straight from the UTF-8 bytes without materialising an intermediate string.
        if (TryParseGuidFromUtf8(stringBytes, out Guid fastResult)) return fastResult;

        Guid result;
#if NET5_0_OR_GREATER
        Utf8Converter.DecodeUtf8ToStringBuilder(stringBytes, stringBuilder);
        ReadOnlySpan<char> span = new ReadOnlySpan<char>();
        foreach (ReadOnlyMemory<char> chunk in stringBuilder.GetChunks())
        {
            if (span.IsEmpty) span = chunk.Span; // First chunk, that is good
            else
            {
                // second chunk is bad and we need to reset to fall back to copying (This is very unlikely for a Guid string)
                span = new ReadOnlySpan<char>();
                break;
            }
        }
        if (span.IsEmpty)
        {
            var chars = charSlicedBuffer.GetSlice(stringBuilder.Length);
            stringBuilder.CopyTo(0, chars.Array, chars.Offset, stringBuilder.Length);
            span = chars;
            charSlicedBuffer.Reset(true); // We reset early, though the slice/span was not used yet. That works because the underlying array is not erased.
        }
        result = Guid.Parse(span);
#elif NETSTANDARD2_1_OR_GREATER
        ReadOnlySpan<char> span = Utf8Converter.DecodeUtf8ToSpanOfChars(stringBytes, stringBuilder, charSlicedBuffer);            
        result = Guid.Parse(span);            
        charSlicedBuffer.Reset(true);
#else
        string str = Utf8Converter.DecodeUtf8ToString(stringBytes, stringBuilder);            
        result = Guid.Parse(str);                        
#endif
        stringBuilder.Clear();
        return result;
    }

    /// <summary>
    /// Decodes the two canonical GUID spellings directly from UTF-8 bytes: the hyphenated
    /// "D" form (36 bytes, 8-4-4-4-12) and the compact "N" form (32 bytes). Anything else
    /// (braces, parentheses, non-ASCII) returns false so the caller can use the general parser.
    /// </summary>
    private static bool TryParseGuidFromUtf8(ByteSegment stringBytes, out Guid result)
    {
        result = default;
        if (!stringBytes.IsValid) return false;

        var segment = stringBytes.AsArraySegment;
        byte[] array = segment.Array;
        int offset = segment.Offset;
        int count = segment.Count;

        if (count == 36)
        {
            if (array[offset + 8] != (byte)'-' || array[offset + 13] != (byte)'-' ||
                array[offset + 18] != (byte)'-' || array[offset + 23] != (byte)'-') return false;
        }
        else if (count != 32) return false;

        int p0 = offset;
        int p1 = offset + (count == 36 ? 9 : 8);
        int p2 = offset + (count == 36 ? 14 : 12);
        int p3 = offset + (count == 36 ? 19 : 16);
        int p4 = offset + (count == 36 ? 24 : 20);

        // Decode straight into the Guid fields, so no intermediate buffer is needed and the
        // result is independent of machine endianness. Invalid digits are accumulated instead
        // of branched on, so a bad character only costs a single check at the end.
        uint bad = 0;
        uint a = ReadHex8(array, p0, ref bad);
        uint b = ReadHex4(array, p1, ref bad);
        uint c = ReadHex4(array, p2, ref bad);
        uint d = ReadHex4(array, p3, ref bad);
        uint e = ReadHex8(array, p4, ref bad);
        uint f = ReadHex4(array, p4 + 8, ref bad);
        if (bad > 0xF) return false;

        result = new Guid((int)a, (short)b, (short)c,
            (byte)(d >> 8), (byte)d,
            (byte)(e >> 24), (byte)(e >> 16), (byte)(e >> 8), (byte)e,
            (byte)(f >> 8), (byte)f);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadHex4(byte[] array, int pos, ref uint bad)
    {
        uint d0 = hexLookup[array[pos]];
        uint d1 = hexLookup[array[pos + 1]];
        uint d2 = hexLookup[array[pos + 2]];
        uint d3 = hexLookup[array[pos + 3]];
        bad |= d0 | d1 | d2 | d3;
        return (d0 << 12) | (d1 << 8) | (d2 << 4) | d3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadHex8(byte[] array, int pos, ref uint bad)
    {
        return (ReadHex4(array, pos, ref bad) << 16) | ReadHex4(array, pos + 4, ref bad);
    }

    /// <summary>
    /// Maps an ASCII byte to its hex value, or 0xFF for anything that is not a hex digit.
    /// Invalid entries stay above 0xF so callers can detect them by OR-ing results together.
    /// </summary>
    private static readonly byte[] hexLookup = CreateHexLookup();

    private static byte[] CreateHexLookup()
    {
        var table = new byte[256];
        for (int i = 0; i < table.Length; i++) table[i] = 0xFF;
        for (int i = '0'; i <= '9'; i++) table[i] = (byte)(i - '0');
        for (int i = 'a'; i <= 'f'; i++) table[i] = (byte)(i - 'a' + 10);
        for (int i = 'A'; i <= 'F'; i++) table[i] = (byte)(i - 'A' + 10);
        return table;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Guid? ReadNullableGuidValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadGuidValue();
    }

    StringBuilder stringBuilder = new StringBuilder(1024 * 8);
    SlicedBuffer<char> charSlicedBuffer = new SlicedBuffer<char>(1024 * 4, 1024 * 16, 2, true, false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object ReadNullValue()
    {
        byte b = SkipWhiteSpaces();
        if (FoldAsciiToLower(b) != (byte)'n') throw new Exception("Failed reading null");

#if NETSTANDARD2_0
        var remaining = buffer.GetRemainingBytes();
#else
        var remaining = buffer.GetRemainingSpan();
#endif
        if (remaining.Length >= 5 &&
            FoldAsciiToLower(remaining[0]) == (byte)'n' &&
            FoldAsciiToLower(remaining[1]) == (byte)'u' &&
            FoldAsciiToLower(remaining[2]) == (byte)'l' &&
            FoldAsciiToLower(remaining[3]) == (byte)'l' &&
            map_IsFieldEnd[remaining[4]] == FilterResult.Found)
        {
            buffer.TrySkipBytes(4); // move to delimiter
            return null;
        }

        if (!buffer.TryNextByte()) throw new Exception("Failed reading null");
        if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'u') throw new Exception("Failed reading null");

        if (!buffer.TryNextByte()) throw new Exception("Failed reading null");
        if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'l') throw new Exception("Failed reading null");

        if (!buffer.TryNextByte()) throw new Exception("Failed reading null");
        if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'l') throw new Exception("Failed reading null");

        if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading null");
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ReadBoolValue()
    {
        byte b = FoldAsciiToLower(SkipWhiteSpaces());

        if (b == (byte)'t')
        {
#if NETSTANDARD2_0
            var remaining = buffer.GetRemainingBytes();
#else
            var remaining = buffer.GetRemainingSpan();
#endif
            if (remaining.Length >= 5 &&
                FoldAsciiToLower(remaining[0]) == (byte)'t' &&
                FoldAsciiToLower(remaining[1]) == (byte)'r' &&
                FoldAsciiToLower(remaining[2]) == (byte)'u' &&
                FoldAsciiToLower(remaining[3]) == (byte)'e' &&
                map_IsFieldEnd[remaining[4]] == FilterResult.Found)
            {
                buffer.TrySkipBytes(4); // move to delimiter
                return true;
            }

            // Fallback if the optimization is not possible (e.g. because the buffer does not contain enough bytes).
            if (!buffer.TryNextByte()) throw new Exception("Failed reading boolean value");
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'r') throw new Exception("Failed reading boolean value");

            if (!buffer.TryNextByte()) throw new Exception("Failed reading boolean value");
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'u') throw new Exception("Failed reading boolean value");

            if (!buffer.TryNextByte()) throw new Exception("Failed reading boolean value");
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'e') throw new Exception("Failed reading boolean value");

            if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading boolean value");
            return true;
        }
        else if (b == (byte)'f')
        {
#if NETSTANDARD2_0
            var remaining = buffer.GetRemainingBytes();
#else
            var remaining = buffer.GetRemainingSpan();
#endif
            if (remaining.Length >= 6 &&
                FoldAsciiToLower(remaining[0]) == (byte)'f' &&
                FoldAsciiToLower(remaining[1]) == (byte)'a' &&
                FoldAsciiToLower(remaining[2]) == (byte)'l' &&
                FoldAsciiToLower(remaining[3]) == (byte)'s' &&
                FoldAsciiToLower(remaining[4]) == (byte)'e' &&
                map_IsFieldEnd[remaining[5]] == FilterResult.Found)
            {
                buffer.TrySkipBytes(5); // move to delimiter
                return false;
            }

            // Fallback if the optimization is not possible (e.g. because the buffer does not contain enough bytes).
            if (!buffer.TryNextByte()) throw new Exception("Failed reading boolean value");
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'a') throw new Exception("Failed reading boolean value");

            if (!buffer.TryNextByte()) throw new Exception("Failed reading boolean value");
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'l') throw new Exception("Failed reading boolean value");

            if (!buffer.TryNextByte()) throw new Exception("Failed reading boolean value");
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'s') throw new Exception("Failed reading boolean value");

            if (!buffer.TryNextByte()) throw new Exception("Failed reading boolean value");
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'e') throw new Exception("Failed reading boolean value");

            if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading boolean value");
            return false;
        }

        throw new Exception("Failed reading boolean value");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadBoolValue(out bool value)
    {
        value = default;
        byte b = FoldAsciiToLower(SkipWhiteSpaces());

        if (b == (byte)'t')
        {
#if NETSTANDARD2_0
            var remaining = buffer.GetRemainingBytes();
#else
            var remaining = buffer.GetRemainingSpan();
#endif
            if (remaining.Length >= 5 &&
                FoldAsciiToLower(remaining[0]) == (byte)'t' &&
                FoldAsciiToLower(remaining[1]) == (byte)'r' &&
                FoldAsciiToLower(remaining[2]) == (byte)'u' &&
                FoldAsciiToLower(remaining[3]) == (byte)'e' &&
                map_IsFieldEnd[remaining[4]] == FilterResult.Found)
            {
                buffer.TrySkipBytes(4); // move to delimiter
                value = true;
                return true;
            }

            // Fallback if the optimization is not possible (e.g. because the buffer does not contain enough bytes).
            using (var undoHandle = CreateUndoReadHandle())
            {
                if (!buffer.TryNextByte()) return false;
                if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'r') return false;

                if (!buffer.TryNextByte()) return false;
                if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'u') return false;

                if (!buffer.TryNextByte()) return false;
                if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'e') return false;

                if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;
                value = true;
                undoHandle.SetUndoReading(false);
                return true;
            }
        }
        else if (b == (byte)'f')
        {
#if NETSTANDARD2_0
            var remaining = buffer.GetRemainingBytes();
#else
            var remaining = buffer.GetRemainingSpan();
#endif
            if (remaining.Length >= 6 &&
                FoldAsciiToLower(remaining[0]) == (byte)'f' &&
                FoldAsciiToLower(remaining[1]) == (byte)'a' &&
                FoldAsciiToLower(remaining[2]) == (byte)'l' &&
                FoldAsciiToLower(remaining[3]) == (byte)'s' &&
                FoldAsciiToLower(remaining[4]) == (byte)'e' &&
                map_IsFieldEnd[remaining[5]] == FilterResult.Found)
            {
                buffer.TrySkipBytes(5); // move to delimiter
                value = false;
                return true;
            }

            // Fallback if the optimization is not possible (e.g. because the buffer does not contain enough bytes).
            using (var undoHandle = CreateUndoReadHandle())
            {
                if (!buffer.TryNextByte()) return false;
                if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'a') return false;

                if (!buffer.TryNextByte()) return false;
                if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'l') return false;

                if (!buffer.TryNextByte()) return false;
                if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'s') return false;

                if (!buffer.TryNextByte()) return false;
                if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'e') return false;

                if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;
                value = false;
                undoHandle.SetUndoReading(false);
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryReadNullValue()
    {
        byte b = SkipWhiteSpaces();
        if (FoldAsciiToLower(b) != (byte)'n') return false;
        return TryReadNullValue_Continuation();
    }

    private bool TryReadNullValue_Continuation()
    {
        using (var undoHandle = CreateUndoReadHandle())
        {
#if NETSTANDARD2_0
            var remaining = buffer.GetRemainingBytes();
#else
            var remaining = buffer.GetRemainingSpan();
#endif
            // If we have full token + delimiter buffered, decide in one pass.
            if (remaining.Length >= 5)
            {
                if (FoldAsciiToLower(remaining[0]) == (byte)'n' &&
                    FoldAsciiToLower(remaining[1]) == (byte)'u' &&
                    FoldAsciiToLower(remaining[2]) == (byte)'l' &&
                    FoldAsciiToLower(remaining[3]) == (byte)'l' &&
                    map_IsFieldEnd[remaining[4]] == FilterResult.Found)
                {
                    buffer.TrySkipBytes(4); // land on delimiter
                    undoHandle.SetUndoReading(false);
                    return true;
                }
                return false;
            }

            // Fallback path (needed for short remaining buffer / cross-buffer token)
            if (!buffer.TryNextByte()) return false;
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'u') return false;

            if (!buffer.TryNextByte()) return false;
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'l') return false;

            if (!buffer.TryNextByte()) return false;
            if (FoldAsciiToLower(buffer.CurrentByte) != (byte)'l') return false;

            if (!buffer.TryNextByte())
            {
                undoHandle.SetUndoReading(false);
                return true;
            }

            if (!LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;

            undoHandle.SetUndoReading(false);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool? ReadNullableBoolValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadBoolValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object ReadNumberValueAsObject()
    {
        ReadNumberParts(out var isNegative, out var integerPart, out var decimalPart, out var numDecimalDigits,
            out var exponentPart, out bool isExponentNegative, out bool hasDecimalPart, out bool hasExponentPart, ValidNumberComponents.all,
            out int droppedIntegerDigits);

        if (hasDecimalPart || isExponentNegative || droppedIntegerDigits > 0)
        {
            int exp = 0;
            if (hasExponentPart)
            {
                exp = (int)exponentPart;
                if (isExponentNegative) exp = -exp;
            }

            return ComposeDouble(isNegative, integerPart, decimalPart, numDecimalDigits, exp, droppedIntegerDigits);
        }
        else
        {
            if (hasExponentPart)
            {
                int exp = (int)exponentPart;
                integerPart = ApplyExponent(integerPart, exp);
            }

            if (isNegative)
            {
                long value = -(long)integerPart;
                if (value < int.MinValue) return value;
                return (int)value;
            }
            else
            {
                if (integerPart > long.MaxValue) return integerPart;
                long value = (long)integerPart;
                if (value > int.MaxValue) return value;
                return (int)value;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLongValue()
    {
        return ReadSignedIntegerValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadSignedIntegerValue()
    {
        if (TrySignedIntFastPath(out long fastPathValue)) return fastPathValue;

        ReadNumberParts(out var isNegative, out var integerPart, out var decimalPart, out var numDecimalDigits,
            out var exponentPart, out bool isExponentNegative, out bool hasDecimalPart, out bool hasExponentPart, ValidNumberComponents.signedInteger,
            out int droppedIntegerDigits);

        // Digits that did not fit the accumulator mean the value exceeds the integer range.
        if (droppedIntegerDigits > 0) throw new Exception("Value is out of bounds.");

        if (hasExponentPart)
        {
            int exp = (int)exponentPart;
            if (isExponentNegative) exp = -exp;
            integerPart = ApplyExponent(integerPart, exp);
        }

        const ulong maxPos = (ulong)long.MaxValue;
        const ulong maxNegAbs = 1UL + (ulong)long.MaxValue; // abs(long.MinValue)

        if (isNegative)
        {
            if (integerPart > maxNegAbs) throw new Exception("Value is out of bounds.");
            return integerPart == maxNegAbs ? long.MinValue : -(long)integerPart;
        }
        else
        {
            if (integerPart > maxPos) throw new Exception("Value is out of bounds.");
            return (long)integerPart;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadSignedIntegerValue(out long value)
    {
        value = default;
        if (!TryReadNumberParts(out var isNegative, out var integerPart, out _, out _,
            out var exponentPart, out bool isExponentNegative, out _, out bool hasExponentPart, ValidNumberComponents.signedInteger))
        {
            return false;
        }

        if (hasExponentPart)
        {
            int exp = (int)exponentPart;
            if (isExponentNegative) exp = -exp;
            integerPart = ApplyExponent(integerPart, exp);
        }

        value = (long)integerPart;
        if (isNegative) value *= -1;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long? ReadNullableLongValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadLongValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadIntValue()
    {
        long longValue = ReadSignedIntegerValue();
        int value = (int)longValue;
        if (value != longValue) throw new Exception("Value is out of bounds.");
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int? ReadNullableIntValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadIntValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadShortValue()
    {
        long longValue = ReadSignedIntegerValue();
        short value = (short)longValue;
        if (value != longValue) throw new Exception("Value is out of bounds.");
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short? ReadNullableShortValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadShortValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadSbyteValue()
    {
        long longValue = ReadSignedIntegerValue();
        sbyte value = (sbyte)longValue;
        if (value != longValue) throw new Exception("Value is out of bounds.");
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte? ReadNullableSbyteValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadSbyteValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUlongValue()
    {
        return ReadUnsignedIntegerValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUnsignedIntegerValue()
    {
        if (TryUnsignedIntFastPath(out ulong fastPathValue)) return fastPathValue;

        ReadNumberParts(out var isNegative, out var integerPart, out var decimalPart, out var numDecimalDigits,
            out var exponentPart, out bool isExponentNegative, out bool hasDecimalPart, out bool hasExponentPart, ValidNumberComponents.unsignedInteger,
            out int droppedIntegerDigits);

        // Digits that did not fit the accumulator mean the value exceeds the integer range.
        if (droppedIntegerDigits > 0) throw new Exception("Value is out of bounds.");

        var value = integerPart;

        if (hasExponentPart)
        {
            int exp = (int)exponentPart;
            if (isExponentNegative) exp = -exp;
            value = ApplyExponent(value, exp);
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadUnsignedIntegerValue(out ulong value)
    {
        value = default;
        if (!TryReadNumberParts(out _, out var integerPart, out _, out _,
            out var exponentPart, out bool isExponentNegative, out _, out bool hasExponentPart, ValidNumberComponents.unsignedInteger))
        {
            return false;
        }

        value = integerPart;
        if (hasExponentPart)
        {
            int exp = (int)exponentPart;
            if (isExponentNegative) exp = -exp;
            value = ApplyExponent(value, exp);
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong? ReadNullableUlongValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadUlongValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUintValue()
    {
        ulong longValue = ReadUnsignedIntegerValue();
        if (longValue > uint.MaxValue) throw new Exception("Value is out of bounds.");
        return (uint)longValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint? ReadNullableUintValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadUintValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUshortValue()
    {
        ulong longValue = ReadUnsignedIntegerValue();
        if (longValue > ushort.MaxValue) throw new Exception("Value is out of bounds.");
        return (ushort)longValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort? ReadNullableUshortValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadUshortValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByteValue()
    {
        ulong longValue = ReadUnsignedIntegerValue();
        if (longValue > byte.MaxValue) throw new Exception("Value is out of bounds.");
        return (byte)longValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte? ReadNullableByteValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadByteValue();
    }

    static ByteSegment SPECIAL_NUMBER_NAN = new ByteSegment("NaN".ToByteArray(), true);
    static ByteSegment SPECIAL_NUMBER_POS_INFINITY = new ByteSegment("Infinity".ToByteArray(), true);
    static ByteSegment SPECIAL_NUMBER_NEG_INFINITY = new ByteSegment("-Infinity".ToByteArray(), true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDoubleValue()
    {
        byte b = SkipWhiteSpaces();
        if (b == (byte)'"')
        {
            var str = ReadStringBytes();
            if (SPECIAL_NUMBER_NAN.Equals(str)) return double.NaN;
            if (SPECIAL_NUMBER_POS_INFINITY.Equals(str)) return double.PositiveInfinity;
            if (SPECIAL_NUMBER_NEG_INFINITY.Equals(str)) return double.NegativeInfinity;
        }
        ReadNumberParts(out var isNegative, out var integerPart, out var decimalPart, out var numDecimalDigits,
            out var exponentPart, out bool isExponentNegative, out bool hasDecimalPart, out bool hasExponentPart, ValidNumberComponents.floatingPointNumber,
            out int droppedIntegerDigits);

        int exponent = 0;
        if (hasExponentPart)
        {
            exponent = (int)exponentPart;
            if (isExponentNegative) exponent = -exponent;
        }

        return ComposeDouble(isNegative, integerPart, decimalPart, numDecimalDigits, exponent, droppedIntegerDigits);
    }

    public bool TryReadFloatingPointValue(out double value)
    {
        value = default;
        byte b = SkipWhiteSpaces();
        if (b == (byte)'"')
        {
            bool isValidString = TryReadStringBytes(out var str);
            if (isValidString)
            {
                if (SPECIAL_NUMBER_NAN.Equals(str)) value = double.NaN;
                else if (SPECIAL_NUMBER_POS_INFINITY.Equals(str)) value = double.PositiveInfinity;
                else if (SPECIAL_NUMBER_NEG_INFINITY.Equals(str)) value = double.NegativeInfinity;
                else isValidString = false;
            }
            if (isValidString) return true;
        }

        if (!TryReadNumberParts(out var isNegative, out var integerPart, out var decimalPart, out var decimalDigits,
            out var exponentPart, out bool isExponentNegative, out _, out bool hasExponentPart, ValidNumberComponents.floatingPointNumber,
            out int droppedIntegerDigits))
        {
            return false;
        }

        int exponent = 0;
        if (hasExponentPart)
        {
            exponent = (int)exponentPart;
            if (isExponentNegative) exponent = -exponent;
        }

        value = ComposeDouble(isNegative, integerPart, decimalPart, decimalDigits, exponent, droppedIntegerDigits);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double? ReadNullableDoubleValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadDoubleValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal ReadDecimalValue()
    {
        // A decimal carries a 96 bit mantissa, so routing it through double would silently drop
        // precision for anything beyond ~17 significant digits and would additionally hit the
        // exception based overflow path of the ulong digit reader. Parsing the digits straight
        // into the decimal mantissa is both exact and allocation free.
        byte b = SkipWhiteSpaces();
        if (b != (byte)'"' && TryDecimalFastPath(out decimal fastResult)) return fastResult;

        double dbl = ReadDoubleValue();
        if (double.IsNaN(dbl) || double.IsInfinity(dbl)) throw new Exception("Decimals cannot be NaN or Infinity");
        return (decimal)dbl;
    }

    /// <summary>
    /// Parses a JSON number token directly into a <see cref="decimal"/> by accumulating the
    /// significant digits into the 96 bit mantissa and deriving the scale from the decimal point
    /// and the exponent. Returns false whenever the token does not fit this representation (too
    /// many significant digits, a scale outside 0..28, or a token that is not fully buffered), so
    /// the caller falls back to the previous behaviour.
    /// </summary>
    private bool TryDecimalFastPath(out decimal value)
    {
        const int MaxNumberTokenBytes = 64;
        buffer.TryEnsureBuffered(MaxNumberTokenBytes);
        value = default;

#if NETSTANDARD2_0
        var remaining = buffer.GetRemainingBytes();
#else
        var remaining = buffer.GetRemainingSpan();
#endif
        int len = remaining.Length;
        if (len == 0) return false;

        int pos = 0;
        bool isNegative = remaining[0] == (byte)'-';
        if (isNegative) pos++;

        uint lo = 0, mid = 0, hi = 0;
        int digitCount = 0;      // significant digits folded into the mantissa
        int fractionDigits = 0;  // digits seen after the decimal point
        bool anyDigit = false;
        bool sawNonZero = false;

        while (pos < len)
        {
            uint d = (uint)(remaining[pos] - (byte)'0');
            if (d > 9u) break;
            anyDigit = true;
            pos++;
            // Leading zeros carry no information and must not consume mantissa capacity.
            if (d == 0 && !sawNonZero) continue;
            sawNonZero = true;
            if (!TryMul10Add(ref lo, ref mid, ref hi, d)) return false;
            digitCount++;
        }

        if (pos < len && remaining[pos] == (byte)'.')
        {
            pos++;
            while (pos < len)
            {
                uint d = (uint)(remaining[pos] - (byte)'0');
                if (d > 9u) break;
                anyDigit = true;
                pos++;
                fractionDigits++;
                if (d == 0 && !sawNonZero) continue;
                sawNonZero = true;
                if (!TryMul10Add(ref lo, ref mid, ref hi, d)) return false;
                digitCount++;
            }
        }

        if (!anyDigit) return false;

        int exponent = 0;
        if (pos < len && (remaining[pos] == (byte)'e' || remaining[pos] == (byte)'E'))
        {
            pos++;
            if (pos >= len) return false;
            bool expNegative = remaining[pos] == (byte)'-';
            if (expNegative || remaining[pos] == (byte)'+') pos++;

            int expDigits = 0;
            while (pos < len)
            {
                uint d = (uint)(remaining[pos] - (byte)'0');
                if (d > 9u) break;
                exponent = exponent * 10 + (int)d;
                pos++;
                if (++expDigits > 4) return false; // far outside the decimal range anyway
            }
            if (expDigits == 0) return false;
            if (expNegative) exponent = -exponent;
        }

        // The token must be terminated by a field end. Running out of buffered bytes here means
        // real end of input, because MaxNumberTokenBytes exceeds the longest decimal token.
        if (pos < len && map_IsFieldEnd[remaining[pos]] != FilterResult.Found) return false;

        int scale = fractionDigits - exponent;
        if (scale < 0)
        {
            // A positive net exponent is folded into the mantissa as trailing zeros.
            if (digitCount == 0) scale = 0; // the value is zero, the exponent is irrelevant
            else
            {
                while (scale < 0)
                {
                    if (!TryMul10Add(ref lo, ref mid, ref hi, 0)) return false;
                    scale++;
                }
            }
        }
        if (scale > 28) return false;

        value = new decimal((int)lo, (int)mid, (int)hi, isNegative, (byte)scale);

        buffer.TrySkipBytes(pos - 1);
        buffer.TryNextByte();
        return true;
    }

    /// <summary>
    /// Multiplies the 96 bit mantissa by ten and adds a digit. Returns false on overflow.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryMul10Add(ref uint lo, ref uint mid, ref uint hi, uint digit)
    {
        ulong t = (ulong)lo * 10 + digit;
        lo = (uint)t;
        t = (ulong)mid * 10 + (t >> 32);
        mid = (uint)t;
        t = (ulong)hi * 10 + (t >> 32);
        hi = (uint)t;
        return (t >> 32) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal? ReadNullableDecimalValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadDecimalValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloatValue() => (float)ReadDoubleValue();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float? ReadNullableFloatValue()
    {
        if (TryReadNullValue()) return null;
        if (!settings.strict && TryReadEmptyStringValue()) return null;
        return ReadFloatValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IntPtr ReadIntPtrValue()
    {
        long value = ReadSignedIntegerValue();
        if (IntPtr.Size == 4 && (value > int.MaxValue || value < int.MinValue)) throw new Exception("Value is out of bounds.");
        return new IntPtr(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UIntPtr ReadUIntPtrValue()
    {
        ulong value = ReadUnsignedIntegerValue();
        if (UIntPtr.Size == 4 && value > uint.MaxValue) throw new Exception("Value is out of bounds.");
        return new UIntPtr(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonFragment ReadJsonFragmentValue()
    {
        SkipWhiteSpaces();
        var rec = buffer.StartRecording();
        SkipValue();
        var utf8Bytes = rec.GetRecordedBytes(buffer.IsBufferReadToEnd);
        JsonFragment fragment = new JsonFragment(utf8Bytes);
        return fragment;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsonFragment? ReadNullableJsonFragmentValue()
    {
        if (TryReadNullValue()) return null;
        return ReadJsonFragmentValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void SkipValue()
    {
        byte b = SkipWhiteSpaces();

        var valueType = Lookup(map_TypeStart, b);
        switch (valueType)
        {
            case TypeResult.String: SkipString(); break;
            case TypeResult.Object: SkipObject(); break;
            case TypeResult.Bool: SkipBool(); break;
            case TypeResult.Null: SkipNull(); break;
            case TypeResult.Array: SkipArray(); break;
            case TypeResult.Number: SkipNumber(); break;
            default: throw new Exception("Invalid character for value");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipNumber()
    {
        ReadNumberParts(out _, out _, out _, out _, out _, out _, out _, out _, ValidNumberComponents.all);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipArray()
    {
        byte b = SkipWhiteSpaces();
        if (b != '[') throw new Exception("Failed reading array");
        if (!buffer.TryNextByte()) throw new Exception("Failed reading array");
        b = SkipWhiteSpaces();
        while (b != ']')
        {
            SkipValue();
            b = SkipWhiteSpaces();
            if (b == ',')
            {
                if (!buffer.TryNextByte()) throw new Exception("Failed reading array");
                b = SkipWhiteSpaces();
            }
            else if (b != ']') throw new Exception("Failed reading array");
        }

        if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading boolean");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipNull()
    {
        ReadNullValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipBool()
    {
        _ = ReadBoolValue();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipObject()
    {
        byte b = SkipWhiteSpaces();
        if (b != '{') throw new Exception("Failed reading object");
        buffer.TryNextByte();

        while (true)
        {
            b = SkipWhiteSpaces();
            if (b == '}') break;

            var fieldName = ReadStringBytes();
            b = SkipWhiteSpaces();
            if (b != ':') throw new Exception("Failed reading object");
            buffer.TryNextByte();
            SkipValue();
            b = SkipWhiteSpaces();
            if (b == ',') buffer.TryNextByte();
        }

        if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed reading object");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipString()
    {
        _ = ReadStringBytes();
    }

    /// <summary>
    /// Decimal exponent below which <see cref="ComposeDouble"/> switches to a correctly-rounded
    /// conversion. Repeated division by powers of ten loses precision as the intermediate result
    /// approaches the subnormal range, where the gap between representable doubles is enormous
    /// relative to the value. Above this threshold the arithmetic path stays within a few ULP.
    /// </summary>
    const int correctlyRoundedExponentThreshold = -290;

    /// <summary>
    /// Combines the parsed number components into a double.
    /// <para>
    /// The common case scales the accumulated mantissa arithmetically, which is fast and accurate
    /// to about 1 ULP for ordinary magnitudes. Values that scale down into the subnormal range are
    /// recomposed and parsed with a correctly-rounded conversion instead, because there the
    /// arithmetic path can be off by a large fraction of the value rather than a few ULP.
    /// </para>
    /// </summary>
    double ComposeDouble(bool isNegative, ulong integerPart, ulong decimalPart, int numDecimalDigits,
        int exponent, int droppedIntegerDigits)
    {
        int effectiveExponent = exponent + droppedIntegerDigits - numDecimalDigits;
        if (effectiveExponent < correctlyRoundedExponentThreshold)
        {
            return ComposeDoubleCorrectlyRounded(isNegative, integerPart, decimalPart,
                numDecimalDigits, effectiveExponent);
        }

        double value = ApplyExponent((double)decimalPart, -numDecimalDigits);
        value += integerPart;
        if (isNegative) value *= -1;

        // Integer digits that exceeded the accumulator were dropped from the low end, so the
        // accumulated value has to be scaled back up by that many powers of ten.
        if (droppedIntegerDigits > 0) value = ApplyExponent(value, droppedIntegerDigits);
        if (exponent != 0) value = ApplyExponent(value, exponent);
        return value;
    }

    /// <summary>
    /// Rebuilds the significant digits and defers to the framework parser, which performs a
    /// correctly-rounded decimal to binary conversion. Only reached for the rare subnormal range.
    /// <para>
    /// The digits are written into a stack buffer so the conversion stays allocation free on
    /// targets that can parse from a span. A ulong mantissa is at most 20 digits, plus sign,
    /// decimal point, exponent marker and a 4 character exponent, so 48 chars is always enough.
    /// </para>
    /// </summary>
    static double ComposeDoubleCorrectlyRounded(bool isNegative, ulong integerPart, ulong decimalPart,
        int numDecimalDigits, int effectiveExponent)
    {
#if !NETSTANDARD2_0
        Span<char> buffer = stackalloc char[48];
        int pos = 0;

        if (isNegative) buffer[pos++] = '-';

        integerPart.TryFormat(buffer.Slice(pos), out int written, default, CultureInfo.InvariantCulture);
        pos += written;

        if (numDecimalDigits > 0)
        {
            // The accumulator drops leading zeros of the fraction, so restore them.
            int fractionDigits = CountDigits(decimalPart);
            for (int i = fractionDigits; i < numDecimalDigits; i++) buffer[pos++] = '0';
            decimalPart.TryFormat(buffer.Slice(pos), out written, default, CultureInfo.InvariantCulture);
            pos += written;
        }

        buffer[pos++] = 'E';
        effectiveExponent.TryFormat(buffer.Slice(pos), out written, default, CultureInfo.InvariantCulture);
        pos += written;

        if (double.TryParse(buffer.Slice(0, pos), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }
#else
        var sb = new StringBuilder(48);
        if (isNegative) sb.Append('-');
        sb.Append(integerPart.ToString(CultureInfo.InvariantCulture));
        if (numDecimalDigits > 0)
        {
            string fraction = decimalPart.ToString(CultureInfo.InvariantCulture);
            for (int i = fraction.Length; i < numDecimalDigits; i++) sb.Append('0');
            sb.Append(fraction);
        }
        sb.Append('E');
        sb.Append(effectiveExponent.ToString(CultureInfo.InvariantCulture));

        if (double.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }
#endif
        // Underflow past double.Epsilon: preserve the sign of the zero.
        return isNegative ? -0.0 : 0.0;
    }

#if !NETSTANDARD2_0
    static int CountDigits(ulong value)
    {
        int digits = 1;
        while (value >= 10) { value /= 10; digits++; }
        return digits;
    }
#endif

    double ApplyExponent(double value, int exponent)
    {
        int maxExponentFactorLookup = exponentFactorMap.Length - 1;
        if (exponent < 0)
        {
            exponent = -exponent;

            if (exponent <= maxExponentFactorLookup)
            {
                ulong factor = exponentFactorMap[exponent];
                value = value / factor;
                return value;
            }

            while (exponent > 0)
            {
                int partialExp = exponent;
                if (exponent > maxExponentFactorLookup)
                {
                    partialExp = maxExponentFactorLookup;
                    exponent -= maxExponentFactorLookup;
                }
                else exponent = 0;

                ulong factor = exponentFactorMap[partialExp];
                value = value / factor;
            }
            return value;
        }
        else
        {
            if (exponent <= maxExponentFactorLookup)
            {
                ulong factor = exponentFactorMap[exponent];
                value = value * factor;
                return value;
            }

            while (exponent > 0)
            {
                int partialExp = exponent;
                if (exponent > maxExponentFactorLookup)
                {
                    partialExp = maxExponentFactorLookup;
                    exponent -= maxExponentFactorLookup;
                }
                else exponent = 0;

                ulong factor = exponentFactorMap[partialExp];
                value = value * factor;
            }
            return value;
        }
    }

    ulong ApplyExponent(ulong value, int exponent)
    {
        int maxExponentFactorLookup = exponentFactorMap.Length - 1;
        if (exponent < 0)
        {
            exponent = -exponent;

            if (exponent <= maxExponentFactorLookup)
            {
                ulong factor = exponentFactorMap[exponent];
                value = value / factor;
                return value;
            }

            while (exponent > 0)
            {
                int partialExp = exponent;
                if (exponent > maxExponentFactorLookup)
                {
                    partialExp = maxExponentFactorLookup;
                    exponent -= maxExponentFactorLookup;
                }
                else exponent = 0;

                ulong factor = exponentFactorMap[partialExp];
                value = value / factor;
            }
            return value;
        }
        else
        {
            if (exponent <= maxExponentFactorLookup)
            {
                ulong factor = exponentFactorMap[exponent];
                value = value * factor;
                return value;
            }

            while (exponent > 0)
            {
                int partialExp = exponent;
                if (exponent > maxExponentFactorLookup)
                {
                    partialExp = maxExponentFactorLookup;
                    exponent -= maxExponentFactorLookup;
                }
                else exponent = 0;

                ulong factor = exponentFactorMap[partialExp];
                value = value * factor;
            }
            return value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ulong BytesToInteger(ByteSegment byteSegment)
    {
        ulong value = 0;
#if NETSTANDARD2_0            
        var bytes = byteSegment;
#else
        var bytes = byteSegment.AsSpan();
#endif
        if (bytes.Length == 0) return value;
        value += (byte)(bytes[0] - (byte)'0');
        for (int i = 1; i < bytes.Length; i++)
        {
            value *= 10;
            value += (byte)(bytes[i] - (byte)'0');
        }
        return value;
    }

    [Flags]
    enum ValidNumberComponents
    {
        negativeSign = 1 << 0,
        decimalPart = 1 << 1,
        exponent = 1 << 2,
        all = negativeSign | decimalPart | exponent,
        floatingPointNumber = negativeSign | decimalPart | exponent,
        signedInteger = negativeSign | exponent,
        unsignedInteger = exponent,
    }

    static readonly ByteSegment zeroAsBytes = new byte[] { (byte)'0' };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TrySignedIntFastPath(out long value)
    {
        buffer.TryEnsureBuffered(21); // max length of long in decimal is 20 chars (including sign)
        bool isNegative = buffer.CurrentByte == (byte)'-';
        ulong uValue = 0;
        value = 0;
#if NETSTANDARD2_0
        var remaining = buffer.GetRemainingBytes();
#else
        var remaining = buffer.GetRemainingSpan();
#endif
        if (isNegative) remaining = remaining.Slice(1); // skip sign for digit parsing, but not for length check
        int len = 0;
        unchecked
        {
            while ((uint)len < (uint)remaining.Length && (uint)(remaining[len] - (byte)'0') <= 9u) len++;
        }
        if (len == 0 || len >= 19) return false; // leave fast path if more than 18 digits as a performance tradeoff (max long is 19 digits, but overflow is possible, which is checked in the slow path)
        if (len < remaining.Length && map_IsFieldEnd[remaining[len]] != FilterResult.Found) return false;

        var digits = remaining.Slice(0, len);
        for (int i = 0; i < digits.Length; i++)
        {
            unchecked { uValue = uValue * 10 + (uint)(digits[i] - (byte)'0'); }
        }

        if (isNegative)
        {
            value = -(long)uValue;
            buffer.TrySkipBytes(len);
        }
        else
        {
            value = (long)uValue;
            buffer.TrySkipBytes(len - 1);
        }
        buffer.TryNextByte();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    private bool TryUnsignedIntFastPath(out ulong value)
    {
        buffer.TryEnsureBuffered(21); // max length of long in decimal is 20 chars (including sign)
        value = 0;

#if NETSTANDARD2_0
        var remaining = buffer.GetRemainingBytes();
#else
        var remaining = buffer.GetRemainingSpan();
#endif
        int len = 0;
        unchecked
        {
            while ((uint)len < (uint)remaining.Length && (uint)(remaining[len] - (byte)'0') <= 9u) len++;
        }
        if (len == 0 || len >= 20) return false; // leave fast path if more than 19 digits as a performance tradeoff (max ulong is 20 digits, but overflow is possible, which is checked in the slow path)
        if (len < remaining.Length && map_IsFieldEnd[remaining[len]] != FilterResult.Found) return false;

        var digits = remaining.Slice(0, len);
        for (int i = 0; i < digits.Length; i++)
        {
            unchecked { value = value * 10 + (uint)(digits[i] - (byte)'0'); }
        }

        buffer.TrySkipBytes(len - 1);
        buffer.TryNextByte();
        return true;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    /// <summary>
    /// Reads a run of ASCII digits into a <see cref="ulong"/>.
    /// <para>
    /// JSON places no limit on the number of digits, so a run that exceeds the capacity of a
    /// <see cref="ulong"/> is not an error by itself: the surplus digits are counted via
    /// <paramref name="droppedDigits"/> and discarded instead of throwing. The caller knows the
    /// target type and decides whether the dropped digits merely shift the scale (floating point)
    /// or constitute an out-of-range value (integers).
    /// </para>
    /// </summary>
    private ulong ReadDigitSegmentAsUInt64(out int digitCount, out bool couldNotSkip, out int droppedDigits)
    {
        ulong value = 0;
        digitCount = 0;
        couldNotSkip = false;
        droppedDigits = 0;

#if NETSTANDARD2_0
        ByteSegment remaining = buffer.GetRemainingBytes();
#else
        ReadOnlySpan<byte> remaining = buffer.GetRemainingSpan();
#endif

        int len = 0;
        unchecked
        {
            while ((uint)len < (uint)remaining.Length && (uint)(remaining[len] - (byte)'0') <= 9u) len++;
        }

        if (len == 0) return 0;
        if (len < 20)
        {
            var digits = remaining.Slice(0, len);
            for (int i = 0; i < digits.Length; i++)
            {
                unchecked { value = value * 10 + (uint)(digits[i] - (byte)'0'); }
            }
        }
        else
        {
            value = HandleManyDigits(value, remaining, len, out droppedDigits);
        }

        digitCount = len;

        if (len < remaining.Length)
        {
            // land on first non-digit
            buffer.TrySkipBytes(len);
        }
        else
        {
            // consumed entire remaining span; advance once to get delimiter or EOF rollback state
            int jump = remaining.Length - 1;
            if (jump > 0) buffer.TrySkipBytes(jump);
            couldNotSkip = !buffer.TryNextByte();
        }

#if NETSTANDARD2_0
        static ulong HandleManyDigits(ulong value, ByteSegment remaining, int len, out int droppedDigits)
#else
        static ulong HandleManyDigits(ulong value, ReadOnlySpan<byte> remaining, int len, out int droppedDigits)
#endif
        {
            // Accumulate as many leading digits as fit without overflowing, then count the rest.
            const ulong maxDiv10 = ulong.MaxValue / 10;
            const byte maxLast = (byte)(ulong.MaxValue % 10);

            int i = 0;
            for (; i < len; i++)
            {
                byte d = (byte)(remaining[i] - (byte)'0');
                if (value > maxDiv10 || (value == maxDiv10 && d > maxLast)) break;
                unchecked { value = value * 10 + d; }
            }
            droppedDigits = len - i;
            return value;
        }
        return value;
    }

    void ReadNumberParts(
        out bool isNegative,
        out ulong integerPart,
        out ulong decimalPart,
        out int decimalDigits,
        out ulong exponentPart,
        out bool isExponentNegative,
        out bool hasDecimalPart,
        out bool hasExponentPart,
        ValidNumberComponents validComponents)
        => ReadNumberParts(out isNegative, out integerPart, out decimalPart, out decimalDigits,
            out exponentPart, out isExponentNegative, out hasDecimalPart, out hasExponentPart,
            validComponents, out _);

    void ReadNumberParts(
        out bool isNegative,
        out ulong integerPart,
        out ulong decimalPart,
        out int decimalDigits,
        out ulong exponentPart,
        out bool isExponentNegative,
        out bool hasDecimalPart,
        out bool hasExponentPart,
        ValidNumberComponents validComponents,
        out int droppedIntegerDigits)
    {
        const int MaxNumberTokenBytes = 52;  // int(20) + dec(20) + exp(8) + signs/dot/delimiter(4)
        _ = buffer.TryEnsureBuffered(MaxNumberTokenBytes);

        bool stringAsNumberStarted = false;

        isNegative = false;
        integerPart = 0;
        decimalPart = 0;
        decimalDigits = 0;
        exponentPart = 0;
        isExponentNegative = false;
        hasDecimalPart = false;
        hasExponentPart = false;
        droppedIntegerDigits = 0;

        bool allowNegative = validComponents.IsFlagSet(ValidNumberComponents.negativeSign);
        bool allowDecimal = validComponents.IsFlagSet(ValidNumberComponents.decimalPart);
        bool allowExponent = validComponents.IsFlagSet(ValidNumberComponents.exponent);

        byte b = SkipWhiteSpaces();
        if (b == '"')
        {
            if (settings.strict) throw new Exception("Failed reading number: unexpected '\"' character");
            stringAsNumberStarted = true;

            if (!buffer.TryNextByte()) throw new Exception("Failed reading number: unexpected end of input");
            if (buffer.CurrentByte == '"')
            {
                // empty string => zero (legacy behavior)
                if (!buffer.TryNextByte()) return;
                return;
            }
        }

        isNegative = buffer.CurrentByte == '-';
        if (isNegative)
        {
            if (!allowNegative) throw new Exception("Failed reading number");
            if (!buffer.TryNextByte()) throw new Exception("Failed reading number");
        }

        integerPart = ReadDigitSegmentAsUInt64(out int intDigits, out bool couldNotSkip, out droppedIntegerDigits);
        b = buffer.CurrentByte;

        if (intDigits == 0)
        {
            if (b != '.') throw new Exception("Failed reading number: no digits found for integer part and no decimal point found");
            integerPart = 0;
        }

        if (b == '.')
        {
            if (!allowDecimal && settings.strict) throw new Exception("Failed reading number: Unexpected decimal point");
            if (!buffer.TryNextByte()) throw new Exception("Failed reading number");

            hasDecimalPart = true;
            decimalPart = ReadDigitSegmentAsUInt64(out decimalDigits, out couldNotSkip, out int droppedDecimalDigits);

            // Surplus fraction digits are beyond the precision of the accumulator and only the
            // digits that were actually folded in may count towards the scale.
            decimalDigits -= droppedDecimalDigits;

            // semantic: "." counts like ".0"
            if (decimalDigits == 0) decimalDigits = 1;
        }

        if (buffer.CurrentByte == 'e' || buffer.CurrentByte == 'E')
        {
            if (!allowExponent) throw new Exception("Failed reading number: Unexpected exponent");

            if (!buffer.TryNextByte()) throw new Exception("Failed reading number");
            hasExponentPart = true;

            isExponentNegative = buffer.CurrentByte == '-';
            if (isExponentNegative || buffer.CurrentByte == '+')
            {
                if (!buffer.TryNextByte()) throw new Exception("Failed reading number");
            }

            exponentPart = ReadDigitSegmentAsUInt64(out int expDigits, out couldNotSkip, out _);
            if (expDigits == 0) exponentPart = 0; // semantic: "e+" => exponent 0                
        }

        if (stringAsNumberStarted)
        {
            if (buffer.CurrentByte != '"') throw new Exception("Failed reading number: string as number not closed");
            couldNotSkip = !buffer.TryNextByte();
        }

        if (!couldNotSkip && map_IsFieldEnd[buffer.CurrentByte] != FilterResult.Found)
            throw new Exception("Failed reading number: unexpected character after number");
    }

    bool TryReadNumberParts(
        out bool isNegative,
        out ulong integerPart,
        out ulong decimalPart,
        out int decimalDigits,
        out ulong exponentPart,
        out bool isExponentNegative,
        out bool hasDecimalPart,
        out bool hasExponentPart,
        ValidNumberComponents validComponents)
        => TryReadNumberParts(out isNegative, out integerPart, out decimalPart, out decimalDigits,
            out exponentPart, out isExponentNegative, out hasDecimalPart, out hasExponentPart,
            validComponents, out _);

    bool TryReadNumberParts(
        out bool isNegative,
        out ulong integerPart,
        out ulong decimalPart,
        out int decimalDigits,
        out ulong exponentPart,
        out bool isExponentNegative,
        out bool hasDecimalPart,
        out bool hasExponentPart,
        ValidNumberComponents validComponents,
        out int droppedIntegerDigits)
    {
        const int MaxNumberTokenBytes = 52;  // int(20) + dec(20) + exp(8) + signs/dot/delimiter(4)
        _ = buffer.TryEnsureBuffered(MaxNumberTokenBytes);

        bool stringAsNumberStarted = false;
        droppedIntegerDigits = 0;

        using (var undoHandle = CreateUndoReadHandle())
        {
            isNegative = false;
            integerPart = 0;
            decimalPart = 0;
            decimalDigits = 0;
            exponentPart = 0;
            isExponentNegative = false;
            hasDecimalPart = false;
            hasExponentPart = false;

            bool allowNegative = validComponents.IsFlagSet(ValidNumberComponents.negativeSign);
            bool allowDecimal = validComponents.IsFlagSet(ValidNumberComponents.decimalPart);
            bool allowExponent = validComponents.IsFlagSet(ValidNumberComponents.exponent);

            byte b = SkipWhiteSpaces();
            if (b == '"')
            {
                if (settings.strict) return false;
                stringAsNumberStarted = true;

                if (!buffer.TryNextByte()) return false;
                if (buffer.CurrentByte == '"')
                {
                    // empty string => zero (legacy behavior)
                    buffer.TryNextByte();
                    undoHandle.SetUndoReading(false);
                    return true;
                }
            }

            isNegative = buffer.CurrentByte == '-';
            if (isNegative)
            {
                if (!allowNegative) return false;
                if (!buffer.TryNextByte()) return false;
            }

            integerPart = ReadDigitSegmentAsUInt64(out int intDigits, out _, out droppedIntegerDigits);
            b = buffer.CurrentByte;

            if (intDigits == 0)
            {
                if (b != '.') return false;
                integerPart = 0;
            }

            if (map_IsFieldEnd[buffer.CurrentByte] == FilterResult.Found)
            {
                undoHandle.SetUndoReading(false);
                return true;
            }

            if (b == '.')
            {
                if (!allowDecimal && settings.strict) return false;
                if (!buffer.TryNextByte()) return false;

                hasDecimalPart = true;
                decimalPart = ReadDigitSegmentAsUInt64(out decimalDigits, out _, out int droppedDecimalDigits);

                // Only the digits actually folded into the accumulator may count towards the scale.
                decimalDigits -= droppedDecimalDigits;

                // semantic: "." counts like ".0"
                if (decimalDigits == 0) decimalDigits = 1;
            }

            if (buffer.CurrentByte == 'e' || buffer.CurrentByte == 'E')
            {
                if (!allowExponent) return false;
                if (!buffer.TryNextByte()) return false;

                hasExponentPart = true;
                isExponentNegative = buffer.CurrentByte == '-';
                if (isExponentNegative || buffer.CurrentByte == '+')
                {
                    if (!buffer.TryNextByte()) return false;
                }

                exponentPart = ReadDigitSegmentAsUInt64(out int expDigits, out _, out _);
                if (expDigits == 0) exponentPart = 0; // semantic: "e+" => exponent 0
            }

            if (stringAsNumberStarted)
            {
                if (buffer.CurrentByte != '"') return false;
                buffer.TryNextByte();
            }

            if (!buffer.IsBufferReadToEnd && map_IsFieldEnd[buffer.CurrentByte] != FilterResult.Found) return false;

            undoHandle.SetUndoReading(false);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ByteSegment ReadStringBytes()
    {
        byte b = SkipWhiteSpaces();
        if (b != (byte)'"') throw new Exception("Failed reading string value: No starting quote found.");

        var recording = buffer.StartRecording(true);

#if NET5_0_OR_GREATER
        if (!buffer.TryNextByte()) throw new Exception("Failed reading string value: No ending quote found.");

        while (true)
        {
            ReadOnlySpan<byte> remaining = buffer.GetRemainingSpan();
            int specialIndex = remaining.IndexOfAny((byte)'"', (byte)'\\');

            if (specialIndex < 0)
            {
                int jump = remaining.Length - 1;
                if (jump > 0) buffer.TrySkipBytes(jump);
                if (!buffer.TryNextByte()) throw new Exception("Failed reading string value: No ending quote found.");
                continue;
            }

            if (specialIndex > 0) buffer.TrySkipBytes(specialIndex);

            if (remaining[specialIndex] == (byte)'"')
            {
                var stringBytes = recording.GetRecordedBytes_WithoutCurrent();
                buffer.TryNextByte();
                return stringBytes;
            }

            if (remaining.Length - specialIndex > 2)
            {
                buffer.TrySkipBytes(2);
                continue;
            }

            if (!buffer.TryNextByte()) throw new Exception("Failed reading string value: Invalid escape sequence.");
            if (!buffer.TryNextByte()) throw new Exception("Failed reading string value: No ending quote found.");
        }
#else
        while (buffer.TryNextByte())
        {
            b = buffer.CurrentByte;
            if ((b & 0b10000000) == 0 && b != (byte)'"' && b != (byte)'\\') continue;
            if (b == (byte)'"')
            {
                var stringBytes = recording.GetRecordedBytes_WithoutCurrent();
                buffer.TryNextByte();
                return stringBytes;
            }
            else if (!HandleSpecialChars(b)) throw new Exception("Failed reading string value: Invalid character found.");
        }

        throw new Exception("Failed reading string value: No ending quote found.");
#endif
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryReadStringBytes(out ByteSegment stringBytes)
    {
        stringBytes = default;
        byte b = SkipWhiteSpaces();

        if (b != (byte)'"') return false;
        using (var undoHandle = CreateUndoReadHandle())
        {
            var recording = buffer.StartRecording(true);

#if NET5_0_OR_GREATER
            if (!buffer.TryNextByte()) return false;

            while (true)
            {
                ReadOnlySpan<byte> remaining = buffer.GetRemainingSpan();
                int specialIndex = remaining.IndexOfAny((byte)'"', (byte)'\\');

                if (specialIndex < 0)
                {
                    int jump = remaining.Length - 1;
                    if (jump > 0) buffer.TrySkipBytes(jump);

                    if (!buffer.TryNextByte()) return false;
                    continue;
                }

                if (specialIndex > 0) buffer.TrySkipBytes(specialIndex);

                if (remaining[specialIndex] == (byte)'"')
                {
                    stringBytes = recording.GetRecordedBytes_WithoutCurrent();
                    buffer.TryNextByte();
                    undoHandle.SetUndoReading(false);
                    return true;
                }

                // Found '\'
                if (remaining.Length - specialIndex > 2)
                {
                    buffer.TrySkipBytes(2);
                    continue;
                }

                if (!buffer.TryNextByte()) return false;
                if (!buffer.TryNextByte()) return false;
            }
#else
            while (buffer.TryNextByte())
            {
                b = buffer.CurrentByte;
                if ((b & 0b10000000) == 0 && b != (byte)'"' && b != (byte)'\\') continue;
                if (b == (byte)'"')
                {
                    stringBytes = recording.GetRecordedBytes_WithoutCurrent();
                    buffer.TryNextByte();
                    undoHandle.SetUndoReading(false);
                    return true;
                }
                else if (!HandleSpecialChars(b)) return false;
            }
            return false;
#endif
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryReadStringBytesOrNull(out ByteSegment stringBytes, out bool isNull)
    {
        stringBytes = default;
        isNull = false;
        byte b = SkipWhiteSpaces();

        if (b != (byte)'"' && b != (byte)'n' && b != (byte)'N') return false;
        using (var undoHandle = CreateUndoReadHandle())
        {
            if (b != (byte)'"')
            {
                isNull = TryReadNullValue();
                if (isNull) undoHandle.SetUndoReading(false);
                return isNull;
            }

            var recording = buffer.StartRecording(true);

#if NET5_0_OR_GREATER
            if (!buffer.TryNextByte()) return false;

            while (true)
            {
                ReadOnlySpan<byte> remaining = buffer.GetRemainingSpan();
                int specialIndex = remaining.IndexOfAny((byte)'"', (byte)'\\');

                if (specialIndex < 0)
                {
                    int jump = remaining.Length - 1;
                    if (jump > 0) buffer.TrySkipBytes(jump);

                    if (!buffer.TryNextByte()) return false;
                    continue;
                }

                if (specialIndex > 0) buffer.TrySkipBytes(specialIndex);

                if (remaining[specialIndex] == (byte)'"')
                {
                    stringBytes = recording.GetRecordedBytes_WithoutCurrent();
                    buffer.TryNextByte();
                    undoHandle.SetUndoReading(false);
                    return true;
                }

                // Found '\'
                if (remaining.Length - specialIndex > 2)
                {
                    buffer.TrySkipBytes(2);
                    continue;
                }

                if (!buffer.TryNextByte()) return false;
                if (!buffer.TryNextByte()) return false;
            }
#else
            while (buffer.TryNextByte())
            {
                b = buffer.CurrentByte;
                if ((b & 0b10000000) == 0 && b != (byte)'"' && b != (byte)'\\') continue;
                if (b == (byte)'"')
                {
                    stringBytes = recording.GetRecordedBytes_WithoutCurrent();
                    buffer.TryNextByte();
                    undoHandle.SetUndoReading(false);
                    return true;
                }
                else if (!HandleSpecialChars(b)) return false;
            }
            return false;
#endif
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HandleSpecialChars(byte b)
    {
        if (b == (byte)'\\')
        {
            buffer.TryNextByte();
        }
        else if ((b & 0b11100000) == 0b11000000) // skip 1 byte
        {
            buffer.TryNextByte();
        }
        else if ((b & 0b11110000) == 0b11100000) // skip 2 bytes
        {
            buffer.TryNextByte();
            buffer.TryNextByte();
        }
        else if ((b & 0b11111000) == 0b11110000) // skip 3 bytes
        {
            buffer.TryNextByte();
            buffer.TryNextByte();
            buffer.TryNextByte();
        }
        else return false;
        return true;
    }

#if NET5_0_OR_GREATER
    static readonly SearchValues<byte> jsonWhitespaceSearchValues = SearchValues.Create(" \t\n\r"u8);
#endif

    // Fast path wrapper: on compact JSON the next byte is almost never whitespace, so the common
    // case is a single byte test. Contains no loop, so the JIT can actually inline it, leaving the
    // scanning loop outlined in SkipWhiteSpaces(). Safe because SkipWhiteSpaces() re-reads
    // CurrentByte and consumes nothing when the byte is not whitespace.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    byte SkipWhiteSpaces()
    {
        byte b = buffer.CurrentByte;
        if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\n' && b != (byte)'\r') return b;
        return SkipWhiteSpaces_Loop();
    }

    // Should only be called by SkipWhiteSpaces(), because it expects the current byte as already checked to be a whitespace and skips it.
    byte SkipWhiteSpaces_Loop()
    {
        buffer.TryNextByte();
        byte b = buffer.CurrentByte;

#if NET5_0_OR_GREATER
        while (true)
        {
            if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\n' && b != (byte)'\r') return b;

            ReadOnlySpan<byte> remaining = buffer.GetRemainingSpan();
            int nonWsIndex = remaining.IndexOfAnyExcept(jsonWhitespaceSearchValues);

            if (nonWsIndex >= 0)
            {
                if (nonWsIndex > 0) buffer.TrySkipBytes(nonWsIndex);
                return buffer.CurrentByte;
            }

            int jump = remaining.Length - 1;
            if (jump > 0) buffer.TrySkipBytes(jump);

            if (!buffer.TryNextByte()) return buffer.CurrentByte; // EOF rollback state
            b = buffer.CurrentByte;
        }
#else
        while ((b == (byte)' ' || b == (byte)'\t' || b == (byte)'\n' || b == (byte)'\r') && buffer.TryNextByte())
        {
            b = buffer.CurrentByte;
        }
        return b;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SkipRemainingFieldsOfObject()
    {
        byte b = SkipWhiteSpaces();
        if (b == ',') buffer.TryNextByte();
        while (true)
        {
            b = SkipWhiteSpaces();
            if (b == '}') break;

            ReadStringBytes();
            b = SkipWhiteSpaces();
            if (b != ':') throw new Exception("Failed skipping object: expected ':' after field name");
            buffer.TryNextByte();
            SkipValue();
            b = SkipWhiteSpaces();
            if (b == ',') buffer.TryNextByte();
        }
        if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) throw new Exception("Failed skipping object: expected field end after object end");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrySkipRemainingFieldsOfObject()
    {
        byte b = SkipWhiteSpaces();
        if (b == ',') buffer.TryNextByte();
        while (true)
        {
            b = SkipWhiteSpaces();
            if (b == '}') break;

            if (!TryReadStringBytes(out var _)) return false;
            b = SkipWhiteSpaces();
            if (b != ':') return false;
            buffer.TryNextByte();
            SkipValue();
            b = SkipWhiteSpaces();
            if (b == ',') buffer.TryNextByte();
        }
        if (buffer.TryNextByte() && !LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryReadEmptyStringValue()
    {
        byte b = SkipWhiteSpaces();
        // check for starting quote before creating undo handle, because if it's not '\"',
        // we can directly return false without needing to reset buffer position
        if (b != '\"') return false;
        using (var undoHandle = CreateUndoReadHandle())
        {
            if (!buffer.TryNextByte()) return false;
            b = buffer.CurrentByte;
            if (b != '\"') return false;
            // Check for field end
            if (!buffer.TryNextByte())
            {
                undoHandle.SetUndoReading(false);
                return true;
            }
            if (!LookupCheck(map_IsFieldEnd, buffer.CurrentByte, FilterResult.Found)) return false;

            undoHandle.SetUndoReading(false);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string DecodeUtf8Bytes(ArraySegment<byte> bytes)
    {
        string str = Utf8Converter.DecodeUtf8ToString(bytes, stringBuilder);
        stringBuilder.Clear();
        return str;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static byte FoldAsciiToLower(byte b) => (byte)(b | 0x20);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TypeResult Lookup(TypeResult[] map, byte index)
    {
        Debug.Assert(map != null && map.Length > byte.MaxValue);
        return map[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LookupCheck(FilterResult[] map, byte index, FilterResult comparant)
    {
        Debug.Assert(map != null && map.Length > byte.MaxValue);
        return comparant == map[index];
    }

    enum FilterResult : byte
    {
        Skip,
        Found,
        Unexpected
    }

    public enum TypeResult : byte
    {
        Whitespace,
        Object,
        Number,
        String,
        Null,
        Bool,
        Array,
        Invalid
    }

    static bool IsWhiteSpace(byte b)
    {
        return b == ' ' || b == '\t' || b == '\n' || b == '\r';
    }

    static readonly FilterResult[] map_IsFieldEnd = CreateFilterMap_IsFieldEnd();
    static readonly TypeResult[] map_TypeStart = CreateTypeStartMap();
    static ulong[] exponentFactorMap = CreateExponentFactorMap(19);

    static FilterResult[] CreateFilterMap_IsFieldEnd()
    {
        FilterResult[] map = new FilterResult[256];
        for (int i = 0; i < map.Length; i++)
        {
            if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = FilterResult.Found;
            else if (i == ',' || i == ']' || i == '}' || i == ':') map[i] = FilterResult.Found;
            else map[i] = FilterResult.Unexpected;
        }
        return map;
    }

    static TypeResult[] CreateTypeStartMap()
    {
        TypeResult[] map = new TypeResult[256];
        for (int i = 0; i < map.Length; i++)
        {
            if (i == ' ' || i == '\t' || i == '\n' || i == '\r') map[i] = TypeResult.Whitespace;
            else if ((i >= '0' && i <= '9') || i == '-') map[i] = TypeResult.Number;
            else if (i == '\"') map[i] = TypeResult.String;
            else if (i == 'N' || i == 'n') map[i] = TypeResult.Null;
            else if (i == 'T' || i == 't' || i == 'F' || i == 'f') map[i] = TypeResult.Bool;
            else if (i == '{') map[i] = TypeResult.Object;
            else if (i == '[') map[i] = TypeResult.Array;
            else map[i] = TypeResult.Invalid;
        }
        return map;
    }
}
