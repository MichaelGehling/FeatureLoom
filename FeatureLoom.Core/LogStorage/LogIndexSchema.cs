using System;
using System.Collections.Generic;

namespace FeatureLoom.LogStorage;

/// <summary>How much metadata is stored in front of each payload in the segment file.</summary>
public enum LogHeaderMode : byte
{
    /// <summary>Timestamp, flags, field source values, length and checksum. Fully recoverable.</summary>
    Full = 0,
    /// <summary>Length and checksum only. Corruption detection and tail recovery, no index rebuild.</summary>
    Minimal = 1,
    /// <summary>Payload only. The index is the only source of metadata.</summary>
    None = 2
}

/// <summary>How records are framed in segment files.</summary>
public enum LogRecordFraming : byte
{
    /// <summary>Compact binary framing.</summary>
    Binary = 0,
    /// <summary>Human/tool friendly line-based framing.</summary>
    Text = 1
}

/// <summary>Size of the payload start position in an index record.</summary>
public enum LogPositionSize : byte
{
    /// <summary>4 bytes: entry start positions must be below 4 GiB.</summary>
    Bytes4 = 4,
    /// <summary>8 bytes: for huge segments or entries.</summary>
    Bytes8 = 8
}

/// <summary>Flags reserved by the log storage. They occupy the lowest bits of the flag field.</summary>
public static class LogReservedFlags
{
    /// <summary>
    /// Entry is deleted. If the segment header marks secure delete, the payload of every deleted entry is (or, after a crash,
    /// will be during recovery) overwritten with zeros; there is no per-entry "zeroed" flag because zeroing is idempotent.
    /// </summary>
    public const ulong Deleted = 1UL << 0;
    /// <summary>Entry was carried forward from a previous segment.</summary>
    public const ulong Carried = 1UL << 1;
    /// <summary>Number of reserved flag bits.</summary>
    public const int Count = 2;
}

/// <summary>
/// Layout of the fixed-size index records of a log. Defined at log creation and stored in every index file header.
/// Record layout: [time offset 4 B][position 4|8 B][flags 1|2|4|8 B][type id 0|1|2 B][custom fields in definition order]. No padding.
/// </summary>
public sealed class LogIndexSchema
{
    /// <summary>Default time resolution (100 µs, ~4.97 days range per segment).</summary>
    public static readonly TimeSpan DefaultTimeResolution = TimeSpan.FromTicks(1000);

    /// <summary>Maximum supported time resolution.</summary>
    public static readonly TimeSpan MaxTimeResolution = TimeSpan.FromMinutes(1);

    /// <summary>Maximum number of user-defined flags.</summary>
    public const int MaxUserFlags = 64 - LogReservedFlags.Count;

    /// <summary>Size of the time offset in bytes.</summary>
    public const int TimeSize = 4;

    private readonly string[] flagNames;
    private readonly LogFieldDefinition[] fields;
    private readonly int[] fieldOffsets;

    /// <summary>User-defined flag names. Flag i uses bit (<see cref="LogReservedFlags.Count"/> + i).</summary>
    public IReadOnlyList<string> FlagNames => flagNames;

    /// <summary>Custom field definitions in record order.</summary>
    public IReadOnlyList<LogFieldDefinition> Fields => fields;

    /// <summary>Resolution of the index time offset.</summary>
    public TimeSpan TimeResolution { get; }

    /// <summary>Size of the position field.</summary>
    public LogPositionSize PositionSize { get; }

    /// <summary>Header mode of segment records.</summary>
    public LogHeaderMode HeaderMode { get; }

    /// <summary>Record framing of segment files.</summary>
    public LogRecordFraming Framing { get; }

    /// <summary>Size of the flag field in bytes (1, 2, 4 or 8).</summary>
    public int FlagSize { get; }

    /// <summary>Size of the payload type id in bytes (0 = single payload type with id 0, 1 or 2).</summary>
    public int TypeIdSize { get; }

    /// <summary>Byte offset of the type id within an index record.</summary>
    public int TypeIdOffset => TimeSize + (int)PositionSize + FlagSize;

    /// <summary>Largest payload type id that fits into <see cref="TypeIdSize"/>.</summary>
    public ushort MaxTypeId => TypeIdSize == 0 ? (ushort)0 : TypeIdSize == 1 ? byte.MaxValue : ushort.MaxValue;

    /// <summary>Total size of one index record in bytes.</summary>
    public int RecordSize { get; }

    /// <summary>Maximum time span covered by the 4-byte time offset.</summary>
    public TimeSpan MaxTimeRange => TimeSpan.FromTicks(TimeResolution.Ticks * uint.MaxValue);

