using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Provides a flexible endpoint that consumes messages of type <typeparamref name="T"/> and executes
/// a configured synchronous or asynchronous delegate, optionally forwarding rejected messages to an
/// alternative source.
/// </summary>
/// <typeparam name="T">The message type accepted by this endpoint.</typeparam>
public sealed class ProcessingEndpoint<T> : IMessageSink<T>, IAlternativeMessageSource
{
    private readonly object action;
    private readonly ActionType actionType;
    private LazyValue<SourceHelper> alternativeSendingHelper;

    /// <summary>
    /// Classifies the processing delegate to distinguish between synchronous/asynchronous
    /// execution and whether the delegate can reject a message.
    /// </summary>
    enum ActionType
    {
        Sync,
        Async,
        SyncChecked,
        AsyncChecked
    }

    /// <summary>
    /// Gets the message type consumed by this endpoint.
    /// </summary>
    public Type ConsumedMessageType => typeof(T);

    /// <summary>
    /// Initializes a new instance that executes a synchronous delegate capable of rejecting messages.
    /// </summary>
    /// <param name="processing">The delegate that processes the message and returns true when handled.</param>
    public ProcessingEndpoint(Func<T, bool> processing)
    {
        this.action = processing;
        this.actionType = ActionType.SyncChecked;
    }

    /// <summary>
    /// Initializes a new instance that executes a synchronous delegate without rejection capability.
    /// </summary>
    /// <param name="processing">The delegate that processes the message.</param>
    public ProcessingEndpoint(Action<T> processing)
    {
        this.action = processing;
        this.actionType = ActionType.Sync;
    }

    /// <summary>
    /// Initializes a new instance that executes an asynchronous delegate capable of rejecting messages.
    /// </summary>
    /// <param name="processingAsync">The asynchronous delegate returning true when the message is handled.</param>
    /// <remarks>
    /// When the message is delivered via the synchronous <see cref="Post{M}(M)"/>, the delegate is invoked
    /// with the current <see cref="SynchronizationContext"/> suspended and is then awaited blocking.
    /// This is required to avoid a deadlock, but it means the delegate's continuations do NOT resume on the
    /// posting thread's context: they run on thread pool threads. Do not touch context-affine state
    /// (e.g. UI controls) after an await inside the delegate unless you marshal back explicitly.
    /// The posting thread's own context is restored again as soon as <see cref="Post{M}(M)"/> returns.
    /// Use <see cref="PostAsync{M}(M)"/> if the context must be preserved.
    /// </remarks>
    public ProcessingEndpoint(Func<T, Task<bool>> processingAsync)
    {
        this.action = processingAsync;
        this.actionType = ActionType.AsyncChecked;
    }

    /// <summary>
    /// Initializes a new instance that executes an asynchronous delegate without rejection capability.
    /// </summary>
    /// <param name="processingAsync">The asynchronous delegate that processes the message.</param>
    /// <remarks>
    /// When the message is delivered via the synchronous <see cref="Post{M}(M)"/>, the delegate is invoked
    /// with the current <see cref="SynchronizationContext"/> suspended and is then awaited blocking.
    /// This is required to avoid a deadlock, but it means the delegate's continuations do NOT resume on the
    /// posting thread's context: they run on thread pool threads. Do not touch context-affine state
    /// (e.g. UI controls) after an await inside the delegate unless you marshal back explicitly.
    /// The posting thread's own context is restored again as soon as <see cref="Post{M}(M)"/> returns.
    /// Use <see cref="PostAsync{M}(M)"/> if the context must be preserved.
    /// </remarks>
    public ProcessingEndpoint(Func<T, Task> processingAsync)
    {
        this.action = processingAsync;
        this.actionType = ActionType.Async;
    }

    /// <summary>
    /// Gets the alternative message source used when a message is rejected or of an incompatible type.
    /// </summary>
    public IMessageSource Else => alternativeSendingHelper.Obj;

