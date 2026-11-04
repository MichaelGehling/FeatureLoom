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

    public LogEntryInfo(LogEntryId id, DateTime timestamp, ulong flags)
    {
        Id = id;
        Timestamp = timestamp;
        Flags = flags;
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

/// <summary>Index values supplied when appending an entry. Custom field values are given by field name.</summary>
public sealed class LogAppendInfo
{
    /// <summary>Timestamp (UTC). If null, the current time is used.</summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>Names of user-defined flags to set.</summary>
    public IReadOnlyList<string> Flags { get; set; }

    /// <summary>Raw values for Raw/Ascending/Descending fields.</summary>
    public IReadOnlyDictionary<string, ulong> FieldValues { get; set; }

    /// <summary>Source values for Hash/Handle fields.</summary>
    public IReadOnlyDictionary<string, string> FieldSources { get; set; }
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

    /// <summary>Exact field matches on raw values, by field name.</summary>
    public IReadOnlyDictionary<string, ulong> FieldEquals { get; set; }

    /// <summary>Exact field matches on source values (Hash/Handle fields), by field name.</summary>
    public IReadOnlyDictionary<string, string> FieldSourceEquals { get; set; }

    /// <summary>Return newest entries first.</summary>
    public bool Reverse { get; set; }

    /// <summary>Maximum number of results. Null means unlimited.</summary>
    public int? Limit { get; set; }
}
