using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Provides a high-performance, non-thread-safe buffer for receiving messages from an underlying <see cref="IReceiver{T}"/>.
/// 
/// - Maintains a local buffer of messages (using <c>ArraySegment&lt;T&gt;</c>) fetched from the underlying receiver.<br/>
/// - When the buffer is empty, it fetches up to <c>maxBufferSize</c> items in a single call.<br/>
/// - Subsequent <c>TryReceive</c> or <c>ReceiveMany</c> calls are served from the local buffer until it is depleted.<br/>
/// - This design minimizes synchronization overhead and is ideal for high-frequency, single-threaded consumption.
/// </summary>
public sealed class ReceiverBuffer<T> : IReceiver<T>
{
    // The underlying receiver (usually a QueueReceiver).
    IReceiver<T> receiver;

    // Used to signal when data is available for reading.
    AsyncManualResetEvent readerWakeEvent = new AsyncManualResetEvent(false);

    // The current buffer of messages available for consumption.
    ArraySegment<T> remainingBuffer = new();

    // The initial buffer slice, used for buffer management and freeing.
    ArraySegment<T> initialBuffer = new();

    // Maximum number of items to fetch from the underlying receiver in one batch.
    int maxBufferSize = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReceiverBuffer{T}"/> class.
    /// </summary>
    /// <param name="receiver">The underlying receiver to buffer messages from.</param>
    /// <param name="maxBufferSize">The maximum number of items to fetch in one batch from the underlying receiver.</param>
    public ReceiverBuffer(IReceiver<T> receiver, int maxBufferSize = 1000)
    {
        this.receiver = receiver;
        this.maxBufferSize = maxBufferSize;

        // Subscribe to the underlying receiver's notifier to update the local wake event.
        receiver.Notifier.ProcessMessage<bool>(set =>
        {
            if (set || remainingBuffer.Count > 0) readerWakeEvent.Set();
            else readerWakeEvent.Reset();
        });            
    }

    /// <summary>
    /// Gets the total number of items available (in the buffer and the underlying receiver).
    /// </summary>
    public int Count => receiver.Count + remainingBuffer.Count;

    /// <summary>
    /// Returns true if both the buffer and the underlying receiver are empty.
    /// </summary>
    public bool IsEmpty => receiver.IsEmpty && remainingBuffer.Count == 0;

    /// <summary>
    /// Returns true if the underlying receiver is full.
    /// Note: The local buffer is not considered for fullness.
    /// </summary>
    public bool IsFull => receiver.IsFull;

    /// <summary>
    /// Wait handle that is set when data is available for reading.
    /// </summary>
    public IAsyncWaitHandle WaitHandle => readerWakeEvent;

    /// <summary>
    /// Task that completes when data is available for reading.
    /// </summary>
    public Task WaitingTask => readerWakeEvent.WaitingTask;

    /// <summary>
    /// Notifier for data availability changes.
    /// </summary>
    public IMessageSource<bool> Notifier => readerWakeEvent;

    /// <summary>
    /// The type of messages consumed by this buffer.
    /// </summary>
    public Type ConsumedMessageType => typeof(T);

    /// <summary>
    /// Peeks up to <paramref name="maxItems"/> items without removing them from the buffer or underlying receiver.
    /// </summary>
    public ArraySegment<T> PeekMany(int maxItems = 0, SlicedBuffer<T> slicedBuffer = null)
    {
        if (IsEmpty || maxItems <= 0) return new ArraySegment<T>();
        if (slicedBuffer == null) slicedBuffer = SlicedBuffer<T>.Shared;

        if (remainingBuffer.Count == 0)
        {
            return receiver.PeekMany(maxItems, slicedBuffer);
        }
        else if (remainingBuffer.Count >= maxItems)
        {
            var peeked = slicedBuffer.GetSlice(maxItems);
            peeked.CopyFrom(remainingBuffer, 0, maxItems);
            return peeked;
        }
        else if (receiver.IsEmpty)
        {
            var peeked = slicedBuffer.GetSlice(remainingBuffer.Count);
            peeked.CopyFrom(remainingBuffer, 0, remainingBuffer.Count);
            return peeked;
        }
        else
        {
            var peekedFromReceiver = receiver.PeekMany(maxItems - remainingBuffer.Count, slicedBuffer);
            var totalPeeked = slicedBuffer.GetSlice(remainingBuffer.Count + peekedFromReceiver.Count);
            remainingBuffer.CopyTo(totalPeeked.Array, totalPeeked.Offset);
            peekedFromReceiver.CopyTo(totalPeeked.Array, totalPeeked.Offset + remainingBuffer.Count);
            return totalPeeked;
        }                                    
    }

