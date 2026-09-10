using FeatureLoom.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Synchronization
{
    /// <summary>
    /// Extension methods to wait for tasks with timeout and cancellation support,
    /// to block on tasks and to temporarily suspend the current <see cref="SynchronizationContext"/>.
    /// </summary>
    public static class TaskExtensions
    {
        // Same upper bound as used by Task.Delay / Task.WaitAsync.
        private const long MaxTimeoutMilliseconds = uint.MaxValue - 1;

        /// <summary>
        /// Temporarily removes the <see cref="SynchronizationContext"/> from the current thread and restores it
        /// when the returned <see cref="SynchronizationContextRestorer"/> is disposed.
        /// Use it around blocking calls (e.g. <see cref="WaitFor(Task, bool?)"/>) to avoid the classic
        /// sync-over-async deadlock, where the awaited continuation cannot resume because it is queued
        /// to a context whose only thread is blocked.
        /// </summary>
        /// <param name="context">Ignored. The context that gets restored is always the one that was current
        /// at the time of the call, so that misuse cannot install a foreign context.</param>
        /// <returns>A disposable restorer. Intended to be used with a using statement.</returns>
        public static SynchronizationContextRestorer Suspend(this SynchronizationContext context)
        {
            var current = SynchronizationContext.Current;
            // Nothing to suspend, so return an inactive restorer that does nothing on dispose.
            if (current == null) return default;

            SynchronizationContext.SetSynchronizationContext(null);
            return new SynchronizationContextRestorer(current);
        }

        /// <summary>
        /// Restores a previously suspended <see cref="SynchronizationContext"/> on dispose.
        /// Created by <see cref="Suspend(SynchronizationContext)"/>.
        /// </summary>
        /// <remarks>
        /// Must be disposed on the same thread that created it, otherwise it would set the context on a
        /// foreign thread. A default instance is inactive and does nothing on dispose.
        /// </remarks>
        public readonly struct SynchronizationContextRestorer : IDisposable
        {
            private readonly SynchronizationContext context;
            // Distinguishes "restore null" from "do nothing", so that a default instance stays harmless.
            private readonly bool active;

            /// <summary>
            /// Creates an active restorer for the given context.
            /// </summary>
            /// <param name="context">The context to restore on dispose. May be null.</param>
            public SynchronizationContextRestorer(SynchronizationContext context)
            {
                this.context = context;
                this.active = true;
            }

            /// <summary>
            /// Restores the captured <see cref="SynchronizationContext"/> on the current thread,
            /// unless it is already the current one or this instance is inactive.
            /// </summary>
            public void Dispose()
            {
                if (!active) return;
                if (SynchronizationContext.Current != context) SynchronizationContext.SetSynchronizationContext(context);
            }
        }

        /// <summary>
        /// Blocks the calling thread until the task completes.
        /// </summary>
        /// <param name="task">The task to wait for.</param>
        /// <param name="unwrapException">If true, the original exception of a failed task is thrown.
        /// If false, it is wrapped in an <see cref="AggregateException"/>.
        /// If null (default), <see cref="AwaitConfig.UnwrapExceptionsByDefault"/> is used.</param>
        /// <remarks>
        /// This is a blocking sync-over-async call. If the calling thread has a <see cref="SynchronizationContext"/>
        /// that runs continuations on that very thread (UI thread, classic ASP.NET), and the awaited code resumes on
        /// the captured context, this deadlocks. Prefer awaiting the task.
        /// If blocking is unavoidable, <see cref="Suspend(SynchronizationContext)"/> must wrap the <i>invocation</i> of the
        /// async method, not just the wait, because the continuation captures the context when the await is reached:
        /// <code>using (SynchronizationContext.Current.Suspend()) DoSomethingAsync().WaitFor();</code>
        /// </remarks>
        public static void WaitFor(this Task task, bool? unwrapException = null)
        {
            if (unwrapException ?? AwaitConfig.UnwrapExceptionsByDefault) task.GetAwaiter().GetResult();
            else task.Wait();
        }

        /// <summary>
        /// Blocks the calling thread until the task completes and returns its result.
        /// </summary>
        /// <typeparam name="T">The result type of the task.</typeparam>
        /// <param name="task">The task to wait for.</param>
        /// <param name="unwrapException">If true, the original exception of a failed task is thrown.
        /// If false, it is wrapped in an <see cref="AggregateException"/>.
        /// If null (default), <see cref="AwaitConfig.UnwrapExceptionsByDefault"/> is used.</param>
        /// <returns>The result of the task.</returns>
        /// <remarks>Blocking sync-over-async call, see <see cref="WaitFor(Task, bool?)"/> for the deadlock warning.</remarks>
        public static T WaitFor<T>(this Task<T> task, bool? unwrapException = null)
        {
            if (unwrapException ?? AwaitConfig.UnwrapExceptionsByDefault) return task.GetAwaiter().GetResult();
            else return task.Result;
        }

        /// <summary>
        /// Blocks the calling thread until the task completes and splits its result tuple
        /// into a success flag and an output value.
        /// </summary>
        /// <typeparam name="OUT">The type of the output value.</typeparam>
        /// <param name="task">The task to wait for.</param>
        /// <param name="result">The output value of the tuple. Only meaningful if true is returned.</param>
        /// <param name="unwrapException">If true, the original exception of a failed task is thrown.
        /// If false, it is wrapped in an <see cref="AggregateException"/>.
        /// If null (default), <see cref="AwaitConfig.UnwrapExceptionsByDefault"/> is used.</param>
        /// <returns>The boolean part of the result tuple.</returns>
        /// <remarks>Blocking sync-over-async call, see <see cref="WaitFor(Task, bool?)"/> for the deadlock warning.</remarks>
        public static bool WaitFor<OUT>(this Task<(bool, OUT)> task, out OUT result, bool? unwrapException = null)
        {
            if (unwrapException ?? AwaitConfig.UnwrapExceptionsByDefault) return task.GetAwaiter().GetResult().TryOut(out result);
            else return task.Result.TryOut(out result);
        }

        /// <summary>
        /// Waits for the task to complete. Never throws: returns false if the task was cancelled or faulted.
        /// </summary>
        public async static Task<bool> TryWaitAsync(this Task task)
        {
            if (task.IsCanceled || task.IsFaulted) return false;
            else if (task.IsCompleted) return true;

            try
            {
                await task.ConfiguredAwait();
            }
            catch when (task.IsFaulted || task.IsCanceled)
            {
                // Cancellation/fault of the awaited task is reported via the return value, not by throwing.
                // Any other exception is not ours to swallow and propagates.
            }

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }

        /// <summary>
        /// Waits for the task to complete within the given timeout. Never throws on task failure:
        /// returns false on timeout or if the task was cancelled or faulted.
        /// A timeout of zero means not waiting at all, <see cref="Timeout.InfiniteTimeSpan"/> means waiting infinitely.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The timeout is negative and not <see cref="Timeout.InfiniteTimeSpan"/>, or it exceeds the maximum supported value.
        /// </exception>
        public static Task<bool> TryWaitAsync(this Task task, TimeSpan timeout)
        {
            ValidateTimeout(timeout, nameof(timeout));
            return TryWaitWithTimeoutAsync(task, timeout);
        }

        private async static Task<bool> TryWaitWithTimeoutAsync(Task task, TimeSpan timeout)
        {
            if (task.IsCanceled || task.IsFaulted) return false;
            else if (task.IsCompleted) return true;
            if (timeout == TimeSpan.Zero) return false;

#if NET8_0_OR_GREATER
            try
            {
                await task.WaitAsync(timeout).ConfiguredAwait();
            }
            catch (Exception e) when (e is TimeoutException || e is OperationCanceledException || task.IsFaulted || task.IsCanceled)
            {
                task.ObserveException();
                return false;
            }
            return true;
#else
            using (var delayCts = new CancellationTokenSource())
            {
                var finished = await Task.WhenAny(task, Task.Delay(timeout, delayCts.Token)).ConfiguredAwait();
                if (finished != task)
                {
                    task.ObserveException();
                    return false;
                }
                delayCts.Cancel(); // stop the timer, otherwise it would be kept alive until the timeout elapsed
            }

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
#endif
        }

        /// <summary>
        /// Waits for the task to complete unless the cancellation token is cancelled. Never throws:
        /// returns false on cancellation or if the task was cancelled or faulted.
        /// </summary>
        public async static Task<bool> TryWaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if (task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if (task.IsCompleted) return true;

#if NET8_0_OR_GREATER
            try
            {
                await task.WaitAsync(cancellationToken).ConfiguredAwait();
            }
            catch (Exception e) when (e is OperationCanceledException || task.IsFaulted || task.IsCanceled)
            {
                task.ObserveException();
                return false;
            }
            return true;
#else
            // RunContinuationsAsynchronously is essential: otherwise the awaiting continuation would run inline
            // inside CancellationTokenSource.Cancel(), which can deadlock the cancelling thread.
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(false), tcs))
            {
                var finished = await Task.WhenAny(task, tcs.Task).ConfiguredAwait();
                if (finished != task)
                {
                    task.ObserveException();
                    return false;
                }
            }

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
#endif
        }

        /// <summary>
        /// Waits for the task to complete within the given timeout unless the cancellation token is cancelled.
        /// Never throws on task failure: returns false on timeout, on cancellation or if the task was cancelled or faulted.
        /// A timeout of zero means not waiting at all, <see cref="Timeout.InfiniteTimeSpan"/> means waiting infinitely.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The timeout is negative and not <see cref="Timeout.InfiniteTimeSpan"/>, or it exceeds the maximum supported value.
        /// </exception>
        public static Task<bool> TryWaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            ValidateTimeout(timeout, nameof(timeout));
            return TryWaitWithTimeoutAsync(task, timeout, cancellationToken);
        }

        private async static Task<bool> TryWaitWithTimeoutAsync(Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if (task.IsCompleted) return true;
            if (timeout == TimeSpan.Zero) return false;

#if NET8_0_OR_GREATER
            try
            {
                await task.WaitAsync(timeout, cancellationToken).ConfiguredAwait();
            }
            catch (Exception e) when (e is TimeoutException || e is OperationCanceledException || task.IsFaulted || task.IsCanceled)
            {
                task.ObserveException();
                return false;
            }
            return true;
#else
            using (var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delayTask = Task.Delay(timeout, delayCts.Token);
                var finished = await Task.WhenAny(task, delayTask).ConfiguredAwait();
                if (finished != task)
                {
                    task.ObserveException();
                    return false;
                }
                delayCts.Cancel(); // stop the timer, otherwise it would be kept alive until the timeout elapsed
            }

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
#endif
        }

        /// <summary>
        /// Applies the same timeout validation rules as Task.Delay and Task.WaitAsync:
        /// only Timeout.InfiniteTimeSpan is accepted as a negative value.
        /// </summary>
        private static void ValidateTimeout(TimeSpan timeout, string paramName)
        {
            long totalMilliseconds = (long)timeout.TotalMilliseconds;
            if ((totalMilliseconds < 0 && timeout != Timeout.InfiniteTimeSpan) || totalMilliseconds > MaxTimeoutMilliseconds)
            {
                throw new ArgumentOutOfRangeException(paramName, timeout, "The timeout must not be negative (except Timeout.InfiniteTimeSpan) and must not exceed the maximum supported value.");
            }
        }

        /// <summary>
        /// Ensures a (possibly later occurring) fault of the task is observed,
        /// so it does not surface as an UnobservedTaskException when the task is finalized.
        /// </summary>
        private static void ObserveException(this Task task)
        {
            if (task.IsCompleted)
            {
                // Reading the Exception property is what marks a fault as observed.
                // If the task is not faulted, the property is simply null and reading it has no effect.
                _ = task.Exception;
                return;
            }

            // The task may still fault after we stopped waiting for it, so observe it whenever that happens.
            // The continuation only runs on a fault and does nothing but read the Exception property,
            // so running it inline on the completing thread is safe.
            _ = task.ContinueWith(t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Returns a task that completes when the given cancellation token is cancelled.
        /// If the token can never be cancelled, the returned task never completes.
        /// </summary>
        public static Task AsTask(this CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return Task.CompletedTask;

            // RunContinuationsAsynchronously prevents continuations from running inline inside
            // CancellationTokenSource.Cancel(), which would block the cancelling thread.
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!cancellationToken.CanBeCanceled) return tcs.Task;

            var registration = cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs);
            // Release the registration once the task completed, so it is not kept alive by the token source.
            _ = tcs.Task.ContinueWith((_, s) => ((CancellationTokenRegistration)s).Dispose(),
                registration,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
            return tcs.Task;
        }
    }
}