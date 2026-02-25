using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

/// <summary>
/// A Hub allows creation of multiple <see cref="Socket"/> instances that form a broadcast group.
/// Each message posted on one socket is forwarded to all other sockets of the hub (excluding the sender).
/// </summary>
/// <remarks>
/// Sockets can optionally be associated with an owner object (passed to <see cref="CreateSocket(object)"/>).
/// If an explicit owner different from the hub is provided, the socket is automatically removed once the owner is GC-collected.
/// This auto-removal is implemented lazily and only checked on the receiving side to keep the send hot path fast.
/// </remarks>
public sealed class Hub : IMessageFlow
{
    private Socket[] sockets = Array.Empty<Socket>();
    private MicroLock socketsChangeLock = new MicroLock();

    /// <summary>
    /// Creates a new <see cref="Socket"/> connected to this hub.
    /// Messages sent over the returned socket are forwarded to all other sockets of this hub.
    /// </summary>
    /// <param name="owner">
    /// Optional owner object. If provided and not the hub itself, the socket will be removed automatically
    /// after the owner is garbage-collected. Pass <c>null</c> (default) to disable auto-removal.
    /// </param>
    /// <returns>The newly created socket.</returns>
    public Socket CreateSocket(object owner = null)
    {
        using (socketsChangeLock.Lock())
        {
            var sockets = this.sockets;
            var newSockets = new Socket[sockets.Length + 1];
            var newSocket = new Socket(this, owner);
            Array.Copy(sockets, newSockets, sockets.Length);
            newSockets[sockets.Length] = newSocket;
            this.sockets = newSockets;
            return newSocket;
        }
    }

    /// <summary>
    /// Removes (disposes) the specified socket from the hub.
    /// Safe to call multiple times; subsequent calls have no effect.
    /// </summary>
    /// <param name="socketToRemove">The socket to remove.</param>
    public void RemoveSocket(Socket socketToRemove)
    {
        socketToRemove.Dispose();
    }

    /// <summary>
    /// Removes (disposes) all sockets currently owned by the specified owner object.
    /// </summary>
    /// <param name="owner">The owner object whose sockets should be removed.</param>
    /// <remarks>
    /// This is an explicit cleanup method. Auto-removal also occurs lazily when a socket detects
    /// its owner was GC-collected during message forwarding.
    /// </remarks>
    public void RemoveSocketByOwner(object owner)
    {
        foreach (var socket in sockets)
        {
            if (socket.IsOwnedBy(owner))
            {
                socket.Dispose();
            }
        }
    }

    /// <summary>
    /// Represents one endpoint participating in the hub's broadcast group.
    /// </summary>
    /// <remarks>
    /// Sending:
    /// - <see cref="Post{M}(in M)"/>, <see cref="Post{M}(M)"/> and <see cref="PostAsync{M}(M)"/> forward messages
    ///   to all other sockets of the hub (excluding this socket).
    /// Receiving / Forwarding:
    /// - Downstream sinks are connected via <see cref="ConnectTo(IMessageSink, bool)"/> or
    ///   <see cref="ConnectTo(IMessageFlowConnection, bool)"/>.
    /// Owner-based auto-removal:
    /// - If constructed with an explicit owner different from the hub, a weak reference is stored.
    ///   Once the owner is GC-collected the socket removes itself from the hub during the next receive path.
    /// Thread-safety:
    /// - Posting is lock-free (except for internal array snapshots). Hub membership changes are protected by a <see cref="MicroLock"/>.
    /// </remarks>
    public sealed class Socket : IMessageSink, IMessageSource, IDisposable
    {
        private Hub hub;
        private WeakReference ownerRef; // null when auto-removal is disabled
        private SourceValueHelper sourceHelper;

        /// <summary>
        /// Number of currently connected downstream sinks (invalid weak refs pruned lazily).
        /// </summary>
        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary>
        /// Indicates whether there are no connected sinks.
        /// </summary>
        public bool NoConnectedSinks => sourceHelper.NoConnectedSinks;

        /// <summary>
        /// Checks whether this socket is explicitly owned by the provided owner instance.
        /// </summary>
        /// <param name="owner">Owner object to test.</param>
        /// <returns>True if this socket was created with that owner and the owner is still alive.</returns>
        public bool IsOwnedBy(object owner)
        {
            var ownerRef = this.ownerRef;
            if (ownerRef == null) return false;
            var target = ownerRef.Target;
            if (target == null) return false;
            return ReferenceEquals(target, owner);
        }

        /// <summary>
        /// Constructs a socket belonging to the specified hub.
        /// </summary>
        /// <param name="hub">Hub that manages this socket.</param>
        /// <param name="owner">
        /// Optional owner. If non-null and not the hub itself, enables auto-removal when the owner is GC-collected.
        /// </param>
        public Socket(Hub hub, object owner)
        {
            this.hub = hub;
            if (owner != null && !ReferenceEquals(owner, hub))
            {
                this.ownerRef = new WeakReference(owner);
            }
            else
            {
                this.ownerRef = null;
            }
        }

