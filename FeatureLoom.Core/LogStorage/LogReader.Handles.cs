using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.LogStorage;

public sealed partial class LogReader
{
	private readonly MicroLock handleLock = new MicroLock();
	private readonly Dictionary<uint, SegmentHandle> handles = new Dictionary<uint, SegmentHandle>();
	private readonly List<uint> evictionList = new List<uint>();
	private long nextEvictionCheckTicks;

	/// <summary>
	/// Cached segment handles that were not used for this time are closed, so long-running readers
	/// do not accumulate open files. Checked lazily on handle access; no timer is involved.
	/// Default: 1 minute. <see cref="TimeSpan.Zero"/> or negative disables eviction.
	/// </summary>
	public TimeSpan HandleIdleTimeout { get; set; } = TimeSpan.FromMinutes(1);

	private uint[] segmentSequences = Array.Empty<uint>();
	private DateTime segmentListStamp;
	private bool segmentListValid;

	/// <summary>
	/// Returns the sorted sequences of existing segments. The list is cached and only rebuilt when the directory's
	/// last-write time changes (creating or deleting files updates it), so queries usually avoid enumerating files.
	/// </summary>
	private uint[] GetSegmentSequences()
	{
		using (handleLock.Lock())
		{
			if (!System.IO.Directory.Exists(Directory))
			{
				segmentListValid = false;
				return segmentSequences = Array.Empty<uint>();
			}
			var stamp = System.IO.Directory.GetLastWriteTimeUtc(Directory);
			if (segmentListValid && stamp == segmentListStamp) return segmentSequences;

			var list = new List<uint>();
			foreach (var file in System.IO.Directory.GetFiles(Directory, LogSegmentFiles.IndexSearchPattern))
			{
				if (LogSegmentFiles.TryParseSequence(Path.GetFileName(file), out uint sequence)) list.Add(sequence);
			}
			list.Sort();
			segmentSequences = list.ToArray();
			segmentListStamp = stamp;
			segmentListValid = true;
			return segmentSequences;
		}
	}

	private void InvalidateSegmentList()
	{
		using (handleLock.Lock()) segmentListValid = false;
	}

	private static class NotFound<T>
	{
		public static readonly Task<(bool found, LogEntry<T> entry)> Task = System.Threading.Tasks.Task.FromResult((false, default(LogEntry<T>)));
	}

	/// <summary>
	/// Returns the cached handle of a segment with an acquired usage reference, opening it on first use.
	/// Null if the segment does not exist. The caller must call <see cref="SegmentHandle.Release"/> after use.
	/// </summary>
	private SegmentHandle AcquireHandle(uint sequence)
	{
		long now = NowMs();
		using (handleLock.Lock())
		{
			CheckNotDisposed();
			if (handles.TryGetValue(sequence, out var handle) || TryOpenHandle(sequence, out handle)) handle.Acquire(now);
			// Evict after acquiring, so the requested handle is protected (in use, fresh timestamp) and not closed and reopened.
			EvictIdleHandles(now);
			return handle;
		}
	}

	// Must be called under handleLock.
	private bool TryOpenHandle(uint sequence, out SegmentHandle handle)
	{
		handle = null;
		if (!System.IO.Directory.Exists(Directory)) return false;
		var files = System.IO.Directory.GetFiles(Directory, LogSegmentFiles.GetIndexSearchPattern(sequence));
		if (files.Length != 1) return false;
		handle = SegmentHandle.TryOpen(files[0], sequence);
		if (handle == null) return false;
		handles.Add(sequence, handle);
		return true;
	}

	// Monotonic milliseconds; int TickCount would wrap after ~25 days.
	private static long NowMs() =>
#if NET
		Environment.TickCount64;
#else
		System.Diagnostics.Stopwatch.GetTimestamp() / (System.Diagnostics.Stopwatch.Frequency / 1000);
#endif

