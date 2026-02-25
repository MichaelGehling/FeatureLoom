using FeatureLoom.Helpers;
using FeatureLoom.Scheduling;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Threading;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Lightweight, delegate-driven aggregator that receives messages of type <typeparamref name="T"/>, invokes a user-provided handler,
/// and allows emitting one or more output messages via an <see cref="ISender"/>.
/// </summary>
/// <typeparam name="T">The input message type.</typeparam>
/// <remarks>
/// Purpose
/// - Provides a minimal surface to implement aggregation/coalescing logic without creating a custom type.
/// - Use <c>onMessage</c> to collect state and emit messages via the provided <see cref="ISender"/>.
/// - Optionally use <c>onTimeout</c> to flush or emit on a time signal.
///
/// Timeout behavior
/// - If a timeout handler is provided and <c>timeout &gt; 0</c>, a timer is scheduled using <see cref="SchedulerService"/>.
/// - When <c>resetTimeoutOnMessage == true</c>, the timeout is inactivity-based (each message resets the window).
/// - When <c>resetTimeoutOnMessage == false</c>, the timeout fires in fixed periods (no reset on messages).
/// - Scheduling uses weak references; schedules are cleaned up automatically when no longer strongly referenced.
///
/// Concurrency and reentrancy
/// - When <c>autoLock == true</c>, handlers are executed inside a <see cref="FeatureLock"/> to simplify thread-safety.
/// - Synchronous sends (<see cref="AggregationSender.Send{M}(in M)"/> / <see cref="AggregationSender.Send{M}(M)"/>) set a same-thread reentrancy guard.
///   If a downstream sink synchronously posts back into this aggregator on the same thread while the lock is held, an exception is thrown to avoid deadlocks.
/// - Asynchronous sends (<see cref="AggregationSender.SendAsync{M}(M)"/>) return a task and intentionally release the reentrancy guard immediately
///   so the handler can decide whether to await or fire-and-forget (see <see cref="AggregationSender.SendAsync{M}(M)"/> remarks).
///
/// Disposal
/// - Calling <see cref="Dispose"/> clears the strong reference to the schedule and disconnects all sinks.
///   The scheduler uses weak references and will stop invoking the callback once the schedule becomes unreachable and collected.
/// </remarks>
public sealed class Aggregator<T> : IMessageSink<T>, IMessageSource, IDisposable, IMessageFlowConnection
{
    readonly TimeSpan timeout;
    readonly Action<T, ISender> onMessage;
    readonly Func<T, ISender, Task> onMessageAsync;
    readonly AggregationSender sender;
    readonly FeatureLock actionLock;
    DateTime nextTimeout;
    readonly bool resetTimeoutOnMessage;
    ActionSchedule schedule;

    /// <summary>
    /// Creates a new aggregator with a synchronous message handler and an optional synchronous timeout handler.
    /// </summary>
    /// <param name="onMessage">
    /// Handler invoked for each input message. Use the provided <see cref="ISender"/> to emit zero or more output messages
    /// (e.g., partial batches, coalesced events, or flush events).
    /// </param>
    /// <param name="onTimeout">
    /// Optional timeout handler invoked when the timeout elapses.
    /// </param>
    /// <param name="timeout">
    /// Timeout duration. Must be greater than zero to enable <paramref name="onTimeout"/>.
    /// </param>
    /// <param name="resetTimeoutOnMessage">
    /// When true, the timeout is inactivity-based (reset on each received message). When false, the timeout fires in fixed periods.
    /// </param>
    /// <param name="autoLock">
    /// When true, <paramref name="onMessage"/> and <paramref name="onTimeout"/> are executed within a <see cref="FeatureLock"/>.
    /// This simplifies thread-safety for stateful aggregations.
    /// </param>
    /// <remarks>
    /// The timeout schedule is parentless and kept only via weak references by <see cref="SchedulerService"/>.
    /// If this instance becomes unreachable or is disposed, the schedule will be tidied automatically.
    /// </remarks>
    public Aggregator(Action<T, ISender> onMessage, Action<ISender> onTimeout = null, TimeSpan timeout = default, bool resetTimeoutOnMessage = true, bool autoLock = true)
    {
        this.onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
        this.timeout = timeout; // ensure inactivity reset uses the configured timeout
        if (autoLock) actionLock = new FeatureLock();
        sender = new AggregationSender(autoLock);
        this.resetTimeoutOnMessage = false;

        this.onMessageAsync = (msg, s) =>
        {
            onMessage(msg, s);
            return Task.CompletedTask;
        };

        if (onTimeout != null && timeout > TimeSpan.Zero)
        {
            this.resetTimeoutOnMessage = resetTimeoutOnMessage;
            nextTimeout = AppTime.Now + timeout;
            schedule = Service<SchedulerService>.Instance.ScheduleAction("Aggregator Timeout", (now) =>
            {
                if (now < nextTimeout) return ScheduleStatus.WaitUntil(nextTimeout);
                nextTimeout = now + timeout;

                // Run timeout handler off the scheduler thread.
                Task.Run(() =>
                {
                    using (actionLock?.Lock())
                    {
                        onTimeout(sender);
                    }
                });
                
                return ScheduleStatus.WaitUntil(nextTimeout);
            });
        }
    }