        /// <summary>
        /// Removes this socket from its hub under lock, if still present.
        /// After removal, the hub reference and owner reference are cleared.
        /// </summary>
        private void RemoveFromHub()
        {
            var hub = this.hub;
            if (hub == null) return;

            using (hub.socketsChangeLock.Lock())
            {
                var sockets = hub.sockets;

                // Find index first to avoid corrupting the array if this socket is not present anymore.
                int index = -1;
                for (int i = 0; i < sockets.Length; i++)
                {
                    if (ReferenceEquals(sockets[i], this))
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                {
                    var newSockets = new Socket[sockets.Length - 1];
                    if (index > 0) Array.Copy(sockets, 0, newSockets, 0, index);
                    if (index < sockets.Length - 1) Array.Copy(sockets, index + 1, newSockets, index, sockets.Length - index - 1);
                    hub.sockets = newSockets;
                }

                this.hub = null;
                ownerRef = null;
                sourceHelper = new SourceValueHelper();
            }
        }

        /// <summary>
        /// Checks whether the owner of this socket has been GC-collected and removes the socket if so.
        /// </summary>
        /// <returns>True if the socket was removed; otherwise false.</returns>
        private bool CheckRemovalByOwner()
        {
            // Fast-path: feature disabled
            var ownerRef = this.ownerRef;
            if (ownerRef == null) return false;

            // Remove lazily once the owner is collected
            if (!ownerRef.IsAlive)
            {
                RemoveFromHub();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Posts a message by reference to all other sockets of the hub.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">Message instance (passed by readonly reference).</param>
        public void Post<M>(in M message)
        {
            // No owner-check on the sender side; removal will be observed on receiver side.
            var sockets = hub.sockets;
            if (sockets.Length == 1) return;

            foreach (var socket in sockets)
            {
                if (!ReferenceEquals(socket, this)) socket.Forward(in message);
            }
        }

        /// <summary>
        /// Posts a message by value to all other sockets of the hub.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">Message instance.</param>
        public void Post<M>(M message)
        {
            var sockets = hub.sockets;
            if (sockets.Length == 1) return;

            foreach (var socket in sockets)
            {
                if (!ReferenceEquals(socket, this)) socket.Forward(message);
            }
        }

        /// <summary>
        /// Forwards a message by reference to connected sinks if still valid.
        /// Performs lazy owner removal check.
        /// </summary>
        private void Forward<M>(in M message)
        {
            if (CheckRemovalByOwner()) return;

            sourceHelper.Forward(in message);
        }

        /// <summary>
        /// Forwards a message by value to connected sinks if still valid.
        /// Performs lazy owner removal check.
        /// </summary>
        private void Forward<M>(M message)
        {
            if (CheckRemovalByOwner()) return;

            sourceHelper.Forward(message);
        }

        /// <summary>
        /// Asynchronously posts a message to all other sockets.
        /// </summary>
        /// <typeparam name="M">Type of the message.</typeparam>
        /// <param name="message">Message instance.</param>
        /// <returns>
        /// A task that completes when all receivers have finished their asynchronous forwarding.
        /// Returns a completed task immediately if there are no other sockets.
        /// </returns>
        public async Task PostAsync<M>(M message)
        {
            // No owner-check on the sender side; removal will be observed on receiver side.
            var sockets = hub.sockets;
            if (sockets.Length == 1) return;

            for (int i = 0; i < sockets.Length; i++)
            {
                if (!ReferenceEquals(sockets[i], this)) await sockets[i].ForwardAsync(message);
            }
        }

        /// <summary>
        /// Asynchronously forwards a message to connected sinks if still valid.
        /// Performs lazy owner removal check.
        /// </summary>
        private Task ForwardAsync<M>(M message)
        {
            if (CheckRemovalByOwner()) return Task.CompletedTask;

            return sourceHelper.ForwardAsync(message);
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        /// <summary>
        /// Disconnects all currently connected sinks.
        /// </summary>
        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        /// <summary>
        /// Returns a snapshot of currently connected sinks.
        /// </summary>
        /// <returns>Array of connected sinks (may be empty).</returns>
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        /// <summary>
        /// Disposes this socket and removes it from the hub.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (hub != null)
            {
                RemoveFromHub();
            }
        }

        /// <summary>
        /// Connects this socket's source side to a sink.
        /// </summary>
        /// <param name="sink">Sink to connect.</param>
        /// <param name="weakReference">
        /// True to hold a weak reference allowing the sink to be GC-collected automatically;
        /// false to hold a strong reference.
        /// </param>
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Connects this socket's source side to a bidirectional flow element.
        /// Returns the same element typed as <see cref="IMessageSource"/> to allow chaining.
        /// </summary>
        /// <param name="sink">Bidirectional flow element.</param>
        /// <param name="weakReference">
        /// True for weak reference semantics; false for strong reference.
        /// </param>
        /// <returns>The provided element, typed as <see cref="IMessageSource"/>.</returns>
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        /// <summary>
        /// Determines whether the specified sink is currently connected.
        /// </summary>
        /// <param name="sink">Sink to test.</param>
        /// <returns>True if connected; otherwise false.</returns>
        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}