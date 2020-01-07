using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public class AsyncManualResetEvent : IAsyncWaitHandleSource, IAsyncWaitHandle
    {
        private volatile TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

        public AsyncManualResetEvent()
        {
        }

        public AsyncManualResetEvent(bool initialState)
        {
            if (initialState) Set();
        }

        public bool IsSet => tcs.Task.IsCompleted;

        public Task WaitingTask => tcs.Task;

        public IAsyncWaitHandle AsyncWaitHandle => this;

        public Task<bool> WaitAsync()
        {
            return tcs.Task.WaitAsync();
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return tcs.Task.WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return tcs.Task.WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return tcs.Task.WaitAsync(timeout, cancellationToken);
        }

        public bool Wait()
        {
            var task = tcs.Task;
            task.Wait();
            return !task.IsCanceled && !task.IsFaulted && task.IsCompleted;
        }

        public bool Wait(TimeSpan timeout)
        {
            return tcs.Task.Wait(timeout);
        }

        public bool Wait(CancellationToken cancellationToken)
        {
            var task = tcs.Task;
            task.Wait(cancellationToken);
            return !task.IsCanceled && !task.IsFaulted && task.IsCompleted;
        }

        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return tcs.Task.Wait((int)timeout.TotalMilliseconds, cancellationToken);
        }

        public void Set()
        {
            if(IsSet) return;
            tcs.TrySetResult(true);
        }

        public void Reset()
        {
            if (!IsSet) return;
            TaskCompletionSource<bool> oldTcs, newTcs;
            do
            {
                oldTcs = this.tcs;
                newTcs = new TaskCompletionSource<bool>();
            }
            while (IsSet && this.tcs != Interlocked.CompareExchange(ref this.tcs, newTcs, oldTcs));
        }

        public void SetAndReset()
        {
            Set();
            Reset();
        }
    }
}