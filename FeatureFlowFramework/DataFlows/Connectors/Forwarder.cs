using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    /// <summary>
    ///     Just forwards messages without processing it. It is thread-safe. Can be used as a
    ///     hub-element in a dataFlow route.
    /// </summary>
    public class Forwarder : IDataFlowSource, IDataFlowConnection
    {
        protected SourceValueHelper sourceHelper;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public virtual void Post<M>(in M message)
        {
            sourceHelper.Forward(message);
        }

        public virtual Task PostAsync<M>(M message)
        {
            return sourceHelper.ForwardAsync(message);            
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

    public class Forwarder<T>: Forwarder, IDataFlowConnection<T>
    {
        public override void Post<M>(in M message)
        {
            if (message is T) sourceHelper.Forward(message);
        }

        public override Task PostAsync<M>(M message)
        {
            if(message is T) return sourceHelper.ForwardAsync(message);
            else return Task.CompletedTask;
        }
    }
}