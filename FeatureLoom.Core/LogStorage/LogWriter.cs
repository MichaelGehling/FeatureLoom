using System.Collections.Generic;
using FeatureLoom.Synchronization;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Collections;
using FeatureLoom.Helpers;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Default file-based log writer. Exactly one writer per log folder.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>On open, the current schema is persisted as <c>schema.def</c>; if the active segment was written with a different
/// schema, a new segment is started (the schema only changes at rollover).</item>
/// <item>Append order: resolve payload type, persist <c>types.map</c> if new type ids were assigned, write the segment record,
/// then the index record. All encoding and validation happens before anything is written.</item>
/// <item>The index timestamp is forced monotonic (never smaller than the previous entry) so time queries can binary search;
/// the full record header keeps the original timestamp.</item>
/// <item>Tail recovery on open: torn index records and segment bytes behind the last indexed record are truncated
/// (not possible in <see cref="LogHeaderMode.None"/>, where record lengths are not stored).</item>
/// <item><see cref="AppendAsync{T}"/> uses group commit: entries are queued and written by a background flush loop,
/// which writes all queued entries and flushes once per batch (see <see cref="LogFlushPolicy"/>).
/// Ordering between synchronous and queued asynchronous appends is not guaranteed.</item>
/// <item>Not yet supported: <see cref="LogFieldKind.Handle"/> fields (lookup table), secure delete zeroing,
/// schema growth.</item>
/// </list>
/// Thread-safe; operations are serialized.
/// </remarks>
public sealed partial class LogWriter : ILogWriter
{
    private const int MinimalHeaderSize = 8;
    private const int FullFixedHeaderSize = 10;

    private readonly MicroLock writeLock = new MicroLock();
    private readonly LogStorageOptions options;
    private readonly string directory;
    private readonly LogIndexSchema schema;
    private readonly Guid logId;
    private readonly LogEntryMetadata metadata;
    private readonly ulong[] fieldValues;
    private readonly byte[] indexBuffer;
    private readonly MemoryStream payloadStream = new MemoryStream();
    private byte[] recordBuffer = new byte[256];

    private const int StreamBufferSize = 64 * 1024;

    private FileStream segmentStream;
    private FileStream indexStream;
    private LogIndexFileHeader activeHeader;
    private long indexHeaderSize;
    // Tracked append offsets: querying FileStream.Length or setting Position flushes its write buffer,
    // so both streams are kept positioned at their end and these fields are authoritative.
    private long segmentEnd;
    private long indexEnd;
    private uint entryCount;
    private DateTime lastTimestamp;
    // Wall-clock creation time of the active segment, used for age-based rollover (BaseTime may be historic).
    private DateTime segmentCreatedUtc;
    private bool disposed;

    /// <summary>Opens or creates the log described by <paramref name="options"/>.</summary>
    /// <param name="options">Log configuration; its schema must be the schema of <paramref name="payloadTypes"/>.</param>
    /// <param name="payloadTypes">
    /// Payload types of the log. Its type map must contain all entries of the persisted <c>types.map</c>
    /// (create it with <see cref="LogPayloadTypeMap.Load"/>).
    /// </param>
    /// <exception cref="ArgumentException">Options and payload types don't match or the type map is inconsistent with the log folder.</exception>
    /// <exception cref="NotSupportedException">The schema uses features that are not implemented yet.</exception>
    public LogWriter(LogStorageOptions options, LogPayloadTypeRegistry payloadTypes)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        PayloadTypes = payloadTypes ?? throw new ArgumentNullException(nameof(payloadTypes));
        if (string.IsNullOrWhiteSpace(options.Directory)) throw new ArgumentException("Directory must be set.", nameof(options));
        if (options.Schema == null) throw new ArgumentException("Schema must be set.", nameof(options));
        if (options.Schema != payloadTypes.Schema) throw new ArgumentException("The payload types must be registered for the schema of the options.", nameof(payloadTypes));

