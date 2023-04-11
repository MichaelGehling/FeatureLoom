using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Time
{
    public class AppTimeService : IAppTime
    {
        private Stopwatch stopWatch = new Stopwatch();
        private DateTime coarseTimeBase;
        private int coarseMillisecondCountBase;
        private TimeSpan lowerSleepLimit = 16.Milliseconds();
        private TimeSpan lowerAsyncSleepLimit = 16.Milliseconds();
        private DateTime unixTimeBase = new DateTime(1970, 1, 1);

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
        /// Note: Every second, the coarse time will be reset by calling DateTime.UtcNow. Calling AppTime.Now will also reset the CoarseTime to the actual time.
        /// </summary>
        public DateTime CoarseNow
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

        private DateTime ResetCoarseNow(DateTime now)
        {
            coarseTimeBase = now;
            coarseMillisecondCountBase = Environment.TickCount;
            return coarseTimeBase;
        }

        public void Wait(TimeSpan minTimeout, TimeSpan maxTimeout)
        {
            Wait(minTimeout, maxTimeout, CancellationToken.None);            
        }

        public void Wait(TimeSpan timeout)
        {
            Wait(timeout, timeout, CancellationToken.None);
        }

        public void Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            Wait(timeout, timeout, cancellationToken);
        }

        public void Wait(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken)
        {
            if (minTimeout.Ticks <= 1 || cancellationToken.IsCancellationRequested) return;
            else minTimeout = new TimeSpan(minTimeout.Ticks - 1);

            var timer = TimeKeeper;

            if (maxTimeout >= lowerSleepLimit) cancellationToken.WaitHandle.WaitOne((maxTimeout+minTimeout).Divide(2));

            if (timer.Elapsed > minTimeout || cancellationToken.IsCancellationRequested) return;

            var lowPrioLimit = minTimeout - 0.1.Milliseconds();
            if (timer.LastElapsed < lowPrioLimit)
            {
                var oldPriority = Thread.CurrentThread.Priority;
                try
                {
                    Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                    while (timer.Elapsed < lowPrioLimit && !cancellationToken.IsCancellationRequested) Thread.Sleep(0);
                }
                finally
                {
                    Thread.CurrentThread.Priority = oldPriority;
                }                
            }

            while (timer.Elapsed < minTimeout && !cancellationToken.IsCancellationRequested) Thread.Sleep(0);
        }

        public Task WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout)
        {            
            return WaitAsync(minTimeout, maxTimeout, CancellationToken.None);
        }

        public Task WaitAsync(TimeSpan timeout)
        {
            return WaitAsync(timeout, timeout, CancellationToken.None);
        }

        public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return WaitAsync(timeout, timeout, cancellationToken);
        }

        public async Task WaitAsync(TimeSpan minTimeout, TimeSpan maxTimeout, CancellationToken cancellationToken)
        {            
            if (minTimeout.Ticks <= 5) Wait(minTimeout, maxTimeout);
            else
            {
                var timer = TimeKeeper;
                if (maxTimeout >= lowerAsyncSleepLimit)
                {
                    try
                    {
                        await Task.Delay((maxTimeout + minTimeout).Divide(2), cancellationToken);
                    }
                    catch(TaskCanceledException)
                    {
                        return;
                    }
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