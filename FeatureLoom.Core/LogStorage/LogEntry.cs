using System;
using System.Collections.Generic;

namespace FeatureLoom.LogStorage;

/// <summary>Index metadata of a log entry, available without reading the payload.</summary>
public readonly struct LogEntryInfo
{
    /// <summary>Stable id of the entry.</summary>
    public LogEntryId Id { get; }

    /// <summary>Timestamp (UTC), truncated to the schema's time resolution.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Flag bits, including <see cref="LogReservedFlags"/>.</summary>
    public ulong Flags { get; }

    /// <summary>Id of the payload type (see <see cref="LogPayloadTypeRegistry"/>).</summary>
    public ushort TypeId { get; }

    public LogEntryInfo(LogEntryId id, DateTime timestamp, ulong flags, ushort typeId = 0)
    {
        Id = id;
        Timestamp = timestamp;
        Flags = flags;
        TypeId = typeId;
    }

    /// <summary>True if the entry is flagged as deleted.</summary>
    public bool IsDeleted => (Flags & LogReservedFlags.Deleted) != 0;
}

/// <summary>A log entry with its decoded payload.</summary>
public readonly struct LogEntry<T>
{
    /// <summary>Index metadata.</summary>
    public LogEntryInfo Info { get; }

    /// <summary>Decoded payload.</summary>
    public T Payload { get; }

    public LogEntry(LogEntryInfo info, T payload)
    {
        Info = info;
        Payload = payload;
    }
}

/// <summary>
/// Index metadata of one entry, derived from the payload by its <see cref="LogPayloadType{T}"/>.
/// Writer-owned scratch object: filled per append and copied into the write buffer, never exposed to callers for writing.
/// </summary>
public sealed class LogEntryMetadata
{
    private readonly ulong[] fieldValues;
    private readonly string[] fieldSources;

    /// <summary>Creates metadata storage matching <paramref name="schema"/>.</summary>
    public LogEntryMetadata(LogIndexSchema schema)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        fieldValues = schema.Fields.Count == 0 ? Array.Empty<ulong>() : new ulong[schema.Fields.Count];
        fieldSources = schema.Fields.Count == 0 ? Array.Empty<string>() : new string[schema.Fields.Count];
    }

    /// <summary>Schema this metadata belongs to.</summary>
    public LogIndexSchema Schema { get; }

    /// <summary>Timestamp (UTC).</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Flag bits (user flags only; reserved bits are managed by the storage).</summary>
    public ulong Flags { get; set; }

    /// <summary>Payload type id.</summary>
    public ushort TypeId { get; set; }

    /// <summary>Sets the raw value of a Raw/Ascending/Descending field.</summary>
    public void SetValue(int fieldIndex, ulong value) => fieldValues[fieldIndex] = value;

    /// <summary>Sets the source value of a Hash/Handle field.</summary>
    public void SetSource(int fieldIndex, string source) => fieldSources[fieldIndex] = source;

    /// <summary>Raw value of a field.</summary>
    public ulong GetValue(int fieldIndex) => fieldValues[fieldIndex];

    /// <summary>Source value of a field (null if not set).</summary>
    public string GetSource(int fieldIndex) => fieldSources[fieldIndex];

    /// <summary>Resets all values for reuse.</summary>
    public void Clear()
    {
        Timestamp = default;
        Flags = 0;
        TypeId = 0;
        Array.Clear(fieldValues, 0, fieldValues.Length);
        Array.Clear(fieldSources, 0, fieldSources.Length);
    }
}

/// <summary>Filter on a single custom field, addressed by field index.</summary>
public readonly struct LogFieldFilter
{
    /// <summary>Index of the field in the schema.</summary>
    public int FieldIndex { get; }

    /// <summary>Raw value to match (Raw/Ascending/Descending fields).</summary>
    public ulong Value { get; }

    /// <summary>Source value to match (Hash/Handle fields). Null for raw matches.</summary>
    public string Source { get; }

    private LogFieldFilter(int fieldIndex, ulong value, string source)
    {
        FieldIndex = fieldIndex;
        Value = value;
        Source = source;
    }

    /// <summary>Matches the raw field value.</summary>
    public static LogFieldFilter ValueEquals(int fieldIndex, ulong value) => new LogFieldFilter(fieldIndex, value, null);

    /// <summary>Matches the source value of a Hash/Handle field.</summary>
    public static LogFieldFilter SourceEquals(int fieldIndex, string source) => new LogFieldFilter(fieldIndex, 0, source ?? throw new ArgumentNullException(nameof(source)));
}

/// <summary>Filter criteria for querying a log. All set criteria must match.</summary>
public sealed class LogQuery
{
    /// <summary>Inclusive lower time bound.</summary>
    public DateTime? From { get; set; }

    /// <summary>Exclusive upper time bound.</summary>
    public DateTime? To { get; set; }

    /// <summary>Flag bits that must all be set.</summary>
    public ulong RequiredFlags { get; set; }

    /// <summary>Flag bits that must all be unset. Deleted entries are excluded by default.</summary>
    public ulong ExcludedFlags { get; set; } = LogReservedFlags.Deleted;

    /// <summary>Field filters; all must match.</summary>
    public IReadOnlyList<LogFieldFilter> FieldFilters { get; set; }

    /// <summary>Payload type ids to include. Null means all (payload queries for <c>T</c> restrict this to the type of <c>T</c>).</summary>
    public IReadOnlyList<ushort> TypeIds { get; set; }

    /// <summary>Return newest entries first.</summary>
    public bool Reverse { get; set; }

    /// <summary>Maximum number of results. Null means unlimited.</summary>
    public int? Limit { get; set; }
}
