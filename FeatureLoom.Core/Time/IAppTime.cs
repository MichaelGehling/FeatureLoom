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
        bool Wait(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken);
        Task<bool> WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken);
    }
}