using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary> Helps implementing IMessageSource and should be used wherever IMessageSource is
    /// implemented. It is thread safe, but doesn't need any lock while sending, so it will never
    /// block if used concurrently. Anyway, changing the list of connected sinks
    /// (connecting/disconnecting) uses a lock and blocks a short time. Sinks can optionally be stored as weak references
    /// and will then not be kept from being garbage-collected, so it is not necessary to disconnect
    /// sinks that are not needed any more. <summary>
    public sealed class SourceHelper : IMessageSource
    {
        private SourceValueHelper sourceHelper;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IMessageSink[] GetConnectedSinks()
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