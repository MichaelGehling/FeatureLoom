using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FeatureLoom.Helpers;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Header of an index (.idx) file. Self-describing: carries format version, schema, segment sequence, reference time
/// and segment settings. Protected by a CRC-32.
/// </summary>
/// <remarks>
/// Layout (little-endian): magic "FLIX" | u16 version | u32 header size | u16 settings | u32 segment seq |
/// i64 base time ticks (UTC) | i64 time resolution ticks | u8 position size | u8 header mode | u8 framing |
/// u8 flag count | u8 type id size | u16 field count | flag names (u16 len + UTF-8) | fields (u8 kind, u8 size, u16 len + UTF-8 name) | u32 CRC.
/// </remarks>
public sealed class LogIndexFileHeader
{
    /// <summary>File magic "FLIX".</summary>
    public const uint Magic = 0x58494C46;

    /// <summary>Current format version.</summary>
    public const ushort CurrentVersion = 1;

    private const ushort SecureDeleteSetting = 1 << 0;
    private const int FixedSize = 4 + 2 + 4 + 2 + 4 + 8 + 8 + 1 + 1 + 1 + 1 + 1 + 2;
    private const int MaxNameBytes = ushort.MaxValue;

    /// <summary>Creates a header.</summary>
    public LogIndexFileHeader(LogIndexSchema schema, uint segmentSequence, DateTime baseTime, bool secureDelete, ushort version = CurrentVersion)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        SegmentSequence = segmentSequence;
        BaseTime = DateTime.SpecifyKind(baseTime.ToUniversalTime(), DateTimeKind.Utc);
        SecureDelete = secureDelete;
        Version = version;
    }

    /// <summary>Format version.</summary>
    public ushort Version { get; }

    /// <summary>Index schema of the segment.</summary>
    public LogIndexSchema Schema { get; }

    /// <summary>Sequence number of the segment.</summary>
    public uint SegmentSequence { get; }

    /// <summary>Reference time; index time offsets are relative to it.</summary>
    public DateTime BaseTime { get; }

    /// <summary>Payloads of deleted entries are zeroed (see <see cref="LogReservedFlags.Deleted"/>).</summary>
    public bool SecureDelete { get; set; }

    /// <summary>Converts a timestamp to an index time offset.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Timestamp is outside the representable range.</exception>
    public uint ToTimeOffset(DateTime timestamp)
    {
        long delta = timestamp.ToUniversalTime().Ticks - BaseTime.Ticks;
        if (delta < 0) throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, "Timestamp is before the base time.");
        long offset = delta / Schema.TimeResolution.Ticks;
        if (offset > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, "Timestamp exceeds the time range of the segment.");
        return (uint)offset;
    }

    /// <summary>Converts an index time offset to a UTC timestamp.</summary>
    public DateTime FromTimeOffset(uint offset) => new DateTime(BaseTime.Ticks + offset * Schema.TimeResolution.Ticks, DateTimeKind.Utc);

    /// <summary>Serializes the header. The returned array length equals the header size.</summary>
    public byte[] ToBytes()
    {
        var utf8 = Encoding.UTF8;
        int size = FixedSize + 4;
        foreach (var name in Schema.FlagNames) size += 2 + GetNameLength(name);
        foreach (var field in Schema.Fields) size += 2 + 2 + GetNameLength(field.Name);

        var buffer = new byte[size];
        int pos = 0;
        LittleEndian.WriteUInt32(buffer, pos, Magic); pos += 4;
        LittleEndian.WriteUInt16(buffer, pos, Version); pos += 2;
        LittleEndian.WriteUInt32(buffer, pos, (uint)size); pos += 4;
        LittleEndian.WriteUInt16(buffer, pos, SecureDelete ? SecureDeleteSetting : (ushort)0); pos += 2;
        LittleEndian.WriteUInt32(buffer, pos, SegmentSequence); pos += 4;
        LittleEndian.WriteUInt64(buffer, pos, (ulong)BaseTime.Ticks); pos += 8;
        LittleEndian.WriteUInt64(buffer, pos, (ulong)Schema.TimeResolution.Ticks); pos += 8;
        buffer[pos++] = (byte)Schema.PositionSize;
        buffer[pos++] = (byte)Schema.HeaderMode;
        buffer[pos++] = (byte)Schema.Framing;
        buffer[pos++] = (byte)Schema.FlagNames.Count;
        buffer[pos++] = (byte)Schema.TypeIdSize;
        LittleEndian.WriteUInt16(buffer, pos, (ushort)Schema.Fields.Count); pos += 2;
        foreach (var name in Schema.FlagNames) pos = WriteName(buffer, pos, name, utf8);
        foreach (var field in Schema.Fields)
        {
            buffer[pos++] = (byte)field.Kind;
            buffer[pos++] = (byte)field.Size;
            pos = WriteName(buffer, pos, field.Name, utf8);
        }
        LittleEndian.WriteUInt32(buffer, pos, Crc32.Compute(buffer, 0, pos));
        return buffer;
    }

    /// <summary>Reads a header from the start of <paramref name="stream"/>.</summary>
    /// <exception cref="InvalidDataException">The header is truncated, corrupt or of an unsupported version.</exception>
    public static LogIndexFileHeader Read(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        var prefix = new byte[10];
        ReadExactly(stream, prefix, 0, prefix.Length);
        if (LittleEndian.ReadUInt32(prefix, 0) != Magic) throw new InvalidDataException("Not a log index file (magic mismatch).");
        ushort version = LittleEndian.ReadUInt16(prefix, 4);
        if (version != CurrentVersion) throw new InvalidDataException($"Unsupported index file version {version}.");
        uint size = LittleEndian.ReadUInt32(prefix, 6);
        if (size < FixedSize + 4 || size > 16 * 1024 * 1024) throw new InvalidDataException($"Invalid index header size {size}.");

        var buffer = new byte[size];
        Array.Copy(prefix, buffer, prefix.Length);
        ReadExactly(stream, buffer, prefix.Length, buffer.Length - prefix.Length);
        return Parse(buffer);
    }

    /// <summary>Parses a serialized header.</summary>
    /// <exception cref="InvalidDataException">The header is truncated, corrupt or of an unsupported version.</exception>
    public static LogIndexFileHeader Parse(byte[] buffer)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (buffer.Length < FixedSize + 4) throw new InvalidDataException("Index header is truncated.");
        if (LittleEndian.ReadUInt32(buffer, 0) != Magic) throw new InvalidDataException("Not a log index file (magic mismatch).");
        ushort version = LittleEndian.ReadUInt16(buffer, 4);
        if (version != CurrentVersion) throw new InvalidDataException($"Unsupported index file version {version}.");
        uint size = LittleEndian.ReadUInt32(buffer, 6);
        if (size < FixedSize + 4 || size > buffer.Length) throw new InvalidDataException("Index header is truncated.");
        int crcPos = (int)size - 4;
        if (Crc32.Compute(buffer, 0, crcPos) != LittleEndian.ReadUInt32(buffer, crcPos)) throw new InvalidDataException("Index header checksum mismatch.");

        try
        {
            var utf8 = Encoding.UTF8;
            int pos = 10;
            ushort settings = LittleEndian.ReadUInt16(buffer, pos); pos += 2;
            uint seq = LittleEndian.ReadUInt32(buffer, pos); pos += 4;
            long baseTicks = (long)LittleEndian.ReadUInt64(buffer, pos); pos += 8;
            long resolutionTicks = (long)LittleEndian.ReadUInt64(buffer, pos); pos += 8;
            var positionSize = (LogPositionSize)buffer[pos++];
            var headerMode = (LogHeaderMode)buffer[pos++];
            var framing = (LogRecordFraming)buffer[pos++];
            int flagCount = buffer[pos++];
            int typeIdSize = buffer[pos++];
            int fieldCount = LittleEndian.ReadUInt16(buffer, pos); pos += 2;

            var flagNames = new string[flagCount];
            for (int i = 0; i < flagCount; i++) flagNames[i] = ReadName(buffer, ref pos, crcPos, utf8);
            var fields = new LogFieldDefinition[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                if (pos + 2 > crcPos) throw new InvalidDataException("Index header is truncated.");
                var kind = (LogFieldKind)buffer[pos++];
                int fieldSize = buffer[pos++];
                fields[i] = new LogFieldDefinition(ReadName(buffer, ref pos, crcPos, utf8), kind, fieldSize);
            }
            if (pos != crcPos) throw new InvalidDataException("Index header size mismatch.");

            var schema = new LogIndexSchema(flagNames, fields, TimeSpan.FromTicks(resolutionTicks), positionSize, headerMode, framing, typeIdSize);
            return new LogIndexFileHeader(schema, seq, new DateTime(baseTicks, DateTimeKind.Utc), (settings & SecureDeleteSetting) != 0, version);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException("Index header contains an invalid schema.", e);
        }
    }

    private static int GetNameLength(string name)
    {
        int length = Encoding.UTF8.GetByteCount(name);
        if (length > MaxNameBytes) throw new InvalidOperationException($"Name '{name.Substring(0, 32)}...' is too long.");
        return length;
    }

    private static int WriteName(byte[] buffer, int pos, string name, Encoding utf8)
    {
        int length = utf8.GetBytes(name, 0, name.Length, buffer, pos + 2);
        LittleEndian.WriteUInt16(buffer, pos, (ushort)length);
        return pos + 2 + length;
    }

    private static string ReadName(byte[] buffer, ref int pos, int end, Encoding utf8)
    {
        if (pos + 2 > end) throw new InvalidDataException("Index header is truncated.");
        int length = LittleEndian.ReadUInt16(buffer, pos);
        pos += 2;
        if (pos + length > end) throw new InvalidDataException("Index header is truncated.");
        string name = utf8.GetString(buffer, pos, length);
        pos += length;
        return name;
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read <= 0) throw new InvalidDataException("Index header is truncated.");
            offset += read;
            count -= read;
        }
    }
}