    /// <summary>
    /// Receives up to <paramref name="maxItems"/> items, removing them from the buffer and/or underlying receiver.
    /// </summary>
    public ArraySegment<T> ReceiveMany(int maxItems = 0, SlicedBuffer<T> slicedBuffer = null)
    {
        if (IsEmpty || maxItems <= 0) return new ArraySegment<T>();
        if (slicedBuffer == null) slicedBuffer = SlicedBuffer<T>.Shared;

        ArraySegment<T> readItems = new ArraySegment<T>();
        if (remainingBuffer.Count == 0)
        {
            readItems = receiver.ReceiveMany(maxItems, slicedBuffer);
        }
        else if (remainingBuffer.Count >= maxItems)
        {
            readItems = slicedBuffer.GetSlice(maxItems);
            readItems.CopyFrom(remainingBuffer, 0, maxItems);
            remainingBuffer = new ArraySegment<T>(remainingBuffer.Array, remainingBuffer.Offset + maxItems, remainingBuffer.Count - maxItems);
        }
        else if (receiver.IsEmpty)
        {
            readItems = slicedBuffer.GetSlice(remainingBuffer.Count);
            readItems.CopyFrom(remainingBuffer, 0, remainingBuffer.Count);
            remainingBuffer = new ArraySegment<T>();
        }
        else
        {
            var readFromReceiver = receiver.ReceiveMany(maxItems - remainingBuffer.Count, slicedBuffer);
            readItems = slicedBuffer.GetSlice(remainingBuffer.Count + readFromReceiver.Count);
            remainingBuffer.CopyTo(readItems.Array, readItems.Offset);
            readFromReceiver.CopyTo(readItems.Array, readItems.Offset + remainingBuffer.Count);
            remainingBuffer = new ArraySegment<T>();
        }
        
        // Free the buffer slice if it is depleted.
        if (remainingBuffer.Count == 0)
        {
            SlicedBuffer<T>.Shared.FreeSlice(ref initialBuffer);
            initialBuffer = remainingBuffer;
        }
        if (IsEmpty) readerWakeEvent.Reset();
        return readItems;
    }

    /// <summary>
    /// Posts a message to the underlying receiver.
    /// </summary>
    public void Post<M>(in M message)
    {
        receiver.Post(in message);
    }

    /// <summary>
    /// Posts a message to the underlying receiver.
    /// </summary>
    public void Post<M>(M message)
    {
        receiver.Post(message);
    }

    /// <summary>
    /// Asynchronously posts a message to the underlying receiver.
    /// </summary>
    public Task PostAsync<M>(M message)
    {
        return receiver.PostAsync(message);
    }

    /// <summary>
    /// Attempts to peek the next item without removing it.
    /// </summary>
    public bool TryPeek(out T nextItem)
    {
        if (remainingBuffer.Count > 0)
        {
            nextItem = remainingBuffer.Array[remainingBuffer.Offset];
            return true;
        }

        return receiver.TryPeek(out nextItem);            
    }

    /// <summary>
    /// Attempts to receive the next item, removing it from the buffer or underlying receiver.
    /// Fetches a new batch from the underlying receiver if the buffer is empty.
    /// </summary>
    public bool TryReceive(out T message)
    {
        message = default;
        if (IsEmpty) return false;

        if (remainingBuffer.Count == 0)
        {
            // Fetch a new batch from the underlying receiver.
            initialBuffer = receiver.ReceiveMany(maxBufferSize, SlicedBuffer<T>.Shared);
            remainingBuffer = initialBuffer;
        }

        if (IsEmpty) return false;

        message = remainingBuffer.Array[remainingBuffer.Offset];                        
        if (remainingBuffer.Count > 1)
        {
            remainingBuffer = new ArraySegment<T>(remainingBuffer.Array, remainingBuffer.Offset + 1, remainingBuffer.Count - 1);
        }
        else
        {
            SlicedBuffer<T>.Shared.FreeSlice(ref initialBuffer);
            remainingBuffer = initialBuffer;
        }
        
        if (IsEmpty) readerWakeEvent.Reset();
        return true;
    }
}