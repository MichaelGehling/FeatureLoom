using FeatureLoom.Services;
using System;
using System.Threading.Tasks;

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
        /// <summary>
        /// Adds a schedule object to the scheduler, to be triggered cyclically.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>        
        public static void AddSchedule(ISchedule schedule) => Service<ISchedulerService>.Instance.AddSchedule(schedule);

        /// <summary>
        /// Resets the scheduler.
        /// </summary>
        /// <returns>The returned task completes when the scheduler is reset.</returns>
        public static Task ClearAllSchedulesAndStop() => Service<ISchedulerService>.Instance.ClearAllSchedulesAndStop();

        /// <summary>
        /// Interrupts waiting and immediatly lets the scheduler trigger all schedules.
        /// </summary>
        public static void InterruptWaiting() => Service<ISchedulerService>.Instance.InterruptWaiting();

        /// <summary>
        /// Creates a new schedule based on a passed lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the created schedule. If the returned schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="name">The name of the new schedule</param>
        /// <param name="triggerAction">The function takes the current time as input parameter and returns a tuple with two values:
        /// 1. If the schedule continues (true) or if it finished (false) and 2. the longest possible wait time when it needs to be triggered again.</param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleAction(string name, Func<DateTime, (bool, TimeSpan)> triggerAction) => Service<ISchedulerService>.Instance.ScheduleAction(name, triggerAction);
    }
}