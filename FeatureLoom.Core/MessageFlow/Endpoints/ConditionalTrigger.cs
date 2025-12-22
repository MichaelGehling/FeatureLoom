using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Triggers when a message of type T (optionally matching a predicate) is received.
/// Optionally resets when a message of type R (matching a predicate) is received.
/// If T and R are the same type, by default a single message can both trigger and reset;
/// you can disable that with allowSameMessageTriggerAndReset = false.
/// </summary>
/// <typeparam name="T">Type of messages that can trigger the internal <see cref="MessageTrigger"/>.</typeparam>
/// <typeparam name="R">Type of messages that can reset the internal <see cref="MessageTrigger"/>.</typeparam>
public sealed class ConditionalTrigger<T, R> : IMessageSink, IAsyncWaitHandle
{
    private readonly MessageTrigger internalTrigger = new MessageTrigger();
    private readonly Predicate<T> triggerCondition;
    private readonly Predicate<R> resetCondition;
    private readonly bool allowSameMessageTriggerAndReset;

    private IAsyncWaitHandle WaitHandle => internalTrigger;

    /// <summary>
    /// A task that completes when the trigger becomes signaled (or may complete due to external interruption).
    /// </summary>
    public Task WaitingTask => WaitHandle.WaitingTask;

    /// <summary>
    /// Creates a conditional trigger that signals on messages of type <typeparamref name="T"/> and optionally resets on messages of type <typeparamref name="R"/>.
    /// </summary>
    /// <param name="triggerCondition">Predicate evaluated for messages of type <typeparamref name="T"/>. If null, any <typeparamref name="T"/> triggers.</param>
    /// <param name="resetCondition">Optional predicate evaluated for messages of type <typeparamref name="R"/>. If provided and true, the trigger is reset.</param>
    /// <param name="allowSameMessageTriggerAndReset">
    /// If false and a single message matches both trigger and reset (only possible when <typeparamref name="T"/> equals <typeparamref name="R"/>),
    /// the operation is chosen based on current state: triggering is preferred if not already triggered; otherwise reset is preferred.
    /// </param>
    public ConditionalTrigger(
        Predicate<T> triggerCondition,
        Predicate<R> resetCondition = null,
        bool allowSameMessageTriggerAndReset = true)
    {
        this.triggerCondition = triggerCondition;
        this.resetCondition = resetCondition;
        this.allowSameMessageTriggerAndReset = allowSameMessageTriggerAndReset;
    }

    /// <summary>
    /// Gets whether the internal trigger is currently signaled.
    /// </summary>
    /// <param name="reset">If true and the trigger is signaled, resets it before returning.</param>
    /// <returns>True if signaled; otherwise false.</returns>
    public bool IsTriggered(bool reset = false) => internalTrigger.IsTriggered(reset);

    /// <summary>
    /// Posts a message by reference to be evaluated for triggering/resetting.
    /// </summary>
    /// <typeparam name="M">The message type.</typeparam>
    /// <param name="message">The message instance.</param>
    public void Post<M>(in M message) => HandleMessage(message);

    /// <summary>
    /// Posts a message by value to be evaluated for triggering/resetting.
    /// </summary>
    /// <typeparam name="M">The message type.</typeparam>
    /// <param name="message">The message instance.</param>
    public void Post<M>(M message) => HandleMessage(message);

    /// <summary>
    /// Asynchronously posts a message to be evaluated for triggering/resetting.
    /// This method does not block and returns a completed task.
    /// </summary>
    /// <typeparam name="M">The message type.</typeparam>
    /// <param name="message">The message instance.</param>
    /// <returns>A completed task.</returns>
    public Task PostAsync<M>(M message)
    {
        HandleMessage(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Evaluates a message against trigger and reset conditions and updates the internal state accordingly.
    /// </summary>
    private void HandleMessage<M>(M message)
    {
        bool willTrigger = false;
        bool willReset = false;

        if (message is T t && (triggerCondition?.Invoke(t) ?? true))
            willTrigger = true;

        if (resetCondition != null && message is R r && resetCondition(r))
            willReset = true;

        if (!allowSameMessageTriggerAndReset && willTrigger && willReset)
        {
            // Prefer the operation that changes state:
            // - If already triggered, prefer reset.
            // - If not triggered, prefer trigger.
            if (IsTriggered())
                willTrigger = false;
            else
                willReset = false;
        }

        if (willTrigger) internalTrigger.Trigger();
        if (willReset) internalTrigger.Reset();
    }

    // IAsyncWaitHandle forwarding

    /// <summary>
    /// Waits asynchronously until signaled.
    /// </summary>
    /// <returns>True if signaled, false in case of external interruption.</returns>
    public Task<bool> WaitAsync() => WaitHandle.WaitAsync();

    /// <summary>
    /// Waits asynchronously until signaled or the timeout elapses.
    /// </summary>
    /// <param name="timeout">Maximum wait duration.</param>
    /// <returns>True if signaled; false if timed out or interrupted.</returns>
    public Task<bool> WaitAsync(TimeSpan timeout) => WaitHandle.WaitAsync(timeout);

    /// <summary>
    /// Waits asynchronously until signaled or the operation is canceled.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <returns>True if signaled; false if canceled or interrupted.</returns>
    public Task<bool> WaitAsync(CancellationToken cancellationToken) => WaitHandle.WaitAsync(cancellationToken);

    /// <summary>
    /// Waits asynchronously until signaled, the timeout elapses, or the operation is canceled.
    /// </summary>
    /// <param name="timeout">Maximum wait duration.</param>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <returns>True if signaled; false if timed out, canceled, or interrupted.</returns>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => WaitHandle.WaitAsync(timeout, cancellationToken);

    /// <summary>
    /// Waits synchronously until signaled.
    /// </summary>
    /// <returns>True if signaled; false in case of external interruption.</returns>
    public bool Wait() => WaitHandle.Wait();

    /// <summary>
    /// Waits synchronously until signaled or the timeout elapses.
    /// </summary>
    /// <param name="timeout">Maximum wait duration.</param>
    /// <returns>True if signaled; false if timed out or interrupted.</returns>
    public bool Wait(TimeSpan timeout) => WaitHandle.Wait(timeout);

    /// <summary>
    /// Waits synchronously until signaled or the operation is canceled.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <returns>True if signaled; false if canceled or interrupted.</returns>
    public bool Wait(CancellationToken cancellationToken) => WaitHandle.Wait(cancellationToken);

    /// <summary>
    /// Waits synchronously until signaled, the timeout elapses, or the operation is canceled.
    /// </summary>
    /// <param name="timeout">Maximum wait duration.</param>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <returns>True if signaled; false if timed out, canceled, or interrupted.</returns>
    public bool Wait(TimeSpan timeout, CancellationToken cancellationToken) => WaitHandle.Wait(timeout, cancellationToken);

    /// <summary>
    /// Indicates whether a caller would currently block if a wait method was called.
    /// </summary>
    /// <returns>False if already signaled; true otherwise.</returns>
    public bool WouldWait() => WaitHandle.WouldWait();

    /// <summary>
    /// Tries to convert to a classic <see cref="WaitHandle"/> backed by the internal wait handle.
    /// </summary>
    /// <param name="waitHandle">The resulting <see cref="WaitHandle"/> when successful.</param>
    /// <returns>True if conversion is supported and successful; otherwise false.</returns>
    public bool TryConvertToWaitHandle(out WaitHandle waitHandle) => WaitHandle.TryConvertToWaitHandle(out waitHandle);
}