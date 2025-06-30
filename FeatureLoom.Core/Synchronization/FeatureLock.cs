using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using FeatureLoom.Scheduling;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.DependencyInversion;

namespace FeatureLoom.Synchronization
{

    /// <summary>
    /// A multi-purpose high-performance lock object that can be used in synchronous and asynchronous contexts.
    /// It supports reentrancy, prioritized lock acquiring, trying for lock acquiring with or without timeout and
    /// read-only locking for parallel access (incl. automatic upgrading/downgrading in conjunction with reentrancy).
    /// When the lock is acquired, it returns a handle that can be used with a using statement for simple and clean usage.
    /// Example: using(myLock.Lock()) { ... }
    /// In many scenarios the FeatureLock is faster than the build-in locks (e.g. Monitor/ReaderWriterLock for synchronous contexts
    /// and SemaphoreSlim for asynchronous contexts). Though reentrant locking in synchronous contexts using FeatureLock
    /// is slower than with Monitor/ReaderWriterLock, it also allows reentrancy for asynchronous contexts and even mixed contexts.
    /// </summary>
    public sealed class FeatureLock : ISchedule
    {
        #region ConstructorAndSettings
        /// <summary>
        /// A multi-purpose high-performance lock object that can be used in synchronous and asynchronous contexts.
        /// It supports reentrancy, prioritized lock acquiring, trying for lock acquiring with or without timeout and
        /// read-only locking for parallel access (incl. automatic upgrading/downgrading in conjunction with reentrancy).
        /// When the lock is acquired it returns a handle that can be used within a using statement for simple and clean usage.
        /// Example: using(myLock.Lock()) { ... }        
        /// </summary>
        public FeatureLock(FeatureLockSettings settings = null)
        {
            if (settings != null) lazy.Obj.settings.Obj = settings;
        }

        // Several configuration settings for the FeatureLock
        public class FeatureLockSettings
        {
            // The lower this value, the more candidates will wait, but not try to take the lock, in favour of the longer waiting candidates
            // 0 means that the wait order will be exactly respected (in nearly all cases), but it comes with a quite noticeable performance cost, especially for high frequency locking.
            public ushort passiveWaitThreshold = 35;
            public ushort passiveWaitThresholdAsync = 35;
            // The lower this value, the more candidates will go to sleep (must not be smaller than PassiveWaitThreshold)
            public ushort sleepWaitThreshold = 200;
            public ushort sleepWaitThresholdAsync = 60;
            // The lower this value, the later a sleeping candidate is waked up
            public ushort awakeThreshold = 40;
            // The lower this value, the earlier the waiter must leave the synchronous path and the more often the waiter must make an async yield
            public ushort asyncYieldBaseFrequency = 120;
            // The weight of the former averageWaitCount vs. the current waitCount (e.g. 3 means 3:1)
            public ushort averageWeighting = 3;
            // How often the scheduler at least checks to wake up sleeping candidates (1 is 0.01ms, so 100 means 1.0ms). Whenever the lock is released the scheduler is triggered, anyway.
            public ushort schedulerDelayFactor = 1500;
            // If true, avoids (in nearly all cases) that a new candidate may acquire a recently released lock before one of the waiting candidates gets it.
            // Comes with a quite noticeable performance cost, especially for high frequency locking.
            public bool restrictQueueJumping = false;
            // If true and and waiting async and it is time for async yield and SynchronizationContext is set, it will not just be a Task.Yield(), but a Task.Delay(1);
            public bool sleepForAsyncYieldInSyncContext = true;            

            public FeatureLockSettings Clone() => (FeatureLockSettings)this.MemberwiseClone();            
        }

        // Predefined settings, default is the same as PerformanceSettings, but can be changed.
        private static FeatureLockSettings defaultSettings = new();
        private static readonly FeatureLockSettings performanceSettings = new();        
        private static readonly FeatureLockSettings fairnessSettings = new() { restrictQueueJumping = true, 
                                                                               passiveWaitThreshold = 0, 
                                                                               sleepWaitThreshold = 20, 
                                                                               passiveWaitThresholdAsync = 0, 
                                                                               sleepWaitThresholdAsync = 20 };
        /// <summary>
        /// The settings that are used by all FeatureLock instances where settings were not explicitly set in the constructor.
        /// The DefaultSettings are the same as the prepared PerformanceSettings, but they can be changed, to affect all FeatureLock instances.
        /// </summary>
        public static FeatureLockSettings DefaultSettings
        {
            get => defaultSettings;
            set => defaultSettings = value;
        }
        /// <summary>
        /// Predefined settings optimized for performance. depending on the application and system, performance can still be improved by further tweaking.
        /// Fairness is less, but it is guaranteed to avoid endless waiting by favouring longer waiting candidates.
        /// </summary>
        public static FeatureLockSettings PerformanceSettings => performanceSettings;
        /// <summary>
        /// Predefined settings optimized for fairness, forcing to keep the order in near to all cases, but coming with a performance penalty.
        /// </summary>
        public static FeatureLockSettings FairnessSettings  => fairnessSettings;
        #endregion ConstructorAndSettings

        #region ObjectLock
        // Stores FeatureLocks associated with objects, for the lifetime of the objects
        private static LazyValue<ConditionalWeakTable<object, FeatureLock>> lockObjects;

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
            if (!lockObjects.Obj.TryGetValue(obj, out FeatureLock featureLock))
            {
                featureLock = new FeatureLock();
                lockObjects.Obj.Add(obj, featureLock);
            }

            return featureLock;
        }
        #endregion ObjectLock

        #region Constants

        // Are set to lockIndicator if the lock is not acquired, acquired for writing, aquired for reading (further readers increase this value, each by one)
        private const int NO_LOCK = 0;
        private const int WRITE_LOCK = NO_LOCK - 1;
        private const int FIRST_READ_LOCK = NO_LOCK + 1;

        // Booleans cannot be used in CompareExchange, so ints are used.
        private const int FALSE = 0;
        private const int TRUE = 1;

        #endregion Constants

        #region Variables
        // NOTE: The order of the variables matters for performance.


        // Additional variables that are used less often, so they are kept in an extra object which is only loaded as required 
        // in order to reduce the memory footprint
        private LazyValue<LazyVariables> lazy;

