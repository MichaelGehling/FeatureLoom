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

        const uint MAX_WAITING_PRESSURE = 1500;
        const uint START_WAITING_PRESSURE = 50;

        readonly bool supportReentrance;
        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockIndicator of -1 implies a write-lock, while a lockIndicator greater than 0 implies a read-lock.
        /// When entering a read-lock, the lockIndicator is increased and decreased when leaving a read-lock.
        /// When entering a write-lock, a WRITE_LOCK(-1) is set and set back to NO_LOCK(0) when the write-lock is left.
        /// </summary>
        volatile int lockIndicator = NO_LOCK;        
        volatile uint highestWaitingPressure = 0;
        volatile int writingReentranceCounter = 0;                
        AsyncLocal<int> reentranceIndicator;


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
        public bool TryForReading(out AcquiredLock readLock, TimeSpan timeout = default, uint waitingPressure = START_WAITING_PRESSURE)
        {
            var timer = new TimeFrame(timeout);
            readLock = new AcquiredLock();

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.Elapsed)
                {
                    if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = 0;
                    return false;
                }

                bool nextInQueue = true;
                if (waitingPressure < MAX_WAITING_PRESSURE) waitingPressure += 1;
                if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = waitingPressure;
                else nextInQueue = false;

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining))
                        {
                            if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = 0;
                            return false;
                        }
                    }
                    else if (didReset) mre.Set();
                    if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            highestWaitingPressure = 0;

            readLock = new AcquiredLock(this, false);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryForWriting(out AcquiredLock writeLock, TimeSpan timeout = default, uint waitingPressure = START_WAITING_PRESSURE)
        {
            var timer = new TimeFrame(timeout);
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

                bool nextInQueue = true;
                if (waitingPressure < MAX_WAITING_PRESSURE) waitingPressure += 1;
                if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = waitingPressure;
                else nextInQueue = false;

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining))
                        {
                            if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = 0;
                            writeLock = new AcquiredLock();
                            return false;
                        }
                    }
                    else if (didReset) mre.Set();
                    if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }

                currentLockIndicator = lockIndicator;
            }
            highestWaitingPressure = 0;

            writeLock = new AcquiredLock(this, true);
            return true;
        } 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock ForReading(uint waitingPressure = START_WAITING_PRESSURE)
        {
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                bool nextInQueue = true;
                if (waitingPressure < MAX_WAITING_PRESSURE) waitingPressure += 1;
                if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = waitingPressure;
                else nextInQueue = false;

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK) mre.Wait();
                    else if (didReset) mre.Set();
                    if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            highestWaitingPressure = 0;

            return new AcquiredLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> ForReadingAsync(uint waitingPressure = START_WAITING_PRESSURE)
        {
            if(TryForReading(out _, default, waitingPressure)) return readLockTask;
            else return ForReadingAsync_(waitingPressure);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<AcquiredLock> ForReadingAsync_(uint waitingPressure = START_WAITING_PRESSURE)
        {
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                bool nextInQueue = true;
                if (waitingPressure < MAX_WAITING_PRESSURE) waitingPressure += 1;
                if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = waitingPressure;
                else nextInQueue = false;

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK) await mre.WaitAsync();
                    else if (didReset) mre.Set();
                    if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            highestWaitingPressure = 0;

            return new AcquiredLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock ForWriting(uint waitingPressure = START_WAITING_PRESSURE)
        {
            int currentReentranceIndicator = NOT_ENTERED;
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
                bool nextInQueue = true;
                if (waitingPressure < MAX_WAITING_PRESSURE) waitingPressure += 1;
                if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = waitingPressure;
                else nextInQueue = false;

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK) mre.Wait();
                    else if (didReset) mre.Set();
                    if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }

                currentLockIndicator = lockIndicator;
            }
            highestWaitingPressure = 0;

            if (supportReentrance)
            {
                writingReentranceCounter++;
                reentranceIndicator.Value = WRITE_ENTERED;
            }

            return new AcquiredLock(this, true);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> ForWritingAsync(uint waitingPressure = START_WAITING_PRESSURE)
        {
            if(TryForWriting(out _, default, waitingPressure)) return writeLockTask;
            else return ForWritingAsync_(waitingPressure);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<AcquiredLock> ForWritingAsync_(uint waitingPressure = START_WAITING_PRESSURE)
        {
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool nextInQueue = true;
                if (waitingPressure < MAX_WAITING_PRESSURE) waitingPressure += 1;
                if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = waitingPressure;
                else nextInQueue = false;

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK) await mre.WaitAsync();
                    else if (didReset) mre.Set();
                    if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }

                currentLockIndicator = lockIndicator;
            }
            highestWaitingPressure = 0;

            return new AcquiredLock(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator, uint waitingPressure)
        {
            return currentLockIndicator == WRITE_LOCK || waitingPressure < highestWaitingPressure;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockIndicator, uint waitingPressure)
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
