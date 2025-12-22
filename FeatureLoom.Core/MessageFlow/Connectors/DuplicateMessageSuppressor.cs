using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// Suppresses forwarding of duplicate messages of type <typeparamref name="T"/> within a configurable time window.
/// Messages of other types are forwarded unchanged.
/// Thread-safe.
/// </summary>
/// <typeparam name="T">Message type to check for duplicates.</typeparam>
/// <remarks>
/// - A message is considered a duplicate when the provided predicate evaluates to true
///   against a previously seen message that is still within the suppression window.
/// - Uses a FIFO queue with expiration timestamps; cleanup is performed periodically by a scheduled action.
/// - Duplicate check is an O(n) scan over currently tracked messages. High volumes of messages with long suppression times
///   may impact performance.
/// - Time source is <see cref="AppTime.Now"/> when <c>preciseTime</c> is true, or <see cref="AppTime.CoarseNow"/> for lower overhead.
/// </remarks>
public sealed class DuplicateMessageSuppressor<T> : IMessageSource, IMessageFlowConnection
{
    private SourceValueHelper sourceHelper;
    private Queue<(T message, DateTime suppressionEnd)> suppressors = new Queue<(T, DateTime)>();
    private MicroLock suppressorsLock = new MicroLock();
    private readonly TimeSpan suppressionTime;
    private readonly TimeSpan cleanupPeriode = 10.Seconds();
    private readonly Func<T, T, bool> isDuplicate;
    private ISchedule scheduledAction;
    private bool preciseTime;

    /// <summary>
    /// Creates a new suppressor for messages of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="suppressionTime">Duration a seen message suppresses identical subsequent ones.</param>
    /// <param name="isDuplicate">
    /// Optional predicate to determine whether two messages are duplicates. Defaults to <c>a.Equals(b)</c>.
    /// </param>
    /// <param name="preciseTime">
    /// When true, uses <see cref="AppTime.Now"/> for higher precision; otherwise uses <see cref="AppTime.CoarseNow"/> for lower overhead.
    /// </param>
    /// <param name="cleanupPeriode">
    /// Optional cleanup period for removing expired entries. If unspecified, a default is used and then clamped to at least
    /// <c>suppressionTime * 100</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="suppressionTime"/> is not greater than zero or when <paramref name="cleanupPeriode"/> is negative.
    /// </exception>
    public DuplicateMessageSuppressor(TimeSpan suppressionTime, Func<T, T, bool> isDuplicate = null, bool preciseTime = false, TimeSpan cleanupPeriode = default)
    {
        if (suppressionTime <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(suppressionTime), "Suppression time must be greater than zero.");
        if (cleanupPeriode < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cleanupPeriode), "Cleanup periode must be non-negative.");

        this.suppressionTime = suppressionTime;
        if (isDuplicate == null) isDuplicate = (a, b) => a.Equals(b);
        this.isDuplicate = isDuplicate;
        this.preciseTime = preciseTime;

        if (cleanupPeriode == default) cleanupPeriode = this.cleanupPeriode;
        cleanupPeriode = cleanupPeriode.ClampLow(suppressionTime.Multiply(100));
        this.cleanupPeriode = cleanupPeriode;

