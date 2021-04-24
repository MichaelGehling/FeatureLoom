using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class AsyncForwarder : IDataFlowConnection
    {
        protected SourceValueHelper sourceHelper;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

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

        public void Post<M>(in M message)
        {            
            _ = sourceHelper.ForwardAsync(message);
        }

        public Task PostAsync<M>(M message)
        {
            return sourceHelper.ForwardAsync(message);
        }
    }
}