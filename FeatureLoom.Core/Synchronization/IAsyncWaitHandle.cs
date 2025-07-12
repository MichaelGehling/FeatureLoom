using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Synchronization
{
    public interface IAsyncWaitHandle 
    {
        /// <summary>
        /// A task that will be completed when wait handle is signalled. May become cancelled or failed, due to some external interruption.
        /// </summary>
        Task WaitingTask { get; }

        /// <summary>
        /// Waits async until signalled.
        /// </summary>
        /// <returns>True if signalled, false in case of some external interruption</returns>
        Task<bool> WaitAsync();

        /// <summary>
        /// Waits async until signalled or timeout elapsed.
        /// </summary>
        /// <returns>True if signalled, false if timeout elapsed or in case of some external interruption</returns>
        Task<bool> WaitAsync(TimeSpan timeout);

        /// <summary>
        /// Waits async until signalled or aborted via cancellation token. 
        /// </summary>
        /// <returns>True if signalled, false if aborted via cancellation token or in case of some external interruption</returns>
        Task<bool> WaitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Waits async until signalled, timeout elapsed or aborted via cancellation token.
        /// </summary>
        /// <returns>True if signalled, false if timeout elapsed or aborted via cancellation token or in case of some external interruption</returns>
        Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Waits until signalled.
        /// </summary>
        /// <returns>True if signalled, false in case of some external interruption</returns>
        bool Wait();

        /// <summary>
        /// Waits until signalled or timeout elapsed.
        /// </summary>
        /// <returns>True if signalled, false if timeout elapsed or in case of some external interruption</returns>
        bool Wait(TimeSpan timeout);

        /// <summary>
        /// Waits until signalled or aborted via cancellation token. 
        /// </summary>
        /// <returns>True if signalled, false if aborted via cancellation token or in case of some external interruption</returns>
        bool Wait(CancellationToken cancellationToken);

        /// <summary>
        /// Waits until signalled, timeout elapsed or aborted via cancellation token.
        /// </summary>
        /// <returns>True if signalled, false if timeout elapsed or aborted via cancellation token or in case of some external interruption</returns>
        bool Wait(TimeSpan timeout, CancellationToken cancellationToken);

        /// <summary>
        /// Checks if the wait handle would actually wait or not if currently waited for.
        /// </summary>
        /// <returns>The reult of the check.</returns>
        bool WouldWait();

        /// <summary>
        /// Trys to convert the AsyncWaitHandle to a standard WaitHandle. Might not be supported by all applications, in that case the result is always false.
        /// </summary>
        /// <param name="waitHandle">The standard WaitHandle</param>
        /// <returns>If the convertion was successful</returns>
        bool TryConvertToWaitHandle(out WaitHandle waitHandle);
    }

    public static class IAsyncWaitHandleExtensions
    {
        /// <summary>
        /// Extracts the tasks from an array of AsyncWaitHandles (More efficient solution compared with LINQ)
        /// </summary>
        /// <param name="waitHandles"></param>
        /// <returns>The resulting array of tasks</returns>
        public static Task[] GetWaitingTasks(this IAsyncWaitHandle[] waitHandles)
        {
            if (waitHandles.Length == 0) return Array.Empty<Task>();

            var tasks = new Task[waitHandles.Length];
            for (int i = 0; i < waitHandles.Length; i++)
            {
                tasks[i] = waitHandles[i].WaitingTask;
            }
            return tasks;
        }

        /// <summary>
        /// Extracts the tasks from an array of AsyncWaitHandles and adds one extra Task to the resulting array.
        /// </summary>
        /// <param name="waitHandles"></param>
        /// <returns>The resulting array of tasks</returns>
        public static Task[] GetWaitingTasks(this IAsyncWaitHandle[] waitHandles, Task extraTask)
        {
            var tasks = new Task[waitHandles.Length + 1];
            for (int i = 0; i < waitHandles.Length; i++)
            {
                tasks[i] = waitHandles[i].WaitingTask;
            }
            tasks[waitHandles.Length] = extraTask;
            return tasks;
        }

        /// <summary>
        /// Executes an action when the IAsyncWaithandle is set to true.
        /// Important: If repeat is true, make sure to reset the wait handle in the action, otherwise it will be executed continuesly in a loop.
        /// </summary>
        /// <param name="waitHandle">The wait handle will trigger the action when set</param>
        /// <param name="action">The action that is executed when the wait handle is set</param>
        /// <param name="repeat">If false, the action will only be executed once, if true, it will remain active and will always execute as long as the wait handle is set</param>
        /// <param name="cancellationToken">Allows to abort waiting for the wait handle and also stop potential repeating</param>
        /// <returns></returns>
        public static async Task OnEvent(this IAsyncWaitHandle waitHandle, Action action, bool repeat = false, CancellationToken cancellationToken = default)
        {
            while(repeat && !cancellationToken.IsCancellationRequested)
            {
                if (await waitHandle.WaitAsync(cancellationToken).ConfiguredAwait())
                {
                    action();
                }
            }
        }

        /// <summary>
        /// Executes an action when the IAsyncWaithandle is set to true.
        /// Important: If repeat is true, make sure to reset the wait handle in the action, otherwise it will be executed continuesly in a loop.
        /// </summary>
        /// <param name="waitHandle">The wait handle will trigger the action when set</param>
        /// <param name="action">The action that is executed when the wait handle is set</param>
        /// <param name="actionArgument">An argument that is passed to the action</param>
        /// <param name="repeat">If false, the action will only be executed once, if true, it will remain active and will always execute as long as the wait handle is set</param>
        /// <param name="cancellationToken">Allows to abort waiting for the wait handle and also stop potential repeating</param>
        /// <returns></returns>
        public static async Task OnEvent<T>(this IAsyncWaitHandle waitHandle, Action<T> action, T actionArgument, bool repeat = false, CancellationToken cancellationToken = default)
        {
            while (repeat && !cancellationToken.IsCancellationRequested)
            {
                if (await waitHandle.WaitAsync(cancellationToken).ConfiguredAwait())
                {
                    action(actionArgument);
                }
            }
        }

        /// <summary>
        /// Executes an async action when the IAsyncWaithandle is set to true.
        /// Important: If repeat is true, make sure to reset the wait handle in the action, otherwise it will be executed continuesly in a loop.
        /// </summary>
        /// <param name="waitHandle">The wait handle will trigger the action when set</param>
        /// <param name="action">The async action that is executed when the wait handle is set</param>
        /// <param name="repeat">If false, the action will only be executed once, if true, it will remain active and will always execute as long as the wait handle is set</param>
        /// <param name="cancellationToken">Allows to abort waiting for the wait handle and also stop potential repeating</param>
        /// <returns></returns>
        public static async Task OnEvent(this IAsyncWaitHandle waitHandle, Func<Task> action, bool repeat = false, CancellationToken cancellationToken = default)
        {
            while (repeat && !cancellationToken.IsCancellationRequested)
            {
                if (await waitHandle.WaitAsync(cancellationToken).ConfiguredAwait())
                {
                    await action().ConfiguredAwait();
                }
            }
        }

        /// <summary>
        /// Executes an async action when the IAsyncWaithandle is set to true.
        /// Important: If repeat is true, make sure to reset the wait handle in the action, otherwise it will be executed continuesly in a loop.
        /// </summary>
        /// <param name="waitHandle">The wait handle will trigger the action when set</param>
        /// <param name="action">The async action that is executed when the wait handle is set</param>
        /// <param name="actionArgument">An argument that is passed to the action</param>
        /// <param name="repeat">If false, the action will only be executed once, if true, it will remain active and will always execute as long as the wait handle is set</param>
        /// <param name="cancellationToken">Allows to abort waiting for the wait handle and also stop potential repeating</param>
        /// <returns></returns>
        public static async Task OnEvent<T>(this IAsyncWaitHandle waitHandle, Func<T, Task> action, T actionArgument, bool repeat = false, CancellationToken cancellationToken = default)
        {
            while (repeat && !cancellationToken.IsCancellationRequested)
            {
                if (await waitHandle.WaitAsync(cancellationToken).ConfiguredAwait())
                {
                    await action(actionArgument).ConfiguredAwait();
                }
            }
        }
    }
}