    /// <summary>
    /// Creates a new aggregator with an asynchronous message handler and an optional asynchronous timeout handler.
    /// </summary>
    /// <param name="onMessageAsync">
    /// Async handler invoked for each input message. Use the provided <see cref="ISender"/> to emit zero or more messages.
    /// </param>
    /// <param name="onTimeoutAsync">
    /// Optional async timeout handler invoked when the timeout elapses.
    /// </param>
    /// <param name="timeout">
    /// Timeout duration. Must be greater than zero to enable <paramref name="onTimeoutAsync"/>.
    /// </param>
    /// <param name="resetTimeoutOnMessage">
    /// When true, the timeout is inactivity-based (reset on each received message). When false, the timeout fires in fixed periods.
    /// </param>
    /// <param name="autoLock">
    /// When true, <paramref name="onMessageAsync"/> and <paramref name="onTimeoutAsync"/> are executed within a <see cref="FeatureLock"/>.
    /// </param>
    /// <remarks>
    /// The timeout schedule is parentless and kept only via weak references by <see cref="SchedulerService"/>.
    /// If this instance becomes unreachable or is disposed, the schedule will be tidied automatically.
    /// </remarks>
    public Aggregator(Func<T, ISender, Task> onMessageAsync, Func<ISender, Task> onTimeoutAsync = null, TimeSpan timeout = default, bool resetTimeoutOnMessage = true, bool autoLock = true)
    {
        this.onMessageAsync = onMessageAsync ?? throw new ArgumentNullException(nameof(onMessageAsync));
        this.timeout = timeout;
        if (autoLock) actionLock = new FeatureLock();
        sender = new AggregationSender(autoLock);
        this.resetTimeoutOnMessage = false;

        this.onMessage = (msg, s) => onMessageAsync(msg, s).WaitFor();

        if (onTimeoutAsync != null && timeout > TimeSpan.Zero)
        {
            this.resetTimeoutOnMessage = resetTimeoutOnMessage;
            nextTimeout = AppTime.Now + timeout;
            schedule = Service<SchedulerService>.Instance.ScheduleAction("Aggregator Timeout", (now) =>
            {
                if (now < nextTimeout) return ScheduleStatus.WaitUntil(nextTimeout);

                // Run timeout handler off the scheduler thread.
                Task.Run(async () =>
                {
                    using (actionLock?.Lock())
                    {
                        await onTimeoutAsync(sender).ConfiguredAwait();
                    }
                });

                nextTimeout = now + timeout;
                return ScheduleStatus.WaitUntil(nextTimeout);
            });
        }
    }

    /// <summary>Gets the consumed message type (<see cref="Type"/> of <typeparamref name="T"/>).</summary>
    public Type ConsumedMessageType => typeof(T);

    /// <summary>Number of currently connected sinks (excluding already collected weak refs).</summary>
    public int CountConnectedSinks => sender.CountConnectedSinks;

