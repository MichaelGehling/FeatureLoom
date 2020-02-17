using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public class AsyncManualResetEvent : IAsyncManualResetEvent
    {
        volatile bool taskUsed = false;
        volatile TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
        ManualResetEventSlim mre = new ManualResetEventSlim(false, 0);

        public AsyncManualResetEvent()
        {
        }

        public AsyncManualResetEvent(bool initialState)
        {
            if (initialState) Set();
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
        public bool Set()
        {
            if(mre.IsSet) return false;

            mre.Set();
            if (taskUsed)
            {
                tcs.TrySetResult(true);
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Reset()
        {
            if (!mre.IsSet) return false;

            mre.Reset();
            var oldTcs = this.tcs;
            if (oldTcs.Task.IsCompleted)
            {    
                Interlocked.CompareExchange(ref this.tcs, new TaskCompletionSource<bool>(), oldTcs);                
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