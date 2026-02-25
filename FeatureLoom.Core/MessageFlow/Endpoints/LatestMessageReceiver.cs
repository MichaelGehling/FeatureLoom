using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Holds only the latest message of type <typeparamref name="T"/> and exposes non-blocking receive/peek APIs.
/// Signals availability via an <see cref="AsyncManualResetEvent"/>.
/// </summary>
/// <remarks>
/// - Availability is determined by an internal occupancy flag (<c>hasMessage</c>), not by the event state.
/// - Producers set the payload inside a lock and signal outside the lock to reduce lock contention for waiters.
/// - Consumers clear the payload and reset the event under the same lock to preserve linearizable transitions.
/// </remarks>
public sealed class LatestMessageReceiver<T> : IMessageSink<T>, IReceiver<T>, IAlternativeMessageSource, IAsyncWaitHandle
{
    private AsyncManualResetEvent readerWakeEvent = new AsyncManualResetEvent(false);
    private MicroLock myLock = new MicroLock();
    private T receivedMessage;
    private bool hasMessage;
    private LazyValue<SourceHelper> alternativeSendingHelper;
    readonly bool isAtomic = !typeof(T).IsValueType || System.IntPtr.Size >= System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

    /// <summary>
    /// Provides an alternative source route for messages that are not of type <typeparamref name="T"/>.
    /// </summary>
    public IMessageSource Else => alternativeSendingHelper.Obj;

    /// <inheritdoc />
    public bool IsEmpty => !Volatile.Read(ref hasMessage);

    /// <inheritdoc />
    public bool IsFull => false;

    /// <inheritdoc />
    public int Count => Volatile.Read(ref hasMessage) ? 1 : 0;

    /// <inheritdoc />
    public IAsyncWaitHandle WaitHandle => readerWakeEvent;

    /// <summary>
    /// Notifier that emits availability changes as boolean messages.
    /// </summary>
    public IMessageSource<bool> Notifier => readerWakeEvent;

    /// <summary>
    /// True if a message is currently held.
    /// </summary>
    public bool HasMessage => Volatile.Read(ref hasMessage);

    /// <summary>
    /// Returns the currently stored latest message or the default value if none is stored.
    /// </summary>
    public T LatestMessageOrDefault
    {
        get
        {
            if (isAtomic)
            {
                if (!Volatile.Read(ref hasMessage)) return default;
                return receivedMessage;
            }
            else
            {
                using (myLock.LockReadOnly())
                {
                    return receivedMessage;
                }
            }
        }
    }

    /// <summary>
    /// The message type consumed by this receiver.
    /// </summary>
    public Type ConsumedMessageType => typeof(T);

    /// <inheritdoc />
    public void Post<M>(in M message)
    {
        if (message is T typedMessage)
        {
            using (myLock.Lock())
            {
                receivedMessage = typedMessage;
                Volatile.Write(ref hasMessage, true);
            }
            // Signal outside the lock to avoid waking a waiter that then blocks on the lock.
            readerWakeEvent.Set();
        }
        else alternativeSendingHelper.ObjIfExists?.Forward(in message);
    }

    /// <inheritdoc />
    public void Post<M>(M message)
    {
        if (message is T typedMessage)
        {
            using (myLock.Lock())
            {
                receivedMessage = typedMessage;
                Volatile.Write(ref hasMessage, true);
            }
            readerWakeEvent.Set();
        }
        else alternativeSendingHelper.ObjIfExists?.Forward(message);
    }

