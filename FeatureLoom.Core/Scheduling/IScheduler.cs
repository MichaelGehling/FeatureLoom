using System;
using System.Threading.Tasks;

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
        /// <summary>
        /// Adds a schedule object to the scheduler, to be triggered cyclically.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>        
        void AddSchedule(ISchedule schedule);

        /// <summary>
        /// Resets the scheduler.
        /// </summary>
        /// <returns>The returned task completes when the scheduler is reset.</returns>
        public Task ClearAllSchedulesAndStop();

        /// <summary>
        /// Interrupts waiting and immediatly lets the scheduler trigger all schedules.
        /// </summary>
        void InterruptWaiting();

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="name">The name of the new schedule</param>
        /// <param name="triggerAction">The function takes the current time as input parameter and returns a tuple with two values:
        /// 1. If the schedule continues (true) or if it finished (false) and 2. the longest possible wait time when it needs to be triggered again.</param>
        /// <returns>The created schedule.</returns>
        ActionSchedule ScheduleAction(string name, Func<DateTime, (bool, TimeSpan)> triggerAction);
    }
}