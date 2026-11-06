using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.LogStorage;

/// <summary>
/// Receives the outcome of a queued append (see <see cref="LogWriter.Enqueue{T}(T, ILogAppendCallback, object)"/>).
/// </summary>
/// <remarks>
/// Called on the writer's background flush loop after the batch containing the entry was written and flushed.
/// Implementations must be fast and should not block; exceptions thrown by callbacks are swallowed.
/// </remarks>
public interface ILogAppendCallback
{
	/// <summary>The entry was written.</summary>
	/// <param name="id">Id of the written entry.</param>
	/// <param name="state">State passed on enqueue.</param>
	void OnAppended(LogEntryId id, object state);

	/// <summary>The entry could not be written.</summary>
	/// <param name="error">The cause.</param>
	/// <param name="state">State passed on enqueue.</param>
	void OnFailed(Exception error, object state);
}

// Group commit for asynchronous appends: callers enqueue, a single background loop writes all queued
// entries under one writeLock acquisition and flushes once per batch, then reports the outcome per item.
// Queue items are structs in reused lists; completion is reported via a callback plus state object, so no
// per-item wrapper objects are needed. Value type payloads are carried in pooled ValueWrapper<T> instances.
public sealed partial class LogWriter
{
	private readonly MicroLock queueLock = new MicroLock();
	private List<QueueItem> queue = new List<QueueItem>();
	private List<QueueItem> spareQueue = new List<QueueItem>();
	private Task flushLoop;
	private TaskCompletionSource<bool> batchFullSignal;
	private bool queueClosed;
	// Number of log entries (not items) in the queue; an AppendManyAsync item counts with all its entries.
	private int queuedEntries;
	// Signalled while the queue has room; only used if LogFlushPolicy.MaxQueuedEntries is set.
	private readonly AsyncManualResetEvent queueHasRoom = new AsyncManualResetEvent(true);

	/// <summary>
	/// Queues an entry for the background group commit without creating a task.
	/// </summary>
	/// <remarks>
	/// The outcome is reported to <paramref name="callback"/> (if given) after the entry's batch was written.
	/// Without a callback, write errors are not observed. Value type payloads are not boxed.
	/// </remarks>
	/// <typeparam name="T">Prepared payload type.</typeparam>
	/// <param name="payload">Payload to append.</param>
	/// <param name="callback">Optional outcome receiver.</param>
	/// <param name="state">Optional state passed to <paramref name="callback"/>.</param>
	public void Enqueue<T>(T payload, ILogAppendCallback callback = null, object state = null)
	{
		object carried = typeof(T).IsValueType ? ValueWrapper<T>.Wrap(payload) : (object)payload;
		EnqueueItem(new QueueItem(carried, ItemWriter<T>.Instance, callback, state));
	}

	private Task<LogEntryId> EnqueueAppend<T>(T payload, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<LogEntryId>(cancellationToken);
		// Single allocation (besides the task) for both cancellable and non-cancellable tokens.
		var cancellable = new CancellableAppend(cancellationToken);
		Enqueue(payload, CancellableCallback.Instance, cancellable);
		return cancellable.Task;
	}

	/// <summary>Queues all payloads as one item: they are written together or, if cancelled before writing started, not at all.</summary>
	/// <remarks>
	/// <see cref="IReadOnlyList{T}"/> inputs are used without copying and must not be modified until the task completes.
	/// Other enumerables are enumerated on the caller's thread into a pooled snapshot, so no user code runs under the write lock.
	/// </remarks>
	private Task<List<LogEntryId>> EnqueueAppendMany<T>(IEnumerable<T> payloads, CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<List<LogEntryId>>(cancellationToken);
		ManyAppend<T> many;
		if (payloads is IReadOnlyList<T> list)
		{
			if (list.Count == 0) return Task.FromResult(new List<LogEntryId>());
			many = list.Count == 1 ? new ManyAppend<T>(list[0], cancellationToken) : new ManyAppend<T>(list, null, cancellationToken);
		}
		else
		{
			using (var enumerator = payloads.GetEnumerator())
			{
				if (!enumerator.MoveNext()) return Task.FromResult(new List<LogEntryId>());
				T first = enumerator.Current;
				if (!enumerator.MoveNext()) many = new ManyAppend<T>(first, cancellationToken);
				else
				{
					var snapshot = SnapshotPool<T>.Take();
					snapshot.Add(first);
					do snapshot.Add(enumerator.Current); while (enumerator.MoveNext());
					many = new ManyAppend<T>(snapshot, snapshot, cancellationToken);
				}
			}
		}
		EnqueueItem(new QueueItem(many, ManyWriter<T>.Instance, ManyCallback<T>.Instance, many), many.Count);
		return many.Task;
	}

