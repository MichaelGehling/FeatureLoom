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
    private byte[] ReadByteArray(CachedTypeReader byteArrayReader)
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
            int maxDecodedLength = Base64.GetMaxDecodedFromUtf8Length(utf8Base64.Length);
            byte[] bytes = new byte[maxDecodedLength];
            Span<byte> decodedSpan = bytes;
            OperationStatus status = Base64.DecodeFromUtf8(utf8Base64, decodedSpan, out int bytesConsumed, out int bytesWritten);
            if (status != OperationStatus.Done) throw new FormatException($"Invalid Base64 sequence (status = {status}).");
            if (bytesWritten != bytes.Length) Array.Resize(ref bytes, bytesWritten);
            return bytes;
#endif
        }
        else if (b == '[')
        {
            return byteArrayReader.ReadValue_CheckProposed<byte[]>();
        }

        throw new Exception("Expected byte array, but didn't got an array nor an Base64 string");
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
        return cachedStringObjectDictionaryReader.ReadValue_CheckProposed<Dictionary<string, object>>();
    }

    CollectionCaster collectionCaster = new CollectionCaster();
    CachedTypeReader cachedObjectArrayReader = null;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object ReadArrayValue()
    {
        if (cachedObjectArrayReader == null) cachedObjectArrayReader = CreateCachedTypeReader(typeof(object[]));
        var objectsArray = cachedObjectArrayReader.ReadValue_CheckProposed<object[]>();
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

    readonly QuickStringCache stringCache;
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
            out var exponentPart, out bool isExponentNegative, out bool hasDecimalPart, out bool hasExponentPart, ValidNumberComponents.all);

        if (hasDecimalPart || isExponentNegative)
        {
            double value = ApplyExponent((double)decimalPart, -numDecimalDigits);
            value += integerPart;
            if (isNegative) value *= -1;

            if (hasExponentPart)
            {
                int exp = (int)exponentPart;
                if (isExponentNegative) exp = -exp;
                value = ApplyExponent(value, exp);
            }

            return value;
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
            out var exponentPart, out bool isExponentNegative, out bool hasDecimalPart, out bool hasExponentPart, ValidNumberComponents.signedInteger);

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
            out var exponentPart, out bool isExponentNegative, out bool hasDecimalPart, out bool hasExponentPart, ValidNumberComponents.unsignedInteger);

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
            out var exponentPart, out bool isExponentNegative, out bool hasDecimalPart, out bool hasExponentPart, ValidNumberComponents.floatingPointNumber);

        double value = ApplyExponent((double)decimalPart, -numDecimalDigits);
        value += integerPart;
        if (isNegative) value *= -1;

        if (hasExponentPart)
        {
            int exp = (int)exponentPart;
            if (isExponentNegative) exp = -exp;
            value = ApplyExponent(value, exp);
        }

        return value;
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
            out var exponentPart, out bool isExponentNegative, out _, out bool hasExponentPart, ValidNumberComponents.floatingPointNumber))
        {
            return false;
        }

        value = ApplyExponent((double)decimalPart, -decimalDigits);
        value += integerPart;
        if (isNegative) value *= -1;

        if (hasExponentPart)
        {
            int exp = (int)exponentPart;
            if (isExponentNegative) exp = -exp;
            value = ApplyExponent(value, exp);
        }

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
        double dbl = ReadDoubleValue();
        if (double.IsNaN(dbl) || double.IsInfinity(dbl)) throw new Exception("Decimals cannot be NaN or Infinity");
        return (decimal)dbl;
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
        string json = DecodeUtf8Bytes(utf8Bytes);
        JsonFragment fragment = new JsonFragment(json);
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
    private ulong ReadDigitSegmentAsUInt64(out int digitCount, out bool couldNotSkip)
    {
        ulong value = 0;
        digitCount = 0;
        couldNotSkip = false;

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
            value = Handle20OrMoreDigits(value, remaining, len);
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
        static ulong Handle20OrMoreDigits(ulong value, ByteSegment remaining, int len)
#else
        static ulong Handle20OrMoreDigits(ulong value, ReadOnlySpan<byte> remaining, int len)
#endif
        {
            if (len == 20)
            {
                var digits = remaining.Slice(0, 20);
                for (int i = 0; i < 19; i++)
                {
                    unchecked { value = value * 10 + (uint)(digits[i] - (byte)'0'); }
                }
                // For the 20th digit we need to check for overflow since the value could exceed ulong.MaxValue
                const ulong maxDiv10 = ulong.MaxValue / 10;
                const byte maxLast = (byte)(ulong.MaxValue % 10);
                byte last = (byte)(remaining[19] - (byte)'0');
                if (value > maxDiv10 || (value == maxDiv10 && last > maxLast)) throw new Exception("Number is too large");
                unchecked { value = value * 10 + last; }
            }
            else throw new Exception("Too many digits in number");
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

        integerPart = ReadDigitSegmentAsUInt64(out int intDigits, out bool couldNotSkip);
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
            decimalPart = ReadDigitSegmentAsUInt64(out decimalDigits, out couldNotSkip);

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

            exponentPart = ReadDigitSegmentAsUInt64(out int expDigits, out couldNotSkip);
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
    {
        const int MaxNumberTokenBytes = 52;  // int(20) + dec(20) + exp(8) + signs/dot/delimiter(4)
        _ = buffer.TryEnsureBuffered(MaxNumberTokenBytes);

        bool stringAsNumberStarted = false;

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

            integerPart = ReadDigitSegmentAsUInt64(out int intDigits, out _);
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
                decimalPart = ReadDigitSegmentAsUInt64(out decimalDigits, out _);

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

                exponentPart = ReadDigitSegmentAsUInt64(out int expDigits, out _);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    byte SkipWhiteSpaces()
    {
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryFindProposedType(ref CachedTypeReader proposedTypeReaderRef, ref ByteSegment proposedTypeNameRef, Type expectedType, out bool foundValueField)
    {
        foundValueField = false;

        // 1. find $type field

        // Already skipped whitespace and checked for '{' in TryReadAsProposedType before calling this method,
        // so we can skip it here to save some performance in the common case where no proposed type is provided.
        // byte b = SkipWhiteSpaces();
        // if (b != (byte)'{') return false;
        buffer.TryNextByte();

        // compare byte per byte to fail early            
        byte b = SkipWhiteSpaces();
        if (b != (byte)'"') return false;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (b != (byte)'$') return false;
        // No inlining here to reduce code size of the main deserialization loop,
        // since this code is only executed when a proposed type is provided, which is not the common case.
        return FindProposedType_Continuation(ref proposedTypeReaderRef, ref proposedTypeNameRef, expectedType, ref foundValueField, out b);
    }

    private bool FindProposedType_Continuation(ref CachedTypeReader proposedTypeReaderRef, ref ByteSegment proposedTypeNameRef, Type expectedType, ref bool foundValueField, out byte b)
    {
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'t') return false;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'y') return false;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'p') return false;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'e') return false;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'"') return false;
        buffer.TryNextByte();

        b = SkipWhiteSpaces();
        if (b != (byte)':') return false;
        buffer.TryNextByte();
        var proposedTypeBytes = ReadStringBytes();

        // 2. try get proposedTypeReader, first check if already present, if not check the cache, otherwise resolve type and get proposedTypeReader
        // Skip finding proposed type if it already found the last time.
        // This allows to avoid expensive type resolution and reader lookup in the common case where many objects of the same type are deserialized in a row.
        bool isProposedTypeCompatible = true;
        if (proposedTypeNameRef.IsValid && proposedTypeNameRef.Equals(proposedTypeBytes))
        {
            isProposedTypeCompatible = proposedTypeReaderRef != null && proposedTypeReaderRef.ReaderType.IsAssignableTo(expectedType);
        }
        else
        {
            proposedTypeBytes.EnsureHashCode();
            // Force a copy of the proposedTypeBytes so it can be safely used as dictionary key without worrying about buffer changes.                
            if (!proposedTypeReaderCache.TryGetValue(proposedTypeBytes, out var proposedTypeReader))
            {
                proposedTypeBytes = proposedTypeBytes.CropArray(true);
                proposedTypeReader = null;
                string proposedTypename = Encoding.UTF8.GetString(proposedTypeBytes.AsArraySegment.Array, proposedTypeBytes.AsArraySegment.Offset, proposedTypeBytes.AsArraySegment.Count);
                var proposedType = TypeNameHelper.Shared.GetTypeFromSimplifiedName(proposedTypename);
                if (proposedType == null)
                {
                    // Try old format with assembly name for backward compatibility
                    try
                    {
                        proposedType = Type.GetType(proposedTypename, false, true);
                    }
                    catch
                    {
                        // Ignore any exceptions from Type.GetType and treat it as type not found, to be consistent with TypeNameHelper behavior.
                    }
                }

                if (proposedType == null)
                {
                    proposedTypeReader = null;
                }
                else
                {
                    bool enforceWhitelist = settings.typeWhitelistMode != Settings.TypeWhitelistMode.Disabled;
                    if (enforceWhitelist && !IsWhitelistedType(proposedType))
                    {
                        if (expectedType.IsInterface || expectedType.IsAbstract)
                        {
                            throw new Exception(
                                $"Proposed type '{proposedTypename}' is not whitelisted and expected type '{expectedType.FullName}' is an interface or abstract class, " +
                                "which is not allowed for security reasons. Consider changing the expected type to a concrete class or adjust the type whitelist settings.");
                        }

                        // No early return here:
                        // proposed type is ignored, fallback to expected type reader later.
                        proposedTypeReader = null;
                    }
                    else if (proposedType != expectedType && proposedType.IsAssignableTo(expectedType))
                    {
                        proposedTypeReader = GetCachedTypeReader(proposedType);
                    }
                }

                proposedTypeReaderCache[proposedTypeBytes] = proposedTypeReader;
            }
            isProposedTypeCompatible = proposedTypeReader != null && proposedTypeReader.ReaderType.IsAssignableTo(expectedType);
            if (isProposedTypeCompatible)
            {
                proposedTypeReaderRef = proposedTypeReader;
                proposedTypeNameRef = proposedTypeBytes;
            }
        }

        // 3. look if next is $value field
        b = SkipWhiteSpaces();
        if (b != ',') return isProposedTypeCompatible;
        buffer.TryNextByte();

        // TODO compare byte per byte to fail early
        b = SkipWhiteSpaces();
        if (b != (byte)'"') return isProposedTypeCompatible;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (b != (byte)'$') return isProposedTypeCompatible;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'v') return isProposedTypeCompatible;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'a') return isProposedTypeCompatible;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'l') return isProposedTypeCompatible;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'u') return isProposedTypeCompatible;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) != (byte)'e') return isProposedTypeCompatible;
        buffer.TryNextByte();
        b = buffer.CurrentByte;
        if (FoldAsciiToLower(b) == (byte)'s')
        {
            buffer.TryNextByte();
            b = buffer.CurrentByte;
        }
        if (FoldAsciiToLower(b) != (byte)'"') return isProposedTypeCompatible;
        buffer.TryNextByte();

        b = SkipWhiteSpaces();
        if (b != (byte)':') return isProposedTypeCompatible;
        buffer.TryNextByte();

        // 4. $value field found
        foundValueField = true;
        return isProposedTypeCompatible;
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

    static readonly ByteSegment refFieldName = new ByteSegment("$ref".ToByteArray(), true);
    List<ByteSegment> fieldPathSegments = new List<ByteSegment>();

    bool TryReadRefObject<T>(out bool pathIsValid, out bool typeIsCompatible, out T refObject)
    {
        pathIsValid = false;
        typeIsCompatible = false;
        refObject = default;
        byte b = SkipWhiteSpaces();
        // first char must be '{', otherwise it's not an object and we can directly return false
        // without needing to reset buffer position
        if (b != (byte)'{') return false;

        using (var undoHandle = CreateUndoReadHandle())
        {
            bool refAttributeFound = Try(out pathIsValid, out typeIsCompatible, out refObject);
            undoHandle.SetUndoReading(!refAttributeFound);
            fieldPathSegments.Clear();
            return refAttributeFound;
        }

        bool Try(out bool pathIsValid, out bool typeIsCompatible, out T itemRef)
        {
            pathIsValid = false;
            typeIsCompatible = false;
            itemRef = default;
            // IMPORTANT: Currently, the ref-field must be the first, TODO: add option to allow it to be anywhere in the object (which is much slower)                                
            buffer.TryNextByte();
            // TODO compare byte per byte to fail early
            if (!TryReadStringBytes(out var fieldName)) return false;
            if (!refFieldName.Equals(fieldName)) return false;
            b = SkipWhiteSpaces();
            if (b != (byte)':') return false;
            buffer.TryNextByte();
            if (!TryReadStringBytes(out var refPath)) return false;
            b = SkipWhiteSpaces();
            if (b == ',') buffer.TryNextByte();

            // Skip the rest
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



            // TODO find object
            if (refPath.Count <= 0) return true;
            int pos = 0;
            int startPos = 0;
            int segmentLength = 0;
            int refPathCount = refPath.Count;
            b = refPath.AsArraySegment.Get(pos);

            while (true)
            {
                if (b == '[')
                {
                    while (true)
                    {
                        pos++;
                        if (pos >= refPathCount) return true;
                        b = refPath.AsArraySegment.Get(pos);
                        if (b == ']')
                        {
                            segmentLength = pos - startPos + 1;
                            pos++;
                            break;
                        }
                    }
                    ByteSegment segment = refPath.AsArraySegment.Slice(startPos, segmentLength);
                    fieldPathSegments.Add(segment);
                    if (pos >= refPathCount) break;
                    b = refPath.AsArraySegment.Get(pos);
                    if (b == '.')
                    {
                        pos++;
                        if (pos >= refPathCount) return true;
                    }
                }
                else
                {
                    while (true)
                    {
                        pos++;
                        if (pos >= refPathCount)
                        {
                            segmentLength = pos - startPos;
                            break;
                        }
                        b = refPath.AsArraySegment.Get(pos);
                        if (b == '.')
                        {
                            segmentLength = pos - startPos;
                            pos++;
                            break;
                        }
                        if (b == '[')
                        {
                            segmentLength = pos - startPos;
                            break;
                        }

                    }
                    ByteSegment segment = refPath.AsArraySegment.Slice(startPos, segmentLength);
                    fieldPathSegments.Add(segment);
                    if (pos >= refPathCount)
                    {
                        if (b == '.' || b == '[') return true;
                        break;
                    }
                }
                startPos = pos;
                b = refPath.AsArraySegment.Get(pos);
            }


            object potentialItemRef = null;
            int lastSegmentIndex = fieldPathSegments.Count - 1;
            var referencedFieldName = fieldPathSegments[lastSegmentIndex];
            foreach (var info in itemInfos)
            {
                if (info.name.Equals(referencedFieldName))
                {
                    potentialItemRef = info.itemRef;
                    int segmentIndex = lastSegmentIndex - 1;
                    int parentIndex = info.parentIndex;
                    ItemInfo parentInfo;
                    while (segmentIndex != -1 && parentIndex != -1)
                    {
                        var segment = fieldPathSegments[segmentIndex];
                        parentInfo = itemInfos[parentIndex];
                        if (!parentInfo.name.Equals(segment)) break;
                        parentIndex = parentInfo.parentIndex;
                        segmentIndex--;
                    }

                    pathIsValid = parentIndex == -1 && segmentIndex == -1;
                    if (pathIsValid) break;
                }
            }

            if (pathIsValid && potentialItemRef is T compatibleItemRef)
            {
                typeIsCompatible = true;
                itemRef = compatibleItemRef;
            }

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
    private static FilterResult Lookup(FilterResult[] map, byte index)
    {
        Debug.Assert(map != null && map.Length > byte.MaxValue);
        return map[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FilterResult Lookup(ref FilterResult map_firstElement, byte index)
    {
        return Unsafe.Add(ref map_firstElement, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TypeResult Lookup(TypeResult[] map, byte index)
    {
        Debug.Assert(map != null && map.Length > byte.MaxValue);
        return map[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TypeResult Lookup(ref TypeResult map_firstElement, byte index)
    {
        return Unsafe.Add(ref map_firstElement, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LookupCheck(FilterResult[] map, byte index, FilterResult comparant)
    {
        return comparant == map[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LookupCheck(ref FilterResult map_firstElement, byte index, FilterResult comparant)
    {
        return comparant == Unsafe.Add(ref map_firstElement, index);
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
