using FeatureLoom.Logging;
using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Forwards incoming messages to connected sinks on the <see cref="SynchronizationContext"/> captured at construction time.
/// </summary>
/// <remarks>
/// <para> 
/// Context affinity:
/// The internal dispatch loop starts on the constructing thread and awaits using <c>ConfiguredAwait(true)</c>, so continuations
/// resume on the captured <see cref="SynchronizationContext"/> (e.g., UI thread). This allows posting from background threads
/// while processing on the UI thread or any other captured context.
/// </para>
/// <para>
/// Ordering and throughput:
/// Messages are queued and processed in FIFO order. Per-send sink order is preserved (0..N-1) for both synchronous and asynchronous sinks.
/// If no sinks are connected, posts return immediately and the loop clears any queued items to avoid unnecessary work.
/// </para>
/// <para>
/// Cancellation:
/// Cancel cancels the loop and returns the running task so callers can decide whether to await it or not.
/// The underlying <see cref="CancellationTokenSource"/> is disposed in Cancel. Tokens remain functional after disposal,
/// and the loop does not register again after cancellation.
/// </para>
/// <para>
/// Restart:
/// Restart awaits the completion of the previous loop on the current context and then starts a new loop on the current
/// context. A lightweight lock prevents concurrent restarts.
/// </para>
/// <para>
/// Type handling:
/// Structs and basic types will be boxed when using this non-generic version.
/// </para>
/// </remarks>
public sealed class CurrentContextForwarder : CurrentContextForwarder<object>
{
}

/// <summary>
/// Forwards incoming messages to connected sinks on the <see cref="SynchronizationContext"/> captured at construction time.
/// </summary>
/// <typeparam name="T">The type of messages forwarded by this instance.</typeparam>
/// <remarks>
/// <para>
/// Context affinity:
/// The internal dispatch loop starts on the constructing thread and awaits using <c>ConfiguredAwait(true)</c>, so continuations
/// resume on the captured <see cref="SynchronizationContext"/> (e.g., UI thread). This allows posting from background threads
/// while processing on the UI thread or any other captured context.
/// </para>
/// <para>
/// Ordering and throughput:
/// Messages are queued and processed in FIFO order. Per-send sink order is preserved (0..N-1) for both synchronous and asynchronous sinks.
/// If no sinks are connected, posts return immediately and the loop clears any queued items to avoid unnecessary work.
/// </para>
/// <para>
/// Cancellation:
/// <see cref="Cancel"/> cancels the loop and returns the running task so callers can decide whether to await it or not.
/// The underlying <see cref="CancellationTokenSource"/> is disposed in <see cref="Cancel"/>. Tokens remain functional after disposal,
/// and the loop does not register again after cancellation.
/// </para>
/// <para>
/// Restart:
/// <see cref="Restart"/> awaits the completion of the previous loop on the current context and then starts a new loop on the current
/// context. A lightweight lock prevents concurrent restarts.
/// </para>
/// <para>
/// Type handling:
/// Only messages assignable to <typeparamref name="T"/> are forwarded; other messages are ignored.
/// </para>
/// </remarks>
public class CurrentContextForwarder<T> : IMessageFlowConnection<T>, IDisposable
{        
    readonly struct ForwardingMessage
    {
        public readonly T message;
        public readonly ForwardingMethod forwardingMethod;

        public ForwardingMessage(T message, ForwardingMethod forwardingMethod)
        {
            this.message = message;
            this.forwardingMethod = forwardingMethod;
        }
    }

    private TypedSourceValueHelper<T> sourceHelper = new TypedSourceValueHelper<T>();
    private readonly QueueReceiver<ForwardingMessage> receiver;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private Task forwardingTask;
    private MicroLock restartLock = new MicroLock();

    /// <summary>
    /// Gets the type of messages sent by this source.
    /// </summary>
    public Type SentMessageType => typeof(T);

    /// <summary>
    /// Gets the message type that this element consumes.
    /// </summary>
    public Type ConsumedMessageType => typeof(T);

    /// <summary> Indicates whether there are no connected sinks. </summary>
    public bool NoConnectedSinks => sourceHelper.NotConnected;

    /// <summary>
    /// Initializes a new instance and captures the current <see cref="SynchronizationContext"/> for forwarding.
    /// </summary>
    public CurrentContextForwarder()
    {
        receiver = new QueueReceiver<ForwardingMessage>();
        forwardingTask = RunAsync(cts.Token);
    }

    /// <summary>
    /// Gets a value indicating whether cancellation was requested, or this forwarder has already been canceled and its source disposed.
    /// </summary>
    public bool IsCancelled => cts == null || cts.IsCancellationRequested;

    /// <summary>
    /// Requests cancellation of the internal dispatch loop and disposes the current <see cref="CancellationTokenSource"/>.
    /// </summary>
    /// <returns>
    /// The running loop task. Callers may choose whether and how to await it (e.g., with <c>ConfiguredAwait(false)</c>).
    /// </returns>
    /// <remarks>
    /// The token remains observable after disposing the source. The dispatch loop does not re-register after cancellation.
    /// </remarks>
    public Task Cancel()
    {
        var tempCts = cts;
        cts = null;
        tempCts?.Cancel();
        tempCts?.Dispose();
        return forwardingTask;
    }

    /// <summary>
    /// Cancels the loop (if not already canceled) without awaiting completion.
    /// </summary>
    /// <remarks>
    /// This method does not block. To ensure the loop has fully terminated, await the task returned by <see cref="Cancel"/>.
    /// </remarks>
    public void Dispose()
    {
        _ = Cancel();               
    }

