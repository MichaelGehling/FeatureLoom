using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Logging;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FeatureLoom.Scheduling
{
    public static class Scheduler
    {        
        private static MicroLock myLock = new MicroLock();
        private static List<ISchedule> newSchedules = new List<ISchedule>();
        private static bool newScheduleAvailable = false;
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
                    newScheduleAvailable = true;
                    newSchedules.Add(schedule);
                    mre.Set();
                    schedulerThread = new Thread(StartScheduling);
                    schedulerThread.IsBackground = true;
                    schedulerThread.Start();
                }
                else
                {
                    newScheduleAvailable = true;
                    newSchedules.Add(schedule);
                    cts.Cancel();
                    mre.Set();
                }
            }
        }

        public static ISchedule ScheduleAction(Func<DateTime, (bool, TimeSpan)> triggerAction)
        {
            var schedule = new ActionSchedule(triggerAction);
            AddSchedule(schedule);
            return schedule;
        }

        private static void StartScheduling()
        {       
            while (true)
            {
                TimeKeeper executionTimer = AppTime.TimeKeeper;

                CheckForNewSchedules();
                TimeSpan delay = HandleActiveSchedules();                
                SwapHandledToActive();

                if (cts.IsCancellationRequested && !newScheduleAvailable) cts = new CancellationTokenSource();
                if (activeSchedules.Count > 0)
                {
                    delay = delay - executionTimer.Elapsed;
                    delay = delay.Clamp(minimumDelay, int.MaxValue.Milliseconds());
                    AppTime.Wait(minimumDelay, delay, cts.Token);
                }
                else
                {
                    WaitForSchedulesOrStop();
                    if (stop) break;
                }
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


        private static TimeSpan HandleActiveSchedules()
        {
            TimeSpan delay = TimeSpan.MaxValue;
            DateTime now = AppTime.Now;
            foreach (var schedule in activeSchedules)
            {
                if (schedule.TryGetTarget(out var sv))
                {
                    bool stillActive = sv.Trigger(now, out TimeSpan maxDelay);
                    if (stillActive)
                    {
                        handledSchedules.Add(schedule);
                        if (maxDelay < delay) delay = maxDelay;
                    }
                }
            }

            return delay;
        }

        private static void CheckForNewSchedules()
        {
            if (newScheduleAvailable)
            {
                using (myLock.Lock())
                {
                    foreach (var newSchedule in newSchedules)
                    {
                        if (!activeSchedules.Any(weak => weak.TryGetTarget(out var schedule) && schedule == newSchedule))
                        {
                            activeSchedules.Add(new WeakReference<ISchedule>(newSchedule));
                        }
                    }
                    newSchedules.Clear();
                    newScheduleAvailable = false;
                }
            }
        }

        private static void SwapHandledToActive()
        {
            SwapHelper.Swap(ref activeSchedules, ref handledSchedules);            
            handledSchedules.Clear();
        }

        private static void WaitForSchedulesOrStop()
        {
            if (activeSchedules.Count == 0 && newSchedules.Count == 0)
            {
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
                                stop = true;
                            }
                        }
                    }
                }
                else lockHandle.Exit();
            }
        }

        private class ActionSchedule : ISchedule
        {
            private Func<DateTime, (bool, TimeSpan)> triggerAction;
            private static HashSet<ActionSchedule> keepAliveSet = new HashSet<ActionSchedule>();
            private static FeatureLock keepAliveLock = new FeatureLock();

            public ActionSchedule(Func<DateTime, (bool, TimeSpan)> handleAction)
            {
                this.triggerAction = handleAction;
                using (keepAliveLock.Lock()) keepAliveSet.Add(this);
            }

            public void Handle(DateTime now)
            {
                triggerAction(now);
            }

            public bool Trigger(DateTime now, out TimeSpan maxDelay)
            {
                (bool active, TimeSpan delay) = triggerAction(now);
                if (!active) using (keepAliveLock.Lock()) keepAliveSet.Remove(this);
                maxDelay = delay;
                return active;
            }
        }
    }
}