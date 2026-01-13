using FeatureLoom.Collections;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// A typed message log that buffers messages in a circular buffer and exposes read and write operations.
/// </summary>
/// <typeparam name="T">The message type to store.</typeparam>
public sealed class MessageLog<T> : IMessageSink<T>, ILogBuffer<T>
{
    /// <summary>
    /// The underlying circular log buffer storing messages.
    /// </summary>
    private readonly CircularLogBuffer<T> buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageLog{T}"/> class with the specified buffer size.
    /// </summary>
    /// <param name="bufferSize">The maximum number of messages retained in the buffer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bufferSize"/> is less than or equal to zero.</exception>
    public MessageLog(int bufferSize)
    {
        if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than zero.");
        buffer = new CircularLogBuffer<T>(bufferSize);
    }

    /// <summary>
    /// Gets the current number of messages stored in the buffer.
    /// </summary>
    public int CurrentSize => buffer.CurrentSize;

    /// <summary>
    /// Gets the latest message identifier added to the buffer.
    /// </summary>
    public long LatestId => buffer.LatestId;

    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    public int MaxSize => buffer.MaxSize;

    /// <summary>
    /// Gets the oldest message identifier that is still available in the buffer.
    /// </summary>
    public long OldestAvailableId => buffer.OldestAvailableId;

    /// <summary>
    /// Gets an async wait handle that is signaled when new messages are added.
    /// </summary>
    public IAsyncWaitHandle WaitHandle => buffer.WaitHandle;

    /// <summary>
    /// Gets the type of messages consumed by this sink.
    /// </summary>
    public Type ConsumedMessageType => typeof(T);

    /// <summary>
    /// Adds a message to the buffer.
    /// </summary>
    /// <param name="item">The message to add.</param>
    /// <returns>The identifier assigned to the added message.</returns>
    public long Add(T item) => buffer.Add(item);

    /// <summary>
    /// Adds a sequence of messages to the buffer.
    /// </summary>
    /// <param name="items">The messages to add.</param>
    public void AddRange<IEnum>(IEnum items) where IEnum : IEnumerable<T>
    {
        buffer.AddRange(items);
    }

    /// <summary>
    /// Retrieves available messages starting from a requested identifier, limited by a maximum count.
    /// </summary>
    /// <param name="firstRequestedId">The first message identifier requested.</param>
    /// <param name="maxItems">The maximum number of items to return.</param>
    /// <param name="firstProvidedId">Outputs the first identifier actually provided.</param>
    /// <param name="lastProvidedId">Outputs the last identifier actually provided.</param>
    /// <returns>An array of messages available.</returns>
    public T[] GetAllAvailable(long firstRequestedId, int maxItems, out long firstProvidedId, out long lastProvidedId) =>
        buffer.GetAllAvailable(firstRequestedId, maxItems, out firstProvidedId, out lastProvidedId);

    /// <summary>
    /// Retrieves all available messages starting from a requested identifier.
    /// </summary>
    /// <param name="firstRequestedId">The first message identifier requested.</param>
    /// <param name="firstProvidedId">Outputs the first identifier actually provided.</param>
    /// <param name="lastProvidedId">Outputs the last identifier actually provided.</param>
    /// <returns>An array of messages available.</returns>
    public T[] GetAllAvailable(long firstRequestedId, out long firstProvidedId, out long lastProvidedId) =>
        buffer.GetAllAvailable(firstRequestedId, out firstProvidedId, out lastProvidedId);

    /// <summary>
    /// Gets the latest message in the buffer.
    /// </summary>
    /// <returns>The latest message.</returns>
    public T GetLatest() => buffer.GetLatest();

    /// <summary>
    /// Posts a message to this sink if it is of the expected type.
    /// </summary>
    /// <typeparam name="M">The incoming message type.</typeparam>
    /// <param name="message">The message to post.</param>
    public void Post<M>(in M message)
    {
        if (message is T typedMessage) buffer.Add(typedMessage);
    }

    /// <summary>
    /// Posts a message to this sink if it is of the expected type.
    /// </summary>
    /// <typeparam name="M">The incoming message type.</typeparam>
    /// <param name="message">The message to post.</param>
    public void Post<M>(M message)
    {
        if (message is T typedMessage) buffer.Add(typedMessage);
    }

    /// <summary>
    /// Asynchronously posts a message to this sink if it is of the expected type.
    /// </summary>
    /// <typeparam name="M">The incoming message type.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <returns>A completed task.</returns>
    public Task PostAsync<M>(M message)
    {
        if (message is T typedMessage) buffer.Add(typedMessage);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears the buffer and resets its state.
    /// </summary>
    public void Reset() => buffer.Reset();

    /// <summary>
    /// Attempts to get a message by its identifier.
    /// </summary>
    /// <param name="number">The message identifier.</param>
    /// <param name="result">Outputs the message if found.</param>
    /// <returns><c>true</c> if the message was found; otherwise, <c>false</c>.</returns>
    public bool TryGetFromId(long number, out T result) => buffer.TryGetFromId(number, out result);

    /// <summary>
    /// Waits asynchronously until the specified identifier is available (or cancellation is requested).
    /// </summary>
    /// <param name="number">The message identifier to wait for.</param>
    /// <param name="ct">An optional cancellation token.</param>
    /// <returns>A task that completes when the identifier becomes available or is canceled.</returns>
    public Task WaitForIdAsync(long number, CancellationToken ct = default) =>
        ((IReadLogBuffer<T>)buffer).WaitForIdAsync(number, ct);
}