        schema = options.Schema;
        foreach (var field in schema.Fields)
        {
            if (field.Kind == LogFieldKind.Handle) throw new NotSupportedException($"Field '{field.Name}': {LogFieldKind.Handle} fields are not supported yet.");
        }
        if (schema.Framing != LogRecordFraming.Binary) throw new NotSupportedException($"Record framing {schema.Framing} is not supported yet.");

        directory = options.Directory;
        metadata = new LogEntryMetadata(schema);
        fieldValues = new ulong[schema.Fields.Count];
        indexBuffer = new byte[schema.RecordSize];

        System.IO.Directory.CreateDirectory(directory);
        VerifyTypeMap();
        logId = LogSchemaFile.TryLoad(directory, out var storedSchema, out var storedId) ? storedId : Guid.NewGuid();
        if (storedSchema == null || !LogIndexSchemaCodec.AreEqual(storedSchema, schema)) LogSchemaFile.Write(directory, schema, logId);

        try
        {
            OpenActiveSegment();
        }
        catch
        {
            CloseStreams();
            throw;
        }
    }

    /// <inheritdoc/>
    public LogIndexSchema Schema => schema;

    /// <inheritdoc/>
    public LogPayloadTypeRegistry PayloadTypes { get; }

    /// <summary>Folder of the log.</summary>
    public string Directory => directory;

    /// <summary>Id of the log, persisted in <c>schema.def</c> and every index header.</summary>
    public Guid LogId => logId;

    /// <summary>Sequence number of the active segment.</summary>
    public uint ActiveSegmentSequence
    {
        get { using (writeLock.Lock()) return activeHeader.SegmentSequence; }
    }

    /// <inheritdoc/>
    public LogEntryId Append<T>(T payload)
    {
        using (writeLock.Lock())
        {
            CheckNotDisposed();
            return AppendLocked(payload, true);
        }
    }

    /// <summary>
    /// Appends multiple entries under a single lock acquisition and flushes once at the end,
    /// which is considerably faster than calling <see cref="Append{T}(T)"/> per entry.
    /// </summary>
    /// <remarks>
    /// Not atomic: if an entry fails (e.g. validation), all previously appended entries of the batch stay written
    /// and are flushed before the exception is rethrown. The failing entry leaves no partial record.
    /// <paramref name="payloads"/> is enumerated on the calling thread while the write lock is held: lazy enumerables
    /// block other writers for their whole enumeration, and an exception thrown by the enumeration behaves like a failing entry.
    /// Materialize expensive or blocking sources first.
    /// </remarks>
    /// <param name="payloads">Payloads to append in order.</param>
    /// <returns>Ids of the appended entries in input order.</returns>
    public List<LogEntryId> AppendMany<T>(IEnumerable<T> payloads)
    {
        if (payloads == null) throw new ArgumentNullException(nameof(payloads));
        var ids = payloads is ICollection<T> collection ? new List<LogEntryId>(collection.Count) : new List<LogEntryId>();
        using (writeLock.Lock())
        {
            CheckNotDisposed();
            try
            {
                var payloadType = ResolvePayloadType<T>();
                DateTime now = DateTime.UtcNow;
                foreach (var payload in payloads) ids.Add(AppendLocked(payload, false, payloadType, now));
            }
            finally
            {
                if (ids.Count > 0) FlushStreams();
            }
        }
        return ids;
    }

    /// <summary>Asynchronous variant of <see cref="AppendMany{T}(IEnumerable{T})"/>.</summary>
    /// <remarks>
    /// Queued as a single item for the background flush loop; all entries are written within one batch.
    /// Cancellation before writing started drops all entries; afterwards all are written.
    /// An <see cref="IReadOnlyList{T}"/> (e.g. array, List) is used without copying and must not be modified until the task completes;
    /// other enumerables are enumerated immediately on the calling thread into a pooled snapshot.
    /// </remarks>
    public Task<List<LogEntryId>> AppendManyAsync<T>(IEnumerable<T> payloads, CancellationToken cancellationToken = default)
    {
        if (payloads == null) throw new ArgumentNullException(nameof(payloads));
        return EnqueueAppendMany(payloads, cancellationToken);
    }

    private LogEntryId AppendLocked<T>(T payload, bool flush) => AppendLocked(payload, flush, ResolvePayloadType<T>(), DateTime.UtcNow);

    // Single-entry cache of the last used payload type: consecutive appends of the same type skip the registry lookup.
    // Registered types are never removed, so a cached type stays valid. Only accessed under writeLock.
    private LogPayloadType lastPayloadType;

    private readonly LogSourceEncodingCache sourceCache = new LogSourceEncodingCache();

    private LogPayloadType<T> ResolvePayloadType<T>()
    {
        if (lastPayloadType is LogPayloadType<T> cached) return cached;
        var payloadType = PayloadTypes.GetOrRegister<T>();
        // A new type id must be persisted before the first entry referencing it.
        if (PayloadTypes.TypeMap.IsDirty) PayloadTypes.TypeMap.Write(directory);
        lastPayloadType = payloadType;
        return payloadType;
    }

    /// <param name="now">Wall-clock time; batch callers pass one value for all entries (fallback timestamp and age-based rollover).</param>
    private LogEntryId AppendLocked<T>(T payload, bool flush, LogPayloadType<T> payloadType, DateTime now)
    {
        {
            metadata.Clear();
            payloadType.Extract(payload, now, metadata);

            payloadStream.SetLength(0);
            payloadType.Codec.Encode(payload, payloadStream);
            var payloadSegment = new ByteSegment(payloadStream.GetBuffer(), 0, (int)payloadStream.Length);

            for (int i = 0; i < fieldValues.Length; i++)
            {
                var field = schema.Fields[i];
                fieldValues[i] = field.Kind == LogFieldKind.Hash ? LogFieldHash.Compute(metadata.GetSource(i), field.MaxValue) : metadata.GetValue(i);
            }

            DateTime timestamp = metadata.Timestamp.ToUniversalTime();
            if (timestamp < lastTimestamp) timestamp = lastTimestamp;

            int headerSize = LogBinaryRecordFraming.GetHeaderSize(schema, metadata, sourceCache);
            long recordLength = headerSize + (long)payloadSegment.Count;
            if (NeedsRollover(now, timestamp, recordLength)) StartNewSegment(now);
            // An empty segment adopts the first entry's timestamp as base time, so older (e.g. imported) timestamps are kept.
            if (entryCount == 0 && timestamp != activeHeader.BaseTime) RewriteHeader(timestamp);
            if (timestamp < activeHeader.BaseTime) timestamp = activeHeader.BaseTime;

            // Encode everything first: a validation error must not leave a partial record on disk.
            long position = segmentEnd;
            LogIndexRecordCodec.Write(schema, indexBuffer, 0, activeHeader.ToTimeOffset(timestamp), (ulong)position, metadata.Flags, metadata.TypeId, fieldValues);
            if (recordBuffer.Length < recordLength) recordBuffer = new byte[Math.Max(recordLength, recordBuffer.Length * 2L)];
            int written = LogBinaryRecordFraming.Write(schema, recordBuffer, 0, metadata, payloadSegment, headerSize, sourceCache);

            try
            {
                segmentStream.Write(recordBuffer, 0, written);
                indexStream.Write(indexBuffer, 0, indexBuffer.Length);
                if (flush) FlushStreams();
            }
            catch
            {
                TryTruncate(segmentStream, position);
                TryTruncate(indexStream, indexEnd);
                throw;
            }
            segmentEnd = position + written;
            indexEnd += indexBuffer.Length;

            lastTimestamp = timestamp;
            return new LogEntryId(activeHeader.SegmentSequence, entryCount++);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Queued for group commit; the task completes after the batch containing the entry was written and flushed.
    /// Cancellation before the entry is written drops it and cancels the task; once writing started, the entry is written.
    /// </remarks>
    public Task<LogEntryId> AppendAsync<T>(T payload, CancellationToken cancellationToken = default)
    {
        return EnqueueAppend(payload, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>Returns false if the entry does not exist or is already deleted. Payload zeroing (secure delete) is not implemented yet.</remarks>
    /// <remarks>Waits for queued asynchronous appends first; cancellation applies to that wait, the delete itself is not interrupted.</remarks>
    public async Task<bool> TryDeleteAsync(LogEntryId id, CancellationToken cancellationToken = default)
    {
        await DrainAsync(cancellationToken).ConfiguredAwait();
        cancellationToken.ThrowIfCancellationRequested();
        return TryDelete(id);
    }

    private bool TryDelete(LogEntryId id)
    {
        using (writeLock.Lock())
        {
            CheckNotDisposed();
            if (id.SegmentSequence == activeHeader.SegmentSequence)
            {
                if (id.EntryIndex >= entryCount) return false;
                bool result = TrySetDeletedFlag(indexStream, activeHeader.Schema, indexHeaderSize, id.EntryIndex);
                indexStream.Position = indexEnd;
                FlushStreams();
                return result;
            }

            var files = System.IO.Directory.GetFiles(directory, LogSegmentFiles.GetIndexSearchPattern(id.SegmentSequence));
            if (files.Length != 1) return false;
            using (var stream = new FileStream(files[0], FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                var header = LogIndexFileHeader.Read(stream);
                long headerSize = stream.Position;
                long count = (stream.Length - headerSize) / header.Schema.RecordSize;
                if (id.EntryIndex >= count) return false;
                bool result = TrySetDeletedFlag(stream, header.Schema, headerSize, id.EntryIndex);
                if (options.Flush.FlushToDisk) stream.Flush(true);
                return result;
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>Waits for queued asynchronous appends first.</remarks>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await DrainAsync(cancellationToken).ConfiguredAwait();
        using (writeLock.Lock())
        {
            CheckNotDisposed();
            segmentStream.Flush(true);
            indexStream.Flush(true);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Waits for queued asynchronous appends first.</remarks>
    public async Task RolloverAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await DrainAsync(cancellationToken).ConfiguredAwait();
        using (writeLock.Lock())
        {
            CheckNotDisposed();
            StartNewSegment(DateTime.UtcNow);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Queued asynchronous appends are written before the files are closed.</remarks>
    public void Dispose()
    {
        WritePendingOnDispose();
        using (writeLock.Lock())
        {
            if (disposed) return;
            disposed = true;
            CloseStreams();
            payloadStream.Dispose();
        }
    }

    private bool NeedsRollover(DateTime now, DateTime timestamp, long recordLength)
    {
        if (entryCount == 0) return false;
        if (entryCount == uint.MaxValue) return true;
        var rotation = options.Rotation;
        long position = segmentEnd;
        if (rotation?.MaxSegmentSize != null && position >= rotation.MaxSegmentSize.Value) return true;
        // Age is wall-clock time since creation, not since BaseTime: BaseTime follows the first entry's timestamp,
        // which may be historic (imports) and would otherwise force a rollover on every append.
        if (rotation?.MaxSegmentAge != null && now - segmentCreatedUtc >= rotation.MaxSegmentAge.Value) return true;
        // Hard format limits: 4-byte positions and the 4-byte time offset range.
        if (schema.PositionSize == LogPositionSize.Bytes4 && position > uint.MaxValue) return true;
        if (timestamp - activeHeader.BaseTime >= schema.MaxTimeRange) return true;
        return false;
    }

    private void VerifyTypeMap()
    {
        var persisted = LogPayloadTypeMap.Load(directory);
        foreach (var entry in persisted.Entries)
        {
            if (!PayloadTypes.TypeMap.TryGetId(entry.Key, out ushort id) || id != entry.Value)
                throw new ArgumentException($"The type map of the payload types does not match '{LogPayloadTypeMap.FileName}' (type '{entry.Key}'). Create it with {nameof(LogPayloadTypeMap)}.{nameof(LogPayloadTypeMap.Load)}.");
        }
    }

    private void OpenActiveSegment()
    {
        uint lastSequence = 0;
        string lastIndexFile = null;
        foreach (var file in System.IO.Directory.GetFiles(directory, LogSegmentFiles.IndexSearchPattern))
        {
            if (LogSegmentFiles.TryParseSequence(Path.GetFileName(file), out uint sequence) && (lastIndexFile == null || sequence > lastSequence))
            {
                lastSequence = sequence;
                lastIndexFile = file;
            }
        }

        if (lastIndexFile == null)
        {
            CreateSegment(1, DateTime.UtcNow);
            return;
        }

        indexStream = new FileStream(lastIndexFile, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, StreamBufferSize);
        activeHeader = LogIndexFileHeader.Read(indexStream);
        indexHeaderSize = indexStream.Position;
        segmentCreatedUtc = LogSegmentFiles.TryParseStartTime(Path.GetFileName(lastIndexFile), out var created) ? created : File.GetCreationTimeUtc(lastIndexFile);
        string segmentFile = Path.ChangeExtension(lastIndexFile, LogSegmentFiles.SegmentExtension);
        segmentStream = new FileStream(segmentFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, StreamBufferSize);
        Recover();

        if (!LogIndexSchemaCodec.AreEqual(activeHeader.Schema, schema) || activeHeader.SecureDelete != options.Maintenance.SecureDelete)
        {
            StartNewSegment(DateTime.UtcNow);
        }
    }

    private void Recover()
    {
        var segmentSchema = activeHeader.Schema;
        int recordSize = segmentSchema.RecordSize;
        long count = (indexStream.Length - indexHeaderSize) / recordSize;
        long segmentLength = segmentStream.Length;
        long validSegmentLength = 0;
        var record = new byte[recordSize];

        while (count > 0)
        {
            ReadIndexRecord(indexStream, indexHeaderSize, recordSize, count - 1, record);
            long position = (long)LogIndexRecordCodec.ReadPosition(segmentSchema, record, 0);
            if (segmentSchema.HeaderMode == LogHeaderMode.None)
            {
                // Record lengths are not stored; the segment end is the only bound.
                if (position <= segmentLength)
                {
                    validSegmentLength = segmentLength;
                    break;
                }
            }
            else
            {
                long length = ReadRecordLength(segmentStream, segmentSchema.HeaderMode, position, segmentLength);
                if (length >= 0)
                {
                    validSegmentLength = position + length;
                    break;
                }
            }
            count--;
        }

        if (count > uint.MaxValue) throw new InvalidDataException("Index file contains too many entries.");
        long validIndexLength = indexHeaderSize + count * recordSize;
        if (indexStream.Length != validIndexLength) indexStream.SetLength(validIndexLength);
        if (segmentLength != validSegmentLength) segmentStream.SetLength(validSegmentLength);
        entryCount = (uint)count;
        segmentEnd = validSegmentLength;
        indexEnd = validIndexLength;
        segmentStream.Position = segmentEnd;

        if (count > 0)
        {
            ReadIndexRecord(indexStream, indexHeaderSize, recordSize, count - 1, record);
            var last = activeHeader.FromTimeOffset(LogIndexRecordCodec.ReadTimeOffset(record, 0));
            if (last > lastTimestamp) lastTimestamp = last;
        }
        indexStream.Position = indexEnd;
    }

    private static long ReadRecordLength(FileStream stream, LogHeaderMode mode, long position, long segmentLength)
    {
        int fixedSize = mode == LogHeaderMode.Minimal ? MinimalHeaderSize : FullFixedHeaderSize;
        if (position < 0 || position + fixedSize > segmentLength) return -1;
        var header = new byte[fixedSize];
        stream.Position = position;
        if (!ReadExactly(stream, header, fixedSize)) return -1;
        long length = fixedSize + (long)LittleEndian.ReadUInt32(header, 0);
        if (mode == LogHeaderMode.Full) length += LittleEndian.ReadUInt16(header, 8);
        return position + length <= segmentLength ? length : -1;
    }

    private void StartNewSegment(DateTime now)
    {
        uint sequence = activeHeader.SegmentSequence + 1;
        if (sequence == 0) throw new InvalidOperationException("Segment sequence numbers are exhausted.");
        CloseStreams();
        CreateSegment(sequence, now);
    }

    private void CreateSegment(uint sequence, DateTime now)
    {
        var header = new LogIndexFileHeader(schema, sequence, now, options.Maintenance.SecureDelete, logId);
        string baseName = Path.Combine(directory, LogSegmentFiles.GetBaseName(sequence, now));
        // Segment file first: an index file without segment would be ambiguous on recovery.
        segmentStream = new FileStream(baseName + LogSegmentFiles.SegmentExtension, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, StreamBufferSize);
        indexStream = new FileStream(baseName + LogSegmentFiles.IndexExtension, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, StreamBufferSize);
        var headerBytes = header.ToBytes();
        indexStream.Write(headerBytes, 0, headerBytes.Length);
        indexStream.Flush(true);
        activeHeader = header;
        indexHeaderSize = headerBytes.Length;
        segmentCreatedUtc = now;
        entryCount = 0;
        segmentEnd = 0;
        indexEnd = headerBytes.Length;
    }

    private void RewriteHeader(DateTime baseTime)
    {
        var header = new LogIndexFileHeader(schema, activeHeader.SegmentSequence, baseTime, activeHeader.SecureDelete, logId);
        var headerBytes = header.ToBytes();
        if (headerBytes.Length != indexHeaderSize) throw new InvalidOperationException("Index header size changed unexpectedly.");
        indexStream.Position = 0;
        indexStream.Write(headerBytes, 0, headerBytes.Length);
        indexStream.Position = indexEnd;
        activeHeader = header;
    }

    private static bool TrySetDeletedFlag(FileStream stream, LogIndexSchema segmentSchema, long headerSize, uint entryIndex)
    {
        var record = new byte[segmentSchema.RecordSize];
        ReadIndexRecord(stream, headerSize, segmentSchema.RecordSize, entryIndex, record);
        ulong flags = LogIndexRecordCodec.ReadFlags(segmentSchema, record, 0);
        if ((flags & LogReservedFlags.Deleted) != 0) return false;
        LogIndexRecordCodec.WriteFlags(segmentSchema, record, 0, flags | LogReservedFlags.Deleted);
        stream.Position = headerSize + (long)entryIndex * segmentSchema.RecordSize;
        stream.Write(record, 0, record.Length);
        return true;
    }

    private static void ReadIndexRecord(FileStream stream, long headerSize, int recordSize, long entryIndex, byte[] record)
    {
        stream.Position = headerSize + entryIndex * recordSize;
        if (!ReadExactly(stream, record, recordSize)) throw new InvalidDataException("Index record is truncated.");
    }

    private static bool ReadExactly(Stream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read <= 0) return false;
            offset += read;
        }
        return true;
    }

    private void FlushStreams()
    {
        bool toDisk = options.Flush.FlushToDisk;
        segmentStream.Flush(toDisk);
        indexStream.Flush(toDisk);
    }

    private static void TryTruncate(FileStream stream, long length)
    {
        try
        {
            stream.SetLength(length);
            stream.Position = length;
        }
        catch
        {
            // Best effort; recovery on next open truncates torn data.
        }
    }

    private void CloseStreams()
    {
        segmentStream?.Dispose();
        indexStream?.Dispose();
        segmentStream = null;
        indexStream = null;
    }

    private void CheckNotDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(LogWriter));
    }
}
