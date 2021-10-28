using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FeatureLoom.Supervision
{
    public static class SupervisionService
    {        
        private static MicroLock myLock = new MicroLock();
        private static List<WeakReference<ISupervision>> newSupervisions = new List<WeakReference<ISupervision>>();
        private static List<WeakReference<ISupervision>> activeSupervisions = new List<WeakReference<ISupervision>>();
        private static List<WeakReference<ISupervision>> handledSupervisions = new List<WeakReference<ISupervision>>();

        private static Thread supervisorThread = null;
        private static ManualResetEventSlim mre = new ManualResetEventSlim(true);
        private static CancellationTokenSource cts = new CancellationTokenSource();
        private static bool stop = false;

        private static TimeSpan minimumDelay = 0.001.Milliseconds();

        public static void AddSupervision(ISupervision supervision)
        {
            using (myLock.Lock())
            {
                stop = false;
                if (supervisorThread == null)
                {
                    newSupervisions.Add(new WeakReference<ISupervision>(supervision));
                    mre.Set();
                    supervisorThread = new Thread(StartSupervision);
                    supervisorThread.IsBackground = true;
                    supervisorThread.Priority = ThreadPriority.BelowNormal;
                    supervisorThread.Start();
                }
                else
                {
                    newSupervisions.Add(new WeakReference<ISupervision>(supervision));
                    cts.Cancel();
                    mre.Set();
                }
            }
        }

        public static void Supervise(Action<DateTime> supervisionAction, Func<TimeSpan> getDelay = null, Func<bool> checkValidity = null)
        {
            AddSupervision(new GenericSupervision(supervisionAction, getDelay, checkValidity));
        }

        private static void StartSupervision()
        {
            int stopCounter = 0;
            TimeKeeper timer = AppTime.TimeKeeper;
            while (true)
            {                
                TimeSpan executionStart = timer.LastElapsed;

                if (!CheckForPause(ref stopCounter) || stop) break;                
                CheckForNewSupervisions(ref stopCounter);
                HandleActiveSupervisions();
                SwapHandledToActive();                

                TimeSpan executionFinish = timer.Elapsed;
                TimeSpan executionTime = executionFinish - executionStart;
                timer.Restart(timer.StartTime + timer.LastElapsed);
                TimeSpan delay = GetDelay(executionTime);
                AppTime.Wait(delay, ref timer, cts.Token);
            }
        }

        public static bool StopSupervision(TimeSpan timeout)
        {
            stop = true;
            cts.Cancel();
            mre.Set();
            return supervisorThread?.Join(timeout) ?? true;
        }

        private static TimeSpan GetDelay(TimeSpan executionTime)
        {
            TimeSpan minDelay = TimeSpan.MaxValue;
            foreach (var supervision in activeSupervisions)
            {
                if (supervision.TryGetTarget(out var sv))
                {
                    var delay = sv.MaxDelay - executionTime;
                    if (delay < minDelay) minDelay = delay;
                }
            }
            return minDelay.Clamp(minimumDelay, int.MaxValue.Milliseconds());
        }

        private static void HandleActiveSupervisions()
        {            
            DateTime now = AppTime.Now;
            foreach (var supervision in activeSupervisions)
            {
                if (supervision.TryGetTarget(out var sv))
                {
                    sv.Handle(now);
                    if (sv.IsActive) handledSupervisions.Add(supervision);
                }
            }            
        }

        private static void CheckForNewSupervisions(ref int stopCounter)
        {
            if (newSupervisions.Count > 0)
            {
                using (myLock.Lock())
                {
                    stopCounter = 0;
                    activeSupervisions.AddRange(newSupervisions);
                    newSupervisions.Clear();
                    if (cts.IsCancellationRequested) cts = new CancellationTokenSource();
                }
            }
        }

        private static void SwapHandledToActive()
        {
            SwapHelper.Swap(ref activeSupervisions, ref handledSupervisions);            
            handledSupervisions.Clear();
        }

        private static bool CheckForPause(ref int stopCounter)
        {
            if (activeSupervisions.Count == 0 && newSupervisions.Count == 0)
            {
                if (++stopCounter > 100)
                {
                    stopCounter = 0;
                    var lockHandle = myLock.Lock();
                    if (activeSupervisions.Count == 0 && newSupervisions.Count == 0)
                    {
                        mre.Reset();
                        lockHandle.Exit();
                        bool wokeUp = mre.Wait(10.Seconds());
                        if (!wokeUp)
                        {
                            using (myLock.Lock())
                            {
                                if (activeSupervisions.Count == 0 && newSupervisions.Count == 0)
                                {
                                    supervisorThread = null;
                                    return false;
                                }
                            }
                        }
                    }
                    else lockHandle.Exit();
                }
            }

            return true;
        }

        private class GenericSupervision : ISupervision
        {
            private Action<DateTime> handleAction;
            private Func<bool> validityCheck;
            private Func<TimeSpan> getDelay = () => TimeSpan.Zero;

            public GenericSupervision(Action<DateTime> handleAction, Func<TimeSpan> getDelay = null, Func<bool> validityCheck = null)
            {
                this.handleAction = handleAction;
                this.validityCheck = validityCheck;
                this.getDelay = getDelay;
            }

            public TimeSpan MaxDelay => getDelay != null ? getDelay() : TimeSpan.Zero;

            public bool IsActive => validityCheck != null ? validityCheck() : true;

            public void Handle(DateTime now)
            {
                handleAction(now);
            }
        }
    }
}