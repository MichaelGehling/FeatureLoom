using FeatureLoom.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    // The latest work item posted from the own thread.
    private (SendOrPostCallback, object)? currentWorkItem;
    // Queue for pending work items posted from other threads or while a callback is running.
    private Queue<(SendOrPostCallback, object)> workItems = new();
    // Lock to protect access to the workItems queue.
    private MicroLock workItemsLock = new MicroLock();
    // The dedicated thread that processes all work items.
    private Thread thread;
    // Event used to signal the worker thread when new work is available.
    private AsyncManualResetEvent workItemsWaitHandle = new(false);
    // Indicates whether the context has been disposed and should stop processing.
    private volatile bool disposed;

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
        if (!currentWorkItem.HasValue && Thread.CurrentThread == thread)
        {
            currentWorkItem = (d, state);
            workItemsWaitHandle.Set(); // Signal that there is work to do
        }
        else
        {
            // Otherwise, enqueue the work item for later processing.
            using (workItemsLock.Lock())
            {
                workItems.Enqueue((d, state));
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
        using (var waitHandle = new ManualResetEventSlim())
        {
            Exception exception = null;
            using (workItemsLock.Lock())
            {                
                workItems.Enqueue((s =>
                {
                    try { d(s); }
                    catch (Exception ex) { exception = ex; }
                    finally { waitHandle.Set(); }
                }, state));
                workItemsWaitHandle.Set(); // Signal that there is work to do
            }
            waitHandle.Wait();                
            if (exception != null) throw exception;
        }
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
                        var (callback, state) = currentWorkItem.Value;
                        currentWorkItem = null;
                        callback(state);
                    }
                    else
                    {
                        // If there are queued items, enqueue the current one and process the next from the queue.
                        using (workItemsLock.Lock())
                        {
                            workItems.Enqueue(currentWorkItem.Value);
                            currentWorkItem = null;

                            var (callback, state) = workItems.Dequeue();
                            callback(state);
                        }
                    }
                }
                // If there are queued items, process the next one.
                else if (workItems.Count > 0)
                {
                    SendOrPostCallback callback;
                    object state;
                    using (workItemsLock.Lock())
                    {
                        (callback, state) = workItems.Dequeue();                        
                    }
                    callback(state);
                }
                // If no work is available, wait for new work to be posted.
                else
                {
                    workItemsWaitHandle.Reset(); // Reset the wait handle
                    // ensure no new work is posted before resetting the wait handle
                    if (workItems.Count == 0 && currentWorkItem == null && !disposed)
                    {
                        workItemsWaitHandle.Wait(); // Wait for new work items                
                    }
                }
            }
            catch (Exception ex)
            {
                // On exception, reset the wait handle and wait for new work.
                workItemsWaitHandle.Reset();
                if (workItems.Count == 0 && currentWorkItem == null && !disposed)
                {
                    workItemsWaitHandle.Wait();
                }

                // Log the exception asynchronously to avoid blocking the context thread.
                Task.Run(() => 
                {
                    OptLog.ERROR()?.Build($"Exception in SingleThreadSynchronizationContext", ex);
                });
            }
        }
    }

    /// <summary>
    /// Disposes the context and signals the dedicated thread to exit.
    /// </summary>
    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            workItemsWaitHandle.Set(); // Signal the thread to exit
        }
    }
}

