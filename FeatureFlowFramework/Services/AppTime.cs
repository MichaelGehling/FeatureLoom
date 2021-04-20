using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Time;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Services
{
    public static class AppTime
    {
        private static Stopwatch stopWatch = new Stopwatch();
        private static DateTime coarseTimeBase;
        private static int coarseMillisecondCountBase;
        private static int lastCoarseMillisecondCount;

        static AppTime()
        {
            stopWatch.Start();
            ResetCoarseNow();
        }

        public static DateTime Now
        {
            get
            {
                var now = DateTime.UtcNow;
                ResetCoarseNow(now);
                return now;
            }
        }

        public static TimeSpan Elapsed => stopWatch.Elapsed;

        public static TimeKeeper TimeKeeper => new TimeKeeper(Elapsed);

        public static DateTime CoarseNow        
        {
            get
            {
                var newCoarseMillisecondCount = Environment.TickCount;
                if (lastCoarseMillisecondCount - coarseMillisecondCountBase > 1000 ||
                    newCoarseMillisecondCount < lastCoarseMillisecondCount)
                {
                    ResetCoarseNow();
                }
                else lastCoarseMillisecondCount = newCoarseMillisecondCount;

                return coarseTimeBase + (lastCoarseMillisecondCount - coarseMillisecondCountBase).Milliseconds();
            }
        }

        private static void ResetCoarseNow(DateTime now)
        {
            coarseTimeBase = now;
            coarseMillisecondCountBase = Environment.TickCount;
            lastCoarseMillisecondCount = coarseMillisecondCountBase;            
        }

        private static void ResetCoarseNow()
        {
            var now = DateTime.UtcNow;
            ResetCoarseNow(now);
        }

        public static void Wait(TimeSpan timeout)
        {            
            if (timeout.Ticks <= 1) return;
            else timeout = new TimeSpan(timeout.Ticks - 1);

            TimeKeeper timer = TimeKeeper;
            if (timeout > 16.Milliseconds()) Thread.Sleep(timeout - 16.Milliseconds());
            while (timer.Elapsed < timeout) Thread.Sleep(0);
        }

        public static void Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (timeout == TimeSpan.Zero || cancellationToken.IsCancellationRequested) return;
            else timeout = new TimeSpan(timeout.Ticks - 1);

            TimeKeeper timer = TimeKeeper;
            if (timeout > 16.Milliseconds()) cancellationToken.WaitHandle.WaitOne(timeout - 16.Milliseconds());
            while (timer.Elapsed < timeout && !cancellationToken.IsCancellationRequested) Thread.Sleep(0);
        }

        public static async Task WaitAsync(TimeSpan timeout)
        {
            if (timeout.Ticks <= 5) Wait(timeout);
            else
            {
                TimeKeeper timer = TimeKeeper;
                if (timeout > 16.Milliseconds()) await Task.Delay(timeout - 16.Milliseconds());
                while (timer.Elapsed < timeout - 0.01.Milliseconds()) await Task.Yield();
                while (timer.Elapsed.Ticks < timeout.Ticks - 1000) await Task.Delay(0);
                while (timer.Elapsed.Ticks < timeout.Ticks - 50) Thread.Sleep(0);
            }
        }

        public static async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (timeout.Ticks <= 5) Wait(timeout, cancellationToken);
            else
            {
                TimeKeeper timer = TimeKeeper;
                if (timeout > 16.Milliseconds()) await Task.Delay(timeout - 16.Milliseconds(), cancellationToken);
                while (timer.Elapsed < timeout - 0.01.Milliseconds() && !cancellationToken.IsCancellationRequested) await Task.Yield();
                while (timer.Elapsed.Ticks < timeout.Ticks - 1000 && !cancellationToken.IsCancellationRequested) await Task.Delay(0);
                while (timer.Elapsed.Ticks < timeout.Ticks - 50 && !cancellationToken.IsCancellationRequested) Thread.Sleep(0);
            }
        }

    }
}