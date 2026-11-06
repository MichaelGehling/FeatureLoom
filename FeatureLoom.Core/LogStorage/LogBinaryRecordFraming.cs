using System;
using System.IO;
using System.Text;
using FeatureLoom.Collections;
using FeatureLoom.Helpers;

namespace FeatureLoom.LogStorage;

/// <summary>Result of decoding a binary-framed segment record.</summary>
public enum LogRecordReadStatus
{
    /// <summary>Record decoded successfully.</summary>
    Ok,
    /// <summary>Not enough data; typically a torn write at the segment tail.</summary>
    Truncated,
    /// <summary>Checksum or structure mismatch.</summary>
    Corrupt
}

/// <summary>Decoded binary record. Payload and metadata refer to the source buffer (no copy).</summary>
public struct LogRecord
{
    /// <summary>Timestamp from the header (<see cref="LogHeaderMode.Full"/> only).</summary>
    public DateTime Timestamp;
    /// <summary>Flags from the header (<see cref="LogHeaderMode.Full"/> only).</summary>
    public ulong Flags;
    /// <summary>Payload type id from the header (<see cref="LogHeaderMode.Full"/> only).</summary>
    public ushort TypeId;
    /// <summary>Raw values of the custom fields from the header (<see cref="LogHeaderMode.Full"/> only).</summary>
    public ulong[] FieldValues;
    /// <summary>Source values of Hash/Handle fields (<see cref="LogHeaderMode.Full"/> only; caller-supplied array).</summary>
    public string[] FieldSources;
    /// <summary>Payload view into the source buffer.</summary>
    public ByteSegment Payload;
    /// <summary>Total number of bytes the record occupies.</summary>
    public int RecordLength;
}

/// <summary>
/// Binary record framing of segment files.
/// <list type="bullet">
/// <item><see cref="LogHeaderMode.None"/>: payload only (length comes from the index).</item>
/// <item><see cref="LogHeaderMode.Minimal"/>: u32 payload length | u32 CRC(payload) | payload.</item>
/// <item><see cref="LogHeaderMode.Full"/>: u32 payload length | u32 CRC(meta + payload) | u16 meta length |
/// meta (i64 ticks, u64 flags, u16 type id, per field value) | payload.</item>
/// </list>
/// </summary>
public static class LogBinaryRecordFraming
{
    private const int MinimalHeaderSize = 8;
    private const int FullFixedHeaderSize = 10;
    private const int FullFixedMetaSize = 18;

    /// <summary>Computes the size of the record header (everything before the payload).</summary>
    public static int GetHeaderSize(LogIndexSchema schema, LogEntryMetadata metadata)
    {
        switch (schema.HeaderMode)
        {
            case LogHeaderMode.None: return 0;
            case LogHeaderMode.Minimal: return MinimalHeaderSize;
            default: return FullFixedHeaderSize + GetMetaSize(schema, metadata, null);
        }
    }

    // Variant using a per-writer cache of UTF-8 encoded source strings.
    internal static int GetHeaderSize(LogIndexSchema schema, LogEntryMetadata metadata, LogSourceEncodingCache sourceCache)
    {
        switch (schema.HeaderMode)
        {
            case LogHeaderMode.None: return 0;
            case LogHeaderMode.Minimal: return MinimalHeaderSize;
            default: return FullFixedHeaderSize + GetMetaSize(schema, metadata, sourceCache);
        }
    }

