using FeatureLoom.Extensions;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace FeatureLoom.Time
{

    public static class AppTimeExtensions
    {
        public static void Wait(this IAppTime appTime, TimeSpan minTimeout, TimeSpan maxTimeout)
        {
            appTime.Wait(minTimeout, maxTimeout, CancellationToken.None);
        }

        public static void Wait(this IAppTime appTime, TimeSpan timeout)
        {
            appTime.Wait(timeout, timeout.Multiply(2), CancellationToken.None);
        }

        public static void WaitPrecisely(this IAppTime appTime, TimeSpan timeout)
        {
            appTime.Wait(timeout, timeout, CancellationToken.None);
        }

        public static bool Wait(this IAppTime appTime, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return appTime.Wait(timeout, timeout.Multiply(2), cancellationToken);
        }

        public static bool WaitPrecisely(this IAppTime appTime, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return appTime.Wait(timeout, timeout, cancellationToken);
        }

        public static Task WaitAsync(this IAppTime appTime, TimeSpan minTimeout, TimeSpan maxTimeout)
        {
            return appTime.WaitAsync(minTimeout, maxTimeout, CancellationToken.None);
        }

        public static Task WaitAsync(this IAppTime appTime, TimeSpan timeout)
        {
            return appTime.WaitAsync(timeout, timeout.Multiply(2), CancellationToken.None);
        }

        public static Task WaitPreciselyAsync(this IAppTime appTime, TimeSpan timeout)
        {
            return appTime.WaitAsync(timeout, timeout, CancellationToken.None);
        }

        public static Task<bool> WaitAsync(this IAppTime appTime, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return appTime.WaitAsync(timeout, timeout.Multiply(2), cancellationToken);
        }

        public static Task<bool> WaitPreciselyAsync(this IAppTime appTime, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return appTime.WaitAsync(timeout, timeout, cancellationToken);
        }

        public static DateTime LocalNow(this IAppTime appTime)
        {
            return appTime.Now.ToLocalTime();
        }

        public static DateTime LocalCoarseNow(this IAppTime appTime)
        {
            return appTime.CoarseNow.ToLocalTime();
        }               
    }

    public class AppTimeService : IAppTime
    {
        private Stopwatch stopWatch = new Stopwatch();
        private DateTime coarseTimeBase;
        private int coarseMillisecondCountBase;
        private TimeSpan lowerSleepLimit = 15.Milliseconds();
        private TimeSpan lowerAsyncSleepLimit = 15.Milliseconds();
        private static readonly DateTime unixTimeBase = new DateTime(1970, 1, 1);

        public AppTimeService()
        {
            stopWatch.Start();
            ResetCoarseNow(DateTime.UtcNow);
        }

        public DateTime UnixTimeBase => unixTimeBase;

        public TimeSpan Elapsed => stopWatch.Elapsed;

        public TimeKeeper TimeKeeper => new TimeKeeper(Elapsed);

        public TimeSpan CoarsePrecision => 20.Milliseconds();

        /// <summary>
        /// Returns the current UTC time
        /// </summary>
        public DateTime Now
        {
            get
            {
                var now = DateTime.UtcNow;
                ResetCoarseNow(now);
                return now;
            }
        }

        /// <summary>
        /// A very quick and cheap way (~5-8% cost of DateTime.UtcNow) to get the current UTC time, but it may deviate between -16 to +16 milliseconds from actual UTC time (roughly in a gaussian normal distribution).
        /// Note: Every second, the coarse time will be reset by calling DateTime.UtcNow to avoid drift. Calling AppTime.Now will also reset the CoarseTime to the actual time.
        /// </summary>
        public DateTime CoarseNow
        {
            get
            {
                var newCoarseMillisecondCount = Environment.TickCount;
                var diff = newCoarseMillisecondCount - coarseMillisecondCountBase;

                if (diff < 0 || diff > 1000) return ResetCoarseNow(DateTime.UtcNow);    // Reset after one second or to handle wrap-around
                if (diff <= 20) return coarseTimeBase;     // Difference is in margin of error, so return previous value                

                return coarseTimeBase + diff.Milliseconds();
            }
        }

        private DateTime ResetCoarseNow(DateTime now)
        {
            coarseTimeBase = now;
            coarseMillisecondCountBase = Environment.TickCount;
            return coarseTimeBase;
        }

        public bool Wait(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken)
        {
            if (minTimeout.Ticks <= 1 || cancellationToken.IsCancellationRequested) return false;

            var timer = TimeKeeper;

            if (maxTimeout >= lowerSleepLimit) cancellationToken.WaitHandle.WaitOne(minTimeout.ClampHigh(maxTimeout - lowerSleepLimit).ClampLow(1.Milliseconds()));            

            if (timer.Elapsed > minTimeout || cancellationToken.IsCancellationRequested) return !cancellationToken.IsCancellationRequested;

            var lowPrioLimit = maxTimeout - 0.1.Milliseconds();
            if (timer.LastElapsed < lowPrioLimit)
            {
                var oldPriority = Thread.CurrentThread.Priority;
                try
                {
                    Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                    while (timer.Elapsed < lowPrioLimit)
                    {
                        if (timer.LastElapsed >= minTimeout) return true;
                        if (cancellationToken.IsCancellationRequested) return false;
                        Thread.Sleep(0);
                    }
                }
                finally
                {
                    Thread.CurrentThread.Priority = oldPriority;
                }                
            }

            while (timer.Elapsed < minTimeout && !cancellationToken.IsCancellationRequested) Thread.Sleep(0);
            return !cancellationToken.IsCancellationRequested;
        }


        
        public async Task<bool> WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (minTimeout.Ticks <= 1) return true;            

            var timer = TimeKeeper;

            if (maxTimeout >= lowerAsyncSleepLimit)
            {                
                // if we may wait longer than lowerAsyncSleepLimit (the precision limit of Task.Delay),
                // we use Task.Delay to wait asynchronously and can return directly, because it is guaranteed that
                // we wait at least minTimeout
                try
                {
                    await Task.Delay(minTimeout.ClampHigh(maxTimeout-lowerAsyncSleepLimit).ClampLow(1.Milliseconds()), cancellationToken).ConfigureAwait(false);                    
                    
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }

            // Fine-grained waiting loop using Task.Yield: yields the current thread's execution to allow other tasks to run                                
            var yieldLimit = maxTimeout - 0.1.Milliseconds();
            while (timer.Elapsed < yieldLimit)
            {
                if (timer.LastElapsed >= minTimeout) return true;
                if (cancellationToken.IsCancellationRequested) return false;
                await Task.Yield();
            }

            // More fine-grained waiting loop using Thread.Sleep(0): will not yield the thread to other tasks,
            // but yields computation time to other threads (blocking the own thread).
            while (timer.Elapsed < minTimeout && !cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(0);
            }

            return !cancellationToken.IsCancellationRequested;
        }
    }
}