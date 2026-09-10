using FeatureLoom.DependencyInversion;
using FeatureLoom.MessageFlow;
using FeatureLoom.Scheduling;
using FeatureLoom.Time;
using System;

namespace FeatureLoom.Synchronization;

/// <summary>
/// Describes a stall detected by a <see cref="SingleThreadSynchronizationContextWatchdog"/>.
/// </summary>
public readonly struct SingleThreadSynchronizationContextStall
{
    /// <summary>The observed context. May already be disposed when the message is handled.</summary>
    public readonly SingleThreadSynchronizationContext Context;
    /// <summary>How long the same work item has been running without finishing.</summary>
    public readonly TimeSpan StallDuration;
    /// <summary>Number of work items waiting in the queue when the stall was detected.</summary>
    public readonly int QueuedWorkItemsCount;

    public SingleThreadSynchronizationContextStall(SingleThreadSynchronizationContext context, TimeSpan stallDuration, int queuedWorkItemsCount)
    {
        Context = context;
        StallDuration = stallDuration;
        QueuedWorkItemsCount = queuedWorkItemsCount;
    }

    public override string ToString() =>
        $"SingleThreadSynchronizationContext '{Context?.Thread?.Name}' is stalled for {StallDuration} with {QueuedWorkItemsCount} queued work item(s).";
}

/// <summary>
/// Optional watchdog that periodically samples the diagnostic properties of a
/// <see cref="SingleThreadSynchronizationContext"/> and forwards a
/// <see cref="SingleThreadSynchronizationContextStall"/> message to all connected sinks when the
/// context thread appears to be stuck inside a work item.
/// </summary>
/// <remarks>
/// A stall is reported when <see cref="SingleThreadSynchronizationContext.IsExecutingWorkItem"/> stays true
/// while <see cref="SingleThreadSynchronizationContext.FinishedWorkItemsCount"/> does not change for at least
/// the configured stall threshold. As long as the stall persists, further messages are sent only once per
/// <c>repeatInterval</c>, so a permanently blocked callback does not flood the listeners.
/// <para>
/// The watchdog is purely observational: it cannot unblock the thread. .NET Core and later do not support
/// <c>Thread.Abort</c>, and even a forced abort would leave the locks held by the stuck callback locked
/// forever. The listener has to decide how to react, typically by logging and disposing/recreating the
/// context.
/// </para>
/// <para>
/// The watchdog only holds a weak reference to the context, so it does not keep it alive. It stops
/// automatically once the context has been collected or disposed, and can be stopped explicitly
/// via <see cref="Dispose"/>.
/// </para>
/// </remarks>
public sealed class SingleThreadSynchronizationContextWatchdog : IMessageSource, IDisposable
{
    private SourceHelper sourceHelper = new SourceHelper();
    private readonly WeakReference<SingleThreadSynchronizationContext> contextRef;
    private readonly TimeSpan stallThreshold;
    private readonly TimeSpan repeatInterval;
    // Strong reference: the scheduler only keeps a weak one, so the schedule must be kept alive here.
    private ActionSchedule schedule;

    // Sampling state, only touched by the scheduler thread.
    private int lastFinishedCount = -1;
    private DateTime unchangedSince;
    private DateTime lastReported = DateTime.MinValue;

    /// <summary>
    /// Creates and starts a watchdog for the given context.
    /// </summary>
    /// <param name="context">The context to observe. Only a weak reference is kept.</param>
    /// <param name="stallThreshold">How long a single work item may run before a stall is reported.</param>
    /// <param name="checkInterval">Sampling interval. Defaults to a quarter of the stall threshold.</param>
    /// <param name="repeatInterval">Minimum interval between repeated reports of an ongoing stall.
    /// Defaults to the stall threshold.</param>
    public SingleThreadSynchronizationContextWatchdog(SingleThreadSynchronizationContext context, TimeSpan stallThreshold, TimeSpan? checkInterval = null, TimeSpan? repeatInterval = null)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (stallThreshold <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(stallThreshold));

        this.contextRef = new WeakReference<SingleThreadSynchronizationContext>(context);
        this.stallThreshold = stallThreshold;
        this.repeatInterval = repeatInterval ?? stallThreshold;
        TimeSpan interval = checkInterval ?? TimeSpan.FromTicks(stallThreshold.Ticks / 4);
        if (interval <= TimeSpan.Zero) interval = stallThreshold;

        this.schedule = Service<SchedulerService>.Instance.ScheduleAction(
            $"Watchdog for {context.Thread?.Name}",
            now => Check(now, interval));
    }

    private ScheduleStatus Check(DateTime now, TimeSpan interval)
    {
        // Terminate the schedule if the observed context is gone or shut down.
        if (!contextRef.TryGetTarget(out var context) || context.IsDisposed) return ScheduleStatus.Terminated;

        int finished = context.FinishedWorkItemsCount;
        if (!context.IsExecutingWorkItem || finished != lastFinishedCount)
        {
            // Progress was made (or the thread is idle): reset the stall tracking.
            lastFinishedCount = finished;
            unchangedSince = now;
            lastReported = DateTime.MinValue;
            return ScheduleStatus.WaitUntil(now + interval);
        }

        TimeSpan stallDuration = now - unchangedSince;
        if (stallDuration >= stallThreshold && now - lastReported >= repeatInterval)
        {
            lastReported = now;
            sourceHelper.Forward(new SingleThreadSynchronizationContextStall(context, stallDuration, context.QueuedWorkItemsCount));
        }

        return ScheduleStatus.WaitUntil(now + interval);
    }

    /// <summary>Gets the number of currently connected sinks.</summary>
    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

    /// <summary>Indicates whether there are no connected sinks.</summary>
    public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

    /// <summary>Checks whether the provided sink is connected.</summary>
    public bool IsConnected(IMessageSink sink) => sourceHelper.IsConnected(sink);

    /// <summary>Connects this watchdog to a sink that receives <see cref="SingleThreadSynchronizationContextStall"/> messages.</summary>
    public void ConnectTo(IMessageSink sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

    /// <summary>Connects this watchdog to a bidirectional element and returns it typed as a source.</summary>
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

    /// <summary>Disconnects all currently connected sinks.</summary>
    public void DisconnectAll() => sourceHelper.DisconnectAll();

    /// <summary>Disconnects the specified sink if connected.</summary>
    public void DisconnectFrom(IMessageSink sink) => sourceHelper.DisconnectFrom(sink);

    /// <summary>Returns the currently connected sinks.</summary>
    public IMessageSink[] GetConnectedSinks() => sourceHelper.GetConnectedSinks();

    /// <summary>
    /// Stops the watchdog and disconnects all sinks. The observed context is not affected.
    /// </summary>
    public void Dispose()
    {
        // Dropping the strong reference lets the scheduler discard the weakly referenced schedule.
        schedule = null;
        sourceHelper.DisconnectAll();
    }
}
