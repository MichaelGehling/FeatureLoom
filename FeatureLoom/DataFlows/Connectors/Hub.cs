using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    /// <summary>
    ///     A Hub allows to create multiple sockets. Every message send over a socket will be
    ///     forwarded to all other sockets of this hub, but not looped back to the sending socket.
    /// </summary>
    public class Hub : IDataFlow
    {
        private List<Socket> sockets = new List<Socket>();

        /// <summary>
        ///     Creates a new Socket. Every message send over this socket will be forwarded to all
        ///     other sockets of this hub, but not looped back to the sending socket.
        /// </summary>
        /// <param name="owner">
        ///     If given, socket will be disposed automatically after owner was garbage-collected.
        /// </param>
        /// <returns> The new Socket </returns>
        public Socket CreateSocket(object owner = null)
        {
            var sockets = this.sockets;
            List<Socket> newSocketList = new List<Socket>(sockets);
            var newSocket = new Socket(this, owner ?? this);
            newSocketList.Add(newSocket);
            this.sockets = newSocketList;
            return newSocket;
        }

        public void RemoveSocket(Socket socketToRemove)
        {
            socketToRemove.Dispose();
        }

        public class Socket : IDataFlowSink, IDataFlowSource, IDisposable
        {
            private Hub hub;
            private WeakReference ownerRef;
            private SourceValueHelper sourceHelper;

            public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

            public Socket(Hub hub, object owner)
            {
                this.hub = hub;
                this.ownerRef = new WeakReference(owner);
            }

            private bool RemoveFromHub()
            {
                if (hub == null) return true;
                var sockets = hub.sockets;
                List<Socket> newSocketList = new List<Socket>(sockets.Count - 1);
                bool removed = false;
                foreach (var socket in sockets)
                {
                    if (socket != this) newSocketList.Add(socket);
                    else removed = true;
                }
                hub.sockets = newSocketList;
                hub = null;
                ownerRef = null;
                sourceHelper = new SourceValueHelper();
                return removed;
            }

            private bool CheckRemovalByOwner()
            {
                if (!ownerRef.IsAlive) return RemoveFromHub();
                else return false;
            }

            public void Post<M>(in M message)
            {
                if (CheckRemovalByOwner()) return;

                var sockets = hub.sockets;
                if (sockets.Count == 1) return;

                foreach (var socket in sockets)
                {
                    if (socket != this) socket.Forward(message);
                }
            }

            private void Forward<M>(in M message)
            {
                if (CheckRemovalByOwner()) return;

                sourceHelper.Forward(message);
            }

            public Task PostAsync<M>(M message)
            {
                if (CheckRemovalByOwner()) return Task.CompletedTask;

                var sockets = hub.sockets;
                if (sockets.Count == 1) return Task.CompletedTask;

                Task[] tasks = new Task[sockets.Count];
                for (int i = 0; i < sockets.Count; i++)
                {
                    if (sockets[i] != this) tasks[i] = sockets[i].ForwardAsync(message);
                    else tasks[i] = Task.CompletedTask;
                }
                return Task.WhenAll(tasks);
            }

            private Task ForwardAsync<M>(M message)
            {
                if (CheckRemovalByOwner()) return Task.CompletedTask;

                return sourceHelper.ForwardAsync(message);
            }

            public void DisconnectFrom(IDataFlowSink sink)
            {
                sourceHelper.DisconnectFrom(sink);
            }

            public void DisconnectAll()
            {
                sourceHelper.DisconnectAll();
            }

            public IDataFlowSink[] GetConnectedSinks()
            {
                return sourceHelper.GetConnectedSinks();
            }

            public void Dispose()
            {
                if (hub != null)
                {
                    RemoveFromHub();
                }
            }

            public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
            {
                sourceHelper.ConnectTo(sink, weakReference);
            }

            public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
            {
                return sourceHelper.ConnectTo(sink, weakReference);
            }
        }
    }
}