using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Time;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public sealed class FeatureLock
    {
        #region Constants
        const int NO_LOCK = 0;
        const int WRITE_LOCK = -1;
        const int FIRST_READ_LOCK = 1;

        const int CYCLES_BEFORE_YIELDING = 200;
        const int CYCLES_BEFORE_SLEEPING = CYCLES_BEFORE_YIELDING + 200;
        const int PRIO_CYCLE_FACTOR = 3;

        const int FALSE = 0;
        const int TRUE = 1;
        #endregion Constants

        #region Variables
        // the main lock variable, indicating current locking state:
        // 0 means no lock
        // -1 means write lock
        // >=1 means read lock (number implies the count of parallel readers)
        volatile int lockIndicator = NO_LOCK;
        // If true, indicates that a reentrant write lock tries to upgrade an existing reentrant read lock,
        // but more than one reader is active, the upgrade must wait until only one reader is left
        volatile int waitingForUpgrade = FALSE;
        // Keeps the last reentrancyId of the "logical thread". 
        // A value that differes from the currently valid reentrancyId implies that the lock was not acquired before in this "logical thread",
        // though it must be acquired and cannot simply be reentered.
        AsyncLocal<int> reentrancyIndicator = new AsyncLocal<int>();
        // The currently valid reentrancyId. It must never be 0, as this is the default value of the reentrancyIndicator.
        volatile int reentrancyId = 1;
        // Indicates that (probably) one or more other waiting candidates need to be waked up after the lock was exited.
        // It may happen that anySleeping is true, though actually no one needs to be awaked, but it will never happen that it is false if someone needs to be awaked.
        volatile bool anySleeping = false;
        // Used to synchronize sleeping and waking up code sections
        FastSpinLock sleepLock = new FastSpinLock();        
        // If a candidate must wait and the spinning-phase is over, it will go to sleep using a sleep handle.
        // The sleep handle will be enqueued to respect the waiting order and after the lock was exited,
        // the first sleep handle from the queue will be waked up.
        Queue<IWakeOrder> wakeOrderQueue = new Queue<IWakeOrder>(0);
        // If a waiter is prioritized its sleep handle is enqueued in the prioritizedWakeOrderQueue, because this
        // will always be preferred over the normal wakeOrderQueue when the next candidate has to be waked up.
        Queue<IWakeOrder> prioritizedWakeOrderQueue = new Queue<IWakeOrder>(0);
        // A sleep handle for standard synchronous writing lock acquiring. 
        // It can be shared by multiple candidates (it then will be enqueued multiple timed), 
        // because it internally uses monitor and pulse to enqueue threads and only wake up the next one.
        SleepHandle sharedSleepHandle = new SleepHandle();
        // Like sharedSleepHandle, but for prioritized waiters.
        SleepHandle sharedPrioSleepHandle = new SleepHandle();
        // A sleep handle for synchronous read-only lock acquiring. 
        // It can be shared by multiple candidates (it will only be enqueued once at a time), 
        // because it internally uses monitor and pulseAll to wake up all waiting threads at once.
        ReadOnlySleepHandle sharedReadOnlySleepHandle = new ReadOnlySleepHandle();
        #endregion Variables

        #region PreparedTasks
        // These variables keep already completed task objects are prepared in advance for reuse,
        // in order to reduce garbage and improve performance by handling async calls synchronously,
        // if they don't require any waiting
        Task<AcquiredLock> readLockTask;
        Task<AcquiredLock> reenterableReadLockTask;
        Task<AcquiredLock> writeLockTask;
        Task<AcquiredLock> reenterableWriteLockTask;
        Task<AcquiredLock> upgradedLockTask;
        Task<AcquiredLock> reenteredLockTask;
        Task<LockAttempt> failedAttemptTask;
        Task<LockAttempt> readLockAttemptTask;
        Task<LockAttempt> reenterableReadLockAttemptTask;
        Task<LockAttempt> writeLockAttemptTask;
        Task<LockAttempt> reenterableWriteLockAttemptTask;
        Task<LockAttempt> upgradedLockAttemptTask;
        Task<LockAttempt> reenteredLockAttemptTask;
        #endregion PreparedTasks

        /// <summary>
        /// A multi-purpose high-performance lock object that can be used in synchronous and asynchronous contexts.
        /// It supports reentrancy, prioritized lock acquiring, trying for lock acquiring with timeout and 
        /// read-only locking for parallel access (incl. automatic upgrading/downgrading in conjunction with reentrancy).
        /// When the lock is acquired it returns a handle that can be used with a using statement for simple and clean usage.
        /// Example: using(myLock.Lock()) { ... }
        /// </summary>
        public FeatureLock()
        {
            readLockTask = Task.FromResult(new AcquiredLock(this, LockMode.ReadLock));
            reenterableReadLockTask = Task.FromResult(new AcquiredLock(this, LockMode.ReadLockReenterable));
            writeLockTask = Task.FromResult(new AcquiredLock(this, LockMode.WriteLock));
            reenterableWriteLockTask = Task.FromResult(new AcquiredLock(this, LockMode.WriteLockReenterable));
            upgradedLockTask = Task.FromResult(new AcquiredLock(this, LockMode.Upgraded));
            reenteredLockTask = Task.FromResult(new AcquiredLock(this, LockMode.Reentered));

            failedAttemptTask = Task.FromResult(new LockAttempt(new AcquiredLock()));
            readLockAttemptTask = Task.FromResult(new LockAttempt(new AcquiredLock(this, LockMode.ReadLock)));
            reenterableReadLockAttemptTask = Task.FromResult(new LockAttempt(new AcquiredLock(this, LockMode.ReadLockReenterable)));
            writeLockAttemptTask = Task.FromResult(new LockAttempt(new AcquiredLock(this, LockMode.WriteLock)));
            reenterableWriteLockAttemptTask = Task.FromResult(new LockAttempt(new AcquiredLock(this, LockMode.WriteLockReenterable)));
            upgradedLockAttemptTask = Task.FromResult(new LockAttempt(new AcquiredLock(this, LockMode.Upgraded)));
            reenteredLockAttemptTask = Task.FromResult(new LockAttempt(new AcquiredLock(this, LockMode.Reentered)));
        }

        
        public bool IsLocked => lockIndicator != NO_LOCK;
        public bool IsWriteLocked => lockIndicator == WRITE_LOCK;
        public bool IsReadOnlyLocked => lockIndicator >= FIRST_READ_LOCK;
        public int CountParallelReadLocks => IsReadOnlyLocked ? lockIndicator : 0;
        public bool IsWriteLockWaitingForUpgrade => waitingForUpgrade == TRUE;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasValidReentrancyContext() => reentrancyId == reentrancyIndicator.Value;

        #region ReentrancyContext
        /// <summary>
        /// When an async call is executed within an acquired lock, but not awaited IMMEDIATLY (or not awaited at all),
        /// reentrancy may lead to collisions, because a parallel executed call will keep its reentrancy context until finished.
        /// This method will remove the reentrancy context before calling the async method, so that a possible locking attempt
        /// will be delayed until the already aquired lock is exited.
        /// IMPORTANT: If the async call is awaited before the acquired lock is exited, DO NOT use this method,
        /// otherwise it will lead to a deadlock if the called method tries to acquire the already acquired lock!
        /// </summary>
        /// <param name="asyncCall">An async method that is not awaited while the lock is acquired</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task RunDeferredAsync(Func<Task> asyncCall)
        {
            RemoveReentrancyContext();
            await asyncCall().ConfigureAwait(false);
        }

        /// <summary>
        /// When an async call is executed within an acquired lock, but not awaited IMMEDIATLY (or not awaited at all),
        /// reentrancy may lead to collisions, because the parallel executed call will keep its reentrancy context until finished.
        /// This method will remove the reentrancy context before calling the async method, so that a possible locking attempt
        /// will be delayed until the already aquired lock is exited.
        /// IMPORTANT: If the async call is awaited before the acquired lock is exited, DO NOT use this method,
        /// otherwise it will lead to a deadlock if the called method tries to acquire the already acquired lock!
        /// </summary>
        /// <param name="asyncCall">An async method that is not awaited while the lock is acquired</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<T> RunDeferredAsync<T>(Func<Task<T>> asyncCall)
        {
            RemoveReentrancyContext();
            return await asyncCall().ConfigureAwait(false);
        }

        /// <summary>
        /// When a new task is executed within an acquired lock, but not awaited IMMEDIATLY (or not awaited at all),
        /// reentrancy may lead to collisions, because the parallel executed task will keep its reentrancy context until finished.
        /// This method will run the passed action in a new Task and remove the reentrancy context before, 
        /// so that a possible locking attempt will be delayed until the already aquired lock is exited.
        /// IMPORTANT: If the task is awaited BEFORE the acquired lock is exited, DO NOT use this method,
        /// otherwise it will lead to a deadlock if the called method tries to acquire the already acquired lock!
        /// </summary>
        /// <param name="syncCall">An synchronous method that should run in a parallel task which is not awaited while the lock is acquired</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task RunDeferredTask(Action syncCall)
        {
            RemoveReentrancyContext();
            await Task.Run(syncCall);
        }

        /// <summary>
        /// When a new task or an async call is executed within an acquired lock, but not awaited IMMEDIATLY (or not awaited at all),
        /// reentrancy may lead to collisions, because the parallel executed task/call will keep its reentrancy context until finished.
        /// This method will remove the reentrancy context FROM WITHIN the new task or async call,
        /// so that a possible locking attempt will be delayed until the already aquired lock is exited.
        /// Alternatively, you can remove the reentrancy context from caller's side by using RunDeferredAsync or RunDeferredTask().
        /// IMPORTANT: If the task or async call is awaited BEFORE the acquired lock is exited, DO NOT use this method,
        /// otherwise it will lead to a deadlock if the called method tries to acquire the already acquired lock!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReentrancyContext()
        {
            // Invalidate reentrancy indicator
            reentrancyIndicator.Value = 0;
        }
        #endregion ReentrancyContext


        private bool MustTryToSleep(int cycleCount, bool prioritized)
        {
            return (!prioritized && anySleeping) || cycleCount > CYCLES_BEFORE_SLEEPING || waitingForUpgrade == TRUE;
        }

        private bool MustAsyncTryToSleep(int cycleCount, bool prioritized)
        {            
            return (!prioritized && anySleeping) || cycleCount > CYCLES_BEFORE_SLEEPING || IsThreadPoolCloseToStarving() || waitingForUpgrade == TRUE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NewReentrancyId() => ++reentrancyId == 0 ? ++reentrancyId : reentrancyId;


        #region Lock
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock()
        {
            if (NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) Lock_Wait();
            return new AcquiredLock(this, LockMode.WriteLock);
        }

        private void Lock_Wait()
        {
            bool prioritized = false;
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if (MustTryToSleep(cycleCount, prioritized))
                {                    
                    if (SleepHandle.TrySleep(this, prioritized))
                    {
                        cycleCount = 0;
                        prioritized = true;
                    }
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));
        }
        #endregion Lock

        #region LockAsync
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockAsync()
        {
            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) return writeLockTask;
            else return LockAsync_Wait(LockMode.WriteLock);
        }

        private async Task<AcquiredLock> LockAsync_Wait(LockMode mode)
        {
            bool prioritized = false;
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if (MustAsyncTryToSleep(cycleCount, prioritized))
                {
                    if (await AsyncSleepHandle.TrySleepAsync(this, prioritized))
                    {
                        cycleCount = 0;
                        prioritized = true;
                    }
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));

            if (mode == LockMode.WriteLockReenterable) reentrancyIndicator.Value = NewReentrancyId();            
            return new AcquiredLock(this, mode);
        }
        #endregion LockAsync

        #region LockPrioritized
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockPrioritized()
        {
            if (NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) LockPrioritized_Wait();
            return new AcquiredLock(this, LockMode.WriteLock);
        }

        private void LockPrioritized_Wait()
        {
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if (cycleCount > CYCLES_BEFORE_SLEEPING * PRIO_CYCLE_FACTOR)
                {
                    if (SleepHandle.TrySleep(this, true))
                    {
                        cycleCount = 0;
                    }                    
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING * PRIO_CYCLE_FACTOR) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));
        }
        #endregion LockPrioritized

        #region LockPrioritizedAsync
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockPrioritizedAsync()
        {
            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) return writeLockTask;
            else return LockPrioritizedAsync_Wait(LockMode.WriteLock);
        }

        private async Task<AcquiredLock> LockPrioritizedAsync_Wait(LockMode mode)
        {
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if (cycleCount > CYCLES_BEFORE_SLEEPING || IsThreadPoolCloseToStarving())
                {
                    if (await AsyncSleepHandle.TrySleepAsync(this, true))
                    {
                        cycleCount = 0;
                    }
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));            

            if (mode == LockMode.WriteLockReenterable) reentrancyIndicator.Value = NewReentrancyId();
            return new AcquiredLock(this, mode);
        }
        #endregion LockPrioritizedAsync

        #region LockReadOnly
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnly()
        {
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if(newLockIndicator < FIRST_READ_LOCK || anySleeping || waitingForUpgrade == TRUE || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator)) LockReadOnly_Wait();
            return new AcquiredLock(this, LockMode.ReadLock);
        }

        private void LockReadOnly_Wait()
        {
            int currentLockIndicator;
            int newLockIndicator;
            bool prioritized = false;
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if(MustTryToSleep(cycleCount, prioritized))
                {                    
                    if (ReadOnlySleepHandle.TrySleep(this))
                    {
                        cycleCount = 0;
                        prioritized = true;
                    }
                }
                else if(cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;

            } while(newLockIndicator < FIRST_READ_LOCK|| waitingForUpgrade == TRUE || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator));
        }
        #endregion LockReadOnly

        #region LockReadOnlyAsync
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReadOnlyAsync()
        {
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if(newLockIndicator >= FIRST_READ_LOCK && !anySleeping && waitingForUpgrade == FALSE && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator)) return readLockTask;
            else return LockReadOnlyAsync_Wait(LockMode.ReadLock);
        }

        private async Task<AcquiredLock> LockReadOnlyAsync_Wait(LockMode mode)
        {
            int currentLockIndicator;
            int newLockIndicator;
            bool prioritized = false;
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if(MustAsyncTryToSleep(cycleCount, prioritized))
                {
                    if(await ReadOnlySleepHandle.TrySleepAsync(this))
                    {
                        cycleCount = 0;
                        prioritized = true;
                    }
                }
                else if(cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;

            } while(newLockIndicator < FIRST_READ_LOCK || waitingForUpgrade == TRUE || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator));

            if (mode == LockMode.ReadLockReenterable)
            {
                if(newLockIndicator == FIRST_READ_LOCK) NewReentrancyId();
                reentrancyIndicator.Value = reentrancyId;
            }

            return new AcquiredLock(this, mode);
        }
        #endregion LockReadOnlyAsync

        #region LockReentrant
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReentrant()
        {
            if (TryReenter(out AcquiredLock acquiredLock, true, out _)) return acquiredLock;
            if (NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) Lock_Wait();
            reentrancyIndicator.Value = NewReentrancyId();
            return new AcquiredLock(this, LockMode.WriteLockReenterable);
        }

        private bool TryReenter(out AcquiredLock acquiredLock, bool waitForUpgrade, out bool upgradePossible)
        {
            var currentLockIndicator = lockIndicator;
            if (currentLockIndicator != NO_LOCK && HasValidReentrancyContext())
            {
                if (currentLockIndicator == WRITE_LOCK)
                {
                    upgradePossible = false;
                    acquiredLock = new AcquiredLock(this, LockMode.Reentered);
                    return true;
                }
                else if (currentLockIndicator >= FIRST_READ_LOCK)
                {
                    // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
                    if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
                    // Waiting for upgrade to writeLock
                    while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
                    {
                        if (waitForUpgrade) Thread.Yield();
                        else
                        {
                            waitingForUpgrade = FALSE;
                            upgradePossible = true;
                            acquiredLock = new AcquiredLock();
                            return false;
                        }                        
                    }
                    waitingForUpgrade = FALSE;

                    upgradePossible = false;
                    acquiredLock = new AcquiredLock(this, LockMode.Upgraded);
                    return true;
                }
            }
            upgradePossible = false;
            acquiredLock = new AcquiredLock();
            return false;
        }
        #endregion LockReentrant

        #region LockReentrantAsync
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReentrantAsync()
        {
            if(TryReenter(out AcquiredLock acquiredLock, false, out bool upgradePossible))
            {
                if(acquiredLock.mode == LockMode.Reentered) return reenteredLockTask;
                else return upgradedLockTask;
            }
            else if(upgradePossible) return WaitForUpgradeAsync();

            if(NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {                
                reentrancyIndicator.Value = NewReentrancyId();
                return reenterableWriteLockTask;
            }
            else return LockAsync_Wait(LockMode.WriteLockReenterable);
        }

        private async Task<AcquiredLock> WaitForUpgradeAsync()
        {
            // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
            if(TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
            // Waiting for upgrade to writeLock
            while(FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
            {
                await Task.Yield();                
            }
            waitingForUpgrade = FALSE;
            return new AcquiredLock(this, LockMode.Upgraded);            
        }
        #endregion LockReentrantAsync

        #region LockReentrantPrioritized
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReentrantPrioritized()
        {
            if(TryReenter(out AcquiredLock acquiredLock, true, out _)) return acquiredLock;
            if(NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) LockPrioritized_Wait();
            reentrancyIndicator.Value = NewReentrancyId();
            return new AcquiredLock(this, LockMode.WriteLockReenterable);
        }
        #endregion LockReentrantPrioritized

        #region LockReentrantPrioritizedAsync
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReentrantPrioritizedAsync()
        {
            if(TryReenter(out AcquiredLock acquiredLock, false, out bool upgradePossible))
            {
                if(acquiredLock.mode == LockMode.Reentered) return reenteredLockTask;
                else return upgradedLockTask;
            }
            else if(upgradePossible) return WaitForUpgradeAsync();

            if(NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {                
                reentrancyIndicator.Value = NewReentrancyId();
                return reenterableWriteLockTask;
            }
            else return LockPrioritizedAsync_Wait(LockMode.WriteLockReenterable);
        }
        #endregion LockReentrantAsync

        #region LockReentrantReadOnly
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReentrantReadOnly()
        {
            if(TryReenterReadOnly()) return new AcquiredLock(this, LockMode.Reentered);

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if(newLockIndicator < FIRST_READ_LOCK || waitingForUpgrade == TRUE || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator)) LockReadOnly_Wait();

            if(newLockIndicator == FIRST_READ_LOCK) NewReentrancyId();
            reentrancyIndicator.Value = reentrancyId;
            return new AcquiredLock(this, LockMode.ReadLockReenterable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReenterReadOnly()
        {
            return lockIndicator != NO_LOCK && HasValidReentrancyContext();
        }

        #endregion LockReentrantReadOnly

        #region LockReentrantReadOnlyAsync
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReentrantReadOnlyAsync()
        {
            if(TryReenterReadOnly()) return reenteredLockTask;
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if(newLockIndicator >= FIRST_READ_LOCK && waitingForUpgrade == FALSE && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if(newLockIndicator == FIRST_READ_LOCK) NewReentrancyId();
                reentrancyIndicator.Value = reentrancyId;
                return reenterableReadLockTask;
            }
            else return LockReadOnlyAsync_Wait(LockMode.ReadLockReenterable);
        }
        #endregion LockReentrantReadOnlyAsync

        #region TryLock
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock acquiredLock)
        {
            if(NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }
        #endregion TryLock

        #region TryLockReadOnly
        public bool TryLockReadOnly(out AcquiredLock acquiredLock)
        {
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (newLockIndicator >= FIRST_READ_LOCK && !anySleeping && waitingForUpgrade == FALSE && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                acquiredLock = new AcquiredLock(this, LockMode.ReadLock);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }
        #endregion TryLockReadOnly

        #region TryLockReentrant
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReentrant(out AcquiredLock acquiredLock)
        {
            if(TryReenter(out acquiredLock, false, out _)) return true;

            if(NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {                
                reentrancyIndicator.Value = NewReentrancyId();
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }
        #endregion TryLockReentrant

        #region TryLockReentrantReadOnly
        public bool TryLockReentrantReadOnly(out AcquiredLock acquiredLock)
        {
            if(TryReenterReadOnly())
            {
                acquiredLock = new AcquiredLock(this, LockMode.Reentered);
                return true;
            }

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while(currentLockIndicator != WRITE_LOCK && waitingForUpgrade == FALSE)
            {
                if(currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
                {
                    if(newLockIndicator == FIRST_READ_LOCK) NewReentrancyId();
                    reentrancyIndicator.Value = reentrancyId;
                    acquiredLock = new AcquiredLock(this, LockMode.ReadLock);
                    return true;
                }
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            acquiredLock = new AcquiredLock();
            return false;
        }
        #endregion TryLockReentrantReadOnly

        #region TryLock_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(TimeSpan timeout, out AcquiredLock acquiredLock)
        {
            if(NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else if(timeout > TimeSpan.Zero && TryLock_Wait(timeout))
            {
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        private bool TryLock_Wait(TimeSpan timeout)
        {
            TimeFrame timer = new TimeFrame(timeout);
            bool prioritized = false;
            int cycleCount = 0;
            do
            {
                // only check time every 10th cycle until start yieldig or sleeping
                if(cycleCount > CYCLES_BEFORE_YIELDING || cycleCount % 10 == 9) 
                {
                    if(timer.Elapsed) return false;
                }

                cycleCount++;
                if(MustTryToSleep(cycleCount, prioritized))
                {
                    if(TimeOutSleepHandle.TrySleep(this, timer, prioritized))
                    {
                        cycleCount = 0;
                        prioritized = true;
                    }                    
                }
                else if(cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while(lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));

            return true;
        }
        #endregion TryLock_Timeout
        
        #region TryLockAsync_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockAsync(TimeSpan timeout)
        {
            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) return writeLockAttemptTask;
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.WriteLock, timeout);
            else return failedAttemptTask;
        }

        private async Task<LockAttempt> TryLockAsync_Wait(LockMode mode, TimeSpan timeout)
        {
            TimeFrame timer = new TimeFrame(timeout);
            bool prioritized = false;
            int cycleCount = 0;
            do
            {
                // only check time every 10th cycle until start yieldig or sleeping
                if (cycleCount > CYCLES_BEFORE_YIELDING || cycleCount % 10 == 9)
                {
                    if (timer.Elapsed) return new LockAttempt(new AcquiredLock());
                }

                cycleCount++;
                if(MustAsyncTryToSleep(cycleCount, prioritized))
                {
                    if(await AsyncTimeoutSleepHandle.TrySleepAsync(this, timer, prioritized))
                    {
                        cycleCount = 0;
                        prioritized = true;
                    }
                }
                else if(cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while(lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));

            if(mode == LockMode.WriteLockReenterable) reentrancyIndicator.Value = NewReentrancyId();
            return new LockAttempt(new AcquiredLock(this, mode));
        }
        #endregion TryLock_Timeout

        #region TryLockPrioritized_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockPrioritized(TimeSpan timeout, out AcquiredLock acquiredLock)
        {
            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLockPrioritized_Wait(timeout))
            {
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        private bool TryLockPrioritized_Wait(TimeSpan timeout)
        {
            TimeFrame timer = new TimeFrame(timeout);
            int cycleCount = 0;
            do
            {
                // only check time every 10th cycle until start yieldig or sleeping
                if (cycleCount > CYCLES_BEFORE_YIELDING || cycleCount % 10 == 9)
                {
                    if (timer.Elapsed) return false;
                }

                cycleCount++;
                if (cycleCount > CYCLES_BEFORE_SLEEPING * PRIO_CYCLE_FACTOR)
                {
                    if (TimeOutSleepHandle.TrySleep(this, timer, true))
                    {
                        cycleCount = 0;
                    }
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));

            return true;
        }
        #endregion TryLockPrioritized_Timeout

        #region TryLockPrioritizedAsync_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockPrioritizedAsync(TimeSpan timeout)
        {
            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) return writeLockAttemptTask;
            else if (timeout > TimeSpan.Zero) return TryLockPrioritizedAsync_Wait(LockMode.WriteLock, timeout);
            else return failedAttemptTask;
        }

        private async Task<LockAttempt> TryLockPrioritizedAsync_Wait(LockMode mode, TimeSpan timeout)
        {
            TimeFrame timer = new TimeFrame(timeout);
            int cycleCount = 0;
            do
            {
                // only check time every 10th cycle until start yieldig or sleeping
                if (cycleCount > CYCLES_BEFORE_YIELDING || cycleCount % 10 == 9)
                {
                    if (timer.Elapsed) return new LockAttempt(new AcquiredLock());
                }

                cycleCount++;
                if (cycleCount > CYCLES_BEFORE_SLEEPING || IsThreadPoolCloseToStarving())
                {
                    if (await AsyncTimeoutSleepHandle.TrySleepAsync(this, timer, true))
                    {
                        cycleCount = 0;
                    }
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));

            if (mode == LockMode.WriteLockReenterable) reentrancyIndicator.Value = NewReentrancyId();
            return new LockAttempt(new AcquiredLock(this, mode));
        }
        #endregion TryLock_Timeout

        #region TryLockReadOnly_Timeout
        public bool TryLockReadOnly(TimeSpan timeout, out AcquiredLock acquiredLock)
        {
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (newLockIndicator >= FIRST_READ_LOCK && !anySleeping && waitingForUpgrade == FALSE && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                acquiredLock = new AcquiredLock(this, LockMode.ReadLock);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLockReadOnly_Wait(timeout))
            {
                acquiredLock = new AcquiredLock(this, LockMode.ReadLock);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        private bool TryLockReadOnly_Wait(TimeSpan timeout)
        {
            TimeFrame timer = new TimeFrame(timeout);
            int currentLockIndicator;
            int newLockIndicator;
            bool prioritized = false;
            int cycleCount = 0;
            do
            {
                // only check time every 10th cycle until start yieldig or sleeping
                if (cycleCount > CYCLES_BEFORE_YIELDING || cycleCount % 10 == 9)
                {
                    if (timer.Elapsed) return false;
                }

                cycleCount++;
                if (MustTryToSleep(cycleCount, prioritized))
                {
                    if (TimeOutSleepHandle.TrySleep(this, timer, prioritized))
                    {
                        cycleCount = 0;
                        prioritized = true;
                    }
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;

            } while (newLockIndicator < FIRST_READ_LOCK || waitingForUpgrade == TRUE || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator));

            return true;
        }
        #endregion TryLockReadOnly_Timeout

        #region TryLockReadOnlyAsync_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReadOnlyAsync(TimeSpan timeout)
        {
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (newLockIndicator >= FIRST_READ_LOCK && !anySleeping && waitingForUpgrade == FALSE && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator)) return readLockAttemptTask;
            else if (timeout > TimeSpan.Zero) return TryLockReadOnlyAsync_Wait(LockMode.ReadLock, timeout);
            else return failedAttemptTask;
        }

        private async Task<LockAttempt> TryLockReadOnlyAsync_Wait(LockMode mode, TimeSpan timeout)
        {
            TimeFrame timer = new TimeFrame(timeout);
            int currentLockIndicator;
            int newLockIndicator;
            bool prioritized = false;
            int cycleCount = 0;
            do
            {
                // only check time every 10th cycle until start yieldig or sleeping
                if (cycleCount > CYCLES_BEFORE_YIELDING || cycleCount % 10 == 9)
                {
                    if (timer.Elapsed) return new LockAttempt(new AcquiredLock());
                }

                cycleCount++;
                if (MustAsyncTryToSleep(cycleCount, prioritized))
                {
                    if (await AsyncTimeoutSleepHandle.TrySleepAsync(this, timer, prioritized))
                    {
                        cycleCount = 0;
                        prioritized = true;
                    }
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;

            } while (newLockIndicator < FIRST_READ_LOCK || waitingForUpgrade == TRUE || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator));

            if (mode == LockMode.ReadLockReenterable)
            {
                if (newLockIndicator == FIRST_READ_LOCK) NewReentrancyId();
                reentrancyIndicator.Value = reentrancyId;
            }

            return new LockAttempt(new AcquiredLock(this, mode));
        }
        #endregion TryLockReadOnlyAsync_Timeout

        #region TryLockReentrant_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReentrant(TimeSpan timeout, out AcquiredLock acquiredLock)
        {
            if (TryReenter(out acquiredLock, false, out bool upgradePossible)) return true;
            else if (upgradePossible)
            {
                if (timeout > TimeSpan.Zero && WaitForUpgrade(timeout))
                {
                    acquiredLock = new AcquiredLock(this, LockMode.Upgraded);
                    return true;
                }
                else
                {
                    acquiredLock = new AcquiredLock();
                    return false;
                }
            }

            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                reentrancyIndicator.Value = NewReentrancyId();
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout))
            {
                reentrancyIndicator.Value = NewReentrancyId();
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        private bool WaitForUpgrade(TimeSpan timeout)
        {
            TimeFrame timer = new TimeFrame(timeout);
            // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
            if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");

            int cycleCount = 0;
            // Waiting for upgrade to writeLock
            while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
            {
                // only check time every 10th cycle until start yieldig or sleeping
                if (++cycleCount > CYCLES_BEFORE_YIELDING || cycleCount % 10 == 9)
                {
                    if (timer.Elapsed)
                    {
                        waitingForUpgrade = FALSE;
                        return false;
                    }
                }

                Thread.Yield();
            }
            waitingForUpgrade = FALSE;
            return true;
        }
        #endregion TryLockReentrant_Timeout

        #region TryLockReentrantAsync_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReentrantAsync(TimeSpan timeout)
        {
            if (TryReenter(out AcquiredLock acquiredLock, false, out bool upgradePossible))
            {
                if (acquiredLock.mode == LockMode.Reentered) return reenteredLockAttemptTask;
                else return upgradedLockAttemptTask;
            }
            else if (upgradePossible)
            {
                if (timeout > TimeSpan.Zero) return WaitForUpgradeAttemptAsync(timeout);
                else return failedAttemptTask;
            }

            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                reentrancyIndicator.Value = NewReentrancyId();
                return reenterableWriteLockAttemptTask;
            }
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.WriteLockReenterable, timeout);
            else return failedAttemptTask;
        }

        private async Task<LockAttempt> WaitForUpgradeAttemptAsync(TimeSpan timeout)
        {
            TimeFrame timer = new TimeFrame(timeout);
            // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
            if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");

            int cycleCount = 0;
            // Waiting for upgrade to writeLock
            while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
            {
                // only check time every 10th cycle until start yieldig or sleeping
                if (++cycleCount > CYCLES_BEFORE_YIELDING || cycleCount % 10 == 9)
                {
                    if (timer.Elapsed)
                    {
                        waitingForUpgrade = FALSE;
                        return new LockAttempt(new AcquiredLock());
                    }
                }

                await Task.Yield();
            }
            waitingForUpgrade = FALSE;
            return new LockAttempt(new AcquiredLock(this, LockMode.Upgraded));
        }
        #endregion TryLock_Timeout

        #region TryLockReentrantPrioritized_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReentrantPrioritized(TimeSpan timeout, out AcquiredLock acquiredLock)
        {
            if (TryReenter(out acquiredLock, false, out bool upgradePossible)) return true;
            else if (upgradePossible)
            {
                if (timeout > TimeSpan.Zero && WaitForUpgrade(timeout))
                {
                    acquiredLock = new AcquiredLock(this, LockMode.Upgraded);
                    return true;
                }
                else
                {
                    acquiredLock = new AcquiredLock();
                    return false;
                }
            }

            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                reentrancyIndicator.Value = NewReentrancyId();
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLockPrioritized_Wait(timeout))
            {
                reentrancyIndicator.Value = NewReentrancyId();
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }
        #endregion TryLockReentrantPrioritized_Timeout

        #region TryLockReentrantPrioritizedAsync_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReentrantPrioritizedAsync(TimeSpan timeout)
        {
            if (TryReenter(out AcquiredLock acquiredLock, false, out bool upgradePossible))
            {
                if (acquiredLock.mode == LockMode.Reentered) return reenteredLockAttemptTask;
                else return upgradedLockAttemptTask;
            }
            else if (upgradePossible)
            {
                if (timeout > TimeSpan.Zero) return WaitForUpgradeAttemptAsync(timeout);
                else return failedAttemptTask;
            }

            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                reentrancyIndicator.Value = NewReentrancyId();
                return reenterableWriteLockAttemptTask;
            }
            else if (timeout > TimeSpan.Zero) return TryLockPrioritizedAsync_Wait(LockMode.WriteLockReenterable, timeout);
            else return failedAttemptTask;
        }
        #endregion TryLockReentrantPriotirizedAsync_Timeout

        #region TryLockReentrantReadOnly_Timeout
        public bool TryLockReentrantReadOnly(TimeSpan timeout, out AcquiredLock acquiredLock)
        {
            if (TryReenterReadOnly())
            {
                acquiredLock = new AcquiredLock(this, LockMode.Reentered);
                return true;
            }

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (newLockIndicator >= FIRST_READ_LOCK && !anySleeping && waitingForUpgrade == FALSE && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (newLockIndicator == FIRST_READ_LOCK) NewReentrancyId();
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new AcquiredLock(this, LockMode.ReadLock);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLockReadOnly_Wait(timeout))
            {
                if (newLockIndicator == FIRST_READ_LOCK) NewReentrancyId();
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new AcquiredLock(this, LockMode.ReadLock);
                return true;
            }
            else
            {
                acquiredLock = new AcquiredLock();
                return false;
            }
        }

        #endregion TryLockReadOnly_Timeout

        #region TryLockReentrantReadOnlyAsync_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReentrantReadOnlyAsync(TimeSpan timeout)
        {
            if (TryReenterReadOnly()) return reenteredLockAttemptTask;

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (newLockIndicator >= FIRST_READ_LOCK && !anySleeping && waitingForUpgrade == FALSE && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (newLockIndicator == FIRST_READ_LOCK) NewReentrancyId();
                reentrancyIndicator.Value = reentrancyId;
                return reenterableReadLockAttemptTask;
            }
            else if (timeout > TimeSpan.Zero) return TryLockReadOnlyAsync_Wait(LockMode.ReadLockReenterable, timeout);
            else return failedAttemptTask;
        }
        #endregion TryLockReadOnlyAsync_Timeout

        #region Exit
        private void ExitReadLock()
        {
            var newLockIndicator = Interlocked.Decrement(ref lockIndicator);
            if (NO_LOCK == newLockIndicator)
            {
                if (anySleeping) WakeUp();
            }
        }

        private void ExitWriteLock()
        {
            lockIndicator = NO_LOCK;
            Thread.MemoryBarrier();
            if (anySleeping) WakeUp();
        }

        volatile bool wakingUp = false;
        
        private void WakeUp()
        {
            if (lockIndicator != NO_LOCK || wakingUp) return;

            using(sleepLock.Lock())
            {
                wakingUp = true;

                bool wakedUpAny = false;
                while (prioritizedWakeOrderQueue.Count > 0 && !wakedUpAny)
                {
                    if (prioritizedWakeOrderQueue.TryDequeue(out var wakeOrder)) wakedUpAny = wakeOrder.TryWakeUp();
                }
                while (wakeOrderQueue.Count > 0 && !wakedUpAny)
                {
                    if (wakeOrderQueue.TryDequeue(out var wakeOrder)) wakedUpAny = wakeOrder.TryWakeUp();
                }
                anySleeping = prioritizedWakeOrderQueue.Count + wakeOrderQueue.Count > 0;

                wakingUp = false;
            }
        }


        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void ExitReentrantReadLock()
        {
            var newLockIndicator = Interlocked.Decrement(ref lockIndicator);
            if (NO_LOCK == newLockIndicator)
            {
                NewReentrancyId();
                if (anySleeping) WakeUp();
            }
            else
            {
                RemoveReentrancyContext();
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void ExitReentrantWriteLock()
        {
            NewReentrancyId();
            lockIndicator = NO_LOCK;
            Thread.MemoryBarrier();
            if (anySleeping) WakeUp();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReenteredLock()
        {
            // Nothing to do!
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
                else if (currentLockMode == LockMode.ReadLockReenterable)
                {
                    currentLockMode = LockMode.WriteLockReenterable;
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
                else if (currentLockMode == LockMode.WriteLockReenterable)
                {
                    currentLockMode = LockMode.ReadLockReenterable;
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

        #endregion Exit

        #region SleepHandles

        interface IWakeOrder
        {
            bool TryWakeUp();
        }

        class SleepHandle : IWakeOrder
        {
            public bool TryWakeUp()
            {
                Monitor.Enter(this);
                Monitor.Pulse(this);
                Monitor.Exit(this);
                return true;
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static bool TrySleep(FeatureLock parent, bool prioritized)
            {
                bool slept = false;
                parent.anySleeping = true;
                Thread.MemoryBarrier();
                if (parent.lockIndicator != NO_LOCK && parent.sleepLock.TryLock(out var acquiredLock))
                {
                    if (parent.lockIndicator != NO_LOCK) // TODO is this check really necessary?
                    {
                        var handle = prioritized ? parent.sharedPrioSleepHandle : parent.sharedSleepHandle;
                        parent.anySleeping = true;
                        if (prioritized) parent.prioritizedWakeOrderQueue.Enqueue(handle);
                        else parent.wakeOrderQueue.Enqueue(handle);
                        Monitor.Enter(handle);
                        acquiredLock.Exit(); // exit before sleeping

                        Monitor.Wait(handle);
                        Monitor.Exit(handle);
                        slept = true;
                    }
                    else acquiredLock.Exit();
                }

                if (!slept) Thread.Yield();
                return slept;
            }
        }

        class AsyncSleepHandle : IWakeOrder
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool TryWakeUp()
            {
                tcs.TrySetResult(true);
                return true;
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static async Task<bool> TrySleepAsync(FeatureLock parent, bool prioritized)
            {
                bool slept = false;
                parent.anySleeping = true;
                Thread.MemoryBarrier();

                if (parent.lockIndicator != NO_LOCK && parent.sleepLock.TryLock(out var acquiredLock))
                {
                    if (parent.lockIndicator != NO_LOCK) // TODO is this check really necessary?
                    {
                        var handle = new AsyncSleepHandle();
                        parent.anySleeping = true;
                        if (prioritized) parent.prioritizedWakeOrderQueue.Enqueue(handle);
                        else parent.wakeOrderQueue.Enqueue(handle);
                        var task = handle.tcs.Task;
                        acquiredLock.Exit(); // exit before sleeping

                        await task.ConfigureAwait(false);
                        slept = true;
                    }
                    else acquiredLock.Exit();
                }

                if (!slept)
                {
                    if (parent.IsThreadPoolCloseToStarving()) await Task.Yield();
                    else Thread.Yield();
                }
                return slept;
            }
        }

        class ReadOnlySleepHandle : IWakeOrder
        {
            TaskCompletionSource<bool> tcs;
            bool enqueued = false;

            public bool TryWakeUp()
            {
                // wake async readers
                if (tcs != null)
                {
                    tcs.TrySetResult(true);
                    tcs = null;
                }

                // wake sync readers
                Monitor.Enter(this);
                Monitor.PulseAll(this);
                Monitor.Exit(this);

                enqueued = false;

                return true;
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static bool TrySleep(FeatureLock parent)
            {
                bool slept = false;
                parent.anySleeping = true;
                Thread.MemoryBarrier();
                if (parent.lockIndicator != NO_LOCK && parent.sleepLock.TryLock(out var acquiredLock)) // TODO also sleep if lockIndicator implies readOnlyLock?
                {
                    if (parent.lockIndicator != NO_LOCK) // TODO is this check really necessary?
                    {
                        var handle = parent.sharedReadOnlySleepHandle;
                        parent.anySleeping = true;
                        if (!handle.enqueued)
                        {
                            parent.wakeOrderQueue.Enqueue(handle);
                            handle.enqueued = true;
                        }

                        Monitor.Enter(handle);
                        acquiredLock.Exit(); // exit before sleeping

                        Monitor.Wait(handle);
                        Monitor.Exit(handle);
                        slept = true;
                    }
                    else acquiredLock.Exit();
                }

                if (!slept) Thread.Yield();
                return slept;
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static async Task<bool> TrySleepAsync(FeatureLock parent)
            {
                bool slept = false;
                parent.anySleeping = true;
                Thread.MemoryBarrier();

                if (parent.lockIndicator != NO_LOCK && parent.sleepLock.TryLock(out var acquiredLock)) // TODO also sleep if lockIndicator implies readOnlyLock?
                {
                    if (parent.lockIndicator != NO_LOCK) // TODO is this check really necessary?
                    {
                        var handle = parent.sharedReadOnlySleepHandle;
                        parent.anySleeping = true;
                        if (handle.tcs == null) handle.tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        if (!handle.enqueued)
                        {
                            parent.wakeOrderQueue.Enqueue(handle);
                            handle.enqueued = true;
                        }
                        var task = handle.tcs.Task;
                        acquiredLock.Exit(); // exit before sleeping

                        await task.ConfigureAwait(false);
                        slept = true;
                    }
                    else acquiredLock.Exit();
                }

                if (!slept)
                {
                    if (parent.IsThreadPoolCloseToStarving()) await Task.Yield();
                    else Thread.Yield();
                }
                return slept;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsThreadPoolCloseToStarving()
        {
            ThreadPool.GetAvailableThreads(out int availableThreads, out _);
            return availableThreads == 0;
        }

        class TimeOutSleepHandle : IWakeOrder
        {
            bool timedOut = false;

            public bool TryWakeUp()
            {
                if (timedOut) return false;

                Monitor.Enter(this);
                Monitor.Pulse(this);
                Monitor.Exit(this);
                return true;
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static bool TrySleep(FeatureLock parent, TimeFrame timer, bool prioritized)
            {
                bool slept = false;
                parent.anySleeping = true;
                Thread.MemoryBarrier();
                if (parent.lockIndicator != NO_LOCK && parent.sleepLock.TryLock(out var acquiredLock))
                {
                    if (parent.lockIndicator != NO_LOCK) // TODO is this check really necessary?
                    {
                        var handle = new TimeOutSleepHandle();
                        parent.anySleeping = true;
                        if (prioritized) parent.prioritizedWakeOrderQueue.Enqueue(handle);
                        else parent.wakeOrderQueue.Enqueue(handle);
                        Monitor.Enter(handle);
                        acquiredLock.Exit(); // exit before sleeping

                        Monitor.Wait(handle, timer.Remaining);
                        handle.timedOut = true;
                        Monitor.Exit(handle);
                        slept = true;
                    }
                    else acquiredLock.Exit();
                }

                if (!slept) Thread.Yield();
                return slept;
            }
        }

        class AsyncTimeoutSleepHandle : IWakeOrder
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool timedOut = false;

            public bool TryWakeUp()
            {
                if (timedOut) return false;

                tcs.TrySetResult(true);
                return true;
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static async Task<bool> TrySleepAsync(FeatureLock parent, TimeFrame timer, bool prioritized)
            {
                bool slept = false;
                parent.anySleeping = true;
                Thread.MemoryBarrier();

                if (parent.lockIndicator != NO_LOCK && parent.sleepLock.TryLock(out var acquiredLock))
                {
                    if (parent.lockIndicator != NO_LOCK) // TODO is this check really necessary?
                    {
                        var handle = new AsyncTimeoutSleepHandle();
                        parent.anySleeping = true;
                        if (prioritized) parent.prioritizedWakeOrderQueue.Enqueue(handle);
                        else parent.wakeOrderQueue.Enqueue(handle);
                        var task = handle.tcs.Task;
                        acquiredLock.Exit(); // exit before sleeping

                        await task.WaitAsync(timer.Remaining).ConfigureAwait(false);
                        handle.timedOut = true;
                        slept = true;
                    }
                    else acquiredLock.Exit();
                }

                if (!slept)
                {
                    if (parent.IsThreadPoolCloseToStarving()) await Task.Yield();
                    else Thread.Yield();
                }
                return slept;
            }
        }


        #endregion SleepHandles

        #region LockHandles

        internal enum LockMode
        {
            WriteLock,
            ReadLock,
            WriteLockReenterable,
            ReadLockReenterable,
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
                // This check is removed, because the *possible* exception nearly doubles run time for the optimal fast-path.
                // Anyway, an NULL-exception will be thrown, in case of re-exit, because the parentlock cannot be accessed.
                //if (parentLock == null) throw new Exception("Lock was already released or wasn't successfully acquired!");

                if (mode == LockMode.WriteLock) parentLock.ExitWriteLock();
                else if (mode == LockMode.ReadLock) parentLock.ExitReadLock();
                else if (mode == LockMode.WriteLockReenterable) parentLock.ExitReentrantWriteLock();
                else if (mode == LockMode.ReadLockReenterable) parentLock.ExitReentrantReadLock();
                else if (mode == LockMode.Reentered) parentLock.ExitReenteredLock();
                else if (mode == LockMode.Upgraded) parentLock.DowngradeReentering();

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

        public struct LockAttempt
        {
            private AcquiredLock acquiredLock;

            internal LockAttempt(AcquiredLock acquiredLock)
            {
                this.acquiredLock = acquiredLock;
            }

            public bool Succeeded(out AcquiredLock acquiredLock)
            {
                acquiredLock = this.acquiredLock;
                return acquiredLock.IsActive;
            }
        }

        #endregion LockHandles

    }
}
