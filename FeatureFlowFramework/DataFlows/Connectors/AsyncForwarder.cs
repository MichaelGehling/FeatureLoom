using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class AsyncForwarder : IDataFlowConnection
    {
        protected DataFlowSourceHelper sendingHelper = new DataFlowSourceHelper();

        public int CountConnectedSinks => ((IDataFlowSource)sendingHelper).CountConnectedSinks;

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return ((IDataFlowSource)sendingHelper).ConnectTo(sink, weakReference);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sendingHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sendingHelper).GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            sendingHelper.ForwardAsync(message).Wait();
        }

        public Task PostAsync<M>(M message)
        {
            return sendingHelper.ForwardAsync(message);
        }
    }
}