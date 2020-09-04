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

        const int SLEEP_CYCLE_LIMIT = 500;
        const int YIELD_CYCLE_LIMIT = 400;
        const int TASK_YIELD_FREQ = 10;

        public const int MAX_PRIORITY = int.MaxValue;
        public const int MIN_PRIORITY = int.MinValue;
        public const int DEFAULT_PRIORITY = 0;
        public const int HIGH_PRIORITY = SLEEP_CYCLE_LIMIT + 1;
        public const int LOW_PRIORITY = -SLEEP_CYCLE_LIMIT - 1;
        public TimeSpan wakeUpTime = 30.Seconds();

        const int FALSE = 0;
        const int TRUE = 1;

        readonly bool reentrancySupported;
        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockIndicator of -1 implies a write-lock, while a lockIndicator greater than 0 implies a read-lock.
        /// When entering a read-lock, the lockIndicator is increased and decreased when leaving a read-lock.
        /// When entering a write-lock, a WRITE_LOCK(-1) is set and set back to NO_LOCK(0) when the write-lock is left.
        /// </summary>
        volatile int lockIndicator = NO_LOCK;        
        volatile int highestPriority = MIN_PRIORITY;
        volatile int secondHighestPriority = MIN_PRIORITY;
        volatile int waitingForUpgrade = FALSE;
        volatile int reentrancyId = 0;

        AsyncLocal<int> reentrancyIndicator;

        AsyncManualResetEvent mre = new AsyncManualResetEvent(true);

        Task<AcquiredLock> readLockTask;
        Task<AcquiredLock> writeLockTask;
        Task<AcquiredLock> upgradedLockTask;
        Task<AcquiredLock> reenteredLockTask;

        public FeatureLock(bool supportReentrancy = false)
        {
            this.reentrancySupported = supportReentrancy;
            if (supportReentrancy) reentrancyIndicator = new AsyncLocal<int>();
            readLockTask = Task.FromResult(new AcquiredLock(this, LockMode.ReadLock));
            writeLockTask = Task.FromResult(new AcquiredLock(this, LockMode.WriteLock));
            upgradedLockTask = Task.FromResult(new AcquiredLock(this, LockMode.Upgraded));
            reenteredLockTask = Task.FromResult(new AcquiredLock(this, LockMode.Reentered));
        }        

        public bool IsLocked => lockIndicator != NO_LOCK;
        public bool IsWriteLocked => lockIndicator == WRITE_LOCK;
        public bool IsReadOnlyLocked => lockIndicator >= FIRST_READ_LOCK;
        public int CountParallelReadLocks => IsReadOnlyLocked ? lockIndicator : 0;
        public bool IsReentranceSupported => reentrancySupported;
        public bool IsWriteLockWaitingForUpgrade => waitingForUpgrade == TRUE;
        public int HighestWaitingPriority => highestPriority;
        public int SecondHighestWaitingPriority => secondHighestPriority;

        /// <summary>
        /// When an async call is awaited AFTER an acquired lock is exited (or not awaited at all),
        /// it may be executed on another thread and reentrancy may lead to collisions. 
        /// This method will remove the reentrancy context, so that an locking attempt
        /// will be delayed until the already aquired lock is exited.
        /// IMPORTANT: If the async call is awaited before the acquired lock is exited, DO NOT use this method,
        /// otherwise it will lead to a deadlock if the called method tries to attempt the already acquired lock!
        /// </summary>
        /// <param name="asyncCall">An async method that is not awaited while the lock is acquired</param>
        /// <returns></returns>
        public async Task RunDeferredAsync(Func<Task> asyncCall)
        {
            await Task.Yield();
            // Invalidate reentrancy indicator
            if (reentrancySupported) reentrancyIndicator.Value = reentrancyId - 1;
            await asyncCall().ConfigureAwait(false);
        }
        /// <summary>
        /// When an new task is executed in parallel and not awaited before an acquired lock is exited, 
        /// reentrancy may lead to collisions. 
        /// This method will runs the passed action in a new Task and remove the reentrancy context before, 
        /// so that an locking attempt will be delayed until the already aquired lock is exited.
        /// IMPORTANT: If the parallel task is awaited BEFORE the acquired lock is exited, DO NOT use this method,
        /// otherwise it will lead to a deadlock if the called method tries to attempt the already acquired lock!
        /// </summary>
        /// <param name="syncCall">An synchronous method that should run in a parallel task which is not awaited while the lock is acquired</param>
        /// <returns></returns>
        public async Task RunDeferredTask(Action syncCall)
        {
            // Invalidate reentrancy indicator
            if (reentrancySupported) reentrancyIndicator.Value = reentrancyId - 1;
            await Task.Run(syncCall);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock(int priority = DEFAULT_PRIORITY)
        {
            var currentLockIndicator = lockIndicator;
            if(reentrancySupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForWriting(currentLockIndicator);
                if(reentered) return acquiredLock;
            }
            bool waited = false;
            int cycleCount = 0;
            while(WriterMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                currentLockIndicator = LockWaitingLoop(ref priority, ref cycleCount);
                waited = true;
            }
            UpdateAfterEnter(priority, waited);

            if(reentrancySupported) reentrancyIndicator.Value = ++reentrancyId;

            return new AcquiredLock(this, LockMode.WriteLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnly(int priority = DEFAULT_PRIORITY)
        {
            var currentLockIndicator = lockIndicator;
            if(reentrancySupported && currentLockIndicator != NO_LOCK)
            {
                if(reentrancyIndicator.Value == reentrancyId) return new AcquiredLock(this, LockMode.Reentered);
            }
            bool waited = false;
            int cycleCount = 0;
            var newLockIndicator = currentLockIndicator + 1;
            while(ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                priority = LockReadOnlyWaitingLoop(priority, out currentLockIndicator, out newLockIndicator, ref cycleCount);
                waited = true;
            }
            UpdateAfterEnter(priority, waited);

            if(reentrancySupported)
            {
                if(newLockIndicator == FIRST_READ_LOCK) reentrancyId++;
                reentrancyIndicator.Value = reentrancyId;
            }

            return new AcquiredLock(this, LockMode.ReadLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockAsync(int priority = DEFAULT_PRIORITY)
        {
            if(TryLockForWritingAsync(priority, out LockMode mode))
            {
                if(mode == LockMode.WriteLock) return writeLockTask;
                else if(mode == LockMode.Reentered) return reenteredLockTask;
                else return upgradedLockTask;
            }
            else return LockForWritingAsync(priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReadOnlyAsync(int priority = DEFAULT_PRIORITY)
        {
            if (TryLockForReadingAsync(priority, out LockMode mode))
            {
                if (mode == LockMode.ReadLock) return readLockTask;
                else return reenteredLockTask;
            }
            else return LockForReadingAsync(priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out AcquiredLock readLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            var timer = new TimeFrame(timeout);
            bool waited = false;
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            if (reentrancySupported && currentLockIndicator != NO_LOCK)
            {
                if (reentrancyIndicator.Value == reentrancyId)
                {
                    readLock = new AcquiredLock(this, LockMode.Reentered);
                    return true;
                }
            }
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                bool timedOut = TryLockReadOnlyWaitingLoop(ref priority, ref timer, ref cycleCount);

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    readLock = new AcquiredLock();
                    return false;
                }
                waited = true;
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(priority, waited);
            if (reentrancySupported)
            {
                if (newLockIndicator == FIRST_READ_LOCK) reentrancyId++;
                reentrancyIndicator.Value = reentrancyId;
            }

            readLock = new AcquiredLock(this, LockMode.ReadLock);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock writeLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            var timer = new TimeFrame(timeout);
            bool waited = false;
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            if (reentrancySupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, timedOut , acquiredLock) = TryReenterForWritingWithTimeout(currentLockIndicator, timer);
                writeLock = acquiredLock;
                if (reentered) return true;                
                else if (timedOut) return false;                
            }            
            while (WriterMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool timedOut = TryLockWaitingLoop(ref priority, ref timer, ref cycleCount);

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    writeLock = new AcquiredLock();
                    return false;
                }
                currentLockIndicator = lockIndicator;
                waited = true;
            }
            UpdateAfterEnter(priority, waited);
            if (reentrancySupported) reentrancyIndicator.Value = ++reentrancyId;

            writeLock = new AcquiredLock(this, LockMode.WriteLock);
            return true;
        }





        private bool TryLockReadOnlyWaitingLoop(ref int priority, ref TimeFrame timer, ref int cycleCount)
        {
            bool timedOut = false;
            if(timer.Elapsed) timedOut = true;
            else
            {
                bool nextInQueue = UpdatePriority(ref priority);

                if(!nextInQueue || ++cycleCount > SLEEP_CYCLE_LIMIT)
                {
                    cycleCount = 1;
                    bool didReset = mre.Reset();
                    if(lockIndicator != NO_LOCK)
                    {
                        if(!mre.Wait(timer.Remaining)) timedOut = true;
                    }
                    if (didReset) mre.Set();
                }
                else if (cycleCount > YIELD_CYCLE_LIMIT) Thread.Yield();
            }

            return timedOut;
        }

        private bool TryLockWaitingLoop(ref int priority, ref TimeFrame timer, ref int cycleCount)
        {
            bool timedOut = false;
            if (timer.Elapsed) timedOut = true;
            else
            {
                bool nextInQueue = UpdatePriority(ref priority);

                if(!nextInQueue || ++cycleCount > SLEEP_CYCLE_LIMIT)
                {
                    cycleCount = 1;
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining)) timedOut = true;
                    }
                    else if (didReset) mre.Set();
                }
                else if (cycleCount > YIELD_CYCLE_LIMIT) Thread.Yield();
            }

            return timedOut;
        }

        private (bool reentered, bool timedOut, AcquiredLock acquiredLock) TryReenterForWritingWithTimeout(int currentLockIndicator, TimeFrame timer)
        {
            if (reentrancyIndicator.Value == reentrancyId)
            {
                if (currentLockIndicator == WRITE_LOCK)
                {
                    return (true, false, new AcquiredLock(this, LockMode.Reentered));
                }
                else if (currentLockIndicator >= FIRST_READ_LOCK)
                {
                    // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
                    if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
                    // Waiting for upgrade to writeLock
                    while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
                    {
                        if(timer.Elapsed)
                        {
                            waitingForUpgrade = FALSE;
                            return (false, true, new AcquiredLock());
                        }
                        Thread.Yield();
                    }
                    waitingForUpgrade = FALSE;
                    return (true, false, new AcquiredLock(this, LockMode.Upgraded));
                }
            }
            return (false, false, new AcquiredLock());
        }

        private int LockReadOnlyWaitingLoop(int priority, out int currentLockIndicator, out int newLockIndicator, ref int cycleCount)
        {
            bool nextInQueue = UpdatePriority(ref priority);

            if(!nextInQueue || ++cycleCount > SLEEP_CYCLE_LIMIT)
            {
                cycleCount = 1;
                bool didReset = mre.Reset();
                if (lockIndicator != NO_LOCK)
                {
                    mre.Wait();
                }
                else if (didReset) mre.Set();
            }
            else if (cycleCount > YIELD_CYCLE_LIMIT) Thread.Yield();

            currentLockIndicator = lockIndicator;
            newLockIndicator = currentLockIndicator + 1;
            return priority;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryLockForReadingAsync(int priority, out LockMode mode)
        {
            var currentLockIndicator = lockIndicator;
            if(reentrancySupported && currentLockIndicator != NO_LOCK)
            {
                mode = LockMode.Reentered;
                return reentrancyIndicator.Value == reentrancyId;
            }
            var newLockIndicator = currentLockIndicator + 1;
            if(ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                mode = LockMode.ReadLock;
                return false;
            }
            else
            {
                if (reentrancySupported)
                {
                    if (newLockIndicator == FIRST_READ_LOCK) reentrancyId++;
                    reentrancyIndicator.Value = reentrancyId;
                }
                mode = LockMode.ReadLock;
                return true;
            }
        }

        private async Task<AcquiredLock> LockForReadingAsync(int priority = DEFAULT_PRIORITY)
        {
            // Reentrance was already handled in TryLockReadOnly, see LockReadOnlyAsync()

            bool waited = false;
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                bool nextInQueue = UpdatePriority(ref priority);

                if(!nextInQueue || ++cycleCount > SLEEP_CYCLE_LIMIT)
                {
                    cycleCount = 1;
                    bool didReset = mre.Reset();
                    if(lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync().ConfigureAwait(false);
                    }
                    if (didReset) mre.Set();
                }
                else if (cycleCount > YIELD_CYCLE_LIMIT)
                {
                    if (cycleCount % TASK_YIELD_FREQ == TASK_YIELD_FREQ-1) await Task.Yield();
                    else Thread.Yield();
                }

                waited = true;
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(priority, waited);
            if (reentrancySupported)
            {
                if (newLockIndicator == FIRST_READ_LOCK) reentrancyId++;
                reentrancyIndicator.Value = reentrancyId;
            }

            return new AcquiredLock(this, LockMode.ReadLock);
        }

        private int LockWaitingLoop(ref int priority, ref int cycleCount)
        {
            int currentLockIndicator;
            bool nextInQueue = UpdatePriority(ref priority);

            if(!nextInQueue || ++cycleCount > SLEEP_CYCLE_LIMIT)
            {
                cycleCount = 1;

                bool didReset = mre.Reset();
                if (lockIndicator != NO_LOCK)
                {
                    mre.Wait();
                }
                else if (didReset) mre.Set();
            }
            else if (cycleCount > YIELD_CYCLE_LIMIT) Thread.Yield();

            currentLockIndicator = lockIndicator;
            return currentLockIndicator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool UpdatePriority(ref int priority)
        {
            if(priority < MAX_PRIORITY) priority++;

            bool nextInQueue = false;
            if(priority >= highestPriority) highestPriority = priority;
            else if(priority > secondHighestPriority) secondHighestPriority = priority;
            nextInQueue = priority >= highestPriority;

            return nextInQueue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateAfterEnter(int priority, bool waited)
        {
            if(waited)
            {
                highestPriority = secondHighestPriority;
                secondHighestPriority = MIN_PRIORITY;
            }
        }

        private (bool reentered, AcquiredLock acquiredLock) TryReenterForWriting(int currentLockIndicator)
        {
            if (reentrancyIndicator.Value == reentrancyId)
            {
                if (currentLockIndicator == WRITE_LOCK)
                {
                    return (true, new AcquiredLock(this, LockMode.Reentered));
                }
                else if (currentLockIndicator >= FIRST_READ_LOCK)
                {
                    // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
                    if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
                    // Waiting for upgrade to writeLock
                    while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
                    {
                        Thread.Yield();
                    }
                    waitingForUpgrade = FALSE;
                    return (true, new AcquiredLock(this, LockMode.Upgraded));
                }
            }
            return (false, new AcquiredLock());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryLockForWritingAsync(int priority, out LockMode mode)
        {
            var currentLockIndicator = lockIndicator;
            if(reentrancySupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, timedOut, acquiredLock) = TryReenterForWritingWithTimeout(currentLockIndicator, new TimeFrame());
                mode = acquiredLock.mode;
                if (reentered) return true;
                else if(timedOut) return false;
            }
            if(WriterMustWait(currentLockIndicator, priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                mode = LockMode.WriteLock;
                return false;
            }
            else
            {
                if (reentrancySupported) reentrancyIndicator.Value = ++reentrancyId;
                mode = LockMode.WriteLock;                
                return true;
            }
        }

        private async Task<AcquiredLock> LockForWritingAsync(int priority = DEFAULT_PRIORITY)
        {
            bool waited = false;

            // Reentrance was already handled in TryLock, see LockAsync()
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool nextInQueue = UpdatePriority(ref priority);

                if (!nextInQueue || ++cycleCount > SLEEP_CYCLE_LIMIT)
                {
                    cycleCount = 1;
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync().ConfigureAwait(false);
                    }
                    else if (didReset) mre.Set();
                }
                else if (cycleCount > YIELD_CYCLE_LIMIT)
                {
                    if (cycleCount % TASK_YIELD_FREQ == TASK_YIELD_FREQ-1) await Task.Yield();
                    else Thread.Yield();
                }

                waited = true;
                currentLockIndicator = lockIndicator;
            }
            UpdateAfterEnter(priority, waited);
            if (reentrancySupported) reentrancyIndicator.Value = ++reentrancyId;

            return new AcquiredLock(this, LockMode.WriteLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ResetMRE()
        {
            if (lockIndicator != NO_LOCK)
            {
                mre.Reset();
                if (lockIndicator == NO_LOCK) mre.Set();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator, int priority)
        {
            return currentLockIndicator == WRITE_LOCK || priority < highestPriority || (reentrancySupported && waitingForUpgrade == TRUE);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockIndicator, int priority)
        {
            return currentLockIndicator != NO_LOCK || priority < highestPriority;
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
            lockIndicator = NO_LOCK;
            mre.Set();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DowngradeReentering()
        {
            lockIndicator = FIRST_READ_LOCK;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryUpgrade(ref LockMode currentLockMode)
        {
            if (lockIndicator == FIRST_READ_LOCK)
            {
                if (currentLockMode == LockMode.ReadLock)
                {
                    currentLockMode = LockMode.WriteLock;
                    lockIndicator = WRITE_LOCK;
                    return true;
                }
                else if (currentLockMode == LockMode.Reentered)
                {
                    currentLockMode = LockMode.Upgraded;
                    lockIndicator = WRITE_LOCK;
                    return true;
                }
                else return false;
            }
            else return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryDowngrade(ref LockMode currentLockMode)
        {
            if (lockIndicator == WRITE_LOCK)
            {
                if (currentLockMode == LockMode.WriteLock)
                {
                    currentLockMode = LockMode.ReadLock;
                    lockIndicator = FIRST_READ_LOCK;
                    return true;
                }
                else if (currentLockMode == LockMode.Upgraded)
                {
                    currentLockMode = LockMode.Reentered;
                    lockIndicator = FIRST_READ_LOCK;
                    return true;
                }
                else return false;
            }
            else return false;
        }



        internal enum LockMode
        {
            WriteLock,
            ReadLock,
            Reentered,
            Upgraded
        }

        public struct AcquiredLock : IDisposable
        {
            FeatureLock parentLock;
            internal LockMode mode;        

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal AcquiredLock(FeatureLock parentLock, LockMode mode)
            {
                this.parentLock = parentLock;
                this.mode = mode;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                Exit();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Exit()
            {
                if (mode == LockMode.WriteLock) parentLock?.ExitWriteLock();
                else if (mode == LockMode.ReadLock) parentLock?.ExitReadLock();
                else if (mode == LockMode.Reentered) { /* do nothing, the lock will stay */ }
                else if (mode == LockMode.Upgraded) parentLock?.DowngradeReentering();
                parentLock = null;
            }

            public bool TryUpgradeToWriteMode()
            {
                return parentLock?.TryUpgrade(ref mode) ?? false;
            }

            public bool TryDowngradeToReadOnlyMode()
            {
                return parentLock?.TryDowngrade(ref mode) ?? false;
            }

            public bool IsActive => parentLock != null;
        }

    }
}
