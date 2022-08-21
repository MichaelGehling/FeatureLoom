using FeatureLoom.Collections;
using FeatureLoom.Synchronization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public class BufferingForwarder : BufferingForwarder<object>
    {
        public BufferingForwarder(int bufferSize) : base(bufferSize)
        {
        }
    }

    public class BufferingForwarder<T> : IMessageFlowConnection<T>
    {
        private SourceValueHelper sourceHelper;
        private CircularLogBuffer<T> buffer;
        private FeatureLock bufferLock = new FeatureLock();

        public Type SentMessageType => typeof(T);
        public Type ConsumedMessageType => typeof(T);

        public BufferingForwarder(int bufferSize)
        {
            buffer = new CircularLogBuffer<T>(bufferSize, false);
        }

        private void OnConnection(IMessageSink sink)
        {
            var bufferedMessages = buffer.GetAllAvailable(0, out long missed);
            foreach (var msg in bufferedMessages)
            {
                sink.Post(msg);
            }
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

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

        public void Post<M>(in M message)
        {
            if (message is T msgT) using (bufferLock.Lock()) buffer.Add(msgT);
            sourceHelper.Forward(in message);
        }

        public void Post<M>(M message)
        {
            if (message is T msgT) using (bufferLock.Lock()) buffer.Add(msgT);
            sourceHelper.Forward(message);
        }

        public async Task PostAsync<M>(M message)
        {
            var task = sourceHelper.ForwardAsync(message);
            if (message is T msgT) using (await bufferLock.LockAsync()) buffer.Add(msgT);
            await task;
        }

        public T[] GetAllBufferEntries()
        {
            using (bufferLock.LockReadOnly())
            {
                return buffer.GetAllAvailable(0, out _);
            }
        }

        public void AddRangeToBuffer<TEnum>(TEnum messages) where TEnum : IEnumerable<T>
        {
            using (bufferLock.Lock())
            {
                foreach (var msg in messages) buffer.Add(msg);
            }
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            using (bufferLock.LockReadOnly())
            {
                OnConnection(sink);
                sourceHelper.ConnectTo(sink, weakReference);
            }
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            using (bufferLock.LockReadOnly())
            {
                OnConnection(sink);
                return sourceHelper.ConnectTo(sink, weakReference);
            }
        }
    }
}