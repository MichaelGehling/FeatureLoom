using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary>
    ///     Just forwards messages without processing it. It is thread-safe. Can be used as a
    ///     hub-element in a dataFlow route.
    /// </summary>
    public class Forwarder : IDataFlowConnection
    {
        protected DataFlowSourceHelper sendingHelper = new DataFlowSourceHelper();

        public int CountConnectedSinks => sendingHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sendingHelper.GetConnectedSinks();
        }

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink);
            return sink;
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

        public virtual async Task PostAsync<M>(M message)
        {
            await sendingHelper.ForwardAsync(message);
        }
    }
}