    /// <summary>Creates and validates a schema.</summary>
    /// <param name="flagNames">User-defined flag names (unique, max <see cref="MaxUserFlags"/>).</param>
    /// <param name="fields">Custom fields (unique names).</param>
    /// <param name="timeResolution">Time offset resolution; default <see cref="DefaultTimeResolution"/>.</param>
    /// <param name="positionSize">Size of the position field.</param>
    /// <param name="headerMode">Header mode of segment records.</param>
    /// <param name="framing">Record framing of segment files.</param>
    /// <param name="typeIdSize">Size of the payload type id (0, 1 or 2 bytes). Use 0 if the log holds a single payload type.</param>
    /// <exception cref="ArgumentException">The combination is invalid.</exception>
    public LogIndexSchema(IEnumerable<string> flagNames = null,
                          IEnumerable<LogFieldDefinition> fields = null,
                          TimeSpan? timeResolution = null,
                          LogPositionSize positionSize = LogPositionSize.Bytes4,
                          LogHeaderMode headerMode = LogHeaderMode.Full,
                          LogRecordFraming framing = LogRecordFraming.Binary,
                          int typeIdSize = 0)
    {
        this.flagNames = flagNames != null ? new List<string>(flagNames).ToArray() : Array.Empty<string>();
        this.fields = fields != null ? new List<LogFieldDefinition>(fields).ToArray() : Array.Empty<LogFieldDefinition>();
        TimeResolution = timeResolution ?? DefaultTimeResolution;
        PositionSize = positionSize;
        HeaderMode = headerMode;
        Framing = framing;
        if (typeIdSize < 0 || typeIdSize > 2) throw new ArgumentOutOfRangeException(nameof(typeIdSize), typeIdSize, "Type id size must be 0, 1 or 2.");
        TypeIdSize = typeIdSize;

        Validate();

        int totalFlags = LogReservedFlags.Count + this.flagNames.Length;
        FlagSize = totalFlags <= 8 ? 1 : totalFlags <= 16 ? 2 : totalFlags <= 32 ? 4 : 8;

        fieldOffsets = new int[this.fields.Length];
        int offset = TimeSize + (int)PositionSize + FlagSize + TypeIdSize;
        for (int i = 0; i < this.fields.Length; i++)
        {
            fieldOffsets[i] = offset;
            offset += this.fields[i].Size;
        }
        RecordSize = offset;
    }

    private void Validate()
    {
        if (TimeResolution <= TimeSpan.Zero || TimeResolution > MaxTimeResolution)
            throw new ArgumentOutOfRangeException("timeResolution", TimeResolution, $"Time resolution must be > 0 and <= {MaxTimeResolution}.");
        if (PositionSize != LogPositionSize.Bytes4 && PositionSize != LogPositionSize.Bytes8)
            throw new ArgumentOutOfRangeException("positionSize", PositionSize, "Unsupported position size.");
        if (flagNames.Length > MaxUserFlags)
            throw new ArgumentException($"At most {MaxUserFlags} user flags are supported.", "flagNames");

        var flagSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in flagNames)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Flag names must not be empty.", "flagNames");
            if (!flagSet.Add(name)) throw new ArgumentException($"Duplicate flag name '{name}'.", "flagNames");
        }

        var fieldSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (field == null) throw new ArgumentException("Field definitions must not be null.", "fields");
            if (!fieldSet.Add(field.Name)) throw new ArgumentException($"Duplicate field name '{field.Name}'.", "fields");
            // Hash matches and overflow handles must be verified against the source value, which only the full header stores.
            // Verifying via the payload type's source extractor instead could relax this rule later.
            if (HeaderMode != LogHeaderMode.Full && (field.Kind == LogFieldKind.Hash || field.Kind == LogFieldKind.Handle))
                throw new ArgumentException($"Field '{field.Name}' of kind {field.Kind} requires header mode {LogHeaderMode.Full}.", "fields");
        }
    }

    /// <summary>Byte offset of the field at <paramref name="fieldIndex"/> within an index record.</summary>
    public int GetFieldOffset(int fieldIndex) => fieldOffsets[fieldIndex];

    /// <summary>Tries to find the index of a field by name.</summary>
    public bool TryGetFieldIndex(string name, out int fieldIndex)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].Name == name)
            {
                fieldIndex = i;
                return true;
            }
        }
        fieldIndex = -1;
        return false;
    }

    /// <summary>Tries to get the bit mask of a user-defined flag by name.</summary>
    public bool TryGetFlagMask(string name, out ulong mask)
    {
        for (int i = 0; i < flagNames.Length; i++)
        {
            if (flagNames[i] == name)
            {
                mask = 1UL << (LogReservedFlags.Count + i);
                return true;
            }
        }
        mask = 0;
        return false;
    }
}
