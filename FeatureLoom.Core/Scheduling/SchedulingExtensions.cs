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

        public static async Task InvokeDelayed(this Action action, TimeSpan delayTime, CancellationToken cancellationToken = default)
        {
            await AppTime.WaitAsync(delayTime, cancellationToken);
            await Task.Run(action);
        }

        public static async Task InvokeDelayed(this Func<Task> asyncAction, TimeSpan delayTime, CancellationToken cancellationToken = default)
        {
            await AppTime.WaitAsync(delayTime, cancellationToken);
            await asyncAction();
        }

        public static async Task<T> InvokeDelayed<T>(this Func<T> action, TimeSpan delayTime, CancellationToken cancellationToken = default)
        {
            await AppTime.WaitAsync(delayTime, cancellationToken);
            return await Task.Run(action);
        }

        public static async Task<T> InvokeDelayed<T>(this Func<Task<T>> asyncAction, TimeSpan delayTime, CancellationToken cancellationToken = default)
        {
            await AppTime.WaitAsync(delayTime, cancellationToken);
            return await asyncAction();
        }

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="name">The name of the new schedule</param>
        /// <param name="triggerAction">The function takes the current time as input parameter and returns a timeframe:
        /// If the timeframe is invalid the schedule is finished, otherwise the timeframe defines when the trigger method is called next. </param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleAction(this SchedulerService scheduler, string name, Func<DateTime, ScheduleStatus> triggerAction)
        {
            var schedule = new ActionSchedule(name, triggerAction);
            scheduler.AddSchedule(schedule);
            return schedule;
        }

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="name">The name of the new schedule</param>
        /// <param name="triggerAction">The action takes the current time as input parameter</param>
        /// <param name="triggerTime">The time between the trigger action is called (with a tolerance of +1%)</param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleAction(this SchedulerService scheduler, string name, Action<DateTime> triggerAction, TimeSpan triggerTime)
        {
            return ScheduleAction(scheduler, name, triggerAction, triggerTime, triggerTime.Multiply(1.01));
        }

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="name">The name of the new schedule</param>
        /// <param name="triggerAction">The action takes the current time as input parameter</param>
        /// <param name="triggerTime">The time between the trigger action is called (with a variation of +1%</param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleAction(this SchedulerService scheduler, string name, Action<DateTime> triggerAction, TimeSpan minTriggerTime, TimeSpan maxTriggerTime)
        {
            return ScheduleAction(scheduler, name, now =>
                {
                    triggerAction(now);
                    return new ScheduleStatus(minTriggerTime, maxTriggerTime);
                });            
        }
    }
}