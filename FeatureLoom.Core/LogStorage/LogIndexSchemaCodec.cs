using System;
using System.IO;
using System.Text;
using FeatureLoom.Helpers;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Binary encoding of a <see cref="LogIndexSchema"/>, shared by the index file header and the schema file.
/// </summary>
/// <remarks>
/// Layout (little-endian): i64 time resolution ticks | u8 position size | u8 header mode | u8 framing | u8 flag count |
/// u8 type id size | u16 field count | flag names (u16 len + UTF-8) | fields (u8 kind, u8 size, u16 len + UTF-8 name).
/// </remarks>
public static class LogIndexSchemaCodec
{
    private const int FixedSize = 8 + 1 + 1 + 1 + 1 + 1 + 2;

    /// <summary>Returns the encoded size of <paramref name="schema"/> in bytes.</summary>
    /// <exception cref="InvalidOperationException">A name exceeds 65535 UTF-8 bytes.</exception>
    public static int GetSize(LogIndexSchema schema)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        int size = FixedSize;
        foreach (var name in schema.FlagNames) size += 2 + GetNameLength(name);
        foreach (var field in schema.Fields) size += 2 + 2 + GetNameLength(field.Name);
        return size;
    }

    /// <summary>True if both schemas have the same binary layout (same encoded bytes).</summary>
    public static bool AreEqual(LogIndexSchema a, LogIndexSchema b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        int size = GetSize(a);
        if (size != GetSize(b)) return false;
        var bytesA = new byte[size];
        var bytesB = new byte[size];
        Write(a, bytesA, 0);
        Write(b, bytesB, 0);
        for (int i = 0; i < size; i++)
        {
            if (bytesA[i] != bytesB[i]) return false;
        }
        return true;
    }

    /// <summary>Writes <paramref name="schema"/> at <paramref name="pos"/> and returns the position after it.</summary>
    public static int Write(LogIndexSchema schema, byte[] buffer, int pos)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        var utf8 = Encoding.UTF8;
        LittleEndian.WriteUInt64(buffer, pos, (ulong)schema.TimeResolution.Ticks); pos += 8;
        buffer[pos++] = (byte)schema.PositionSize;
        buffer[pos++] = (byte)schema.HeaderMode;
        buffer[pos++] = (byte)schema.Framing;
        buffer[pos++] = (byte)schema.FlagNames.Count;
        buffer[pos++] = (byte)schema.TypeIdSize;
        LittleEndian.WriteUInt16(buffer, pos, (ushort)schema.Fields.Count); pos += 2;
        foreach (var name in schema.FlagNames) pos = WriteName(buffer, pos, name, utf8);
        foreach (var field in schema.Fields)
        {
            buffer[pos++] = (byte)field.Kind;
            buffer[pos++] = (byte)field.Size;
            pos = WriteName(buffer, pos, field.Name, utf8);
        }
        return pos;
    }

    /// <summary>Reads a schema starting at <paramref name="pos"/>; never reads at or beyond <paramref name="end"/>.</summary>
    /// <exception cref="InvalidDataException">The data is truncated or describes an invalid schema.</exception>
    public static LogIndexSchema Read(byte[] buffer, ref int pos, int end)
    {
        if (pos + FixedSize > end) throw new InvalidDataException("Schema is truncated.");
        try
        {
            var utf8 = Encoding.UTF8;
            long resolutionTicks = (long)LittleEndian.ReadUInt64(buffer, pos); pos += 8;
            var positionSize = (LogPositionSize)buffer[pos++];
            var headerMode = (LogHeaderMode)buffer[pos++];
            var framing = (LogRecordFraming)buffer[pos++];
            int flagCount = buffer[pos++];
            int typeIdSize = buffer[pos++];
            int fieldCount = LittleEndian.ReadUInt16(buffer, pos); pos += 2;

            var flagNames = new string[flagCount];
            for (int i = 0; i < flagCount; i++) flagNames[i] = ReadName(buffer, ref pos, end, utf8);
            var fields = new LogFieldDefinition[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                if (pos + 2 > end) throw new InvalidDataException("Schema is truncated.");
                var kind = (LogFieldKind)buffer[pos++];
                int fieldSize = buffer[pos++];
                fields[i] = new LogFieldDefinition(ReadName(buffer, ref pos, end, utf8), kind, fieldSize);
            }
            return new LogIndexSchema(flagNames, fields, TimeSpan.FromTicks(resolutionTicks), positionSize, headerMode, framing, typeIdSize);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException("Schema data is invalid.", e);
        }
    }

    private static int GetNameLength(string name)
    {
        int length = Encoding.UTF8.GetByteCount(name);
        if (length > ushort.MaxValue) throw new InvalidOperationException($"Name '{name.Substring(0, 32)}...' is too long.");
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
        if (pos + 2 > end) throw new InvalidDataException("Schema is truncated.");
        int length = LittleEndian.ReadUInt16(buffer, pos);
        pos += 2;
        if (pos + length > end) throw new InvalidDataException("Schema is truncated.");
        string name = utf8.GetString(buffer, pos, length);
        pos += length;
        return name;
    }
}