    /// <summary>
    /// Writes a record into <paramref name="buffer"/> at <paramref name="offset"/> and returns the number of bytes written.
    /// <paramref name="metadata"/> is only used in <see cref="LogHeaderMode.Full"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The buffer is too small.</exception>
    public static int Write(LogIndexSchema schema, byte[] buffer, int offset, LogEntryMetadata metadata, ByteSegment payload)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        return Write(schema, buffer, offset, metadata, payload, GetHeaderSize(schema, metadata), null);
    }

    // Variant for callers that already computed the header size (avoids rescanning source strings).
    // sourceCache (optional) must be the same cache that was passed to GetHeaderSize.
    internal static int Write(LogIndexSchema schema, byte[] buffer, int offset, LogEntryMetadata metadata, ByteSegment payload, int headerSize, LogSourceEncodingCache sourceCache)
    {
        if (!payload.IsValid) throw new ArgumentException("Payload is invalid.", nameof(payload));
        var payloadSegment = payload.AsArraySegment;
        int payloadCount = payloadSegment.Count;
        int total = headerSize + payloadCount;
        if (offset < 0 || offset > buffer.Length - total) throw new ArgumentException("Buffer is too small.", nameof(buffer));

        Array.Copy(payloadSegment.Array, payloadSegment.Offset, buffer, offset + headerSize, payloadCount);
        if (schema.HeaderMode == LogHeaderMode.None) return total;

        LittleEndian.WriteUInt32(buffer, offset, (uint)payloadCount);
        if (schema.HeaderMode == LogHeaderMode.Minimal)
        {
            LittleEndian.WriteUInt32(buffer, offset + 4, Crc32.Compute(buffer, offset + headerSize, payloadCount));
            return total;
        }

        if (metadata == null) throw new ArgumentNullException(nameof(metadata), "Full header mode requires metadata.");
        int metaSize = headerSize - FullFixedHeaderSize;
        if (metaSize > ushort.MaxValue) throw new ArgumentException("Record metadata is too large.", nameof(metadata));
        LittleEndian.WriteUInt16(buffer, offset + 8, (ushort)metaSize);
        int pos = offset + FullFixedHeaderSize;
        LittleEndian.WriteUInt64(buffer, pos, (ulong)metadata.Timestamp.ToUniversalTime().Ticks); pos += 8;
        LittleEndian.WriteUInt64(buffer, pos, metadata.Flags); pos += 8;
        LittleEndian.WriteUInt16(buffer, pos, metadata.TypeId); pos += 2;
        for (int i = 0; i < schema.Fields.Count; i++)
        {
            if (IsSourceField(schema.Fields[i].Kind))
            {
                string source = metadata.GetSource(i) ?? string.Empty;
                int length;
                if (sourceCache != null)
                {
                    length = sourceCache.GetLength(i, source);
                    Buffer.BlockCopy(sourceCache.GetBuffer(i), 0, buffer, pos + 2, length);
                }
                else length = Encoding.UTF8.GetBytes(source, 0, source.Length, buffer, pos + 2);
                LittleEndian.WriteUInt16(buffer, pos, (ushort)length);
                pos += 2 + length;
            }
            else
            {
                LittleEndian.WriteUInt64(buffer, pos, metadata.GetValue(i));
                pos += 8;
            }
        }
        LittleEndian.WriteUInt32(buffer, offset + 4, Crc32.Compute(buffer, offset + FullFixedHeaderSize, metaSize + payloadCount));
        return total;
    }

    /// <summary>
    /// Decodes a record starting at <paramref name="offset"/>; <paramref name="count"/> is the available data.
    /// For <see cref="LogHeaderMode.None"/>, <paramref name="count"/> must be the payload length from the index.
    /// In <see cref="LogHeaderMode.Full"/>, <c>FieldValues</c>/<c>FieldSources</c> of <paramref name="record"/> are filled if set and large enough.
    /// </summary>
    public static LogRecordReadStatus TryRead(LogIndexSchema schema, byte[] buffer, int offset, int count, ref LogRecord record)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        if (schema.HeaderMode == LogHeaderMode.None)
        {
            record.Payload = new ByteSegment(buffer, offset, count);
            record.RecordLength = count;
            return LogRecordReadStatus.Ok;
        }

        int fixedSize = schema.HeaderMode == LogHeaderMode.Minimal ? MinimalHeaderSize : FullFixedHeaderSize;
        if (count < fixedSize) return LogRecordReadStatus.Truncated;
        uint payloadLength = LittleEndian.ReadUInt32(buffer, offset);
        uint crc = LittleEndian.ReadUInt32(buffer, offset + 4);
        int metaSize = schema.HeaderMode == LogHeaderMode.Full ? LittleEndian.ReadUInt16(buffer, offset + 8) : 0;
        long total = fixedSize + metaSize + (long)payloadLength;
        if (total > count) return LogRecordReadStatus.Truncated;

        int checkedStart = offset + fixedSize;
        if (Crc32.Compute(buffer, checkedStart, metaSize + (int)payloadLength) != crc) return LogRecordReadStatus.Corrupt;

        record.Payload = new ByteSegment(buffer, checkedStart + metaSize, (int)payloadLength);
        record.RecordLength = (int)total;
        if (schema.HeaderMode == LogHeaderMode.Minimal) return LogRecordReadStatus.Ok;

        if (metaSize < FullFixedMetaSize) return LogRecordReadStatus.Corrupt;
        int pos = checkedStart;
        int end = checkedStart + metaSize;
        long ticks = (long)LittleEndian.ReadUInt64(buffer, pos); pos += 8;
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return LogRecordReadStatus.Corrupt;
        record.Timestamp = new DateTime(ticks, DateTimeKind.Utc);
        record.Flags = LittleEndian.ReadUInt64(buffer, pos); pos += 8;
        record.TypeId = LittleEndian.ReadUInt16(buffer, pos); pos += 2;
        for (int i = 0; i < schema.Fields.Count; i++)
        {
            if (IsSourceField(schema.Fields[i].Kind))
            {
                if (pos + 2 > end) return LogRecordReadStatus.Corrupt;
                int length = LittleEndian.ReadUInt16(buffer, pos);
                pos += 2;
                if (pos + length > end) return LogRecordReadStatus.Corrupt;
                if (record.FieldSources != null && i < record.FieldSources.Length) record.FieldSources[i] = Encoding.UTF8.GetString(buffer, pos, length);
                pos += length;
            }
            else
            {
                if (pos + 8 > end) return LogRecordReadStatus.Corrupt;
                if (record.FieldValues != null && i < record.FieldValues.Length) record.FieldValues[i] = LittleEndian.ReadUInt64(buffer, pos);
                pos += 8;
            }
        }
        return pos == end ? LogRecordReadStatus.Ok : LogRecordReadStatus.Corrupt;
    }

    private static int GetMetaSize(LogIndexSchema schema, LogEntryMetadata metadata, LogSourceEncodingCache sourceCache)
    {
        int size = FullFixedMetaSize;
        for (int i = 0; i < schema.Fields.Count; i++)
        {
            if (IsSourceField(schema.Fields[i].Kind))
            {
                string source = metadata?.GetSource(i);
                int length = source == null ? 0 : sourceCache != null ? sourceCache.GetLength(i, source) : Encoding.UTF8.GetByteCount(source);
                if (length > ushort.MaxValue) throw new ArgumentException($"Source value of field '{schema.Fields[i].Name}' is too long.", nameof(metadata));
                size += 2 + length;
            }
            else size += 8;
        }
        return size;
    }

    private static bool IsSourceField(LogFieldKind kind) => kind == LogFieldKind.Hash || kind == LogFieldKind.Handle;
}