    /// <summary>
    /// Restarts the dispatch loop after it has been canceled, preserving the captured <see cref="SynchronizationContext"/>.
    /// </summary>
    /// <returns>A task that completes after the restart has been scheduled.</returns>
    /// <remarks>
    /// If the forwarder is not canceled, this method is a no-op. A small lock prevents concurrent restarts.
    /// The previous loop is awaited using <c>ConfiguredAwait(true)</c> to ensure the new loop starts on the same context.
    /// </remarks>
    public async Task Restart()
    {
        if (!IsCancelled) return;
        await forwardingTask.ConfiguredAwait(true);
        using (restartLock.Lock())
        {
            if (!IsCancelled) return;                
            cts = new CancellationTokenSource();
        }
        forwardingTask = RunAsync(cts.Token);
    }

    /// <summary>
    /// Gets the current number of messages in the internal queue.
    /// </summary>
    public int Count => receiver.Count;

    /// <summary>
    /// Gets the number of connected sinks (invalid weak references are pruned lazily).
    /// </summary>
    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

    /// <summary>
    /// Posts a message by reference. If the message is assignable to <typeparamref name="T"/>, it is forwarded; otherwise ignored.
    /// </summary>
    /// <typeparam name="M">Type of the posted message.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <remarks>
    /// When no sinks are connected, this method returns immediately without enqueuing.
    /// </remarks>
    public void Post<M>(in M message)
    {
        if (sourceHelper.NotConnected) return;
        if (message is T typedMessage)
        {
            ForwardingMessage forwardingMessage = new ForwardingMessage(typedMessage, ForwardingMethod.SynchronousByRef);
            receiver.Post(in forwardingMessage);
        }
    }

    /// <summary>
    /// Posts a message by value. If the message is assignable to <typeparamref name="T"/>, it is forwarded; otherwise ignored.
    /// </summary>
    /// <typeparam name="M">Type of the posted message.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <remarks>
    /// When no sinks are connected, this method returns immediately without enqueuing.
    /// </remarks>
    public void Post<M>(M message)
    {
        if (sourceHelper.NotConnected) return;
        if (message is T typedMessage)
        {
            ForwardingMessage forwardingMessage = new ForwardingMessage(typedMessage, ForwardingMethod.Synchronous);
            receiver.Post(forwardingMessage);
        }
    }

    /// <summary>
    /// Posts a message asynchronously. If the message is assignable to <typeparamref name="T"/>, it is forwarded; otherwise ignored.
    /// </summary>
    /// <typeparam name="M">Type of the posted message.</typeparam>
    /// <param name="message">The message to post.</param>
    /// <returns>Always returns a completed task.</returns>
    /// <remarks>
    /// Posting is non-blocking. When no sinks are connected, this method returns immediately without enqueuing.
    /// </remarks>
    public Task PostAsync<M>(M message)
    {
        if (sourceHelper.NotConnected) return Task.CompletedTask;
        if (message is T typedMessage)
        {
            ForwardingMessage forwardingMessage = new ForwardingMessage(typedMessage, ForwardingMethod.Asynchronous);
            receiver.Post(forwardingMessage);                
        }
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await receiver.WaitAsync(cancellationToken).ConfiguredAwait(true);
            if (cancellationToken.IsCancellationRequested) break;
            if (sourceHelper.NotConnected)
            {
                receiver.Clear();
                continue;
            }
            while (receiver.TryReceive(out ForwardingMessage forwardingMessage))
            {                    
                try
                {
                    if (forwardingMessage.forwardingMethod == ForwardingMethod.Synchronous) sourceHelper.Forward(forwardingMessage.message);
                    else if (forwardingMessage.forwardingMethod == ForwardingMethod.SynchronousByRef) sourceHelper.Forward(in forwardingMessage.message);
                    else if (forwardingMessage.forwardingMethod == ForwardingMethod.Asynchronous) await sourceHelper.ForwardAsync(forwardingMessage.message).ConfiguredAwait(true);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown during async forwarding.
                    return;
                }
                catch (Exception e)
                {
                    OptLog.ERROR()?.Build("Exception caught in CurrentContextForwarder while sending.", e);
                }
                if (cancellationToken.IsCancellationRequested) break;
            }
        }
    }

    /// <summary>
    /// Connects this forwarder to a sink.
    /// </summary>
    /// <param name="sink">The sink to connect.</param>
    /// <param name="weakReference">True to hold a weak reference so the sink can be GC-collected.</param>
    public void ConnectTo(IMessageSink sink, bool weakReference = false)
    {
        sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>
    /// Connects this forwarder to a bidirectional element and returns it typed as a source for fluent chaining.
    /// </summary>
    /// <param name="sink">The bidirectional element to connect.</param>
    /// <param name="weakReference">True to hold a weak reference so the sink can be GC-collected.</param>
    /// <returns>The connected element typed as a source.</returns>
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
    {
        return sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>
    /// Disconnects the specified sink if currently connected.
    /// </summary>
    /// <param name="sink">The sink to disconnect.</param>
    public void DisconnectFrom(IMessageSink sink)
    {
        sourceHelper.DisconnectFrom(sink);
    }

    /// <summary>
    /// Disconnects all currently connected sinks.
    /// </summary>
    public void DisconnectAll()
    {
        sourceHelper.DisconnectAll();
    }

    /// <summary>
    /// Returns the currently connected sinks. Invalid weak references are pruned lazily.
    /// </summary>
    public IMessageSink[] GetConnectedSinks()
    {
        return sourceHelper.GetConnectedSinks();
    }

    /// <summary>
    /// Checks whether the specified sink is connected and alive.
    /// </summary>
    /// <param name="sink">The sink to check.</param>
    /// <returns>True if connected; otherwise, false.</returns>
    public bool IsConnected(IMessageSink sink)
    {
        return sourceHelper.IsConnected(sink);
    }
}