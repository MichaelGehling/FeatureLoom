using System;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Defines how a custom field value is stored in the fixed-size index record.
/// </summary>
public enum LogFieldKind : byte
{
    /// <summary>Caller-supplied numeric value. Exact; supports equality, set and range filters.</summary>
    Raw = 0,
    /// <summary>Hash of a caller-supplied source value. Matches are verified against the source value in the record header.</summary>
    Hash = 1,
    /// <summary>Handle of a source value interned in the log's lookup table. Exact, except for the overflow handle.</summary>
    Handle = 2,
    /// <summary>Like <see cref="Raw"/>, but values never decrease within a log, enabling binary search.</summary>
    Ascending = 3,
    /// <summary>Like <see cref="Raw"/>, but values never increase within a log, enabling binary search.</summary>
    Descending = 4
}

/// <summary>
/// Definition of a custom index field. Immutable.
/// </summary>
public sealed class LogFieldDefinition
{
    /// <summary>Minimum field size in bytes.</summary>
    public const int MinSize = 1;
    /// <summary>Maximum field size in bytes.</summary>
    public const int MaxSize = 8;

    /// <summary>Unique field name within the schema.</summary>
    public string Name { get; }

    /// <summary>Storage kind of the field.</summary>
    public LogFieldKind Kind { get; }

    /// <summary>Size of the field in the index record in bytes (1-8).</summary>
    public int Size { get; }

    /// <summary>Creates a field definition.</summary>
    /// <exception cref="ArgumentException">Name is empty or size is out of range.</exception>
    public LogFieldDefinition(string name, LogFieldKind kind, int size)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Field name must not be empty.", nameof(name));
        if (size < MinSize || size > MaxSize) throw new ArgumentOutOfRangeException(nameof(size), size, $"Field size must be between {MinSize} and {MaxSize} bytes.");
        Name = name;
        Kind = kind;
        Size = size;
    }

    /// <summary>Largest value that fits into the field.</summary>
    public ulong MaxValue => Size == 8 ? ulong.MaxValue : (1UL << (Size * 8)) - 1;

    public override string ToString() => $"{Name} ({Kind}, {Size} B)";
}
