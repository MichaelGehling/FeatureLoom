using FeatureLoom.DependencyInversion;
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
            if (await AppTime.WaitAsync(minDelayTime, maxDelayTime, cancellationToken).ConfigureAwait(false)) action();            
        }

        public static async Task InvokeDelayed(this Func<Task> asyncAction, TimeSpan minDelayTime, TimeSpan maxDelayTime, CancellationToken cancellationToken = default)
        {
            if (await AppTime.WaitAsync(minDelayTime, maxDelayTime, cancellationToken).ConfigureAwait(false)) await asyncAction();            
        }

        public static async Task<T> InvokeDelayed<T>(this Func<T> action, TimeSpan minDelayTime, TimeSpan maxDelayTime, CancellationToken cancellationToken = default, T cancellationReturnValue = default)
        {
            if (await AppTime.WaitAsync(minDelayTime, maxDelayTime, cancellationToken).ConfigureAwait(false)) return action();
            else return cancellationReturnValue;
        }

        public static async Task<T> InvokeDelayed<T>(this Func<Task<T>> asyncAction, TimeSpan minDelayTime, TimeSpan maxDelayTime, CancellationToken cancellationToken = default, T cancellationReturnValue = default)
        {
            if (await AppTime.WaitAsync(minDelayTime, maxDelayTime, cancellationToken).ConfigureAwait(false)) return await asyncAction();
            else return cancellationReturnValue;
        }

        public static async Task InvokeDelayed(this Action action, TimeSpan delayTime, CancellationToken cancellationToken = default)
        {
            if (await AppTime.WaitAsync(delayTime, cancellationToken).ConfigureAwait(false)) action();
        }

        public static async Task InvokeDelayed(this Func<Task> asyncAction, TimeSpan delayTime, CancellationToken cancellationToken = default)
        {
            if (await AppTime.WaitAsync(delayTime, cancellationToken).ConfigureAwait(false)) await asyncAction();
        }

        public static async Task<T> InvokeDelayed<T>(this Func<T> action, TimeSpan delayTime, CancellationToken cancellationToken = default, T cancellationReturnValue = default)
        {
            if (await AppTime.WaitAsync(delayTime, cancellationToken).ConfigureAwait(false)) return action();
            else return cancellationReturnValue;
        }

        public static async Task<T> InvokeDelayed<T>(this Func<Task<T>> asyncAction, TimeSpan delayTime, CancellationToken cancellationToken = default, T cancellationReturnValue = default)
        {
            if (await AppTime.WaitAsync(delayTime, cancellationToken).ConfigureAwait(false)) return await asyncAction();
            else return cancellationReturnValue;
        }

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="name">The name of the new schedule</param>
        /// <param name="triggerAction">The function takes the current time as input parameter and returns a timeframe:
        /// If the timeframe is invalid the schedule is finished, otherwise the timeframe defines when the trigger method is called next. </param>
        /// <param name="ct">Can be used to cancel the action from outside</param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleAction(this SchedulerService scheduler, string name, Func<DateTime, ScheduleStatus> triggerAction, CancellationToken ct = default)
        {
            ActionSchedule schedule;
            if (ct == CancellationToken.None)
            {
                schedule = new ActionSchedule(name, triggerAction);
            }
            else
            {
                schedule = new ActionSchedule(name, t =>
                {
                    if (ct.IsCancellationRequested) return ScheduleStatus.Terminated;
                    return triggerAction(t);
                });
            }

            scheduler.AddSchedule(schedule);
            return schedule;
        }

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="name">The name of the new schedule</param>
        /// <param name="triggerAction">The action that will be executed. It takes the current time as input parameter</param>
        /// <param name="triggerTime">The time between the trigger action is called (with a tolerance of +15ms)</param>
        /// <param name="ct">Can be used to cancel the action from outside.</param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleAction(this SchedulerService scheduler, string name, Action<DateTime> triggerAction, TimeSpan triggerTime, CancellationToken ct = default)
        {
            return ScheduleAction(scheduler, name, now =>
            {
                if (ct.IsCancellationRequested) return ScheduleStatus.Terminated;
                triggerAction(now);
                return ScheduleStatus.WaitUntil(now + triggerTime);
            });
        }

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="name">The name of the new schedule</param>
        /// <param name="triggerAction">The action that will be executed</param>
        /// <param name="triggerTime">The time between the trigger action is called (with a tolerance of +15ms)</param>
        /// <param name="ct">Can be used to cancel the action from outside. Otherwise the action will be repeated forever.</param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleAction(this SchedulerService scheduler, string name, Action triggerAction, TimeSpan triggerTime, CancellationToken ct = default)
        {
            return ScheduleAction(scheduler, name, now =>
            {
                if (ct.IsCancellationRequested) return ScheduleStatus.Terminated;
                triggerAction();
                return ScheduleStatus.WaitUntil(now + triggerTime);
            });
        }

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="triggerAction">The action that will be executed. It takes the current time as input parameter</param>
        /// <param name="name">The name of the new schedule</param>        
        /// <param name="triggerTime">The time between the trigger action is called (with a tolerance of +15ms)</param>
        /// <param name="ct">Can be used to cancel the action from outside. Otherwise the action will be repeated forever.</param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleForRecurringExecution(this Action<DateTime> triggerAction, string name, TimeSpan triggerTime, CancellationToken ct = default)
        {
            return ScheduleAction(Service<SchedulerService>.Instance, name, now =>
            {
                if (ct.IsCancellationRequested) return ScheduleStatus.Terminated;
                triggerAction(now);
                return ScheduleStatus.WaitUntil(now + triggerTime);
            });
        }

        /// <summary>
        /// Creates a new schedule based on a lamda function, adds it to the scheduler and returns it.
        /// NOTE: The scheduler only keeps a weak reference to the schedule. If the schedule is not kept in another reference, it will be garbage collected.
        /// </summary>
        /// <param name="triggerAction">The action that will be executed</param>
        /// <param name="name">The name of the new schedule</param>        
        /// <param name="triggerTime">The time between the trigger action is called (with a tolerance of +15ms)</param>
        /// <param name="ct">Can be used to cancel the action from outside. Otherwise the action will be repeated forever.</param>
        /// <returns>The created schedule.</returns>
        public static ActionSchedule ScheduleForRecurringExecution(this Action triggerAction, string name, TimeSpan triggerTime, CancellationToken ct = default)
        {
            return ScheduleAction(Service<SchedulerService>.Instance, name, now =>
            {
                if (ct.IsCancellationRequested) return ScheduleStatus.Terminated;
                triggerAction();
                return ScheduleStatus.WaitUntil(now + triggerTime);
            });
        }
    }
}