using System;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    /// <summary> Helps implementing IDataFlowSource and should be used wherever IDataFlowSource is
    /// implemented. It is thread safe, but doesn't need any lock while sending, so it will never
    /// block if used concurrently. Anyway, changing the list of connected sinks
    /// (connecting/disconnecting) uses a lock and blocks a short time. Sinks can optionally be stored as weak references
    /// and will then not be kept from being garbage-collected, so it is not necessary to disconnect
    /// sinks that are not needed any more. <summary>
    public class TypedSourceHelper<T> : IDataFlowSource<T>
    {
        private TypedSourceValueHelper<T> sourceHelper;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public Type SentMessageType => typeof(T);

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void Forward<M>(in M message)
        {
            sourceHelper.Forward<M>(in message);
        }

        public Task ForwardAsync<M>(M message)
        {
            return sourceHelper.ForwardAsync<M>(message);
        }
    }
}