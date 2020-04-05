using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public static class WaitHandleExtensions
    {
        public static async Task<bool> WaitOneAsync(this WaitHandle waitHandle, int millisecondsTimeout, CancellationToken cancellationToken)
        {
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
                result = await tcs.Task;
            }
            catch(Exception e)
            {
                string s = e.ToString();
            }
            finally
            {
                if(registeredHandle != null)
                    registeredHandle.Unregister(null);
                tokenRegistration.Dispose();
            }
            return result;
        }

        public async static Task<bool> WaitOneAsync(this WaitHandle handle, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return await handle.WaitOneAsync(timeout.TotalMilliseconds.ToIntTruncated(), cancellationToken);
        }

        public async static Task<bool> WaitOneAsync(this WaitHandle handle, CancellationToken cancellationToken)
        {
            return await handle.WaitOneAsync(Timeout.Infinite, cancellationToken);
        }

        public static async Task<bool> WaitAllAsync(this WaitHandle[] handles, TimeSpan timeout, CancellationToken cancellationToken)
        {
            bool completed = true;
            Task[] tasks = new Task[handles.Length];
            for(int i = 0; i < handles.Length; i++)
            {
                tasks[i] = handles[i].WaitOneAsync(timeout, cancellationToken);
            }
            await Task.WhenAll(tasks);

            return completed;
        }

        public static async Task<bool> WaitAnyAsync(this WaitHandle[] handles, TimeSpan timeout, CancellationToken cancellationToken)
        {
            bool completed = true;
            Task[] tasks = new Task[handles.Length];
            for(int i = 0; i < handles.Length; i++)
            {
                tasks[i] = handles[i].WaitOneAsync(timeout, cancellationToken);
            }
            await Task.WhenAny(tasks);
            return completed;
        }

        public static async Task<bool> WaitAllAsync(this WaitHandle[] handles, CancellationToken cancellationToken)
        {
            bool completed = true;
            Task[] tasks = new Task[handles.Length];
            for(int i = 0; i < handles.Length; i++)
            {
                tasks[i] = handles[i].WaitOneAsync(cancellationToken);
            }
            await Task.WhenAll(tasks);
            return completed;
        }

        public static async Task<bool> WaitAnyAsync(this WaitHandle[] handles, CancellationToken cancellationToken)
        {
            bool completed = true;
            Task[] tasks = new Task[handles.Length];
            for(int i = 0; i < handles.Length; i++)
            {
                tasks[i] = handles[i].WaitOneAsync(cancellationToken);
            }
            await Task.WhenAny(tasks);
            return completed;
        }
    }
}