    /// <summary>
    /// Posts a message by reference for immediate processing, forwarding to the alternative source when necessary.
    /// </summary>
    /// <typeparam name="M">The actual message type supplied by the caller.</typeparam>
    /// <param name="message">The message instance to process.</param>
    public void Post<M>(in M message)
    {
        if (message is T msgT)
        {
            switch (actionType)
            {
                case ActionType.Sync:
                    ((Action<T>)action)(msgT);
                    break;
                case ActionType.Async:
                    // The context must be suspended around the INVOCATION, not around the blocking
                    // wait: the async action captures SynchronizationContext.Current when it reaches
                    // its first await. Without this, posting from a UI/single-threaded context and
                    // blocking here would deadlock.
                    using (SynchronizationContext.Current.Suspend()) ((Func<T, Task>)action)(msgT).WaitFor();
                    break;
                case ActionType.SyncChecked:
                    if (!((Func<T, bool>)action)(msgT))
                    {
                        alternativeSendingHelper.ObjIfExists?.Forward(in message);
                    }
                    break;
                case ActionType.AsyncChecked:
                    bool checkedResult;
                    using (SynchronizationContext.Current.Suspend()) checkedResult = ((Func<T, Task<bool>>)action)(msgT).WaitFor();
                    if (!checkedResult)
                    {
                        alternativeSendingHelper.ObjIfExists?.Forward(in message);
                    }
                    break;
            }
        }
        else alternativeSendingHelper.ObjIfExists?.Forward(in message);
    }

    /// <summary>
    /// Posts a message by value for immediate processing, forwarding to the alternative source when necessary.
    /// </summary>
    /// <typeparam name="M">The actual message type supplied by the caller.</typeparam>
    /// <param name="message">The message instance to process.</param>
    public void Post<M>(M message)
    {
        if (message is T msgT)
        {
            switch (actionType)
            {
                case ActionType.Sync:
                    ((Action<T>)action)(msgT);
                    break;
                case ActionType.Async:
                    // See Post<M>(in M): suspending around the invocation avoids a sync-over-async
                    // deadlock when posting from a context-bound thread.
                    using (SynchronizationContext.Current.Suspend()) ((Func<T, Task>)action)(msgT).WaitFor();
                    break;
                case ActionType.SyncChecked:
                    if (!((Func<T, bool>)action)(msgT))
                    {
                        alternativeSendingHelper.ObjIfExists?.Forward(message);
                    }
                    break;
                case ActionType.AsyncChecked:
                    bool checkedResult;
                    using (SynchronizationContext.Current.Suspend()) checkedResult = ((Func<T, Task<bool>>)action)(msgT).WaitFor();
                    if (!checkedResult)
                    {
                        alternativeSendingHelper.ObjIfExists?.Forward(message);
                    }
                    break;
            }
        }
        else alternativeSendingHelper.ObjIfExists?.Forward(message);
    }

    /// <summary>
    /// Posts a message asynchronously and optionally leverages the alternative source when the handler rejects it.
    /// </summary>
    /// <typeparam name="M">The actual message type supplied by the caller.</typeparam>
    /// <param name="message">The message instance to process.</param>
    /// <returns>A task that completes when processing (and optional forwarding) finish.</returns>
    public Task PostAsync<M>(M message)
    {

        if (message is T msgT)
        {
            if (alternativeSendingHelper.Exists &&
                !alternativeSendingHelper.Obj.NoConnectedSinks &&
                actionType == ActionType.AsyncChecked)
            {
                return ProcessWithAlternativeAsync(msgT);
            }

            switch (actionType)
            {
                case ActionType.Sync:
                    ((Action<T>)action)(msgT);
                    return Task.CompletedTask;
                case ActionType.Async:
                    return ((Func<T, Task>)action)(msgT);
                case ActionType.SyncChecked:
                    if (!((Func<T, bool>)action)(msgT))
                    {
                        return alternativeSendingHelper.ObjIfExists?.ForwardAsync(message) ?? Task.CompletedTask;
                    }
                    return Task.CompletedTask;
                case ActionType.AsyncChecked:
                    return ((Func<T, Task<bool>>)action)(msgT);
            }
        }
        return alternativeSendingHelper.ObjIfExists?.ForwardAsync(message) ?? Task.CompletedTask;
    }

    /// <summary>
    /// Processes an asynchronously handled message and forwards it to the alternative source when rejected.
    /// </summary>
    /// <param name="message">The message instance to process.</param>
    /// <returns>A task that completes when processing and any forwarding finish.</returns>
    private async Task ProcessWithAlternativeAsync(T message)
    {
        if (!await ((Func<T, Task<bool>>)action)(message) && alternativeSendingHelper.Exists)
        {
            await alternativeSendingHelper.Obj.ForwardAsync(message);
        }
    }
}