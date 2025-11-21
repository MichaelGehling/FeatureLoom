using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

public static class ReceiverExtensions
{
    /// <summary>
    /// Tries to receive a request message and extract its payload and request id.
    /// </summary>
    /// <typeparam name="T">Type of the request payload.</typeparam>
    /// <param name="receiver">Receiver of request messages.</param>
    /// <param name="message">Outputs the request payload if successful; otherwise default.</param>
    /// <param name="requestId">Outputs the request id if successful; otherwise default.</param>
    /// <returns>True if a request was received; otherwise false.</returns>
    public static bool TryReceiveRequest<T>(this IReceiver<IRequestMessage<T>> receiver, out T message, out long requestId)
    {
        if (receiver.TryReceive(out IRequestMessage<T> request))
        {
            requestId = request.RequestId;
            message = request.Content;
            return true;
        }
        else
        {
            message = default;
            requestId = default;
            return false;
        }
    }

    /// <summary>
    /// Repeatedly waits (with cancellation) until a message can be received without blocking.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to read from.</param>
    /// <param name="message">Outputs the received message if successful; otherwise default.</param>
    /// <param name="token">Cancellation token to abort waiting.</param>
    /// <returns>True if a message was received; false if cancelled.</returns>
    public static bool TryReceive<T>(this IReceiver<T> receiver, out T message, CancellationToken token)
    {
        while (!receiver.TryReceive(out message))
        {
            if (!receiver.WaitHandle.Wait(token)) return false;
        }
        return true;
    }

    /// <summary>
    /// Repeatedly waits (with cancellation) until a message is available to peek without removing it.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to peek from.</param>
    /// <param name="message">Outputs the next message if successful; otherwise default.</param>
    /// <param name="token">Cancellation token to abort waiting.</param>
    /// <returns>True if a message was available to peek; false if cancelled.</returns>
    public static bool TryPeek<T>(this IReceiver<T> receiver, out T message, CancellationToken token)
    {
        while (!receiver.TryPeek(out message))
        {
            if (!receiver.WaitHandle.Wait(token)) return false;
        }
        return true;
    }

    /// <summary>
    /// Asynchronously waits (with cancellation) until a message can be received without blocking.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to read from.</param>
    /// <param name="token">Cancellation token to abort waiting.</param>
    /// <returns>
    /// A tuple where Item1 indicates success and Item2 is the received message (or default if cancelled).
    /// </returns>
    public static async Task<(bool, T)> TryReceiveAsync<T>(this IReceiver<T> receiver, CancellationToken token)
    {
        T message = default;
        while (!receiver.TryReceive(out message))
        {
            if (!(await receiver.WaitHandle.WaitAsync(token).ConfiguredAwait())) return (false, message);
        }
        return (true, message);
    }

    /// <summary>
    /// Asynchronously waits (with cancellation) until a message is available to peek without removing it.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to peek from.</param>
    /// <param name="token">Cancellation token to abort waiting.</param>
    /// <returns>
    /// A tuple where Item1 indicates success and Item2 is the peeked message (or default if cancelled).
    /// </returns>
    public static async Task<(bool, T)> TryPeekAsync<T>(this IReceiver<T> receiver, CancellationToken token)
    {
        T message = default;
        while (!receiver.TryPeek(out message))
        {
            if (!(await receiver.WaitHandle.WaitAsync(token).ConfiguredAwait())) return (false, message);
        }
        return (true, message);
    }