    /// <inheritdoc />
    public Task PostAsync<M>(M message)
    {
        if (message is T typedMessage)
        {
            using (myLock.Lock())
            {
                receivedMessage = typedMessage;
                Volatile.Write(ref hasMessage, true);
            }
            readerWakeEvent.Set();
            return Task.CompletedTask;
        }
        else return alternativeSendingHelper.ObjIfExists?.ForwardAsync(message) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Tries to receive the latest message and clears the slot if successful.
    /// </summary>
    /// <param name="message">The received message if available; otherwise default.</param>
    /// <returns>True if a message was received; otherwise false.</returns>
    public bool TryReceive(out T message)
    {
        message = default;
        if (IsEmpty) return false;

        using (myLock.Lock(true))
        {
            if (!hasMessage) return false; // plain read OK inside lock
            message = receivedMessage;
            receivedMessage = default;
            hasMessage = false; // plain write; relies on lock release for visibility
            readerWakeEvent.Reset();
            return true;
        }
    }

    /// <summary>
    /// Receives up to one item into the provided or shared sliced buffer.
    /// Returns an empty segment when no message is available or <paramref name="maxItems"/> ≤ 0.
    /// </summary>
    public ArraySegment<T> ReceiveMany(int maxItems = int.MaxValue, SlicedBuffer<T> slicedBuffer = null)
    {
        if (IsEmpty || maxItems <= 0) return new ArraySegment<T>();
        using (myLock.Lock(true))
        {
            if (!hasMessage) return new ArraySegment<T>();
            if (slicedBuffer == null) slicedBuffer = SlicedBuffer<T>.Shared;
            ArraySegment<T> items = slicedBuffer.GetSlice(1);
            items.Array[items.Offset] = receivedMessage;
            receivedMessage = default;
            hasMessage = false;
            readerWakeEvent.Reset();
            return items;
        }
    }

    /// <summary>
    /// Peeks up to one item into the provided or shared sliced buffer without clearing it.
    /// Returns an empty segment when no message is available or <paramref name="maxItems"/> ≤ 0.
    /// </summary>
    public ArraySegment<T> PeekMany(int maxItems = int.MaxValue, SlicedBuffer<T> slicedBuffer = null)
    {
        if (IsEmpty || maxItems <= 0) return new ArraySegment<T>();
        using (myLock.Lock(true))
        {
            if (!hasMessage) return new ArraySegment<T>();
            if (slicedBuffer == null) slicedBuffer = SlicedBuffer<T>.Shared;
            ArraySegment<T> items = slicedBuffer.GetSlice(1);
            items.Array[items.Offset] = receivedMessage;
            return items;
        }
    }

    /// <summary>
    /// Tries to peek the latest message without removing it.
    /// </summary>
    public bool TryPeek(out T nextItem)
    {
        nextItem = default;
        if (IsEmpty) return false;

        using (myLock.Lock())
        {
            if (!hasMessage) return false;
            nextItem = receivedMessage;
            return true;
        }
    }

    /// <summary>
    /// Clears the stored message and resets the availability signal.
    /// </summary>
    public void Clear()
    {
        using (myLock.Lock())
        {
            receivedMessage = default;
            hasMessage = false;
            readerWakeEvent.Reset();
        }
    }

    /// <summary>
    /// Returns the queued messages as objects. For this receiver, the result contains at most a single element.
    /// </summary>
    public object[] GetQueuedMesssages()
    {
        if (IsEmpty) return Array.Empty<object>();

        using (myLock.Lock())
        {
            if (!Volatile.Read(ref hasMessage)) return Array.Empty<object>();
            T message = receivedMessage;
            return message.ToSingleEntryArray<object>();
        }
    }

    /// <inheritdoc />
    public Task<bool> WaitAsync()
    {
        return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync();
    }

    /// <inheritdoc />
    public Task<bool> WaitAsync(TimeSpan timeout)
    {
        return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync(timeout);
    }

    /// <inheritdoc />
    public Task<bool> WaitAsync(CancellationToken cancellationToken)
    {
        return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync(timeout, cancellationToken);
    }

    /// <inheritdoc />
    public bool Wait()
    {
        return ((IAsyncWaitHandle)readerWakeEvent).Wait();
    }

    /// <inheritdoc />
    public bool Wait(TimeSpan timeout)
    {
        return ((IAsyncWaitHandle)readerWakeEvent).Wait(timeout);
    }

    /// <inheritdoc />
    public bool Wait(CancellationToken cancellationToken)
    {
        return ((IAsyncWaitHandle)readerWakeEvent).Wait(cancellationToken);
    }

    /// <inheritdoc />
    public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return ((IAsyncWaitHandle)readerWakeEvent).Wait(timeout, cancellationToken);
    }

    /// <inheritdoc />
    public bool WouldWait()
    {
        return ((IAsyncWaitHandle)readerWakeEvent).WouldWait();
    }

    /// <inheritdoc />
    public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
    {
        return ((IAsyncWaitHandle)readerWakeEvent).TryConvertToWaitHandle(out waitHandle);
    }

    /// <inheritdoc />
    public Task WaitingTask => ((IAsyncWaitHandle)readerWakeEvent).WaitingTask;
}