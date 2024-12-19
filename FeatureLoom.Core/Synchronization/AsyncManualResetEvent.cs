using FeatureLoom.Extensions;
using FeatureLoom.Time;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Synchronization
{

    /// <summary>
    /// AsyncManualResetEvent allows status based waiting (sync/async) and signalling between multiple threads.
    /// It can be used as a replacement for the ManualResetEvent/-Slim, but also supporting async waiting.
    /// With an actually waiting thread, performance is comparable to ManualResetEventSlim, setting/resetting 
    /// without any waiting thread and waiting in already set state is significantly faster than ManualResetEventSlim.
    /// </summary>
    public sealed class AsyncManualResetEvent : IAsyncWaitHandle
    {
        private static Task<bool> storedResult_true = Task.FromResult(true);
        private static Task<bool> storedResult_false = Task.FromResult(false);

        private MicroValueLock myLock = new MicroValueLock();

        private volatile bool isSet = false;
        private volatile bool anyAsyncWaiter = false;
        private volatile bool anySyncWaiter = false;
        private volatile byte setCounter = 0;

        /// <summary>
        /// Before actually going to sleep, the waiting thread may yield some cycles.
        /// We don't use spinning to reduce CPU load and it rarely makes a difference.
        /// </summary>
        public ushort YieldCyclesForSyncWait { get; set; } = 100;
        /// <summary>
        /// Before actually going to sleep, the waiting thread may yield some cycles.
        /// We don't use spinning to reduce CPU load and it rarely makes a difference.
        /// NOTE: Yielding the thread contradicts the idea of async computation, and yielding the task is too slow, so the default is 0.
        /// </summary>
        public ushort YieldCyclesForAsyncWait { get; set; } = 0;


        private volatile TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private object monitorObj = new object();
        private EventWaitHandle eventWaitHandle = null;

        /// <summary>
        /// AsyncManualResetEvent allows status based waiting (sync/async) and signalling between multiple threads.
        /// Initial state is not set.
        /// </summary>
        public AsyncManualResetEvent()
        {
        }

        /// <summary>
        /// AsyncManualResetEvent allows status based waiting (sync/async) and signalling between multiple threads.
        /// </summary>
        public AsyncManualResetEvent(bool initialState)
        {
            if (initialState) Set();
        }

        public bool IsSet => isSet;

        /// <summary>
        /// Returns a task that will be completed when the state is set.
        /// </summary>
        public Task WaitingTask
        {
            get
            {
                if (isSet) return storedResult_true;

                anyAsyncWaiter = true;
                var task = tcs.Task;
                if (isSet) return storedResult_true;

                return task;
            }
        }

        /// <summary>
        /// Waits until state is set. If already set, the call returns immediatly.
        /// </summary>
        /// <returns>Always true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait()
        {
            var lastSetCount = setCounter;
            if (isSet) return true;

            for(int i=0; i < YieldCyclesForSyncWait; i++)
            {
                Thread.Yield();
                if (setCounter != lastSetCount) return true;
            }

            anySyncWaiter = true;
            Thread.MemoryBarrier();    
            
            lock (monitorObj)
            {
                if (setCounter != lastSetCount) return true;
                Monitor.Wait(monitorObj);
            }
            return true;
        }

        /// <summary>
        /// Waits until state is set or the timeout exceeds. If already set, the call returns immediatly.
        /// </summary>
        /// <param name="timeout">Timeout to cancel waiting. The cancellation may be later than the defined timeout.</param>
        /// <returns>True if set, false if timeout</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout)
        {
            var lastSetCount = setCounter;
            if (setCounter != lastSetCount) return true;
            if (timeout <= TimeSpan.Zero) return false;

            TimeFrame timer = new TimeFrame(timeout);
            for (int i = 0; i < YieldCyclesForSyncWait; i++)
            {
                Thread.Yield();
                if (setCounter != lastSetCount) return true;
                if (timer.Elapsed()) return false;
            }

            anySyncWaiter = true;
            Thread.MemoryBarrier();

            lock (monitorObj)
            {
                if (setCounter != lastSetCount) return true;
                return Monitor.Wait(monitorObj, timer.Remaining());
            }
        }

        /// <summary>
        /// Waits until state is set or the call is cancelled. If already set, the call returns immediatly.
        /// </summary>
        /// <param name="cancellationToken">May cancel the waiting</param>
        /// <returns>True if set, false if cancelled</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(CancellationToken cancellationToken)
        {
            var lastSetCount = setCounter;
            if (cancellationToken.IsCancellationRequested) return false;
            if (setCounter != lastSetCount) return true;

            for (int i = 0; i < YieldCyclesForSyncWait; i++)
            {
                Thread.Yield();
                if (cancellationToken.IsCancellationRequested) return false;
                if (setCounter != lastSetCount) return true;
            }

            anySyncWaiter = true;
            Thread.MemoryBarrier();            

            do
            {
                using (cancellationToken.Register(Cancellation, this))
                {
                    lock (monitorObj)
                    {
                        if (setCounter != lastSetCount) return true;
                        Monitor.Wait(monitorObj);
                    }
                }
            }
            while (setCounter == lastSetCount && !cancellationToken.IsCancellationRequested);
            return !cancellationToken.IsCancellationRequested;
        }

        /// <summary>
        /// Waits until state is set, the call is cancelled or the timeout exceeds. If already set, the call returns immediatly.
        /// </summary>
        /// <param name="timeout">Timeout to cancel waiting. The cancallation may be later than the defined timeout.</param>
        /// <param name="cancellationToken">May cancel the waiting</param>
        /// <returns>True if set, false if cancelled or timeout exceeded</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var lastSetCount = setCounter;
            if (cancellationToken.IsCancellationRequested) return false;
            if (isSet) return true;            
            if (timeout <= TimeSpan.Zero) return false;

            TimeFrame timer = new TimeFrame(timeout);
            for (int i = 0; i < YieldCyclesForSyncWait; i++)
            {
                Thread.Yield();
                if (cancellationToken.IsCancellationRequested) return false;
                if (setCounter != lastSetCount) return true;
                if (timer.Elapsed()) return false;
            }

            anySyncWaiter = true;
            Thread.MemoryBarrier();            

            do
            {
                using (cancellationToken.Register(Cancellation, this))
                {
                    lock (monitorObj)
                    {
                        if (setCounter != lastSetCount) return true;
                        if (!Monitor.Wait(monitorObj, timer.Remaining())) return false;
                    }
                }
            }
            while (setCounter == lastSetCount && !cancellationToken.IsCancellationRequested);
            return !cancellationToken.IsCancellationRequested;
        }

        private void Cancellation(object self)
        {
            var _waitingLockObject = self.As<AsyncManualResetEvent>().monitorObj;
            lock (_waitingLockObject) Monitor.PulseAll(_waitingLockObject);
        }

        /// <summary>
        /// Waits asynchronously until state is set. If already set, the call returns immediatly.
        /// </summary>
        /// <returns>Always true</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync()
        {
            var lastSetCount = setCounter;
            if (isSet) return storedResult_true;

            for (int i = 0; i < YieldCyclesForAsyncWait; i++)
            {
                Thread.Yield();
                if (setCounter != lastSetCount) return storedResult_true;
            }

            anyAsyncWaiter = true;
            Thread.MemoryBarrier();

            var task = tcs.Task;
            if (setCounter != lastSetCount) return storedResult_true;
            return task;
        }

        /// <summary>
        /// Waits asynchronously until state is set or the timeout exceeds. If already set, the call returns immediatly.
        /// </summary>
        /// <param name="timeout">Timeout to cancel waiting. The cancallation may be later than the defined timeout.</param>
        /// <returns>True if set, false if timeout</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            var lastSetCount = setCounter;
            if (isSet) return storedResult_true;
            if (timeout <= TimeSpan.Zero) return storedResult_false;

            TimeFrame timer = new TimeFrame(timeout);
            for (int i = 0; i < YieldCyclesForAsyncWait; i++)
            {
                Thread.Yield();
                if (setCounter != lastSetCount) return storedResult_true;
                if (timer.Elapsed()) return storedResult_false;
            }

            anyAsyncWaiter = true;
            Thread.MemoryBarrier();

            var task = tcs.Task.WaitAsync(timeout);
            if (setCounter != lastSetCount) return storedResult_true;
            return task;
        }

        /// <summary>
        /// Waits asynchronously until state is set or the call is cancelled. If already set, the call returns immediatly.
        /// </summary>
        /// <param name="cancellationToken">May cancel the waiting</param>
        /// <returns>True if set, false if cancelled</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            var lastSetCount = setCounter;
            if (cancellationToken.IsCancellationRequested) return storedResult_false;
            if (isSet) return storedResult_true;

            for (int i = 0; i < YieldCyclesForAsyncWait; i++)
            {
                Thread.Yield();
                if (cancellationToken.IsCancellationRequested) return storedResult_false;
                if (setCounter != lastSetCount) return storedResult_true;
            }

            anyAsyncWaiter = true;
            Thread.MemoryBarrier();

            var task = tcs.Task.WaitAsync(cancellationToken);
            if (setCounter != lastSetCount) return storedResult_true;
            return task;
        }

        /// <summary>
        /// Waits asynchronously until state is set, the call is cancelled or the timeout exceeds. If already set, the call returns immediatly.
        /// </summary>
        /// <param name="timeout">Timeout to cancel waiting. The cancallation may be later than the defined timeout.</param>
        /// <param name="cancellationToken">May cancel the waiting</param>
        /// <returns>True if set, false if cancelled or timeout exceeded</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var lastSetCount = setCounter;
            if (cancellationToken.IsCancellationRequested) return storedResult_false;
            if (isSet) return storedResult_true;
            if (timeout <= TimeSpan.Zero) return storedResult_false;

            TimeFrame timer = new TimeFrame(timeout);
            for (int i = 0; i < YieldCyclesForAsyncWait; i++)
            {
                Thread.Yield();
                if (cancellationToken.IsCancellationRequested) return storedResult_false;
                if (setCounter != lastSetCount) return storedResult_true;
                if (timer.Elapsed()) return storedResult_false;
            }

            anyAsyncWaiter = true;
            Thread.MemoryBarrier();

            var task = tcs.Task.WaitAsync(timeout, cancellationToken);
            if (setCounter != lastSetCount) return storedResult_true;
            return task;
        }

        /// <summary>
        /// Sets the event, so that all waiting threads will procede. Threads will not wait until the event is reset, again.
        /// </summary>
        /// <returns>True if event was not set before, false if it was already set.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set()
        {
            if (isSet) return false;
            myLock.Enter();
            if (isSet)
            {
                myLock.Exit();
                return false;
            }

            isSet = true;
            setCounter++;
            Thread.MemoryBarrier();

            if (anyAsyncWaiter)
            {
                anyAsyncWaiter = false;                
                tcs.SetResult(true);
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            if (anySyncWaiter)
            {
                anySyncWaiter = false;
                lock (monitorObj) Monitor.PulseAll(monitorObj);
            }
            if (eventWaitHandle != null)
            {
                eventWaitHandle.Set();
            }

            myLock.Exit();
            return true;
        }

        /// <summary>
        /// Resets the event, so that threads will wait until thw evwnt is set again.
        /// </summary>
        /// <returns>True if event was set before, false if it was already reset.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Reset()
        {
            if (!isSet) return false;
            
            isSet = false;
            
            if (eventWaitHandle != null)
            {
                eventWaitHandle.Reset();
            }
            
            return true;
        }

        /// <summary>
        /// Every thread waiting for this event is woken up, by setting and resetting the event in one step.
        /// If the event is already set, it will only be reset.
        /// </summary>        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PulseAll()
        {
            if (isSet && Reset()) return;
            myLock.Enter();
            if (isSet && Reset())
            {
                myLock.Exit();
                return;
            }

            //SET            
            isSet = true;
            setCounter++;

            bool yield = false;
            if (anyAsyncWaiter)
            {
                anyAsyncWaiter = false;
                tcs.SetResult(true);
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                yield = true;
            }

            
            if (anySyncWaiter)
            {
                anySyncWaiter = false;
                lock (monitorObj) Monitor.PulseAll(monitorObj);
                yield = true;
            }
            if (eventWaitHandle != null)
            {
                eventWaitHandle.Set();
            }

            //RESET
            Thread.MemoryBarrier();
            isSet = false;
            if (eventWaitHandle != null)
            {
                eventWaitHandle.Reset();
            }

            myLock.Exit();

            // If threads are waiting and PulseAll is called rapidly in a row it might happen that not all waiters will wake up.
            // Calling yield at the end will avoid this problem.
            if (yield) Thread.Yield();
        }

        /// <summary>
        /// Indicates if a thread would actually wait if one of the wait methods was called. (Invert of IsSet)
        /// </summary>
        /// <returns>False if event is set, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool WouldWait()
        {
            return !isSet;
        }

        /// <summary>
        /// Provides a classic Waithandle that is updated by the AsyncManualResetEvent.
        /// The WaitHandle must be detached and disposed (DetachAndDisposeWaitHandle) if not needed anymore.
        /// Do not dispose the WaitHandle before it is detached from the AsyncManualResetEvent.
        /// </summary>
        /// <param name="waitHandle">A WaitHandle updated the AsyncManualResetEvent</param>
        /// <returns>Always true</returns>
        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            if (eventWaitHandle == null) Interlocked.CompareExchange(ref eventWaitHandle, new EventWaitHandle(isSet, EventResetMode.ManualReset), null);
            
            waitHandle = eventWaitHandle;
            return true;
        }

        /// <summary>
        /// Removes a controlled WaitHandle and disposes it.
        /// </summary>
        public void DetachAndDisposeWaitHandle()
        {
            var waitHandle = eventWaitHandle;
            eventWaitHandle = null;
            waitHandle?.Dispose();
        }
    }
}