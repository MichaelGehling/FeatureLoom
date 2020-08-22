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

        public const int MAX_WAITING_PRESSURE = int.MaxValue;
        public const int START_WAITING_PRESSURE = 0;
        public const int MIN_WAITING_PRESSURE = int.MinValue;
        public readonly TimeSpan sleepTime = 100.Milliseconds();

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
        volatile int highestWaitingPressure = MIN_WAITING_PRESSURE;
        volatile int secondHighestWaitingPressure = MIN_WAITING_PRESSURE;
        volatile int waitingForUpgrade = FALSE;
        volatile int globalInitId = int.MinValue;
        volatile int numWaitingApprox = 0;

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
        public bool TryLockReadOnly(out AcquiredLock readLock, TimeSpan timeout = default, int waitingPressure = START_WAITING_PRESSURE)
        {
            var timer = new TimeFrame(timeout);
            int initId = 0;
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
            waitingPressure = ApplyWaitOrder(waitingPressure);
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                bool timedOut = TryLockReadOnlyWaitingLoop(ref waitingPressure, ref timer, ref initId);

                if (timedOut)
                {
                    if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = MIN_WAITING_PRESSURE;
                    readLock = new AcquiredLock();
                    return false;
                }
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            highestWaitingPressure = MIN_WAITING_PRESSURE;
            if (reentranceSupported) reentranceIndicator.Value = FIRST_READ_ENTERED;

            readLock = new AcquiredLock(this, false);
            return true;
        }

        private bool TryLockReadOnlyWaitingLoop(ref int waitingPressure, ref TimeFrame timer, ref int initId)
        {
            bool timedOut = false;
            if (timer.Elapsed) timedOut = true;
            else
            {
                bool nextInQueue = UpdateWaitingPressure(ref waitingPressure, ref initId);

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining)) timedOut = true;
                        //else numWaitingApprox++;
                    }
                    else if (didReset) mre.Set();
                    if (!timedOut && waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }
            }

            return timedOut;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock writeLock, TimeSpan timeout = default, int waitingPressure = START_WAITING_PRESSURE)
        {
            int initId = 0;
            var timer = new TimeFrame(timeout);
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, timedOut , acquiredLock) = TryReenterForWritingWithTimeout(timer);
                writeLock = acquiredLock;
                if (reentered) return true;                
                else if (timedOut) return false;                
            }
            waitingPressure = ApplyWaitOrder(waitingPressure);            
            while (WriterMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool timedOut = TryLockWaitingLoop(ref waitingPressure, ref timer, ref initId);

                if (timedOut)
                {
                    if (waitingPressure >= highestWaitingPressure) highestWaitingPressure = MIN_WAITING_PRESSURE;
                    writeLock = new AcquiredLock();
                    return false;
                }
                currentLockIndicator = lockIndicator;
            }
            highestWaitingPressure = MIN_WAITING_PRESSURE;
            if (reentranceSupported) reentranceIndicator.Value = FIRST_WRITE_ENTERED;

            writeLock = new AcquiredLock(this, true);
            return true;
        }

        private bool TryLockWaitingLoop(ref int waitingPressure, ref TimeFrame timer, ref int initId)
        {
            bool timedOut = false;
            if (timer.Elapsed) timedOut = true;
            else
            {
                bool nextInQueue = UpdateWaitingPressure(ref waitingPressure, ref initId);

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining)) timedOut = true;
                        //else numWaitingApprox++;
                    }
                    else if (didReset) mre.Set();
                    if (!timedOut && waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }
            }

            return timedOut;
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
        public AcquiredLock LockReadOnly(int waitingPressure = START_WAITING_PRESSURE)
        {
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForReading();
                if (reentered) return acquiredLock;
            }

            waitingPressure = ApplyWaitOrder(waitingPressure);
            int initId = 0;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                waitingPressure = LockReadOnlyWaitingLoop(waitingPressure, out currentLockIndicator, out newLockIndicator, ref initId);
            }
            highestWaitingPressure = MIN_WAITING_PRESSURE;

            if (reentranceSupported) reentranceIndicator.Value = FIRST_READ_ENTERED;

            return new AcquiredLock(this, false);
        }

        private int LockReadOnlyWaitingLoop(int waitingPressure, out int currentLockIndicator, out int newLockIndicator, ref int initId)
        {
            bool nextInQueue = UpdateWaitingPressure(ref waitingPressure, ref initId);

            if (!nextInQueue)
            {
                bool didReset = mre.Reset();
                if (lockIndicator != NO_LOCK)
                {
                    mre.Wait(sleepTime);
                    //numWaitingApprox++;
                }
                else if (didReset) mre.Set();
                if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
            }

            currentLockIndicator = lockIndicator;
            newLockIndicator = currentLockIndicator + 1;
            return waitingPressure;
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
        public Task<AcquiredLock> LockReadOnlyAsync(int waitingPressure = START_WAITING_PRESSURE)
        {
            if(TryLockReadOnly(out _, default, waitingPressure)) return readLockTask;
            else return LockForReadingAsync(waitingPressure);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<AcquiredLock> LockForReadingAsync(int waitingPressure = START_WAITING_PRESSURE)
        {
            waitingPressure = ApplyWaitOrder(waitingPressure);

            var currentLockIndicator = lockIndicator;
            int initId = 0;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                bool nextInQueue = UpdateWaitingPressure(ref waitingPressure, ref initId);

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
            highestWaitingPressure = MIN_WAITING_PRESSURE;
            if (reentranceSupported) reentranceIndicator.Value = FIRST_READ_ENTERED;

            return new AcquiredLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock(int waitingPressure = START_WAITING_PRESSURE)
        {
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForWriting();
                if (reentered) return acquiredLock;
            }

            waitingPressure = ApplyWaitOrder(waitingPressure);
            int initId = 0;

            while (WriterMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                currentLockIndicator = LockWaitingLoop(ref waitingPressure, ref initId);
            }
            highestWaitingPressure = secondHighestWaitingPressure;
            secondHighestWaitingPressure = MIN_WAITING_PRESSURE;
            if (reentranceSupported) reentranceIndicator.Value = FIRST_WRITE_ENTERED;

            return new AcquiredLock(this, true);
        }

        private int LockWaitingLoop(ref int waitingPressure, ref int initId)
        {
            int currentLockIndicator;
            bool nextInQueue = UpdateWaitingPressure(ref waitingPressure, ref initId);

            if (!nextInQueue)
            {
                bool didReset = mre.Reset();
                if (lockIndicator != NO_LOCK)
                {
                    mre.Wait(sleepTime);
                    //numWaitingApprox++;
                }
                else if (didReset) mre.Set();
                if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
            }

            currentLockIndicator = lockIndicator;
            return currentLockIndicator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ApplyWaitOrder(int waitingPressure)
        {
            waitingPressure -= numWaitingApprox;
            return waitingPressure;
        }

        private bool UpdateWaitingPressure(ref int waitingPressure, ref int initId)
        {
            if (initId != globalInitId)
            {
                initId = globalInitId;
                numWaitingApprox++;
            }
            bool nextInQueue = false;
            //if (waitingPressure < MAX_WAITING_PRESSURE) waitingPressure += 1;
            while (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
            if (waitingPressure >= highestWaitingPressure) nextInQueue = true;
            else while (waitingPressure > secondHighestWaitingPressure) secondHighestWaitingPressure = waitingPressure;
            return nextInQueue;
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
        public Task<AcquiredLock> LockAsync(int waitingPressure = START_WAITING_PRESSURE)
        {
            if(TryLock(out _, default, waitingPressure)) return writeLockTask;
            else return LockForWritingAsync(waitingPressure);
        }

        private async Task<AcquiredLock> LockForWritingAsync(int waitingPressure = START_WAITING_PRESSURE)
        {
            waitingPressure = ApplyWaitOrder(waitingPressure);
            int initId = 0;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator, waitingPressure) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool nextInQueue = UpdateWaitingPressure(ref waitingPressure, ref initId);

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync(sleepTime);
                        //numWaitingApprox++;
                    }
                    else if (didReset) mre.Set();
                    if (waitingPressure > highestWaitingPressure) highestWaitingPressure = waitingPressure;
                }

                currentLockIndicator = lockIndicator;
            }
            highestWaitingPressure = MIN_WAITING_PRESSURE;
            if (reentranceSupported) reentranceIndicator.Value = FIRST_WRITE_ENTERED;

            return new AcquiredLock(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator, int waitingPressure)
        {
            return currentLockIndicator == WRITE_LOCK || waitingPressure < highestWaitingPressure || (reentranceSupported && waitingForUpgrade == TRUE);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockIndicator, int waitingPressure)
        {
            return currentLockIndicator != NO_LOCK || waitingPressure < highestWaitingPressure;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            if (!reentranceSupported || !HandleReentranceForReadExit())
            {
                var newLockIndicator = Interlocked.Decrement(ref lockIndicator);
                if (NO_LOCK == newLockIndicator)
                {
                    numWaitingApprox = 0;
                    globalInitId++;                    
                    mre.Set();
                }
            }
        }

        private bool HandleReentranceForReadExit()
        {
            var currentReentranceIndicator = reentranceIndicator.Value;
            currentReentranceIndicator--;

            if (currentReentranceIndicator == NOT_ENTERED)
            {
                reentranceIndicator.Value = NOT_ENTERED;
                return false; // go on, unlock...
            }
            else
            {
                reentranceIndicator.Value = currentReentranceIndicator;
                return true; // still a writelock in place
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock(bool downgrade)
        {
            if (!reentranceSupported || !HandleReentranceForWriteExit(downgrade))
            {
                numWaitingApprox = 0;
                globalInitId++;
                lockIndicator = NO_LOCK;
                mre.Set();
            }
        }

        private bool HandleReentranceForWriteExit(bool downgrade)
        {
            bool done = false;
            var currentReentranceIndicator = reentranceIndicator.Value;
            currentReentranceIndicator++;
            if (currentReentranceIndicator == NOT_ENTERED)
            {
                reentranceIndicator.Value = NOT_ENTERED;
                done = false; // go on, unlock...
            }
            else if (downgrade)
            {
                reentranceIndicator.Value = -currentReentranceIndicator;
                lockIndicator = FIRST_READ_LOCK;
                done = true; // now it's a readlock again
            }
            else
            {
                reentranceIndicator.Value = currentReentranceIndicator;
                done = true; // still a writelock in place
            }

            return done;
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
                Exit();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Exit()
            {
                if (isWriteLock) parentLock?.ExitWriteLock(downgrade);
                else parentLock?.ExitReadLock();
                parentLock = null;
            }
        }

    }
}