	/// <summary>Completes after all asynchronous appends queued before the call are written (or failed).</summary>
	/// <remarks>Cancellation only abandons the wait; queued entries are still written.</remarks>
	private Task DrainAsync(CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
		using (queueLock.Lock())
		{
			if (flushLoop == null) return Task.CompletedTask;
		}
		var marker = new CancellableAppend(cancellationToken);
		EnqueueItem(new QueueItem(null, null, DrainCallback.Instance, marker), 0);
		return marker.Task;
	}

	/// <param name="entryCount">Number of log entries carried by the item; limits count entries, not items.</param>
	private void EnqueueItem(in QueueItem item, int entryCount = 1)
	{
		TaskCompletionSource<bool> signal = null;
		bool closed = false;
		while (true)
		{
			using (queueLock.Lock())
			{
				if (queueClosed) closed = true;
				else
				{
					// Backpressure: block the caller while the queue is full. Drain markers (0 entries) are never blocked.
					// An item larger than the limit is admitted into an empty queue, otherwise it could never be queued.
					int limit = options.Flush.MaxQueuedEntries;
					if (limit > 0 && entryCount > 0 && flushLoop != null && queuedEntries > 0 && queuedEntries + entryCount > limit)
					{
						queueHasRoom.Reset();
						goto wait;
					}
					queue.Add(item);
					queuedEntries += entryCount;
					if (flushLoop == null)
					{
						flushLoop = Task.Run(FlushLoopAsync);
					}
					else if (batchFullSignal != null && queuedEntries >= options.Flush.MaxBufferedEntries)
					{
						signal = batchFullSignal;
						batchFullSignal = null;
					}
				}
			}
			break;
		wait:
			// A full queue also ends the buffer delay early, so the loop frees room immediately.
			TaskCompletionSource<bool> full;
			using (queueLock.Lock()) { full = batchFullSignal; batchFullSignal = null; }
			full?.TrySetResult(true);
			queueHasRoom.Wait();
		}
		// Async callers get a failure callback (faulted task) instead of a synchronous exception.
		if (closed)
		{
			item.Release();
			item.Fail(new ObjectDisposedException(nameof(LogWriter)));
		}
		signal?.TrySetResult(true);
	}

	private async Task FlushLoopAsync()
	{
		while (true)
		{
			TimeSpan delay = options.Flush.MaxBufferDelay;
			if (delay > TimeSpan.Zero)
			{
				Task fullTask = null;
				using (queueLock.Lock())
				{
					if (!queueClosed && queuedEntries < options.Flush.MaxBufferedEntries)
					{
						batchFullSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
						fullTask = batchFullSignal.Task;
					}
				}
				if (fullTask != null)
				{
					await Task.WhenAny(Task.Delay(delay), fullTask).ConfiguredAwait();
					using (queueLock.Lock()) batchFullSignal = null;
				}
			}

			List<QueueItem> batch;
			using (queueLock.Lock())
			{
				if (queue.Count == 0)
				{
					flushLoop = null;
					return;
				}
				batch = queue;
				queue = spareQueue;
				spareQueue = null;
				queuedEntries = 0;
				queueHasRoom.Set();
			}

			WriteBatch(batch);

			batch.Clear();
			using (queueLock.Lock()) spareQueue = batch;
		}
	}

	// Wall-clock time of the batch currently written by WriteBatch (only valid under writeLock).
	private DateTime batchNow;