    /// <summary> Indicates whether there are no connected sinks. </summary>
    public bool NoConnectedSinks => sender.NoConnectedSinks;

    /// <summary>
    /// Connects this source to a sink. When <paramref name="weakReference"/> is true, the sink is held weakly (GC can collect it).
    /// </summary>
    public void ConnectTo(IMessageSink sink, bool weakReference = false)
    {
        sender.ConnectTo(sink, weakReference);
    }

    /// <summary>
    /// Connects this source to a bidirectional element (sink+source),
    /// returning the same element typed as a source to enable fluent chaining.
    /// </summary>
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
    {
        return sender.ConnectTo(sink, weakReference);
    }

    /// <summary>Disconnects all sinks from this source.</summary>
    public void DisconnectAll()
    {
        sender.DisconnectAll();
    }

    /// <summary>Disconnects the specified sink if connected.</summary>
    public void DisconnectFrom(IMessageSink sink)
    {
        sender.DisconnectFrom(sink);
    }

    /// <summary>
    /// Disposes this aggregator.
    /// </summary>
    /// <remarks>
    /// - Clears the strong reference to the schedule; since <see cref="SchedulerService"/> uses weak references,
    ///   the schedule will stop once collected.
    /// - Disconnects all sinks.
    /// </remarks>
    public void Dispose()
    {
        schedule = null;
        sender.DisconnectAll();
    }

    /// <summary>
    /// Returns the connected sinks. Invalid weak references are pruned lazily.
    /// </summary>
    public IMessageSink[] GetConnectedSinks()
    {
        return sender.GetConnectedSinks();
    }

    /// <summary>
    /// Checks whether the provided sink is currently connected and alive.
    /// </summary>
    public bool IsConnected(IMessageSink sink)
    {
        return sender.IsConnected(sink);
    }

    /// <summary>
    /// Processes a typed message by invoking the handler under the optional lock
    /// and updating the inactivity timeout when enabled.
    /// </summary>
    private void HandleMessage(T message)
    {
        if (sender.DeadLockDetected) throw new InvalidOperationException("Reentrant message sending detected in Aggregator. This is not allowed to avoid deadlocks.");
        using (actionLock?.Lock())
        {
            onMessage(message, sender);
            if (resetTimeoutOnMessage) nextTimeout = AppTime.Now + timeout;
        }
    }

    /// <summary>
    /// Async variant that invokes the async handler under the optional lock and updates the inactivity timeout when enabled.
    /// </summary>
    private async Task HandleMessageAsync(T message)
    {
        if (sender.DeadLockDetected) throw new InvalidOperationException("Reentrant message sending detected in Aggregator. This is not allowed to avoid deadlocks.");
        if (actionLock == null)
        {
            await onMessageAsync(message, sender).ConfiguredAwait();
            if (resetTimeoutOnMessage) nextTimeout = AppTime.Now + timeout;
            return;
        }
        else
        {
            using (await actionLock.LockAsync().ConfiguredAwait())
            {
                await onMessageAsync(message, sender).ConfiguredAwait();
                if (resetTimeoutOnMessage) nextTimeout = AppTime.Now + timeout;
                return;
            }
        }
    }

    /// <summary>Posts a message by reference. If it is of type <typeparamref name="T"/>, it is handled; otherwise ignored.</summary>
    public void Post<M>(in M message)
    {
        if (message is not T typedMessage) return;
        HandleMessage(typedMessage);
    }

    /// <summary>Posts a message by value. If it is of type <typeparamref name="T"/>, it is handled; otherwise ignored.</summary>
    public void Post<M>(M message)
    {
        if (message is not T typedMessage) return;
        HandleMessage(typedMessage);
    }

    /// <summary>
    /// Posts a message asynchronously. If it is of type <typeparamref name="T"/>, it is handled and a task is returned.
    /// </summary>
    /// <remarks>
    /// - With the synchronous constructor, the returned task completes immediately.
    /// - With the asynchronous constructor, the returned task completes when the async handler has finished.
    /// - For forwarding within handlers, <see cref="AggregationSender.SendAsync{M}(M)"/> allows caller-controlled awaiting semantics.
    /// </remarks>
    public Task PostAsync<M>(M message)
    {
        if (message is not T typedMessage) return Task.CompletedTask;
        return HandleMessageAsync(typedMessage);
    }

