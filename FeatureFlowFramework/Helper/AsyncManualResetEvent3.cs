using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public class AsyncManualResetEvent3 : IAsyncWaitHandle
    {
        volatile bool taskUsed = false;
        volatile TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        ManualResetEventSlim mre = new ManualResetEventSlim(false);

        public AsyncManualResetEvent3()
        {
        }

        public AsyncManualResetEvent3(bool initialState)
        {
            if (initialState) Set();
        }
        
        public bool IsSet => mre.IsSet;

        public Task WaitingTask
        {
            get
            {
                taskUsed = true;
                return tcs.Task;
            }
        }

        public IAsyncWaitHandle AsyncWaitHandle => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync()
        {
            if (mre.IsSet) return Task.FromResult(true);
            else
            {
                taskUsed = true;                
                return tcs.Task.WaitAsync();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            if (mre.IsSet) return Task.FromResult(true);
            else
            {
                taskUsed = true;                
                return tcs.Task.WaitAsync(timeout);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            if (mre.IsSet) return Task.FromResult(true);
            else
            {
                taskUsed = true;                
                return tcs.Task.WaitAsync(cancellationToken);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (mre.IsSet) return Task.FromResult(true);
            else
            {
                taskUsed = true;
                return tcs.Task.WaitAsync(timeout, cancellationToken);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait()
        {
            mre.Wait();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout)
        {
            return mre.Wait(timeout);            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(CancellationToken cancellationToken)
        {
            mre.Wait(cancellationToken);
            return !cancellationToken.IsCancellationRequested;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return mre.Wait(timeout, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set()
        {
            if(mre.IsSet) return;
            mre.Set();
            if (taskUsed)
            {
                tcs.TrySetResult(true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            if (!mre.IsSet) return;
            if(taskUsed)
            {
                TaskCompletionSource<bool> oldTcs, newTcs;
                do
                {
                    oldTcs = this.tcs;
                    newTcs = new TaskCompletionSource<bool>();
                }
                while(!this.tcs.Task.IsCompleted && this.tcs != Interlocked.CompareExchange(ref this.tcs, newTcs, oldTcs));
            }
            mre.Reset();
        }

        public void SetAndReset()
        {
            Set();
            Reset();
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