/// <summary>
/// Caches the UTF-8 encoding of the last source string per field. Sources (e.g. logger names) usually repeat
/// from entry to entry, so a reference check avoids re-encoding. On a miss the string is encoded into a reused
/// per-field buffer, so changing sources do not allocate. Not thread-safe; owned by a single writer.
/// </summary>
internal sealed class LogSourceEncodingCache
{
    private string[] sources = new string[0];
    private byte[][] buffers = new byte[0][];
    private int[] lengths = new int[0];

    /// <summary>Returns the UTF-8 length of <paramref name="source"/>; the bytes are available via <see cref="GetBuffer"/>.</summary>
    public int GetLength(int field, string source)
    {
        if (field >= sources.Length)
        {
            Array.Resize(ref sources, field + 1);
            Array.Resize(ref buffers, field + 1);
            Array.Resize(ref lengths, field + 1);
        }
        // Reference equality suffices: strings are immutable and repeated sources are typically the same instance.
        if (ReferenceEquals(sources[field], source)) return lengths[field];
        var buffer = buffers[field];
        int maxBytes = Encoding.UTF8.GetMaxByteCount(source.Length);
        if (buffer == null || buffer.Length < maxBytes) buffers[field] = buffer = new byte[Math.Max(maxBytes, 32)];
        int length = Encoding.UTF8.GetBytes(source, 0, source.Length, buffer, 0);
        sources[field] = source;
        lengths[field] = length;
        return length;
    }

    /// <summary>Buffer holding the encoded bytes of the last source passed to <see cref="GetLength"/> for the field.</summary>
    public byte[] GetBuffer(int field) => buffers[field];
}
