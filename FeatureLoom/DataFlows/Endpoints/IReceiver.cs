using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    public interface IReceiver<T> : IDataFlowSink<T>
    {
        bool IsEmpty { get; }
        bool IsFull { get; }
        int Count { get; }
        IAsyncWaitHandle WaitHandle { get; }

        bool TryReceive(out T message);

        Task<AsyncOut<bool, T>> TryReceiveAsync(TimeSpan timeout = default);

        T[] ReceiveAll();

        bool TryPeek(out T nextItem);

        T[] PeekAll();
    }
}