	// Must be called under handleLock. Handles in use (usage count > 0) are never closed, so lock-free reads stay safe.
	private void EvictIdleHandles(long now)
	{
		var timeout = HandleIdleTimeout;
		if (timeout <= TimeSpan.Zero || handles.Count == 0) return;
		// Only scan periodically to keep the hot path cheap.
		if (now - nextEvictionCheckTicks < 0) return;
		long timeoutMs = (long)Math.Min(timeout.TotalMilliseconds, int.MaxValue);
		nextEvictionCheckTicks = now + Math.Max(1, timeoutMs / 4);

		foreach (var pair in handles)
		{
			if (pair.Value.IsIdle(now, timeoutMs)) evictionList.Add(pair.Key);
		}
		foreach (var sequence in evictionList)
		{
			handles[sequence].Dispose();
			handles.Remove(sequence);
		}
		evictionList.Clear();
	}

	private void DisposeHandles()
	{
		using (handleLock.Lock())
		{
			// Called after 'disposed' is set, so no new reads start; running reads finish on their handles first.
			foreach (var handle in handles.Values) handle.DisposeWhenUnused();
			handles.Clear();
		}
	}

	/// <summary>
	/// Open read handles of one segment for point reads without per-call allocations.
	/// Reads re-check the index length, so entries appended by a concurrent writer become visible.
	/// On .NET 8+ reads use explicit offsets (RandomAccess) with thread-local buffers, so concurrent reads need no lock.
	/// On older targets the shared FileStream position requires serializing reads with a MicroLock.
	/// </summary>
	private sealed class SegmentHandle
	{
		private int usage;
		private int disposeRequested;
		private int closed;
		private long lastUseMs;

		// Called under the reader's handleLock, so it cannot race with eviction.
		public void Acquire(long nowMs)
		{
			Interlocked.Increment(ref usage);
			Volatile.Write(ref lastUseMs, nowMs);
		}

		public void Release()
		{
			// Interlocked operations are full fences, so either Release or DisposeWhenUnused observes the other's write.
			if (Interlocked.Decrement(ref usage) == 0 && Volatile.Read(ref disposeRequested) != 0) CloseOnce();
		}

		// Called under handleLock; new usages also require handleLock, so an idle handle cannot be acquired concurrently.
		public bool IsIdle(long nowMs, long timeoutMs) => Volatile.Read(ref usage) == 0 && nowMs - Volatile.Read(ref lastUseMs) >= timeoutMs;

		/// <summary>Closes the files now, or when the last running read releases the handle.</summary>
		public void DisposeWhenUnused()
		{
			Interlocked.Exchange(ref disposeRequested, 1);
			if (Volatile.Read(ref usage) == 0) CloseOnce();
		}

		public void Dispose() => DisposeWhenUnused();

		private void CloseOnce()
		{
			if (Interlocked.CompareExchange(ref closed, 1, 0) == 0) CloseFiles();
		}
		public LogIndexFileHeader Header => header;
		public LogIndexSchema Schema => header.Schema;
		public int HeaderSize => headerSize;

		/// <summary>Number of complete index records; a torn record at the tail (concurrent append) is ignored.</summary>
		public uint GetCount() => (uint)((GetIndexLength() - headerSize) / header.Schema.RecordSize);

		public long GetSegmentLength() => GetSegmentFileLength();

		/// <summary>Reads <paramref name="count"/> index bytes at <paramref name="fileOffset"/> into the start of <paramref name="buffer"/>.</summary>
		public bool ReadIndex(long fileOffset, byte[] buffer, int count) => ReadIndexAt(fileOffset, buffer, count);

		/// <summary>Reads and frames the record in [position, end); the payload refers to <paramref name="buffer"/>, which grows as needed.</summary>
		public bool TryReadRecord(long position, long end, ref byte[] buffer, ref LogRecord record)
		{
			long length = end - position;
			if (position < 0 || length < 0 || length > int.MaxValue) return false;
			if (buffer == null || buffer.Length < length) buffer = new byte[Math.Max(length, Math.Max(256, (buffer?.Length ?? 0) * 2L))];
			if (!ReadSegmentAt(position, buffer, (int)length)) return false;
			return LogBinaryRecordFraming.TryRead(header.Schema, buffer, 0, (int)length, ref record) == LogRecordReadStatus.Ok;
		}

