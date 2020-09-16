using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary>
    ///     Just forwards messages without processing it. It is thread-safe. Can be used as a
    ///     hub-element in a dataFlow route.
    /// </summary>
    public class Forwarder : IDataFlowSource, IDataFlowConnection
    {
        protected DataFlowSourceHelper sendingHelper = new DataFlowSourceHelper();

        public int CountConnectedSinks => sendingHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sendingHelper.GetConnectedSinks();
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sendingHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).DisconnectFrom(sink);
        }

        public virtual void Post<M>(in M message)
        {
            sendingHelper.Forward(message);
        }

        public virtual Task PostAsync<M>(M message)
        {
            return sendingHelper.ForwardAsync(message);            
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return ((IDataFlowSource)sendingHelper).ConnectTo(sink, weakReference);
        }
    }

    public class Forwarder<T>: Forwarder, IDataFlowConnection<T>
    {
        public override void Post<M>(in M message)
        {
            if (message is T) sendingHelper.Forward(message);
        }

        public override Task PostAsync<M>(M message)
        {
            if(message is T) return sendingHelper.ForwardAsync(message);
            else return Task.CompletedTask;
        }
    }
}