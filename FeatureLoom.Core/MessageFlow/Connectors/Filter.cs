using FeatureLoom.Helpers;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Filters messages by type and an optional predicate.
    /// Messages of type <typeparamref name="T"/> are checked with the provided predicate and forwarded
    /// to connected sinks if accepted; otherwise they are routed to the alternative <see cref="Else"/> source
    /// (when it is accessed/connected). Messages not of type <ref name="T"/> are either forwarded
    /// unchanged or routed to <see cref="Else"/>, depending on forwardOtherMessages.
    /// </summary>
    /// <remarks>
    /// Thread-safety: forwarding is thread-safe as long as the provided predicate is thread-safe.
    /// Avoid executing long-running predicates to prevent blocking the sender.
    /// </remarks>
    /// <typeparam name="T">The input message type to filter.</typeparam>
    public sealed class Filter<T> : IMessageFlowConnection<T>, IAlternativeMessageSource
    {
        private SourceValueHelper sourceHelper;
        private readonly Predicate<T> predicate;
        private LazyValue<SourceHelper> alternativeSendingHelper;
        private readonly bool forwardOtherMessages;

        /// <summary>
        /// Gets the runtime type of messages sent to connected sinks by this element.
        /// </summary>
        public Type SentMessageType => typeof(T);

        /// <summary>
        /// Gets the runtime type of messages this element consumes from upstream sources.
        /// </summary>
        public Type ConsumedMessageType => typeof(T);

        /// <summary>
        /// Gets the number of currently connected sinks (excluding already collected weak references).
        /// </summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary> Indicates whether there are no connected sinks. </summary>
        public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

        /// <summary>
        /// Returns the currently connected sinks. Stale weak references are pruned lazily.
        /// </summary>
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        /// <summary>
        /// Creates a new filter.
        /// </summary>
        /// <param name="predicate">
        /// Optional predicate to check messages of type <typeparamref name="T"/>. If <c>null</c>,
        /// all messages of type <typeparamref name="T"/> are forwarded.
        /// </param>
        /// <param name="forwardOtherMessages">
        /// If <c>true</c>, messages that are not of type <typeparamref name="T"/> are forwarded unchanged.
        /// If <c>false</c>, non-matching types are routed to <see cref="Else"/> (when it is accessed/connected).
        /// </param>
        public Filter(Predicate<T> predicate = null, bool forwardOtherMessages = false)
        {
            this.predicate = predicate;
            this.forwardOtherMessages = forwardOtherMessages;
        }

        /// <summary>
        /// Provides an alternative message source that receives messages declined by the filter:
        /// - Messages of type <typeparamref name="T"/> for which the filter predicate returns <c>false</c>.
        /// - Messages not of type <typeparamref name="T"/> when forwardOtherMessages is <c>false</c>.
        /// </summary>
        /// <remarks>
        /// The underlying source is created lazily on first access to avoid allocations when unused.
        /// </remarks>
        public IMessageSource Else => alternativeSendingHelper.Obj;

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
        /// Posts a message by reference to the filter.
        /// </summary>
        /// <typeparam name="M">The runtime type of the posted message.</typeparam>
        /// <param name="message">The message to post.</param>
        /// <remarks>
        /// - If <paramref name="message"/> is of type <typeparamref name="T"/> and the predicate accepts it (or no predicate was provided),
        ///   it is forwarded to the connected sinks.
        /// - Otherwise, the message is routed to the alternative <see cref="Else"/> source if it exists.
        /// - For messages not of type <typeparamref name="T"/>, forwarding vs. routing depends on forwardOtherMessages.
        /// </remarks>
        public void Post<M>(in M message)
        {
            if (message is T msgT)
            {
                if (predicate == null || predicate(msgT)) sourceHelper.Forward(in msgT);
                else alternativeSendingHelper.ObjIfExists?.Forward(in msgT);
            }
            else if (forwardOtherMessages) sourceHelper.Forward(in message);
            else alternativeSendingHelper.ObjIfExists?.Forward(in message);
        }

        /// <summary>
        /// Posts a message by value to the filter.
        /// </summary>
        /// <typeparam name="M">The runtime type of the posted message.</typeparam>
        /// <param name="message">The message to post.</param>
        /// <remarks>
        /// - If <paramref name="message"/> is of type <typeparamref name="T"/> and the predicate accepts it (or no predicate was provided),
        ///   it is forwarded to the connected sinks.
        /// - Otherwise, the message is routed to the alternative <see cref="Else"/> source if it exists.
        /// - For messages not of type <typeparamref name="T"/>, forwarding vs. routing depends on forwardOtherMessages>.
        /// </remarks>
        public void Post<M>(M message)
        {
            if (message is T msgT)
            {
                if (predicate == null || predicate(msgT)) sourceHelper.Forward(msgT);
                else alternativeSendingHelper.ObjIfExists?.Forward(msgT);
            }
            else if (forwardOtherMessages) sourceHelper.Forward(message);
            else alternativeSendingHelper.ObjIfExists?.Forward(message);
        }

        /// <summary>
        /// Asynchronously posts a message to the filter.
        /// </summary>
        /// <typeparam name="M">The runtime type of the posted message.</typeparam>
        /// <param name="message">The message to post asynchronously.</param>
        /// <returns>
        /// A task that completes when forwarding finished for the current snapshot of connected sinks.
        /// If the message is routed to <see cref="Else"/> and it does not exist, a completed task is returned.
        /// </returns>
        public Task PostAsync<M>(M message)
        {
            if (message is T msgT)
            {
                if (predicate == null || predicate(msgT)) return sourceHelper.ForwardAsync(msgT);
                else return alternativeSendingHelper.ObjIfExists?.ForwardAsync(msgT) ?? Task.CompletedTask;
            }
            else if (forwardOtherMessages) return sourceHelper.ForwardAsync(message);
            else return alternativeSendingHelper.ObjIfExists?.ForwardAsync(message) ?? Task.CompletedTask;
        }

        /// <summary>
        /// Connects this filter to a sink.
        /// </summary>
        /// <param name="sink">The sink to connect.</param>
        /// <param name="weakReference">If true, the connection is held weakly so the sink can be garbage-collected.</param>
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Connects this filter to a bidirectional flow element and returns it typed as a source for fluent chaining.
        /// </summary>
        /// <param name="sink">The bidirectional flow element (sink+source) to connect.</param>
        /// <param name="weakReference">If true, the connection is held weakly so the sink can be garbage-collected.</param>
        /// <returns>The same element typed as <see cref="IMessageSource"/> to continue fluent chaining.</returns>
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Checks whether the specified sink is currently connected and alive.
        /// </summary>
        /// <param name="sink">The sink to check.</param>
        /// <returns><c>true</c> if the sink is connected; otherwise <c>false</c>.</returns>
        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}