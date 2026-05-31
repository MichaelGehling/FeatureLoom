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
                for (int i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
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
            for (int i = 0; i < tasks.Length; i++)
            {
                if (tasks[i].IsCompleted) return i;
            }
            return WaitHandle.WaitTimeout;
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
                try { Task.WhenAny(tasks).Wait(token); } catch (OperationCanceledException) { }
                for (int i = 0; i < tasks.Length; i++)
                {
                    if (tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
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
            for (int i = 0; i < tasks.Length; i++)
            {
                if (tasks[i].IsCompleted) return i;
            }
            return WaitHandle.WaitTimeout;
        }

        /// <summary>
        /// Synchronously waits until any of the provided <see cref="IAsyncWaitHandle"/> instances is signalled
        /// or the <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="timeout">Maximum time to wait.</param>
        /// <param name="asyncWaitHandles">The handles to wait for.</param>
        /// <returns>The zero-based index of the first signalled handle, or <see cref="WaitHandle.WaitTimeout"/> if timed out.</returns>
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
                Task[] tasks = asyncWaitHandles.GetWaitingTasks(Task.Delay(timeout));
                Task.WhenAny(tasks).WaitFor();
                for (int i = 0; i < tasks.Length - 1; i++)
                {
                    if (tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
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

            Task[] tasks = asyncWaitHandles.GetWaitingTasks(Task.Delay(timeout));
            await Task.WhenAny(tasks).ConfiguredAwait();
            for (int i = 0; i < tasks.Length - 1; i++)
            {
                if (tasks[i].IsCompleted) return i;
            }
            return WaitHandle.WaitTimeout;
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
                Task[] tasks = asyncWaitHandles.GetWaitingTasks(Task.Delay(timeout));
                try { Task.WhenAny(tasks).Wait(token); } catch (OperationCanceledException) { }
                for (int i = 0; i < tasks.Length - 1; i++)
                {
                    if (tasks[i].IsCompleted) return i;
                }
                return WaitHandle.WaitTimeout;
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

            Task[] tasks = asyncWaitHandles.GetWaitingTasks(Task.Delay(timeout));
            await Task.WhenAny(tasks).TryWaitAsync(token).ConfiguredAwait();
            for (int i = 0; i < tasks.Length - 1; i++)
            {
                if (tasks[i].IsCompleted) return i;
            }
            return WaitHandle.WaitTimeout;
        }

        /// <summary>
        /// A pre-allocated <see cref="IAsyncWaitHandle"/> backed by <see cref="Task.CompletedTask"/> that never blocks.
        /// Returned by <see cref="FromTask"/> when the supplied task is already completed.
        /// </summary>
        public static IAsyncWaitHandle NoWaitingHandle { get; } = new AsyncWaitHandle(Task.CompletedTask);

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
        public bool Wait()
        {
            task.WaitFor();
            return !task.IsCanceled && !task.IsFaulted && task.IsCompleted;
        }

        /// <inheritdoc/>
        public bool Wait(TimeSpan timeout)
        {
            return task.Wait(timeout);
        }

        /// <inheritdoc/>
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
            return !task.IsCanceled && !task.IsFaulted && task.IsCompleted;
        }

        /// <inheritdoc/>
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                return task.Wait((int)timeout.TotalMilliseconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public bool WouldWait()
        {
            return !task.IsCompleted;
        }

        /// <inheritdoc/>
        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            if (eventWaitHandle == null &&
                null == Interlocked.CompareExchange(ref eventWaitHandle, new EventWaitHandle(task.IsCompleted, EventResetMode.ManualReset), null))
            {
                if (!task.IsCompleted)
                {
                    task.ContinueWith(t => eventWaitHandle.Set());
                }
            }

            waitHandle = eventWaitHandle;
            return true;
        }

        /// <summary>
        /// Implicitly converts a <see cref="Task"/> to an <see cref="AsyncWaitHandle"/>.
        /// </summary>
        /// <param name="task">The task to wrap.</param>
        public static implicit operator AsyncWaitHandle(Task task) => new AsyncWaitHandle(task);
    }
}