		/// <summary>
		/// Binary searches the first entry with timestamp &gt;= <paramref name="time"/> (or <paramref name="count"/> if none).
		/// Relies on non-decreasing timestamps within a segment, which the writer guarantees.
		/// </summary>
		public bool TryFindFirstAtOrAfter(DateTime time, uint count, byte[] probeBuffer, out uint index)
		{
			index = 0;
			if (time <= header.BaseTime) return true;
			int recordSize = header.Schema.RecordSize;
			uint lo = 0, hi = count;
			while (lo < hi)
			{
				uint mid = lo + (hi - lo) / 2;
				if (!ReadIndexAt(headerSize + (long)mid * recordSize, probeBuffer, recordSize)) return false;
				if (header.FromTimeOffset(LogIndexRecordCodec.ReadTimeOffset(probeBuffer, 0)) < time) lo = mid + 1;
				else hi = mid;
			}
			index = lo;
			return true;
		}

#if NET
		private long GetIndexLength() => RandomAccess.GetLength(indexHandle);
		private long GetSegmentFileLength() => RandomAccess.GetLength(segmentHandle);
		private bool ReadIndexAt(long offset, byte[] buffer, int count) => ReadExactly(indexHandle, buffer.AsSpan(0, count), offset);
		private bool ReadSegmentAt(long offset, byte[] buffer, int count) => ReadExactly(segmentHandle, buffer.AsSpan(0, count), offset);
#else
		private long GetIndexLength() { using (readLock.Lock()) return indexStream.Length; }
		private long GetSegmentFileLength() { using (readLock.Lock()) return segmentStream.Length; }
		private bool ReadIndexAt(long offset, byte[] buffer, int count) => ReadAt(indexStream, offset, buffer, count);
		private bool ReadSegmentAt(long offset, byte[] buffer, int count) => ReadAt(segmentStream, offset, buffer, count);

		// The streams' position is shared state, so positioned reads are serialized.
		private bool ReadAt(FileStream stream, long offset, byte[] buffer, int count)
		{
			using (readLock.Lock())
			{
				stream.Position = offset;
				return ReadExactly(stream, buffer, count);
			}
		}
#endif

		private readonly LogIndexFileHeader header;
		private readonly int headerSize;
#if NET
		// Despite the 'Win32' namespace (historic naming), SafeFileHandle, File.OpenHandle and RandomAccess are fully
		// cross-platform on .NET Core/.NET 5+: on Unix the handle wraps a file descriptor and RandomAccess.Read maps to pread,
		// so offset-based, lock-free reads work identically on Windows, Linux and macOS.
		private readonly Microsoft.Win32.SafeHandles.SafeFileHandle indexHandle;
		private readonly Microsoft.Win32.SafeHandles.SafeFileHandle segmentHandle;

		// Per-thread buffers: no shared mutable state between concurrent readers.
		[ThreadStatic] private static byte[] tsRecordBuffer;
		[ThreadStatic] private static byte[] tsPayloadBuffer;

		private SegmentHandle(Microsoft.Win32.SafeHandles.SafeFileHandle indexHandle, Microsoft.Win32.SafeHandles.SafeFileHandle segmentHandle, LogIndexFileHeader header, int headerSize)
		{
			this.indexHandle = indexHandle;
			this.segmentHandle = segmentHandle;
			this.header = header;
			this.headerSize = headerSize;
		}