	private void WriteBatch(List<QueueItem> batch)
	{
		// Per-item results are stored back into the struct list: Id on success, Error on failure.
		Exception batchError = null;
		using (writeLock.Lock())
		{
			if (disposed) batchError = new ObjectDisposedException(nameof(LogWriter));
			// One clock read per batch instead of per entry.
			batchNow = DateTime.UtcNow;
			bool anyWritten = false;
			for (int i = 0; i < batch.Count; i++)
			{
				var item = batch[i];
				if (item.Writer == null) continue;
				// A cancelled append is dropped; once writing started, cancellation no longer applies.
				if (item.State is ICancellableWrite cancellable && !cancellable.TryBeginWrite())
				{
					item.Release();
					item.Skipped = true;
					batch[i] = item;
					continue;
				}
				try
				{
					if (batchError == null)
					{
						// Write takes ownership of the payload (returns a pooled wrapper even if writing throws).
                        object payload = item.Payload;
                        item.Payload = null;
                        item.Id = item.Writer.Write(this, payload);
						anyWritten = true;
					}
				}
				catch (Exception ex)
				{
					item.Error = ex;
				}
				item.Release();
				batch[i] = item;
			}
			try
			{
				if (anyWritten) FlushStreams();
			}
			catch (Exception ex)
			{
				batchError = ex;
			}
		}

		// Reported outside the lock.
		for (int i = 0; i < batch.Count; i++)
		{
			var item = batch[i];
			if (item.Skipped) continue;
			var error = item.Error ?? batchError;
			if (error != null) item.Fail(error);
			else item.Succeed();
		}
	}

	private void WritePendingOnDispose()
	{
		Task loop;
		using (queueLock.Lock())
		{
			if (queueClosed) return;
			queueClosed = true;
			queueHasRoom.Set();
			loop = flushLoop;
			batchFullSignal?.TrySetResult(true);
		}
		// The loop writes everything queued before closing; it uses no captured context, so blocking is safe.
		loop?.GetAwaiter().GetResult();
	}

	private struct QueueItem
	{
		public object Payload;
		public readonly ItemWriter Writer;
		public readonly ILogAppendCallback Callback;
		public readonly object State;
		public LogEntryId Id;
		public Exception Error;
		public bool Skipped;

		public QueueItem(object payload, ItemWriter writer, ILogAppendCallback callback, object state)
		{
			Payload = payload;
			Writer = writer;
			Callback = callback;
			State = state;
			Id = default;
			Error = null;
			Skipped = false;
		}

		/// <summary>Returns a pooled value wrapper and drops the payload reference.</summary>
		public void Release()
		{
			if (Payload != null) Writer?.Release(Payload);
			Payload = null;
		}

		public void Succeed()
		{
			if (Callback == null) return;
			try { Callback.OnAppended(Id, State); } catch { }
		}

		public void Fail(Exception error)
		{
			if (Callback == null) return;
			try { Callback.OnFailed(error, State); } catch { }
		}
	}

	// One cached instance per payload type; unwraps value types without boxing.
	private abstract class ItemWriter
	{
		public abstract LogEntryId Write(LogWriter writer, object payload);
		public abstract void Release(object payload);
	}

	private sealed class ItemWriter<T> : ItemWriter
	{
		public static readonly ItemWriter<T> Instance = new ItemWriter<T>();

		public override LogEntryId Write(LogWriter writer, object payload)
		{
			T value = typeof(T).IsValueType ? ((ValueWrapper<T>)payload).UnwrapAndDispose() : (T)payload;
			return writer.AppendLocked(value, false, writer.ResolvePayloadType<T>(), writer.batchNow);
		}

		public override void Release(object payload)
		{
			if (typeof(T).IsValueType) ((ValueWrapper<T>)payload).UnwrapAndDispose();
		}
	}

	private sealed class TaskCallback : ILogAppendCallback
	{
		public static readonly TaskCallback Instance = new TaskCallback();
		public void OnAppended(LogEntryId id, object state) => ((TaskCompletionSource<LogEntryId>)state).TrySetResult(id);
		public void OnFailed(Exception error, object state) => ((TaskCompletionSource<LogEntryId>)state).TrySetException(error);
	}

	private interface ICancellableWrite
	{
		/// <summary>Returns false if cancelled; afterwards cancellation no longer applies.</summary>
		bool TryBeginWrite();
	}

	// Task completion plus cancellation in one object. State: 0 = queued, 1 = writing, 2 = cancelled.
	// For non-cancellable tokens Register is a no-op returning a default registration.
	private abstract class CancellableCompletion<TResult> : TaskCompletionSource<TResult>, ICancellableWrite
	{
		private int state;
		private readonly CancellationTokenRegistration registration;

		protected CancellableCompletion(CancellationToken cancellationToken) : base(TaskCreationOptions.RunContinuationsAsynchronously)
		{
			if (cancellationToken.CanBeCanceled) registration = cancellationToken.Register(s => ((CancellableCompletion<TResult>)s).TryCancel(), this);
		}

