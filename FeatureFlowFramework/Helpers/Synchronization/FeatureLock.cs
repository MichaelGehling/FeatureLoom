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
        const int FIRST_WRITE_ENTERED = -1;
        const int FIRST_READ_ENTERED = 1;

        public const uint MAX_WAITING_PRESSURE = 1500;
        public const uint START_WAITING_PRESSURE = 50;
        public const uint MIN_WAITING_PRESSURE = 0;

        const int FALSE = 0;
        const int TRUE = 1;

        readonly bool reentranceSupported;
        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockIndicator of -1 implies a write-lock, while a lockIndicator greater than 0 implies a read-lock.
        /// When entering a read-lock, the lockIndicator is increased and decreased when leaving a read-lock.
        /// When entering a write-lock, a WRITE_LOCK(-1) is set and set back to NO_LOCK(0) when the write-lock is left.
        /// </summary>
        volatile int lockIndicator = NO_LOCK;        
        volatile uint highestWaitingPressure = 0;
        volatile uint secondHighestWaitingPressure = 0;
        volatile int waitingForUpgrade = FALSE;
        AsyncLocal<int> reentranceIndicator;


        AsyncManualResetEvent mre = new AsyncManualResetEvent(true);

        Task<AcquiredLock> readLockTask;
        Task<AcquiredLock> writeLockTask;

        public FeatureLock(bool supportReentrance = false)
        {
            this.reentranceSupported = supportReentrance;
            if (supportReentrance) reentranceIndicator = new AsyncLocal<int>();
            readLockTask = Task.FromResult(new AcquiredLock(this, false));
            writeLockTask = Task.FromResult(new AcquiredLock(this, true));
        }

        public void ResetReentranceContext()
        {
            if (reentranceSupported) reentranceIndicator.Value = NOT_ENTERED;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out AcquiredLock readLock, TimeSpan timeout = default, uint waitingPressure = START_WAITING_PRESSURE)
        {
            var timer = new TimeFrame(timeout);

            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForReading();
                if (reentered)
                {
                    readLock = acquiredLock;
                    return true;
                }
            }

            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.Elapsed)
                {
                    readLock = new AcquiredLock();
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
                            readLock = new AcquiredLock();
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
            if (reentranceSupported) reentranceIndicator.Value = FIRST_READ_ENTERED;

            readLock = new AcquiredLock(this, false);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock writeLock, TimeSpan timeout = default, uint waitingPressure = START_WAITING_PRESSURE)
        {
            var timer = new TimeFrame(timeout);
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, timedOut , acquiredLock) = TryReenterForWritingWithTimeout(timer);
                if (reentered)
                {
                    writeLock = acquiredLock;
                    return true;
                }
                else if (timedOut)
                {
                    writeLock = acquiredLock;
                    return false;
                }
            }

            while (WriterMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
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
            if (reentranceSupported) reentranceIndicator.Value = FIRST_WRITE_ENTERED;

            writeLock = new AcquiredLock(this, true);
            return true;
        }

        private (bool reentered, bool timedOut, AcquiredLock acquiredLock) TryReenterForWritingWithTimeout(TimeFrame timer)
        {
            var currentReentranceIndicator = reentranceIndicator.Value;
            if (currentReentranceIndicator <= FIRST_WRITE_ENTERED)
            {
                reentranceIndicator.Value = currentReentranceIndicator - 1;
                return (true, false, new AcquiredLock(this, false, true));
            }
            else if (currentReentranceIndicator >= FIRST_READ_ENTERED)
            {
                // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
                if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
                // Waiting for upgrade to writeLock
                while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
                {
                    if (timer.Elapsed) return (false, true, new AcquiredLock());
                    Thread.Yield(); // Could be more optimized, but it's such a rare case...
                }
                waitingForUpgrade = FALSE;
                reentranceIndicator.Value = -currentReentranceIndicator - 1;
                return (true, false, new AcquiredLock(this, true, true)); // ...with downgrade flag set!
            }
            else return (false, false, new AcquiredLock());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnly(uint waitingPressure = START_WAITING_PRESSURE)
        {
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForReading();
                if (reentered) return acquiredLock;
            }

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

            if (reentranceSupported) reentranceIndicator.Value = FIRST_READ_ENTERED;

            return new AcquiredLock(this, false);
        }

        private (bool reentered, AcquiredLock acquiredLock) TryReenterForReading()
        {
            var currentReentranceIndicator = reentranceIndicator.Value;
            if (currentReentranceIndicator <= FIRST_WRITE_ENTERED)
            {
                // Already a writeLock in place in this flow, so reenter just reenter as a writeLock
                reentranceIndicator.Value = currentReentranceIndicator - 1;
                return (true, new AcquiredLock(this, true));
            }
            else if (currentReentranceIndicator >= FIRST_READ_ENTERED)
            {
                // Already a readlock in place in this flow, so reenter
                reentranceIndicator.Value = currentReentranceIndicator + 1;
                return (true, new AcquiredLock(this, false));
            }
            else return (false, new AcquiredLock());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReadOnlyAsync(uint waitingPressure = START_WAITING_PRESSURE)
        {
            if(TryLockReadOnly(out _, default, waitingPressure)) return readLockTask;
            else return LockForReadingAsync(waitingPressure);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<AcquiredLock> LockForReadingAsync(uint waitingPressure = START_WAITING_PRESSURE)
        {
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForReading();
                if (reentered) return acquiredLock;
            }
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
            if (reentranceSupported) reentranceIndicator.Value = FIRST_READ_ENTERED;

            return new AcquiredLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock(uint waitingPressure = START_WAITING_PRESSURE)
        {
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForWriting();
                if (reentered) return acquiredLock;
            }            

            while (WriterMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool nextInQueue = true;
                if (waitingPressure < MAX_WAITING_PRESSURE) waitingPressure += 1;
                if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = waitingPressure;
                else if (waitingForUpgrade > secondHighestWaitingPressure)
                {
                    secondHighestWaitingPressure = waitingPressure;
                    nextInQueue = false;
                }
                else nextInQueue = false;

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK) mre.Wait(/*1.Seconds()*/);
                    else if (didReset) mre.Set();
                    if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }

                currentLockIndicator = lockIndicator;
            }
            highestWaitingPressure = secondHighestWaitingPressure;
            secondHighestWaitingPressure = 0;
            if (reentranceSupported) reentranceIndicator.Value = FIRST_WRITE_ENTERED;

            return new AcquiredLock(this, true);
        }

        private (bool reentered, AcquiredLock acquiredLock) TryReenterForWriting()
        {
            var currentReentranceIndicator = reentranceIndicator.Value;
            if (currentReentranceIndicator <= FIRST_WRITE_ENTERED)
            {
                reentranceIndicator.Value = currentReentranceIndicator - 1;
                return (true, new AcquiredLock(this, true));
            }
            else if (currentReentranceIndicator >= FIRST_READ_ENTERED)
            {
                // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
                if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
                // Waiting for upgrade to writeLock
                while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
                {
                    Thread.Yield(); // Could be more optimized, but it's such a rare case...
                }
                waitingForUpgrade = FALSE;
                reentranceIndicator.Value = -currentReentranceIndicator - 1;
                return (true, new AcquiredLock(this, true, true)); // ...with downgrade flag set!
            }
            else return (false, new AcquiredLock());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockAsync(uint waitingPressure = START_WAITING_PRESSURE)
        {
            if(TryLock(out _, default, waitingPressure)) return writeLockTask;
            else return LockForWritingAsync(waitingPressure);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<AcquiredLock> LockForWritingAsync(uint waitingPressure = START_WAITING_PRESSURE)
        {
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForWriting();
                if (reentered) return acquiredLock;
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
                    if (lockIndicator != NO_LOCK) await mre.WaitAsync();
                    else if (didReset) mre.Set();
                    if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }

                currentLockIndicator = lockIndicator;
            }
            highestWaitingPressure = 0;
            if (reentranceSupported) reentranceIndicator.Value = FIRST_WRITE_ENTERED;

            return new AcquiredLock(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator, uint waitingPressure)
        {
            return currentLockIndicator == WRITE_LOCK || waitingPressure < highestWaitingPressure || (reentranceSupported && waitingForUpgrade == TRUE);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockIndicator, uint waitingPressure)
        {
            return currentLockIndicator != NO_LOCK || waitingPressure < highestWaitingPressure;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            if (reentranceSupported)
            {
                var currentReentranceIndicator = reentranceIndicator.Value;
                currentReentranceIndicator--;

                if (currentReentranceIndicator == NOT_ENTERED)
                {
                    reentranceIndicator.Value = NOT_ENTERED;
                    // go on, unlock...
                }
                else
                {
                    reentranceIndicator.Value = currentReentranceIndicator;
                    return; // still a writelock in place
                }
            }

            var newLockIndicator = Interlocked.Decrement(ref lockIndicator);
            if (NO_LOCK == newLockIndicator)
            {
                mre.Set();
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock(bool downgrade)
        {
            if (reentranceSupported)
            {
                var currentReentranceIndicator = reentranceIndicator.Value;
                currentReentranceIndicator++;
                if (currentReentranceIndicator == NOT_ENTERED)
                {
                    reentranceIndicator.Value = NOT_ENTERED;
                    // go on, unlock...
                }
                else if (downgrade)
                {
                    reentranceIndicator.Value = -currentReentranceIndicator;
                    lockIndicator = FIRST_READ_LOCK;
                    return; // now it's a readlock again
                }
                else
                {
                    reentranceIndicator.Value = currentReentranceIndicator;
                    return; // still a writelock in place
                }
            }

            lockIndicator = NO_LOCK;
            mre.Set();
        }

        public struct AcquiredLock : IDisposable
        {
            FeatureLock parentLock;
            readonly bool isWriteLock;
            readonly bool downgrade;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public AcquiredLock(FeatureLock parentLock, bool isWriteLock, bool downgrade = false)
            {
                this.parentLock = parentLock;
                this.isWriteLock = isWriteLock;
                this.downgrade = downgrade;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                Return();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Return()
            {
                if (isWriteLock) parentLock?.ExitWriteLock(downgrade);
                else parentLock?.ExitReadLock();
                parentLock = null;
            }
        }

    }
}
