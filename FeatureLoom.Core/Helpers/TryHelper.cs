using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.Helpers
{
    public static class TryHelper
    {
        public static bool Try<T>(Func<T> function, out T result)
        {
            try
            {
                result = function();
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        public static bool Try<T>(Func<Task<T>> function, out T result)
        {
            try
            {
                result = function().WaitFor();
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        public static async Task<(bool, T)> TryAsync<T>(Func<Task<T>> function)
        {
            try
            {
                var result = await function().ConfiguredAwait();
                return (true, result);
            }
            catch
            {
                return (false, default);
            }
        }

        public static bool Try(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Try(Func<Task> action)
        {
            try
            {
                action().WaitFor();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> TryAsync(Func<Task> action)
        {
            try
            {
                await action().ConfiguredAwait();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}