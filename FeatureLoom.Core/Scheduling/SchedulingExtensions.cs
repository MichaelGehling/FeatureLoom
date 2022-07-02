using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Scheduling
{
    public static class SchedulingExtensions
    {        
        public static async Task InvokeDelayed(this Action action, TimeSpan minDelayTime, TimeSpan maxDelayTime, CancellationToken cancellationToken = default)
        {
            await AppTime.WaitAsync(minDelayTime, maxDelayTime, cancellationToken);
            await Task.Run(action);
        }

        public static async Task InvokeDelayed(this Func<Task> asyncAction, TimeSpan minDelayTime, TimeSpan maxDelayTime, CancellationToken cancellationToken = default)
        {
            await AppTime.WaitAsync(minDelayTime, maxDelayTime, cancellationToken);
            await asyncAction();
        }

        public static async Task<T> InvokeDelayed<T>(this Func<T> action, TimeSpan minDelayTime, TimeSpan maxDelayTime, CancellationToken cancellationToken = default)
        {
            await AppTime.WaitAsync(minDelayTime, maxDelayTime, cancellationToken);
            return await Task.Run(action);
        }

        public static async Task<T> InvokeDelayed<T>(this Func<Task<T>> asyncAction, TimeSpan minDelayTime, TimeSpan maxDelayTime, CancellationToken cancellationToken = default)
        {
            await AppTime.WaitAsync(minDelayTime, maxDelayTime, cancellationToken);
            return await asyncAction();
        }

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="name">The name of the new schedule</param>
        /// <param name="triggerAction">The function takes the current time as input parameter and returns a tuple with two values:
        /// 1. If the schedule continues (true) or if it finished (false) and 2. the longest possible wait time when it needs to be triggered again.</param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleAction(this IScheduler scheduler, string name, Func<DateTime, TimeFrame> triggerAction)
        {
            var schedule = new ActionSchedule(name, triggerAction);
            scheduler.AddSchedule(schedule);
            return schedule;
        }
    }
}