        // Stored prepared tasks to speed up async calls that can be handeled synchronously.
        // More prepared tasks are in LazyVariables object
        // Note: failedAttemptTask can be static, because it is the same for all lock instances.
        private static Task<LockAttempt> failedAttemptTask;
        private Task<LockHandle> writeLockTask;
        private Task<LockAttempt> writeLockAttemptTask;        

        // The first element of a linked list of SleepHandles
        private SleepHandle queueHead = null;        
        // Used to safely manage Sleephandles
        private MicroValueLock sleepLock = new();

        // the main lock variable, indicating current locking state:
        // 0 means no lock
        // -1 means write lock
        // >=1 means read lock (number implies the count of parallel readers)
        private volatile int lockIndicator = NO_LOCK;

        // Is used to measure how long it takes until the lock is released again.
        // It is incremented in every waiting cycle by the candidate with the firstRankTicket (rank 0).
        // When the lock is acquired again, it is reset to 1.
        private ushort waitCounter = 1;

        // After the lock was acquired, the average is updated by including the last waitCounter.
        private ushort averageWaitCount = 10;

        // The next ticket number that will be provided. A ticket number must never be 0, so if nextTicket turns to be 0, it must be incremented, again.
        private ushort nextTicket = 1;

        // Contains the oldest ticket number that is currently waiting and therefor has the rank 0. The rank of the other tickets is calculated relative to this one.
        // Will be reset to 0 when the currently oldest candidate acquired the lock. For a short time, it is possible that some newer ticket number
        // is set until the actually oldest one reaches the pooint to update.
        private volatile ushort firstRankTicket = 0;

        // Will be true if a candidate tries to acquire the lock with priority, so the other candidates know that they have to stay back.
        // If a prioritized candidate acquired the lock it will reset this variable, so if another prioritized candidate already waits,
        // it must set it back to true in its next cycle. So there can ba a short time when another non-priority candidate might acquire the lock nevertheless.
        private volatile bool prioritizedWaiting = false;

        private bool reentrancyActive = false;

        // Indicates if scheduler is currently observing SleepHandles to wake up the candidates on time
        private bool IsScheduleActive => queueHead != null;

        #endregion Variables

        #region LazyVariables
        internal class LazyVariables
        {
            // If not using the default settings, the settings are stored here.
            internal LazyValue<FeatureLockSettings> settings;

            // Stored prepared tasks to speed up async calls that can be handeled synchronously.
            private Task<LockHandle> readLockTask;
            private Task<LockHandle> reenterableReadLockTask;
            private Task<LockHandle> reenterableWriteLockTask;
            private Task<LockHandle> upgradedLockTask;
            private Task<LockHandle> reenteredLockTask;
            private Task<LockAttempt> readLockAttemptTask;
            private Task<LockAttempt> reenterableReadLockAttemptTask;
            private Task<LockAttempt> reenterableWriteLockAttemptTask;
            private Task<LockAttempt> upgradedLockAttemptTask;
            private Task<LockAttempt> reenteredLockAttemptTask;

            // Keeps the last reentrancyId of the "logical thread".
            // A value that differs from the currently valid reentrancyId implies that the lock was not acquired before in this "logical thread",
            // so it must be acquired and cannot simply be reentered.
            internal LazyValue<AsyncLocal<ushort>> reentrancyIndicator;

            // If true, indicates that a reentrant write lock tries to upgrade an existing reentrant read lock,
            // but more than one reader is active, the upgrade must wait until only one reader is left
            internal int waitingForUpgrade = FALSE;

            // The currently valid reentrancyId. It must never be 0, as this is the default value of the reentrancyIndicator.
            internal ushort reentrancyId = 1;

            // The following methods provide the prepared tasks. If not existent yet they create and store them.
            internal Task<LockHandle> GetReadLockTask(FeatureLock parent)
            {
                if (readLockTask == null) Interlocked.CompareExchange(ref readLockTask, Task.FromResult(new LockHandle(parent, LockMode.ReadLock)), null);
                return readLockTask;
            }

            internal Task<LockHandle> GetReenterableReadLockTask(FeatureLock parent)
            {
                if (reenterableReadLockTask == null) Interlocked.CompareExchange(ref reenterableReadLockTask, Task.FromResult(new LockHandle(parent, LockMode.ReadLockReenterable)), null);
                return reenterableReadLockTask;
            }

            internal Task<LockHandle> GetReenterableWriteLockTask(FeatureLock parent)
            {
                if (reenterableWriteLockTask == null) Interlocked.CompareExchange(ref reenterableWriteLockTask, Task.FromResult(new LockHandle(parent, LockMode.WriteLockReenterable)), null);
                return reenterableWriteLockTask;
            }

            internal Task<LockHandle> GetUpgradedLockTask(FeatureLock parent)
            {
                if (upgradedLockTask == null) Interlocked.CompareExchange(ref upgradedLockTask, Task.FromResult(new LockHandle(parent, LockMode.Upgraded)), null);
                return upgradedLockTask;
            }

            internal Task<LockHandle> GetReenteredLockTask(FeatureLock parent)
            {
                if (reenteredLockTask == null) Interlocked.CompareExchange(ref reenteredLockTask, Task.FromResult(new LockHandle(parent, LockMode.Reentered)), null);
                return reenteredLockTask;
            }

            internal Task<LockAttempt> GetReadLockAttemptTask(FeatureLock parent)
            {
                if (readLockAttemptTask == null) Interlocked.CompareExchange(ref readLockAttemptTask, Task.FromResult(new LockAttempt(new LockHandle(parent, LockMode.ReadLock))), null);
                return readLockAttemptTask;
            }

            internal Task<LockAttempt> GetReenterableReadLockAttemptTask(FeatureLock parent)
            {
                if (reenterableReadLockAttemptTask == null) Interlocked.CompareExchange(ref reenterableReadLockAttemptTask, Task.FromResult(new LockAttempt(new LockHandle(parent, LockMode.ReadLockReenterable))), null);
                return reenterableReadLockAttemptTask;
            }

