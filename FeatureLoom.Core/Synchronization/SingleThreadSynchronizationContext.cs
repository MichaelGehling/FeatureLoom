using FeatureLoom.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Synchronization;

/// <summary>
/// A SynchronizationContext that processes all posted work items on a single dedicated thread.
/// Ensures that all callbacks are executed sequentially on the same thread, similar to a UI message loop.
///
/// <para>Example usage:</para>
/// <code>
/// using FeatureLoom.Synchronization;
/// using System;
/// using System.Threading;
/// using System.Threading.Tasks;
///
/// // Create and set the context
/// using var context = new SingleThreadSynchronizationContext();
/// SynchronizationContext.SetSynchronizationContext(context);
///
/// // Post work to the context
/// context.Post(_ => Console.WriteLine($"Hello from thread {Thread.CurrentThread.ManagedThreadId}"), null);
///
/// // Run an async method on the context
/// context.Post(async _ =>
/// {
///     Console.WriteLine($"Before await, thread {Thread.CurrentThread.ManagedThreadId}");
///     await Task.Delay(100);
///     Console.WriteLine($"After await, thread {Thread.CurrentThread.ManagedThreadId}");
/// }, null);
/// </code>
/// </summary>
public sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
{
    /// <summary>
    /// Tracks the outcome of a <see cref="Send"/> work item so the blocked caller can distinguish
    /// between successful execution, a faulted callback and a work item that was never executed
    /// because the context was disposed.
    /// </summary>
    private sealed class SendCompletion
    {
        // Not disposed on purpose: the waiting caller must not dispose the handle while the
        // completing thread may still be inside Set(). The handle is cheap and collectable.
        public readonly ManualResetEventSlim WaitHandle = new ManualResetEventSlim(false);
        public Exception Exception;
        public bool Aborted;

        public void Complete(Exception exception)
        {
            Exception = exception;
            WaitHandle.Set();
        }

        public void Abort()
        {
            Aborted = true;
            WaitHandle.Set();
        }
    }

    /// <summary>
    /// A queued unit of work. <see cref="Completion"/> is null for items posted via <see cref="Post"/>.
    /// </summary>
    private readonly struct WorkItem
    {
        public readonly SendOrPostCallback Callback;
        public readonly object State;
        public readonly SendCompletion Completion;

        public WorkItem(SendOrPostCallback callback, object state, SendCompletion completion = null)
        {
            Callback = callback;
            State = state;
            Completion = completion;
        }
    }

    // The latest work item posted from the own thread.
    private WorkItem? currentWorkItem;
    // Queue for pending work items posted from other threads or while a callback is running.
    // It doubles as the registry of pending Send() callers: an item that is still in the queue
    // has provably not started running, so Dispose() can safely abort it.
    private Queue<WorkItem> workItems = new();
    // Lock to protect access to the workItems queue and the disposed flag transition.
    private MicroLock workItemsLock = new MicroLock();
    // The dedicated thread that processes all work items.
    private Thread thread;
    // Event used to signal the worker thread when new work is available.
    private AsyncManualResetEvent workItemsWaitHandle = new(false);
    // Indicates whether the context has been disposed and should stop processing.
    private volatile bool disposed;
    // Diagnostics: set while the context thread is parked in the idle wait (no work available).
    private volatile bool waitingForWork;
    // Diagnostics: number of work items the context thread started executing. Written ONLY by the
    // context thread, so a plain increment on a volatile field is sufficient - no interlocked needed.
    private volatile int startedWorkItemsCount;
    // Diagnostics: number of work items that finished execution. Same single-writer rule.
    // A difference to startedWorkItemsCount means a callback is currently running.
    private volatile int finishedWorkItemsCount;

    /// <summary>
    /// The dedicated thread used by this SynchronizationContext.
    /// </summary>
    public Thread Thread => thread;

    /// <summary>
    /// Number of work items that are queued and waiting to be executed.
    /// Intended for diagnostics/watchdogs: a permanently growing value indicates that the
    /// context thread cannot keep up or is blocked.
    /// The value is read without locking and is therefore only a snapshot.
    /// </summary>
    public int QueuedWorkItemsCount => workItems.Count;

    /// <summary>
    /// True while the context thread is parked in its idle wait because no work is available.
    /// A watchdog seeing <c>false</c> together with a non-zero <see cref="QueuedWorkItemsCount"/>
    /// and a stagnating <see cref="FinishedWorkItemsCount"/> has found a stuck callback.
    /// </summary>
    public bool IsWaitingForWork => waitingForWork;

    /// <summary>
    /// Number of work items the context thread started to execute since the context was created.
    /// </summary>
    public int StartedWorkItemsCount => startedWorkItemsCount;

    /// <summary>
    /// Number of work items that finished execution since the context was created.
    /// </summary>
    public int FinishedWorkItemsCount => finishedWorkItemsCount;

    /// <summary>
    /// True while the context thread is inside a work item callback.
    /// If this stays true while <see cref="FinishedWorkItemsCount"/> does not change, the callback is stuck.
    /// </summary>
    public bool IsExecutingWorkItem => startedWorkItemsCount != finishedWorkItemsCount;

    /// <summary>
    /// True if the context was disposed and the dedicated thread is shutting down or already stopped.
    /// </summary>
    public bool IsDisposed => disposed;

    /// <summary>
    /// Initializes a new instance and starts the dedicated thread.
    /// </summary>
    /// <param name="threadName">Optional name for the dedicated thread.</param>
    public SingleThreadSynchronizationContext(string threadName = "SingleThreadSynchronizationContext")
    {
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = threadName
        };
        thread.Start();
    }

    /// <summary>
    /// Posts a callback to be executed asynchronously on the dedicated thread.
    /// </summary>
    /// <param name="d">The delegate to invoke.</param>
    /// <param name="state">An object passed to the delegate.</param>
    public override void Post(SendOrPostCallback d, object state)
    {        
        if (disposed) throw new ObjectDisposedException(nameof(SingleThreadSynchronizationContext));

        // If called from the context thread and no work is running, set as current work item for immediate execution.
        // The thread check must come first: currentWorkItem is written by the context thread without
        // synchronization, so only the context thread itself may read it.
        if (Thread.CurrentThread == thread && !currentWorkItem.HasValue)
        {
            currentWorkItem = new WorkItem(d, state);
            workItemsWaitHandle.Set(); // Signal that there is work to do
        }
        else
        {
            // Otherwise, enqueue the work item for later processing.
            using (workItemsLock.Lock())
            {
                if (disposed) throw new ObjectDisposedException(nameof(SingleThreadSynchronizationContext));
                workItems.Enqueue(new WorkItem(d, state));
                workItemsWaitHandle.Set(); // Signal that there is work to do
            }
        }
    }

    /// <summary>
    /// Sends a callback to be executed synchronously on the dedicated thread.
    /// If called from the context thread, executes inline.
    /// Otherwise, blocks until the callback has completed.
    /// </summary>
    /// <param name="d">The delegate to invoke.</param>
    /// <param name="state">An object passed to the delegate.</param>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the context was disposed before the callback could be executed.
    /// </exception>
    public override void Send(SendOrPostCallback d, object state)
    {
        if (disposed) throw new ObjectDisposedException(nameof(SingleThreadSynchronizationContext));

        // If already on the context thread, execute directly.
        if (Thread.CurrentThread == thread)
        {
            d(state);
            return;
        }

        // Otherwise, enqueue the work item and wait for completion.
        var completion = new SendCompletion();
        using (workItemsLock.Lock())
        {
            // Checked under the lock: Dispose() flips the flag and drains the queue under the same
            // lock, so this item is either enqueued before the drain (and gets aborted) or rejected here.
            if (disposed) throw new ObjectDisposedException(nameof(SingleThreadSynchronizationContext));
            workItems.Enqueue(new WorkItem(d, state, completion));
            workItemsWaitHandle.Set(); // Signal that there is work to do
        }

        completion.WaitHandle.Wait();

        if (completion.Aborted) throw new ObjectDisposedException(nameof(SingleThreadSynchronizationContext));
        // Rethrow preserving the original stack trace.
        if (completion.Exception != null) ExceptionDispatchInfo.Capture(completion.Exception).Throw();
    }

    /// <summary>
    /// The main loop for the dedicated thread. Processes work items sequentially.
    /// </summary>
    private void Run()
    {
        SetSynchronizationContext(this);
        while (!disposed)
        {
            try
            {
                // If there is a current work item, process it.
                if (currentWorkItem.HasValue)
                {
                    if (workItems.Count == 0)
                    {
                        var item = currentWorkItem.Value;
                        currentWorkItem = null;
                        Execute(item);
                    }
                    else
                    {
                        WorkItem item;
                        // If there are queued items, enqueue the current one and process the next from the queue.
                        using (workItemsLock.Lock())
                        {
                            // Dispose() may have drained the queue in the meantime.
                            if (disposed) continue;
                            workItems.Enqueue(currentWorkItem.Value);
                            currentWorkItem = null;

                            item = workItems.Dequeue();
                        }
                        Execute(item);
                    }
                }
                // If there are queued items, process the next one.
                else if (workItems.Count > 0)
                {
                    WorkItem item;
                    using (workItemsLock.Lock())
                    {
                        // Dispose() may have drained the queue after the unsynchronized count check.
                        if (workItems.Count == 0) continue;
                        item = workItems.Dequeue();
                    }
                    Execute(item);
                }
                // If no work is available, wait for new work to be posted.
                // This is the ONLY place where the loop blocks, so the wait protocol is documented
                // and maintained here alone.
                else
                {
                    workItemsWaitHandle.Reset();

                    // The count MUST be read under the lock here.
                    // Reset() above discards any signal a producer raised before this point, so a
                    // stale read of 0 would make us block while an item is already queued and its
                    // wake-up has been erased. Taking the lock (which producers also hold while
                    // enqueuing) forces an up-to-date read and closes that window.
                    bool noWork;
                    using (workItemsLock.Lock())
                    {
                        noWork = workItems.Count == 0;
                    }

                    // A producer enqueuing AFTER this check is harmless: the event is level-triggered
                    // (sticky), so a Set() landing between here and Wait() leaves isSet == true and
                    // Wait() returns immediately. A Set() landing during Wait() is covered by the
                    // event's internal setCounter re-check under its monitor.
                    if (noWork && currentWorkItem == null && !disposed)
                    {
                        waitingForWork = true;
                        try
                        {
                            workItemsWaitHandle.Wait(); // Wait for new work items
                        }
                        finally
                        {
                            waitingForWork = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // A failing callback must not park the loop: pending work may still be queued.
                // Just log and continue; if there is genuinely nothing to do, the idle branch above
                // performs the blocking wait using the single, properly synchronized protocol.
                // Logged asynchronously to avoid blocking the context thread.
                Task.Run(() =>
                {
                    OptLog.ERROR()?.Build($"Exception in SingleThreadSynchronizationContext", ex);
                });
            }
        }
    }

    /// <summary>
    /// Executes a work item. For items originating from <see cref="Send"/> the outcome is reported
    /// to the blocked caller instead of being propagated into the loop's exception handler.
    /// </summary>
    private void Execute(in WorkItem item)
    {
        // Only ever incremented by the context thread, so no synchronization is required.
        startedWorkItemsCount++;
        try
        {
            var completion = item.Completion;
            if (completion == null)
            {
                item.Callback(item.State);
                return;
            }

            try
            {
                item.Callback(item.State);
                completion.Complete(null);
            }
            catch (Exception ex)
            {
                completion.Complete(ex);
            }
        }
        finally
        {
            finishedWorkItemsCount++;
        }
    }

    /// <summary>
    /// Disposes the context and signals the dedicated thread to exit.
    /// Work items that are still queued are aborted, so blocked <see cref="Send"/> callers
    /// are released with an <see cref="ObjectDisposedException"/> instead of waiting forever.
    /// </summary>
    public void Dispose()
    {
        WorkItem[] pending = null;
        using (workItemsLock.Lock())
        {
            if (disposed) return;
            disposed = true;
            if (workItems.Count > 0)
            {
                pending = workItems.ToArray();
                workItems.Clear();
            }
        }

        // Released outside the lock: an item still in the queue never started running,
        // so aborting it cannot race with its execution.
        if (pending != null)
        {
            foreach (var item in pending) item.Completion?.Abort();
        }

        workItemsWaitHandle.Set(); // Signal the thread to exit
    }
}

