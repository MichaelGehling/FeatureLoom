using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.DependencyInversion;
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
    /// Calls the Trigger() method of all registered ISchedule objects in a loop.
    /// After each ISchedule was triggered the scheduler waits for some time. 
    /// The waiting time will most likely not exceed any of the maxDelay values returned by the trigger() calls,
    /// but the time may be a lot shorter.
    /// The Scheduler's loop runs in an own thread which is used to trigger all ISchedule objects. 
    /// </summary>
    public class SchedulerService
    {
        private MicroLock myLock = new MicroLock();
        private List<ISchedule> newSchedules = new List<ISchedule>();
        private bool newScheduleAvailable = false;
        private List<ScheduleContainer> activeSchedules = new List<ScheduleContainer>();
        private List<ScheduleContainer> triggeredSchedules = new List<ScheduleContainer>();

        private Thread schedulerThread = null;
        private ManualResetEventSlim mre = new ManualResetEventSlim(true);
        private CancellationTokenSource cts = new CancellationTokenSource();
        private bool stop = false;

        private TimeSpan minimumDelay = 0.01.Milliseconds();
        private TimeSpan maximumDelay = 60.Seconds();

        public void AddSchedule(ISchedule schedule)
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

        private void RunScheduling()
        {
            while (!stop)
            {                
                DateTime triggerStart = AppTime.Now;

                CheckForNewSchedules();
                TimeFrame triggerTimeFrame = TriggerActiveSchedules(triggerStart);
                SwapTriggeredToActive();

                if (cts.IsCancellationRequested && !newScheduleAvailable) cts = new CancellationTokenSource();
                if (activeSchedules.Count > 0)
                {
                    DateTime triggerEnd = AppTime.Now;
                    TimeSpan minDelay = triggerTimeFrame.TimeUntilStart(triggerEnd);
                    TimeSpan maxDelay = triggerTimeFrame.Remaining(triggerEnd);
                    AppTime.Wait(minDelay, maxDelay, cts.Token);
                }
                else
                {
                    WaitForSchedulesOrStop();
                }
            }
            activeSchedules.Clear();
        }

        public void InterruptWaiting()
        {
            cts.Cancel();
        }

        public Task ClearAllSchedulesAndStop()
        {
            return Task.Run(() =>
            {
                using (myLock.Lock())
                {
                    stop = true;
                    cts.Cancel();
                    mre.Set();
                    schedulerThread?.Join();
                }
            });
        }


        private TimeFrame TriggerActiveSchedules(DateTime now)
        {            
            TimeFrame triggerTimeFrame = new TimeFrame(now + maximumDelay, now + maximumDelay);
            foreach (var schedule in activeSchedules)
            {
                if (!schedule.TryGetSchedule(out var sv)) continue;

                if (schedule.nextTriggerTimeFrame.Started(now))
                {
                    schedule.nextTriggerTimeFrame = sv.Trigger(now);                     
                    if (schedule.nextTriggerTimeFrame.IsInvalid) continue;
                }

                triggeredSchedules.Add(schedule);
                triggerTimeFrame = new TimeFrame(triggerTimeFrame.utcStartTime.TheEarlierOne(schedule.nextTriggerTimeFrame.utcStartTime), 
                                                 triggerTimeFrame.utcEndTime.TheEarlierOne(schedule.nextTriggerTimeFrame.utcEndTime));
            }
            triggerTimeFrame = new TimeFrame(triggerTimeFrame.utcStartTime.TheLaterOne(now + minimumDelay), triggerTimeFrame.utcEndTime);
            return triggerTimeFrame;
        }

        private void CheckForNewSchedules()
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

        private void SwapTriggeredToActive()
        {
            SwapHelper.Swap(ref activeSchedules, ref triggeredSchedules);
            triggeredSchedules.Clear();
        }

        private void WaitForSchedulesOrStop()
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
            public TimeFrame nextTriggerTimeFrame;
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