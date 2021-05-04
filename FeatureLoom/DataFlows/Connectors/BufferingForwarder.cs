using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Collections;
using FeatureLoom.Helpers.Synchronization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    public class BufferingForwarder : BufferingForwarder<object>
    {
        public BufferingForwarder(int bufferSize) : base(bufferSize)
        {
        }
    }

    public class BufferingForwarder<T> : IDataFlowConnection<T>
    {
        private SourceValueHelper sourceHelper;
        private CountingRingBuffer<T> buffer;
        private FeatureLock bufferLock = new FeatureLock();

        public BufferingForwarder(int bufferSize)
        {
            buffer = new CountingRingBuffer<T>(bufferSize);
        }

        private void OnConnection(IDataFlowSink sink)
        {
            using(bufferLock.LockReadOnly())
            {
                var bufferedMessages = buffer.GetAvailableSince(0, out long missed);
                foreach(var msg in bufferedMessages)
                {
                    sink.Post(msg);
                }
            }
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

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
            if(message is T msgT) using(bufferLock.Lock()) buffer.Add(msgT);
            sourceHelper.Forward(in message);
        }

        public async Task PostAsync<M>(M message)
        {
            var task = sourceHelper.ForwardAsync(message);
            if (message is T msgT) using(await bufferLock.LockAsync()) buffer.Add(msgT);
            await task;
        }

        public T[] GetAllBufferEntries()
        {
            using(bufferLock.LockReadOnly())
            {
                return buffer.GetAvailableSince(0, out _);
            }
        }

        public void AddRangeToBuffer(IEnumerable<T> messages)
        {
            using(bufferLock.Lock())
            {
                foreach(var msg in messages) buffer.Add(msg);
            }
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            OnConnection(sink);
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }
    }
}