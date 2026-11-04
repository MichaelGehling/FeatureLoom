using System;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Globally unique and stable identifier of an entry within one log.
/// Encodes the segment sequence number (upper 32 bits) and the entry index within the segment (lower 32 bits).
/// Ids never shift: compaction keeps index slots of deleted entries.
/// </summary>
public readonly struct LogEntryId : IEquatable<LogEntryId>, IComparable<LogEntryId>
{
    /// <summary>The raw 64-bit value.</summary>
    public ulong Value { get; }

    /// <summary>Creates an id from its raw 64-bit value.</summary>
    public LogEntryId(ulong value) => Value = value;

    /// <summary>Creates an id from segment sequence number and entry index.</summary>
    public LogEntryId(uint segmentSequence, uint entryIndex) => Value = ((ulong)segmentSequence << 32) | entryIndex;

    /// <summary>Sequence number of the segment containing the entry.</summary>
    public uint SegmentSequence => (uint)(Value >> 32);

    /// <summary>Index of the entry within its segment.</summary>
    public uint EntryIndex => (uint)Value;

    public bool Equals(LogEntryId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is LogEntryId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(LogEntryId other) => Value.CompareTo(other.Value);
    public override string ToString() => $"{SegmentSequence}:{EntryIndex}";

    public static bool operator ==(LogEntryId left, LogEntryId right) => left.Value == right.Value;
    public static bool operator !=(LogEntryId left, LogEntryId right) => left.Value != right.Value;
    public static bool operator <(LogEntryId left, LogEntryId right) => left.Value < right.Value;
    public static bool operator >(LogEntryId left, LogEntryId right) => left.Value > right.Value;
    public static bool operator <=(LogEntryId left, LogEntryId right) => left.Value <= right.Value;
    public static bool operator >=(LogEntryId left, LogEntryId right) => left.Value >= right.Value;
}
