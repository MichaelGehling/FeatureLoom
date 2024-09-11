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

        bool TryReceive(out T message);

        bool TryPeek(out T nextItem);

        int ReceiveMany(ref T[] items);

        int PeekMany(ref T[] items);
    }
}