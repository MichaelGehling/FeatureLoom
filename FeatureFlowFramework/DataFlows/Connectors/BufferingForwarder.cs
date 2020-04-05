using FeatureFlowFramework.Helper;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class BufferingForwarder<T> : IDataFlowSink, IDataFlowSource, IDataFlowConnection
    {
        private DataFlowSourceHelper sourceHelper;
        private CountingRingBuffer<T> buffer;
        private FeatureLock bufferLock = new FeatureLock();

        public BufferingForwarder(int bufferSize)
        {
            sourceHelper = new DataFlowSourceHelper(OnConnection);
            buffer = new CountingRingBuffer<T>(bufferSize);
        }

        private void OnConnection(IDataFlowSink sink)
        {
            using(bufferLock.ForReading())
            {
                var bufferedMessages = buffer.GetAvailableSince(0, out long missed);
                foreach(var msg in bufferedMessages)
                {
                    sink.Post(msg);
                }
            }
        }

        public int CountConnectedSinks => ((IDataFlowSource)sourceHelper).CountConnectedSinks;

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)sourceHelper).ConnectTo(sink);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sourceHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sourceHelper).GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            if(message is T msgT) using(bufferLock.ForWriting()) buffer.Add(msgT);
            sourceHelper.Forward(in message);
        }

        public async Task PostAsync<M>(M message)
        {
            if(message is T msgT) using(await bufferLock.ForWritingAsync()) buffer.Add(msgT);
            await sourceHelper.ForwardAsync(message);
        }
    }
}