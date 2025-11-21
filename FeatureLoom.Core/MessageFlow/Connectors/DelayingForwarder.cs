using FeatureLoom.Logging;
using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Forwards messages after a fixed delay.
    /// Supports blocking (caller waits) and non-blocking (fire-and-forget) modes.
    /// Optional precise timing uses the precise wait APIs (higher cost).
    /// </summary>
    /// <remarks>
    /// - Non-blocking mode offloads to the ThreadPool; the returned task from <see cref="PostAsync{M}(M)"/> does NOT
    ///   represent completion of forwarding.
    /// - The configured delay is always performed for consistency, regardless of whether sinks are connected
    ///   at call time. If no sinks were connected at call time, no forwarding is performed after the delay.
    /// - Instance-level <see cref="CancellationToken"/> (see constructor and <see cref="UpdateCancellationToken(CancellationToken)"/>)
    ///   is honored during waiting. The AppTime wait APIs used here do not throw when canceled; they return early instead.
    ///   In the current implementation, cancellation may shorten or skip the wait, but forwarding behavior is determined
    ///   by the sink snapshot taken at call time.
    /// </remarks>
    public sealed class DelayingForwarder : IMessageSink, IMessageSource, IMessageFlowConnection
    {
        private SourceHelper sourceHelper = new SourceHelper();
        private readonly TimeSpan delay;
        private readonly bool blocking;
        private readonly bool delayPrecisely;
        private CancellationToken ct;

        /// <summary>
        /// Creates a forwarder that delays forwarding by a fixed amount.
        /// </summary>
        /// <param name="delay">The fixed delay applied before forwarding. Must be non-negative.</param>
        /// <param name="delayPrecisely">True to use precise waiting APIs (higher CPU cost, higher accuracy).</param>
        /// <param name="blocking">True to block the caller until the delay elapsed (or was canceled).</param>
        /// <param name="ct">
        /// Instance-level cancellation token used during the wait period.
        /// Cancellation does not throw; waits return early when canceled.
        /// Waiting happens even when no sinks are connected at call time (for consistency).
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="delay"/> is negative.</exception>
        public DelayingForwarder(TimeSpan delay, bool delayPrecisely = false, bool blocking = false, CancellationToken ct = default)
        {
            if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay), "Delay must not be negative.");
            this.delay = delay;
            this.blocking = blocking;
            this.delayPrecisely = delayPrecisely;
            this.ct = ct;
        }

        /// <summary>
        /// Updates the instance-level cancellation token used for future waits.
        /// </summary>
        /// <param name="newCt">The new token. It may affect pending non-started waits depending on timing.</param>
        /// <remarks>
        /// Both blocking and non-blocking waits observe the token at the time they execute.
        /// Cancellation shortens/skips the wait; forwarding after the delay depends on whether sinks were connected at call time.
        /// </remarks>
        public void UpdateCancellationToken(CancellationToken newCt)
        {
            ct = newCt;
        }

        /// <summary>Number of currently connected sinks (excluding already collected weak references).</summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary>True when no sinks are connected.</summary>
        public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

        /// <summary>Disconnects all connected sinks.</summary>
        public void DisconnectAll() => sourceHelper.DisconnectAll();

        /// <summary>Disconnects a specific sink if connected.</summary>
        /// <param name="sink">The sink to disconnect.</param>
        public void DisconnectFrom(IMessageSink sink) => sourceHelper.DisconnectFrom(sink);

        /// <summary>Returns a snapshot of currently connected sinks (invalid weak refs are pruned lazily).</summary>
        public IMessageSink[] GetConnectedSinks() => sourceHelper.GetConnectedSinks();

        /// <summary>Checks whether a specific sink is connected.</summary>
        /// <param name="sink">The sink to check.</param>
        public bool IsConnected(IMessageSink sink) => sourceHelper.IsConnected(sink);

        /// <summary>
        /// Posts by reference (avoids a copy for large structs) respecting the configured delay behavior.
        /// Always performs the delay. Only forwards if sinks were connected at call time.
        /// </summary>
        /// <typeparam name="M">Message type.</typeparam>
        /// <param name="message">The message to forward.</param>
        public void Post<M>(in M message)
        {
            bool hadSinksAtCall = !NoConnectedSinks;

            // Zero-delay fast path.
            if (delay <= TimeSpan.Zero)
            {
                if (hadSinksAtCall) sourceHelper.Forward(in message);
                return;
            }

            if (blocking)
            {
                DelayBlocking();
                if (hadSinksAtCall) sourceHelper.Forward(in message);
                return;
            }

            if (!hadSinksAtCall) return;

            var msgCopy = message; // avoid closure on in parameter
            _ = Task.Run(async () =>
            {
                try
                {
                    await DelayAsync().ConfiguredAwait();
                    sourceHelper.Forward(in msgCopy);
                }
                catch (Exception ex)
                {
                    OptLog.ERROR()?.Build("Exception caught in DelayingForwarder while sending (Post by-ref).", ex);
                }
            });
        }

        /// <summary>
        /// Posts by value respecting the configured delay behavior.
        /// Always performs the delay. Only forwards if sinks were connected at call time.
        /// </summary>
        /// <typeparam name="M">Message type.</typeparam>
        /// <param name="message">The message to forward.</param>
        public void Post<M>(M message)
        {
            bool hadSinksAtCall = !NoConnectedSinks;

            // Zero-delay fast path.
            if (delay <= TimeSpan.Zero)
            {
                if (hadSinksAtCall) sourceHelper.Forward(message);
                return;
            }

            if (blocking)
            {
                DelayBlocking();
                if (hadSinksAtCall) sourceHelper.Forward(message);
                return;
            }

            if (!hadSinksAtCall) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await DelayAsync().ConfiguredAwait();
                    sourceHelper.Forward(message);
                }
                catch (Exception ex)
                {
                    OptLog.ERROR()?.Build("Exception caught in DelayingForwarder while sending (Post by-value).", ex);
                }
            });
        }

        /// <summary>
        /// Asynchronously posts a message.
        /// Always performs the delay. Only forwards if sinks were connected at call time.
        /// </summary>
        /// <typeparam name="M">Message type.</typeparam>
        /// <param name="message">The message to forward.</param>
        /// <returns>
        /// In blocking mode: a task that completes after the delay and forwarding finished (if sinks existed at call time).
        /// In non-blocking mode: returns immediately after scheduling background work; the returned task does NOT represent forwarding completion.
        /// </returns>
        public async Task PostAsync<M>(M message)
        {
            bool hadSinksAtCall = !NoConnectedSinks;

            if (blocking)
            {
                await DelayAsync().ConfiguredAwait();
                if (hadSinksAtCall) await sourceHelper.ForwardAsync(message).ConfiguredAwait();
                return;
            }

            if (!hadSinksAtCall) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (delay > TimeSpan.Zero)
                    {
                        await DelayAsync().ConfiguredAwait();
                    }
                    await sourceHelper.ForwardAsync(message).ConfiguredAwait();
                }
                catch (Exception ex)
                {
                    OptLog.ERROR()?.Build("Exception caught in DelayingForwarder while sending (PostAsync).", ex);
                }
            });
        }

        /// <summary>
        /// Connects this forwarder to a sink.
        /// </summary>
        /// <param name="sink">The sink to connect.</param>
        /// <param name="weakReference">True to hold a weak reference (GC can collect the sink).</param>
        public void ConnectTo(IMessageSink sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

        /// <summary>
        /// Connects this forwarder to a bidirectional element and returns it typed as a source for fluent chaining.
        /// </summary>
        /// <param name="sink">The bidirectional element to connect.</param>
        /// <param name="weakReference">True to hold a weak reference (GC can collect the sink).</param>
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false) => sourceHelper.ConnectTo(sink, weakReference);

        /// <summary>
        /// Performs the blocking delay if configured (ignores sink count).
        /// </summary>
        private void DelayBlocking()
        {
            if (delayPrecisely) AppTime.WaitPrecisely(delay, ct);
            else AppTime.Wait(delay, ct);
        }

        /// <summary>
        /// Performs the asynchronous delay if configured (ignores sink count).
        /// </summary>
        private Task DelayAsync()
        {
            if (delayPrecisely) return AppTime.WaitPreciselyAsync(delay, ct);
            return AppTime.WaitAsync(delay, ct);
        }
    }
}