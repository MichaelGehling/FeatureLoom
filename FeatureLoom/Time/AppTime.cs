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
        private static int lastCoarseMillisecondCount;

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
        /// A very quick and cheap way to get the current UTC time, but it is between 0 - 20 milliseconds (usually 2-18 ms) behind the actual time.
        /// </summary>
        public static DateTime CoarseNow
        {
            get
            {
                var newCoarseMillisecondCount = Environment.TickCount;
                if (coarseMillisecondCountBase > lastCoarseMillisecondCount ||
                    lastCoarseMillisecondCount - coarseMillisecondCountBase > 1000 ||
                    newCoarseMillisecondCount < lastCoarseMillisecondCount)
                 {
                     return ResetCoarseNow(DateTime.UtcNow);
                 }
                 else lastCoarseMillisecondCount = newCoarseMillisecondCount;
                 
                return coarseTimeBase + (lastCoarseMillisecondCount - coarseMillisecondCountBase).Milliseconds();
            }
        }

        private static DateTime ResetCoarseNow(DateTime now)
        {
            coarseTimeBase = now - 2.Milliseconds(); // Shift by 2 milliseconds so that the coarse time will (nearly) always be behind the normal UTC time.
            coarseMillisecondCountBase = Environment.TickCount;
            lastCoarseMillisecondCount = coarseMillisecondCountBase;
            return coarseTimeBase;
        }

        public static void Wait(TimeSpan timeout)
        {
            if (timeout.Ticks <= 1) return;
            else timeout = new TimeSpan(timeout.Ticks - 1);

            TimeKeeper timer = TimeKeeper;
            if (timeout > 18.Milliseconds()) Thread.Sleep(timeout - 18.Milliseconds());
            while (timer.Elapsed < timeout) Thread.Sleep(0);
        }

        public static void Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (timeout.Ticks <= 1 || cancellationToken.IsCancellationRequested) return;
            else timeout = new TimeSpan(timeout.Ticks - 1);

            TimeKeeper timer = TimeKeeper;
            if (timeout > 18.Milliseconds()) cancellationToken.WaitHandle.WaitOne(timeout - 18.Milliseconds());
            while (timer.Elapsed < timeout && !cancellationToken.IsCancellationRequested) Thread.Sleep(0);
        }

        public static async Task WaitAsync(TimeSpan timeout)
        {
            if (timeout.Ticks <= 5) Wait(timeout);
            else
            {
                TimeKeeper timer = TimeKeeper;
                if (timeout > 18.Milliseconds()) await Task.Delay(timeout - 18.Milliseconds());
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
                if (timeout > 18.Milliseconds()) await Task.Delay(timeout - 18.Milliseconds(), cancellationToken);
                while (timer.Elapsed < timeout - 0.01.Milliseconds() && !cancellationToken.IsCancellationRequested) await Task.Yield();
                while (timer.Elapsed.Ticks < timeout.Ticks - 1000 && !cancellationToken.IsCancellationRequested) await Task.Delay(0);
                while (timer.Elapsed.Ticks < timeout.Ticks - 50 && !cancellationToken.IsCancellationRequested) Thread.Sleep(0);
            }
        }
    }
}