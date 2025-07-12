using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public interface IReceiver<T> : IMessageSink<T>
    {
        bool IsEmpty { get; }
        bool IsFull { get; }
        int Count { get; }
        IAsyncWaitHandle WaitHandle { get; }
        IMessageSource<bool> Notifier { get; }

        bool TryReceive(out T message);

        bool TryPeek(out T nextItem);

        ArraySegment<T> ReceiveMany(int maxItems = 0, SlicedBuffer<T> buffer = null);

        ArraySegment<T> PeekMany(int maxItems = 0, SlicedBuffer<T> buffer = null);
    }
}