		public static SegmentHandle TryOpen(string indexFile, uint sequence)
		{
			Microsoft.Win32.SafeHandles.SafeFileHandle index = null, segment = null;
			try
			{
				LogIndexFileHeader header;
				int headerSize;
				// Header parsing needs a Stream; a FileStream would own (and close) a passed-in handle, so use a separate one.
				using (var stream = new FileStream(indexFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
				{
					header = LogIndexFileHeader.Read(stream);
					headerSize = (int)stream.Position;
				}
				index = OpenRead(indexFile);
				if (header.SegmentSequence != sequence) throw new InvalidDataException($"Index file '{indexFile}' has sequence {header.SegmentSequence}, expected {sequence}.");
				segment = OpenRead(Path.ChangeExtension(indexFile, LogSegmentFiles.SegmentExtension));
				return new SegmentHandle(index, segment, header, headerSize);
			}
			catch (FileNotFoundException)
			{
				index?.Dispose();
				return null;
			}
			catch
			{
				index?.Dispose();
				segment?.Dispose();
				throw;
			}
		}

		// FileOptions.RandomAccess is only an access-pattern hint and is ignored where unsupported.
		// FileShare is advisory on Unix; ReadWrite|Delete lets the writer append and maintenance delete files while we read.
		// On Unix a deleted file stays readable through an open handle until it is disposed.
		private static Microsoft.Win32.SafeHandles.SafeFileHandle OpenRead(string file) =>
			File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileOptions.RandomAccess);

		public bool TryRead<T>(uint entryIndex, LogPayloadType<T> payloadType, out LogEntry<T> entry)
		{
			entry = default;
			var schema = header.Schema;
			int recordSize = schema.RecordSize;
			long count = (RandomAccess.GetLength(indexHandle) - headerSize) / recordSize;
			if (entryIndex >= count) return false;
			bool hasNext = entryIndex + 1 < count;

			var recordBuffer = tsRecordBuffer;
			if (recordBuffer == null || recordBuffer.Length < 2 * recordSize) tsRecordBuffer = recordBuffer = new byte[2 * recordSize];
			long indexOffset = headerSize + (long)entryIndex * recordSize;
			if (!ReadExactly(indexHandle, recordBuffer.AsSpan(0, hasNext ? 2 * recordSize : recordSize), indexOffset)) return false;

			ulong flags = LogIndexRecordCodec.ReadFlags(schema, recordBuffer, 0);
			if ((flags & LogReservedFlags.Deleted) != 0) return false;
			ushort typeId = LogIndexRecordCodec.ReadTypeId(schema, recordBuffer, 0);
			if (typeId != payloadType.TypeId) return false;

			long position = (long)LogIndexRecordCodec.ReadPosition(schema, recordBuffer, 0);
			// Records are contiguous: the next indexed position (or the file end) bounds this record.
			long end = hasNext ? (long)LogIndexRecordCodec.ReadPosition(schema, recordBuffer, recordSize) : RandomAccess.GetLength(segmentHandle);
			long length = end - position;
			if (position < 0 || length < 0 || length > int.MaxValue) return false;

			var payloadBuffer = tsPayloadBuffer;
			if (payloadBuffer == null || payloadBuffer.Length < length) tsPayloadBuffer = payloadBuffer = new byte[Math.Max(length, Math.Max(256, (payloadBuffer?.Length ?? 0) * 2L))];
			if (!ReadExactly(segmentHandle, payloadBuffer.AsSpan(0, (int)length), position)) return false;

			LogRecord record = default;
			if (LogBinaryRecordFraming.TryRead(schema, payloadBuffer, 0, (int)length, ref record) != LogRecordReadStatus.Ok) return false;
			// Decoding happens before the thread-local buffer can be reused, since this thread does not yield.
			if (!payloadType.Codec.TryDecode(record.Payload, out T payload)) return false;

			var timestamp = header.FromTimeOffset(LogIndexRecordCodec.ReadTimeOffset(recordBuffer, 0));
			entry = new LogEntry<T>(new LogEntryInfo(new LogEntryId(header.SegmentSequence, entryIndex), timestamp, flags, typeId), payload);
			return true;
		}

		private static bool ReadExactly(Microsoft.Win32.SafeHandles.SafeFileHandle handle, Span<byte> buffer, long fileOffset)
		{
			while (buffer.Length > 0)
			{
				int n = RandomAccess.Read(handle, buffer, fileOffset);
				if (n <= 0) return false;
				buffer = buffer.Slice(n);
				fileOffset += n;
			}
			return true;
		}

		private void CloseFiles()
		{
			indexHandle.Dispose();
			segmentHandle.Dispose();
		}
#else
		private readonly MicroLock readLock = new MicroLock();
		private readonly FileStream indexStream;
		private readonly FileStream segmentStream;
		private readonly byte[] recordBuffer;
		private byte[] payloadBuffer = new byte[256];
		private LogRecord record;

		private SegmentHandle(FileStream indexStream, FileStream segmentStream, LogIndexFileHeader header, int headerSize)
		{
			this.indexStream = indexStream;
			this.segmentStream = segmentStream;
			this.header = header;
			this.headerSize = headerSize;
			recordBuffer = new byte[header.Schema.RecordSize * 2];
		}

		public static SegmentHandle TryOpen(string indexFile, uint sequence)
		{
			FileStream index = null, segment = null;
			try
			{
				index = OpenRead(indexFile);
				var header = LogIndexFileHeader.Read(index);
				if (header.SegmentSequence != sequence) throw new InvalidDataException($"Index file '{indexFile}' has sequence {header.SegmentSequence}, expected {sequence}.");
				int headerSize = (int)index.Position;
				segment = OpenRead(Path.ChangeExtension(indexFile, LogSegmentFiles.SegmentExtension));
				return new SegmentHandle(index, segment, header, headerSize);
			}
			catch (FileNotFoundException)
			{
				index?.Dispose();
				return null;
			}
			catch
			{
				index?.Dispose();
				segment?.Dispose();
				throw;
			}
		}

		// bufferSize 1 disables the internal FileStream buffer: reads are exact-sized and random.
		private static FileStream OpenRead(string file) =>
			new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1, FileOptions.RandomAccess);

