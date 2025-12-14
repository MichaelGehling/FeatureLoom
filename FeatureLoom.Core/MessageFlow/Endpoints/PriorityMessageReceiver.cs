using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Receives messages of type <typeparamref name="T"/> and only accepts a message if it has
    /// equal or higher priority compared to the latest accepted message, based on the provided <see cref="IComparer{T}"/>.
    /// Lower-priority messages are forwarded to an alternative source (if configured).
    /// Also exposes asynchronous wait and receive capabilities via the wrapped <see cref="LatestMessageReceiver{T}"/>.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    public sealed class PriorityMessageReceiver<T> : IReceiver<T>, IAlternativeMessageSource, IAsyncWaitHandle, IMessageSink<T>
    {
        /// <summary>
        /// The underlying receiver storing the latest accepted message and providing wait/peek/receive operations.
        /// </summary>
        LatestMessageReceiver<T> receiver = new LatestMessageReceiver<T>();

        /// <summary>
        /// Lazily created helper used to forward rejected messages (lower priority) to an alternative message source.
        /// </summary>
        private LazyValue<SourceHelper> alternativeSendingHelper;

        /// <summary>
        /// Compares messages to determine their relative priority.
        /// </summary>
        IComparer<T> comparer;

        /// <summary>
        /// Initializes a new instance of <see cref="PriorityMessageReceiver{T}"/> with the specified <paramref name="comparer"/>.
        /// </summary>
        /// <param name="comparer">
        /// The comparer used to determine message priority. A message is accepted if
        /// <c>comparer.Compare(incoming, latest) &gt;= 0</c>, otherwise it is forwarded to the alternative source (if present).
        /// </param>
        public PriorityMessageReceiver(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }

        /// <summary>
        /// Gets an <see cref="IMessageSource"/> to which lower-priority messages are forwarded when rejected.
        /// </summary>
        public IMessageSource Else => alternativeSendingHelper.Obj;
        
        /// <summary>
        /// Gets a task that completes when a message becomes available.
        /// </summary>
        public Task WaitingTask => receiver.WaitingTask;

        /// <summary>
        /// Indicates whether the receiver currently has no accepted message.
        /// </summary>
        public bool IsEmpty => receiver.IsEmpty;

        /// <summary>
        /// Indicates whether the receiver buffer is full (if applicable).
        /// </summary>
        public bool IsFull => receiver.IsFull;

        /// <summary>
        /// Gets the number of messages currently buffered (if applicable).
        /// </summary>
        public int Count => receiver.Count;

        /// <summary>
        /// Gets an <see cref="IAsyncWaitHandle"/> to wait for messages asynchronously.
        /// </summary>
        public IAsyncWaitHandle WaitHandle => receiver.WaitHandle;

        /// <summary>
        /// Gets a notifier that publishes boolean signals related to message availability.
        /// </summary>
        public IMessageSource<bool> Notifier => receiver.Notifier;

        /// <summary>
        /// Gets the <see cref="Type"/> of messages consumed by this receiver.
        /// </summary>
        public Type ConsumedMessageType => receiver.ConsumedMessageType;

        /// <summary>
        /// Returns a view of up to <paramref name="maxItems"/> messages without removing them from the buffer.
        /// </summary>
        /// <param name="maxItems">Maximum number of items to peek. If 0, implementation-defined behavior (e.g., peek all available).</param>
        /// <param name="buffer">Optional buffer for slicing the underlying storage.</param>
        /// <returns>An <see cref="ArraySegment{T}"/> representing the peeked messages.</returns>
        public ArraySegment<T> PeekMany(int maxItems = int.MaxValue, SlicedBuffer<T> buffer = null)
        {
            return receiver.PeekMany(maxItems, buffer);
        }

        /// <summary>
        /// Checks whether the incoming <paramref name="message"/> meets the priority requirement.
        /// If rejected and an alternative source exists, forwards the message to that source.
        /// </summary>
        /// <typeparam name="M">The incoming message type.</typeparam>
        /// <param name="message">The incoming message instance.</param>
        /// <returns>True if the message is accepted; otherwise false.</returns>
        private bool CheckPriority<M>(in M message)
        {
            bool ok = true;
            if (message is not T typedMessage) ok = false;
            else if (receiver.HasMessage &&
                (comparer.Compare(typedMessage, receiver.LatestMessageOrDefault) < 0)) ok = false;
            
            if (!ok && alternativeSendingHelper.Exists)
            {
                alternativeSendingHelper.Obj.Forward(in message);
            }
            return ok;
        }

        /// <summary>
        /// Posts a message by reference. The message is accepted only if it meets the priority requirement.
        /// Rejected messages are forwarded to the alternative source if available.
        /// </summary>
        /// <typeparam name="M">The message type.</typeparam>
        /// <param name="message">The message to post.</param>
        public void Post<M>(in M message)
        {
            if (!CheckPriority(in message)) return;
            ((IMessageSink)receiver).Post(message);
        }

        /// <summary>
        /// Posts a message by value. The message is accepted only if it meets the priority requirement.
        /// Rejected messages are forwarded to the alternative source if available.
        /// </summary>
        /// <typeparam name="M">The message type.</typeparam>
        /// <param name="message">The message to post.</param>
        public void Post<M>(M message)
        {
            if (!CheckPriority(in message)) return;
            ((IMessageSink)receiver).Post(message);
        }

        /// <summary>
        /// Asynchronously posts a message. The message is accepted only if it meets the priority requirement.
        /// Rejected messages are forwarded to the alternative source if available.
        /// </summary>
        /// <typeparam name="M">The message type.</typeparam>
        /// <param name="message">The message to post.</param>
        /// <returns>
        /// A task that completes when the post operation has been handled. If the message is rejected,
        /// the returned task completes immediately.
        /// </returns>
        public Task PostAsync<M>(M message)
        {
            if (!CheckPriority(in message)) return Task.CompletedTask;
            return ((IMessageSink)receiver).PostAsync(message);
        }

        /// <summary>
        /// Receives up to <paramref name="maxItems"/> messages, removing them from the buffer.
        /// </summary>
        /// <param name="maxItems">Maximum number of items to receive. If 0, implementation-defined behavior (e.g., receive all available).</param>
        /// <param name="buffer">Optional buffer for slicing the underlying storage.</param>
        /// <returns>An <see cref="ArraySegment{T}"/> representing the received messages.</returns>
        public ArraySegment<T> ReceiveMany(int maxItems = int.MaxValue, SlicedBuffer<T> buffer = null)
        {
            return receiver.ReceiveMany(maxItems, buffer);
        }

        /// <summary>
        /// Attempts to convert the asynchronous wait handle to a synchronous <see cref="WaitHandle"/>.
        /// </summary>
        /// <param name="waitHandle">The resulting wait handle on success.</param>
        /// <returns>True if the conversion succeeded; otherwise false.</returns>
        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            return receiver.TryConvertToWaitHandle(out waitHandle);
        }

        /// <summary>
        /// Attempts to peek at the next message without removing it from the buffer.
        /// </summary>
        /// <param name="nextItem">The next item if available.</param>
        /// <returns>True if a message was available to peek; otherwise false.</returns>
        public bool TryPeek(out T nextItem)
        {
            return receiver.TryPeek(out nextItem);
        }

        /// <summary>
        /// Attempts to receive (remove) the next message from the buffer.
        /// </summary>
        /// <param name="message">The received message if available.</param>
        /// <returns>True if a message was received; otherwise false.</returns>
        public bool TryReceive(out T message)
        {
            return receiver.TryReceive(out message);
        }

        /// <summary>
        /// Blocks until a message is available.
        /// </summary>
        /// <returns>True when a message becomes available.</returns>
        public bool Wait()
        {
            return receiver.Wait();
        }

        /// <summary>
        /// Blocks until a message is available or the timeout elapses.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>True if a message became available within the timeout; otherwise false.</returns>
        public bool Wait(TimeSpan timeout)
        {
            return receiver.Wait(timeout);
        }

        /// <summary>
        /// Blocks until a message is available or cancellation is requested.
        /// </summary>
        /// <param name="cancellationToken">The token used to cancel the wait.</param>
        /// <returns>True if a message became available before cancellation; otherwise false.</returns>
        public bool Wait(CancellationToken cancellationToken)
        {
            return receiver.Wait(cancellationToken);
        }

        /// <summary>
        /// Blocks until a message is available, the timeout elapses, or cancellation is requested.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="cancellationToken">The token used to cancel the wait.</param>
        /// <returns>True if a message became available before timeout or cancellation; otherwise false.</returns>
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return receiver.Wait(timeout, cancellationToken);
        }

        /// <summary>
        /// Asynchronously waits until a message is available.
        /// </summary>
        /// <returns>A task that completes with true when a message becomes available.</returns>
        public Task<bool> WaitAsync()
        {
            return receiver.WaitAsync();
        }

        /// <summary>
        /// Asynchronously waits until a message is available or the timeout elapses.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <returns>A task that completes with true if a message became available; otherwise false.</returns>
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return receiver.WaitAsync(timeout);
        }

        /// <summary>
        /// Asynchronously waits until a message is available or cancellation is requested.
        /// </summary>
        /// <param name="cancellationToken">The token used to cancel the wait.</param>
        /// <returns>A task that completes with true if a message became available before cancellation; otherwise false.</returns>
        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return receiver.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Asynchronously waits until a message is available, the timeout elapses, or cancellation is requested.
        /// </summary>
        /// <param name="timeout">The maximum time to wait.</param>
        /// <param name="cancellationToken">The token used to cancel the wait.</param>
        /// <returns>A task that completes with true if a message became available before timeout or cancellation; otherwise false.</returns>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return receiver.WaitAsync(timeout, cancellationToken);
        }

        /// <summary>
        /// Indicates whether a wait would currently block (i.e., no message available).
        /// </summary>
        /// <returns>True if a wait would block; otherwise false.</returns>
        public bool WouldWait()
        {
            return receiver.WouldWait();
        }
    }
}