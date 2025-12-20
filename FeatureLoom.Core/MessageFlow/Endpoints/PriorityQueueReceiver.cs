using FeatureLoom.Collections;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    ///     An endpoint with a priority queue to receive messages asynchronously and process them in
    ///     one or multiple threads. It is thread-safe. When the maximum queue limit is exceeded,
    ///     items are removed in dequeue order (as defined by the comparer) until the queue size is
    ///     back within the limit. Optionally the sender waits until either the timeout expires or a
    ///     consumer frees capacity.
    /// </summary>
    /// Uses a priority queue plus locking instead of a concurrent queue because of better
    /// performance in usual scenarios.
    /// <typeparam name="T"> The expected message type </typeparam>
    public sealed class PriorityQueueReceiver<T> : IReceiver<T>, IAlternativeMessageSource, IAsyncWaitHandle, IMessageSink<T>
    {
        private PriorityQueue<T> queue;
        private MicroLock queueLock = new MicroLock();

        public Type ConsumedMessageType => typeof(T);

        public bool waitOnFullQueue = false;
        public TimeSpan timeoutOnFullQueue;
        public int maxQueueSize = int.MaxValue;

        private AsyncManualResetEvent readerWakeEvent = new AsyncManualResetEvent(false);
        private AsyncManualResetEvent writerWakeEvent = new AsyncManualResetEvent(true);

        private LazyValue<SourceHelper> alternativeSendingHelper;

        /// <summary>
        /// Creates a new priority-ordered queue-based receiver.
        /// </summary>
        /// <param name="comparer">Comparer that defines the dequeue/priority ordering.</param>
        /// <param name="maxQueueSize">Maximum number of items allowed in the queue before full-queue behavior applies.</param>
        /// <param name="maxWaitOnFullQueue">
        /// Optional duration senders are willing to wait while the queue is full. Non-default values enable the waiting mode.
        /// </param>
        public PriorityQueueReceiver(IComparer<T> comparer, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default)
        {
            this.queue = new(comparer);
            this.maxQueueSize = maxQueueSize;
            this.waitOnFullQueue = maxWaitOnFullQueue != default;
            this.timeoutOnFullQueue = maxWaitOnFullQueue;
        }

        /// <summary>
        /// Gets the lazily-created alternative message source that receives messages routed away from the main queue.
        /// </summary>
        public IMessageSource Else => alternativeSendingHelper.Obj;

        /// <summary>
        /// Gets a value indicating whether the queue currently contains no items.
        /// </summary>
        public bool IsEmpty => queue.Count == 0;

        /// <summary>
        /// Gets a value indicating whether the queue currently reached its configured capacity.
        /// </summary>
        public bool IsFull => queue.Count >= maxQueueSize;

        /// <summary>
        /// Gets the current number of queued messages.
        /// </summary>
        public int Count => queue.Count;

        /// <summary>
        /// Gets the wait handle that becomes signaled when the reader side can proceed.
        /// </summary>
        public IAsyncWaitHandle WaitHandle => readerWakeEvent;

        /// <summary>
        /// Exposes the underlying wake event as a boolean message source for external observers.
        /// </summary>
        public IMessageSource<bool> Notifier => readerWakeEvent;

        /// <summary>
        /// Posts a message by reference, applying full-queue handling rules when necessary.
        /// </summary>
        /// <typeparam name="M">Actual runtime type of the message.</typeparam>
        /// <param name="message">Message instance passed by readonly reference.</param>
        public void Post<M>(in M message)
        {
            if (message is T typedMessage)
            {
                bool done = false;
                bool dropOldest = false;
                using (queueLock.Lock())
                {
                    if (!IsFull)
                    {
                        queue.Enqueue(typedMessage);
                        if (IsFull) writerWakeEvent.Reset();
                        done = true;
                    }
                    else if (!waitOnFullQueue)
                    {
                        writerWakeEvent.Reset();
                        
                        queue.Enqueue(typedMessage);
                        if (queue.Count > maxQueueSize)
                        {
                            dropOldest = true;
                            typedMessage = queue.Dequeue(false);
                        }
                        done = true;
                        
                    }
                }
                if (done)
                {
                    readerWakeEvent.Set();
                    if (dropOldest && alternativeSendingHelper.Exists) alternativeSendingHelper.Obj.Forward(in typedMessage);
                    return;
                }


                TimeFrame timeout = new TimeFrame(timeoutOnFullQueue);
                while (writerWakeEvent.Wait(timeout.Remaining()))
                {
                    using (queueLock.Lock())
                    {
                        if (!IsFull)
                        {
                            queue.Enqueue(typedMessage);
                            if (IsFull) writerWakeEvent.Reset();
                            done = true;
                        }
                    }
                    if (done)
                    {
                        readerWakeEvent.Set();
                        return;
                    }
                }

                using (queueLock.Lock())
                {
                    queue.Enqueue(typedMessage);
                    if (queue.Count > maxQueueSize)
                    {
                        dropOldest = true;
                        typedMessage = queue.Dequeue(false);
                    }
                }
                if (IsFull) writerWakeEvent.Reset();
                readerWakeEvent.Set();
                if (dropOldest && alternativeSendingHelper.Exists) alternativeSendingHelper.Obj.Forward(in typedMessage);
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(in message);
        }

        /// <summary>
        /// Posts a message by value, applying full-queue handling rules when necessary.
        /// </summary>
        /// <typeparam name="M">Actual runtime type of the message.</typeparam>
        /// <param name="message">Message instance.</param>
        public void Post<M>(M message)
        {
            if (message is T typedMessage)
            {
                bool done = false;
                bool dropOldest = false;
                using (queueLock.Lock())
                {
                    if (!IsFull)
                    {
                        queue.Enqueue(typedMessage);
                        if (IsFull) writerWakeEvent.Reset();
                        done = true;
                    }
                    else if (!waitOnFullQueue)
                    {
                        writerWakeEvent.Reset();
                        queue.Enqueue(typedMessage);
                        if (queue.Count > maxQueueSize)
                        {
                            dropOldest = true;
                            typedMessage = queue.Dequeue(false);
                        }
                        done = true;
                    }
                }
                if (done)
                {
                    readerWakeEvent.Set();
                    if (dropOldest && alternativeSendingHelper.Exists) alternativeSendingHelper.Obj.Forward(typedMessage);
                    return;
                }


                TimeFrame timeout = new TimeFrame(timeoutOnFullQueue);
                while (writerWakeEvent.Wait(timeout.Remaining()))
                {
                    using (queueLock.Lock())
                    {
                        if (!IsFull)
                        {
                            queue.Enqueue(typedMessage);
                            if (IsFull) writerWakeEvent.Reset();
                            done = true;
                        }
                    }
                    if (done)
                    {
                        readerWakeEvent.Set();
                        return;
                    }
                }

                using (queueLock.Lock())
                {
                    queue.Enqueue(typedMessage);
                    if (queue.Count > maxQueueSize)
                    {
                        dropOldest = true;
                        typedMessage = queue.Dequeue(false);
                    }
                }
                if (IsFull) writerWakeEvent.Reset();
                readerWakeEvent.Set();
                if (dropOldest && alternativeSendingHelper.Exists) alternativeSendingHelper.Obj.Forward(typedMessage);
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(message);
        }

        /// <summary>
        /// Posts a message asynchronously. When the queue is full and waiting is enabled, the sender will wait asynchronously.
        /// </summary>
        /// <typeparam name="M">Actual runtime type of the message.</typeparam>
        /// <param name="message">Message instance.</param>
        /// <returns>A task that completes when the message was enqueued or forwarded.</returns>
        public Task PostAsync<M>(M message)
        {
            if (message is T typedMessage)
            {
                bool done = false;
                bool dropOldest = false;
                using (queueLock.Lock())
                {
                    if (!IsFull)
                    {
                        queue.Enqueue(typedMessage);
                        if (IsFull) writerWakeEvent.Reset();
                        done = true;
                    }
                    else if (!waitOnFullQueue)
                    {
                        writerWakeEvent.Reset();
                        queue.Enqueue(typedMessage);
                        if (queue.Count > maxQueueSize)
                        {
                            dropOldest = true;
                            typedMessage = queue.Dequeue(false);
                        }
                        done = true;
                    }
                }
                if (done)
                {
                    readerWakeEvent.Set();
                    if (dropOldest && alternativeSendingHelper.Exists) return alternativeSendingHelper.Obj.ForwardAsync(typedMessage);
                    return Task.CompletedTask;
                }

                return PostWaitingAsync(typedMessage);
            }
            else return alternativeSendingHelper.ObjIfExists?.ForwardAsync(message) ?? Task.CompletedTask;
        }

        /// <summary>
        /// Enqueues a message while respecting the configured wait timeout when the queue is full.
        /// </summary>
        /// <param name="message">Message to enqueue.</param>
        /// <returns>A task that completes when the message was handled.</returns>
        public async Task PostWaitingAsync(T message)
        {
            bool success = false;
            TimeFrame timeout = new TimeFrame(timeoutOnFullQueue);
            while (await writerWakeEvent.WaitAsync(timeout.Remaining()))
            {
                using (queueLock.Lock())
                {
                    if (!IsFull)
                    {
                        queue.Enqueue(message);
                        if (IsFull) writerWakeEvent.Reset();
                        success = true;
                    }
                }
                if (success)
                {
                    readerWakeEvent.Set();
                    return;
                }
            }

            bool dropOldest = false;
            using (queueLock.Lock())
            {
                queue.Enqueue(message);
                if (queue.Count > maxQueueSize)
                {
                    dropOldest = true;
                    message = queue.Dequeue(false);
                }
            }
            if (dropOldest && alternativeSendingHelper.Exists) await alternativeSendingHelper.Obj.ForwardAsync(message);
            if (IsFull) writerWakeEvent.Reset();
            readerWakeEvent.Set();
            return;
        }

        /// <summary>
        /// Attempts to dequeue the next item in priority order in a thread-safe manner.
        /// </summary>
        /// <param name="message">Outputs the dequeued message when successful; otherwise default.</param>
        /// <returns>True if a message was removed; otherwise false.</returns>
        public bool TryReceive(out T message)
        {
            message = default;
            if (IsEmpty) return false;

            bool success = false;
            using (queueLock.Lock(true))
            {
                success = queue.TryDequeue(out message, true);
            }
            if (IsEmpty) readerWakeEvent.Reset();
            if (!IsFull) writerWakeEvent.Set();
            return success;
        }

        /// <summary>
        /// Dequeues up to the specified number of messages in priority order and returns them inside a sliced buffer.
        /// </summary>
        /// <param name="maxItems">Maximum number of items to dequeue.</param>
        /// <param name="slicedBuffer">Optional buffer provider; defaults to <see cref="SlicedBuffer{T}.Shared"/>.</param>
        /// <returns>An <see cref="ArraySegment{T}"/> containing the dequeued items.</returns>
        public ArraySegment<T> ReceiveMany(int maxItems = 0, SlicedBuffer<T> slicedBuffer = null)
        {
            if (IsEmpty || maxItems <= 0) return new ArraySegment<T>();
            ArraySegment<T> items;
            using (queueLock.Lock(true))
            {
                if (IsEmpty) return new ArraySegment<T>();

                if (slicedBuffer == null) slicedBuffer = SlicedBuffer<T>.Shared;
                var numItems = maxItems.ClampHigh(Count);
                items = slicedBuffer.GetSlice(numItems);

                if (Count == numItems)
                {
                    queue.CopyTo(items.Array, items.Offset);
                    queue.Clear();
                }
                else
                {
                    for (int i = 0; i < numItems; i++)
                    {
                        items.Array[i + items.Offset] = queue.Dequeue(true);
                    }
                }
            }
            if (IsEmpty) readerWakeEvent.Reset();
            if (!IsFull) writerWakeEvent.Set();
            return items;
        }

        /// <summary>
        /// Copies up to the specified number of items in priority order without removing them from the queue.
        /// </summary>
        /// <param name="maxItems">Maximum number of items to copy.</param>
        /// <param name="slicedBuffer">Optional buffer provider; defaults to <see cref="SlicedBuffer{T}.Shared"/>.</param>
        /// <returns>An <see cref="ArraySegment{T}"/> containing the peeked items.</returns>
        public ArraySegment<T> PeekMany(int maxItems = 0, SlicedBuffer<T> slicedBuffer = null)
        {
            if (IsEmpty || maxItems <= 0) return new ArraySegment<T>();
            ArraySegment<T> items;
            using (queueLock.LockReadOnly(true))
            {
                if (IsEmpty) return new ArraySegment<T>();

                if (slicedBuffer == null) slicedBuffer = SlicedBuffer<T>.Shared;
                var numItems = maxItems.ClampHigh(Count);
                items = slicedBuffer.GetSlice(numItems);

                if (queue.Count == numItems)
                {
                    queue.CopyTo(items.Array, items.Offset);
                }
                else
                {
                    queue.CopyToArray(items.Array, numItems, items.Offset);
                }
            }
            if (IsEmpty) readerWakeEvent.Reset();
            if (!IsFull) writerWakeEvent.Set();
            return items;
        }

        /// <summary>
        /// Attempts to peek at the next message in priority order without removing it.
        /// </summary>
        /// <param name="nextItem">Outputs the next item when available; otherwise default.</param>
        /// <returns>True if an item was read; otherwise false.</returns>
        public bool TryPeek(out T nextItem)
        {
            nextItem = default;
            if (IsEmpty) return false;

            using (queueLock.LockReadOnly(true))
            {
                if (IsEmpty) return false;
                nextItem = queue.Peek(true);
                return true;
            }
        }

        /// <summary>
        /// Removes all items from the queue and resets reader/writer events as needed.
        /// </summary>
        public void Clear()
        {
            using (queueLock.Lock(true))
            {
                queue.Clear();
            }
            if (IsEmpty) readerWakeEvent.Reset();
            if (!IsFull) writerWakeEvent.Set();
        }

        /// <summary>
        /// Gets the task that completes when the reader wait handle is signaled.
        /// </summary>
        public Task WaitingTask => WaitHandle.WaitingTask;

        /// <summary>
        /// Returns a thread-safe snapshot of the queue contents as plain objects.
        /// </summary>
        public object[] GetQueuedMesssages()
        {
            using (queueLock.Lock())
            {
                return Array.ConvertAll(queue.ToArray(), input => (object)input);
            }
        }

        /// <summary>
        /// Waits asynchronously until data becomes available or the wait handle is otherwise satisfied.
        /// </summary>
        public Task<bool> WaitAsync()
        {
            return WaitHandle.WaitAsync();
        }

        /// <summary>
        /// Waits asynchronously with a timeout until data becomes available.
        /// </summary>
        /// <param name="timeout">Maximum duration to wait.</param>
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitHandle.WaitAsync(timeout);
        }

        /// <summary>
        /// Waits asynchronously, observing a cancellation token, until data becomes available.
        /// </summary>
        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return WaitHandle.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Waits asynchronously while observing both timeout and cancellation token.
        /// </summary>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return WaitHandle.WaitAsync(timeout, cancellationToken);
        }

        /// <summary>
        /// Blocks the caller until data becomes available or the wait handle is otherwise satisfied.
        /// </summary>
        public bool Wait()
        {
            return WaitHandle.Wait();
        }

        /// <summary>
        /// Blocks with a timeout until data becomes available.
        /// </summary>
        public bool Wait(TimeSpan timeout)
        {
            return WaitHandle.Wait(timeout);
        }

        /// <summary>
        /// Blocks while observing the provided cancellation token.
        /// </summary>
        public bool Wait(CancellationToken cancellationToken)
        {
            return WaitHandle.Wait(cancellationToken);
        }

        /// <summary>
        /// Blocks with a timeout while observing a cancellation token.
        /// </summary>
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return WaitHandle.Wait(timeout, cancellationToken);
        }

        /// <summary>
        /// Returns true when the wait handle would block if waited on right now.
        /// </summary>
        public bool WouldWait()
        {
            return WaitHandle.WouldWait();
        }

        /// <summary>
        /// Attempts to expose the internal wait-handle as a <see cref="WaitHandle"/> instance.
        /// </summary>
        /// <param name="waitHandle">The resulting wait handle when conversion succeeds.</param>
        /// <returns>True when conversion succeeded.</returns>
        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            return WaitHandle.TryConvertToWaitHandle(out waitHandle);
        }


    }
}