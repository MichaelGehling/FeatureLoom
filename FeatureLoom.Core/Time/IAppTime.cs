using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Time
{
    public interface IAppTime
    {
        DateTime UnixTimeBase { get; }
        TimeSpan Elapsed { get; }
        TimeKeeper TimeKeeper { get; }
        TimeSpan CoarsePrecision { get; }
        DateTime Now { get; }
        DateTime CoarseNow { get; }
        void Wait(TimeSpan minTimeout, TimeSpan maxTimeout);
        void Wait(TimeSpan timeout);
        void Wait(TimeSpan timeout, CancellationToken cancellationToken);
        void Wait(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken);
        Task WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout);
        Task WaitAsync(TimeSpan timeout);
        Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
        Task WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken);

    }
}