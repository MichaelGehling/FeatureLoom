using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Collections;
using FeatureLoom.Helpers;

namespace FeatureLoom.LogStorage;
/// <summary>
/// Default file-based log reader. Can run concurrently with a <see cref="LogWriter"/> on the same folder and also works on read-only archives.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Query field filters and flags refer to the reader's schema (<see cref="Schema"/>). They are mapped by name to the schema
/// of each segment, so segments written with an older/newer schema remain queryable. A segment lacking a filtered field or
/// required flag yields no results; a missing excluded flag is treated as unset.</item>
/// <item>Hash source filters pre-filter by hash in the index and verify the source value stored in the full record header,
/// so hash collisions never produce false matches.</item>
/// <item>Not yet supported: <see cref="LogFieldKind.Handle"/> source filters, index caching (each query reads the index files),
/// binary search on time ranges, truly asynchronous I/O.</item>
/// </list>
/// </remarks>
public sealed partial class LogReader : ILogReader
{
    private bool disposed;

    /// <summary>Opens the log in <paramref name="directory"/> for reading. Also works on read-only archives.</summary>
    /// <param name="directory">Folder of the log.</param>
    /// <param name="payloadTypes">
    /// Prepared payload types used to decode payloads. Its type map should be loaded from the log folder
    /// (<see cref="LogPayloadTypeMap.Load"/>) so that type ids match.
    /// </param>
    public LogReader(string directory, LogPayloadTypeRegistry payloadTypes)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Directory must be set.", nameof(directory));
        Directory = directory;
        PayloadTypes = payloadTypes ?? throw new ArgumentNullException(nameof(payloadTypes));
    }

    /// <summary>Folder of the log.</summary>
    public string Directory { get; }

    /// <inheritdoc/>
    public LogIndexSchema Schema => PayloadTypes.Schema;

    /// <inheritdoc/>
    public LogPayloadTypeRegistry PayloadTypes { get; }

    /// <inheritdoc/>
    /// <remarks>Deleted entries and entries of another payload type are not found.</remarks>
    public Task<(bool found, LogEntry<T> entry)> TryReadAsync<T>(LogEntryId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckNotDisposed();
        if (!TryResolveType(out LogPayloadType<T> payloadType)) return NotFound<T>.Task;
        var handle = AcquireHandle(id.SegmentSequence);
        if (handle == null) return NotFound<T>.Task;
        try
        {
            if (!handle.TryRead(id.EntryIndex, payloadType, out var entry)) return NotFound<T>.Task;
            return Task.FromResult((true, entry));
        }
        finally
        {
            handle.Release();
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<LogEntry<T>>> QueryAsync<T>(LogQuery query, CancellationToken cancellationToken = default)
    {
        var result = new List<LogEntry<T>>();
        foreach (var entry in Query<T>(query, cancellationToken)) result.Add(entry);
        return Task.FromResult<IReadOnlyList<LogEntry<T>>>(result);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<LogEntryInfo>> QueryInfoAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var result = new List<LogEntryInfo>();
        foreach (var cursor in Scan(query, null, cancellationToken)) result.Add(cursor.GetInfo());
        return Task.FromResult<IReadOnlyList<LogEntryInfo>>(result);
    }

    /// <inheritdoc/>
    public Task<long> CountAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        long count = 0;
        foreach (var _ in Scan(query, null, cancellationToken)) count++;
        return Task.FromResult(count);
    }

#if NETSTANDARD2_1_OR_GREATER || NET
#pragma warning disable CS1998 // File access is synchronous for now; the async signature keeps the contract stable.
    /// <inheritdoc/>
    public async IAsyncEnumerable<LogEntry<T>> QueryStreamAsync<T>(LogQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS1998
    {
        foreach (var entry in Query<T>(query, cancellationToken)) yield return entry;
    }
#endif

    /// <inheritdoc/>
    public void Dispose()
    {
        disposed = true;
        DisposeHandles();
    }

	private IEnumerable<LogEntry<T>> Query<T>(LogQuery query, CancellationToken cancellationToken)
	{
		CheckNotDisposed();
		if (!TryResolveType(out LogPayloadType<T> payloadType)) yield break;
		if (query?.TypeIds != null && !Contains(query.TypeIds, payloadType.TypeId)) yield break;

		foreach (var cursor in Scan(query, payloadType.TypeId, cancellationToken))
		{
			var record = new LogRecord();
			// Entries that can't be decoded (e.g. torn tail being written) are skipped.
			// The payload refers to the cursor's buffer, so it is decoded before the scan continues.
			if (cursor.TryReadRecord(ref record) && payloadType.Codec.TryDecode(record.Payload, out T payload))
			{
				yield return new LogEntry<T>(cursor.GetInfo(), payload);
			}
		}
	}

    private bool TryResolveType<T>(out LogPayloadType<T> payloadType)
    {
        if (PayloadTypes.TryGet(out payloadType)) return true;
        // Implicit types are only resolved if the log already knows them; a reader must never assign new ids.
        if (!PayloadTypes.AllowImplicitTypes) return false;
        if (!PayloadTypes.TypeMap.TryGetId(TypeNameHelper.Shared.GetSimplifiedTypeName(typeof(T)), out _)) return false;
        payloadType = PayloadTypes.GetOrRegister<T>();
        return true;
    }

	/// <summary>
	/// Yields a cursor positioned on each matching index record. The cursor is reused: its data is only valid until the next iteration.
	/// Segments are accessed through the cached handles; index records are read in chunks, never as whole files.
	/// </summary>
	private IEnumerable<IndexCursor> Scan(LogQuery query, ushort? typeId, CancellationToken cancellationToken)
	{
		CheckNotDisposed();
		query ??= new LogQuery();
		if (query.Limit.HasValue && query.Limit.Value <= 0) yield break;
		var sequences = GetSegmentSequences();
		if (sequences.Length == 0) yield break;

		long remaining = query.Limit ?? long.MaxValue;
		DateTime? from = query.From?.ToUniversalTime();
		DateTime? to = query.To?.ToUniversalTime();
		var cursor = new IndexCursor();

		for (int s = 0; s < sequences.Length; s++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			uint sequence = query.Reverse ? sequences[sequences.Length - 1 - s] : sequences[s];
			var handle = AcquireHandle(sequence);
			if (handle == null)
			{
				// Removed (e.g. by retention) since the segment list was built.
				InvalidateSegmentList();
				continue;
			}
			try
			{
				if (to.HasValue && handle.Header.BaseTime >= to.Value) continue;
				if (!SegmentFilter.TryCreate(Schema, handle.Schema, query, typeId, out var filter)) continue;
				uint count = handle.GetCount();
				if (count == 0) continue;

				// The writer keeps timestamps non-decreasing within a segment, so time bounds can be binary searched.
				// If a probe read fails, the full range is scanned; the filter still checks timestamps.
				uint start = 0, end = count;
				if (from.HasValue && !handle.TryFindFirstAtOrAfter(from.Value, count, cursor.GetProbeBuffer(handle.Schema.RecordSize), out start)) start = 0;
				if (to.HasValue && !handle.TryFindFirstAtOrAfter(to.Value, count, cursor.GetProbeBuffer(handle.Schema.RecordSize), out end)) end = count;
				if (start >= end) continue;

				cursor.Reset(handle, start, end, count, query.Reverse);
				while (cursor.MoveNext())
				{
					if (!filter.Matches(cursor)) continue;
					if (filter.HasSourceChecks && !filter.VerifySources(cursor)) continue;
					yield return cursor;
					if (--remaining == 0) yield break;
				}
			}
			finally
			{
				handle.Release();
			}
		}
	}

    private static bool Contains(IReadOnlyList<ushort> list, ushort value)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == value) return true;
        }
        return false;
    }

    private void CheckNotDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(LogReader));
    }

    /// <summary>Query criteria translated to the schema of one segment.</summary>
    private sealed class SegmentFilter
    {
        private DateTime? from;
        private DateTime? to;
        private ulong requiredFlags;
        private ulong excludedFlags;
        private ushort? typeId;
        private IReadOnlyList<ushort> typeIds;
        private int[] fieldIndices;
        private ulong[] fieldValues;
        private string[] fieldSources;

        public bool HasSourceChecks { get; private set; }

        public static bool TryCreate(LogIndexSchema readerSchema, LogIndexSchema segmentSchema, LogQuery query, ushort? typeId, out SegmentFilter filter)
        {
            filter = null;
            if (!TryMapFlags(readerSchema, segmentSchema, query.RequiredFlags, true, out ulong required)) return false;
            TryMapFlags(readerSchema, segmentSchema, query.ExcludedFlags, false, out ulong excluded);

            var filters = query.FieldFilters;
            int filterCount = filters?.Count ?? 0;
            var indices = new int[filterCount];
            var values = new ulong[filterCount];
            var sources = new string[filterCount];
            bool hasSourceChecks = false;
            for (int i = 0; i < filterCount; i++)
            {
                var fieldFilter = filters[i];
                if (fieldFilter.FieldIndex < 0 || fieldFilter.FieldIndex >= readerSchema.Fields.Count) throw new ArgumentException($"Invalid field index {fieldFilter.FieldIndex}.", nameof(query));
                var readerField = readerSchema.Fields[fieldFilter.FieldIndex];
                if (!segmentSchema.TryGetFieldIndex(readerField.Name, out int segmentIndex)) return false;
                var segmentField = segmentSchema.Fields[segmentIndex];
                indices[i] = segmentIndex;
                if (fieldFilter.Source == null)
                {
                    values[i] = fieldFilter.Value;
                }
                else
                {
                    if (segmentField.Kind == LogFieldKind.Handle) throw new NotSupportedException($"Field '{segmentField.Name}': source filters on {LogFieldKind.Handle} fields are not supported yet.");
                    if (segmentField.Kind != LogFieldKind.Hash) throw new ArgumentException($"Field '{segmentField.Name}' has no source values.", nameof(query));
                    values[i] = LogFieldHash.Compute(fieldFilter.Source, segmentField.MaxValue);
                    sources[i] = fieldFilter.Source;
                    hasSourceChecks = true;
                }
                if (values[i] > segmentField.MaxValue) return false;
            }

            filter = new SegmentFilter
            {
                from = query.From?.ToUniversalTime(),
                to = query.To?.ToUniversalTime(),
                requiredFlags = required,
                excludedFlags = excluded,
                typeId = typeId,
                typeIds = query.TypeIds,
                fieldIndices = indices,
                fieldValues = values,
                fieldSources = sources,
                HasSourceChecks = hasSourceChecks
            };
            return true;
        }

        private static bool TryMapFlags(LogIndexSchema readerSchema, LogIndexSchema segmentSchema, ulong flags, bool requireAll, out ulong mapped)
        {
            const ulong reservedMask = (1UL << LogReservedFlags.Count) - 1;
            mapped = flags & reservedMask;
            for (int i = 0; i < readerSchema.FlagNames.Count; i++)
            {
                ulong readerMask = 1UL << (LogReservedFlags.Count + i);
                if ((flags & readerMask) == 0) continue;
                if (segmentSchema.TryGetFlagMask(readerSchema.FlagNames[i], out ulong segmentMask)) mapped |= segmentMask;
                else if (requireAll) return false;
            }
            return true;
        }

		public bool Matches(IndexCursor cursor)
		{
			var schema = cursor.Schema;
			var records = cursor.Records;
			int offset = cursor.Offset;

			ulong flags = LogIndexRecordCodec.ReadFlags(schema, records, offset);
			if ((flags & requiredFlags) != requiredFlags || (flags & excludedFlags) != 0) return false;

			ushort entryTypeId = LogIndexRecordCodec.ReadTypeId(schema, records, offset);
			if (typeId.HasValue && entryTypeId != typeId.Value) return false;
			if (typeIds != null && !Contains(typeIds, entryTypeId)) return false;

			if (from.HasValue || to.HasValue)
			{
				var timestamp = cursor.Handle.Header.FromTimeOffset(LogIndexRecordCodec.ReadTimeOffset(records, offset));
				if (from.HasValue && timestamp < from.Value) return false;
				if (to.HasValue && timestamp >= to.Value) return false;
			}

			for (int i = 0; i < fieldIndices.Length; i++)
			{
				if (LogIndexRecordCodec.ReadField(schema, records, offset, fieldIndices[i]) != fieldValues[i]) return false;
			}
			return true;
		}

		public bool VerifySources(IndexCursor cursor)
		{
			// One source array per filter (i.e. per segment), reused for all entries.
			if (sourceBuffer == null) sourceBuffer = new string[cursor.Schema.Fields.Count];
			else Array.Clear(sourceBuffer, 0, sourceBuffer.Length);
			var record = new LogRecord { FieldSources = sourceBuffer };
			if (!cursor.TryReadRecord(ref record)) return false;
			for (int i = 0; i < fieldSources.Length; i++)
			{
				if (fieldSources[i] != null && !string.Equals(record.FieldSources[fieldIndices[i]] ?? string.Empty, fieldSources[i], StringComparison.Ordinal)) return false;
			}
			return true;
		}

		private string[] sourceBuffer;
	}

	/// <summary>
	/// Iterates the index records of one segment in chunks, so queries never load whole index files.
	/// A chunk always also contains the record following the current one (if any), which bounds the payload length.
	/// Buffers are owned per query and not thread-static, because the scan yields between reads.
	/// </summary>
	private sealed class IndexCursor
	{
		private const int ChunkBytes = 64 * 1024;
		private byte[] chunk = Array.Empty<byte>();
		private byte[] payloadBuffer;
		private uint chunkStart, chunkCount;
		private uint start, end, count, n;
		private bool reverse;
		private int recordSize;

		public SegmentHandle Handle { get; private set; }
		public LogIndexSchema Schema => Handle.Schema;
		public byte[] Records => chunk;
		public uint EntryIndex { get; private set; }
		public int Offset { get; private set; }

		/// <summary>Provides a buffer for single-record probes; invalidates the loaded chunk.</summary>
		public byte[] GetProbeBuffer(int size)
		{
			if (chunk.Length < size) chunk = new byte[size];
			chunkCount = 0;
			return chunk;
		}

		public void Reset(SegmentHandle handle, uint start, uint end, uint count, bool reverse)
		{
			Handle = handle;
			recordSize = handle.Schema.RecordSize;
			this.start = start;
			this.end = end;
			this.count = count;
			this.reverse = reverse;
			n = 0;
			chunkCount = 0;
		}

		public bool MoveNext()
		{
			if (n >= end - start) return false;
			uint i = reverse ? end - 1 - n : start + n;
			n++;
			if (!IsLoaded(i) && !TryLoad(i)) return false;
			EntryIndex = i;
			Offset = (int)(i - chunkStart) * recordSize;
			return true;
		}

		// Record i is usable if it and its successor are loaded, or if it is the last record of the segment.
		private bool IsLoaded(uint i)
		{
			if (chunkCount == 0 || i < chunkStart) return false;
			uint chunkEnd = chunkStart + chunkCount;
			return i + 1 < chunkEnd || (i < chunkEnd && chunkEnd == count);
		}

		private bool TryLoad(uint i)
		{
			uint perChunk = (uint)Math.Max(2, ChunkBytes / recordSize);
			// Reverse scans load the chunk that ends with i (plus its successor), forward scans the one starting at i.
			uint first = reverse ? (i + 2 > perChunk ? Math.Max(start, i + 2 - perChunk) : start) : i;
			uint last = (uint)Math.Min(count, (long)first + perChunk);
			int bytes = (int)(last - first) * recordSize;
			if (chunk.Length < bytes) chunk = new byte[bytes];
			chunkCount = 0;
			if (!Handle.ReadIndex(Handle.HeaderSize + (long)first * recordSize, chunk, bytes)) return false;
			chunkStart = first;
			chunkCount = last - first;
			return true;
		}

		public LogEntryInfo GetInfo()
		{
			var header = Handle.Header;
			return new LogEntryInfo(
				new LogEntryId(header.SegmentSequence, EntryIndex),
				header.FromTimeOffset(LogIndexRecordCodec.ReadTimeOffset(chunk, Offset)),
				LogIndexRecordCodec.ReadFlags(Schema, chunk, Offset),
				LogIndexRecordCodec.ReadTypeId(Schema, chunk, Offset));
		}

		/// <summary>Reads the current record; its payload refers to an internal buffer that is reused by the next read.</summary>
		public bool TryReadRecord(ref LogRecord record)
		{
			long position = (long)LogIndexRecordCodec.ReadPosition(Schema, chunk, Offset);
			// Records are contiguous: the next indexed position (or the file end) bounds this record.
			long recordEnd = EntryIndex + 1 < count ? (long)LogIndexRecordCodec.ReadPosition(Schema, chunk, Offset + recordSize) : Handle.GetSegmentLength();
			return Handle.TryReadRecord(position, recordEnd, ref payloadBuffer, ref record);
		}
	}
}
