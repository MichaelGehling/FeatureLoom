using System;
using FeatureLoom.Helpers;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Reads and writes fixed-size index records according to a <see cref="LogIndexSchema"/>.
/// Record layout: [time offset 4 B][position][flags][custom fields], little-endian, no padding.
/// </summary>
public static class LogIndexRecordCodec
{
    /// <summary>Writes a full record at <paramref name="offset"/>.</summary>
    /// <param name="fieldValues">Stored values of all custom fields (raw values, hashes or handles), in schema order.</param>
    /// <exception cref="ArgumentException">A value does not fit its field.</exception>
    public static void Write(LogIndexSchema schema, byte[] buffer, int offset, uint timeOffset, ulong position, ulong flags, ushort typeId, ulong[] fieldValues)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));
        int fieldCount = schema.Fields.Count;
        if (fieldCount > 0 && (fieldValues == null || fieldValues.Length < fieldCount)) throw new ArgumentException("A value is required for every field.", nameof(fieldValues));
        if (offset < 0 || offset > buffer.Length - schema.RecordSize) throw new ArgumentOutOfRangeException(nameof(offset));

        int posSize = (int)schema.PositionSize;
        if (posSize == 4 && position > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(position), position, "Position exceeds the 4-byte limit.");
        if (schema.FlagSize < 8 && (flags >> (schema.FlagSize * 8)) != 0) throw new ArgumentOutOfRangeException(nameof(flags), flags, "Flags exceed the flag field.");
        if (typeId > schema.MaxTypeId) throw new ArgumentOutOfRangeException(nameof(typeId), typeId, "Type id exceeds the type id field.");

        LittleEndian.WriteUInt32(buffer, offset, timeOffset);
        LittleEndian.Write(buffer, offset + LogIndexSchema.TimeSize, position, posSize);
        LittleEndian.Write(buffer, offset + LogIndexSchema.TimeSize + posSize, flags, schema.FlagSize);
        if (schema.TypeIdSize > 0) LittleEndian.Write(buffer, offset + schema.TypeIdOffset, typeId, schema.TypeIdSize);
        for (int i = 0; i < fieldCount; i++)
        {
            var field = schema.Fields[i];
            ulong value = fieldValues[i];
            if (value > field.MaxValue) throw new ArgumentException($"Value {value} does not fit field '{field.Name}'.", nameof(fieldValues));
            LittleEndian.Write(buffer, offset + schema.GetFieldOffset(i), value, field.Size);
        }
    }

    /// <summary>Reads the time offset of a record.</summary>
    public static uint ReadTimeOffset(byte[] buffer, int offset) => LittleEndian.ReadUInt32(buffer, offset);

    /// <summary>Reads the payload position of a record.</summary>
    public static ulong ReadPosition(LogIndexSchema schema, byte[] buffer, int offset) =>
        LittleEndian.Read(buffer, offset + LogIndexSchema.TimeSize, (int)schema.PositionSize);

    /// <summary>Reads the flags of a record.</summary>
    public static ulong ReadFlags(LogIndexSchema schema, byte[] buffer, int offset) =>
        LittleEndian.Read(buffer, offset + LogIndexSchema.TimeSize + (int)schema.PositionSize, schema.FlagSize);

    /// <summary>Overwrites the flags of a record in place (e.g. to set <see cref="LogReservedFlags.Deleted"/>).</summary>
    public static void WriteFlags(LogIndexSchema schema, byte[] buffer, int offset, ulong flags) =>
        LittleEndian.Write(buffer, offset + LogIndexSchema.TimeSize + (int)schema.PositionSize, flags, schema.FlagSize);

    /// <summary>Reads the payload type id of a record (0 if the schema has no type id field).</summary>
    public static ushort ReadTypeId(LogIndexSchema schema, byte[] buffer, int offset) =>
        schema.TypeIdSize == 0 ? (ushort)0 : (ushort)LittleEndian.Read(buffer, offset + schema.TypeIdOffset, schema.TypeIdSize);

    /// <summary>Reads the stored value of a custom field.</summary>
    public static ulong ReadField(LogIndexSchema schema, byte[] buffer, int offset, int fieldIndex) =>
        LittleEndian.Read(buffer, offset + schema.GetFieldOffset(fieldIndex), schema.Fields[fieldIndex].Size);
}
