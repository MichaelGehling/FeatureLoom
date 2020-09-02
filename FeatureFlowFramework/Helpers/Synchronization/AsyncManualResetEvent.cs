using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public class AsyncManualResetEvent : IAsyncManualResetEvent
    {
        const int BARRIER_OPEN = 0;
        const int BARRIER_CLOSED = 1;        

        private volatile bool isSet = false;
        private volatile bool isTaskUsed = false;        
        private volatile int barrier = BARRIER_OPEN;
        private volatile TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private ManualResetEventSlim mre = new ManualResetEventSlim(false, 0);
        private ManualResetEventSlim mre_backup = new ManualResetEventSlim(false, 0);

        public AsyncManualResetEvent()
        {
        }

        public AsyncManualResetEvent(bool initialState)
        {
            if(initialState) Set();
        }

        public bool IsSet => isSet;

        public Task WaitingTask
        {
            get
            {
                if(isSet) return Task.CompletedTask;

                isTaskUsed = true;
                Thread.MemoryBarrier();
                if (isSet) return Task.CompletedTask;

                return tcs.Task;                
            }
        }

        public IAsyncWaitHandle AsyncWaitHandle => this;

        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait()
        {
            if(isSet) return true;
            mre.Wait();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout)
        {
            if(isSet) return true;
            if(timeout <= TimeSpan.Zero) return false;
            return mre.Wait(timeout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (isSet) return true;            
            mre.Wait(cancellationToken);
            return !cancellationToken.IsCancellationRequested;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (isSet) return true;            
            if (timeout <= TimeSpan.Zero) return false;
            return mre.Wait(timeout, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync()
        {
            if (isSet) return Task.FromResult(true);

            isTaskUsed = true;
            Thread.MemoryBarrier();
            if (isSet) return Task.FromResult(true);

            return tcs.Task;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            if (isSet) return Task.FromResult(true);
            if (timeout <= TimeSpan.Zero) return Task.FromResult(false);

            isTaskUsed = true;
            Thread.MemoryBarrier();
            if (isSet) return Task.FromResult(true);

            return tcs.Task.WaitAsync(timeout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return Task.FromResult(false);
            if (isSet) return Task.FromResult(true);

            isTaskUsed = true;
            Thread.MemoryBarrier();
            if (isSet) return Task.FromResult(true);

            return tcs.Task.WaitAsync(cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {

            if (cancellationToken.IsCancellationRequested) return Task.FromResult(false);
            if (isSet) return Task.FromResult(true);
            if (timeout <= TimeSpan.Zero) return Task.FromResult(false);

            isTaskUsed = true;
            Thread.MemoryBarrier();
            if (isSet) return Task.FromResult(true);

            return tcs.Task.WaitAsync(timeout, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set()
        {
            if (isSet) return false;
            while (barrier == BARRIER_CLOSED || Interlocked.CompareExchange(ref barrier, BARRIER_CLOSED, BARRIER_OPEN) != BARRIER_OPEN) Thread.Yield();
            if (isSet)
            {
                barrier = BARRIER_OPEN;
                return false;
            }

            isSet = true;
            Thread.MemoryBarrier();
            
            if (isTaskUsed) tcs.SetResult(true);

            var mreTemp = mre;
            mre = mre_backup;
            mre_backup = mreTemp;

            mre_backup.Set();

            barrier = BARRIER_OPEN;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Reset()
        {
            if (!isSet) return false;
            while (barrier == BARRIER_CLOSED || Interlocked.CompareExchange(ref barrier, BARRIER_CLOSED, BARRIER_OPEN) != BARRIER_OPEN) Thread.Yield();
            if (!isSet)
            {
                barrier = BARRIER_OPEN;
                return false;
            }

            mre.Reset();

            if (tcs.Task.IsCompleted)
            {
                isTaskUsed = false;
                Thread.MemoryBarrier();
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            Thread.MemoryBarrier();
            isSet = false;

            barrier = BARRIER_OPEN;
            return true;
        }

        /// <summary>
        /// Averyone waiting for this event is woken up, by setting and resetting in one step.
        /// If the AsyncManualResetEvent was already set, nothing happens.        
        /// Note: Avoid calling PulseAll twice directly after each other, because it might happen, that not all are woken up.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PulseAll()
        {
            if(isSet) return;
            while(barrier == BARRIER_CLOSED || Interlocked.CompareExchange(ref barrier, BARRIER_CLOSED, BARRIER_OPEN) != BARRIER_OPEN) Thread.Yield();
            if(isSet)
            {
                barrier = BARRIER_OPEN;
                return;
            }

            //SET
            isSet = true;
            Thread.MemoryBarrier();
            if(isTaskUsed) tcs.SetResult(true);
            var mreTemp = mre;
            mre = mre_backup;
            mre_backup = mreTemp;
            mre_backup.Set();

            //RESET
            mre.Reset();
            if(tcs.Task.IsCompleted)
            {
                isTaskUsed = false;
                Thread.MemoryBarrier();
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            Thread.MemoryBarrier();
            isSet = false;

            barrier = BARRIER_OPEN;
        }

        public bool WouldWait()
        {
            return !isSet;
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            waitHandle = mre.WaitHandle;
            return true;
        }
    }
}