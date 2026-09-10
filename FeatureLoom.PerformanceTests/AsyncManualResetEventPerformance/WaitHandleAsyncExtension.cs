using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance
{
    /// <summary>
    /// Provides an asynchronous wait for a classic <see cref="WaitHandle"/>, used to benchmark
    /// <see cref="ManualResetEvent"/> and <see cref="ManualResetEventSlim"/> against
    /// FeatureLoom's AsyncManualResetEvent.
    /// This lives in the benchmark project on purpose: it is only needed to make the competing
    /// primitives awaitable and is not part of the FeatureLoom API.
    /// </summary>
    public static class WaitHandleAsyncExtension
    {
        /// <summary>
        /// Waits asynchronously until the handle is signalled or the wait is cancelled.
        /// </summary>
        /// <param name="handle">The handle to wait for.</param>
        /// <param name="cancellationToken">May cancel the waiting.</param>
        /// <returns>True if the handle was signalled, false if the wait was cancelled.</returns>
        public static Task<bool> WaitOneAsync(this WaitHandle handle, CancellationToken cancellationToken)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));

            if (handle.WaitOne(0)) return Task.FromResult(true);
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(false);

            // RegisterWaitForSingleObject parks the handle on a shared wait thread instead of
            // blocking a pooled thread for the whole duration, so the measurement is not
            // distorted by thread pool scheduling.
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            RegisteredWaitHandle registration = null;
            CancellationTokenRegistration tokenRegistration = default;

            registration = ThreadPool.RegisterWaitForSingleObject(
                handle,
                (state, timedOut) => tcs.TrySetResult(!timedOut),
                null,
                Timeout.Infinite,
                executeOnlyOnce: true);

            if (cancellationToken.CanBeCanceled)
            {
                tokenRegistration = cancellationToken.Register(() => tcs.TrySetResult(false));
            }

            return tcs.Task.ContinueWith(t =>
            {
                // Both registrations must be released, otherwise the wait thread keeps the
                // handle registered and the token keeps the callback alive.
                registration.Unregister(null);
                tokenRegistration.Dispose();
                return t.Result;
            }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
