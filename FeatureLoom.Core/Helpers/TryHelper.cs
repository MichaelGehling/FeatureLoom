using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.Helpers
{
    public static class TryHelper
    {
        /// <summary>
        /// Executes a function and returns true if it completes without an exception, otherwise false. The function's result is provided via an out parameter.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="function">The function to execute.</param>
        /// <param name="result">The result of the function if it completes successfully, otherwise the default value of T.</param>
        /// <returns>True on success, false on exception.</returns>
        public static bool Try<T>(this Func<T> function, out T result)
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

        /// <summary>
        /// Executes a function and returns true if it completes without an exception, otherwise false. The function's result and a potential exception are provided via out parameters.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="function">The function to execute.</param>
        /// <param name="result">The result of the function if it completes successfully, otherwise the default value of T.</param>
        /// <param name="exception">The caught exception if the function fails, otherwise null.</param>
        /// <returns>True on success, false on exception.</returns>
        public static bool Try<T>(this Func<T> function, out T result, out Exception exception)
        {
            try
            {
                result = function();
                exception = null;
                return true;
            }
            catch (Exception e)
            {
                result = default;
                exception = e;
                return false;
            }
        }

        /// <summary>
        /// Executes an async function and returns a tuple indicating success and the function's result.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="function">The async function to execute.</param>
        /// <returns>A tuple with a boolean indicating success and the result of the function.</returns>
        public static async Task<(bool success, T result)> TryAsync<T>(this Func<Task<T>> function)
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

        /// <summary>
        /// Executes an async function and returns a tuple indicating success, the function's result, and any caught exception.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="function">The async function to execute.</param>
        /// <returns>A tuple with a boolean indicating success, the result of the function, and the caught exception.</returns>
        public static async Task<(bool success, T result, Exception exception)> TryAsyncWithException<T>(this Func<Task<T>> function)
        {
            try
            {
                var result = await function().ConfiguredAwait();
                return (true, result, null);
            }
            catch (Exception e)
            {
                return (false, default, e);
            }
        }

        /// <summary>
        /// Executes an action and returns true if it completes without an exception, otherwise false.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <returns>True on success, false on exception.</returns>
        public static bool Try(this Action action)
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

        /// <summary>
        /// Executes an action and returns true if it completes without an exception, otherwise false. A potential exception is provided via an out parameter.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="exception">The caught exception if the action fails, otherwise null.</param>
        /// <returns>True on success, false on exception.</returns>
        public static bool Try(this Action action, out Exception exception)
        {
            try
            {
                action();
                exception = null;
                return true;
            }
            catch (Exception e)
            {
                exception = e;
                return false;
            }
        }

        /// <summary>
        /// Executes an async action and returns a boolean indicating if it completed without an exception.
        /// </summary>
        /// <param name="action">The async action to execute.</param>
        /// <returns>True on success, false on exception.</returns>
        public static async Task<bool> TryAsync(this Func<Task> action)
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

        /// <summary>
        /// Executes an async action and returns a tuple indicating success and any caught exception.
        /// </summary>
        /// <param name="action">The async action to execute.</param>
        /// <returns>A tuple with a boolean indicating success and the caught exception.</returns>
        public static async Task<(bool success, Exception exception)> TryAsyncWithException(this Func<Task> action)
        {
            try
            {
                await action().ConfiguredAwait();
                return (true, null);
            }
            catch (Exception e)
            {
                return (false, e);
            }
        }
    }
}