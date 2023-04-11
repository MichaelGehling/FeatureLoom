using FeatureLoom.DependencyInversion;
using FeatureLoom.Helpers;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Time
{
    public static class AppTime
    {
        static IAppTime GetService => Service<IAppTime>.Get();

        public static DateTime UnixTimeBase => GetService.UnixTimeBase;

        public static TimeSpan Elapsed => GetService.Elapsed;

        public static TimeKeeper TimeKeeper => GetService.TimeKeeper;

        public static TimeSpan CoarsePrecision => GetService.CoarsePrecision;

        public static DateTime Now => GetService.Now;

        public static DateTime CoarseNow => GetService.CoarseNow;

        public static void Wait(TimeSpan minTimeout, TimeSpan maxTimeout)
        {
            GetService.Wait(minTimeout, maxTimeout);
        }

        public static void Wait(TimeSpan timeout)
        {
            GetService.Wait(timeout);
        }

        public static void Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            GetService.Wait(timeout, cancellationToken);
        }

        public static void Wait(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken)
        {
            GetService.Wait(minTimeout, maxTimeout, cancellationToken);
        }

        public static Task WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout)
        {
            return GetService.WaitAsync(minTimeout, maxTimeout);
        }

        public static Task WaitAsync(TimeSpan timeout)
        {
            return GetService.WaitAsync(timeout);
        }

        public static Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return GetService.WaitAsync(timeout, cancellationToken);
        }

        public static Task WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken)
        {
            return GetService.WaitAsync(minTimeout, maxTimeout, cancellationToken);
        }
    }
}