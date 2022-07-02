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
    public interface IScheduler
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
    }
}