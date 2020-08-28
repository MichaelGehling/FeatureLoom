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

        const int CYCLE_LIMIT = 10_000;

        public const int MAX_PRIORITY = int.MaxValue;
        public const int MIN_PRIORITY = int.MinValue;
        public const int DEFAULT_PRIORITY = 0;
        public const int HIGH_PRIORITY = CYCLE_LIMIT + 1;
        public const int LOW_PRIORITY = -CYCLE_LIMIT - 1;
        public readonly TimeSpan sleepTime = 1000.Milliseconds(); 

        const int FALSE = 0;
        const int TRUE = 1;
        
        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockIndicator of -1 implies a write-lock, while a lockIndicator greater than 0 implies a read-lock.
        /// When entering a read-lock, the lockIndicator is increased and decreased when leaving a read-lock.
        /// When entering a write-lock, a WRITE_LOCK(-1) is set and set back to NO_LOCK(0) when the write-lock is left.
        /// </summary>
        volatile int lockIndicator = NO_LOCK;        
        volatile int highestPriority = MIN_PRIORITY;
        volatile int secondHighestPriority = MIN_PRIORITY;
        volatile int reentranceId = 0;
        volatile int waitingForUpgrade = FALSE;              

        bool reentranceSupported;
        AsyncLocal<int> reentranceIndicator;
        AsyncManualResetEvent mre = new AsyncManualResetEvent(true);

        Task<AcquiredLock> readLockTask;
        Task<AcquiredLock> writeLockTask;
        Task<AcquiredLock> upgradedLockTask;
        Task<AcquiredLock> reenteredLockTask;

        /// <summary>
        /// Creates a FeatureLock object that can be used to synchronize access to resources that do not support concurrency.
        /// It provides several mechanisms to cover all kind of usage-scenarios:
        /// syncronous and asynchronous access, parallel read-only, reentrance (if activated), and prioritization.
        /// The lock can be used with the using statement: using(myLock.Lock()){ ... }
        /// </summary>
        /// <param name="supportReentrance">Set to true if reentrancy (nested access) is needed.</param>
        public FeatureLock(bool supportReentrance = false)
        {
            this.reentranceSupported = supportReentrance;
            if (supportReentrance) reentranceIndicator = new AsyncLocal<int>();
            readLockTask = Task.FromResult(new AcquiredLock(this, LockMode.ReadLock));
            writeLockTask = Task.FromResult(new AcquiredLock(this, LockMode.WriteLock));
            upgradedLockTask = Task.FromResult(new AcquiredLock(this, LockMode.Upgraded));
            reenteredLockTask = Task.FromResult(new AcquiredLock(this, LockMode.Reenterd));
        }        

        public bool IsLocked => lockIndicator != NO_LOCK;
        public bool IsWriteLocked => lockIndicator == WRITE_LOCK;
        public bool IsReadOnlyLocked => lockIndicator >= FIRST_READ_LOCK;
        public int CountParallelReadLocks => IsReadOnlyLocked ? lockIndicator : 0;
        public bool IsReentranceSupported => reentranceSupported;
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
            // Invalidate reentrance indicator
            if (reentranceSupported) reentranceIndicator.Value = reentranceId - 1;
            await asyncCall();
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
        public Task RunDeferredTask(Action syncCall)
        {
            // Invalidate reentrance indicator
            if (reentranceSupported) reentranceIndicator.Value = reentranceId - 1;
            return Task.Run(syncCall);
        }

        /// <summary>
        /// Acquires the lock. If the lock is already acquired, the execution will be blocked until it was released first.
        /// 
        /// The lock is roughly fair, meaning that the first in line will acquire the lock next. Though, if multiple start waiting
        /// for a release at the same time, they will have the same priority, but once it was released, the priority is increased and 
        /// candidates coming later to the party will have to wait longer, due to a lower priority.     
        /// 
        /// If the lock needs to be acquired multiple times in nested code, the supportReentrance flag from the constructor must be set to true to avoid a deadlock.
        /// </summary>
        /// <example>
        /// using(myLock.Lock())
        /// {
        ///   // do your worked on the restricted resource
        /// }
        /// </example>
        /// <param name="priority">The lock attempt can start with a lower or higher priority to change the waiting behaviour.</param>
        /// <returns>The AcquiredLock object wich must be exited or disposed when the job is done (otherwise the lock will stay forever)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock(int priority = DEFAULT_PRIORITY)
        {
            var currentLockIndicator = lockIndicator;
            if(reentranceSupported && currentLockIndicator != NO_LOCK)
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

            if(reentranceSupported) reentranceIndicator.Value = ++reentranceId;

            return new AcquiredLock(this, LockMode.WriteLock);
        }

        /// <summary>
        /// Acquires the lock for read-only. If the lock is already acquired for writing, the execution will be blocked until it was released first.
        /// If it was already acquired in read-only mode, the lock can be acquired again for parallel reading.
        /// 
        /// The lock is roughly fair, meaning that the first in line will acquire the lock next. Though, if multiple start waiting
        /// for a release at the same time, they will have the same priority, but once it was released, the priority is increased and 
        /// candidates coming later to the party will have to wait longer, due to a lower priority.     
        /// 
        /// If the lock needs to be re-acquired also for writing in nested code, the supportReentrance flag from the constructor must be set to true to avoid a deadlock.
        /// If a read-only lock is reentered for writing, it will temporarily be upgraded and downgraded again after releasing the write-lock. 
        /// If the lock is acquired in read-only mode by multiple in parallel, an upgrading attempt will have to wait until only the last read-only lock owner is left.
        /// </summary>
        /// <example>
        /// using(myLock.LockReadOnly())
        /// {
        ///   // do your worked on the restricted resource
        /// }
        /// </example>
        /// <param name="priority">The lock attempt can start with a lower or higher priority to change the waiting behaviour.</param>
        /// <returns>The AcquiredLock object wich must be exited or disposed when the job is done (otherwise the lock will stay forever)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnly(int priority = DEFAULT_PRIORITY)
        {
            var currentLockIndicator = lockIndicator;
            if(reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                if(reentranceIndicator.Value == reentranceId) return new AcquiredLock(this, LockMode.Reenterd);
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

            if(reentranceSupported)
            {
                if(newLockIndicator == FIRST_READ_LOCK) reentranceId++;
                reentranceIndicator.Value = reentranceId;
            }

            return new AcquiredLock(this, LockMode.ReadLock);
        }

        /// <summary>
        /// Acquires the lock. If the lock is already acquired, the execution will be awaited (suspended without blocking the thread) until it was released first.
        /// 
        /// The lock is roughly fair, meaning that the first in line will acquire the lock next. Though, if multiple start waiting
        /// for a release at the same time, they will have the same priority, but once it was released, the priority is increased and 
        /// candidates coming later to the party will have to wait longer, due to a lower priority.     
        /// 
        /// If the lock needs to be acquired multiple times in nested code, the supportReentrance flag from the constructor must be set to true to avoid a deadlock.
        /// Then it is possible to reenter the lock for reading and writing.
        /// </summary>
        /// <example>
        /// using(await myLock.LockAsync())
        /// {
        ///   // do your worked on the restricted resource
        /// }
        /// </example>
        /// <param name="priority">The lock attempt can start with a lower or higher priority to change the waiting behaviour.</param>
        /// <returns>The AcquiredLock object wich must be exited or disposed when the job is done (otherwise the lock will stay forever)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockAsync(int priority = DEFAULT_PRIORITY)
        {
            if(TryLockForWriting(priority, out LockMode mode))
            {
                if(mode == LockMode.WriteLock) return writeLockTask;
                else if(mode == LockMode.Reenterd) return reenteredLockTask;
                else return upgradedLockTask;
            }
            else return LockForWritingAsync(priority);
        }

        /// <summary>
        /// Acquires the lock for read-only. If the lock is already acquired for writing, the execution will be awaited (suspended without blocking the thread) 
        /// until it was released first. If it was already acquired in read-only mode, the lock can be acquired again for parallel reading.
        /// 
        /// The lock is roughly fair, meaning that the first in line will acquire the lock next. Though, if multiple start waiting
        /// for a release at the same time, they will have the same priority, but once it was released, the priority is increased and 
        /// candidates coming later to the party will have to wait longer, due to a lower priority.
        /// 
        /// If the lock needs to be re-acquired also for writing in nested code, the supportReentrance flag from the constructor must be set to true to avoid a deadlock.
        /// If a read-only lock is reentered for writing, it will temporarily be upgraded and downgraded again after releasing the write-lock. 
        /// If the lock is acquired in read-only mode by multiple in parallel, an upgrading attempt will have to wait until only the last read-only lock owner is left.
        /// </summary>
        /// <example>
        /// using(await myLock.LockReadOnlyAsync())
        /// {
        ///   // do your worked on the restricted resource
        /// }
        /// </example>
        /// <param name="priority">The lock attempt can start with a lower or higher priority to change the waiting behaviour.</param>
        /// <returns>The AcquiredLock object wich must be exited or disposed when the job is done (otherwise the lock will stay forever)</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReadOnlyAsync(int priority = DEFAULT_PRIORITY)
        {
            if(TryLockForReadingAsync(priority, out LockMode mode))
            {
                if(mode == LockMode.ReadLock) return readLockTask;
                else return reenteredLockTask;
            }
            else return LockForReadingAsync(priority);
        }

        /// <summary>
        /// Tries to acquires the lock. If the lock is already acquired, the execution will be blocked for the defined timeout (or return immediatly in case of no timeout) 
        /// or until it was released. If the attempt failed it will return false, otherwise true and the acquiredLock is returned as an out parameter.
        /// It must be released after usage by calling Exit on it. Alternatively, it can be used with a using statement:
        /// if (myLock.TryLock(out writeLock) using(writeLock) { ... }
        /// 
        /// The lock is roughly fair, meaning that the first in line will acquire the lock next. Though, if multiple start waiting
        /// for a release at the same time, they will have the same priority, but once it was released, the priority is increased and 
        /// candidates coming later to the party will have to wait longer, due to a lower priority.     
        /// 
        /// If the lock needs to be acquired multiple times in nested code, the supportReentrance flag from the constructor must be set to true to avoid a deadlock.
        /// </summary>
        /// <example>
        /// if (myLock.TryLock(out writeLock) using(writeLock)
        /// {
        ///   // do your worked on the restricted resource
        /// }
        /// </example>
        /// <param name="writeLock">The AcquiredLock object which must be exited or disposed when the job is done (otherwise the lock will stay forever).</param>
        /// <param name="timeout">The timeout after which the lock attempt is cancelled.</param>
        /// <param name="priority">The lock attempt can start with a lower or higher priority to change the waiting behaviour.</param>
        /// <returns>True if lock was acquired, otherwise false.</returns>        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock writeLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            var timer = new TimeFrame(timeout);
            bool waited = false;
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            if(reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, timedOut, acquiredLock) = TryReenterForWritingWithTimeout(currentLockIndicator, timer);
                writeLock = acquiredLock;
                if(reentered) return true;
                else if(timedOut) return false;
            }
            while(WriterMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool timedOut = TryLockWaitingLoop(ref priority, ref timer, ref cycleCount);

                if(timedOut)
                {
                    if(priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    writeLock = new AcquiredLock();
                    return false;
                }
                currentLockIndicator = lockIndicator;
                waited = true;
            }
            UpdateAfterEnter(priority, waited);
            if(reentranceSupported) reentranceIndicator.Value = ++reentranceId;

            writeLock = new AcquiredLock(this, LockMode.WriteLock);
            return true;
        }

        /// <summary>
        /// Tries to acquires the lock for read-only. If the lock is already acquired for writing, the execution will be blocked for the defined timeout 
        /// (or return immediatly in case of no timeout) or until it was released. 
        /// If it was already acquired in read-only mode, the lock can be acquired again for parallel reading. 
        /// If the attempt failed it will return false, otherwise true and the acquiredLock is returned as an out parameter.
        /// It must be released after usage by calling Exit on it. Alternatively, it can be used with a using statement:
        /// if (myLock.TryLock(out writeLock) using(writeLock) { ... }
        /// 
        /// The lock is roughly fair, meaning that the first in line will acquire the lock next. Though, if multiple start waiting
        /// for a release at the same time, they will have the same priority, but once it was released, the priority is increased and 
        /// candidates coming later to the party will have to wait longer, due to a lower priority.     
        /// 
        /// If the lock needs to be re-acquired also for writing in nested code, the supportReentrance flag from the constructor must be set to true to avoid a deadlock 
        /// (or constantly failing TryLock-attempts).
        /// If a read-only lock is reentered for writing, it will temporarily be upgraded and downgraded again after releasing the write-lock. 
        /// If the lock is acquired in read-only mode by multiple in parallel, an upgrading attempt will have to wait until only the last read-only lock owner is left.
        /// </summary>
        /// <example>
        /// if (myLock.TryLockReadOnly(out writeLock) using(writeLock)
        /// {
        ///   // do your worked on the restricted resource
        /// }
        /// </example>
        /// <param name="writeLock">The AcquiredLock object which must be exited or disposed when the job is done (otherwise the lock will stay forever).</param>
        /// <param name="timeout">The timeout after which the lock attempt is cancelled.</param>
        /// <param name="priority">The lock attempt can start with a lower or higher priority to change the waiting behaviour.</param>
        /// <returns>True if lock was acquired, otherwise false.</returns>   
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out AcquiredLock readLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            var timer = new TimeFrame(timeout);
            bool waited = false;
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                if (reentranceIndicator.Value == reentranceId)
                {
                    readLock = new AcquiredLock(this, LockMode.Reenterd);
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
            if (reentranceSupported)
            {
                if (newLockIndicator == FIRST_READ_LOCK) reentranceId++;
                reentranceIndicator.Value = reentranceId;
            }

            readLock = new AcquiredLock(this, LockMode.ReadLock);
            return true;
        }





        private bool TryLockReadOnlyWaitingLoop(ref int priority, ref TimeFrame timer, ref int cycleCount)
        {
            bool timedOut = false;
            if(timer.Elapsed) timedOut = true;
            else
            {
                bool nextInQueue = UpdatePriority(ref priority);

                if(!nextInQueue || ++cycleCount > CYCLE_LIMIT)
                {
                    cycleCount = 0;
                    bool didReset = mre.Reset();
                    if(lockIndicator != NO_LOCK)
                    {
                        if(!mre.Wait(timer.Remaining)) timedOut = true;
                    }
                    else if(didReset) mre.Set();
                }
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

                if(!nextInQueue || ++cycleCount > CYCLE_LIMIT)
                {
                    cycleCount = 0;
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining)) timedOut = true;
                    }
                    else if (didReset) mre.Set();
                }
            }

            return timedOut;
        }

        private (bool reentered, bool timedOut, AcquiredLock acquiredLock) TryReenterForWritingWithTimeout(int currentLockIndicator, TimeFrame timer)
        {
            if (reentranceIndicator.Value == reentranceId)
            {
                if (currentLockIndicator == WRITE_LOCK)
                {
                    return (true, false, new AcquiredLock(this, LockMode.Reenterd));
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

            if(!nextInQueue || ++cycleCount > CYCLE_LIMIT)
            {
                cycleCount = 0;
                bool didReset = mre.Reset();
                if (lockIndicator != NO_LOCK)
                {
                    mre.Wait(sleepTime);
                }
                else if (didReset) mre.Set();
            }

            currentLockIndicator = lockIndicator;
            newLockIndicator = currentLockIndicator + 1;
            return priority;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryLockForReadingAsync(int priority, out LockMode mode)
        {
            var currentLockIndicator = lockIndicator;
            if(reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                mode = LockMode.Reenterd;
                return reentranceIndicator.Value == reentranceId;
            }
            var newLockIndicator = currentLockIndicator + 1;
            if(ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                mode = LockMode.ReadLock;
                return false;
            }
            else
            {
                if (reentranceSupported)
                {
                    if (newLockIndicator == FIRST_READ_LOCK) reentranceId++;
                    reentranceIndicator.Value = reentranceId;
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

                if(!nextInQueue || ++cycleCount > CYCLE_LIMIT)
                {
                    cycleCount = 0;
                    bool didReset = mre.Reset();
                    if(lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync(sleepTime);
                    }
                    else if(didReset) mre.Set();
                }
                waited = true;
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(priority, waited);
            if (reentranceSupported)
            {
                if (newLockIndicator == FIRST_READ_LOCK) reentranceId++;
                reentranceIndicator.Value = reentranceId;
            }

            return new AcquiredLock(this, LockMode.ReadLock);
        }

        private int LockWaitingLoop(ref int priority, ref int cycleCount)
        {
            int currentLockIndicator;
            bool nextInQueue = UpdatePriority(ref priority);

            if(!nextInQueue || ++cycleCount > CYCLE_LIMIT)
            {
                cycleCount = 0;

                bool didReset = mre.Reset();
                if(lockIndicator != NO_LOCK)
                {
                    mre.Wait(sleepTime);
                }
                else if(didReset) mre.Set();
            }                   

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
            if (reentranceIndicator.Value == reentranceId)
            {
                if (currentLockIndicator == WRITE_LOCK)
                {
                    return (true, new AcquiredLock(this, LockMode.Reenterd));
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
        private bool TryLockForWriting(int priority, out LockMode mode)
        {
            var currentLockIndicator = lockIndicator;
            if(reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, timedOut, acquiredLock) = TryReenterForWritingWithTimeout(currentLockIndicator, new TimeFrame());
                mode = acquiredLock.Mode;
                if (reentered) return true;
                else if(timedOut) return false;
            }
            if(WriterMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                mode = LockMode.WriteLock;
                return false;
            }
            else
            {
                mode = LockMode.WriteLock;
                if (reentranceSupported) reentranceIndicator.Value = ++reentranceId;
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

                if(!nextInQueue || ++cycleCount > CYCLE_LIMIT)
                {
                    cycleCount = 0;
                    if(highestPriority > HIGH_PRIORITY*10) mre.RunContinuationsAsynchronously = true;
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if(nextInQueue) mre.Wait(sleepTime);
                        else await mre.WaitAsync(sleepTime);
                    }
                    else if (didReset) mre.Set();
                }

                waited = true;
                currentLockIndicator = lockIndicator;
            }
            UpdateAfterEnter(priority, waited);
            if (reentranceSupported) reentranceIndicator.Value = ++reentranceId;

            return new AcquiredLock(this, LockMode.WriteLock);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator, int priority)
        {
            return currentLockIndicator == WRITE_LOCK || priority < highestPriority || (reentranceSupported && waitingForUpgrade == TRUE);
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
        private void ExitAndDowngrade()
        {
            lockIndicator = FIRST_READ_LOCK;
        }



        internal enum LockMode
        {
            WriteLock,
            ReadLock,
            Reenterd,
            Upgraded
        }

        public struct AcquiredLock : IDisposable
        {
            FeatureLock parentLock;
            LockMode mode;        

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
                else if (mode == LockMode.Reenterd) { /* do nothing, the lock will stay */ }
                else if (mode == LockMode.Upgraded) parentLock?.ExitAndDowngrade();
                parentLock = null;
            }
            public bool IsActive => parentLock != null;

            internal LockMode Mode => mode;

        }

    }
}
