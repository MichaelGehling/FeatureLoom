using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;

namespace FeatureLoom.Supervisions
{
    public static class SupervisionService
    {        
        private static MicroLock myLock = new MicroLock();
        private static List<ISupervision> newSupervisions = new List<ISupervision>();
        private static List<ISupervision> activeSupervisions = new List<ISupervision>();
        private static List<ISupervision> handledSupervisions = new List<ISupervision>();

        private static Thread supervisorThread = null;
        private static ManualResetEventSlim mre = new ManualResetEventSlim(true);
        private static CancellationTokenSource cts = new CancellationTokenSource();

        private static TimeSpan minimumDelay = 0.001.Milliseconds();

        public static void AddSupervision(ISupervision supervision)
        {
            using (myLock.Lock())
            {
                if (supervisorThread == null)
                {
                    newSupervisions.Add(supervision);
                    mre.Set();
                    supervisorThread = new Thread(StartSupervision);
                    supervisorThread.Start();
                }
                else
                {
                    newSupervisions.Add(supervision);
                    cts.Cancel();
                    mre.Set();
                }
            }
        }

        public static void Supervise(Action supervisionAction, Func<bool> checkValidity, Func<TimeSpan> getDelay = null)
        {
            AddSupervision(new GenericSupervision(supervisionAction, checkValidity, getDelay));
        }

        private static void StartSupervision()
        {
            int stopCounter = 0;
            int timingCounter = 0;
            TimeKeeper timer = AppTime.TimeKeeper;
            while (true)
            {
                if (!CheckForPause(ref stopCounter, ref timingCounter, ref timer)) return;
                CheckForNewSupervisions(ref stopCounter);
                HandleActiveSupervisions();
                SwapHandledToActive();

                TimeSpan delay = GetDelay();
                if (timingCounter++ == 100_000)
                {
                    timingCounter = 0;                    
                    timer.Restart(timer.StartTime + timer.LastElapsed);
                }

                AppTime.Wait(delay, cts.Token);
            }
        }

        private static TimeSpan GetDelay()
        {
            TimeSpan minDelay = TimeSpan.MaxValue;
            foreach (var supervision in activeSupervisions)
            {
                var delay = supervision.MaxDelay;
                if (delay < minDelay) minDelay = delay;
            }
            return minDelay.Clamp(minimumDelay, int.MaxValue.Milliseconds());
        }

        private static void HandleActiveSupervisions()
        {
            foreach (var supervisor in activeSupervisions)
            {
                supervisor.Handle();
                if (supervisor.IsActive) handledSupervisions.Add(supervisor);
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

        private static bool CheckForPause(ref int stopCounter, ref int timingCounter, ref TimeKeeper timer)
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

                        timingCounter = 0;
                        timer = AppTime.TimeKeeper;
                    }
                    else lockHandle.Exit();
                }
            }

            return true;
        }

        private class GenericSupervision : ISupervision
        {
            private Action handleAction;
            private Func<bool> validityCheck;
            private Func<TimeSpan> getDelay = () => TimeSpan.Zero;

            public GenericSupervision(Action handleAction, Func<bool> validityCheck, Func<TimeSpan> getDelay = null)
            {
                this.handleAction = handleAction;
                this.validityCheck = validityCheck;
                this.getDelay = getDelay;
            }

            public TimeSpan MaxDelay => getDelay == null ? TimeSpan.Zero : getDelay();

            public bool IsActive => validityCheck();

            public void Handle()
            {
                handleAction();
            }
        }
    }
}