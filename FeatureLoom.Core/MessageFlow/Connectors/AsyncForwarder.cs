using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Messages are forwarded async without awaiting, so the control is returned immediatly to the sender if 
    /// further executed code is async.
    /// Be aware that the order of message might change, due to parallel processing.
    /// If you need to keep the order, please use QueueForwarder.
    /// </summary>
    public sealed class AsyncForwarder : IMessageFlowConnection
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

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }

        public void Post<M>(in M message)
        {
            _ = sourceHelper.ForwardAsync(message);
        }

        public void Post<M>(M message)
        {
            _ = sourceHelper.ForwardAsync(message);
        }

        public Task PostAsync<M>(M message)
        {
            _ = sourceHelper.ForwardAsync(message);
            return Task.CompletedTask;
        }
    }
}