		private void TryCancel()
		{
			if (Interlocked.CompareExchange(ref state, 2, 0) == 0) TrySetCanceled();
		}

		public bool TryBeginWrite() => Interlocked.CompareExchange(ref state, 1, 0) == 0;

		public void Complete(TResult result, Exception error)
		{
			registration.Dispose();
			if (error != null) TrySetException(error);
			else TrySetResult(result);
		}
	}

	// Also used as drain marker: it has no writer, so cancellation only cancels the wait.
	private sealed class CancellableAppend : CancellableCompletion<LogEntryId>
	{
		public CancellableAppend(CancellationToken cancellationToken) : base(cancellationToken) { }
	}

	// One queue item for a whole AppendManyAsync call: payloads and resulting ids live in this object.
	// A single payload is stored inline, so it needs neither a snapshot nor the caller's list.
	private sealed class ManyAppend<T> : CancellableCompletion<List<LogEntryId>>
	{
		private IReadOnlyList<T> payloads;
		private List<T> pooledSnapshot;
		private T single;
		public readonly List<LogEntryId> Ids;

		public ManyAppend(T single, CancellationToken cancellationToken) : base(cancellationToken)
		{
			this.single = single;
			Ids = new List<LogEntryId>(1);
		}

		public ManyAppend(IReadOnlyList<T> payloads, List<T> pooledSnapshot, CancellationToken cancellationToken) : base(cancellationToken)
		{
			this.payloads = payloads;
			this.pooledSnapshot = pooledSnapshot;
			Ids = new List<LogEntryId>(payloads.Count);
		}

		public int Count => payloads?.Count ?? 1;

		public void Write(LogWriter writer)
		{
			var payloadType = writer.ResolvePayloadType<T>();
			if (payloads == null) Ids.Add(writer.AppendLocked(single, false, payloadType, writer.batchNow));
			else for (int i = 0; i < payloads.Count; i++) Ids.Add(writer.AppendLocked(payloads[i], false, payloadType, writer.batchNow));
		}

		/// <summary>Drops payload references and returns the pooled snapshot (called once by the queue).</summary>
		public void Release()
		{
			if (pooledSnapshot != null) SnapshotPool<T>.Return(pooledSnapshot);
			pooledSnapshot = null;
			payloads = null;
			single = default;
		}
	}

	private static class SnapshotPool<T>
	{
		static SnapshotPool()
		{
			SharedPool<List<T>>.TryInit(() => new List<T>(), list => list.Clear());
		}

		public static List<T> Take() => SharedPool<List<T>>.Take();

		public static void Return(List<T> list)
		{
			// Very large snapshots are not kept to avoid pinning memory.
			if (list.Capacity <= 4096) SharedPool<List<T>>.Return(list);
		}
	}

	private sealed class ManyWriter<T> : ItemWriter
	{
		public static readonly ManyWriter<T> Instance = new ManyWriter<T>();

		public override LogEntryId Write(LogWriter writer, object payload)
		{
			var many = (ManyAppend<T>)payload;
			try { many.Write(writer); }
			finally { many.Release(); }
			return default;
		}

		public override void Release(object payload) => ((ManyAppend<T>)payload).Release();
	}

	private sealed class ManyCallback<T> : ILogAppendCallback
	{
		public static readonly ManyCallback<T> Instance = new ManyCallback<T>();
		public void OnAppended(LogEntryId id, object state) => ((ManyAppend<T>)state).Complete(((ManyAppend<T>)state).Ids, null);
		public void OnFailed(Exception error, object state) => ((ManyAppend<T>)state).Complete(null, error);
	}

	private sealed class CancellableCallback : ILogAppendCallback
	{
		public static readonly CancellableCallback Instance = new CancellableCallback();
		public void OnAppended(LogEntryId id, object state) => ((CancellableAppend)state).Complete(id, null);
		public void OnFailed(Exception error, object state) => ((CancellableAppend)state).Complete(default, error);
	}

	private sealed class DrainCallback : ILogAppendCallback
	{
		public static readonly DrainCallback Instance = new DrainCallback();
		public void OnAppended(LogEntryId id, object state) => ((CancellableAppend)state).Complete(default, null);
		// A drain marker only signals progress; failures of other entries are reported to their own callers.
		public void OnFailed(Exception error, object state) => ((CancellableAppend)state).Complete(default, null);
	}
}