    /// <summary>
    /// Internal sender used by the aggregator handlers to forward messages to connected sinks.
    /// </summary>
    /// <remarks>
    /// Reentrancy and deadlock behavior
    /// - When the aggregator is configured with <c>autoLock == true</c>, synchronous sends
    ///   (<see cref="Send{M}(in M)"/> / <see cref="Send{M}(M)"/>) install a same-thread reentrancy guard.
    ///   If a downstream sink synchronously posts back into the same aggregator on the same thread while the lock is held,
    ///   an exception is thrown in the aggregator to prevent deadlocks.
    /// - <see cref="SendAsync{M}(M)"/> intentionally clears this guard immediately after returning the task, so the caller
    ///   can decide whether to await inside the handler or fire-and-forget.
    ///   Be careful: awaiting inside the lock can deadlock if downstream synchronously calls back into the same lock.
    /// </remarks>
    class AggregationSender : ISender, IMessageSource
    {
        SourceHelper sourceHelper = new SourceHelper();
        bool locking = false;

        Thread sendingThread = null;

        /// <summary>Creates a new sender.</summary>
        /// <param name="locking">True when the parent aggregator executes handlers under a lock (used for reentrancy detection).</param>
        public AggregationSender(bool locking)
        {
            this.locking = locking;
        }

        /// <summary>
        /// True when a synchronous reentrant send on the same thread would deadlock the caller.
        /// Detection is limited to same-thread recursion while the aggregator lock is in use.
        /// </summary>
        public bool DeadLockDetected => sendingThread != null && Thread.CurrentThread == sendingThread;

        /// <summary>Number of currently connected sinks (excluding already collected weak refs).</summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary> Indicates whether there are no connected sinks. </summary>
        public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

        /// <inheritdoc/>
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <inheritdoc/>
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <inheritdoc/>
        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        /// <inheritdoc/>
        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        /// <inheritdoc/>
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        /// <inheritdoc/>
        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }

        /// <summary>
        /// Sends a message by reference to connected sinks.
        /// Sets a same-thread reentrancy guard while forwarding to prevent deadlocks when the aggregator lock is held.
        /// </summary>
        public void Send<M>(in M message)
        {
            sendingThread = locking ? Thread.CurrentThread : null;
            try
            {
                sourceHelper.Forward(in message);
            }
            finally
            {
                sendingThread = null;
            }
        }

        /// <summary>
        /// Sends a message by value to connected sinks.
        /// Sets a same-thread reentrancy guard while forwarding to prevent deadlocks when the aggregator lock is held.
        /// </summary>
        public void Send<M>(M message)
        {
            sendingThread = locking ? Thread.CurrentThread : null;
            try
            {
                sourceHelper.Forward(message);
            }
            finally
            {
                sendingThread = null;
            }
        }

        /// <summary>
        /// Sends a message asynchronously to connected sinks and returns the downstream task.
        /// </summary>
        /// <remarks>
        /// Reentrancy/awaiting semantics:
        /// - The reentrancy guard is intentionally cleared immediately after returning the task.
        /// - If you do not await the returned task inside the handler (fire-and-forget), ensure downstream does not synchronously
        ///   call back into the same aggregator while the handler still holds its lock.
        /// - If you await inside the handler while holding a lock, and downstream calls back requiring the same lock,
        ///   a deadlock may occur. Consider awaiting after leaving the lock or disable autoLock and coordinate concurrency yourself.
        /// </remarks>
        public Task SendAsync<M>(M message)
        {
            sendingThread = locking ? Thread.CurrentThread : null;
            try
            {
                return sourceHelper.ForwardAsync(message);
            }
            finally
            {
                // Guard intentionally released to let the caller decide whether to wait.
                sendingThread = null;
            }
        }
    }
}