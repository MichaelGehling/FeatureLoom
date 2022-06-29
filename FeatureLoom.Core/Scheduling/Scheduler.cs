using FeatureLoom.Services;
using System;

namespace FeatureLoom.Scheduling
{
    /// <summary>
    /// Static shortcut for "Service<ISchedulerService>.Instance".
    /// If no ISchedulerService is initialized as Service<ISchedulerService> it will use SchedulerService as the default.
    /// Calls the Trigger() method of all registered ISchedule objects in a loop.
    /// After each ISchedule was triggered the scheduler waits for some time. 
    /// The waiting time will most likely not exceed any of the maxDelay values returned by the trigger() calls,
    /// but the time may be a lot shorter.
    /// </summary>
    public static class Scheduler
    {
        static Scheduler()
        {
            ServiceRegistry.DeclareServiceType(typeof(SchedulerService));
        }

        public static void AddSchedule(ISchedule schedule) => Service<ISchedulerService>.Instance.AddSchedule(schedule);
        public static bool ClearAllSchedulesAndStop(TimeSpan timeout) => Service<ISchedulerService>.Instance.ClearAllSchedulesAndStop(timeout);
        public static void InterruptWaiting() => Service<ISchedulerService>.Instance.InterruptWaiting();
        public static ActionSchedule ScheduleAction(string name, Func<DateTime, (bool, TimeSpan)> triggerAction) => Service<ISchedulerService>.Instance.ScheduleAction(name, triggerAction);
    }
}