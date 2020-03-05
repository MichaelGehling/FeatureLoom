using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class BufferingForwarder<T> : IDataFlowSink, IDataFlowSource, IDataFlowConnection
    {
        DataFlowSourceHelper sourceHelper;
        CountingRingBuffer<T> buffer;

        public BufferingForwarder(int bufferSize)
        {
            sourceHelper = new DataFlowSourceHelper(OnConnection);
            buffer = new CountingRingBuffer<T>(bufferSize);
        }

        void OnConnection(IDataFlowSink sink)
        {
            
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
            if(message is T msgT) buffer.Add(msgT);
            sourceHelper.Forward(in message);
        }

        public Task PostAsync<M>(M message)
        {
            if(message is T msgT) buffer.Add(msgT);
            return sourceHelper.ForwardAsync(message);
        }
    }
}
