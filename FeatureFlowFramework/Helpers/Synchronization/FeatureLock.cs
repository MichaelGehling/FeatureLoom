using FeatureFlowFramework.Helpers.Time;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public class FeatureLock
    {
        const int NO_LOCK = 0;
        const int WRITE_LOCK = -1;

        const int NOT_ENTERED = 0;
        const int WRITE_ENTERED = -1;        

        const int SLEEP_WAITING_PRESSURE_EQUIVALENT = 10;
        const int MAX_WAITING_LIMIT_PRESSURE_CORRECTION = 100;
        const int MIN_WAITING_LIMIT_PRESSURE_CORRECTION = -100;

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockIndicator of -1 implies a write-lock, while a lockIndicator greater than 0 implies a read-lock.
        /// When entering a read-lock, the lockIndicator is increased and decreased when leaving a read-lock.
        /// When entering a write-lock, a WRITE_LOCK(-1) is set and set back to NO_LOCK(0) when the write-lock is left.
        /// </summary>
        volatile int lockIndicator = NO_LOCK;        

        volatile int maxWaitingPressure = 0;
        int waitingPressureLimitCorrection = 0;
        int defaultWaitingPressureLimit = 30;

        bool supportReentrance;
        AsyncLocal<int> reentranceIndicator;
        int writingReentranceCounter = 0;
        

        AsyncManualResetEvent mre = new AsyncManualResetEvent(true);

        Task<AcquiredLock> readLockTask;
        Task<AcquiredLock> writeLockTask;

        public FeatureLock(bool supportReentrance = false)
        {
            this.supportReentrance = supportReentrance;
            if (supportReentrance) reentranceIndicator = new AsyncLocal<int>();
            readLockTask = Task.FromResult(new AcquiredLock(this, false));
            writeLockTask = Task.FromResult(new AcquiredLock(this, true));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForReading(out AcquiredLock readLock, TimeSpan timeout = default)
        {
            var timer = new TimeFrame(timeout);
            readLock = new AcquiredLock();
            
            bool didSleep = false;

            int waitingPressure = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator) || waitingPressure < maxWaitingPressure || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.Elapsed)
                {
                    if (waitingPressure >= maxWaitingPressure) maxWaitingPressure = 0;
                    return false;
                }                
                if (waitingPressure < int.MaxValue) waitingPressure++;
                if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                if (waitingPressureLimitCorrection < MIN_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MIN_WAITING_LIMIT_PRESSURE_CORRECTION;
                else if (waitingPressureLimitCorrection > MAX_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MAX_WAITING_LIMIT_PRESSURE_CORRECTION;

                int waitingPressureLimit = defaultWaitingPressureLimit + waitingPressureLimitCorrection;
                if (maxWaitingPressure <= waitingPressureLimit)
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    if (int.MaxValue - SLEEP_WAITING_PRESSURE_EQUIVALENT > waitingPressure) waitingPressure += SLEEP_WAITING_PRESSURE_EQUIVALENT;
                    else waitingPressure = int.MaxValue;
                    if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                    waitingPressureLimitCorrection++;
                    didSleep = true;

                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {

                        if (!mre.Wait(timer.Remaining))
                        {
                            if (waitingPressure >= maxWaitingPressure) maxWaitingPressure = 0;
                            return false;
                        }
                        spinWait.Reset();                        
                    }
                    else if (didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            maxWaitingPressure = 0;
            if (!didSleep) waitingPressureLimitCorrection--;

            readLock = new AcquiredLock(this, false);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForWriting(out AcquiredLock writeLock, TimeSpan timeout = default)
        {
            var timer = new TimeFrame(timeout);
            writeLock = new AcquiredLock();

            bool didSleep = false;

            int waitingPressure = 0;
            SpinWait spinWait = new SpinWait();
            var newLockIndicator = WRITE_LOCK;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator) || waitingPressure < maxWaitingPressure || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, NO_LOCK))
            {
                if (timer.Elapsed)
                {
                    if (waitingPressure >= maxWaitingPressure) maxWaitingPressure = 0;
                    return false;
                }
                if (waitingPressure < int.MaxValue) waitingPressure++;
                if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                if (waitingPressureLimitCorrection < MIN_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MIN_WAITING_LIMIT_PRESSURE_CORRECTION;
                else if (waitingPressureLimitCorrection > MAX_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MAX_WAITING_LIMIT_PRESSURE_CORRECTION;

                int waitingPressureLimit = defaultWaitingPressureLimit + waitingPressureLimitCorrection;
                if (maxWaitingPressure <= waitingPressureLimit)
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    if (int.MaxValue - SLEEP_WAITING_PRESSURE_EQUIVALENT > waitingPressure) waitingPressure += SLEEP_WAITING_PRESSURE_EQUIVALENT;
                    else waitingPressure = int.MaxValue;
                    if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                    waitingPressureLimitCorrection++;
                    didSleep = true;

                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining))
                        {
                            if (waitingPressure >= maxWaitingPressure) maxWaitingPressure = 0;
                            return false;
                        }
                        spinWait.Reset();
                    }
                    else if (didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            maxWaitingPressure = 0;
            if (!didSleep) waitingPressureLimitCorrection--;

            writeLock = new AcquiredLock(this, true);
            return true;
        } 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock ForReading()
        {
            bool didSleep = false;

            int waitingPressure = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator) || waitingPressure < maxWaitingPressure || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (waitingPressure < int.MaxValue) waitingPressure++;
                if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                if (waitingPressureLimitCorrection < MIN_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MIN_WAITING_LIMIT_PRESSURE_CORRECTION;
                else if (waitingPressureLimitCorrection > MAX_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MAX_WAITING_LIMIT_PRESSURE_CORRECTION;

                int waitingPressureLimit = defaultWaitingPressureLimit + waitingPressureLimitCorrection;
                if (maxWaitingPressure <= waitingPressureLimit)
                {                    
                    spinWait.SpinOnce();
                }
                else
                {
                    if (int.MaxValue - SLEEP_WAITING_PRESSURE_EQUIVALENT > waitingPressure) waitingPressure += SLEEP_WAITING_PRESSURE_EQUIVALENT;
                    else waitingPressure = int.MaxValue;
                    if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                    waitingPressureLimitCorrection++;
                    didSleep = true;

                    bool didReset = mre.Reset();
                    if(lockIndicator != NO_LOCK)
                    {
                        mre.Wait();
                        spinWait.Reset();
                    }
                    else if(didReset) mre.Set();
                } 

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            maxWaitingPressure = 0;
            if (!didSleep) waitingPressureLimitCorrection--;

            return new AcquiredLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> ForReadingAsync()
        {
            if(TryForReading(out _)) return readLockTask;
            else return ForReadingAsync_();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<AcquiredLock> ForReadingAsync_()
        {
            bool didSleep = false;

            int waitingPressure = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator) || waitingPressure < maxWaitingPressure || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (waitingPressure < int.MaxValue) waitingPressure++;
                if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                if (waitingPressureLimitCorrection < MIN_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MIN_WAITING_LIMIT_PRESSURE_CORRECTION;
                else if (waitingPressureLimitCorrection > MAX_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MAX_WAITING_LIMIT_PRESSURE_CORRECTION;

                int waitingPressureLimit = defaultWaitingPressureLimit + waitingPressureLimitCorrection;
                if (maxWaitingPressure <= waitingPressureLimit)
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    if (int.MaxValue - SLEEP_WAITING_PRESSURE_EQUIVALENT > waitingPressure) waitingPressure += SLEEP_WAITING_PRESSURE_EQUIVALENT;
                    else waitingPressure = int.MaxValue;
                    if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                    waitingPressureLimitCorrection++;
                    didSleep = true;

                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync();
                        spinWait.Reset();
                    }
                    else if(didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            maxWaitingPressure = 0;
            if (!didSleep) waitingPressureLimitCorrection--;

            return new AcquiredLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock ForWriting()
        {
            int currentReentranceIndicator = NOT_ENTERED;
            if (supportReentrance)
            {
                currentReentranceIndicator = reentranceIndicator.Value;
                if (currentReentranceIndicator == WRITE_ENTERED)
                {
                    writingReentranceCounter++;
                    return new AcquiredLock(this, true);
                }
            }

            bool didSleep = false;

            int waitingPressure = 0;
            SpinWait spinWait = new SpinWait();
            var newLockIndicator = WRITE_LOCK;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator) || waitingPressure < maxWaitingPressure || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, NO_LOCK))
            {
                if (waitingPressure < int.MaxValue) waitingPressure++;
                if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                if (waitingPressureLimitCorrection < MIN_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MIN_WAITING_LIMIT_PRESSURE_CORRECTION;
                else if (waitingPressureLimitCorrection > MAX_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MAX_WAITING_LIMIT_PRESSURE_CORRECTION;

                int waitingPressureLimit = defaultWaitingPressureLimit + waitingPressureLimitCorrection;
                if (maxWaitingPressure <= waitingPressureLimit)
                {                    
                    spinWait.SpinOnce();
                }
                else
                {
                    if (int.MaxValue - SLEEP_WAITING_PRESSURE_EQUIVALENT > waitingPressure) waitingPressure += SLEEP_WAITING_PRESSURE_EQUIVALENT;
                    else waitingPressure = int.MaxValue;
                    if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                    waitingPressureLimitCorrection++;
                    didSleep = true;

                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        mre.Wait();
                        spinWait.Reset();
                    }
                    else if(didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            maxWaitingPressure = 0;
            if (!didSleep) waitingPressureLimitCorrection--;

            if (supportReentrance)
            {
                writingReentranceCounter++;
                if (currentReentranceIndicator != WRITE_ENTERED) reentranceIndicator.Value = WRITE_ENTERED;
            }

            return new AcquiredLock(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> ForWritingAsync()
        {
            if(TryForWriting(out _)) return writeLockTask;
            else return ForWritingAsync_();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<AcquiredLock> ForWritingAsync_()
        {
            bool didSleep = false;

            int waitingPressure = 0;
            SpinWait spinWait = new SpinWait();
            var newLockIndicator = WRITE_LOCK;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator) || waitingPressure < maxWaitingPressure || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, NO_LOCK))
            {
                if (waitingPressure < int.MaxValue) waitingPressure++;
                if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                if (waitingPressureLimitCorrection < MIN_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MIN_WAITING_LIMIT_PRESSURE_CORRECTION;
                else if (waitingPressureLimitCorrection > MAX_WAITING_LIMIT_PRESSURE_CORRECTION) waitingPressureLimitCorrection = MAX_WAITING_LIMIT_PRESSURE_CORRECTION;

                int waitingPressureLimit = defaultWaitingPressureLimit + waitingPressureLimitCorrection;
                if (maxWaitingPressure <= waitingPressureLimit)
                {                    
                    spinWait.SpinOnce();
                }
                else
                {
                    if (int.MaxValue - SLEEP_WAITING_PRESSURE_EQUIVALENT > waitingPressure) waitingPressure += SLEEP_WAITING_PRESSURE_EQUIVALENT;
                    else waitingPressure = int.MaxValue;
                    if (waitingPressure > maxWaitingPressure) maxWaitingPressure = waitingPressure;
                    waitingPressureLimitCorrection++;
                    didSleep = true;

                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync();
                        spinWait.Reset();
                    }
                    else if(didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            maxWaitingPressure = 0;
            if (!didSleep) waitingPressureLimitCorrection--;

            return new AcquiredLock(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator)
        {
            return currentLockIndicator < NO_LOCK;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockIndicator)
        {
            return currentLockIndicator != NO_LOCK;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            var newLockIndicator = Interlocked.Decrement(ref lockIndicator);
            if (NO_LOCK == newLockIndicator)
            {
                mre.Set();
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            if (supportReentrance)
            {
                writingReentranceCounter--;
                if (writingReentranceCounter == 0) reentranceIndicator.Value = NOT_ENTERED;
                else return;
            }

            lockIndicator = NO_LOCK;
            mre.Set();
        }

        public struct AcquiredLock : IDisposable
        {
            FeatureLock parentLock;
            readonly bool isWriteLock;            

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public AcquiredLock(FeatureLock parentLock, bool isWriteLock)
            {
                this.parentLock = parentLock;
                this.isWriteLock = isWriteLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                Return();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Return()
            {
                if (isWriteLock) parentLock?.ExitWriteLock();
                else parentLock?.ExitReadLock();
                parentLock = null;
            }
        }

    }
}
