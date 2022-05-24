using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Scheduling
{
    /// <summary>
    /// 
    /// </summary>
    public static partial class Scheduler
    {        
        private static MicroLock myLock = new MicroLock();
        private static List<ISchedule> newSchedules = new List<ISchedule>();
        private static bool newScheduleAvailable = false;
        private static List<ScheduleContainer> activeSchedules = new List<ScheduleContainer>();
        private static List<ScheduleContainer> triggeredSchedules = new List<ScheduleContainer>();

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
                    schedulerThread = new Thread(RunScheduling);
                    schedulerThread.Name = "Scheduler";
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

        public static ActionSchedule ScheduleAction(string name, Func<DateTime, (bool, TimeSpan)> triggerAction)
        {
            var schedule = new ActionSchedule(name, triggerAction);
            AddSchedule(schedule);
            return schedule;
        }        

        private static void RunScheduling()
        {       
            while (!stop)
            {
                TimeKeeper executionTimer = AppTime.TimeKeeper;

                CheckForNewSchedules();
                TimeSpan delay = TriggerActiveSchedules();                
                SwapTriggeredToActive();
                
                if (cts.IsCancellationRequested && !newScheduleAvailable) cts = new CancellationTokenSource();
                if (activeSchedules.Count > 0)
                {
                    delay = delay - executionTimer.Elapsed;
                    delay = delay.Clamp(minimumDelay, int.MaxValue.Milliseconds());                    
                    AppTime.Wait(delay.Multiply(0.5).ClampLow(minimumDelay), delay, cts.Token);
                }
                else
                {
                    WaitForSchedulesOrStop();
                }
            }
            activeSchedules.Clear();
        }

        public static void InterruptWaiting()
        {
            cts.Cancel();
        }

        public static bool ClearAllSchedulesAndStop(TimeSpan timeout)
        {
            stop = true;
            cts.Cancel();
            mre.Set();
            return schedulerThread?.Join(timeout) ?? true;
        }


        private static TimeSpan TriggerActiveSchedules()
        {
            TimeSpan delay = TimeSpan.MaxValue;
            DateTime now = AppTime.Now;
            foreach (var schedule in activeSchedules)
            {
                if (schedule.TryGetSchedule(out var sv))
                {
                    bool stillActive = sv.Trigger(now, out TimeSpan maxDelay);
                    if (stillActive)
                    {
                        schedule.maxDelay = maxDelay;
                        triggeredSchedules.Add(schedule);
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
                        if (!activeSchedules.Any(weak => weak.TryGetSchedule(out var schedule) && schedule == newSchedule))
                        {
                            activeSchedules.Add(new ScheduleContainer(newSchedule));
                        }
                    }
                    newSchedules.Clear();
                    newScheduleAvailable = false;
                }
            }
        }

        private static void SwapTriggeredToActive()
        {
            SwapHelper.Swap(ref activeSchedules, ref triggeredSchedules);            
            triggeredSchedules.Clear();
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

        private class ScheduleContainer
        {
            WeakReference<ISchedule> scheduleRef;
            string name;
            public TimeSpan maxDelay;
            public string Name => name;


            public ScheduleContainer(ISchedule schedule)
            {
                this.scheduleRef = new WeakReference<ISchedule>(schedule);
                this.name = schedule.Name;
            }

            public bool TryGetSchedule(out ISchedule schedule)
            {                
                if (scheduleRef.TryGetTarget(out schedule))
                {
                    this.name = schedule.Name;
                    return true;
                }
                return false;
            }
            
        }

        
    }
}