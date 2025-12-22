using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Message receiver that transparently wraps value type messages in pooled <see cref="ValueWrapper{T}"/> instances
    /// before enqueuing them, avoiding boxing allocations when the underlying <see cref="QueueReceiver{T}"/> expects <see cref="object"/>.
    /// </summary>
    /// <remarks>
    /// Purpose:
    /// - When sending a value type (struct) into a queue of <see cref="object"/>, .NET would normally box the value.
    ///   This class prevents boxing by storing the struct inside a pooled <see cref="ValueWrapper{T}"/> object.
    /// - Reference types are forwarded unchanged.
    ///
    /// Receiving:
    /// - Calls to <see cref="TryReceive(out object)"/>, <see cref="PeekMany"/>, <see cref="ReceiveMany"/> etc. may return
    ///   either the original reference type or a <see cref="ValueWrapper{T}"/> instance for value types.
    /// - The consumer is responsible for detecting <see cref="IValueWrapper"/> and unwrapping/disposing (call <c>UnwrapAndDispose()</c> on the concrete wrapper).
    ///   Failing to dispose wrappers will leak them from the pool (they remain rented and not reusable).
    ///
    /// Thread-safety:
    /// - Delegated entirely to the inner <see cref="QueueReceiver{T}"/>; this type adds no additional synchronization.
    ///
    /// Performance:
    /// - Wrapper creation uses a pooled object to reduce allocation pressure versus boxing which is always an allocation for structs.
    /// - Fast type check (<c>typeof(M).IsValueType</c>) determines wrapping path.
    /// </remarks>
    public class ValueWrappingQueueReceiver : IReceiver<object>, IAlternativeMessageSource, IAsyncWaitHandle, IMessageSink
    {
        private readonly QueueReceiver<object> innerReceiver;

        /// <summary>
        /// Creates a new <see cref="ValueWrappingQueueReceiver"/> with an internally constructed <see cref="QueueReceiver{T}"/>.
        /// </summary>
        /// <param name="maxQueueSize">Maximum number of items the queue will retain (older items dropped if exceeded).</param>
        /// <param name="maxWaitOnFullQueue">
        /// If non-default and <c>waitOnFullQueue</c> is enabled in <see cref="QueueReceiver{T}"/>, producers may block until space becomes available.
        /// </param>
        /// <param name="dropLatestMessageOnFullQueue">
        /// If true, when full the latest arriving message is dropped; otherwise the oldest existing message is evicted.
        /// </param>
        public ValueWrappingQueueReceiver(int maxQueueSize = int.MaxValue,
            TimeSpan maxWaitOnFullQueue = default,
            bool dropLatestMessageOnFullQueue = true)
        {
            innerReceiver = new QueueReceiver<object>(maxQueueSize, maxWaitOnFullQueue, dropLatestMessageOnFullQueue);
        }

        /// <summary>
        /// Wraps an existing <see cref="QueueReceiver{T}"/> which must be configured for <see cref="object"/> messages.
        /// </summary>
        /// <param name="queueReceiver">The underlying queue receiver.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="queueReceiver"/> is null.</exception>
        public ValueWrappingQueueReceiver(QueueReceiver<object> queueReceiver)
        {
            innerReceiver = queueReceiver ?? throw new ArgumentNullException(nameof(queueReceiver));
        }

        /// <summary>Indicates whether the underlying queue currently has no items.</summary>
        public bool IsEmpty => innerReceiver.IsEmpty;

        /// <summary>Indicates whether the underlying queue is at its configured capacity.</summary>
        public bool IsFull => innerReceiver.IsFull;

        /// <summary>The number of queued items.</summary>
        public int Count => innerReceiver.Count;

        /// <summary>Async-capable wait handle signalled when items become available.</summary>
        public IAsyncWaitHandle WaitHandle => innerReceiver.WaitHandle;

        /// <summary>Notifier source emitting availability events (implementation-specific semantics).</summary>
        public IMessageSource<bool> Notifier => innerReceiver.Notifier;

        /// <summary>The consumed message type of the underlying queue (always <see cref="object"/> here).</summary>
        public Type ConsumedMessageType => innerReceiver.ConsumedMessageType;

        /// <summary>Alternative source route (e.g., for overflow or error paths) exposed by the underlying queue.</summary>
        public IMessageSource Else => innerReceiver.Else;

        /// <summary>A task that completes when an item becomes available (implementation-specific).</summary>
        public Task WaitingTask => innerReceiver.WaitingTask;

        /// <summary>
        /// Non-destructively inspects up to <paramref name="maxItems"/> queued items.
        /// </summary>
        /// <param name="maxItems">Maximum number of items to return (0 means implementation default/all).</param>
        /// <param name="buffer">Optional reusable buffer abstraction to minimize allocations.</param>
        /// <returns>Segment containing the peeked items (may include <see cref="ValueWrapper{T}"/> instances).</returns>
        public ArraySegment<object> PeekMany(int maxItems = 0, SlicedBuffer<object> buffer = null) =>
            innerReceiver.PeekMany(maxItems, buffer);

        /// <summary>
        /// Posts a message by reference. Value types are wrapped in a pooled <see cref="ValueWrapper{T}"/> to avoid boxing.
        /// </summary>
        /// <typeparam name="M">Message type.</typeparam>
        /// <param name="message">Message instance.</param>
        public void Post<M>(in M message)
        {
            if (!typeof(M).IsValueType)
            {
                innerReceiver.Post(message);
            }
            else
            {
                var wrappedMessage = ValueWrapper<M>.Wrap(message);
                innerReceiver.Post(wrappedMessage);
            }
        }

        /// <summary>
        /// Posts a message. Value types are wrapped in a pooled <see cref="ValueWrapper{T}"/> to avoid boxing.
        /// </summary>
        /// <typeparam name="M">Message type.</typeparam>
        /// <param name="message">Message instance.</param>
        public void Post<M>(M message)
        {
            if (!typeof(M).IsValueType)
            {
                innerReceiver.Post(message);
            }
            else
            {
                var wrappedMessage = ValueWrapper<M>.Wrap(message);
                innerReceiver.Post(wrappedMessage);
            }
        }

        /// <summary>
        /// Asynchronously posts a message. Value types are wrapped in a pooled <see cref="ValueWrapper{T}"/> to avoid boxing.
        /// </summary>
        /// <typeparam name="M">Message type.</typeparam>
        /// <param name="message">Message instance.</param>
        /// <returns>Task completing when the post operation finishes.</returns>
        public Task PostAsync<M>(M message)
        {
            if (!typeof(M).IsValueType)
            {
                return innerReceiver.PostAsync(message);
            }
            else
            {
                var wrappedMessage = ValueWrapper<M>.Wrap(message);
                return innerReceiver.PostAsync(wrappedMessage);
            }
        }

        /// <summary>
        /// Receives (removes) up to <paramref name="maxItems"/> items from the queue.
        /// </summary>
        /// <param name="maxItems">Maximum number of items to remove (0 means implementation default/all available).</param>
        /// <param name="buffer">Optional sliced buffer to minimize allocations.</param>
        /// <returns>Segment of removed items (may include <see cref="ValueWrapper{T}"/> instances).</returns>
        public ArraySegment<object> ReceiveMany(int maxItems = 0, SlicedBuffer<object> buffer = null) =>
            innerReceiver.ReceiveMany(maxItems, buffer);

        /// <summary>
        /// Tries to expose a standard <see cref="WaitHandle"/> representing the wait state, if supported.
        /// </summary>
        /// <param name="waitHandle">Resulting wait handle.</param>
        /// <returns>True if conversion succeeded; otherwise false.</returns>
        public bool TryConvertToWaitHandle(out WaitHandle waitHandle) =>
            innerReceiver.TryConvertToWaitHandle(out waitHandle);

        /// <summary>
        /// Attempts to peek at the next item without removing it.
        /// </summary>
        /// <param name="nextItem">The next queued item (possibly a <see cref="ValueWrapper{T}"/>).</param>
        /// <returns>True if an item was available; false if queue empty.</returns>
        public bool TryPeek(out object nextItem) =>
            innerReceiver.TryPeek(out nextItem);

        /// <summary>
        /// Attempts to receive and remove the next item.
        /// </summary>
        /// <param name="message">The dequeued item (possibly a <see cref="ValueWrapper{T}"/>).</param>
        /// <returns>True if an item was received; false if queue empty.</returns>
        public bool TryReceive(out object message) =>
            innerReceiver.TryReceive(out message);

        /// <summary>
        /// Waits synchronously until at least one item is available.
        /// </summary>
        /// <returns>True if signalled; false if interrupted (implementation-specific).</returns>
        public bool Wait() => innerReceiver.Wait();

        /// <summary>
        /// Waits synchronously until an item is available or the timeout elapses.
        /// </summary>
        /// <param name="timeout">Maximum wait duration.</param>
        /// <returns>True if item became available; false if timeout or interrupted.</returns>
        public bool Wait(TimeSpan timeout) => innerReceiver.Wait(timeout);

        /// <summary>
        /// Waits synchronously until an item is available or cancellation is requested.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if item became available; false if cancelled or interrupted.</returns>
        public bool Wait(CancellationToken cancellationToken) => innerReceiver.Wait(cancellationToken);

        /// <summary>
        /// Waits synchronously until an item is available, timeout elapses, or cancellation is requested.
        /// </summary>
        /// <param name="timeout">Maximum wait duration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if item became available; false if timeout/cancelled.</returns>
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken) =>
            innerReceiver.Wait(timeout, cancellationToken);

        /// <summary>
        /// Asynchronously waits for an item to become available.
        /// </summary>
        /// <returns>Task resolving to true when signalled.</returns>
        public Task<bool> WaitAsync() => innerReceiver.WaitAsync();

        /// <summary>
        /// Asynchronously waits until an item is available or the timeout elapses.
        /// </summary>
        /// <param name="timeout">Maximum wait duration.</param>
        /// <returns>Task resolving to true if available; false if timeout.</returns>
        public Task<bool> WaitAsync(TimeSpan timeout) => innerReceiver.WaitAsync(timeout);

        /// <summary>
        /// Asynchronously waits until an item is available or cancellation is requested.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task resolving to true if available; false if cancelled.</returns>
        public Task<bool> WaitAsync(CancellationToken cancellationToken) => innerReceiver.WaitAsync(cancellationToken);

        /// <summary>
        /// Asynchronously waits until an item is available, timeout elapses, or cancellation is requested.
        /// </summary>
        /// <param name="timeout">Maximum wait duration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task resolving to true if available; false if timeout/cancelled.</returns>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
            innerReceiver.WaitAsync(timeout, cancellationToken);

        /// <summary>
        /// Indicates whether a wait operation would actually block at this moment.
        /// </summary>
        /// <returns>False if items are already available; true if waiting would block.</returns>
        public bool WouldWait() => innerReceiver.WouldWait();
    }
}