    /// <summary>
    /// Attempts to receive a message within the specified timeout.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to read from.</param>
    /// <param name="message">Outputs the received message if successful; otherwise default.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>True if a message was received before the timeout; otherwise false.</returns>
    public static bool TryReceive<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout)
    {
        if (receiver.TryReceive(out message)) return true;

        TimeFrame timer = new TimeFrame(timeout);
        do
        {
            if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample))) return false;
            if (receiver.TryReceive(out message)) return true;
        }
        while (!timer.Elapsed());

        message = default;
        return false;
    }

    /// <summary>
    /// Attempts to peek a message within the specified timeout.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to peek from.</param>
    /// <param name="message">Outputs the peeked message if successful; otherwise default.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>True if a message was available before the timeout; otherwise false.</returns>
    public static bool TryPeek<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout)
    {
        if (receiver.TryPeek(out message)) return true;

        TimeFrame timer = new TimeFrame(timeout);
        do
        {
            if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample))) return false;
            if (receiver.TryPeek(out message)) return true;
        }
        while (!timer.Elapsed());

        message = default;
        return false;
    }

    /// <summary>
    /// Asynchronously attempts to receive a message within the specified timeout.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to read from.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>
    /// A tuple where Item1 indicates success and Item2 is the received message (or default if timed out).
    /// </returns>
    public static async Task<(bool, T)> TryReceiveAsync<T>(this IReceiver<T> receiver, TimeSpan timeout)
    {
        T message;
        if (receiver.TryReceive(out message)) return (true, message);

        TimeFrame timer = new TimeFrame(timeout);
        do
        {
            if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample)).ConfiguredAwait())) return (false, default);
            if (receiver.TryReceive(out message)) return (true, message);
        }
        while (!timer.Elapsed());

        return (false, default);
    }

    /// <summary>
    /// Asynchronously attempts to peek a message within the specified timeout.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to peek from.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <returns>
    /// A tuple where Item1 indicates success and Item2 is the peeked message (or default if timed out).
    /// </returns>
    public static async Task<(bool, T)> TryPeekAsync<T>(this IReceiver<T> receiver, TimeSpan timeout)
    {
        T message;
        if (receiver.TryPeek(out message)) return (true, message);

        TimeFrame timer = new TimeFrame(timeout);
        do
        {
            if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample)).ConfiguredAwait())) return (false, default);
            if (receiver.TryPeek(out message)) return (true, message);
        }
        while (!timer.Elapsed());

        return (false, default);
    }

    /// <summary>
    /// Attempts to receive a message within the specified timeout or until cancelled.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to read from.</param>
    /// <param name="message">Outputs the received message if successful; otherwise default.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="token">Cancellation token to abort waiting.</param>
    /// <returns>True if a message was received before the timeout or cancellation; otherwise false.</returns>
    public static bool TryReceive<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout, CancellationToken token)
    {
        if (receiver.TryReceive(out message)) return true;

        TimeFrame timer = new TimeFrame(timeout);
        do
        {
            if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample), token)) return false;
            if (receiver.TryReceive(out message)) return true;
        }
        while (!timer.Elapsed());

        message = default;
        return false;
    }

    /// <summary>
    /// Attempts to peek a message within the specified timeout or until cancelled.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to peek from.</param>
    /// <param name="message">Outputs the peeked message if successful; otherwise default.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="token">Cancellation token to abort waiting.</param>
    /// <returns>True if a message was available before the timeout or cancellation; otherwise false.</returns>
    public static bool TryPeek<T>(this IReceiver<T> receiver, out T message, TimeSpan timeout, CancellationToken token)
    {
        if (receiver.TryPeek(out message)) return true;

        TimeFrame timer = new TimeFrame(timeout);
        do
        {
            if (!receiver.WaitHandle.Wait(timer.Remaining(timer.LastTimeSample), token)) return false;
            if (receiver.TryPeek(out message)) return true;
        }
        while (!timer.Elapsed());

        message = default;
        return false;
    }

    /// <summary>
    /// Asynchronously attempts to receive a message within the specified timeout or until cancelled.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to read from.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="token">Cancellation token to abort waiting.</param>
    /// <returns>
    /// A tuple where Item1 indicates success and Item2 is the received message (or default if timed out or cancelled).
    /// </returns>
    public static async Task<(bool, T)> TryReceiveAsync<T>(this IReceiver<T> receiver, TimeSpan timeout, CancellationToken token)
    {
        T message;
        if (receiver.TryReceive(out message)) return (true, message);

        TimeFrame timer = new TimeFrame(timeout);
        do
        {
            if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample), token).ConfiguredAwait())) return (false, default);
            if (receiver.TryReceive(out message)) return (true, message);
        }
        while (!timer.Elapsed());

        return (false, default);
    }

    /// <summary>
    /// Asynchronously attempts to peek a message within the specified timeout or until cancelled.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to peek from.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="token">Cancellation token to abort waiting.</param>
    /// <returns>
    /// A tuple where Item1 indicates success and Item2 is the peeked message (or default if timed out or cancelled).
    /// </returns>
    public static async Task<(bool, T)> TryPeekAsync<T>(this IReceiver<T> receiver, TimeSpan timeout, CancellationToken token)
    {
        T message;
        if (receiver.TryPeek(out message)) return (true, message);

        TimeFrame timer = new TimeFrame(timeout);
        do
        {
            if (!(await receiver.WaitHandle.WaitAsync(timer.Remaining(timer.LastTimeSample), token).ConfiguredAwait())) return (false, default);
            if (receiver.TryPeek(out message)) return (true, message);
        }
        while (!timer.Elapsed());

        return (false, default);
    }

    /// <summary>
    /// Receives all currently available items by requesting a very large batch.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to read from.</param>
    /// <returns>An <see cref="ArraySegment{T}"/> containing up to all currently available items.</returns>
    /// <remarks>
    /// Implementations may use a shared buffer if no explicit buffer is supplied internally.
    /// Do not hold onto the returned slice longer than necessary to avoid memory retention of the entire buffer.
    /// </remarks>
    public static ArraySegment<T> ReceiveAll<T>(this IReceiver<T> receiver)
    {
        return receiver.ReceiveMany(int.MaxValue, null);
    }

    /// <summary>
    /// Receives all currently available items into the provided buffer by requesting a very large batch.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to read from.</param>
    /// <param name="buffer">
    /// A reusable <see cref="SlicedBuffer{T}"/> to control slice lifetime and avoid retaining a shared buffer longer than necessary.
    /// After processing, consider freeing the slice (e.g., <c>buffer.FreeSlice(ref slice)</c>) if appropriate.
    /// </param>
    /// <returns>An <see cref="ArraySegment{T}"/> containing up to all currently available items.</returns>
    public static ArraySegment<T> ReceiveAll<T>(this IReceiver<T> receiver, SlicedBuffer<T> buffer)
    {
        return receiver.ReceiveMany(int.MaxValue, buffer);
    }

    /// <summary>
    /// Peeks all currently available items by requesting a very large batch without removing them.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to peek from.</param>
    /// <returns>An <see cref="ArraySegment{T}"/> containing up to all currently available items.</returns>
    /// <remarks>
    /// Implementations may use a shared buffer if no explicit buffer is supplied internally.
    /// Do not hold onto the returned slice longer than necessary to avoid memory retention of the entire buffer.
    /// </remarks>
    public static ArraySegment<T> PeekAll<T>(this IReceiver<T> receiver)
    {
        return receiver.PeekMany(int.MaxValue, null);
    }

    /// <summary>
    /// Peeks all currently available items into the provided buffer by requesting a very large batch, without removing them.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    /// <param name="receiver">The receiver to peek from.</param>
    /// <param name="buffer">
    /// A reusable <see cref="SlicedBuffer{T}"/> to control slice lifetime and avoid retaining a shared buffer longer than necessary.
    /// After processing, consider freeing the slice (e.g., <c>buffer.FreeSlice(ref slice)</c>) if appropriate.
    /// </param>
    /// <returns>An <see cref="ArraySegment{T}"/> containing up to all currently available items.</returns>
    public static ArraySegment<T> PeekAll<T>(this IReceiver<T> receiver, SlicedBuffer<T> buffer)
    {
        return receiver.PeekMany(int.MaxValue, buffer);
    }
}
