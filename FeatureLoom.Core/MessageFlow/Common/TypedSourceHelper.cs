using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Thin, typed wrapper around <see cref="TypedSourceValueHelper{T}"/> to implement <see cref="IMessageSource{T}"/>.
    /// </summary>
    /// <remarks>
    /// Behavior
    /// - Forwarding is lock-free and preserves sink order (0..N-1) for both sync and async paths.
    /// - The async path uses a zero-allocation fast path when all sinks complete synchronously, and may return the last pending task directly.
    /// - Weak references are supported; invalid (collected) sinks are lazily pruned after forwarding or during connect/disconnect.
    /// - Connecting/disconnecting acquires a short lock.
    /// - This helper is intended to forward messages of type <see cref="SentMessageType"/>.
    /// </remarks>
    public sealed class TypedSourceHelper<T> : IMessageSource<T>
    {
        private TypedSourceValueHelper<T> sourceHelper;

        /// <summary>Number of currently connected sinks (excluding already collected weak refs).</summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary>
        /// Gets a value indicating whether there are no connected sinks.
        /// </summary>
        public bool NotConnected => sourceHelper.NotConnected;

        /// <summary>The static message type this helper forwards.</summary>
        public Type SentMessageType => typeof(T);

        /// <summary>
        /// Connects this source to a sink. When <paramref name="weakReference"/> is true, the sink is held weakly (GC can collect it).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Connects this source to a bidirectional element (sink+source) and returns it typed as a source for fluent chaining.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>Disconnects all sinks from this source.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        /// <summary>Disconnects the specified sink if connected.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        /// <summary>
        /// Returns the currently connected sinks (invalid weak refs are pruned lazily).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        /// <summary>Checks whether the provided sink is currently connected and alive.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }

        /// <summary>
        /// Forwards a message by reference to all connected sinks (0..N-1). Lock-free; lazily prunes invalid sinks after the send if encountered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Forward(in T message)
        {
            sourceHelper.Forward(in message);
        }

        /// <summary>
        /// Forwards a message by value to all connected sinks (0..N-1). Lock-free; lazily prunes invalid sinks after the send if encountered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Forward(T message)
        {
            sourceHelper.Forward(message);
        }

        /// <summary>
        /// Asynchronously forwards a message to all sinks sequentially (0..N-1).
        /// Uses a zero-allocation fast path when all sinks complete synchronously; may return the last pending task directly.
        /// Lazily prunes invalid sinks after the send if encountered.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task ForwardAsync(T message)
        {
            return sourceHelper.ForwardAsync(message);
        }
    }
}