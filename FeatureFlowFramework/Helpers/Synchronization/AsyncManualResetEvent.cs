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
        private ManualResetEventSlim mre_active = new ManualResetEventSlim(false, 0);
        private ManualResetEventSlim mre_wakingUp = new ManualResetEventSlim(true, 0);

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
            var mre = mre_active;
            Thread.MemoryBarrier();
            if(isSet) return true;
            Thread.MemoryBarrier();
            mre.Wait();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout)
        {
            if(isSet) return true;
            if(timeout <= TimeSpan.Zero) return false;
            return mre_active.Wait(timeout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (isSet) return true;            
            mre_active.Wait(cancellationToken);
            return !cancellationToken.IsCancellationRequested;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;
            if (isSet) return true;            
            if (timeout <= TimeSpan.Zero) return false;
            return mre_active.Wait(timeout, cancellationToken);
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

            Thread.MemoryBarrier();
            isSet = true;
            Thread.MemoryBarrier();

            if (isTaskUsed) tcs.SetResult(true);

            var mreTemp = mre_active;
            mre_active = mre_wakingUp;
            mre_wakingUp = mreTemp;
            Thread.MemoryBarrier();
            mre_wakingUp.Set();
            Thread.MemoryBarrier();
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

            mre_active.Reset();

            if (tcs.Task.IsCompleted)
            {
                //isTaskUsed = false; // TODO: Still in some cases it seems that the task completion source is not renewed when it should.
                Thread.MemoryBarrier();
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            Thread.MemoryBarrier();
            isSet = false;
            Thread.MemoryBarrier();

            barrier = BARRIER_OPEN;
            return true;
        }

        /// <summary>
        /// Everyone waiting for this event is woken up, by setting and resetting in one step.
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
            Thread.MemoryBarrier();
            isSet = true;
            Thread.MemoryBarrier();
            if(isTaskUsed) tcs.SetResult(true);
            var mreTemp = mre_active;
            mre_active = mre_wakingUp;
            mre_wakingUp = mreTemp;
            mre_wakingUp.Set();

            //RESET
            mre_active.Reset();
            if(tcs.Task.IsCompleted)
            {
                //isTaskUsed = false;  // TODO: Still in some cases it seems that the task completion source is not renewed when it should.
                Thread.MemoryBarrier();
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            Thread.MemoryBarrier();
            isSet = false;
            Thread.MemoryBarrier();

            barrier = BARRIER_OPEN;
        }

        public bool WouldWait()
        {
            return !isSet;
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            waitHandle = mre_active.WaitHandle;
            return true;
        }
    }
}