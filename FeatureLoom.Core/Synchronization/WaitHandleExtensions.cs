using FeatureLoom.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Synchronization
{
    public static class WaitHandleExtensions
    {

        public static Task<bool> WaitOneAsync(this WaitHandle waitHandle, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                return WaitHandle.WaitAny(new[] { waitHandle, cancellationToken.WaitHandle }, millisecondsTimeout) == 0;
            });
        }
        /*{
            bool result = false;
            RegisteredWaitHandle registeredHandle = null;
            CancellationTokenRegistration tokenRegistration = default;
            try
            {
                var tcs = new TaskCompletionSource<bool>();
                registeredHandle = ThreadPool.RegisterWaitForSingleObject(
                    waitHandle,
                    (myTcs, timedOut) => ((TaskCompletionSource<bool>)myTcs).TrySetResult(!timedOut),
                    tcs,
                    millisecondsTimeout,
                    true);

                tokenRegistration = cancellationToken.Register(
                    myTcs => ((TaskCompletionSource<bool>)myTcs).TrySetCanceled(),
                    tcs);
                result = await tcs.Task.ConfiguredAwait();
            }
            catch (Exception e)
            {
                string s = e.ToString();
            }
            finally
            {
                if (registeredHandle != null)
                    registeredHandle.Unregister(null);
                tokenRegistration.Dispose();
            }
            return result;
        }*/

        public static Task<bool> WaitOneAsync(this WaitHandle handle, TimeSpan timeout, CancellationToken cancellationToken)
        {
            //return await handle.WaitOneAsync(timeout.TotalMilliseconds.ToIntTruncated(), cancellationToken).ConfiguredAwait();
            return Task.Run(() => WaitHandle.WaitAny(new[] { handle, cancellationToken.WaitHandle }, timeout) == 0);
        }

        public static Task<bool> WaitOneAsync(this WaitHandle handle, CancellationToken cancellationToken)
        {
            //return await handle.WaitOneAsync(Timeout.Infinite, cancellationToken).ConfiguredAwait();
            return Task.Run(() => WaitHandle.WaitAny(new[] { handle, cancellationToken.WaitHandle }) == 0);
        }

        public static async Task<bool> WaitAllAsync(this WaitHandle[] handles, TimeSpan timeout, CancellationToken cancellationToken)
        {

            bool completed = true;
            Task[] tasks = new Task[handles.Length];
            for (int i = 0; i < handles.Length; i++)
            {
                tasks[i] = handles[i].WaitOneAsync(timeout, cancellationToken);
            }
            await Task.WhenAll(tasks).ConfiguredAwait();

            return completed;
        }

        public static async Task<bool> WaitAnyAsync(this WaitHandle[] handles, TimeSpan timeout, CancellationToken cancellationToken)
        {
            bool completed = true;
            Task[] tasks = new Task[handles.Length];
            for (int i = 0; i < handles.Length; i++)
            {
                tasks[i] = handles[i].WaitOneAsync(timeout, cancellationToken);
            }
            await Task.WhenAny(tasks).ConfiguredAwait();
            return completed;
        }

        public static async Task<bool> WaitAllAsync(this WaitHandle[] handles, CancellationToken cancellationToken)
        {
            bool completed = true;
            Task[] tasks = new Task[handles.Length];
            for (int i = 0; i < handles.Length; i++)
            {
                tasks[i] = handles[i].WaitOneAsync(cancellationToken);
            }
            await Task.WhenAll(tasks).ConfiguredAwait();
            return completed;
        }

        public static async Task<bool> WaitAnyAsync(this WaitHandle[] handles, CancellationToken cancellationToken)
        {
            bool completed = true;
            Task[] tasks = new Task[handles.Length];
            for (int i = 0; i < handles.Length; i++)
            {
                tasks[i] = handles[i].WaitOneAsync(cancellationToken);
            }
            await Task.WhenAny(tasks).ConfiguredAwait();
            return completed;
        }
    }
}