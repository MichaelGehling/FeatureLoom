using System;

namespace FeatureLoom.Scheduling
{
    /// <summary>
    /// Calls the Trigger() method of all registered ISchedule objects in a loop.
    /// After each ISchedule was triggered the scheduler waits for some time. 
    /// The waiting time will most likely not exceed any of the maxDelay values returned by the trigger() calls,
    /// but the time may be a lot shorter.
    /// </summary>
    public interface ISchedulerService
    {
        void AddSchedule(ISchedule schedule);
        bool ClearAllSchedulesAndStop(TimeSpan timeout);
        void InterruptWaiting();
        ActionSchedule ScheduleAction(string name, Func<DateTime, (bool, TimeSpan)> triggerAction);
    }
}