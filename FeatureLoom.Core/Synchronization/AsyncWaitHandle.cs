using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Synchronization
{
    /// <summary>
    /// A lightweight, task-based implementation of <see cref="IAsyncWaitHandle"/> that wraps a <see cref="Task"/>
    /// and exposes both synchronous and asynchronous wait operations.
    /// <para>
    /// Use <see cref="FromTask"/> to create an instance from any <see cref="Task"/>, or use the implicit
    /// conversion operator. If the task is already completed, <see cref="NoWaitingHandle"/> is returned
    /// to avoid unnecessary allocations.
    /// </para>
    /// </summary>
    public class AsyncWaitHandle : IAsyncWaitHandle
    {
        #region static

        /// <summary>
        /// Synchronously waits until all of the provided <see cref="IAsyncWaitHandle"/> instances are signalled.
        /// If all handles are already signalled, returns immediately.
        /// When all handles support <see cref="WaitHandle"/> conversion, <see cref="WaitHandle.WaitAll(WaitHandle[])"/>
        /// is used for efficiency; otherwise handles are waited sequentially.
        /// </summary>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns><c>true</c> when all handles have been signalled.</returns>
        /// <remarks>
        /// Blocks the calling thread. When the handles cannot all be converted to <see cref="WaitHandle"/>s,
        /// the wait is performed on the underlying tasks. Calling this from a single-threaded
        /// <see cref="SynchronizationContext"/> can deadlock if those tasks resume on the captured context.
        /// Prefer <see cref="WaitAllAsync(IAsyncWaitHandle[])"/> in asynchronous code.
        /// </remarks>
        public static bool WaitAll(params IAsyncWaitHandle[] asyncWaitHandles)
        {
            bool allProvideWaitHandle = true;
            bool anyWouldWait = false;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (!anyWouldWait) return true;

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length];
                for (int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                return WaitHandle.WaitAll(handles);
            }
            else
            {
                bool allReady;
                do
                {
                    allReady = true;
                    for (int i = 0; i < asyncWaitHandles.Length; i++)
                    {
                        if (asyncWaitHandles[i].WouldWait())
                        {
                            allReady = false;
                            asyncWaitHandles[i].Wait();
                            break;
                        }
                    }
                }
                while (!allReady);
            }

            return true;
        }

        /// <summary>
        /// Asynchronously waits until all of the provided <see cref="IAsyncWaitHandle"/> instances are signalled.
        /// </summary>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns><c>true</c> when all handles have been signalled.</returns>
        public static async Task<bool> WaitAllAsync(params IAsyncWaitHandle[] asyncWaitHandles)
        {
            bool anyWouldWait = false;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
            }

            if (!anyWouldWait) return true;

            await Task.WhenAll(asyncWaitHandles.GetWaitingTasks()).ConfiguredAwait();
            return true;
        }

        /// <summary>
        /// Synchronously waits until all of the provided <see cref="IAsyncWaitHandle"/> instances are signalled
        /// or the <paramref name="token"/> is cancelled.
        /// </summary>
        /// <param name="token">A cancellation token to abort the wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns><c>true</c> if all handles were signalled; <c>false</c> if cancelled.</returns>
        public static bool WaitAll(CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (token.IsCancellationRequested) return false;

            bool allReady;
            do
            {
                allReady = true;
                for (int i = 0; i < asyncWaitHandles.Length; i++)
                {
                    if (asyncWaitHandles[i].WouldWait())
                    {
                        allReady = false;
                        asyncWaitHandles[i].Wait(token);
                        break;
                    }
                }
            }
            while (!allReady && !token.IsCancellationRequested);

            return !token.IsCancellationRequested;
        }

        /// <summary>
        /// Asynchronously waits until all of the provided <see cref="IAsyncWaitHandle"/> instances are signalled
        /// or the <paramref name="token"/> is cancelled.
        /// </summary>
        /// <param name="token">A cancellation token to abort the wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns><c>true</c> if all handles were signalled; <c>false</c> if cancelled.</returns>
        public static async Task<bool> WaitAllAsync(CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (token.IsCancellationRequested) return false;

            bool anyWouldWait = false;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
            }

            if (!anyWouldWait) return true;

            await Task.WhenAll(asyncWaitHandles.GetWaitingTasks()).TryWaitAsync(token).ConfiguredAwait();
            return !token.IsCancellationRequested;
        }

        /// <summary>
        /// Synchronously waits until all of the provided <see cref="IAsyncWaitHandle"/> instances are signalled
        /// or the <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns><c>true</c> if all handles were signalled within the timeout; <c>false</c> if timed out.</returns>
        public static bool WaitAll(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (timeout <= TimeSpan.Zero) return false;

            DateTime now = AppTime.Now;
            TimeFrame timeoutFrame = new TimeFrame(now, timeout);

            bool allProvideWaitHandle = true;
            bool anyWouldWait = false;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (!anyWouldWait) return true;
            if (timeoutFrame.Elapsed(now)) return false;

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length];
                for (int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                return WaitHandle.WaitAll(handles, timeoutFrame.Remaining(now));
            }
            else
            {
                bool allReady;
                do
                {
                    allReady = true;
                    now = AppTime.Now;
                    for (int i = 0; i < asyncWaitHandles.Length && !timeoutFrame.Elapsed(now); i++)
                    {
                        if (asyncWaitHandles[i].WouldWait())
                        {
                            allReady = false;
                            asyncWaitHandles[i].Wait(timeoutFrame.Remaining(now));
                            now = AppTime.Now;
                            break;
                        }
                    }
                }
                while (!allReady && !timeoutFrame.Elapsed(now));
            }

            return !timeoutFrame.Elapsed(now);
        }

        /// <summary>
        /// Asynchronously waits until all of the provided <see cref="IAsyncWaitHandle"/> instances are signalled
        /// or the <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns><c>true</c> if all handles were signalled within the timeout; <c>false</c> if timed out.</returns>
        public static async Task<bool> WaitAllAsync(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (timeout <= TimeSpan.Zero) return false;

            bool anyWouldWait = false;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
            }

            if (!anyWouldWait) return true;

            var allCompleted = await Task.WhenAll(asyncWaitHandles.GetWaitingTasks()).TryWaitAsync(timeout).ConfiguredAwait();
            return allCompleted;
        }

        /// <summary>
        /// Synchronously waits until all of the provided <see cref="IAsyncWaitHandle"/> instances are signalled,
        /// the <paramref name="timeout"/> elapses, or the <paramref name="token"/> is cancelled.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="token">A cancellation token to abort the wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns><c>true</c> if all handles were signalled; <c>false</c> if timed out or cancelled.</returns>
        public static bool WaitAll(TimeSpan timeout, CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (token.IsCancellationRequested) return false;
            if (timeout <= TimeSpan.Zero) return false;

            DateTime now = AppTime.Now;
            TimeFrame timeoutFrame = new TimeFrame(now, timeout);

            bool allReady;
            do
            {
                allReady = true;
                now = AppTime.Now;
                for (int i = 0; i < asyncWaitHandles.Length && !timeoutFrame.Elapsed(now); i++)
                {
                    if (asyncWaitHandles[i].WouldWait())
                    {
                        allReady = false;
                        asyncWaitHandles[i].Wait(timeoutFrame.Remaining(now), token);
                        now = AppTime.Now;
                        break;
                    }
                }
            }
            while (!allReady && !token.IsCancellationRequested && !timeoutFrame.Elapsed(now));

            return !token.IsCancellationRequested && !timeoutFrame.Elapsed(AppTime.Now);
        }

        /// <summary>
        /// Asynchronously waits until all of the provided <see cref="IAsyncWaitHandle"/> instances are signalled,
        /// the <paramref name="timeout"/> elapses, or the <paramref name="token"/> is cancelled.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="token">A cancellation token to abort the wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns><c>true</c> if all handles were signalled; <c>false</c> if timed out or cancelled.</returns>
        public static async Task<bool> WaitAllAsync(TimeSpan timeout, CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (token.IsCancellationRequested) return false;
            if (timeout <= TimeSpan.Zero) return false;

            bool anyWouldWait = false;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                anyWouldWait |= asyncWaitHandles[i].WouldWait();
            }

            if (!anyWouldWait) return true;

            var allCompleted = await Task.WhenAll(asyncWaitHandles.GetWaitingTasks()).TryWaitAsync(timeout, token).ConfiguredAwait();
            return allCompleted;
        }

        /// <summary>
        /// Synchronously waits until any of the provided <see cref="IAsyncWaitHandle"/> instances is signalled.
        /// Returns immediately if any handle is already signalled.
        /// </summary>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns>The zero-based index of the first signalled handle, or <see cref="WaitHandle.WaitTimeout"/> on failure.</returns>
        public static int WaitAny(params IAsyncWaitHandle[] asyncWaitHandles)
        {
            bool allProvideWaitHandle = true;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;

                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length];
                for (int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                return WaitHandle.WaitAny(handles);
            }
            else
            {
                Task[] tasks = asyncWaitHandles.GetWaitingTasks();
                Task.WhenAny(tasks).WaitFor();
                return FindSignalledIndex(tasks, tasks.Length);
            }
        }

        /// <summary>
        /// Asynchronously waits until any of the provided <see cref="IAsyncWaitHandle"/> instances is signalled.
        /// Returns immediately if any handle is already signalled.
        /// </summary>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns>The zero-based index of the first signalled handle, or <see cref="WaitHandle.WaitTimeout"/> on failure.</returns>
        public static async Task<int> WaitAnyAsync(params IAsyncWaitHandle[] asyncWaitHandles)
        {
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;
            }

            Task[] tasks = asyncWaitHandles.GetWaitingTasks();
            await Task.WhenAny(tasks).ConfiguredAwait();
            return FindSignalledIndex(tasks, tasks.Length);
        }

        /// <summary>
        /// Synchronously waits until any of the provided <see cref="IAsyncWaitHandle"/> instances is signalled
        /// or the <paramref name="token"/> is cancelled.
        /// </summary>
        /// <param name="token">A cancellation token to abort the wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns>The zero-based index of the first signalled handle, or <see cref="WaitHandle.WaitTimeout"/> if cancelled.</returns>
        public static int WaitAny(CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (token.IsCancellationRequested) return WaitHandle.WaitTimeout;

            bool allProvideWaitHandle = true;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;

                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length + 1];
                for (int i = 0; i < asyncWaitHandles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                handles[handles.Length - 1] = token.WaitHandle;
                var index = WaitHandle.WaitAny(handles);
                if (index == handles.Length - 1) index = WaitHandle.WaitTimeout;
                return index;
            }
            else
            {
                Task[] tasks = asyncWaitHandles.GetWaitingTasks();
                // Cancellation is expected here and simply ends the wait; the scan below then
                // reports whichever handle (if any) was signalled.
                try { Task.WhenAny(tasks).Wait(token); } catch (OperationCanceledException) { }
                return FindSignalledIndex(tasks, tasks.Length);
            }
        }

        /// <summary>
        /// Asynchronously waits until any of the provided <see cref="IAsyncWaitHandle"/> instances is signalled
        /// or the <paramref name="token"/> is cancelled.
        /// </summary>
        /// <param name="token">A cancellation token to abort the wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns>The zero-based index of the first signalled handle, or <see cref="WaitHandle.WaitTimeout"/> if cancelled.</returns>
        public static async Task<int> WaitAnyAsync(CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (token.IsCancellationRequested) return WaitHandle.WaitTimeout;

            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;
            }

            Task[] tasks = asyncWaitHandles.GetWaitingTasks();
            await Task.WhenAny(tasks).TryWaitAsync(token).ConfiguredAwait();
            return FindSignalledIndex(tasks, tasks.Length);
        }

        /// <summary>
        /// Synchronously waits until any of the provided <see cref="IAsyncWaitHandle"/> instances is signalled
        /// or the <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns>The zero-based index of the first signalled handle, or <see cref="WaitHandle.WaitTimeout"/> if timed out.</returns>
        /// <remarks>
        /// Blocks the calling thread. Prefer <see cref="WaitAnyAsync(TimeSpan, IAsyncWaitHandle[])"/>
        /// in asynchronous code, especially when running on a single-threaded
        /// <see cref="SynchronizationContext"/>.
        /// </remarks>
        public static int WaitAny(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (timeout <= TimeSpan.Zero) return WaitHandle.WaitTimeout;

            bool allProvideWaitHandle = true;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;

                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length];
                for (int i = 0; i < handles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                return WaitHandle.WaitAny(handles, timeout);
            }
            else
            {
                Task[] tasks = GetWaitingTasksWithTimeout(asyncWaitHandles, timeout, out var timerCts);
                using (timerCts)
                {
                    Task.WhenAny(tasks).WaitFor();
                    timerCts.Cancel();
                    return FindSignalledIndex(tasks, tasks.Length - 1);
                }
            }
        }

        /// <summary>
        /// Asynchronously waits until any of the provided <see cref="IAsyncWaitHandle"/> instances is signalled
        /// or the <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns>The zero-based index of the first signalled handle, or <see cref="WaitHandle.WaitTimeout"/> if timed out.</returns>
        public static async Task<int> WaitAnyAsync(TimeSpan timeout, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (timeout <= TimeSpan.Zero) return WaitHandle.WaitTimeout;

            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;
            }

            Task[] tasks = GetWaitingTasksWithTimeout(asyncWaitHandles, timeout, out var timerCts);
            using (timerCts)
            {
                await Task.WhenAny(tasks).ConfiguredAwait();
                timerCts.Cancel();
                return FindSignalledIndex(tasks, tasks.Length - 1);
            }
        }

        /// <summary>
        /// Synchronously waits until any of the provided <see cref="IAsyncWaitHandle"/> instances is signalled,
        /// the <paramref name="timeout"/> elapses, or the <paramref name="token"/> is cancelled.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="token">A cancellation token to abort the wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns>The zero-based index of the first signalled handle, or <see cref="WaitHandle.WaitTimeout"/> if timed out or cancelled.</returns>
        public static int WaitAny(TimeSpan timeout, CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (token.IsCancellationRequested) return WaitHandle.WaitTimeout;
            if (timeout <= TimeSpan.Zero) return WaitHandle.WaitTimeout;

            bool allProvideWaitHandle = true;
            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;

                allProvideWaitHandle &= asyncWaitHandles[i].TryConvertToWaitHandle(out _);
            }

            if (allProvideWaitHandle)
            {
                WaitHandle[] handles = new WaitHandle[asyncWaitHandles.Length + 1];
                for (int i = 0; i < asyncWaitHandles.Length; i++)
                {
                    asyncWaitHandles[i].TryConvertToWaitHandle(out handles[i]);
                }
                handles[handles.Length - 1] = token.WaitHandle;
                var index = WaitHandle.WaitAny(handles, timeout);
                if (index == handles.Length - 1) index = WaitHandle.WaitTimeout;
                return index;
            }
            else
            {
                Task[] tasks = GetWaitingTasksWithTimeout(asyncWaitHandles, timeout, out var timerCts);
                using (timerCts)
                {
                    // Cancellation is expected here and simply ends the wait.
                    try { Task.WhenAny(tasks).Wait(token); } catch (OperationCanceledException) { }
                    timerCts.Cancel();
                    return FindSignalledIndex(tasks, tasks.Length - 1);
                }
            }
        }

        /// <summary>
        /// Asynchronously waits until any of the provided <see cref="IAsyncWaitHandle"/> instances is signalled,
        /// the <paramref name="timeout"/> elapses, or the <paramref name="token"/> is cancelled.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="token">A cancellation token to abort the wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns>The zero-based index of the first signalled handle, or <see cref="WaitHandle.WaitTimeout"/> if timed out or cancelled.</returns>
        public static async Task<int> WaitAnyAsync(TimeSpan timeout, CancellationToken token, params IAsyncWaitHandle[] asyncWaitHandles)
        {
            if (token.IsCancellationRequested) return WaitHandle.WaitTimeout;
            if (timeout <= TimeSpan.Zero) return WaitHandle.WaitTimeout;

            for (int i = 0; i < asyncWaitHandles.Length; i++)
            {
                if (!asyncWaitHandles[i].WouldWait()) return i;
            }

            Task[] tasks = GetWaitingTasksWithTimeout(asyncWaitHandles, timeout, out var timerCts);
            using (timerCts)
            {
                await Task.WhenAny(tasks).TryWaitAsync(token).ConfiguredAwait();
                timerCts.Cancel();
                return FindSignalledIndex(tasks, tasks.Length - 1);
            }
        }

        /// <summary>
        /// A pre-allocated <see cref="IAsyncWaitHandle"/> backed by <see cref="Task.CompletedTask"/> that never blocks.
        /// Returned by <see cref="FromTask"/> when the supplied task is already completed.
        /// </summary>
        public static IAsyncWaitHandle NoWaitingHandle { get; } = new AsyncWaitHandle(Task.CompletedTask);

        /// <summary>
        /// Returns the index of the first task that is genuinely signalled, i.e. ran to completion.
        /// Faulted and cancelled tasks are skipped: <see cref="Task.IsCompleted"/> is also true for
        /// those, so a naive check would report a failed handle as signalled.
        /// Faults are observed to avoid <see cref="TaskScheduler.UnobservedTaskException"/>.
        /// </summary>
        /// <param name="tasks">The tasks to scan.</param>
        /// <param name="count">Number of leading entries to consider (excludes an appended timeout task).</param>
        /// <returns>The index of the first signalled task, or <see cref="WaitHandle.WaitTimeout"/>.</returns>
        private static int FindSignalledIndex(Task[] tasks, int count)
        {
            int result = WaitHandle.WaitTimeout;
            for (int i = 0; i < count; i++)
            {
                var task = tasks[i];
                if (!task.IsCompleted) continue;
                if (task.IsFaulted)
                {
                    _ = task.Exception; // observe, so it cannot resurface as unobserved
                    continue;
                }
                if (task.IsCanceled) continue;
                if (result == WaitHandle.WaitTimeout) result = i;
            }
            return result;
        }

        /// <summary>
        /// Builds the task array for a timeout-based wait, together with a source that stops the
        /// timer once the wait is over. Without cancelling it, an abandoned <see cref="Task.Delay(TimeSpan)"/>
        /// keeps a timer armed for the full duration even after another handle already won.
        /// </summary>
        private static Task[] GetWaitingTasksWithTimeout(IAsyncWaitHandle[] asyncWaitHandles, TimeSpan timeout, out CancellationTokenSource timerCts)
        {
            timerCts = new CancellationTokenSource();
            return asyncWaitHandles.GetWaitingTasks(Task.Delay(timeout, timerCts.Token));
        }

        /// <summary>
        /// Creates an <see cref="IAsyncWaitHandle"/> from the given <paramref name="task"/>.
        /// If the task is already completed, <see cref="NoWaitingHandle"/> is returned to avoid allocations.
        /// </summary>
        /// <param name="task">The task to wrap.</param>
        /// <returns>An <see cref="IAsyncWaitHandle"/> that is signalled when the task completes.</returns>
        public static IAsyncWaitHandle FromTask(Task task) => task.IsCompleted ? NoWaitingHandle : new AsyncWaitHandle(task);

        #endregion static

        private Task task;
        private EventWaitHandle eventWaitHandle = null;

        private AsyncWaitHandle(Task task)
        {
            this.task = task;
        }

        /// <inheritdoc/>
        public Task WaitingTask => task;

        /// <inheritdoc/>
        public Task<bool> WaitAsync()
        {
            return task.TryWaitAsync();
        }

        /// <inheritdoc/>
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return task.TryWaitAsync(timeout);
        }

        /// <inheritdoc/>
        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return task.TryWaitAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return task.TryWaitAsync(timeout, cancellationToken);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Blocks the calling thread until the wrapped task completes.
        /// <para>
        /// Do NOT call this from a single-threaded <see cref="SynchronizationContext"/> (e.g.
        /// <see cref="SingleThreadSynchronizationContext"/>) unless the wrapped task is known not to
        /// require that context to complete. If the task's continuation is scheduled back onto the
        /// blocked thread, it can never run and the wait deadlocks.
        /// </para>
        /// <para>
        /// This cannot be mitigated from inside this method: the context is captured when the awaited
        /// task is created, long before this call. Use <see cref="WaitAsync()"/> instead, or suspend
        /// the context around the invocation that creates the task.
        /// </para>
        /// </remarks>
        public bool Wait()
        {
            task.WaitFor();
            return IsSignalled();
        }

        /// <inheritdoc/>
        /// <remarks>Blocks the calling thread. See <see cref="Wait()"/> for the deadlock caveat.</remarks>
        public bool Wait(TimeSpan timeout)
        {
            try
            {
                if (!task.Wait(ToMilliseconds(timeout))) return false;
            }
            catch (AggregateException)
            {
                // Contract: a faulted task counts as an external interruption, not as signalled.
                return false;
            }
            return IsSignalled();
        }

        /// <inheritdoc/>
        /// <remarks>Blocks the calling thread. See <see cref="Wait()"/> for the deadlock caveat.</remarks>
        public bool Wait(CancellationToken cancellationToken)
        {
            try
            {
                task.Wait(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (AggregateException)
            {
                // Contract: a faulted task counts as an external interruption, not as signalled.
                return false;
            }
            return IsSignalled();
        }

        /// <inheritdoc/>
        /// <remarks>Blocks the calling thread. See <see cref="Wait()"/> for the deadlock caveat.</remarks>
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                if (!task.Wait(ToMilliseconds(timeout), cancellationToken)) return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (AggregateException)
            {
                // Contract: a faulted task counts as an external interruption, not as signalled.
                return false;
            }
            return IsSignalled();
        }

        // A completed task only counts as signalled if it ran to completion:
        // IsCompleted is also true for faulted and cancelled tasks.
        private bool IsSignalled() => !task.IsCanceled && !task.IsFaulted && task.IsCompleted;

        /// <summary>
        /// Converts a timeout to milliseconds for Task.Wait, saturating instead of overflowing.
        /// A plain (int) cast would wrap around for timeouts beyond ~24.8 days and produce an
        /// invalid (possibly negative) value.
        /// </summary>
        private static int ToMilliseconds(TimeSpan timeout)
        {
            if (timeout == Timeout.InfiniteTimeSpan) return Timeout.Infinite;
            if (timeout <= TimeSpan.Zero) return 0;

            double ms = timeout.TotalMilliseconds;
            return ms >= int.MaxValue ? int.MaxValue : (int)ms;
        }

        /// <inheritdoc/>
        public bool WouldWait()
        {
            return !task.IsCompleted;
        }

        /// <inheritdoc/>
        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            var existing = Volatile.Read(ref eventWaitHandle);
            if (existing == null)
            {
                // Only allocate when the field is actually unset. The candidate is created outside
                // the CompareExchange, so a losing thread must dispose its instance, otherwise the
                // OS handle would stay alive until finalization.
                var candidate = new EventWaitHandle(task.IsCompleted, EventResetMode.ManualReset);
                existing = Interlocked.CompareExchange(ref eventWaitHandle, candidate, null);
                if (existing == null)
                {
                    existing = candidate;
                    if (!task.IsCompleted) task.ContinueWith(_ => candidate.Set());
                }
                else
                {
                    candidate.Dispose();
                }
            }

            waitHandle = existing;
            return true;
        }

        /// <summary>
        /// Implicitly converts a <see cref="Task"/> to an <see cref="AsyncWaitHandle"/>.
        /// </summary>
        /// <param name="task">The task to wrap.</param>
        public static implicit operator AsyncWaitHandle(Task task) => new AsyncWaitHandle(task);
    }
}