using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Forwarder that can be activated and deactivated. When deactivated it will not forward any message.
    /// Activation and deactivation can either be done via the <see cref="Active"/> property or it can be done
    /// automatically by providing a function delegate that, for each incoming message, decides whether the
    /// forwarder is active or not. This allows inhibiting communication in specific application states.
    /// </summary>
    /// <remarks>
    /// Threading:
    /// - Reads/writes to <see cref="Active"/> can optionally use volatile memory semantics controlled by the constructor's
    ///   <c>volatileAccess</c> parameter. When disabled, plain reads/writes are used for higher performance.
    /// - If an <c>autoActivationCondition</c> is supplied, its result is written back to <see cref="Active"/> and used immediately
    ///   for the forwarding decision to avoid race conditions between evaluation and send.
    ///
    /// Usage:
    /// - Connect sinks via <see cref="ConnectTo(IMessageSink, bool)"/> or <see cref="ConnectTo(IMessageFlowConnection, bool)"/>.
    /// - Send via <see cref="Post{M}(in M)"/>, <see cref="Post{M}(M)"/>, or <see cref="PostAsync{M}(M)"/>.
    /// - Set <see cref="Active"/> to enable/disable forwarding manually or provide an <c>autoActivationCondition</c> to compute it per message.
    ///
    /// Performance notes:
    /// - Forwarding is allocation-conscious and leverages <see cref="SourceValueHelper"/> which is a mutable struct intended
    ///   to be kept as a field on a reference type. Avoid copying it.
    /// </remarks>
    public sealed class DeactivatableForwarder : IMessageSink, IMessageSource, IMessageFlowConnection
    {
        private SourceValueHelper sourceHelper;
        private bool active = true;
        private readonly Func<bool, bool> autoActivationCondition = null;
        readonly bool volatileAccess;

        /// <summary>
        /// Number of currently connected sinks (excluding already collected weak references).
        /// </summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary> Indicates whether there are no connected sinks. </summary>
        public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

        /// <summary>
        /// Gets or sets the activation state.
        /// When <c>false</c>, messages are not forwarded.
        /// </summary>
        /// <remarks>
        /// - If constructed with <c>volatileAccess == true</c>, getter/setter use volatile access for cross-thread visibility.
        /// - If an <see cref="autoActivationCondition"/> was provided, it may update this state per message.
        /// </remarks>
        public bool Active 
        { 
            get 
            { 
                return volatileAccess ? Volatile.Read(ref active) : active; 
            } 
            set 
            { 
                if (volatileAccess) Volatile.Write(ref active, value);
                else active = value; 
            } 
        }

        /// <summary>
        /// Creates a new <see cref="DeactivatableForwarder"/>.
        /// </summary>
        /// <param name="autoActivationCondition">
        /// Optional per-message activation function. It receives the current <see cref="Active"/> state and
        /// returns the desired new state. The returned state is written back to <see cref="Active"/> and used
        /// immediately to decide whether to forward the current message.
        /// </param>
        /// <param name="volatileAccess">
        /// When <c>true</c> (default), <see cref="Active"/> uses volatile reads/writes for better cross-thread visibility.
        /// When <c>false</c>, plain reads/writes are used for lower overhead.
        /// </param>
        public DeactivatableForwarder(Func<bool, bool> autoActivationCondition = null, bool volatileAccess = true)
        {
            this.autoActivationCondition = autoActivationCondition;
            this.volatileAccess = volatileAccess;
        }

        /// <summary>
        /// Re-evaluates activation when an autoActivationCondition was provided.
        /// Reads the current <see cref="Active"/> state, passes it to the condition, writes back the result,
        /// and returns the new state.
        /// </summary>
        /// <returns>The updated activation state.</returns>
        private bool UpdateActive()
        {
            bool localActive = Active;
            localActive = autoActivationCondition(localActive);
            Active = localActive;
            return localActive;
        }

        /// <summary>
        /// Posts a message to connected sinks by reference.
        /// Uses the per-message activation condition when present; otherwise uses the current <see cref="Active"/> state.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">Message to post (passed by reference to avoid copies for large structs).</param>
        public void Post<M>(in M message)
        {
            if (autoActivationCondition != null)
            {
                bool localActive = UpdateActive();
                if (localActive) sourceHelper.Forward(in message);
            }
            else if (Active) sourceHelper.Forward(in message);
        }

        /// <summary>
        /// Posts a message to connected sinks by value.
        /// Uses the per-message activation condition when present; otherwise uses the current <see cref="Active"/> state.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">Message to post.</param>
        public void Post<M>(M message)
        {
            if (autoActivationCondition != null)
            {
                bool localActive = UpdateActive();
                if (localActive) sourceHelper.Forward(message);
            }
            else if (Active) sourceHelper.Forward(message);
        }

        /// <summary>
        /// Posts a message to connected sinks asynchronously.
        /// Uses the per-message activation condition when present; otherwise uses the current <see cref="Active"/> state.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">Message to post asynchronously.</param>
        /// <returns>
        /// A task that completes when forwarding completes. If deactivated for this message, returns <see cref="Task.CompletedTask"/>.
        /// </returns>
        public Task PostAsync<M>(M message)
        {
            if (autoActivationCondition != null)
            {
                bool localActive = UpdateActive();
                if (localActive) return sourceHelper.ForwardAsync(message);
            }
            else if (Active) return sourceHelper.ForwardAsync(message);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Connects this source to a sink.
        /// When <paramref name="weakReference"/> is <c>true</c>, the connection is held weakly so the sink can be GC-collected.
        /// </summary>
        /// <param name="sink">The sink to connect.</param>
        /// <param name="weakReference">Whether to keep a weak reference to the sink.</param>
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Connects this source to a bidirectional flow element (sink + source).
        /// Returns the same element typed as a source to enable fluent chaining.
        /// </summary>
        /// <param name="sink">The bidirectional element to connect.</param>
        /// <param name="weakReference">Whether to keep a weak reference to the element.</param>
        /// <returns>The provided <paramref name="sink"/> typed as an <see cref="IMessageSource"/>.</returns>
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
        /// Returns a snapshot of currently connected sinks.
        /// Invalid weak references are pruned lazily after the call if encountered.
        /// </summary>
        /// <returns>Array of connected sinks.</returns>
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        /// <summary>
        /// Checks whether the provided sink is currently connected.
        /// </summary>
        /// <param name="sink">Sink to check.</param>
        /// <returns><c>true</c> if connected; otherwise <c>false</c>.</returns>
        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}