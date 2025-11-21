using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Splits messages of type <typeparamref name="T"/> into zero or more messages of type <typeparamref name="E"/>
    /// using the provided split function and forwards each produced element to all connected sinks.
    /// Non-matching messages (not of type <typeparamref name="T"/>) can be forwarded unchanged when enabled.
    /// </summary>
    /// <typeparam name="T">The input type that is recognized and split.</typeparam>
    /// <typeparam name="E">The output type produced for each split element.</typeparam>
    /// <remarks>
    /// Concurrency and ordering:
    /// - Forwarding to connected sinks is performed by <see cref="SourceValueHelper"/>, which calls sinks in index order (0..N-1).
    /// - The async path awaits each produced element sequentially to preserve ordering.
    ///
    /// Split function behavior:
    /// - When the split delegate returns <c>null</c>, no output is forwarded.
    /// - Prefer a fast, non-blocking split function to avoid blocking the caller for synchronous posts.
    ///
    /// Other messages:
    /// - When <c>forwardOtherMessages == true</c>, messages not of type <typeparamref name="T"/> are forwarded unchanged.
    /// </remarks>
    public sealed class Splitter<T, E> : IMessageFlowConnection, IMessageSink
    {
        private SourceValueHelper sourceHelper;
        private readonly Func<T, IEnumerable<E>> split;
        private readonly bool forwardOtherMessages;

        /// <summary>
        /// Creates a new <see cref="Splitter{T, E}"/>.
        /// </summary>
        /// <param name="split">Delegate used to split an input message of type <typeparamref name="T"/> into zero or more <typeparamref name="E"/> elements.</param>
        /// <param name="forwardOtherMessages">
        /// When true, messages that are not of type <typeparamref name="T"/> are forwarded unchanged to connected sinks.
        /// When false, such messages are ignored.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="split"/> is null.</exception>
        public Splitter(Func<T, IEnumerable<E>> split, bool forwardOtherMessages = true)
        {
            this.split = split ?? throw new ArgumentNullException(nameof(split));
            this.forwardOtherMessages = forwardOtherMessages;
        }

        /// <summary>
        /// Posts a message by reference.
        /// If <paramref name="message"/> is of type <typeparamref name="T"/>, it is split and each produced <typeparamref name="E"/> is forwarded.
        /// If the split result is <c>null</c>, nothing is forwarded.
        /// Non-matching messages are optionally forwarded unchanged, depending on configuration.
        /// </summary>
        /// <typeparam name="M">The runtime type of the posted message.</typeparam>
        /// <param name="message">The message to post.</param>
        public void Post<M>(in M message)
        {
            if (message is T tMsg)
            {
                var output = split(tMsg);
                if (output == null) return;

                foreach (var msg in output)
                {
                    sourceHelper.Forward(in msg);
                }
            }
            else if (forwardOtherMessages) sourceHelper.Forward(in message);
        }

        /// <summary>
        /// Posts a message by value.
        /// If <paramref name="message"/> is of type <typeparamref name="T"/>, it is split and each produced <typeparamref name="E"/> is forwarded.
        /// If the split result is <c>null</c>, nothing is forwarded.
        /// Non-matching messages are optionally forwarded unchanged, depending on configuration.
        /// </summary>
        /// <typeparam name="M">The runtime type of the posted message.</typeparam>
        /// <param name="message">The message to post.</param>
        public void Post<M>(M message)
        {
            if (message is T tMsg)
            {
                var output = split(tMsg);
                if (output == null) return;

                foreach (var msg in output)
                {
                    sourceHelper.Forward(msg);
                }
            }
            else if (forwardOtherMessages) sourceHelper.Forward(message);
        }

        /// <summary>
        /// Posts a message asynchronously.
        /// If <paramref name="message"/> is of type <typeparamref name="T"/>, it is split and each produced <typeparamref name="E"/> is forwarded sequentially,
        /// awaiting each send to preserve ordering. If the split result is <c>null</c>, nothing is forwarded.
        /// Non-matching messages are optionally forwarded unchanged, depending on configuration.
        /// </summary>
        /// <typeparam name="M">The runtime type of the posted message.</typeparam>
        /// <param name="message">The message to post.</param>
        public async Task PostAsync<M>(M message)
        {
            if (message is T tMsg)
            {
                var output = split(tMsg);
                if (output == null) return;

                foreach (var msg in output)
                {
                    await sourceHelper.ForwardAsync(msg).ConfigureAwait(false);
                }
            }
            else if (forwardOtherMessages) await sourceHelper.ForwardAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Number of currently connected sinks (excluding already collected weak references).
        /// </summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary>
        /// Indicates whether there are no connected sinks.
        /// </summary>
        public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

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
        /// Returns a snapshot of the currently connected sinks.
        /// Invalid weak references are pruned lazily after the call if encountered.
        /// </summary>
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        /// <summary>
        /// Connects this source to a sink.
        /// When <paramref name="weakReference"/> is true, the connection is held weakly so the GC can collect the sink.
        /// </summary>
        /// <param name="sink">The sink to connect.</param>
        /// <param name="weakReference">When true, keeps a weak reference to the sink.</param>
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Connects this source to a bidirectional element (sink + source),
        /// returning the same element typed as a source to enable fluent chaining.
        /// </summary>
        /// <param name="sink">The message flow connection to connect to.</param>
        /// <param name="weakReference">When true, keeps a weak reference to the sink.</param>
        /// <returns>The provided <paramref name="sink"/> typed as an <see cref="IMessageSource"/>.</returns>
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Checks whether the provided sink is currently connected and alive.
        /// </summary>
        /// <param name="sink">The sink to check.</param>
        /// <returns>True if the sink is connected; otherwise false.</returns>
        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}