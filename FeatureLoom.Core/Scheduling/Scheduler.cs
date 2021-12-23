using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace FeatureLoom.Scheduling
{
    public static class Scheduler
    {        
        private static MicroLock myLock = new MicroLock();
        private static List<WeakReference<ISchedule>> newSchedules = new List<WeakReference<ISchedule>>();
        private static List<WeakReference<ISchedule>> activeSchedules = new List<WeakReference<ISchedule>>();
        private static List<WeakReference<ISchedule>> handledSchedules = new List<WeakReference<ISchedule>>();

        private static Thread schedulerThread = null;
        private static ManualResetEventSlim mre = new ManualResetEventSlim(true);
        private static CancellationTokenSource cts = new CancellationTokenSource();
        private static bool stop = false;

        private static TimeSpan minimumDelay = 0.01.Milliseconds();

        public static void AddSchedule(ISchedule schedule)
        {
            using (myLock.Lock())
            {
                stop = false;
                if (schedulerThread == null)
                {
                    newSchedules.Add(new WeakReference<ISchedule>(schedule));
                    mre.Set();
                    schedulerThread = new Thread(StartScheduling);
                    schedulerThread.IsBackground = true;
                    schedulerThread.Start();
                }
                else
                {
                    newSchedules.Add(new WeakReference<ISchedule>(schedule));
                    cts.Cancel();
                    mre.Set();
                }
            }
        }

        public static void ScheduleAction(Action<DateTime> scheduleAction, Func<TimeSpan> getDelay = null, Func<bool> checkValidity = null)
        {
            AddSchedule(new ActionSchedule(scheduleAction, getDelay, checkValidity));
        }

        private static void StartScheduling()
        {
            int stopCounter = 0;            
            while (true)
            {                
                if (!CheckForPause(ref stopCounter) || stop) break;

                TimeKeeper executionTimer = AppTime.TimeKeeper;
                CheckForNewSchedules(ref stopCounter);
                HandleActiveSchedules();
                if (cts.IsCancellationRequested) cts = new CancellationTokenSource();
                SwapHandledToActive();
                TimeSpan delay = GetDelay(executionTimer.Elapsed);
                AppTime.Wait(minimumDelay, delay, cts.Token);                
            }
        }

        public static void InterruptWaiting()
        {
            cts.Cancel();
        }

        public static bool StopScheduling(TimeSpan timeout)
        {
            stop = true;
            cts.Cancel();
            mre.Set();
            return schedulerThread?.Join(timeout) ?? true;
        }

        private static TimeSpan GetDelay(TimeSpan executionTime)
        {
            TimeSpan minDelay = TimeSpan.MaxValue;
            foreach (var schedule in activeSchedules)
            {
                if (schedule.TryGetTarget(out var sv))
                {
                    var delay = sv.MaxDelay - executionTime;
                    if (delay < minDelay) minDelay = delay;
                }

            }
            return minDelay.Clamp(minimumDelay, int.MaxValue.Milliseconds());
        }

        private static void HandleActiveSchedules()
        {            
            DateTime now = AppTime.Now;
            foreach (var schedule in activeSchedules)
            {
                if (schedule.TryGetTarget(out var sv))
                {
                    sv.Handle(now);
                    if (sv.IsActive) handledSchedules.Add(schedule);
                }
            }            
        }

        private static void CheckForNewSchedules(ref int stopCounter)
        {
            if (newSchedules.Count > 0)
            {
                using (myLock.Lock())
                {
                    stopCounter = 0;
                    activeSchedules.AddRange(newSchedules);
                    newSchedules.Clear();                    
                }
            }
        }

        private static void SwapHandledToActive()
        {
            SwapHelper.Swap(ref activeSchedules, ref handledSchedules);            
            handledSchedules.Clear();
        }

        private static bool CheckForPause(ref int stopCounter)
        {
            if (activeSchedules.Count == 0 && newSchedules.Count == 0)
            {
                if (++stopCounter > 100)
                {
                    stopCounter = 0;
                    var lockHandle = myLock.Lock();
                    if (activeSchedules.Count == 0 && newSchedules.Count == 0)
                    {
                        mre.Reset();
                        lockHandle.Exit();
                        bool wokeUp = mre.Wait(10.Seconds());
                        if (!wokeUp)
                        {
                            using (myLock.Lock())
                            {
                                if (activeSchedules.Count == 0 && newSchedules.Count == 0)
                                {
                                    schedulerThread = null;
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

        private class ActionSchedule : ISchedule
        {
            private Action<DateTime> handleAction;
            private Func<bool> validityCheck;
            private Func<TimeSpan> getDelay = () => TimeSpan.Zero;
            private static HashSet<ActionSchedule> keepAliveSet = new HashSet<ActionSchedule>();
            private static FeatureLock keepAliveLock = new FeatureLock();

            public ActionSchedule(Action<DateTime> handleAction, Func<TimeSpan> getDelay = null, Func<bool> validityCheck = null)
            {
                this.handleAction = handleAction;
                this.validityCheck = validityCheck;
                this.getDelay = getDelay;
                if (validityCheck == null || validityCheck())
                {
                    using (keepAliveLock.Lock()) keepAliveSet.Add(this);
                }
            }

            public TimeSpan MaxDelay => getDelay != null ? getDelay() : TimeSpan.Zero;

            public bool IsActive 
            {
                get
                {
                    if (validityCheck != null)
                    {
                        if (validityCheck()) return true;
                        else
                        {
                            using (keepAliveLock.Lock()) keepAliveSet.Remove(this);
                            return false;
                        }
                    }
                    else return true;
                }
            }

            public void Handle(DateTime now)
            {
                handleAction(now);
            }
        }
    }
}