using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public class AsyncWaitHandle : IAsyncWaitHandle
    {
        private IAsyncWaitHandleSource source;
        private static readonly IAsyncWaitHandle noWaitingHandle = new AsyncManualResetEvent(false);

        public AsyncWaitHandle(IAsyncWaitHandleSource source)
        {
            this.source = source;
        }

        public static IAsyncWaitHandle NoWaitingHandle => noWaitingHandle;

        public Task WaitingTask => source.WaitingTask;

        public Task<bool> WaitAsync()
        {
            return WaitingTask.WaitAsync();
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitingTask.WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return WaitingTask.WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return WaitingTask.WaitAsync(timeout, cancellationToken);
        }

        public bool Wait()
        {
            var task = WaitingTask;
            task.Wait();
            return !task.IsCanceled && !task.IsFaulted && task.IsCompleted;
        }

        public bool Wait(TimeSpan timeout)
        {
            return WaitingTask.Wait(timeout);
        }

        public bool Wait(CancellationToken cancellationToken)
        {
            var task = WaitingTask;
            task.Wait(cancellationToken);
            return !task.IsCanceled && !task.IsFaulted && task.IsCompleted;
        }

        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return WaitingTask.Wait((int)timeout.TotalMilliseconds, cancellationToken);
        }
    }
}
