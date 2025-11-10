using FeatureLoom.Logging;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Forwards messages asynchronously without awaiting, so control returns immediately to the caller.
    /// </summary>
    /// <remarks>
    /// Behavior:
    /// - Each post is offloaded to the ThreadPool and uses <see cref="SourceHelper.ForwardAsync{M}(M)"/> to forward to sinks.
    /// - Per-send sink order (0..N-1) is preserved by the underlying async forwarding; however, ordering across multiple concurrent sends is not guaranteed.
    /// - Exceptions thrown by downstream sinks are observed and logged via <see cref="OptLog"/>, and are not propagated to the caller.
    /// - When no sinks are connected, posts are ignored without scheduling any work.
    /// If strict ordering across sends is required, use <see cref="QueueForwarder{T}"/>.
    /// </remarks>
    public sealed class AsyncForwarder : IMessageFlowConnection
    {
        private SourceHelper sourceHelper = new SourceHelper();

        /// <summary>
        /// Gets the number of currently connected sinks (excluding already collected weak references).
        /// </summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary> Indicates whether there are no connected sinks. </summary>
        public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

        /// <summary>
        /// Connects this forwarder to a sink.
        /// </summary>
        /// <param name="sink">The sink to connect.</param>
        /// <param name="weakReference">True to keep a weak reference so the sink can be collected by the GC.</param>
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Connects this forwarder to a bidirectional element and returns it typed as a source for fluent chaining.
        /// </summary>
        /// <param name="sink">The bidirectional element to connect.</param>
        /// <param name="weakReference">True to keep a weak reference so the sink can be collected by the GC.</param>
        /// <returns>The connected element typed as a source.</returns>
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Disconnects all currently connected sinks.
        /// </summary>
        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        /// <summary>
        /// Disconnects the specified sink if connected.
        /// </summary>
        /// <param name="sink">The sink to disconnect.</param>
        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        /// <summary>
        /// Returns the currently connected sinks. Invalid weak references are pruned lazily.
        /// </summary>
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        /// <summary>
        /// Checks whether the provided sink is connected.
        /// </summary>
        /// <param name="sink">The sink to check.</param>
        /// <returns>True if the sink is connected; otherwise, false.</returns>
        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }

        /// <summary>
        /// Posts a message by reference and returns immediately. Forwarding is executed asynchronously.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">The message to post.</param>
        public void Post<M>(in M message)
        {
            _ = PostAsync(message);
        }

        /// <summary>
        /// Posts a message by value and returns immediately. Forwarding is executed asynchronously.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">The message to post.</param>
        public void Post<M>(M message)
        {
            _ = PostAsync(message);
        }

        /// <summary>
        /// Posts a message asynchronously but returns immediately (fire-and-forget).
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">The message to post.</param>
        /// <returns>Always returns a completed task.</returns>
        /// <remarks>
        /// The actual forwarding work is scheduled on the ThreadPool and uses <see cref="SourceHelper.ForwardAsync{M}(M)"/>
        /// to sequentially await sinks (per-send order and backpressure). Exceptions are logged.
        /// </remarks>
        public Task PostAsync<M>(M message)
        {
            if (sourceHelper.NoConnectedSinks) return Task.CompletedTask;
            _ = Task.Run(async () =>
            {
                try
                {
                    await sourceHelper.ForwardAsync(message).ConfiguredAwait();
                }
                catch (Exception ex)
                {
                    OptLog.ERROR()?.Build("Exception caught in AsyncForwarder while sending.", ex);
                }
            });
            return Task.CompletedTask;
        }
    }
}