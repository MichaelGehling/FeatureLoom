using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Collections;
using FeatureFlowFramework.Helpers.Synchronization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class BufferingForwarder : BufferingForwarder<object>
    {
        public BufferingForwarder(int bufferSize) : base(bufferSize)
        {
        }
    }

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

        public T[] GetAllBufferEntries()
        {
            using(bufferLock.ForReading())
            {
                return buffer.GetAvailableSince(0, out _);
            }
        }

        public void AddRangeToBuffer(IEnumerable<T> messages)
        {
            using(bufferLock.ForWriting())
            {
                foreach(var msg in messages) buffer.Add(msg);
            }
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            ((IDataFlowSource)sourceHelper).ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return ((IDataFlowSource)sourceHelper).ConnectTo(sink, weakReference);
        }
    }
}