            internal Task<LockAttempt> GetReenterableWriteLockAttemptTask(FeatureLock parent)
            {
                if (reenterableWriteLockAttemptTask == null) Interlocked.CompareExchange(ref reenterableWriteLockAttemptTask, Task.FromResult(new LockAttempt(new LockHandle(parent, LockMode.WriteLockReenterable))), null);
                return reenterableWriteLockAttemptTask;
            }

            internal Task<LockAttempt> GetUpgradedLockAttemptTask(FeatureLock parent)
            {
                if (upgradedLockAttemptTask == null) Interlocked.CompareExchange(ref upgradedLockAttemptTask, Task.FromResult(new LockAttempt(new LockHandle(parent, LockMode.Upgraded))), null);
                return upgradedLockAttemptTask;
            }

            internal Task<LockAttempt> GetReenteredLockAttemptTask(FeatureLock parent)
            {
                if (reenteredLockAttemptTask == null) Interlocked.CompareExchange(ref reenteredLockAttemptTask, Task.FromResult(new LockAttempt(new LockHandle(parent, LockMode.Reentered))), null);
                return reenteredLockAttemptTask;
            }
        }

        #endregion LazyVariables

        #region LocalVariableAccess
        // Provides the active settings of the FeatureLock
        private FeatureLockSettings Settings => lazy.ObjIfExists?.settings.ObjIfExists ?? DefaultSettings;

        // Access to the last reentrancyId of the "logical thread".
        // A value that differs from the currently valid reentrancyId implies that the lock was not acquired before in this "logical thread",
        // so it must be acquired and cannot simply be reentered.
        private ushort ReentrancyIndicator
        {
            get { return lazy.Exists ? lazy.Obj.reentrancyIndicator.Obj.Value : default; }

            set { lazy.Obj.reentrancyIndicator.Obj.Value = value; }
        }

        // True when the ReentrancyIndicator was already created
        private bool ReentrancyIndicatorExists => lazy.Exists && lazy.Obj.reentrancyIndicator.Exists;

        // The currently valid reentrancyId. It must never be 0, as this is the default value of the reentrancyIndicator.
        private ushort ReentrancyId => lazy.Obj.reentrancyId;

        // If true, indicates that a reentrant write lock tries to upgrade an existing reentrant read lock,
        // but more than one reader is active, the upgrade must wait until only one reader is left
        private int WaitingForUpgrade => lazy.Obj.waitingForUpgrade;

        // Creates a prepared task if not already available and returns it
        private Task<LockHandle> GetWriteLockTask(FeatureLock parent)
        {
            if (writeLockTask == null) Interlocked.CompareExchange(ref writeLockTask, Task.FromResult(new LockHandle(parent, LockMode.WriteLock)), null);
            return writeLockTask;
        }

        // Creates a prepared task if not already available and returns it
        private Task<LockAttempt> GetFailedAttemptTask()
        {
            if (failedAttemptTask == null) Interlocked.CompareExchange(ref failedAttemptTask, Task.FromResult(new LockAttempt(new LockHandle())), null);
            return failedAttemptTask;
        }

        // Creates a prepared task if not already available and returns it
        private Task<LockAttempt> GetWriteLockAttemptTask(FeatureLock parent)
        {
            if (writeLockAttemptTask == null) Interlocked.CompareExchange(ref writeLockAttemptTask, Task.FromResult(new LockAttempt(new LockHandle(parent, LockMode.WriteLock))), null);
            return writeLockAttemptTask;
        }

        // Access to the prepared tasks (most are in the lazyVariable class)
        private Task<LockHandle> ReadLockTask => lazy.Obj.GetReadLockTask(this);
        private Task<LockHandle> ReenterableReadLockTask => lazy.Obj.GetReenterableReadLockTask(this);
        private Task<LockHandle> WriteLockTask => GetWriteLockTask(this);
        private Task<LockHandle> ReenterableWriteLockTask => lazy.Obj.GetReenterableWriteLockTask(this);
        private Task<LockHandle> UpgradedLockTask => lazy.Obj.GetUpgradedLockTask(this);
        private Task<LockHandle> ReenteredLockTask => lazy.Obj.GetReenteredLockTask(this);
        private Task<LockAttempt> FailedAttemptTask => GetFailedAttemptTask();
        private Task<LockAttempt> ReadLockAttemptTask => lazy.Obj.GetReadLockAttemptTask(this);
        private Task<LockAttempt> ReenterableReadLockAttemptTask => lazy.Obj.GetReenterableReadLockAttemptTask(this);
        private Task<LockAttempt> WriteLockAttemptTask => GetWriteLockAttemptTask(this);
        private Task<LockAttempt> ReenterableWriteLockAttemptTask => lazy.Obj.GetReenterableWriteLockAttemptTask(this);
        private Task<LockAttempt> UpgradedLockAttemptTask => lazy.Obj.GetUpgradedLockAttemptTask(this);
        private Task<LockAttempt> ReenteredLockAttemptTask => lazy.Obj.GetReenteredLockAttemptTask(this);
        #endregion LocalVariableAccess

        #region PublicProperties

        /// <summary>
        /// True when the lock is currently taken
        /// </summary>
        public bool IsLocked => lockIndicator != NO_LOCK;

        /// <summary>True if the lock is currently exclusively taken for writing</summary>
        public bool IsWriteLocked => lockIndicator == WRITE_LOCK;

        /// <summary>True if the lock is currently taken for shared reading</summary>
        public bool IsReadOnlyLocked => lockIndicator >= FIRST_READ_LOCK;

        /// <summary>In case of read-lock, it indicates the number of parallel readers</summary>
        public int CountParallelReadLocks => IsReadOnlyLocked ? lockIndicator : 0;

        /// <summary>True if a reentrant write lock attempt is waiting for upgrading a former reentrant read lock, but other readlocks are in place</summary>
        public bool IsWriteLockWaitingForUpgrade => WaitingForUpgrade == TRUE;

        /// <summary>Lock was already taken reentrantly in the same context</summary>
        public bool HasValidReentrancyContext => ReentrancyIndicatorExists && ReentrancyId == ReentrancyIndicator;

        /// <summary>True if anyone is waiting to acquire the already acquired lock (Is not guaranteed to be accurate in every case)</summary>
        public bool IsAnyWaiting => firstRankTicket != 0 || IsScheduleActive;

        #endregion PublicProperties

        #region ReentrancyContext

