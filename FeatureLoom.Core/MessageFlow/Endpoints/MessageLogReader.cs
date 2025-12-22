using FeatureLoom.Collections;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Reads messages from an <see cref="IReadLogBuffer{T}"/> like the <see cref="cref="MessageLog{T}"/>
/// in ascending log-id order and forwards them to connected sinks according to the configured <see cref="ForwardingMethod"/>.
/// </summary>
/// <typeparam name="T">The message type.</typeparam>
public sealed class MessageLogReader<T> : IMessageSource<T>
{
    /// <summary>
    /// Helper that implements typed source forwarding and sink management.
    /// </summary>
    TypedSourceHelper<T> sourceHelper = new TypedSourceHelper<T>();

    /// <summary>
    /// The message log buffer to read from.
    /// </summary>
    IReadLogBuffer<T> messageSource;

    /// <summary>
    /// The next log id to attempt reading and forwarding.
    /// </summary>
    long NextMessageId { get; set; } = 0;

    /// <summary>
    /// Cancellation token used to stop the reader loop.
    /// </summary>
    CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <summary>
    /// The task representing the running reader loop. Completed when stopped or canceled.
    /// </summary>
    Task executionTask = Task.CompletedTask;

    /// <summary>
    /// Controls how messages are forwarded to connected sinks.
    /// </summary>
    ForwardingMethod ForwardingMethod { get; set; }

    /// <summary>
    /// Indicates whether there are no connected sinks.
    /// </summary>
    public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

    /// <summary>
    /// Creates a new <see cref="MessageLogReader{T}"/> bound to the specified log buffer.
    /// </summary>
    /// <param name="messageSource">The readable log buffer that provides messages.</param>
    /// <param name="forwardingMethod">The forwarding strategy (synchronous, by-ref, or asynchronous).</param>
    public MessageLogReader(IReadLogBuffer<T> messageSource, ForwardingMethod forwardingMethod = ForwardingMethod.Synchronous)
    {
        this.messageSource = messageSource;
        ForwardingMethod = forwardingMethod;
    }

    /// <summary>
    /// Gets the task representing the current execution of the reader loop.
    /// </summary>
    public Task ExecutionTask => executionTask;

    /// <summary>
    /// Starts the reader loop. If already running, throws an exception.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the reader.</param>
    /// <returns>The started execution task.</returns>
    /// <exception cref="Exception">Thrown if the reader is already running.</exception>
    /// <remarks>
    /// On start, aligns <see cref="NextMessageId"/> to <see cref="IReadLogBuffer{T}.OldestAvailableId"/>
    /// to avoid immediate missed-message warnings when starting late.
    /// </remarks>
    public Task Run(CancellationToken ct)
    {
        if (!executionTask.IsCompleted) throw new Exception("MessageLogReader is already running!");
        CancellationToken = ct;

        // Align to current oldest ID at start to avoid immediate missed-message warnings when starting late.
        if (NextMessageId < messageSource.OldestAvailableId) NextMessageId = messageSource.OldestAvailableId;

        executionTask = Run();
        return executionTask;
    }

    /// <summary>
    /// Reader loop:
    /// - Skips over expired ids and logs warnings.
    /// - Reads available messages by <see cref="NextMessageId"/> and forwards them.
    /// - Waits for the next id when no message is available.
    /// </summary>
    /// <returns>A task that completes when cancellation is requested.</returns>
    private async Task Run()
    {
        while (!CancellationToken.IsCancellationRequested)
        {
            while (!CancellationToken.IsCancellationRequested && NextMessageId < messageSource.OldestAvailableId)
            {
                var oldest = messageSource.OldestAvailableId;
                OptLog.WARNING()?.Build($"Missed messages with log Ids {NextMessageId.ToString()} - {(oldest - 1).ToString()}");
                NextMessageId = oldest;
            }

            while (!CancellationToken.IsCancellationRequested && messageSource.TryGetFromId(NextMessageId, out T message))
            {
                if (ForwardingMethod == ForwardingMethod.Synchronous) sourceHelper.Forward(message);
                if (ForwardingMethod == ForwardingMethod.SynchronousByRef) sourceHelper.Forward(in message);
                if (ForwardingMethod == ForwardingMethod.Asynchronous) await sourceHelper.ForwardAsync(message).ConfiguredAwait();

                // Advance to next message ID after successful retrieval/forwarding.
                NextMessageId++;
            }

            await messageSource.WaitForIdAsync(NextMessageId, CancellationToken).ConfiguredAwait();
        }
    }

    /// <summary>
    /// Gets the type of messages sent by this source.
    /// </summary>
    public Type SentMessageType => ((ITypedMessageSource)sourceHelper).SentMessageType;

    /// <summary>
    /// Gets the number of connected sinks.
    /// </summary>
    public int CountConnectedSinks => ((IMessageSource)sourceHelper).CountConnectedSinks;

    /// <summary>
    /// Connects a sink to receive forwarded messages.
    /// </summary>
    /// <param name="sink">The sink to connect.</param>
    /// <param name="weakReference">Whether to keep a weak reference to the sink.</param>
    public void ConnectTo(IMessageSink sink, bool weakReference = false)
    {
        ((IMessageSource)sourceHelper).ConnectTo(sink, weakReference);
    }

    /// <summary>
    /// Connects a message flow connection and returns the source.
    /// </summary>
    /// <param name="sink">The sink or connection endpoint.</param>
    /// <param name="weakReference">Whether to keep a weak reference to the sink.</param>
    /// <returns>The source instance to allow fluent chaining.</returns>
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
    {
        return ((IMessageSource)sourceHelper).ConnectTo(sink, weakReference);
    }

    /// <summary>
    /// Disconnects all sinks from this source.
    /// </summary>
    public void DisconnectAll()
    {
        ((IMessageSource)sourceHelper).DisconnectAll();
    }

    /// <summary>
    /// Disconnects the specified sink.
    /// </summary>
    /// <param name="sink">The sink to disconnect.</param>
    public void DisconnectFrom(IMessageSink sink)
    {
        ((IMessageSource)sourceHelper).DisconnectFrom(sink);
    }

    /// <summary>
    /// Returns a snapshot of currently connected sinks.
    /// </summary>
    /// <returns>An array of connected sinks.</returns>
    public IMessageSink[] GetConnectedSinks()
    {
        return ((IMessageSource)sourceHelper).GetConnectedSinks();
    }

    /// <summary>
    /// Checks whether the specified sink is connected.
    /// </summary>
    /// <param name="sink">The sink to check.</param>
    /// <returns>True if connected; otherwise false.</returns>
    public bool IsConnected(IMessageSink sink)
    {
        return ((IMessageSource)sourceHelper).IsConnected(sink);
    }
}
