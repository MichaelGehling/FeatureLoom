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
    }
}