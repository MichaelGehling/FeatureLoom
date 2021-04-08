using FeatureFlowFramework.Helpers.Time;
using System;
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
        #region ObjectLock
        // Stores FeatureLocks associated with objects, for the lifetime of the object
        static ConditionalWeakTable<object, FeatureLock> lockObjects = new ConditionalWeakTable<object, FeatureLock>();

        /// <summary>
        /// Provides a FeatureLock object that is associated with the given object.
        /// When called the first time for an object a new FeatureLock is created.
        /// Can be used directly in a using statement: 
        /// using(FeatureLock.GetLockFor(myObj).Lock()) {...}
        /// But using GetLockFor requires some extra CPU time, so in performance critical sections, better store the
        /// lock as a member variable and use this one in the using statement.        
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static FeatureLock GetLockFor(object obj)
        {
            if (!lockObjects.TryGetValue(obj, out FeatureLock featureLock))
            {
                featureLock = new FeatureLock();
                lockObjects.Add(obj, featureLock);
            }

            return featureLock;
        }
        #endregion ObjectLock

        #region Constants

        // Are set to lockIndicator if the lock is not acquired, acquired for writing, cquired for reading (further readers increase this value, each by one)
        private const int NO_LOCK = 0;

        private const int WRITE_LOCK = NO_LOCK - 1;
        private const int FIRST_READ_LOCK = NO_LOCK + 1;

        // Booleans cannot be used in CompareExchange, so ints are used.
        private const int FALSE = 0;

        private const int TRUE = 1;

        #endregion Constants

        #region Variables

        // The lower this value, the more candidates will wait, but not try to take the lock, in favour of the longer waiting candidates
        private int passiveWaitThreshold = 100;

        // The lower this value, the earlier async threads start yielding
        private int asyncYieldThreshold = 300;

        // The lower this value, the more often async threads yield
        private int asyncYieldBaseFrequency = 100;        

        // Keeps the last reentrancyId of the "logical thread".
        // A value that differs from the currently valid reentrancyId implies that the lock was not acquired before in this "logical thread",
        // so it must be acquired and cannot simply be reentered.
        private AsyncLocal<int> reentrancyIndicator = new AsyncLocal<int>();

        // If true, indicates that a reentrant write lock tries to upgrade an existing reentrant read lock,
        // but more than one reader is active, the upgrade must wait until only one reader is left
        private int waitingForUpgrade = FALSE;

        // The currently valid reentrancyId. It must never be 0, as this is the default value of the reentrancyIndicator.
        private int reentrancyId = 1;

        // Will be true if a candidate tries to acquire the lock with priority, so the other candidates know that they have to stay back.
        // If a prioritized candidate acquired the lock it will reset this variable, so if another prioritized candidate already waits,
        // it must set it back to true in its next cycle. So there can ba a short time when another non-priority candidate might acquire the lock nevertheless.
        private bool prioritizedWaiting = false;

        // the main lock variable, indicating current locking state:
        // 0 means no lock
        // -1 means write lock
        // >=1 means read lock (number implies the count of parallel readers)
        private int lockIndicator = NO_LOCK;

        // The next ticket number that will be provided. A ticket number must never be 0, so if nextTicket turns to be 0, it must be incremented, again.
        private int nextTicket = 1;

        // Contains the oldest ticket number that is currently waiting and therefor has the rank 0. The rank of the other tickets is calculated relative to this one.
        // Will be reset to 0 when the currently oldest candidate acquired the lock. For a short time, it is possible that some newer ticket number
        // is set until the oldest one reaches the pooint to update.
        private int firstRankTicket = 0;

        // Is used to measure how long it takes until the lock is released again.
        // It is incremented in every waiting cycle by the candidate with the firstRankTicket (rank 0).
        // When the lock is acquired again, it is reset to 0.
        private int waitCounter = 0;

        // After the lock was acquired, the average is updated by including the last waitCounter.
        private int averageWaitCount = 10;

        #endregion Variables

        #region PreparedTasks

        // These variables hold already completed task objects, prepared in advance in the constructor for later reuse,
        // in order to reduce garbage and improve performance by handling async calls synchronously, if no asynchronous sleeping/yielding is required
        private Task<LockHandle> readLockTask;

        private Task<LockHandle> reenterableReadLockTask;
        private Task<LockHandle> writeLockTask;
        private Task<LockHandle> reenterableWriteLockTask;
        private Task<LockHandle> upgradedLockTask;
        private Task<LockHandle> reenteredLockTask;
        private Task<LockAttempt> failedAttemptTask;
        private Task<LockAttempt> readLockAttemptTask;
        private Task<LockAttempt> reenterableReadLockAttemptTask;
        private Task<LockAttempt> writeLockAttemptTask;
        private Task<LockAttempt> reenterableWriteLockAttemptTask;
        private Task<LockAttempt> upgradedLockAttemptTask;
        private Task<LockAttempt> reenteredLockAttemptTask;

        /// <summary>
        /// A multi-purpose high-performance lock object that can be used in synchronous and asynchronous contexts.
        /// It supports reentrancy, prioritized lock acquiring, trying for lock acquiring with timeout and
        /// read-only locking for parallel access (incl. automatic upgrading/downgrading in conjunction with reentrancy).
        /// When the lock is acquired it returns a handle that can be used with a using statement for simple and clean usage.
        /// Example: using(myLock.Lock()) { ... }
        /// </summary>
        public FeatureLock()
        {
            readLockTask = Task.FromResult(new LockHandle(this, LockMode.ReadLock));
            reenterableReadLockTask = Task.FromResult(new LockHandle(this, LockMode.ReadLockReenterable));
            writeLockTask = Task.FromResult(new LockHandle(this, LockMode.WriteLock));
            reenterableWriteLockTask = Task.FromResult(new LockHandle(this, LockMode.WriteLockReenterable));
            upgradedLockTask = Task.FromResult(new LockHandle(this, LockMode.Upgraded));
            reenteredLockTask = Task.FromResult(new LockHandle(this, LockMode.Reentered));

            failedAttemptTask = Task.FromResult(new LockAttempt(new LockHandle()));
            readLockAttemptTask = Task.FromResult(new LockAttempt(new LockHandle(this, LockMode.ReadLock)));
            reenterableReadLockAttemptTask = Task.FromResult(new LockAttempt(new LockHandle(this, LockMode.ReadLockReenterable)));
            writeLockAttemptTask = Task.FromResult(new LockAttempt(new LockHandle(this, LockMode.WriteLock)));
            reenterableWriteLockAttemptTask = Task.FromResult(new LockAttempt(new LockHandle(this, LockMode.WriteLockReenterable)));
            upgradedLockAttemptTask = Task.FromResult(new LockAttempt(new LockHandle(this, LockMode.Upgraded)));
            reenteredLockAttemptTask = Task.FromResult(new LockAttempt(new LockHandle(this, LockMode.Reentered)));
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
        /// The lower this value, the earlier async threads start yielding
        /// </summary>
        public int AsyncYieldThreshold { get => asyncYieldThreshold; set => asyncYieldThreshold = value; }

        /// <summary>
        /// The lower this value, the more candidates will wait, but not try to take the lock, in favour of the longer waiting candidates
        /// </summary>
        public int PassiveWaitThreshold { get => passiveWaitThreshold; set => passiveWaitThreshold = value; }

        /// <summary>
        /// The lower this value, the more often async threads yield
        /// </summary>
        public int AsyncYieldBaseFrequency { get => asyncYieldBaseFrequency; set => asyncYieldBaseFrequency = value; }

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

        // Used to check if a candidate is allowed trying to acquire the lock without going into the waiting cycle
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustGoToWaiting(bool prioritized)
        {
            return !prioritized && (prioritizedWaiting || firstRankTicket != 0);
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
        private bool MustYieldAsyncThread(int rank, bool prioritized, int counter)
        {
            // If a special synchronization context is set (e.g. UI-Thread) the task must always be yielded to avoid blocking
            if (SynchronizationContext.Current != null) return true;

            // We must ensure that the rank is at least 1, otherwise we can get division by zero!
            rank = rank + 1;

            // Only yield if we waited long enough for the tickets rank
            if (rank * counter < asyncYieldThreshold) return false;

            ThreadPool.GetAvailableThreads(out int availableThreads, out _);
            ThreadPool.GetMaxThreads(out int maxThreads, out _);
            ThreadPool.GetMinThreads(out int minThreads, out _);
            var usedThreads = maxThreads - availableThreads;
            
            // If less thread pool threads are used than minimum available, we must never yield the task
            if (usedThreads < minThreads) return false;
                        
            // Otherwise we yield every now and then, less often for older tickets that should be preferred and less often the more threads we have in the thread pool
            // must be at least 1 to avoid division by zero!
            int asyncYieldFrequency = (asyncYieldBaseFrequency / rank) + 1;
            if (prioritized) asyncYieldFrequency = asyncYieldBaseFrequency; // prioritized waiters should yield the task very rarely, because it takes so much time
            return counter % asyncYieldFrequency == 0;
                        
        }

        // Checks if a candidate must still remain in the waiting queue or if acquiring would be possible
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustStillWait(bool prioritized, bool readOnly)
        {
            return (!readOnly && lockIndicator != NO_LOCK) || (readOnly && lockIndicator == WRITE_LOCK) || (!prioritized && prioritizedWaiting);
        }

        // Creates the a new ticket for a new waiting candidate.
        // It will ensure to never provide the 0, because it is reserved to indicate when the firstRankTicket is undefined.
        // GetTicket() might be called concurrently and we don't use interlocked, so it might happen that a ticket is provided multiple times,
        // but that is acceptable and will not cause any trouble.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetTicket()
        {
            var ticket = nextTicket++;
            if (ticket == 0) ticket = nextTicket++;
            return ticket;
        }

        // Checks if the given ticket might be the first rank ticket.
        // Returns the rank of the ticket, which is the distance from the first rank ticket to the given ticket.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int UpdateRank(int ticket)
        {
            var first = firstRankTicket;
            if (ticket == first) return 0;

            if (first == 0 || IsTicketOlder(ticket, first))
            {
                firstRankTicket = ticket;
                return 0;
            }
            else
            {
                if (ticket >= first)
                {
                    return ticket - first;
                }
                else
                {
                    // covers the wrap around case
                    return (int)(((long)ticket + int.MaxValue) - first);
                }
            }
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

        // Actually tries to acquire the lock either for writing or reading.        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAcquire(bool readOnly)
        {
            if (readOnly) return TryAcquireForReading();
            else return TryAcquireForWriting();
        }

        // Actually tries to acquire the lock for writing.
        // Uses Interlocked.CompareExchange to ensure that only one candidate can change the lockIndicator at a time.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAcquireForWriting()
        {
            // Only allow setting the write lock if the lockIndicator was set to NO_LOCK before
            return NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK);
        }

        // Actually tries to acquire the lock for reading.
        // Uses Interlocked.CompareExchange to ensure that only one candidate can change the lockIndicator at a time.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAcquireForReading()
        {
            // It is allowed for multiple candidates to acquire the lock for readOnly in parallel.
            // The number of candidates that acquired under readOnly restriction is counted in the lockIndicator, starting with FIRST_READ_LOCK,
            // so we take the current lockIndicator and increase it by try and check if it would be a proper readOnly-value (newLockIndicator >= FIRST_READ_LOCK)
            // If it is the case we try to actually set the new value to the lockIndicator variable
            int currentLockIndicator = lockIndicator;
            int newLockIndicator = currentLockIndicator + 1;
            return newLockIndicator >= FIRST_READ_LOCK && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator);
        }

        // Must be called after waiting, either when the lock is acquied or when timed out
        // Will reset values where necessary and calculate the average wait count
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FinishWaiting(bool prioritized, bool acquired, int rank)
        {
            if (rank == 0) firstRankTicket = 0;
            if (prioritized) prioritizedWaiting = false;
            if (acquired)
            {
                averageWaitCount = (averageWaitCount * 9 + waitCounter + 1) / 10;
                waitCounter = 0;
            }
        }

        // Checks if the candidate may acquire the lock or must stay back and wait based on its rank
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustWaitPassive(bool prioritized, int rank)
        {
            int waitFactor = Math.Max(averageWaitCount, waitCounter);
            return !prioritized && rank * waitFactor > passiveWaitThreshold;
        }

        #endregion HelperMethods

        #region Lock

        /// <summary>
        /// Waits until the lock is acquired exclusively and then returns the LockHandle.
        /// The LockHandle must be disposed to release the lock, most convenient with the using statement:
        /// using(myLock.Lock()) {...}
        /// </summary>
        /// <param name="prioritized">When true the caller will be preferred if multiple candidates wait for the lock</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LockHandle Lock(bool prioritized = false)
        {
            if (MustGoToWaiting(prioritized) || !TryAcquireForWriting()) Lock_Wait(prioritized, false);
            return new LockHandle(this, LockMode.WriteLock);
        }

        // If the lock couldn't be acquired immediatly, this method is called to let the candidate wait until the lock is finally acquired
        // In case of a higher rank and longer waiting time the candidate doesn't get the chance to acquire the lock, yet, 
        // but simply sleeps and checks the rank again later.
        // If the rank is low enough, the candidate will first check if the lock is potentially acquireable, if yes, it tries to get it,
        // otherwise it also sleeps and tries later, again.
        // The candidate with the lowest rank (rank==0) is responsible to count the global waitCounter as an indicator how long the lock is currently acquired.
        private void Lock_Wait(bool prioritized, bool readOnly)
        {
            if (prioritized) prioritizedWaiting = true;
            int ticket = GetTicket();
            int rank;
            bool skip;
            do
            {                
                rank = UpdateRank(ticket);                
                if (MustWaitPassive(prioritized, rank))
                {
                    skip = true;
                    Thread.Sleep(0);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) waitCounter++;
                        Thread.Sleep(0);
                        skip = MustStillWait(prioritized, readOnly);
                    }
                }
            }
            while (skip || !TryAcquire(readOnly));

            FinishWaiting(prioritized, true, rank);
        }

        #endregion Lock

        #region LockAsync

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockHandle> LockAsync(bool prioritized = false)
        {
            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting()) return writeLockTask;
            else return LockAsync_Wait(LockMode.WriteLock, prioritized, false);
        }

        private Task<LockHandle> LockAsync_Wait(LockMode mode, bool prioritized, bool readOnly)
        {
            if (prioritized) prioritizedWaiting = true;
            int ticket = GetTicket();
            int rank;
            int counter = 0;
            bool skip;
            do
            {
                rank = UpdateRank(ticket);
                if (MustWaitPassive(prioritized, rank))
                {
                    skip = true;

                    if (MustYieldAsyncThread(rank, prioritized, counter++)) return LockAsync_Wait_ContinueWithAwaiting(mode, ticket, prioritized, readOnly, counter);
                    else Thread.Sleep(0);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) waitCounter++;

                        if (MustYieldAsyncThread(rank, prioritized, counter++)) return LockAsync_Wait_ContinueWithAwaiting(mode, ticket, prioritized, readOnly, counter);
                        else Thread.Sleep(0);

                        skip = MustStillWait(prioritized, readOnly);
                    }
                }
            }
            while (skip || !TryAcquire(readOnly));

            FinishWaiting(prioritized, true, rank);

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

        private async Task<LockHandle> LockAsync_Wait_ContinueWithAwaiting(LockMode mode, int ticket, bool prioritized, bool readOnly, int counter)
        {
            if (prioritized) prioritizedWaiting = true;

            await Task.Yield();

            int rank;
            bool skip;
            do
            {
                rank = UpdateRank(ticket);
                if (MustWaitPassive(prioritized, rank))
                {
                    skip = true;

                    if (MustYieldAsyncThread(rank, prioritized, counter++)) await Task.Yield();
                    else Thread.Sleep(0);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) waitCounter++;

                        if (MustYieldAsyncThread(rank, prioritized, counter++)) await Task.Yield();
                        else Thread.Sleep(0);

                        skip = MustStillWait(prioritized, readOnly);
                    }
                }
            }
            while (skip || !TryAcquire(readOnly));

            FinishWaiting(prioritized, true, rank);

            if (readOnly)
            {
                if (mode == LockMode.ReadLockReenterable) reentrancyIndicator.Value = reentrancyId;
                return new LockHandle(this, mode);
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable) reentrancyIndicator.Value = reentrancyId;
                return new LockHandle(this, mode);
            }
        }

        #endregion LockAsync

        #region LockReadOnly

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LockHandle LockReadOnly(bool prioritized = false)
        {
            if (MustGoToWaiting(prioritized) || !TryAcquireForReading()) Lock_Wait(prioritized, true);
            return new LockHandle(this, LockMode.ReadLock);
        }

        #endregion LockReadOnly

        #region LockReadOnlyAsync

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockHandle> LockReadOnlyAsync(bool prioritized = false)
        {
            if (!MustGoToWaiting(prioritized) && TryAcquireForReading()) return readLockTask;
            else return LockAsync_Wait(LockMode.ReadLock, prioritized, true);
        }

        #endregion LockReadOnlyAsync

        #region LockReentrant

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LockHandle LockReentrant(bool prioritized = false)
        {
            if (TryReenter(out LockHandle acquiredLock, true, out _)) return acquiredLock;
            if (MustGoToWaiting(prioritized) || !TryAcquireForWriting()) Lock_Wait(prioritized, false);
            reentrancyIndicator.Value = reentrancyId;
            return new LockHandle(this, LockMode.WriteLockReenterable);
        }

        private bool TryReenter(out LockHandle acquiredLock, bool waitForUpgrade, out bool upgradePossible)
        {
            var currentLockIndicator = lockIndicator;
            if (currentLockIndicator != NO_LOCK && HasValidReentrancyContext)
            {
                if (currentLockIndicator == WRITE_LOCK)
                {
                    upgradePossible = false;
                    acquiredLock = new LockHandle(this, LockMode.Reentered);
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
                            acquiredLock = new LockHandle();
                            return false;
                        }
                    }
                    waitingForUpgrade = FALSE;
                    prioritizedWaiting = false;

                    upgradePossible = false;
                    acquiredLock = new LockHandle(this, LockMode.Upgraded);
                    return true;
                }
            }
            upgradePossible = false;
            acquiredLock = new LockHandle();
            return false;
        }

        #endregion LockReentrant

        #region LockReentrantAsync

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockHandle> LockReentrantAsync(bool prioritized = false)
        {
            if (TryReenter(out LockHandle acquiredLock, false, out bool upgradePossible))
            {
                if (acquiredLock.mode == LockMode.Reentered) return reenteredLockTask;
                else return upgradedLockTask;
            }
            else if (upgradePossible) return WaitForUpgradeAsync();

            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting())
            {
                reentrancyIndicator.Value = reentrancyId;
                return reenterableWriteLockTask;
            }
            else return LockAsync_Wait(LockMode.WriteLockReenterable, prioritized, false);
        }

        private async Task<LockHandle> WaitForUpgradeAsync()
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
            return new LockHandle(this, LockMode.Upgraded);
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
        public LockHandle LockReentrantReadOnly(bool prioritized = false)
        {
            if (TryReenterReadOnly()) return new LockHandle(this, LockMode.Reentered);

            if (MustGoToWaiting(prioritized) || !TryAcquireForReading()) Lock_Wait(prioritized, true);

            reentrancyIndicator.Value = reentrancyId;
            return new LockHandle(this, LockMode.ReadLockReenterable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReenterReadOnly()
        {
            return lockIndicator != NO_LOCK && HasValidReentrancyContext;
        }

        #endregion LockReentrantReadOnly

        #region LockReentrantReadOnlyAsync

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockHandle> LockReentrantReadOnlyAsync(bool prioritized = false)
        {
            if (TryReenterReadOnly()) return reenteredLockTask;

            if (!MustGoToWaiting(prioritized) && TryAcquireForReading())
            {
                reentrancyIndicator.Value = reentrancyId;
                return reenterableReadLockTask;
            }
            else return LockAsync_Wait(LockMode.ReadLockReenterable, prioritized, true);
        }

        #endregion LockReentrantReadOnlyAsync

        #region TryLock

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out LockHandle acquiredLock, bool prioritized = false)
        {
            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting())
            {
                acquiredLock = new LockHandle(this, LockMode.WriteLock);
                return true;
            }
            else
            {
                acquiredLock = new LockHandle();
                return false;
            }
        }

        #endregion TryLock

        #region TryLockReadOnly

        public bool TryLockReadOnly(out LockHandle acquiredLock, bool prioritized = false)
        {
            if (!MustGoToWaiting(prioritized) && TryAcquireForReading())
            {
                acquiredLock = new LockHandle(this, LockMode.ReadLock);
                return true;
            }
            else
            {
                acquiredLock = new LockHandle();
                return false;
            }
        }

        #endregion TryLockReadOnly

        #region TryLockReentrant

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReentrant(out LockHandle acquiredLock, bool prioritized = false)
        {
            if (TryReenter(out acquiredLock, false, out _)) return true;

            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting())
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new LockHandle(this, LockMode.WriteLockReenterable);
                return true;
            }
            else
            {
                acquiredLock = new LockHandle();
                return false;
            }
        }

        #endregion TryLockReentrant

        #region TryLockReentrantReadOnly

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReentrantReadOnly(out LockHandle acquiredLock, bool prioritized = false)
        {
            if (TryReenterReadOnly())
            {
                acquiredLock = new LockHandle(this, LockMode.Reentered);
                return true;
            }

            if (!MustGoToWaiting(prioritized) && TryAcquireForReading())
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new LockHandle(this, LockMode.ReadLockReenterable);
                return true;
            }
            acquiredLock = new LockHandle();
            return false;
        }

        #endregion TryLockReentrantReadOnly

        #region TryLock_Timeout

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(TimeSpan timeout, out LockHandle acquiredLock, bool prioritized = false)
        {
            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting())
            {
                acquiredLock = new LockHandle(this, LockMode.WriteLock);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, false))
            {
                acquiredLock = new LockHandle(this, LockMode.WriteLock);
                return true;
            }
            else
            {
                acquiredLock = new LockHandle();
                return false;
            }
        }

        private bool TryLock_Wait(TimeSpan timeout, bool prioritized, bool readOnly)
        {
            TimeFrame timer = new TimeFrame(timeout);

            if (prioritized) prioritizedWaiting = true;
            int ticket = GetTicket();
            int rank;
            bool skip;
            do
            {
                rank = UpdateRank(ticket);
                if (timer.Elapsed)
                {
                    FinishWaiting(prioritized, false, rank);
                    return false;
                }
                if (MustWaitPassive(prioritized, rank))
                {
                    skip = true;
                    Thread.Sleep(0);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) waitCounter++;
                        Thread.Sleep(0);
                        skip = MustStillWait(prioritized, readOnly);
                    }
                }
            }
            while (skip || !TryAcquire(readOnly));

            FinishWaiting(prioritized, true, rank);
            return true;
        }

        #endregion TryLock_Timeout

        #region TryLockAsync_Timeout

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockAsync(TimeSpan timeout, bool prioritized = false)
        {
            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting()) return writeLockAttemptTask;
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.WriteLock, timeout, prioritized, false);
            else return failedAttemptTask;
        }

        private Task<LockAttempt> TryLockAsync_Wait(LockMode mode, TimeSpan timeout, bool prioritized, bool readOnly)
        {
            TimeFrame timer = new TimeFrame(timeout);

            if (prioritized) prioritizedWaiting = true;
            int ticket = GetTicket();
            int rank;
            bool skip;
            int counter = 0;
            do
            {
                rank = UpdateRank(ticket);
                if (timer.Elapsed)
                {
                    FinishWaiting(prioritized, false, rank);
                    return failedAttemptTask;
                }

                if (MustWaitPassive(prioritized, rank))
                {
                    skip = true;
                    if (MustYieldAsyncThread(rank, prioritized, counter++)) return TryLockAsync_Wait_ContinueWithAwaiting(mode, timer, ticket, prioritized, readOnly, counter);
                    else Thread.Sleep(0);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) waitCounter++;

                        if (MustYieldAsyncThread(rank, prioritized, counter++)) return TryLockAsync_Wait_ContinueWithAwaiting(mode, timer, ticket, prioritized, readOnly, counter);
                        else Thread.Sleep(0);

                        skip = MustStillWait(prioritized, readOnly);
                    }
                }
            }
            while (skip || !TryAcquire(readOnly));

            FinishWaiting(prioritized, true, rank);

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

        private async Task<LockAttempt> TryLockAsync_Wait_ContinueWithAwaiting(LockMode mode, TimeFrame timer, int ticket, bool prioritized, bool readOnly, int counter)
        {
            if (prioritized) prioritizedWaiting = true;
            await Task.Yield();

            int rank;
            bool skip;
            do
            {
                rank = UpdateRank(ticket);
                if (timer.Elapsed)
                {
                    FinishWaiting(prioritized, false, rank);
                    return new LockAttempt(new LockHandle());
                }

                if (MustWaitPassive(prioritized, rank))
                {
                    skip = true;
                    if (MustYieldAsyncThread(rank, prioritized, counter++)) await Task.Yield();
                    else Thread.Sleep(0);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) waitCounter++;

                        if (MustYieldAsyncThread(rank, prioritized, counter++)) await Task.Yield();
                        else Thread.Sleep(0);

                        skip = MustStillWait(prioritized, readOnly);
                    }
                }
            }
            while (skip || !TryAcquire(readOnly));

            FinishWaiting(prioritized, true, rank);

            if (readOnly)
            {
                if (mode == LockMode.ReadLockReenterable) reentrancyIndicator.Value = reentrancyId;
                return new LockAttempt(new LockHandle(this, mode));
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable) reentrancyIndicator.Value = reentrancyId;
                return new LockAttempt(new LockHandle(this, mode));
            }
        }

        #endregion TryLockAsync_Timeout

        #region TryLockReadOnly_Timeout

        public bool TryLockReadOnly(TimeSpan timeout, out LockHandle acquiredLock, bool prioritized = false)
        {
            if (!MustGoToWaiting(prioritized) && TryAcquireForReading())
            {
                acquiredLock = new LockHandle(this, LockMode.ReadLock);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, true))
            {
                acquiredLock = new LockHandle(this, LockMode.ReadLock);
                return true;
            }
            else
            {
                acquiredLock = new LockHandle();
                return false;
            }
        }

        #endregion TryLockReadOnly_Timeout

        #region TryLockReadOnlyAsync_Timeout

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReadOnlyAsync(TimeSpan timeout, bool prioritized = false)
        {
            if (!MustGoToWaiting(prioritized) && TryAcquireForReading()) return readLockAttemptTask;
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.ReadLock, timeout, prioritized, true);
            else return failedAttemptTask;
        }

        #endregion TryLockReadOnlyAsync_Timeout

        #region TryLockReentrant_Timeout

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReentrant(TimeSpan timeout, out LockHandle acquiredLock, bool prioritized = false)
        {
            if (TryReenter(out acquiredLock, false, out bool upgradePossible)) return true;
            else if (upgradePossible)
            {
                if (timeout > TimeSpan.Zero && WaitForUpgrade(timeout))
                {
                    acquiredLock = new LockHandle(this, LockMode.Upgraded);
                    return true;
                }
                else
                {
                    acquiredLock = new LockHandle();
                    return false;
                }
            }

            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting())
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new LockHandle(this, LockMode.WriteLockReenterable);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, false))
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new LockHandle(this, LockMode.WriteLockReenterable);
                return true;
            }
            else
            {
                acquiredLock = new LockHandle();
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
            if (TryReenter(out LockHandle acquiredLock, false, out bool upgradePossible))
            {
                if (acquiredLock.mode == LockMode.Reentered) return reenteredLockAttemptTask;
                else return upgradedLockAttemptTask;
            }
            else if (upgradePossible)
            {
                if (timeout > TimeSpan.Zero) return WaitForUpgradeAttemptAsync(timeout);
                else return failedAttemptTask;
            }

            if (!MustGoToWaiting(prioritized) && NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
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
                    return new LockAttempt(new LockHandle());
                }

                if (MustYieldAsyncThread_ForReentranceUpgrade(++counter)) await Task.Yield();
                else Thread.Sleep(0);
            }
            waitingForUpgrade = FALSE;
            return new LockAttempt(new LockHandle(this, LockMode.Upgraded));
        }

        #endregion TryLockReentrantAsync_Timeout

        #region TryLockReentrantReadOnly_Timeout

        public bool TryLockReentrantReadOnly(TimeSpan timeout, out LockHandle acquiredLock, bool prioritized = false)
        {
            if (TryReenterReadOnly())
            {
                acquiredLock = new LockHandle(this, LockMode.Reentered);
                return true;
            }

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (!MustGoToWaiting(false) && newLockIndicator >= FIRST_READ_LOCK && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new LockHandle(this, LockMode.ReadLockReenterable);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, true))
            {
                reentrancyIndicator.Value = reentrancyId;
                acquiredLock = new LockHandle(this, LockMode.ReadLockReenterable);
                return true;
            }
            else
            {
                acquiredLock = new LockHandle();
                return false;
            }
        }

        #endregion TryLockReentrantReadOnly_Timeout

        #region TryLockReentrantReadOnlyAsync_Timeout

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReentrantReadOnlyAsync(TimeSpan timeout, bool prioritized = false)
        {
            if (TryReenterReadOnly()) return reenteredLockAttemptTask;

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (!MustGoToWaiting(false) && newLockIndicator >= FIRST_READ_LOCK && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                reentrancyIndicator.Value = reentrancyId;
                return reenterableReadLockAttemptTask;
            }
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.ReadLockReenterable, timeout, prioritized, true);
            else return failedAttemptTask;
        }

        #endregion TryLockReentrantReadOnlyAsync_Timeout

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
            if (FIRST_READ_LOCK == lockIndicator) RenewReentrancyId();
            Interlocked.Decrement(ref lockIndicator);            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReentrantWriteLock()
        {
            RenewReentrancyId();
            lockIndicator = NO_LOCK;            
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

        #region LockHandle

        internal enum LockMode
        {
            WriteLock,
            ReadLock,
            WriteLockReenterable,
            ReadLockReenterable,
            Reentered,
            Upgraded
        }

        public struct LockHandle : IDisposable
        {
            private FeatureLock parentLock;
            internal LockMode mode;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal LockHandle(FeatureLock parentLock, LockMode mode)
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
            private LockHandle acquiredLock;

            internal LockAttempt(LockHandle acquiredLock)
            {
                this.acquiredLock = acquiredLock;
            }

            public bool Succeeded(out LockHandle acquiredLock)
            {
                acquiredLock = this.acquiredLock;
                return acquiredLock.IsActive;
            }
        }

        #endregion LockHandle         
    }

}