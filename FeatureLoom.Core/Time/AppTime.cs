using FeatureLoom.Helpers;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Time
{
    public static class AppTime
    {
        private static Stopwatch stopWatch = new Stopwatch();
        private static DateTime coarseTimeBase;
        private static int coarseMillisecondCountBase;
        private static TimeSpan lowerSleepLimit = 18.Milliseconds();

        static AppTime()
        {
            stopWatch.Start();
            ResetCoarseNow(DateTime.UtcNow);
        }

        public static TimeSpan Elapsed => stopWatch.Elapsed;

        public static TimeKeeper TimeKeeper => new TimeKeeper(Elapsed);

        public static TimeSpan CoarsePrecision => 20.Milliseconds();

        /// <summary>
        /// Returns the current UTC time
        /// </summary>
        public static DateTime Now
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
        /// Note: Calling AppTime.Now will reset the CoarseTime to the actual time.
        /// </summary>
        public static DateTime CoarseNow
        {
            get
            {
                var newCoarseMillisecondCount = Environment.TickCount;
                if (newCoarseMillisecondCount - coarseMillisecondCountBase <= 20) return coarseTimeBase;

                if (coarseMillisecondCountBase > newCoarseMillisecondCount ||
                    newCoarseMillisecondCount - coarseMillisecondCountBase > 1000 )
                 {
                     return ResetCoarseNow(DateTime.UtcNow);
                 }

                return coarseTimeBase + (newCoarseMillisecondCount - coarseMillisecondCountBase).Milliseconds();
            }
        }

        private static DateTime ResetCoarseNow(DateTime now)
        {
            coarseTimeBase = now;
            coarseMillisecondCountBase = Environment.TickCount;
            return coarseTimeBase;
        }

        public static void Wait(TimeSpan minTimeout, TimeSpan maxTimeout)
        {
            Wait(minTimeout, maxTimeout, CancellationToken.None);
        }

        public static void Wait(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken)
        {
            if (minTimeout.Ticks <= 1 || cancellationToken.IsCancellationRequested) return;
            else minTimeout = new TimeSpan(minTimeout.Ticks - 1);

            var timer = AppTime.TimeKeeper;

            if (maxTimeout > lowerSleepLimit) cancellationToken.WaitHandle.WaitOne((maxTimeout+minTimeout).Divide(2));

            if (timer.Elapsed > minTimeout || cancellationToken.IsCancellationRequested) return;

            if (timer.LastElapsed < minTimeout - 0.1.Milliseconds())
            {
                var oldPriority = Thread.CurrentThread.Priority;
                try
                {
                    Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                    while (timer.Elapsed < minTimeout - 0.1.Milliseconds() && !cancellationToken.IsCancellationRequested) Thread.Sleep(0);
                }
                finally
                {
                    Thread.CurrentThread.Priority = oldPriority;
                }                
            }

            while (timer.Elapsed < minTimeout && !cancellationToken.IsCancellationRequested) Thread.Sleep(0);
        }

        public static Task WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout)
        {            
            return WaitAsync(minTimeout, maxTimeout, CancellationToken.None);
        }

        public static async Task WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken)
        {            
            if (minTimeout.Ticks <= 5) Wait(minTimeout, maxTimeout);
            else
            {
                var timer = AppTime.TimeKeeper;
                if (maxTimeout > 18.Milliseconds())
                {
                    await Task.Delay(minTimeout, cancellationToken);
                    _ = timer.Elapsed;
                }
                while (timer.LastElapsed < minTimeout - 0.01.Milliseconds() && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Yield();
                    _ = timer.Elapsed;
                }
                while (timer.LastElapsed.Ticks < minTimeout.Ticks - 1000 && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(0);
                    _ = timer.Elapsed;
                }
                while (timer.LastElapsed.Ticks < minTimeout.Ticks - 50 && !cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(0);
                    _ = timer.Elapsed;
                }
            }
        }
    }
}