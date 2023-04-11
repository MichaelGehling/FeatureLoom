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
        void Wait(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken);
        Task WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken);
    }
}