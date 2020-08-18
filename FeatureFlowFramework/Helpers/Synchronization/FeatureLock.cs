using FeatureFlowFramework.Helpers.Time;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public class FeatureLock
    {
        const int NO_LOCK = 0;
        const int WRITE_LOCK = -1;
        const int FIRST_READ_LOCK = 1;

        const int NOT_ENTERED = 0;
        const int WRITE_ENTERED = -1;        

        const int SLEEP_WAITING_PRESSURE_EQUIVALENT = 10;
        const int MAX_WAITING_PRESSURE = 100;
        const int MIN_WAITING_PRESSURE = -10;
        const int INIT_WAITING_PRESSURE_THRESHOLD = 30;

        readonly bool supportReentrance;
        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockIndicator of -1 implies a write-lock, while a lockIndicator greater than 0 implies a read-lock.
        /// When entering a read-lock, the lockIndicator is increased and decreased when leaving a read-lock.
        /// When entering a write-lock, a WRITE_LOCK(-1) is set and set back to NO_LOCK(0) when the write-lock is left.
        /// </summary>
        volatile int lockIndicator = NO_LOCK;        
        volatile int highestWaitingPressure = 0;
        volatile int writingReentranceCounter = 0;                
        AsyncLocal<int> reentranceIndicator;
        int waitingPressureSleepThreshold = INIT_WAITING_PRESSURE_THRESHOLD;


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
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.Elapsed)
                {
                    if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = 0;
                    return false;
                }

                if (highestWaitingPressure <= waitingPressureSleepThreshold)
                {
                    waitingPressure = IncrementWaitingPressure(waitingPressure, 1);
                    spinWait.SpinOnce();
                }
                else
                {
                    waitingPressureSleepThreshold++;
                    waitingPressure = IncrementWaitingPressure(waitingPressure, SLEEP_WAITING_PRESSURE_EQUIVALENT);
                    didSleep = true;

                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining))
                        {
                            if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = 0;
                            return false;
                        }
                        spinWait.Reset();                        
                    }
                    else if (didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            highestWaitingPressure = 0;
            if (!didSleep) waitingPressureSleepThreshold--;

            readLock = new AcquiredLock(this, false);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForWriting(out AcquiredLock writeLock, TimeSpan timeout = default)
        {
            var timer = new TimeFrame(timeout);
            bool didSleep = false;
            int waitingPressure = 0;
            SpinWait spinWait = new SpinWait();
            var newLockIndicator = WRITE_LOCK;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, NO_LOCK))
            {
                if (timer.Elapsed)
                {
                    if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = 0;
                    writeLock = new AcquiredLock();
                    return false;
                }
                
                if (highestWaitingPressure <= waitingPressureSleepThreshold)
                {
                    waitingPressure = IncrementWaitingPressure(waitingPressure, 1);
                    spinWait.SpinOnce();
                }
                else
                {
                    waitingPressureSleepThreshold++;
                    waitingPressure = IncrementWaitingPressure(waitingPressure, SLEEP_WAITING_PRESSURE_EQUIVALENT);
                    didSleep = true;

                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining))
                        {
                            if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = 0;
                            writeLock = new AcquiredLock();
                            return false;
                        }
                        spinWait.Reset();
                    }
                    else if (didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            highestWaitingPressure = 0;
            if (!didSleep) waitingPressureSleepThreshold--;

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
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (highestWaitingPressure <= waitingPressureSleepThreshold)
                {
                    waitingPressure = IncrementWaitingPressure(waitingPressure, 1);
                    spinWait.SpinOnce();
                }
                else
                {
                    waitingPressureSleepThreshold++;
                    waitingPressure = IncrementWaitingPressure(waitingPressure, SLEEP_WAITING_PRESSURE_EQUIVALENT);
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
            highestWaitingPressure = 0;
            if (!didSleep) waitingPressureSleepThreshold--;

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
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (highestWaitingPressure <= waitingPressureSleepThreshold)
                {
                    waitingPressure = IncrementWaitingPressure(waitingPressure, 1);
                    spinWait.SpinOnce();
                }
                else
                {
                    waitingPressureSleepThreshold++;
                    waitingPressure = IncrementWaitingPressure(waitingPressure, SLEEP_WAITING_PRESSURE_EQUIVALENT);
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
            highestWaitingPressure = 0;
            if (!didSleep) waitingPressureSleepThreshold--;

            return new AcquiredLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock ForWriting()
        {
            int currentReentranceIndicator = NOT_ENTERED;
            bool didSleep = false;
            int waitingPressure = 0;
            SpinWait spinWait = new SpinWait();
            var currentLockIndicator = lockIndicator;
            if (supportReentrance && currentLockIndicator != NO_LOCK)
            {                
                currentReentranceIndicator = reentranceIndicator.Value;
                if (currentReentranceIndicator == WRITE_ENTERED)
                {
                    writingReentranceCounter++;
                    return new AcquiredLock(this, true);
                }
            }
            while (WriterMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {                
                if (highestWaitingPressure <= waitingPressureSleepThreshold)
                {
                    waitingPressure = IncrementWaitingPressure(waitingPressure, 1);
                    spinWait.SpinOnce();
                }
                else
                {
                    waitingPressureSleepThreshold++;
                    waitingPressure = IncrementWaitingPressure(waitingPressure, SLEEP_WAITING_PRESSURE_EQUIVALENT);
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
            highestWaitingPressure = 0;
            if (!didSleep) waitingPressureSleepThreshold--;

            if (supportReentrance)
            {
                writingReentranceCounter++;
                reentranceIndicator.Value = WRITE_ENTERED;
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
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {                
                if (highestWaitingPressure <= waitingPressureSleepThreshold)
                {
                    waitingPressure = IncrementWaitingPressure(waitingPressure, 1);
                    spinWait.SpinOnce();
                }
                else
                {
                    waitingPressureSleepThreshold++;
                    waitingPressure = IncrementWaitingPressure(waitingPressure, SLEEP_WAITING_PRESSURE_EQUIVALENT);
                    didSleep = true;

                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync();
                        spinWait.Reset();
                    }
                    else if (didReset) mre.Set();
                }

                currentLockIndicator = lockIndicator;
            }
            highestWaitingPressure = 0;
            if (!didSleep) waitingPressureSleepThreshold--;

            return new AcquiredLock(this, true);
        }

        private int IncrementWaitingPressure(int waitingPressure, int increment)
        {
            waitingPressure += increment;
            if (waitingPressure > MAX_WAITING_PRESSURE) waitingPressure = MAX_WAITING_PRESSURE;
            if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
            if (waitingPressureSleepThreshold < MIN_WAITING_PRESSURE) waitingPressureSleepThreshold = MIN_WAITING_PRESSURE;
            else if (waitingPressureSleepThreshold > MAX_WAITING_PRESSURE) waitingPressureSleepThreshold = MAX_WAITING_PRESSURE;
            return waitingPressure;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator, int waitingPressure)
        {
            return currentLockIndicator == WRITE_LOCK || waitingPressure < highestWaitingPressure;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockIndicator, int waitingPressure)
        {
            return currentLockIndicator != NO_LOCK || waitingPressure < highestWaitingPressure;
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
