using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Time;
using System;
using System.Collections.Generic;
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

        const int CYCLES_BEFORE_YIELDING = 200;
        const int CYCLES_BEFORE_SLEEPING = CYCLES_BEFORE_YIELDING + 50;
        const int PRIO_CYCLE_FACTOR = 3;

        const int NUM_PARALLEL_IDLE = 2;

        public const int MAX_PRIORITY = int.MaxValue;
        public const int MIN_PRIORITY = int.MinValue;
        public const int DEFAULT_PRIORITY = 0;
        public const int HIGH_PRIORITY = CYCLES_BEFORE_SLEEPING + 5;
        public const int LOW_PRIORITY = -CYCLES_BEFORE_SLEEPING - 5;

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
        volatile int waitingForUpgrade = FALSE;
        volatile int reentrancyId = 1;

        AsyncLocal<int> reentrancyIndicator;

        AsyncManualResetEvent mre = new AsyncManualResetEvent(true);


        const int BARRIER_OPEN = 0;
        const int BARRIER_SLEEP = 1;
        const int BARRIER_WAKEUP = 2;
        FastSpinLock sleepLock = new FastSpinLock();

        Queue<TaskCompletionSource<bool>> asyncWriterQueue = new Queue<TaskCompletionSource<bool>>(16);
        Queue<TaskCompletionSource<bool>> prioAsyncWriterQueue = new Queue<TaskCompletionSource<bool>>(5);
        TaskCompletionSource<bool> readersTcs;
        object priorityMonitor = new object();
        object writerMonitor = new object();
        object readerMonitor = new object();
        volatile int numPrioSleeping = 0;
        volatile int numSyncWritersSleeping = 0;
        volatile int numReadersSleeping = 0;        
        volatile bool anySleeping = false;

        volatile int ticketCounter = 0;
        volatile int nextTicket = 0;

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


        public FeatureLock()
        {
            reentrancyIndicator = new AsyncLocal<int>();

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
        public int HighestWaitingPriority => highestPriority;
        public int SecondHighestWaitingPriority => secondHighestPriority;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasValidReentrancyContext() => reentrancyId == reentrancyIndicator.Value;

        /// <summary>
        /// When an async call is awaited AFTER an acquired lock is exited (or not awaited at all),
        /// it may be executed on another thread and reentrancy may lead to collisions. 
        /// This method will remove the reentrancy context, so that an locking attempt
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
        /// When an new task is executed in parallel and not awaited before an acquired lock is exited, 
        /// reentrancy may lead to collisions. 
        /// This method will runs the passed action in a new Task and remove the reentrancy context before, 
        /// so that an locking attempt will be delayed until the already aquired lock is exited.
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
        /// When an new task is executed in parallel and not awaited before an acquired lock is exited or
        /// when an async call is awaited AFTER an acquired lock is exited (or not awaited at all),
        /// reentrancy may lead to collisions.
        /// This method will remove the reentrancy context FROM WITHIN the new task or async call,
        /// so that an locking attempt will be delayed until the already aquired lock is exited.
        /// IMPORTANT: If the task or async call is awaited BEFORE the acquired lock is exited, DO NOT use this method,
        /// otherwise it will lead to a deadlock if the called method tries to acquire the already acquired lock!
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveReentrancyContext()
        {
            // Invalidate reentrancy indicator
            reentrancyIndicator.Value = reentrancyId - 1;
        }

        private bool MustTryToSleep(int ticket, int cycleCount)
        {
            bool backInLine = (ticket >= nextTicket + NUM_PARALLEL_IDLE); //At nextTicket overFlow this will fail -> Ignore as it has no severe consequence and is only for short.
            return (backInLine && anySleeping) || cycleCount > CYCLES_BEFORE_SLEEPING;
        }

        private bool MustAsyncTryToSleep(int ticket, int cycleCount, int minThreadPoolThreads)
        {
            bool backInLine = (ticket >= nextTicket + NUM_PARALLEL_IDLE); //At nextTicket overFlow this will fail -> Ignore as it has no severe consequence and is only for short.
            return (backInLine && (anySleeping || IsThreadPoolCloseToStarving(minThreadPoolThreads))) || cycleCount > CYCLES_BEFORE_SLEEPING;
        }


        #region Lock

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock()
        {
            if (NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) Lock_Wait();
            return new AcquiredLock(this, LockMode.WriteLock);
        }

        private void Lock_Wait()
        {            
            int ticket = Interlocked.Increment(ref ticketCounter);
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if (MustTryToSleep(ticket, cycleCount))
                {                    
                    if (Sleep())
                    {
                        cycleCount = 0;
                    }
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));
            Interlocked.Increment(ref nextTicket);
            //if (ticketCounter < nextTicket) ticketCounter = nextTicket;

        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private bool Sleep()
        {
            bool slept = false;

            anySleeping = true;
            Thread.MemoryBarrier();

            if (lockIndicator != NO_LOCK && sleepLock.TryLock(out var acquiredLock))
            {
                Monitor.Enter(writerMonitor);
                if (lockIndicator != NO_LOCK)
                {
                    numSyncWritersSleeping++;
                    anySleeping = true;
                    acquiredLock.Exit(); // reopen before sleeping
                    Monitor.Wait(writerMonitor);
                    slept = true;
                }
                else acquiredLock.Exit();
                Monitor.Exit(writerMonitor);
            }
            else Thread.Yield();

            return slept;
        }
        #endregion Lock

        #region LockAsync

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockAsync()
        {
            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) return writeLockTask;
            else return LockAsync_Wait();
        }

        private async Task<AcquiredLock> LockAsync_Wait()
        {
            int ticket = Interlocked.Increment(ref ticketCounter);
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if (MustAsyncTryToSleep(ticket, cycleCount, 1))
                {
                    if (await SleepAsync())
                    {
                        cycleCount = 0;
                    }
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));

            Interlocked.Increment(ref nextTicket);
            //if (ticketCounter < nextTicket) ticketCounter = nextTicket;

            return new AcquiredLock(this, LockMode.WriteLock);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async Task<bool> SleepAsync()
        {
            bool slept = false;

            anySleeping = true;
            Thread.MemoryBarrier();

            if (lockIndicator != NO_LOCK && sleepLock.TryLock(out var acquiredLock))
            {
                anySleeping = true;
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                asyncWriterQueue.Enqueue(tcs);
                var task = tcs.Task;

                acquiredLock.Exit();
                await task.ConfigureAwait(false);
                slept = true;
            }
            else if (lockIndicator != NO_LOCK)
            {
                if (IsThreadPoolCloseToStarving(1)) await Task.Yield();
                else Thread.Yield();
            }
            return slept;
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
            int ticket = Interlocked.Increment(ref ticketCounter);
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if (cycleCount > CYCLES_BEFORE_SLEEPING * PRIO_CYCLE_FACTOR)
                {
                    cycleCount = 0;
                    SleepPrioritized();
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING * PRIO_CYCLE_FACTOR) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));
            Interlocked.Increment(ref nextTicket);
            //if (ticketCounter < nextTicket) ticketCounter = nextTicket;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void SleepPrioritized()
        {
            anySleeping = true;
            Thread.MemoryBarrier();

            if (lockIndicator != NO_LOCK && sleepLock.TryLock(out var acquiredLock))
            {
                Monitor.Enter(priorityMonitor);
                if (lockIndicator != NO_LOCK)
                {
                    numPrioSleeping++;
                    anySleeping = true;
                    acquiredLock.Exit(); // reopen before sleeping
                    Monitor.Wait(priorityMonitor);
                }
                else acquiredLock.Exit();
                Monitor.Exit(priorityMonitor);
            }
            else Thread.Yield();
        }

        #endregion LockPrioritized

        #region LockPrioritizedAsync

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockPrioritizedAsync()
        {
            if (NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK)) return writeLockTask;
            else return LockPrioritizedAsync_Wait();
        }

        private async Task<AcquiredLock> LockPrioritizedAsync_Wait()
        {
            int ticket = Interlocked.Increment(ref ticketCounter);
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if (cycleCount > CYCLES_BEFORE_SLEEPING || IsThreadPoolCloseToStarving(0))
                {
                    cycleCount = 0;
                    await SleepPrioritizedAsync();
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

            } while (lockIndicator != NO_LOCK || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));

            Interlocked.Increment(ref nextTicket);
            //if (ticketCounter < nextTicket) ticketCounter = nextTicket;

            return new AcquiredLock(this, LockMode.WriteLock);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async Task SleepPrioritizedAsync()
        {
            anySleeping = true;
            Thread.MemoryBarrier();

            if (lockIndicator != NO_LOCK && sleepLock.TryLock(out var acquiredLock))
            {
                anySleeping = true;
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                prioAsyncWriterQueue.Enqueue(tcs);
                var task = tcs.Task;

                acquiredLock.Exit();
                await task.ConfigureAwait(false);
            }
            else if (lockIndicator != NO_LOCK)
            {
                if (IsThreadPoolCloseToStarving(0)) await Task.Yield();
                else Thread.Yield();
            }
        }

        #endregion LockPrioritizedAsync

        #region LockReadOnly

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnly()
        {
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if(newLockIndicator < FIRST_READ_LOCK || waitingForUpgrade == TRUE || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator)) LockReadOnly_Wait();
            return new AcquiredLock(this, LockMode.ReadLock);
        }

        private void LockReadOnly_Wait()
        {
            int ticket = Interlocked.Increment(ref ticketCounter);

            int currentLockIndicator;
            int newLockIndicator;
            int cycleCount = 0;
            do
            {
                cycleCount++;
                if((ticket > nextTicket && anySleeping) || cycleCount > CYCLES_BEFORE_SLEEPING)
                {                    
                    if (SleepReadOnly())
                    {
                        cycleCount = 0;
                    }
                }
                else if(cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;

            } while(newLockIndicator < FIRST_READ_LOCK|| waitingForUpgrade == TRUE || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator));

            Interlocked.Increment(ref nextTicket);
            //if (ticketCounter < nextTicket) ticketCounter = nextTicket;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private bool SleepReadOnly()
        {
            bool slept = false;

            anySleeping = true;
            Thread.MemoryBarrier();

            if(lockIndicator == WRITE_LOCK && sleepLock.TryLock(out var acquiredLock))
            {
                Monitor.Enter(readerMonitor);
                if(lockIndicator == WRITE_LOCK)
                {
                    numReadersSleeping++;
                    anySleeping = true;
                    acquiredLock.Exit(); // reopen before sleeping
                    Monitor.Wait(readerMonitor);
                    slept = true;
                }
                else acquiredLock.Exit();
                Monitor.Exit(readerMonitor);
            }
            else Thread.Yield();

            return slept;
        }
        #endregion LockReadOnly









        public void WaitForWritingLock(int priority = DEFAULT_PRIORITY)
        {
            int cycleCount = 0;
            do
            {
                bool nextInQueue = UpdatePriority(ref priority);

                cycleCount++;
                if (!nextInQueue || cycleCount > CYCLES_BEFORE_SLEEPING)
                {
                    cycleCount = 1;

                    mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        mre.Wait();
                    }
                    else mre.Set();
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();
            }
            while (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK));

            highestPriority = secondHighestPriority;
            secondHighestPriority = MIN_PRIORITY;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReentrant(int priority = DEFAULT_PRIORITY)
        {
            var currentLockIndicator = lockIndicator;
            if (currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForWriting(currentLockIndicator);
                if (reentered) return acquiredLock;
            }
            int cycleCount = 0;
            while (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                priority = LockWaitingLoop(priority, ref cycleCount);
            }
            UpdateAfterEnter(cycleCount != 0);
            reentrancyIndicator.Value = ++reentrancyId;
            return new AcquiredLock(this, LockMode.WriteLockReenterable);
        }
        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnly(int priority = DEFAULT_PRIORITY)
        {
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                priority = LockReadOnlyWaitingLoop(priority, ref cycleCount);
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(cycleCount != 0);
            return new AcquiredLock(this, LockMode.ReadLock);
        }
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnlyReentrant(int priority = DEFAULT_PRIORITY)
        {
            var currentLockIndicator = lockIndicator;
            if (currentLockIndicator != NO_LOCK)
            {
                if (reentrancyIndicator.Value == reentrancyId) return new AcquiredLock(this, LockMode.Reentered);
            }
            int cycleCount = 0;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReentrantReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                priority = LockReadOnlyWaitingLoop(priority, ref cycleCount);
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(cycleCount != 0);

            if (newLockIndicator == FIRST_READ_LOCK) reentrancyId++;
            reentrancyIndicator.Value = reentrancyId;

            return new AcquiredLock(this, LockMode.ReadLockReenterable);
        }
        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockAsync(int priority = DEFAULT_PRIORITY)
        {
            if(TryGetWritingLock(priority)) return writeLockTask;
            else return LockForWritingAsync(priority);
        }*/

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReentrantAsync(int priority = DEFAULT_PRIORITY)
        {
            if (TryLockForWritingReentrantAsync(priority, out LockMode mode))
            {
                if (mode == LockMode.WriteLockReenterable) return reenterableWriteLockTask;
                else if (mode == LockMode.Reentered) return reenteredLockTask;
                else return upgradedLockTask;
            }
            else return LockForWritingReentrantAsync(priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReadOnlyAsync(int priority = DEFAULT_PRIORITY)
        {
            if (TryLockForReadingAsync(priority)) return readLockTask;
            else return LockForReadingAsync(priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReadOnlyReentrantAsync(int priority = DEFAULT_PRIORITY)
        {
            if (TryLockForReadingReentrantAsync(priority, out LockMode mode))
            {
                if (mode == LockMode.ReadLockReenterable) return reenterableReadLockTask;
                else return reenteredLockTask;
            }
            else return LockForReadingReentrantAsync(priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out AcquiredLock readLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            TimeFrame timer = new TimeFrame();
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);
                bool timedOut = TryLockWaitingLoop(ref priority, timer, ref cycleCount);

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    readLock = new AcquiredLock();
                    return false;
                }
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(cycleCount != 0);

            readLock = new AcquiredLock(this, LockMode.ReadLock);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnlyReentrant(out AcquiredLock readLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            TimeFrame timer = new TimeFrame();
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            if (currentLockIndicator != NO_LOCK)
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);
                if (reentrancyIndicator.Value == reentrancyId)
                {
                    readLock = new AcquiredLock(this, LockMode.Reentered);
                    return true;
                }
            }
            var newLockIndicator = currentLockIndicator + 1;
            while (ReentrantReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);
                bool timedOut = TryLockWaitingLoop(ref priority, timer, ref cycleCount);

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    readLock = new AcquiredLock();
                    return false;
                }
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(cycleCount != 0);

            if (newLockIndicator == FIRST_READ_LOCK) reentrancyId++;
            reentrancyIndicator.Value = reentrancyId;

            readLock = new AcquiredLock(this, LockMode.ReadLockReenterable);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReadOnlyAsync(TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            if (TryLockForReadingAsync(priority)) return readLockAttemptTask;
            else return TryLockForReadingAsync(timeout, priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<LockAttempt> TryLockForReadingAsync(TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            TimeFrame timer = new TimeFrame();
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);

                bool timedOut = false;
                if (timer.Elapsed) timedOut = true;
                else
                {
                    bool nextInQueue = UpdatePriority(ref priority);
                    cycleCount++;
                    if (!nextInQueue || cycleCount > CYCLES_BEFORE_SLEEPING)
                    {
                        cycleCount = 1;
                        bool didReset = mre.Reset();
                        if (lockIndicator != NO_LOCK)
                        {
                            if (!await mre.WaitAsync(timer.Remaining)) timedOut = true;
                        }
                        else if (didReset) mre.Set();
                    }
                    else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();
                }

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    return new LockAttempt(new AcquiredLock());
                }
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(cycleCount != 0);

            return new LockAttempt(new AcquiredLock(this, LockMode.ReadLock));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockAsync(TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            if (TryGetWritingLock(priority)) return writeLockAttemptTask;
            else return TryLockForWritingAsync(timeout, priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<LockAttempt> TryLockForWritingAsync(TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            TimeFrame timer = new TimeFrame();
            int cycleCount = 0;
            while (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);

                bool timedOut = false;
                if (timer.Elapsed) timedOut = true;
                else
                {
                    bool nextInQueue = UpdatePriority(ref priority);
                    cycleCount++;
                    if (!nextInQueue || cycleCount > CYCLES_BEFORE_SLEEPING)
                    {
                        cycleCount = 1;
                        bool didReset = mre.Reset();
                        if (lockIndicator != NO_LOCK)
                        {
                            if (!await mre.WaitAsync(timer.Remaining)) timedOut = true;
                        }
                        else if (didReset) mre.Set();
                    }
                    else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();
                }

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    return new LockAttempt(new AcquiredLock());
                }
            }
            UpdateAfterEnter(cycleCount != 0);

            return new LockAttempt(new AcquiredLock(this, LockMode.WriteLock));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock writeLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            TimeFrame timer = new TimeFrame();
            int cycleCount = 0;
            while (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);
                bool timedOut = TryLockWaitingLoop(ref priority, timer, ref cycleCount);

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    writeLock = new AcquiredLock();
                    return false;
                }
            }
            UpdateAfterEnter(cycleCount != 0);

            writeLock = new AcquiredLock(this, LockMode.WriteLock);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReadOnlyReentrantAsync(TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            if (TryLockForReadingReentrantAsync(priority, out var lockMode))
            {
                if (lockMode == LockMode.ReadLockReenterable) return reenterableReadLockAttemptTask;
                else return reenteredLockAttemptTask;
            }
            else return TryLockForReadingReentrantAsync(timeout, priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<LockAttempt> TryLockForReadingReentrantAsync(TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            // Reentrance check was already done on first try

            TimeFrame timer = new TimeFrame();
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);

                bool timedOut = false;
                if (timer.Elapsed) timedOut = true;
                else
                {
                    bool nextInQueue = UpdatePriority(ref priority);
                    cycleCount++;
                    if (!nextInQueue || cycleCount > CYCLES_BEFORE_SLEEPING)
                    {
                        cycleCount = 1;
                        bool didReset = mre.Reset();
                        if (lockIndicator != NO_LOCK)
                        {
                            if (!await mre.WaitAsync(timer.Remaining)) timedOut = true;
                        }
                        else if (didReset) mre.Set();
                    }
                    else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();
                }

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    return new LockAttempt(new AcquiredLock());
                }
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(cycleCount != 0);

            if (newLockIndicator == FIRST_READ_LOCK) reentrancyId++;
            reentrancyIndicator.Value = reentrancyId;

            return new LockAttempt(new AcquiredLock(this, LockMode.ReadLockReenterable));
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReentrantAsync(TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            if (TryLockForWritingReentrantAsync(priority, out var lockMode))
            {
                if (lockMode == LockMode.WriteLockReenterable) return reenterableWriteLockAttemptTask;
                else if (lockMode == LockMode.Reentered) return reenteredLockAttemptTask;
                else return upgradedLockAttemptTask;
            }
            else return TryLockForWritingReentrantAsync(timeout, priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<LockAttempt> TryLockForWritingReentrantAsync(TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            // Reentrance was already handled in TryLock, see LockAsync()

            TimeFrame timer = new TimeFrame();
            int cycleCount = 0;
            while (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);

                bool timedOut = false;
                if (timer.Elapsed) timedOut = true;
                else
                {
                    bool nextInQueue = UpdatePriority(ref priority);
                    cycleCount++;
                    if (!nextInQueue || cycleCount > CYCLES_BEFORE_SLEEPING)
                    {
                        cycleCount = 1;
                        bool didReset = mre.Reset();
                        if (lockIndicator != NO_LOCK)
                        {
                            if (!await mre.WaitAsync(timer.Remaining)) timedOut = true;
                        }
                        else if (didReset) mre.Set();
                    }
                    else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();
                }

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    return new LockAttempt(new AcquiredLock());
                }
            }
            UpdateAfterEnter(cycleCount != 0);
            reentrancyIndicator.Value = ++reentrancyId;

            return new LockAttempt(new AcquiredLock(this, LockMode.WriteLockReenterable));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReentrant(out AcquiredLock writeLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            TimeFrame timer = new TimeFrame();
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            if (currentLockIndicator != NO_LOCK)
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);
                var (reentered, timedOut, acquiredLock) = TryReenterForWritingWithTimeout(currentLockIndicator, timer);
                writeLock = acquiredLock;
                if (reentered) return true;
                else if (timedOut) return false;
            }
            while (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                if (timer.IsInvalid) timer = new TimeFrame(timeout);
                bool timedOut = TryLockWaitingLoop(ref priority, timer, ref cycleCount);

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = MIN_PRIORITY;
                    writeLock = new AcquiredLock();
                    return false;
                }
            }
            UpdateAfterEnter(cycleCount != 0);

            reentrancyIndicator.Value = ++reentrancyId;

            writeLock = new AcquiredLock(this, LockMode.WriteLockReenterable);
            return true;
        }



        private bool TryLockWaitingLoop(ref int priority, TimeFrame timer, ref int cycleCount)
        {
            bool timedOut = false;
            if (timer.Elapsed) timedOut = true;
            else
            {
                bool nextInQueue = UpdatePriority(ref priority);
                cycleCount++;
                if (!nextInQueue || cycleCount > CYCLES_BEFORE_SLEEPING)
                {
                    cycleCount = 1;
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining)) timedOut = true;
                    }
                    else if (didReset) mre.Set();
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();
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
                        if (timer.Elapsed)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int LockWaitingLoop(int priority, ref int cycleCount)
        {
            bool nextInQueue = UpdatePriority(ref priority);

            cycleCount++;
            if (!nextInQueue || cycleCount > CYCLES_BEFORE_SLEEPING)
            {
                cycleCount = 1;

                bool didReset = mre.Reset();
                if (lockIndicator != NO_LOCK)
                {
                    mre.Wait();
                }
                else if (didReset) mre.Set();
            }
            else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();
            return priority;
        }

        private int LockReadOnlyWaitingLoop(int priority, ref int cycleCount)
        {
            bool nextInQueue = UpdatePriority(ref priority);
            cycleCount++;
            if (!nextInQueue || cycleCount > CYCLES_BEFORE_SLEEPING)
            {
                cycleCount = 1;
                bool didReset = mre.Reset();
                if (lockIndicator != NO_LOCK)
                {
                    mre.Wait();
                }
                else if (didReset) mre.Set();
            }
            else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();
            return priority;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryLockForReadingAsync(int priority)
        {
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryLockForReadingReentrantAsync(int priority, out LockMode mode)
        {
            var currentLockIndicator = lockIndicator;
            if (currentLockIndicator != NO_LOCK)
            {
                mode = LockMode.Reentered;
                return reentrancyIndicator.Value == reentrancyId;
            }
            var newLockIndicator = currentLockIndicator + 1;
            if (ReentrantReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                mode = LockMode.ReadLockReenterable;
                return false;
            }
            else
            {
                if (newLockIndicator == FIRST_READ_LOCK) reentrancyId++;
                reentrancyIndicator.Value = reentrancyId;
                mode = LockMode.ReadLockReenterable;
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

                if (!nextInQueue || ++cycleCount > CYCLES_BEFORE_SLEEPING || IsThreadPoolCloseToStarving(0))
                {
                    cycleCount = 1;
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync().ConfigureAwait(false);
                    }
                    if (didReset) mre.Set();
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

                waited = true;
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(waited);

            return new AcquiredLock(this, LockMode.ReadLock);
        }

        private async Task<AcquiredLock> LockForReadingReentrantAsync(int priority = DEFAULT_PRIORITY)
        {
            // Reentrance was already handled in TryLockReadOnly, see LockReadOnlyAsync()

            bool waited = false;
            int cycleCount = 0;
            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReentrantReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                bool nextInQueue = UpdatePriority(ref priority);

                if (!nextInQueue || ++cycleCount > CYCLES_BEFORE_SLEEPING || IsThreadPoolCloseToStarving(0))
                {
                    cycleCount = 1;
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync().ConfigureAwait(false);
                    }
                    if (didReset) mre.Set();
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

                waited = true;
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(waited);

            if (newLockIndicator == FIRST_READ_LOCK) reentrancyId++;
            reentrancyIndicator.Value = reentrancyId;

            return new AcquiredLock(this, LockMode.ReadLockReenterable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool UpdatePriority(ref int priority)
        {
            if (priority < MAX_PRIORITY) priority++;

            bool nextInQueue = false;
            if (priority >= highestPriority)
            {
                highestPriority = priority;
                nextInQueue = true;
            }
            else if (priority > secondHighestPriority)
            {
                secondHighestPriority = priority;
            }

            return nextInQueue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateAfterEnter(bool waited)
        {
            if (waited)
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
        private bool TryGetWritingLock(int priority)
        {
            if (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryLockForWritingReentrantAsync(int priority, out LockMode mode)
        {
            var currentLockIndicator = lockIndicator;
            if (currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForWriting(currentLockIndicator);
                if (reentered)
                {
                    mode = acquiredLock.mode;
                    return true;
                }
            }
            if (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                mode = LockMode.WriteLockReenterable;
                return false;
            }
            else
            {
                reentrancyIndicator.Value = ++reentrancyId;
                mode = LockMode.WriteLockReenterable;
                return true;
            }
        }

        private async Task<AcquiredLock> LockForWritingAsync(int priority = DEFAULT_PRIORITY)
        {
            bool waited = false;

            // Reentrance was already handled in TryLock, see LockAsync()
            int cycleCount = 0;
            while (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool nextInQueue = UpdatePriority(ref priority);

                if (!nextInQueue || ++cycleCount > CYCLES_BEFORE_SLEEPING || IsThreadPoolCloseToStarving(0))
                {
                    cycleCount = 1;
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync().ConfigureAwait(false);
                    }
                    else if (didReset) mre.Set();
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

                waited = true;
            }
            UpdateAfterEnter(waited);

            return new AcquiredLock(this, LockMode.WriteLock);
        }

        private async Task<AcquiredLock> LockForWritingReentrantAsync(int priority = DEFAULT_PRIORITY)
        {
            bool waited = false;

            // Reentrance was already handled in TryLock, see LockAsync()
            int cycleCount = 0;
            while (WriterMustWait(priority) || NO_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool nextInQueue = UpdatePriority(ref priority);

                if (!nextInQueue || ++cycleCount > CYCLES_BEFORE_SLEEPING || IsThreadPoolCloseToStarving(0))
                {
                    cycleCount = 1;
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync().ConfigureAwait(false);
                    }
                    else if (didReset) mre.Set();
                }
                else if (cycleCount > CYCLES_BEFORE_YIELDING) Thread.Yield();

                waited = true;
            }
            UpdateAfterEnter(waited);
            reentrancyIndicator.Value = ++reentrancyId;

            return new AcquiredLock(this, LockMode.WriteLockReenterable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsThreadPoolCloseToStarving(int minAvailableThreads)
        {
            ThreadPool.GetAvailableThreads(out int availableThreads, out _);
            return availableThreads == minAvailableThreads;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator, int priority)
        {
            return currentLockIndicator == WRITE_LOCK || priority < highestPriority || waitingForUpgrade == TRUE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReentrantReaderMustWait(int currentLockIndicator, int priority)
        {
            return currentLockIndicator == WRITE_LOCK || priority < highestPriority;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int priority)
        {
            return lockIndicator != NO_LOCK || priority < highestPriority;
        }


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

                int numSleeping = numPrioSleeping + prioAsyncWriterQueue.Count + numSyncWritersSleeping + asyncWriterQueue.Count + numReadersSleeping;
                if (numSleeping > 0)
                {

                    long numWaiting = ticketCounter - nextTicket;
                    long numIdleing = numWaiting - numSleeping;
                    //Console.WriteLine($"ticketCounte={ticketCounter}, nextTicket={nextTicket}, numWaiting={numWaiting}, numSleeping={numSleeping}, numIdleing={numIdleing}");
                    //if (numIdleing >= NUM_PARALLEL_IDLE) Console.Write("*");

                    while (numSleeping > 0 && numIdleing < NUM_PARALLEL_IDLE)
                    {
                        WakeUpOne();
                        numIdleing++;
                        numSleeping--;
                    };

                    anySleeping = numPrioSleeping + prioAsyncWriterQueue.Count + numSyncWritersSleeping + asyncWriterQueue.Count + numReadersSleeping > 0;
                }
                else anySleeping = false;

                wakingUp = false;
            }
        }

        private void WakeUpOne()
        {
            if (numPrioSleeping > 0)
            {
                Monitor.Enter(priorityMonitor);
                numPrioSleeping--;
                Monitor.Pulse(priorityMonitor);
                Monitor.Exit(priorityMonitor);
            }
            else if (prioAsyncWriterQueue.Count > 0)
            {
                var tcs = prioAsyncWriterQueue.Dequeue();
                tcs.SetResult(true);
            }
            else if (numSyncWritersSleeping > 0)
            {
                Monitor.Enter(writerMonitor);
                numSyncWritersSleeping--;
                Monitor.Pulse(writerMonitor);
                Monitor.Exit(writerMonitor);
            }
            else if (asyncWriterQueue.Count > 0)
            {
                var tcs = asyncWriterQueue.Dequeue();
                tcs.SetResult(true);
            }
            else if (numReadersSleeping > 0)
            {
                Monitor.Enter(readerMonitor);
                numReadersSleeping = 0;
                Monitor.PulseAll(readerMonitor);
                Monitor.Exit(readerMonitor);
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void ExitReentrantReadLock()
        {
            var newLockIndicator = Interlocked.Decrement(ref lockIndicator);
            if (NO_LOCK == newLockIndicator)
            {
                reentrancyId++;
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
            reentrancyId++;
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

    }
}
