using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public class AsyncManualResetEvent : IAsyncManualResetEvent
    {
        private volatile bool taskUsed = false;
        private volatile TaskCompletionSource<bool> tcs;
        private ManualResetEventSlim mre = new ManualResetEventSlim(false, 0);
        TaskCreationOptions taskCreationOptions = TaskCreationOptions.None;

        public AsyncManualResetEvent()
        {
            tcs = new TaskCompletionSource<bool>(taskCreationOptions);
        }

        public AsyncManualResetEvent(bool initialState = false, bool runContinuationsAsynchronously = false)
        {
            if(runContinuationsAsynchronously) taskCreationOptions = TaskCreationOptions.RunContinuationsAsynchronously;
            tcs = new TaskCompletionSource<bool>(taskCreationOptions);
            if(initialState) Set();
        }

        public bool RunContinuationsAsynchronously
        {
            get => taskCreationOptions == TaskCreationOptions.RunContinuationsAsynchronously;
            set => taskCreationOptions = value ? TaskCreationOptions.RunContinuationsAsynchronously : TaskCreationOptions.None;
        }

        public bool IsSet => mre.IsSet;

        public Task WaitingTask
        {
            get
            {
                if(mre.IsSet) return Task.CompletedTask;
                else
                {
                     taskUsed = true;
                    return tcs.Task;
                }
            }
        }

        public IAsyncWaitHandle AsyncWaitHandle => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync()
        {
            if(mre.IsSet) return Task.FromResult(true);
            else
            {
                taskUsed = true;
                return tcs.Task.WaitAsync();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            if(timeout <= TimeSpan.Zero) return Task.FromResult(false);
            if(mre.IsSet) return Task.FromResult(true);
            else
            {
                taskUsed = true;
                return tcs.Task.WaitAsync(timeout);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            if(cancellationToken.IsCancellationRequested) return Task.FromResult(false);
            if(mre.IsSet) return Task.FromResult(true);
            else
            {
                taskUsed = true;
                return tcs.Task.WaitAsync(cancellationToken);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if(timeout <= TimeSpan.Zero) return Task.FromResult(false);
            if(cancellationToken.IsCancellationRequested) return Task.FromResult(false);
            if(mre.IsSet) return Task.FromResult(true);
            else
            {
                taskUsed = true;
                return tcs.Task.WaitAsync(timeout, cancellationToken);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait()
        {
            if(mre.IsSet) return true;
            mre.Wait();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout)
        {
            if(mre.IsSet) return true;
            if(timeout <= TimeSpan.Zero) return false;
            return mre.Wait(timeout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(CancellationToken cancellationToken)
        {
            if(mre.IsSet) return true;
            if(cancellationToken.IsCancellationRequested) return false;
            mre.Wait(cancellationToken);
            return !cancellationToken.IsCancellationRequested;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if(mre.IsSet) return true;
            if(cancellationToken.IsCancellationRequested) return false;
            if(timeout <= TimeSpan.Zero) return false;
            return mre.Wait(timeout, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set()
        {
            if(mre.IsSet) return false;

            mre.Set();
            if(taskUsed)
            {
                taskUsed = false;                
                tcs.TrySetResult(true);                
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Reset()
        {
            if(!mre.IsSet) return false;

            mre.Reset();
            var oldTcs = this.tcs;
            if(oldTcs.Task.IsCompleted)
            {
                Interlocked.CompareExchange(ref this.tcs, new TaskCompletionSource<bool>(taskCreationOptions), oldTcs);                
            }
            return true;
        }

        public bool WouldWait()
        {
            return !mre.IsSet;
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            waitHandle = mre.WaitHandle;
            return true;
        }
    }
}