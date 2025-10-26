using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// A typed, non-blocking message receiver that can be connected into a message flow.
    /// Implementations are expected to be thread-safe unless stated otherwise.
    /// </summary>
    /// <typeparam name="T">Type of the received messages.</typeparam>
    public interface IReceiver<T> : IMessageSink<T>
    {
        /// <summary>
        /// True if the receiver currently holds no items.
        /// </summary>
        /// <remarks>
        /// This is a transient snapshot in concurrent environments; the state may change immediately after reading.
        /// </remarks>
        bool IsEmpty { get; }

        /// <summary>
        /// True if the receiver has reached its capacity limit.
        /// </summary>
        /// <remarks>
        /// Capacity semantics are implementation-specific (e.g., a queue size limit).
        /// </remarks>
        bool IsFull { get; }

        /// <summary>
        /// Approximate number of items currently held by the receiver.
        /// </summary>
        /// <remarks>
        /// This is a transient snapshot in concurrent environments; the value may change during/after the call.
        /// </remarks>
        int Count { get; }

        /// <summary>
        /// An async-capable wait handle that is signaled when items become available (implementation-specific).
        /// </summary>
        /// <remarks>
        /// Typical semantics for queue-based receivers:
        /// - Set when the first item is enqueued (i.e., transitions from empty to non-empty).
        /// - Reset when the last item is dequeued (i.e., transitions to empty).
        /// Consumers can use <see cref="IAsyncWaitHandle.WaitAsync()"/> or the provided WaitingTask.
        /// </remarks>
        IAsyncWaitHandle WaitHandle { get; }

        /// <summary>
        /// Optional notifier that emits availability events (e.g., a bool set/reset) as a message source.
        /// </summary>
        /// <remarks>
        /// Semantics are implementation-specific. For queue semantics, a value of true often indicates that items are available.
        /// </remarks>
        IMessageSource<bool> Notifier { get; }

        /// <summary>
        /// Tries to receive (dequeue) the next item without blocking.
        /// </summary>
        /// <param name="message">The next message if available; otherwise the default value.</param>
        /// <returns>True if an item was received; otherwise false.</returns>
        bool TryReceive(out T message);

        /// <summary>
        /// Tries to peek at the next item without removing it and without blocking.
        /// </summary>
        /// <param name="nextItem">The next item if available; otherwise the default value.</param>
        /// <returns>True if an item was available; otherwise false.</returns>
        bool TryPeek(out T nextItem);

        /// <summary>
        /// Receives up to <paramref name="maxItems"/> items into a provided or shared buffer (non-blocking).
        /// Returns an empty segment if no items are available or if <paramref name="maxItems"/> is less than or equal to zero.
        /// </summary>
        /// <param name="maxItems">Maximum number of items to receive (must be &gt; 0 to receive anything).</param>
        /// <param name="buffer">
        /// Prefer passing your own <see cref="SlicedBuffer{T}"/> instance to control lifetime and reuse.
        /// After processing, release the slice (e.g., <c>buffer.FreeSlice(ref slice)</c> if it is the latest slice)
        /// or reset the buffer to avoid keeping a large underlying array alive.
        /// If null, an implementation may use a thread-local shared <see cref="SlicedBuffer{T}"/>.
        /// The returned segment remains valid, but retaining it keeps the entire underlying buffer in memory.
        /// </param>
        /// <returns>An array segment containing the received items, or an empty segment.</returns>
        ArraySegment<T> ReceiveMany(int maxItems = 0, SlicedBuffer<T> buffer = null);

        /// <summary>
        /// Peeks up to <paramref name="maxItems"/> items into a provided or shared buffer (non-blocking, non-destructive).
        /// Returns an empty segment if no items are available or if <paramref name="maxItems"/> is less than or equal to zero.
        /// </summary>
        /// <param name="maxItems">Maximum number of items to peek (must be &gt; 0 to receive anything).</param>
        /// <param name="buffer">
        /// Prefer passing your own <see cref="SlicedBuffer{T}"/> instance to control lifetime and reuse.
        /// After processing, release the slice (e.g., <c>buffer.FreeSlice(ref slice)</c> if it is the latest slice)
        /// or reset the buffer to avoid keeping a large underlying array alive.
        /// If null, an implementation may use a thread-local shared <see cref="SlicedBuffer{T}"/>.
        /// The returned segment remains valid, but retaining it keeps the entire underlying buffer in memory.
        /// </param>
        /// <returns>An array segment containing the peeked items, or an empty segment.</returns>
        ArraySegment<T> PeekMany(int maxItems = 0, SlicedBuffer<T> buffer = null);
    }
}