        Action<DateTime> cleanUpAction = now =>
        {
            if (suppressors.Count == 0) return;

            Task.Run(() =>
            {
                using (suppressorsLock.Lock())
                {
                    CleanUpSuppressors(now);
                }
            });
        };
        this.scheduledAction = cleanUpAction.ScheduleForRecurringExecution("DuplicateMessageSuppressor", cleanupPeriode);
    }

    /// <summary>
    /// Adds a message to the suppression set as if it had just been received.
    /// Subsequent equal messages (according to the duplicate predicate) will be suppressed until the window expires.
    /// </summary>
    /// <param name="suppressorMessage">Message to insert into the suppression window.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddSuppressor(T suppressorMessage)
    {            
        using (suppressorsLock.Lock())
        {
            DateTime now = preciseTime ? AppTime.Now :AppTime.CoarseNow;
            suppressors.Enqueue((suppressorMessage, now + suppressionTime));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsSuppressed(in T message)
    {
        using (suppressorsLock.Lock())
        {
            DateTime now = preciseTime ? AppTime.Now : AppTime.CoarseNow;
            CleanUpSuppressors(now);
            foreach (var suppressor in suppressors)
            {
                if (isDuplicate(suppressor.message, message))
                {
                    return true;
                }
            }
            suppressors.Enqueue((message, now + suppressionTime));
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CleanUpSuppressors(DateTime now)
    {
        while (suppressors.Count > 0)
        {
            if (now > suppressors.Peek().suppressionEnd) suppressors.Dequeue();
            else break;
        }
    }

    /// <summary>
    /// Number of currently connected sinks.
    /// </summary>
    public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

    /// <summary>
    /// Indicates whether there are no connected sinks.
    /// </summary>
    public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

    /// <summary>
    /// Disconnects all sinks from this source.
    /// </summary>
    public void DisconnectAll()
    {
        sourceHelper.DisconnectAll();
    }

    /// <summary>
    /// Disconnects the specified sink from this source.
    /// </summary>
    /// <param name="sink">Sink to disconnect.</param>
    public void DisconnectFrom(IMessageSink sink)
    {
        sourceHelper.DisconnectFrom(sink);
    }

    /// <summary>
    /// Returns the currently connected sinks.
    /// </summary>
    public IMessageSink[] GetConnectedSinks()
    {
        return sourceHelper.GetConnectedSinks();
    }

    /// <summary>
    /// Forwards the message by reference unless it is of type <typeparamref name="T"/> and is suppressed.
    /// </summary>
    /// <typeparam name="M">Type of the message.</typeparam>
    /// <param name="message">Message to forward.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Post<M>(in M message)
    {
        if (message is T msg && IsSuppressed(in msg)) return;
        sourceHelper.Forward(in message);
    }

    /// <summary>
    /// Forwards the message by value unless it is of type <typeparamref name="T"/> and is suppressed.
    /// </summary>
    /// <typeparam name="M">Type of the message.</typeparam>
    /// <param name="message">Message to forward.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Post<M>(M message)
    {
        if (message is T msg && IsSuppressed(msg)) return;
        sourceHelper.Forward(message);
    }

    /// <summary>
    /// Asynchronously forwards the message unless it is of type <typeparamref name="T"/> and is suppressed.
    /// </summary>
    /// <typeparam name="M">Type of the message.</typeparam>
    /// <param name="message">Message to forward.</param>
    /// <returns>
    /// A completed task when the message was suppressed; otherwise, the forwarding task from the connected sinks.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PostAsync<M>(M message)
    {
        if (message is T msg && IsSuppressed(msg)) return Task.CompletedTask;
        return sourceHelper.ForwardAsync(message);
    }

    /// <summary>
    /// Connects this source to a sink.
    /// When <paramref name="weakReference"/> is true, the connection is held weakly so the sink can be GC-collected.
    /// </summary>
    /// <param name="sink">The sink to connect.</param>
    /// <param name="weakReference">True to keep a weak reference to the sink; otherwise, a strong reference.</param>
    public void ConnectTo(IMessageSink sink, bool weakReference = false)
    {
        sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>
    /// Connects this source to a bidirectional flow element and returns it typed as a source to enable fluent chaining.
    /// </summary>
    /// <param name="sink">The bidirectional flow element (sink+source) to connect.</param>
    /// <param name="weakReference">True to keep a weak reference; otherwise, a strong reference.</param>
    /// <returns>The provided flow element typed as a source.</returns>
    public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
    {
        return sourceHelper.ConnectTo(sink, weakReference);
    }

    /// <summary>
    /// Checks whether the specified sink is currently connected.
    /// </summary>
    /// <param name="sink">Sink to check.</param>
    /// <returns>True when connected; otherwise false.</returns>
    public bool IsConnected(IMessageSink sink)
    {
        return sourceHelper.IsConnected(sink);
    }
}