using System;

namespace FeatureLoom.LogStorage;

/// <summary>When segments are rolled over. Soft limits are checked before appending; hard format limits always apply.</summary>
public sealed class LogRotationPolicy
{
    /// <summary>Roll over when the segment file reaches this size. Null disables size-based rotation.</summary>
    public long? MaxSegmentSize { get; set; } = 256L * 1024 * 1024;

    /// <summary>Roll over when the segment's first entry is older than this. Null disables age-based rotation.</summary>
    public TimeSpan? MaxSegmentAge { get; set; } = TimeSpan.FromDays(1);
}

/// <summary>When closed segments are removed. All set limits apply; the active segment is never removed.</summary>
public sealed class LogRetentionPolicy
{
    /// <summary>Remove segments whose newest entry is older than this.</summary>
    public TimeSpan? MaxAge { get; set; }

    /// <summary>Keep at most this many closed segments.</summary>
    public int? MaxSegmentCount { get; set; }

    /// <summary>Keep the total size of all segments below this.</summary>
    public long? MaxTotalSize { get; set; }
}

/// <summary>How closed segments are post-processed.</summary>
public sealed class LogMaintenancePolicy
{
    /// <summary>Compact closed segments containing deleted entries before compression.</summary>
    public bool CompactClosedSegments { get; set; } = true;

    /// <summary>GZip-compress closed segments older than this. Null disables compression.</summary>
    public TimeSpan? CompressAfter { get; set; }

    /// <summary>
    /// Overwrite payloads of deleted entries with zeros. Stored as a segment header setting; when enabled for an existing
    /// segment, its already deleted entries are zeroed before the setting is persisted.
    /// </summary>
    public bool SecureDelete { get; set; }
}

/// <summary>Controls batching of writes and flushing to disk.</summary>
public sealed class LogFlushPolicy
{
    /// <summary>Maximum number of buffered entries before writing.</summary>
    public int MaxBufferedEntries { get; set; } = 1000;

    /// <summary>Maximum time an entry stays buffered before writing.</summary>
    public TimeSpan MaxBufferDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Flush to the physical disk (fsync) after each write batch.</summary>
    public bool FlushToDisk { get; set; }
}

/// <summary>Configuration of a single log. The schema is fixed at creation; policies may change on reopen.</summary>
public sealed class LogStorageOptions
{
    /// <summary>Folder containing all files of this log.</summary>
    public string Directory { get; set; }

    /// <summary>Index schema used when the log is created (or at the next rollover if changed).</summary>
    public LogIndexSchema Schema { get; set; } = new LogIndexSchema();

    /// <summary>Segment rotation policy.</summary>
    public LogRotationPolicy Rotation { get; set; } = new LogRotationPolicy();

    /// <summary>Segment retention policy.</summary>
    public LogRetentionPolicy Retention { get; set; } = new LogRetentionPolicy();

    /// <summary>Maintenance policy for closed segments.</summary>
    public LogMaintenancePolicy Maintenance { get; set; } = new LogMaintenancePolicy();

    /// <summary>Write batching and flushing policy.</summary>
    public LogFlushPolicy Flush { get; set; } = new LogFlushPolicy();
}
