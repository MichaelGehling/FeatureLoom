using FeatureLoom.Collections;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{

    public class MessageLog<T> : IMessageSink<T>, ILogBuffer<T>
    {
        CircularLogBuffer<T> buffer;

        public MessageLog(int bufferSize)
        {
            buffer = new CircularLogBuffer<T>(bufferSize);
        }

        public int CurrentSize => ((ILogBuffer<T>)buffer).CurrentSize;

        public long LatestId => ((ILogBuffer<T>)buffer).LatestId;

        public int MaxSize => ((ILogBuffer<T>)buffer).MaxSize;

        public long OldestAvailableId => ((ILogBuffer<T>)buffer).OldestAvailableId;

        public IAsyncWaitHandle WaitHandle => ((ILogBuffer<T>)buffer).WaitHandle;

        public Type ConsumedMessageType => typeof(T);

        public long Add(T item)
        {
            return ((ILogBuffer<T>)buffer).Add(item);
        }

        public long AddRange<IEnum>(IEnum items) where IEnum : IEnumerable<T>
        {
            return ((ILogBuffer<T>)buffer).AddRange(items);
        }

        public T[] GetAllAvailable(long firstRequestedId, int maxItems, out long firstProvidedId, out long lastProvidedId)
        {
            return ((ILogBuffer<T>)buffer).GetAllAvailable(firstRequestedId, maxItems, out firstProvidedId, out lastProvidedId);
        }

        public T[] GetAllAvailable(long firstRequestedId, out long firstProvidedId, out long lastProvidedId)
        {
            return ((ILogBuffer<T>)buffer).GetAllAvailable(firstRequestedId, out firstProvidedId, out lastProvidedId);
        }

        public T GetLatest()
        {
            return ((ILogBuffer<T>)buffer).GetLatest();
        }

        public void Post<M>(in M message)
        {
            if (message is T typedMessage) buffer.Add(typedMessage);
        }

        public void Post<M>(M message)
        {
            if (message is T typedMessage) buffer.Add(typedMessage);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T typedMessage) buffer.Add(typedMessage);
            return Task.CompletedTask;
        }

        public void Reset()
        {
            ((ILogBuffer<T>)buffer).Reset();
        }

        public bool TryGetFromId(long number, out T result)
        {
            return ((ILogBuffer<T>)buffer).TryGetFromId(number, out result);
        }
    }
}
