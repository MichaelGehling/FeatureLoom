using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    /// <summary>
    /// A multi-purpose high-performance lock object that can be used in synchronous and asynchronous contexts.
    /// It supports reentrancy, prioritized lock acquiring, trying for lock acquiring with timeout and 
    /// read-only locking for parallel access (incl. automatic upgrading/downgrading in conjunction with reentrancy).
    /// When the lock is acquired, it returns a handle that can be used with a using statement for simple and clean usage.
    /// Example: using(myLock.Lock()) { ... }
    /// In most scenarios the FeatureLock is faster than the build-in locks (e.g. Monitor/ReaderWriterLock for synchronous contexts 
    /// and SemaphoreSlim for asynchronous contexts). Though reentrant locking in synchronous contexts using FeatureLock 
    /// is slower than with Monitor/ReaderWriterLock, it also allows reentrancy for asynchronous contexts and even mixed contexts.
    /// </summary>
    public sealed class FeatureLock
    {
        #region Constants
        // Are set to lockIndicator if the lock is not acquired, acquired for writing, cquired for reading (further readers increase this value, each by one)
        const int NO_LOCK = 0;
        const int WRITE_LOCK = NO_LOCK - 1;
        const int FIRST_READ_LOCK = NO_LOCK + 1;

        // Booleans cannot be used in CompareExchange, so ints are used.
        const int FALSE = 0;
        const int TRUE = 1;

        // Some values that are used to be compared with the sleepPressure variable
        const int LONG_YIELD_CYCLE_COUNT = 50;
        const int CYCLES_BEFORE_FIRST_SLEEP = 10_000;
        const int CYCLES_BETWEEN_SLEEPS = 2000;
        #endregion Constants

        #region Variables               
        
        // This static variable will only be checked once, so if CPU affinity changes after the first FeatureLock was created, it will not change behaviour, anymore
        private static readonly int numCores = CalculateAvailableCores();

        // Used to synchronize sleeping and waking up code sections.
        // If we have only one core, the FastSpinLock must not do any spinning before yielding to avoid blocking the only thread.
        FastSpinLock sleepLock = new FastSpinLock(numCores > 1 ? 50 : 0);

        // If any candidate is sleeping, its wake order is added to the queue.
        // the queue head references the candidates wake order with the oldest ticket.
        // (only write under sleepLock!)
        volatile WakeOrder queueHead = null;

        // Counting sleepers (only write under sleepLock!)
        volatile int numSleeping = 0;
        // Counting readonly sleepers (only write under sleepLock!)
        volatile int numSleepingReadOnly = 0;

        // the main lock variable, indicating current locking state:
        // 0 means no lock
        // -1 means write lock
        // >=1 means read lock (number implies the count of parallel readers)
        volatile int lockIndicator = NO_LOCK;

        // If true, indicates that a reentrant write lock tries to upgrade an existing reentrant read lock,
        // but more than one reader is active, the upgrade must wait until only one reader is left
        volatile int waitingForUpgrade = FALSE;

        // Keeps the last reentrancyId of the "logical thread".
        // A value that differs from the currently valid reentrancyId implies that the lock was not acquired before in this "logical thread",
        // so it must be acquired and cannot simply be reentered.
        AsyncLocal<int> reentrancyIndicator = new AsyncLocal<int>();

        // The currently valid reentrancyId. It must never be 0, as this is the default value of the reentrancyIndicator.
        volatile int reentrancyId = 1;

        // Indicates that (probably) one or more other waiting candidates are sleeping and need to be waked up.
        // It may happen that anySleeping is true, though actually no one needs to be awaked, 
        // but it will never happen that it is false if someone needs to be awaked.
        volatile bool anySleeping = false;

        // Will be true if a lock is to be acquied with priority, so the other candidates know that they have to stay back.
        // If a prioritized candidate acquired the lock it will reset this variable, so if another prioritized candidate waits,
        // it must set it back to true. So there can ba a short time when another non-priority candidate might acquire the lock nevertheless.
        volatile bool prioritizedWaiting = false;

        // The next ticket number that will be provided. A ticket number must never be 0, so if nextTicket turns to be 0, it must be incremented, again.
        volatile int nextTicket = 1;        

        // Contains the oldest ticket number that is currently waiting. That candidate will never sleep and also ensures to wake up the others.
        // Will be reset to 0 when the currently oldest candidate acquired the lock. For a short time, it is possible that some newer ticket number
        // is set until the oldest one reaches the pooint to update.
        volatile int stayAwakeTicket = 0;        

        // Will be counted up by the waiting candidates that are not sleeping yet.
        // Is used to set the candidates to sleep, one after another, starting with the youngest ticket.
        volatile int sleepPressure = 0;
        
        #endregion Variables

        #region PreparedTasks
        // These variables hold already completed task objects, prepared in advance in the constructor for later reuse,
        // in order to reduce garbage and improve performance by handling async calls synchronously, if no asynchronous sleeping/yielding is required
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
        #endregion PreparedTasks

        #region PublicProperties        
        /// <summary>
        /// True when the lock is currently taken
        /// </summary>
        public bool IsLocked => lockIndicator != NO_LOCK;
        /// <summary>
        /// True if the lock is currently exclusively taken for writing
        /// </summary>
        public bool IsWriteLocked => lockIndicator == WRITE_LOCK;
        /// <summary>
        /// True if the lock is currently taken for shared reading
        /// </summary>
        public bool IsReadOnlyLocked => lockIndicator >= FIRST_READ_LOCK;
        /// <summary>
        /// In case of read-lock, it indicates the number of parallel readers
        /// </summary>
        public int CountParallelReadLocks => IsReadOnlyLocked ? lockIndicator : 0;
        /// <summary>
        /// True if a reentrant write lock attempt is waiting for upgrading a former reentrant read lock, but other readlocks are in place
        /// </summary>
        public bool IsWriteLockWaitingForUpgrade => waitingForUpgrade == TRUE;
        /// <summary>
        /// Lock was already taken reentrantly in the same context
        /// </summary>
        public bool HasValidReentrancyContext => reentrancyId == reentrancyIndicator.Value;
        /// <summary>
        /// The number of candidates in the sleeping queue
        /// </summary>
        public int NumSleeping => numSleeping;        
        /// <summary>
        /// The number of readOnly candidates in the sleeping queue
        /// </summary>
        public int NumSleepingReadOnly => numSleepingReadOnly;
        #endregion PublicProperties

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
            await Task.Run(syncCall).ConfigureAwait(false);
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

        #region HelperMethods
        
        // calculates the number of logical cores that are assigned to the process
        private static int CalculateAvailableCores()
        {
            int numCores = 0;
            var affinity = (long)Process.GetCurrentProcess().ProcessorAffinity;
            while (affinity > 0)
            {
                if ((affinity & (long)1) > 0) numCores++;
                affinity = affinity >> 1;
            }

            if (numCores == 0) numCores = Environment.ProcessorCount;
            return numCores;
        }

        // Used to check if a candidate is allowed trying to acquire the lock without going into the waiting cycle
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustWait(bool prioritized)
        {
            return !prioritized && (prioritizedWaiting || stayAwakeTicket != 0);
        }

        // Invalidates the current reentrancyIndicator by changing the reentrancy ID to compare 
        // without changing the AsyncLocal reentrancyIndicator itself, which would be expensive.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenewReentrancyId()
        {
            if (++reentrancyId == 0) ++reentrancyId;
        }

        // Yielding a task is very expensive, but must be done to avoid block the thread pool threads.
        // This method checks if the task should be yielded or the thread can still be blocked.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustYieldAsyncThread(int ticket, bool prioritized)
        {
            // If a special synchronization context is set (e.g. UI-Thread) the task must always be yielded to avoid blocking
            if (SynchronizationContext.Current != null) return true;
                        
            ThreadPool.GetAvailableThreads(out int availableThreads, out _);
            ThreadPool.GetMaxThreads(out int maxThreads, out _);
            ThreadPool.GetMinThreads(out int minThreads, out _);
            var usedThreads = maxThreads - availableThreads;
            // If less thread pool threads are used than minimum available, we must never yield the task
            if (usedThreads >= minThreads)
            {                
                var ticketAge = GetTicketAge(ticket);
                // Only yield if we waited long enough for the specific ticket age
                if (sleepPressure < ticketAge * 1000) return false;

                // Otherwise we yield every now and then, less often for older tickets that should be preferred and less often the more threads we have in the thread pool
                int fullYieldCycle = (usedThreads / ticketAge.ClampLow(1)).ClampLow(1);
                if (prioritizedWaiting)
                {
                    // prioritized waiters should yield the task very rarely, because it takes so much time
                    if (prioritized) fullYieldCycle *= 20;
                }
                else
                {
                    // if no priorized waiter is there, the oldest ticket should yield a task less
                    if (ticket == stayAwakeTicket) fullYieldCycle *= 2;
                }
                return (sleepPressure % fullYieldCycle) == 0;

            }
            else return false;            
        }

        // Checks on every waiting cylce if the candidate must go to sleep
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustTrySleep(bool prioritized, bool readOnly, int ticket)
        {
            // In some cases the candidate will not go to sleep at all...
            if (ticket == stayAwakeTicket || prioritized || sleepLock.IsLocked || !MustStillWait(prioritized, readOnly)) return false; 

            // Otherwise it depends on the age of the ticket and the cycles that all candidates have done together since the last time the lock was acquired
            var ticketAge = GetTicketAge(ticket);
            return sleepPressure > CYCLES_BEFORE_FIRST_SLEEP + (ticketAge * CYCLES_BETWEEN_SLEEPS);
        }

        // Checks if a candidate must still remain in the waiting queue or if acquiring would be possible
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustStillWait(bool prioritized, bool readOnly)
        {
            return (!readOnly && lockIndicator != NO_LOCK) || (readOnly && lockIndicator == WRITE_LOCK) || (!prioritized && prioritizedWaiting);
        }

        // Checks if it currently allowed to go to sleep.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MaySleep()
        {
            // If we don't have anyone who stays awake we could deadlock, so do not sleep in that case
            return stayAwakeTicket != 0;
        }

        // Calculates the age of the current ticket, where age means how many other tickets were provided since the own one
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetTicketAge(int ticket)
        {
            if (ticket < nextTicket)
            {
                return nextTicket - ticket;
            }
            else
            {
                // covers the wrap around case
                return (int)(((long)nextTicket + int.MaxValue) - ticket);
            }
        }

        // Increases the overall sleep pressure variable
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateSleepPressure(bool longCycle)
        {
            int addition = longCycle ? LONG_YIELD_CYCLE_COUNT + 1 : 1;
            sleepPressure += addition;            
        }

        // Wakes up the sleeper at the top of the sleeping queue which is the oldest one (or none if not available)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WakeUp()
        {
            WakeOrder wakeOrder = null;
            using (sleepLock.Lock())
            {
                wakeOrder = DequeueWakeOrder();
                anySleeping = queueHead != null;
            }
            wakeOrder?.WakeUp(this);
        }

        // Wakes up the first readOnly sleeper in the queue and removes it from the queue (or none if not available)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WakeUpNextReadOnly()
        {
            WakeOrder wakeOrder = null;
            using (sleepLock.Lock())
            {
                wakeOrder = PickNextReadOnlyWakeOrder();
                anySleeping = queueHead != null;
            }
            wakeOrder?.WakeUp(this);
        }

        // Creates the a new ticket for a new waiting candidate.
        // It will ensure to never provide the 0, because it is reserved to indicate when no one has the stayAwakeTicket.        
        // GetTicket() might be called concurrently and we don't use interlocked, so it might happen that a ticket is provided multiple times,
        // but that is acceptable and will not cause any trouble.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetTicket()
        {            
            var ticket = nextTicket++;
            if (ticket == 0) ticket = nextTicket++;
            return ticket;
        }

        // If the candidates ticket is older than the current stayAwakeTicket, it will take its place
        // UpdateStayAwakeTicket() might be called concurrently and it might happen that the younger ticket wins,
        // but that is acceptable and will not cause any trouble, because in the next cycle it will try again.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStayAwakeTicket(int ticket)
        {            
            var otherTicket = stayAwakeTicket;            
            if (otherTicket == 0 || IsTicketOlder(ticket, otherTicket)) stayAwakeTicket = ticket;
        }

        // Checks if the one ticket is older than the other one
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsTicketOlder(int ticket, int otherTicket)
        {            
            if (ticket < otherTicket)
            {
                // handle the wrap-around case
                if ((otherTicket - ticket) < (int.MaxValue / 2)) return true;
            }
            else if (ticket > otherTicket)
            {
                // handle the wrap-around case
                if ((ticket - otherTicket) > (int.MaxValue / 2)) return true;
            }

            return false;
        }

        // Actually tries to acquire the lock.                
        // Uses Interlocked.CompareExchange to ensure that only one candidate can change the lockIndicator at a time.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAcquire(bool readOnly)
        {
            if (readOnly)
            {
                // It is allowed for multiple candidates to acquire the lock for readOnly in parallel.
                // The number of candidates that acquired under readOnly restriction is counted in the lockIndicator, starting with FIRST_READ_LOCK,
                // so we take the current lockIndicator and increase it by try and check if it would be a proper readOnly-value (newLockIndicator >= FIRST_READ_LOCK)
                // If it is the case we try to actually set the new value to the lockIndicator variable
                int currentLockIndicator = lockIndicator;
                int newLockIndicator = currentLockIndicator + 1;
                return newLockIndicator >= FIRST_READ_LOCK && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator);
            }
            else
            {
                // Only allow setting the write lock if the lockIndicator was set to NO_LOCK before
                return NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK);
            }
        }

        // May wake up sleeping candidates if approriate. Is called cyclically from the waiting loop.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AwakeSleeper(int ticket)
        {
            // Cancel if no one is sleeping or if the sleepLock is already locked (we may try later in the next cycle)
            // We don't want to slow down the designated waiter, let the others do the job
            if (!anySleeping || ticket == stayAwakeTicket || sleepLock.IsLocked) return;

            // Don't wake up anyboy if the lock could be taken
            if (lockIndicator != NO_LOCK)
            {
                // Only wake up a candidate with an older ticket
                var sleeper = queueHead;
                if (sleeper != null && IsTicketOlder(sleeper.ticket, ticket)) WakeUp();
            }

            // If recently the lock was taken for read only, wake up all the readOnly sleepers
            // If lockIndicator indicates that there already more than one candidates have taken the lock,
            // we are too late. Otherwise it could happen that non-readOnly candidates must wait too long.
            while (lockIndicator == FIRST_READ_LOCK && numSleepingReadOnly > 0 && !sleepLock.IsLocked)
            {
                WakeUpNextReadOnly();
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FinishWaiting(bool prioritized, int ticket, bool acquired)
        {
            if (acquired) sleepPressure = 0;
            if (prioritized) prioritizedWaiting = false;
            if (ticket == stayAwakeTicket || stayAwakeTicket == 0)
            {
                stayAwakeTicket = 0;
                if (anySleeping) WakeUp();
            }
        }

        #endregion HelperMethods

        #region Lock
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock(bool prioritized = false)
        {
            if (MustWait(prioritized) || !TryAcquire(false)) Lock_Wait(prioritized, false);
            return new AcquiredLock(this, LockMode.WriteLock);
        }

        private void Lock_Wait(bool prioritized, bool readOnly)
        {
            int ticket = GetTicket();
            do
            {
                if (prioritized) prioritizedWaiting = true;
                UpdateStayAwakeTicket(ticket);

                bool longCycle = false;
                if (MustStillWait(prioritized, readOnly))
                {
                    if (ticket == stayAwakeTicket) longCycle = Thread.Yield();
                    else
                    {
                        Thread.Sleep(0);
                        longCycle = true;
                    }
                }
                UpdateSleepPressure(longCycle);
                if (longCycle) UpdateStayAwakeTicket(ticket);

                if (MustTrySleep(prioritized, readOnly, ticket))
                {
                    WakeOrder.TrySleep(this, ticket, readOnly);
                    UpdateStayAwakeTicket(ticket);
                }

                AwakeSleeper(ticket);
            }
            while (MustStillWait(prioritized, readOnly) || !TryAcquire(readOnly));

            FinishWaiting(prioritized, ticket, true);
        }
        #endregion Lock

        #region LockAsync
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockAsync(bool prioritized = false)
        {
            if (!MustWait(prioritized) && TryAcquire(false)) return writeLockTask;
            else return LockAsync_Wait(LockMode.WriteLock, prioritized, false);
        }

        private Task<AcquiredLock> LockAsync_Wait(LockMode mode, bool prioritized, bool readOnly)
        {
            int ticket = GetTicket();
            do
            {
                if (prioritized) prioritizedWaiting = true;
                UpdateStayAwakeTicket(ticket);

                bool longCycle = false;
                if (MustStillWait(prioritized, readOnly))
                {
                    if (MustYieldAsyncThread(ticket, prioritized))
                    {
                        return LockAsync_Wait_ContinueWithAwaiting(mode, ticket, prioritized, true, readOnly);
                    }
                    else
                    {
                        if (ticket == stayAwakeTicket) longCycle = Thread.Yield();
                        else
                        {
                            Thread.Sleep(0);
                            longCycle = true;
                        }
                    }
                }
                UpdateSleepPressure(longCycle);
                if (longCycle) UpdateStayAwakeTicket(ticket);

                if (MustTrySleep(prioritized, readOnly, ticket))
                {
                    return LockAsync_Wait_ContinueWithAwaiting(mode, ticket, prioritized, false, readOnly);
                }

                AwakeSleeper(ticket);
            }
            while (MustStillWait(prioritized, readOnly) || !TryAcquire(readOnly));

            FinishWaiting(prioritized, ticket, true);

            if (readOnly)
            {
                if (mode == LockMode.ReadLockReenterable)
                {
                    reentrancyIndicator.Value = reentrancyId;
                    return reenterableReadLockTask;
                }
                else return readLockTask;
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable)
                {
                    reentrancyIndicator.Value = reentrancyId;
                    return reenterableWriteLockTask;
                }
                else return writeLockTask;
            }
        }

        private async Task<AcquiredLock> LockAsync_Wait_ContinueWithAwaiting(LockMode mode, int ticket, bool prioritized, bool yieldAsyncNow, bool readOnly)
        {
            if (yieldAsyncNow) await Task.Yield();

            do
            {
                if (prioritized) prioritizedWaiting = true;
                UpdateStayAwakeTicket(ticket);

                bool longCycle = false;
                if (MustStillWait(prioritized, readOnly))
                {
                    if (MustYieldAsyncThread(ticket, prioritized))
                    {
                        await Task.Yield();
                        longCycle = true;
                    }
                    else
                    {
                        if (ticket == stayAwakeTicket) longCycle = Thread.Yield();
                        else
                        {
                            Thread.Sleep(0);
                            longCycle = true;
                        }
                    }
                }
                UpdateSleepPressure(longCycle);
                if (longCycle) UpdateStayAwakeTicket(ticket);

                if (MustTrySleep(prioritized, readOnly, ticket))
                {
                    await WakeOrder.TrySleepAsync(this, ticket, readOnly);
                    UpdateStayAwakeTicket(ticket);
                }

                AwakeSleeper(ticket);
            }
            while (MustStillWait(prioritized, readOnly) || !TryAcquire(readOnly));

            FinishWaiting(prioritized, ticket, true);

            if (readOnly)
            {
                if (mode == LockMode.ReadLockReenterable) reentrancyIndicator.Value = reentrancyId;
                return new AcquiredLock(this, mode);
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable) reentrancyIndicator.Value = reentrancyId;
                return new AcquiredLock(this, mode);
            }
        }
        #endregion LockAsync

        #region LockReadOnly
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnly(bool prioritized = false)
        {
            if (MustWait(prioritized) || !TryAcquire(true)) Lock_Wait(prioritized, true);
            return new AcquiredLock(this, LockMode.ReadLock);
        }
        #endregion LockReadOnly

        #region LockReadOnlyAsync
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReadOnlyAsync(bool prioritized = false)
        {
            if (!MustWait(prioritized) && TryAcquire(true)) return readLockTask;
            else return LockAsync_Wait(LockMode.ReadLock, prioritized, true);
        }
        #endregion LockReadOnlyAsync

        #region LockReentrant
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReentrant(bool prioritized = false)
        {
            if (TryReenter(out AcquiredLock acquiredLock, true, out _)) return acquiredLock;
            if (MustWait(prioritized) || !TryAcquire(false)) Lock_Wait(prioritized, false);
            reentrancyIndicator.Value = reentrancyId;
            return new AcquiredLock(this, LockMode.WriteLockReenterable);
        }

        private bool TryReenter(out AcquiredLock acquiredLock, bool waitForUpgrade, out bool upgradePossible)
        {
            var currentLockIndicator = lockIndicator;
            if (currentLockIndicator != NO_LOCK && HasValidReentrancyContext)
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
                        if (waitForUpgrade)
                        {
                            prioritizedWaiting = true;
                            Thread.Sleep(0);
                        }
                        else
                        {
                            waitingForUpgrade = FALSE;
                            upgradePossible = true;
                            acquiredLock = new AcquiredLock();
                            return false;
                        }
                    }
                    waitingForUpgrade = FALSE;
                    prioritizedWaiting = false;

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
        public Task<AcquiredLock> LockReentrantAsync(bool prioritized = false)
        {
            if (TryReenter(out AcquiredLock acquiredLock, false, out bool upgradePossible))
            {
                if (acquiredLock.mode == LockMode.Reentered) return reenteredLockTask;
                else return upgradedLockTask;
            }
            else if (upgradePossible) return WaitForUpgradeAsync();

            if (!MustWait(prioritized) && TryAcquire(false))
            {
                reentrancyIndicator.Value = reentrancyId;
                return reenterableWriteLockTask;
            }
            else return LockAsync_Wait(LockMode.WriteLockReenterable, prioritized, false);
        }

        private async Task<AcquiredLock> WaitForUpgradeAsync()
        {
            int counter = 0;
            SynchronizationContext.SetSynchronizationContext(null);
            // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
            if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
            // Waiting for upgrade to writeLock
            while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
            {
                prioritizedWaiting = true;
                if (MustYieldAsyncThread_ForReentranceUpgrade(++counter)) await Task.Yield();
                else Thread.Sleep(0);
            }
            waitingForUpgrade = FALSE;
            prioritizedWaiting = false;
            return new AcquiredLock(this, LockMode.Upgraded);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustYieldAsyncThread_ForReentranceUpgrade(int counter)
        {
            if (SynchronizationContext.Current == null)
            {
                ThreadPool.GetAvailableThreads(out int availableThreads, out _);
                ThreadPool.GetMaxThreads(out int maxThreads, out _);
                ThreadPool.GetMinThreads(out int minThreads, out _);
                var usedThreads = maxThreads - availableThreads;
                if (usedThreads >= minThreads)
                {
                    return (counter % 100) == 0;

                }
                else return false;
            }
            else return true;
        }
        #endregion LockReentrantAsync

        #region LockReentrantReadOnly
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReentrantReadOnly(bool prioritized = false)
        {
            if (TryReenterReadOnly()) return new AcquiredLock(this, LockMode.Reentered);
            
            if (MustWait(prioritized) || !TryAcquire(true)) Lock_Wait(prioritized, true);

            reentrancyIndicator.Value = reentrancyId;
            return new AcquiredLock(this, LockMode.ReadLockReenterable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReenterReadOnly()
        {
            return lockIndicator != NO_LOCK && HasValidReentrancyContext;
        }

        #endregion LockReentrantReadOnly

        #region LockReentrantReadOnlyAsync
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReentrantReadOnlyAsync(bool prioritized = false)
        {
            if (TryReenterReadOnly()) return reenteredLockTask;

            if (!MustWait(prioritized) && TryAcquire(true))
            {
                reentrancyIndicator.Value = reentrancyId;
                return reenterableReadLockTask;
            }
            else return LockAsync_Wait(LockMode.ReadLockReenterable, prioritized, true);
        }
        #endregion LockReentrantReadOnlyAsync

        #region TryLock
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock acquiredLock, bool prioritized = false)
        {
            if (!MustWait(prioritized) && TryAcquire(false))
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
        public bool TryLockReadOnly(out AcquiredLock acquiredLock, bool prioritized = false)
        {
            if (!MustWait(prioritized) && TryAcquire(true))
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
        public bool TryLockReentrant(out AcquiredLock acquiredLock, bool prioritized = false)
        {
            if (TryReenter(out acquiredLock, false, out _)) return true;

            if (!MustWait(prioritized) && TryAcquire(false))
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new AcquiredLock(this, LockMode.WriteLockReenterable);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReentrantReadOnly(out AcquiredLock acquiredLock, bool prioritized = false)
        {
            if (TryReenterReadOnly())
            {
                acquiredLock = new AcquiredLock(this, LockMode.Reentered);
                return true;
            }

            if (!MustWait(prioritized) && TryAcquire(true))
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new AcquiredLock(this, LockMode.ReadLockReenterable);
                return true;
            }
            acquiredLock = new AcquiredLock();
            return false;
        }
        #endregion TryLockReentrantReadOnly

        #region TryLock_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(TimeSpan timeout, out AcquiredLock acquiredLock, bool prioritized = false)
        {
            if (!MustWait(prioritized) && TryAcquire(false))
            {
                acquiredLock = new AcquiredLock(this, LockMode.WriteLock);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, false))
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

        private bool TryLock_Wait(TimeSpan timeout, bool prioritized, bool readOnly)
        {
            TimeFrame timer = new TimeFrame(timeout);

            int ticket = GetTicket();
            do
            {
                if (prioritized) prioritizedWaiting = true;
                UpdateStayAwakeTicket(ticket);

                if (timer.Elapsed)
                {
                    FinishWaiting(prioritized, ticket, false);
                    return false;
                }

                bool longCycle = false;
                if (MustStillWait(prioritized, readOnly))
                {
                    if (ticket == stayAwakeTicket) longCycle = Thread.Yield();
                    else
                    {
                        Thread.Sleep(0);
                        longCycle = true;
                    }
                }
                UpdateSleepPressure(longCycle);
                if (longCycle) UpdateStayAwakeTicket(ticket);

                if (MustTrySleep(prioritized, readOnly, ticket))
                {
                    if (WakeOrder.TrySleepUntilTimeout(this, timer, ticket, readOnly))
                    {
                        FinishWaiting(prioritized, ticket, false);
                        return false;
                    }
                    UpdateStayAwakeTicket(ticket);
                }

                AwakeSleeper(ticket);
            }
            while (MustStillWait(prioritized, readOnly) || !TryAcquire(readOnly));

            FinishWaiting(prioritized, ticket, true);

            return true;
        }  
        #endregion TryLock_Timeout

        #region TryLockAsync_Timeout

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockAsync(TimeSpan timeout, bool prioritized = false)
        {
            if (!MustWait(prioritized) && TryAcquire(false)) return writeLockAttemptTask;
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.WriteLock, timeout, prioritized, false);
            else return failedAttemptTask;
        }

        private Task<LockAttempt> TryLockAsync_Wait(LockMode mode, TimeSpan timeout, bool prioritized, bool readOnly)
        {
            TimeFrame timer = new TimeFrame(timeout);

            int ticket = GetTicket();
            do
            {
                if (prioritized) prioritizedWaiting = true;
                UpdateStayAwakeTicket(ticket);

                if (timer.Elapsed)
                {
                    FinishWaiting(prioritized, ticket, false);
                    return failedAttemptTask;
                }

                bool longCycle = false;
                if (MustStillWait(prioritized, readOnly))
                {
                    if (MustYieldAsyncThread(ticket, prioritized))
                    {
                        return TryLockAsync_Wait_ContinueWithAwaiting(mode, timer, ticket, prioritized, true, readOnly);
                    }
                    else
                    {
                        if (ticket == stayAwakeTicket) longCycle = Thread.Yield();
                        else
                        {
                            Thread.Sleep(0);
                            longCycle = true;
                        }
                    }
                }
                UpdateSleepPressure(longCycle);
                if (longCycle) UpdateStayAwakeTicket(ticket);

                if (MustTrySleep(prioritized, readOnly, ticket))
                {
                    return TryLockAsync_Wait_ContinueWithAwaiting(mode, timer, ticket, prioritized, false, readOnly);
                }

                AwakeSleeper(ticket);
            }
            while (MustStillWait(prioritized, readOnly) || !TryAcquire(readOnly));

            FinishWaiting(prioritized, ticket, true);

            if (readOnly)
            {
                if (mode == LockMode.ReadLockReenterable)
                {
                    reentrancyIndicator.Value = reentrancyId;
                    return reenterableReadLockAttemptTask;                    
                }
                else return readLockAttemptTask;
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable)
                {
                    reentrancyIndicator.Value = reentrancyId;
                    return reenterableWriteLockAttemptTask;                    
                }
                else return writeLockAttemptTask;
            }
        }

        private async Task<LockAttempt> TryLockAsync_Wait_ContinueWithAwaiting(LockMode mode, TimeFrame timer, int ticket, bool prioritized, bool yieldAsyncNow, bool readOnly)
        {
            if (yieldAsyncNow) await Task.Yield();

            do
            {
                if (prioritized) prioritizedWaiting = true;
                UpdateStayAwakeTicket(ticket);

                if (timer.Elapsed)
                {
                    FinishWaiting(prioritized, ticket, false);
                    return new LockAttempt(new AcquiredLock());
                }

                bool longCycle = false;
                if (MustStillWait(prioritized, readOnly))
                {
                    if (MustYieldAsyncThread(ticket, prioritized))
                    {
                        await Task.Yield();
                        longCycle = true;
                    }
                    else
                    {
                        if (ticket == stayAwakeTicket) longCycle = Thread.Yield();
                        else
                        {
                            Thread.Sleep(0);
                            longCycle = true;
                        }
                    }
                }
                UpdateSleepPressure(longCycle);
                if (longCycle) UpdateStayAwakeTicket(ticket);

                if (MustTrySleep(prioritized, readOnly, ticket))
                {
                    if (await WakeOrder.TrySleepUntilTimeoutAsync(this, timer, ticket, readOnly))
                    {
                        FinishWaiting(prioritized, ticket, false);
                        return new LockAttempt(new AcquiredLock());
                    }
                    UpdateStayAwakeTicket(ticket);
                }

                AwakeSleeper(ticket);
            }
            while (MustStillWait(prioritized, readOnly) || !TryAcquire(readOnly));

            FinishWaiting(prioritized, ticket, true);

            if (readOnly)
            {
                if (mode == LockMode.ReadLockReenterable) reentrancyIndicator.Value = reentrancyId;
                return new LockAttempt(new AcquiredLock(this, mode));
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable) reentrancyIndicator.Value = reentrancyId;
                return new LockAttempt(new AcquiredLock(this, mode));
            }
        }
        #endregion TryLockAsync_Timeout

        #region TryLockReadOnly_Timeout
        public bool TryLockReadOnly(TimeSpan timeout, out AcquiredLock acquiredLock, bool prioritized = false)
        {
            if (!MustWait(prioritized) && TryAcquire(true))
            {
                acquiredLock = new AcquiredLock(this, LockMode.ReadLock);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, true))
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
        
        #endregion TryLockReadOnly_Timeout

        #region TryLockReadOnlyAsync_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReadOnlyAsync(TimeSpan timeout, bool prioritized = false)
        {
            if (!MustWait(prioritized) && TryAcquire(true)) return readLockAttemptTask;
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.ReadLock, timeout, prioritized, true);
            else return failedAttemptTask;
        }
        #endregion TryLockReadOnlyAsync_Timeout

        #region TryLockReentrant_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReentrant(TimeSpan timeout, out AcquiredLock acquiredLock, bool prioritized = false)
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

            if (!MustWait(prioritized) && TryAcquire(false))
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new AcquiredLock(this, LockMode.WriteLockReenterable);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, false))
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new AcquiredLock(this, LockMode.WriteLockReenterable);
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

            // Waiting for upgrade to writeLock
            while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
            {
                if (timer.Elapsed)
                {
                    waitingForUpgrade = FALSE;
                    return false;
                }
                Thread.Sleep(0);
            }
            waitingForUpgrade = FALSE;
            return true;
        }
        #endregion TryLockReentrant_Timeout

        #region TryLockReentrantAsync_Timeout
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReentrantAsync(TimeSpan timeout, bool prioritized = false)
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

            if (!MustWait(prioritized) && NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                reentrancyIndicator.Value = reentrancyId;
                return reenterableWriteLockAttemptTask;
            }
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.WriteLockReenterable, timeout, prioritized, false);
            else return failedAttemptTask;
        }

        private async Task<LockAttempt> WaitForUpgradeAttemptAsync(TimeSpan timeout)
        {
            SynchronizationContext.SetSynchronizationContext(null);
            TimeFrame timer = new TimeFrame(timeout);
            // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
            if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");

            int counter = 0;
            // Waiting for upgrade to writeLock
            while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
            {
                if (timer.Elapsed)
                {
                    waitingForUpgrade = FALSE;
                    return new LockAttempt(new AcquiredLock());
                }

                if (MustYieldAsyncThread_ForReentranceUpgrade(++counter)) await Task.Yield();
                else Thread.Sleep(0);
            }
            waitingForUpgrade = FALSE;
            return new LockAttempt(new AcquiredLock(this, LockMode.Upgraded));
        }
        #endregion TryLockReentrantAsync_Timeout

        #region TryLockReentrantReadOnly_Timeout
        public bool TryLockReentrantReadOnly(TimeSpan timeout, out AcquiredLock acquiredLock, bool prioritized = false)
        {
            if (TryReenterReadOnly())
            {
                acquiredLock = new AcquiredLock(this, LockMode.Reentered);
                return true;
            }

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (!MustWait(false) && newLockIndicator >= FIRST_READ_LOCK && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new AcquiredLock(this, LockMode.ReadLockReenterable);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, true))
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new AcquiredLock(this, LockMode.ReadLockReenterable);
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
        public Task<LockAttempt> TryLockReentrantReadOnlyAsync(TimeSpan timeout, bool prioritized = false)
        {
            if (TryReenterReadOnly()) return reenteredLockAttemptTask;

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (!MustWait(false) && newLockIndicator >= FIRST_READ_LOCK && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                reentrancyIndicator.Value = reentrancyId;
                return reenterableReadLockAttemptTask;
            }
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.ReadLockReenterable, timeout, prioritized, true);
            else return failedAttemptTask;
        }
        #endregion TryLockReadOnlyAsync_Timeout

        #region Exit        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            int newLockIndicator = Interlocked.Decrement(ref lockIndicator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            lockIndicator = NO_LOCK;            
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void ExitReentrantReadLock()
        {
            int newLockIndicator = Interlocked.Decrement(ref lockIndicator);

            if (NO_LOCK == newLockIndicator)
            {
                RenewReentrancyId();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReentrantWriteLock()
        {
            lockIndicator = NO_LOCK;

            RenewReentrancyId();
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
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

        #region WakeOrder

        void EnqueueWakeOrder(WakeOrder wakeOrder)
        {
            if (queueHead == null)
            {
                queueHead = wakeOrder;
                wakeOrder.Next = null;
            }
            else
            {
                if (IsTicketOlder(wakeOrder.ticket, queueHead.ticket))
                {
                    wakeOrder.Next = queueHead;
                    queueHead = wakeOrder;
                }
                else
                {
                    var node = queueHead;
                    while (node.Next != null && IsTicketOlder(node.Next.ticket, wakeOrder.ticket)) node = node.Next;
                    wakeOrder.Next = node.Next;
                    node.Next = wakeOrder;
                }
            }

            numSleeping++;
            if (wakeOrder.isReadOnly) numSleepingReadOnly++;
        }

        WakeOrder DequeueWakeOrder()
        {
            while (queueHead != null && !queueHead.IsValid)
            {
                numSleeping--;
                if (queueHead.isReadOnly) numSleepingReadOnly--;

                queueHead = queueHead.Next;                
            }

            WakeOrder wakeOrder = null;
            if (queueHead != null)
            {
                numSleeping--;
                if (queueHead.isReadOnly) numSleepingReadOnly--;

                wakeOrder = queueHead;
                queueHead = queueHead.Next;                
            }
            return wakeOrder;
        }

        WakeOrder PickNextReadOnlyWakeOrder()
        {
            while (queueHead != null && !queueHead.IsValid)
            {
                numSleeping--;
                if (queueHead.isReadOnly) numSleepingReadOnly--;

                queueHead = queueHead.Next;                
            }

            WakeOrder wakeOrder = null;
            if (queueHead != null && queueHead.isReadOnly)
            {
                numSleeping--;
                if (queueHead.isReadOnly) numSleepingReadOnly--;

                wakeOrder = queueHead;
                queueHead = queueHead.Next;                
            }
            else
            {
                var prev = queueHead;
                while (numSleepingReadOnly > 0 && prev != null && wakeOrder == null)
                {
                    if (!prev.Next.IsValid)
                    {
                        numSleeping--;
                        if (prev.Next.isReadOnly) numSleepingReadOnly--;

                        prev.Next = prev.Next.Next;                        
                    }
                    else if (prev.Next.isReadOnly)
                    {
                        numSleeping--;
                        numSleepingReadOnly--;

                        wakeOrder = prev.Next;
                        prev.Next = wakeOrder.Next;                        
                    }
                    prev = prev.Next;
                }
            }
            return wakeOrder;
        }

        internal class WakeOrder
        {
            internal WakeOrder(int ticket, bool sleepsAsync, bool readOnly)
            {
                this.isReadOnly = readOnly;
                this.ticket = ticket;
                if (sleepsAsync) tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            TaskCompletionSource<bool> tcs = null;
            public readonly int ticket = 0;
            public readonly bool isReadOnly = false;
            volatile public WakeOrder Next;            
            public bool IsValid => sleeping;
            volatile protected bool sleeping = true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WakeUp(FeatureLock parent)
            {
                sleeping = false;                

                if (tcs == null)
                {
                    Monitor.Enter(this);
                    Monitor.PulseAll(this);
                    Monitor.Exit(this);
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static protected bool TryPrepareSleep(FeatureLock parent, out FastSpinLock.AcquiredLock acquiredSleepLock)
            {
                parent.anySleeping = true;
                acquiredSleepLock = parent.sleepLock.Lock();
                if (parent.MaySleep())
                {
                    parent.anySleeping = true;
                    return true;
                }
                else
                {
                    acquiredSleepLock.Exit();
                    return false;
                }
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static void TrySleep(FeatureLock parent, int ticket, bool readOnly)
            {
                if (TryPrepareSleep(parent, out var sleepLock))
                {
                    var wakeOrder = new WakeOrder(ticket, false, readOnly);
                    parent.EnqueueWakeOrder(wakeOrder);
                    sleepLock.Exit(); // exit before sleeping                    

                    Monitor.Enter(wakeOrder);
                    if (parent.MaySleep() && wakeOrder.sleeping)
                    {
                        Monitor.Wait(wakeOrder);
                    }
                    else wakeOrder.sleeping = false; //Invalidate, so wakeOrder will be skipped when waking up
                    Monitor.Exit(wakeOrder);
                }
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static async Task TrySleepAsync(FeatureLock parent, int ticket, bool readOnly)
            {
                SynchronizationContext.SetSynchronizationContext(null);
                if (TryPrepareSleep(parent, out var sleepLock))
                {
                    var wakeOrder = new WakeOrder(ticket, true, readOnly);
                    parent.EnqueueWakeOrder(wakeOrder);
                    sleepLock.Exit(); // exit before sleeping                    

                    if (parent.MaySleep() && wakeOrder.sleeping)
                    {
                        await wakeOrder.tcs.Task.ConfigureAwait(false);
                    }
                    else wakeOrder.sleeping = false; //Invalidate, so wakeOrder will be skipped when waking up
                }
            }
            
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static bool TrySleepUntilTimeout(FeatureLock parent, TimeFrame timer, int ticket, bool readOnly)
            {
                if (timer.Elapsed) return true;

                if (TryPrepareSleep(parent, out var sleepLock))
                {
                    var wakeOrder = new WakeOrder(ticket, false, readOnly);
                    parent.EnqueueWakeOrder(wakeOrder);
                    sleepLock.Exit(); // exit before sleeping                    

                    bool elapsed = false;
                    Monitor.Enter(wakeOrder);
                    if (parent.MaySleep() && wakeOrder.sleeping)
                    {
                        elapsed = !Monitor.Wait(wakeOrder, timer.Remaining);
                    }

                    wakeOrder.sleeping = false; //Invalidate, so wakeOrder will be skipped when waking up
                    Monitor.Exit(wakeOrder);

                    return elapsed;
                }
                else return false;
            }

            [MethodImpl(MethodImplOptions.NoOptimization)]
            public static async Task<bool> TrySleepUntilTimeoutAsync(FeatureLock parent, TimeFrame timer, int ticket, bool readOnly)
            {
                if (timer.Elapsed) return true;

                SynchronizationContext.SetSynchronizationContext(null);
                if (TryPrepareSleep(parent, out var sleepLock))
                {
                    var wakeOrder = new WakeOrder(ticket, true, readOnly);
                    parent.EnqueueWakeOrder(wakeOrder);
                    sleepLock.Exit(); // exit before sleeping                    

                    bool elapsed = false;
                    if (parent.MaySleep() && wakeOrder.sleeping)
                    {
                        elapsed = !await wakeOrder.tcs.Task.WaitAsync(timer.Remaining).ConfigureAwait(false);
                    }

                    wakeOrder.sleeping = false; //Invalidate, so wakeOrder will be skipped when waking up             
                    return elapsed;
                }
                else return false;
            }
        }

        #endregion WakeOrder

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