		public bool TryRead<T>(uint entryIndex, LogPayloadType<T> payloadType, out LogEntry<T> entry)
		{
			entry = default;
			var schema = header.Schema;
			int recordSize = schema.RecordSize;
			using (readLock.Lock())
			{
				long count = (indexStream.Length - headerSize) / recordSize;
				if (entryIndex >= count) return false;
				bool hasNext = entryIndex + 1 < count;
				indexStream.Position = headerSize + (long)entryIndex * recordSize;
				if (!ReadExactly(indexStream, recordBuffer, hasNext ? 2 * recordSize : recordSize)) return false;

				ulong flags = LogIndexRecordCodec.ReadFlags(schema, recordBuffer, 0);
				if ((flags & LogReservedFlags.Deleted) != 0) return false;
				ushort typeId = LogIndexRecordCodec.ReadTypeId(schema, recordBuffer, 0);
				if (typeId != payloadType.TypeId) return false;

				long position = (long)LogIndexRecordCodec.ReadPosition(schema, recordBuffer, 0);
				// Records are contiguous: the next indexed position (or the file end) bounds this record.
				long end = hasNext ? (long)LogIndexRecordCodec.ReadPosition(schema, recordBuffer, recordSize) : segmentStream.Length;
				long length = end - position;
				if (position < 0 || length < 0 || length > int.MaxValue) return false;
				if (payloadBuffer.Length < length) payloadBuffer = new byte[Math.Max(length, payloadBuffer.Length * 2L)];
				segmentStream.Position = position;
				if (!ReadExactly(segmentStream, payloadBuffer, (int)length)) return false;

				if (LogBinaryRecordFraming.TryRead(schema, payloadBuffer, 0, (int)length, ref record) != LogRecordReadStatus.Ok) return false;
				if (!payloadType.Codec.TryDecode(record.Payload, out T payload)) return false;

				var timestamp = header.FromTimeOffset(LogIndexRecordCodec.ReadTimeOffset(recordBuffer, 0));
				entry = new LogEntry<T>(new LogEntryInfo(new LogEntryId(header.SegmentSequence, entryIndex), timestamp, flags, typeId), payload);
				return true;
			}
		}

		private static bool ReadExactly(Stream stream, byte[] buffer, int count)
		{
			int offset = 0;
			while (offset < count)
			{
				int n = stream.Read(buffer, offset, count - offset);
				if (n <= 0) return false;
				offset += n;
			}
			return true;
		}

		private void CloseFiles()
		{
			using (readLock.Lock())
			{
				indexStream.Dispose();
				segmentStream.Dispose();
			}
		}
#endif
	}
}