        /// <summary>
        /// Only for reentrant locking: 
        /// When an async call is executed within an acquired lock, but not awaited within it,
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
            await asyncCall().ConfiguredAwait();
        }

        /// <summary>
        /// Only for reentrant locking: 
        /// When an async call is executed within an acquired lock, but not awaited within it,
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
            return await asyncCall().ConfiguredAwait();
        }

        /// <summary>
        /// Only for reentrant locking: 
        /// When a new task is executed within an acquired lock, but not awaited within it,
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
            await Task.Run(syncCall).ConfiguredAwait();
        }

        /// <summary>
        /// Only for reentrant locking: 
        /// When a new task or an async call is executed within an acquired lock, but not awaited within it,
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
            if (ReentrancyIndicatorExists) ReentrancyIndicator = 0;
        }

        #endregion ReentrancyContext

        #region HelperMethods

        // Used to check if a candidate must go to the waiting cycle, before trying to acquire the lock.
        // Prioritized candidates may always try for queue jumping, others only if configured so and no prioritized candidate is waiting.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustGoToWaiting(bool prioritized)
        {
            return !prioritized && (prioritizedWaiting || IsScheduleActive || (Settings.restrictQueueJumping && firstRankTicket != 0));
        }

        // Invalidates the current reentrancyIndicator by changing the reentrancy ID.
        // That allows to avoid changing the AsyncLocal reentrancyIndicator itself, which would be expensive.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RenewReentrancyId()
        {
            var obj = lazy.Obj;
            if (++obj.reentrancyId == 0) ++obj.reentrancyId;
        }

        // Yielding a task is very expensive, but must be done to avoid blocking the thread pool threads.
        // This method checks if the task should be yielded or the thread can still be blocked.
        private bool MustYieldAsyncThread(int rank, bool prioritized, int counter)
        {
            // If a special synchronization context is set (e.g. UI-Thread) the task must always be yielded to avoid blocking
            if (SynchronizationContext.Current != null) return true;

            // Otherwise we only yield every now and then, less often for prioritized and lower ranks that should be preferred
            int asyncYieldFrequency;
            if (prioritized || rank == 0) asyncYieldFrequency = Settings.asyncYieldBaseFrequency;
            else asyncYieldFrequency = Settings.asyncYieldBaseFrequency / rank;
            if (counter % asyncYieldFrequency.ClampLow(1) != 0) return false;

            // If it is time to yield, we might still be able to skip yielding if the thread pool is barely used.
            ThreadPool.GetAvailableThreads(out int availableThreads, out _);
            ThreadPool.GetMaxThreads(out int maxThreads, out _);
            ThreadPool.GetMinThreads(out int minThreads, out _);
            var usedThreads = maxThreads - availableThreads;

            // If all minimum available thread pool threads are used, we must yield the task
            return usedThreads >= minThreads;
        }


        // Creates the a new ticket for a new waiting candidate.
        // It will ensure to never provide the 0, because it is reserved to indicate when the firstRankTicket is undefined.
        // NOTE: GetTicket() might be called concurrently and we don't use Interlocked, so it can happen that a ticket is provided multiple times,
        // but that is acceptable and will not cause any trouble.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetTicket()
        {
            var ticket = nextTicket++;
            if (ticket == 0) ticket = nextTicket++;
            return ticket;
        }

        // Checks if the given ticket might be the first rank ticket.
        // Returns the rank of the ticket, which is the distance from the first rank ticket to the given ticket.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort UpdateRank(ushort ticket)
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
                    return (ushort)(ticket - first);
                }
                else
                {
                    // covers the wrap around case
                    return (ushort)(((int)ticket + ushort.MaxValue) - first);
                }
            }
        }

        // Checks if the one ticket is older than the other one
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTicketOlder(ushort ticket, ushort otherTicket)
        {
            if (ticket < otherTicket)
            {
                // handle the wrap-around case
                if ((otherTicket - ticket) < (ushort.MaxValue / 2)) return true;
            }
            else if (ticket > otherTicket)
            {
                // handle the wrap-around case
                if ((ticket - otherTicket) > (ushort.MaxValue / 2)) return true;
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
            return lockIndicator == NO_LOCK && NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK);
        }

        // Actually tries to acquire the lock for reading.
        // Uses Interlocked.CompareExchange to ensure that only one candidate can change the lockIndicator at a time.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryAcquireForReading()
        {
            // It is allowed for multiple candidates to acquire the lock for readOnly in parallel.
            // The number of candidates that acquired under readOnly restriction is counted in the lockIndicator, starting with FIRST_READ_LOCK,
            // so we take the current lockIndicator and increase it by 1 and check if it would be a proper readOnly-value (newLockIndicator >= FIRST_READ_LOCK)
            // If it is the case we try to actually set the new value to the lockIndicator variable
            int currentLockIndicator = lockIndicator;
            int newLockIndicator = currentLockIndicator + 1;
            return newLockIndicator >= FIRST_READ_LOCK && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator);
        }

        // Must be called after waiting, either when the lock is acquired or when timed out
        // Will reset values where necessary and calculate the average wait count
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FinishWaiting(bool prioritized, bool acquired, int rank)
        {
            if (rank == 0) firstRankTicket = 0;
            if (prioritized) prioritizedWaiting = false;
            if (!acquired) return;

            var averageWeighting = Settings.averageWeighting;
            averageWaitCount = (ushort)((((uint)averageWaitCount * averageWeighting) + waitCounter) / (averageWeighting + 1));
            // Reset the waitCounter to 1 (not 0), so that averageWaitCount will never be 0, which would never allow PassiveWaiting and Sleeping, even with a very high rank
            waitCounter = 1;
        }

        // Increases the waitCounter while avoiding a possible overflow
        private void IncWaitCounter(ushort increment = 1)
        {            
            if (ushort.MaxValue - increment <= waitCounter) waitCounter = ushort.MaxValue;
            else waitCounter += increment;
        }

        // Checks if a candidate must still remain in the waiting queue or if acquiring would be possible
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustStillWait(bool prioritized, bool readOnly)
        {
            return (!readOnly && lockIndicator != NO_LOCK) || (readOnly && lockIndicator == WRITE_LOCK) || (!prioritized && prioritizedWaiting);
        }

        // Checks if the candidate may acquire the lock or must stay back and wait based on its rank
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustWaitPassive(bool prioritized, int rank, bool readOnly)
        {
            if (prioritized) return false;
            if (readOnly && IsReadOnlyLocked) return false;
            //if (IsLocked) rank++;
            if (rank * averageWaitCount <= Settings.passiveWaitThreshold) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MustWaitAsyncPassive(bool prioritized, int rank, bool readOnly)
        {
            if (prioritized) return false;
            if (readOnly && IsReadOnlyLocked) return false;
            //if (IsLocked) rank++;
            if (rank * averageWaitCount <= Settings.passiveWaitThresholdAsync) return false;
            return true;
        }

        // Checks if the candidate must go to sleep based on its rank
        private bool MustWaitSleeping(int rank, bool readOnly)
        {            
            if (readOnly && IsReadOnlyLocked) return false;            
            if (sleepLock.IsLocked) return false;
            //if (IsLocked) rank++;
            return rank * averageWaitCount > Settings.sleepWaitThreshold;
        }

        // Checks if the candidate must go to sleep based on its rank
        private bool MustWaitAsyncSleeping(int rank, bool readOnly)
        {
            if (readOnly && IsReadOnlyLocked) return false;
            if (sleepLock.IsLocked) return false;
            //if (IsLocked) rank++;
            return rank * averageWaitCount > Settings.sleepWaitThresholdAsync;
        }

        // Checks if a sleeping candidate may be waked up
        private bool MustAwake(int rank, bool readOnly)
        {
            if (readOnly && IsReadOnlyLocked) return true;
            //if (IsLocked) rank++;
            uint waitFactor = Math.Max(averageWaitCount, waitCounter);
            return rank * waitFactor <= Settings.awakeThreshold;
        }

        // Yields CPU time to wait synchronously
        private void YieldCpuTime(bool lowerPriority)
        {
            if (!lowerPriority) Thread.Yield();
            else
            {
                var currentThread = Thread.CurrentThread;
                var oldPriority = currentThread.Priority;
                currentThread.Priority = ThreadPriority.BelowNormal;
                Thread.Yield();
                currentThread.Priority = oldPriority;
            }
        }

        // Yields thread to wait asynchronously
        private async Task YieldThreadAsync()
        {
            if (Settings.sleepForAsyncYieldInSyncContext && SynchronizationContext.Current != null) await Task.Delay(1).ConfiguredAwait();
            else await Task.Yield();
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
            var ticket = GetTicket();
            int rank;
            bool skip;
            do
            {
                rank = UpdateRank(ticket);
                if (MustWaitPassive(prioritized, rank, readOnly))
                {
                    skip = true;
                    if (MustWaitSleeping(rank, readOnly)) Sleep(ticket, readOnly);
                    else YieldCpuTime(true);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) IncWaitCounter();
                        YieldCpuTime(false);
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
            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting()) return WriteLockTask;
            else return LockAsync_Wait(LockMode.WriteLock, prioritized, false);
        }

        private Task<LockHandle> LockAsync_Wait(LockMode mode, bool prioritized, bool readOnly)
        {
            if (prioritized) prioritizedWaiting = true;
            var ticket = GetTicket();
            int rank;
            int counter = 0;
            bool skip;
            do
            {
                rank = UpdateRank(ticket);
                if (MustWaitAsyncPassive(prioritized, rank, readOnly))
                {
                    skip = true;
                    if (MustWaitAsyncSleeping(rank, readOnly)) return LockAsync_Wait_ContinueWithAwaiting(mode, ticket, prioritized, readOnly, counter, false);
                    else if (MustYieldAsyncThread(rank, prioritized, ++counter)) return LockAsync_Wait_ContinueWithAwaiting(mode, ticket, prioritized, readOnly, counter, true);
                    else YieldCpuTime(true);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) IncWaitCounter();
                        if (MustYieldAsyncThread(rank, prioritized, ++counter)) return LockAsync_Wait_ContinueWithAwaiting(mode, ticket, prioritized, readOnly, counter, true);
                        YieldCpuTime(false);
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
                    ReentrancyIndicator = ReentrancyId;
                    reentrancyActive = true;
                    return ReenterableReadLockTask;
                }
                else return ReadLockTask;
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable)
                {
                    ReentrancyIndicator = ReentrancyId;
                    reentrancyActive = true;
                    return ReenterableWriteLockTask;
                }
                else return WriteLockTask;
            }
        }

        private async Task<LockHandle> LockAsync_Wait_ContinueWithAwaiting(LockMode mode, ushort ticket, bool prioritized, bool readOnly, int counter, bool yieldNow)
        {
            if (yieldNow) await YieldThreadAsync().ConfiguredAwait();
            int rank;
            bool skip;
            do
            {
                rank = UpdateRank(ticket);
                if (MustWaitAsyncPassive(prioritized, rank, readOnly))
                {
                    skip = true;
                    if (MustWaitAsyncSleeping(rank, readOnly)) await SleepAsync(ticket, readOnly).ConfiguredAwait();
                    else if (MustYieldAsyncThread(rank, prioritized, ++counter)) await YieldThreadAsync().ConfiguredAwait();
                    else YieldCpuTime(true);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) IncWaitCounter();
                        if (MustYieldAsyncThread(rank, prioritized, ++counter)) await YieldThreadAsync().ConfiguredAwait();
                        else YieldCpuTime(false);
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
                    ReentrancyIndicator = ReentrancyId;
                    reentrancyActive = true;
                }
                return new LockHandle(this, mode);
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable)
                {
                    ReentrancyIndicator = ReentrancyId;
                    reentrancyActive = true;
                }
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
            if (!MustGoToWaiting(prioritized) && TryAcquireForReading()) return ReadLockTask;
            else return LockAsync_Wait(LockMode.ReadLock, prioritized, true);
        }

        #endregion LockReadOnlyAsync

        #region LockReentrant

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LockHandle LockReentrant(bool prioritized = false)
        {
            if (TryReenter(out LockHandle acquiredLock, true, out _)) return acquiredLock;
            if (MustGoToWaiting(prioritized) || !TryAcquireForWriting()) Lock_Wait(prioritized, false);
            ReentrancyIndicator = ReentrancyId;
            reentrancyActive = true;
            return new LockHandle(this, LockMode.WriteLockReenterable);
        }

        private bool TryReenter(out LockHandle acquiredLock, bool waitForUpgrade, out bool upgradePossible)
        {
            var currentLockIndicator = lockIndicator;
            if (reentrancyActive && currentLockIndicator != NO_LOCK && HasValidReentrancyContext)
            {
                if (currentLockIndicator == WRITE_LOCK)
                {
                    upgradePossible = false;
                    acquiredLock = new LockHandle(this, LockMode.Reentered);
                    return true;
                }
                else if (currentLockIndicator >= FIRST_READ_LOCK)
                {
                    var lazyObj = lazy.Obj;

                    // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
                    if (TRUE == Interlocked.CompareExchange(ref lazyObj.waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
                    // Waiting for upgrade to writeLock
                    while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
                    {
                        if (waitForUpgrade)
                        {
                            prioritizedWaiting = true;
                            YieldCpuTime(false);
                        }
                        else
                        {
                            lazyObj.waitingForUpgrade = FALSE;
                            upgradePossible = true;
                            acquiredLock = new LockHandle();
                            return false;
                        }
                    }
                    lazyObj.waitingForUpgrade = FALSE;
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
                if (acquiredLock.Mode == LockMode.Reentered) return ReenteredLockTask;
                else return UpgradedLockTask;
            }
            else if (upgradePossible) return WaitForUpgradeAsync();

            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting())
            {
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
                return ReenterableWriteLockTask;
            }
            else return LockAsync_Wait(LockMode.WriteLockReenterable, prioritized, false);
        }

        private async Task<LockHandle> WaitForUpgradeAsync()
        {
            var lazyObj = lazy.Obj;
            int counter = 0;
            // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
            if (TRUE == Interlocked.CompareExchange(ref lazyObj.waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
            // Waiting for upgrade to writeLock
            while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
            {
                prioritizedWaiting = true;
                if (MustYieldAsyncThread_ForReentranceUpgrade(++counter)) await YieldThreadAsync().ConfiguredAwait();
                else YieldCpuTime(false);
            }
            lazyObj.waitingForUpgrade = FALSE;
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

            ReentrancyIndicator = ReentrancyId;
            reentrancyActive = true;
            return new LockHandle(this, LockMode.ReadLockReenterable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReenterReadOnly()
        {
            return reentrancyActive && lockIndicator != NO_LOCK && HasValidReentrancyContext;
        }

        #endregion LockReentrantReadOnly

        #region LockReentrantReadOnlyAsync

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockHandle> LockReentrantReadOnlyAsync(bool prioritized = false)
        {
            if (TryReenterReadOnly()) return ReenteredLockTask;

            if (!MustGoToWaiting(prioritized) && TryAcquireForReading())
            {
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
                return ReenterableReadLockTask;
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
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
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
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
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
            TimeFrame timer = new(timeout);

            if (prioritized) prioritizedWaiting = true;
            var ticket = GetTicket();
            int rank;
            bool skip;
            do
            {
                rank = UpdateRank(ticket);
                if (timer.Elapsed())
                {
                    FinishWaiting(prioritized, false, rank);
                    return false;
                }
                if (MustWaitPassive(prioritized, rank, readOnly))
                {
                    skip = true;
                    if (MustWaitSleeping(rank, readOnly)) TrySleep(timer, ticket, readOnly);
                    else YieldCpuTime(true);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) IncWaitCounter();
                        YieldCpuTime(false);
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
            if (!MustGoToWaiting(prioritized) && TryAcquireForWriting()) return WriteLockAttemptTask;
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.WriteLock, timeout, prioritized, false);
            else return FailedAttemptTask;
        }

        private Task<LockAttempt> TryLockAsync_Wait(LockMode mode, TimeSpan timeout, bool prioritized, bool readOnly)
        {
            TimeFrame timer = new(timeout);

            if (prioritized) prioritizedWaiting = true;
            var ticket = GetTicket();
            int rank;
            bool skip;
            int counter = 0;
            do
            {
                rank = UpdateRank(ticket);
                if (timer.Elapsed())
                {
                    FinishWaiting(prioritized, false, rank);
                    return FailedAttemptTask;
                }

                if (MustWaitAsyncPassive(prioritized, rank, readOnly))
                {
                    skip = true;
                    if (MustWaitAsyncSleeping(rank, readOnly)) return TryLockAsync_Wait_ContinueWithAwaiting(mode, timer, ticket, prioritized, readOnly, counter, false);
                    if (MustYieldAsyncThread(rank, prioritized, counter++)) return TryLockAsync_Wait_ContinueWithAwaiting(mode, timer, ticket, prioritized, readOnly, counter, true);
                    else YieldCpuTime(true);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) IncWaitCounter();
                        if (MustYieldAsyncThread(rank, prioritized, counter++)) return TryLockAsync_Wait_ContinueWithAwaiting(mode, timer, ticket, prioritized, readOnly, counter, true);
                        YieldCpuTime(false);
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
                    ReentrancyIndicator = ReentrancyId;
                    reentrancyActive = true;
                    return ReenterableReadLockAttemptTask;
                }
                else return ReadLockAttemptTask;
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable)
                {
                    ReentrancyIndicator = ReentrancyId;
                    reentrancyActive = true;
                    return ReenterableWriteLockAttemptTask;
                }
                else return WriteLockAttemptTask;
            }
        }

        private async Task<LockAttempt> TryLockAsync_Wait_ContinueWithAwaiting(LockMode mode, TimeFrame timer, ushort ticket, bool prioritized, bool readOnly, int counter, bool yieldNow)
        {
            if (yieldNow) await YieldThreadAsync().ConfiguredAwait();
            int rank;
            bool skip;
            do
            {
                rank = UpdateRank(ticket);
                if (timer.Elapsed())
                {
                    FinishWaiting(prioritized, false, rank);
                    return new LockAttempt(new LockHandle());
                }

                if (MustWaitAsyncPassive(prioritized, rank, readOnly))
                {
                    skip = true;
                    if (MustWaitAsyncSleeping(rank, readOnly)) await TrySleepAsync(timer, ticket, readOnly).ConfiguredAwait();
                    else if (MustYieldAsyncThread(rank, prioritized, counter++)) await YieldThreadAsync().ConfiguredAwait();
                    else YieldCpuTime(true);
                }
                else
                {
                    skip = false;
                    if (prioritized) prioritizedWaiting = true;
                    if (MustStillWait(prioritized, readOnly))
                    {
                        if (rank == 0) IncWaitCounter();
                        if (MustYieldAsyncThread(rank, prioritized, counter++)) await YieldThreadAsync().ConfiguredAwait();
                        else YieldCpuTime(false);
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
                    ReentrancyIndicator = ReentrancyId;
                    reentrancyActive = true;
                }
                return new LockAttempt(new LockHandle(this, mode));
            }
            else
            {
                if (mode == LockMode.WriteLockReenterable)
                {
                    ReentrancyIndicator = ReentrancyId;
                    reentrancyActive = true;
                }
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
            if (!MustGoToWaiting(prioritized) && TryAcquireForReading()) return ReadLockAttemptTask;
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.ReadLock, timeout, prioritized, true);
            else return FailedAttemptTask;
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
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
                acquiredLock = new LockHandle(this, LockMode.WriteLockReenterable);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, false))
            {
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
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
            var lazyObj = lazy.Obj;
            TimeFrame timer = new(timeout);
            // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
            if (TRUE == Interlocked.CompareExchange(ref lazyObj.waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");

            // Waiting for upgrade to writeLock
            while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
            {
                if (timer.Elapsed())
                {
                    lazyObj.waitingForUpgrade = FALSE;
                    return false;
                }
                YieldCpuTime(false);
            }
            lazyObj.waitingForUpgrade = FALSE;
            return true;
        }

        #endregion TryLockReentrant_Timeout

        #region TryLockReentrantAsync_Timeout

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<LockAttempt> TryLockReentrantAsync(TimeSpan timeout, bool prioritized = false)
        {
            if (TryReenter(out LockHandle acquiredLock, false, out bool upgradePossible))
            {
                if (acquiredLock.Mode == LockMode.Reentered) return ReenteredLockAttemptTask;
                else return UpgradedLockAttemptTask;
            }
            else if (upgradePossible)
            {
                if (timeout > TimeSpan.Zero) return WaitForUpgradeAttemptAsync(timeout);
                else return FailedAttemptTask;
            }

            if (!MustGoToWaiting(prioritized) && NO_LOCK == Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
                return ReenterableWriteLockAttemptTask;
            }
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.WriteLockReenterable, timeout, prioritized, false);
            else return FailedAttemptTask;
        }

        private async Task<LockAttempt> WaitForUpgradeAttemptAsync(TimeSpan timeout)
        {
            var lazyObj = lazy.Obj;
            TimeFrame timer = new(timeout);
            // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
            if (TRUE == Interlocked.CompareExchange(ref lazyObj.waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");

            int counter = 0;
            // Waiting for upgrade to writeLock
            while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
            {
                if (timer.Elapsed())
                {
                    lazyObj.waitingForUpgrade = FALSE;
                    return new LockAttempt(new LockHandle());
                }

                if (MustYieldAsyncThread_ForReentranceUpgrade(++counter)) await YieldThreadAsync().ConfiguredAwait();
                else YieldCpuTime(false);
            }
            lazyObj.waitingForUpgrade = FALSE;
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
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
                acquiredLock = new LockHandle(this, LockMode.ReadLockReenterable);
                return true;
            }
            else if (timeout > TimeSpan.Zero && TryLock_Wait(timeout, prioritized, true))
            {
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
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
            if (TryReenterReadOnly()) return ReenteredLockAttemptTask;

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            if (!MustGoToWaiting(false) && newLockIndicator >= FIRST_READ_LOCK && currentLockIndicator == Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                ReentrancyIndicator = ReentrancyId;
                reentrancyActive = true;
                return ReenterableReadLockAttemptTask;
            }
            else if (timeout > TimeSpan.Zero) return TryLockAsync_Wait(LockMode.ReadLockReenterable, timeout, prioritized, true);
            else return FailedAttemptTask;
        }

        #endregion TryLockReentrantReadOnlyAsync_Timeout

        #region Exit

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            Interlocked.Decrement(ref lockIndicator);
            if (IsScheduleActive) Service<SchedulerService>.Instance.InterruptWaiting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock()
        {
            lockIndicator = NO_LOCK;
            if (IsScheduleActive) Service<SchedulerService>.Instance.InterruptWaiting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReentrantWriteLock()
        {
            // We simply change the reentrancyId and keep the ReentrancyContext as it is which also invalidates it. That is a lot cheaper.
            RenewReentrancyId();
            reentrancyActive = false;
            lockIndicator = NO_LOCK;
            if (IsScheduleActive) Service<SchedulerService>.Instance.InterruptWaiting();
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void ExitReentrantReadLock()
        {
            // We must clear the ReentrancyContext, because the lock might be used by multiple reader, so we can't change the current reentrancyId, though it would be cheaper.
            RemoveReentrancyContext();            
            reentrancyActive = Interlocked.Decrement(ref lockIndicator) > 0;
            if (IsScheduleActive) Service<SchedulerService>.Instance.InterruptWaiting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReenteredLock()
        {
            // Nothing to do!
            if (IsScheduleActive) Service<SchedulerService>.Instance.InterruptWaiting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DowngradeReentering()
        {
            lockIndicator = FIRST_READ_LOCK;
            if (IsScheduleActive) Service<SchedulerService>.Instance.InterruptWaiting();
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
            else if (lockIndicator == WRITE_LOCK) return true;
            else return false;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private void Upgrade(ref LockMode currentLockMode)
        {
            if (TryUpgrade(ref currentLockMode)) return;

            if (currentLockMode == LockMode.ReadLock)
            {
                ExitReadLock();
                Lock(true);
                currentLockMode = LockMode.WriteLock;
            }
            else if (currentLockMode == LockMode.ReadLockReenterable)
            {
                ExitReentrantReadLock();
                LockReentrant(true);
                currentLockMode = LockMode.WriteLockReenterable;
            }
            else if (currentLockMode == LockMode.Reentered && lockIndicator >= FIRST_READ_LOCK)
            {
                LockReentrant(true);
                currentLockMode = LockMode.Upgraded;
            }
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
            else if (lockIndicator >= FIRST_READ_LOCK) return true;
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
            private LockMode mode;

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

            public void UpgradeToWriteMode()
            {
                parentLock?.Upgrade(ref mode);
            }

            public bool TryDowngradeToReadOnlyMode()
            {
                return parentLock?.TryDowngrade(ref mode) ?? false;
            }

            public bool IsActive => parentLock != null;

            internal LockMode Mode => mode;
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

        #region Scheduling        

        string ISchedule.Name => "FeatureLock";
        ScheduleStatus ISchedule.Trigger(DateTime now)
        {            
            if (!IsScheduleActive) return TimeFrame.Invalid;           
            var candidate = queueHead;

            ScheduleStatus scheduleStatus = new ScheduleStatus(1.Milliseconds(), (0.01 * Settings.schedulerDelayFactor).Milliseconds());

            if (!IsReadOnlyLocked && !MustAwake(UpdateRank(candidate.ticket), candidate.isReadOnly)) return scheduleStatus;

            SleepHandle firstAwaking = null;
            SleepHandle lastAwaking = null;                                   

            sleepLock.Enter(true);
            try
            {
                while (candidate != null)
                {
                    bool awake = false;
                    var rank = UpdateRank(candidate.ticket);
                    if (MustAwake(rank, candidate.isReadOnly))
                    {
                        RemoveSleepHandle(candidate);
                        awake = true;
                    }

                    if (awake)
                    {
                        if (firstAwaking == null) firstAwaking = candidate;
                        if (lastAwaking != null) lastAwaking.Next = candidate;
                        lastAwaking = candidate;
                    }

                    if (awake || IsReadOnlyLocked) candidate = candidate.Next;
                    else candidate = null;

                    if (lastAwaking != null) lastAwaking.Next = null;
                }
            }
            finally
            {
                sleepLock.Exit();
            }

            candidate = firstAwaking;
            while (candidate != null)
            {
                candidate.WakeUp();
                candidate = candidate.Next;
            }

            if (IsScheduleActive) return scheduleStatus;
            else return ScheduleStatus.Terminated;
        }

        void Sleep(ushort ticket, bool readOnly)
        {
            var sleepHandle = new SleepHandle(ticket, false, readOnly);
            AddSleepHandle(sleepHandle);
            lock (sleepHandle)
            {
                if (sleepHandle.sleeping) Monitor.Wait(sleepHandle);
            }
        }

        async Task SleepAsync(ushort ticket, bool readOnly)
        {
            var sleepHandle = new SleepHandle(ticket, true, readOnly);
            AddSleepHandle(sleepHandle);

            if (sleepHandle.sleeping) await sleepHandle.tcs.Task.ConfiguredAwait();
        }

        bool TrySleep(TimeFrame timer, ushort ticket, bool readOnly)
        {
            var remaining = timer.Remaining();
            if (remaining < TimeSpan.Zero) return true;

            var sleepHandle = new SleepHandle(ticket, false, readOnly);
            AddSleepHandle(sleepHandle);
            bool elapsed = false;
            lock (sleepHandle)
            {
                if (sleepHandle.sleeping) elapsed = !Monitor.Wait(sleepHandle, remaining);
                sleepHandle.sleeping = false;
            }

            return elapsed;
        }

        async Task<bool> TrySleepAsync(TimeFrame timer, ushort ticket, bool readOnly)
        {
            var remaining = timer.Remaining();
            if (remaining < TimeSpan.Zero) return true;

            var sleepHandle = new SleepHandle(ticket, true, readOnly);
            AddSleepHandle(sleepHandle);
            bool elapsed = false;
            if (sleepHandle.sleeping) elapsed = !await sleepHandle.tcs.Task.TryWaitAsync(remaining).ConfiguredAwait();
            sleepHandle.sleeping = false;

            return elapsed;
        }

        bool RemoveSleepHandle(SleepHandle searchedOne)
        {
            if (queueHead == null) return false;

            SleepHandle prev = null;
            SleepHandle sleepHandle = queueHead;
            bool found = false;
            do
            {
                if (sleepHandle == searchedOne || !sleepHandle.sleeping)
                {
                    if (queueHead == sleepHandle) queueHead = sleepHandle.Next;
                    else prev.Next = sleepHandle.Next;

                    if (sleepHandle == searchedOne) found = true;
                }
                else prev = sleepHandle;
                sleepHandle = sleepHandle.Next;
            }
            while (!found && sleepHandle != null);

            return found;
        }

        void AddSleepHandle(SleepHandle sleepHandle)
        {
            bool activateSchedule = false;
            sleepLock.Enter();
            try
            {
                if (queueHead == null)
                {
                    queueHead = sleepHandle;
                    sleepHandle.Next = null;
                    activateSchedule = true;
                }
                else
                {
                    if (IsTicketOlder(sleepHandle.ticket, queueHead.ticket))
                    {
                        sleepHandle.Next = queueHead;
                        queueHead = sleepHandle;
                    }
                    else
                    {
                        var node = queueHead;
                        while (node.Next != null && IsTicketOlder(node.Next.ticket, sleepHandle.ticket)) node = node.Next;
                        sleepHandle.Next = node.Next;
                        node.Next = sleepHandle;
                    }
                }
            }
            finally
            {
                sleepLock.Exit();
                if (activateSchedule) Service<SchedulerService>.Instance.AddSchedule(this);
            }
        }

        public class SleepHandle
        {
            internal SleepHandle(ushort ticket, bool sleepsAsync, bool readOnly)
            {
                this.isReadOnly = readOnly;
                this.ticket = ticket;
                if (sleepsAsync) tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            internal TaskCompletionSource<bool> tcs = null;
            internal readonly ushort ticket = 0;
            internal readonly bool isReadOnly = false;
            volatile internal SleepHandle Next;
            volatile internal bool sleeping = true;

            [MethodImpl(MethodImplOptions.NoOptimization)]
            internal void WakeUp()
            {
                sleeping = false;

                if (tcs == null)
                {
                    lock (this)
                    {
                        Monitor.PulseAll(this);
                    }
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            }
        }

        #endregion Scheduling
    }
}