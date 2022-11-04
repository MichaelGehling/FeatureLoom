using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Collections
{
    public interface ILogBuffer<T> : IWriteLogBuffer<T>, IReadLogBuffer<T>
    {

    }

    public interface IWriteLogBuffer<T>
    {
        long Add(T item);
        long AddRange<IEnum>(IEnum items) where IEnum : IEnumerable<T>;
        void Reset();
    }

    public interface IReadLogBuffer<T>
    {
        int CurrentSize { get; }
        long LatestId { get; }
        int MaxSize { get; }
        long OldestAvailableId { get; }
        IAsyncWaitHandle WaitHandle { get; }

        T[] GetAllAvailable(long firstRequestedId, int maxItems, out long firstProvidedId, out long lastProvidedId);
        T[] GetAllAvailable(long firstRequestedId, out long firstProvidedId, out long lastProvidedId);
        T GetLatest();
        bool TryGetFromId(long number, out T result);        

        Task<AsyncOut<T[], (long firstProvidedId, long lastProvidedId)>> GetAllAvailableAsync(long firstRequestedId, int maxItems, CancellationToken ct = default);
        Task<AsyncOut<T[], (long firstProvidedId, long lastProvidedId)>> GetAllAvailableAsync(long firstRequestedId, CancellationToken ct = default);
        Task WaitForIdAsync(long number, CancellationToken ct = default);
    }
}