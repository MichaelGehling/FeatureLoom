using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Forwards every posted message to all currently connected sinks without modification. Thread-safe.
/// </summary>
/// <remarks>
/// BEHAVIOR
/// - Acts purely as a pass-through: no filtering, transformation, buffering, suppression or ordering changes.
/// - Forwards any message type; suitable for heterogeneous flows.
/// PERFORMANCE
/// - Forwarding is allocation-free on hot paths.
/// - Async forwarding preserves sink index order and avoids an async state machine when all sinks complete synchronously.
/// USAGE
/// - Use this variant when you do not want implicit type filtering.
/// - Use <see cref="Forwarder{T}"/> to express intended message type in the graph.
/// - Combine freely with typed or untyped connectors; this element imposes no type constraints.
/// </remarks>
public sealed class Forwarder : IMessageFlowConnection
{
    private SourceValueHelper sourceHelper;

    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;
    public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMessageSink[] GetConnectedSinks() => sourceHelper.GetConnectedSinks();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DisconnectAll() => sourceHelper.DisconnectAll();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DisconnectFrom(IMessageSink sink) => sourceHelper.DisconnectFrom(sink);

    /// <summary>
    /// Forwards the message by reference to all connected sinks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Post<M>(in M message) => sourceHelper.Forward(in message);

    /// <summary>
    /// Forwards the message by value to all connected sinks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Post<M>(M message) => sourceHelper.Forward(message);

    /// <summary>
    /// Asynchronously forwards the message to all connected sinks in index order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PostAsync<M>(M message) => sourceHelper.ForwardAsync(message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ConnectTo(IMessageSink sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnected(IMessageSink sink) => sourceHelper.IsConnected(sink);
}


/// <summary>
/// Typed forwarder that only forwards messages assignable to <typeparamref name="T"/>; other messages are silently ignored. Thread-safe.
/// </summary>
/// <typeparam name="T">The message type this forwarder intends to propagate.</typeparam>
/// <remarks>
/// FILTERING
/// - Performs a single runtime type check (message is T); non-matching messages are discarded without side effects.
/// - This silent filtering enables flexible composition in mixed typed/untyped graphs without logging or exceptions.
/// PERFORMANCE
/// - Overhead of the type check is typically negligible relative to sink dispatch, 
/// but when used in performance-critical paths consider using an untyped <see cref="Forwarder"/> instead.
/// USAGE
/// - Choose this to document intent and constrain propagation to a specific type.
/// - Downstream typed sinks may still validate compatibility; this element reduces unnecessary work for non-matching messages.
/// - To observe or branch on non-T messages, insert a Filter before this forwarder.
/// ASYNC
/// - Asynchronous forwarding preserves sink index order and avoids allocations when completions are synchronous.
/// LIMITATION
/// - Non-T messages are not visible here; they vanish by design.
/// </remarks>
public sealed class Forwarder<T> : IMessageFlowConnection<T>
{
    private TypedSourceValueHelper<T> sourceHelper;

    /// <summary>Static message type forwarded by this instance.</summary>
    public Type SentMessageType => typeof(T);

    /// <summary>Static message type consumed (same as sent).</summary>
    public Type ConsumedMessageType => typeof(T);

    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;
    public bool NoConnectedSinks => sourceHelper.NotConnected;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ConnectTo(IMessageSink sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DisconnectAll() => sourceHelper.DisconnectAll();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DisconnectFrom(IMessageSink sink) => sourceHelper.DisconnectFrom(sink);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMessageSink[] GetConnectedSinks() => sourceHelper.GetConnectedSinks();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConnected(IMessageSink sink) => sourceHelper.IsConnected(sink);

    /// <summary>
    /// Forwards the message by reference if it is assignable to <typeparamref name="T"/>; otherwise does nothing (silent drop).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Post<M>(in M message)
    {
        if (message is T typedMessage) sourceHelper.Forward(in typedMessage);
    }

    /// <summary>
    /// Forwards the message by value if it is assignable to <typeparamref name="T"/>; otherwise does nothing (silent drop).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Post<M>(M message)
    {
        if (message is T typedMessage) sourceHelper.Forward(typedMessage);
    }

    /// <summary>
    /// Asynchronously forwards the message if it is assignable to <typeparamref name="T"/>; otherwise returns a completed task.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PostAsync<M>(M message)
    {
        if (message is T typedMessage) return sourceHelper.ForwardAsync(typedMessage);
        else return Task.CompletedTask;
    }
}