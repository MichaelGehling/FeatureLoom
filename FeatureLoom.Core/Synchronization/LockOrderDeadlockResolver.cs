using FeatureLoom.Synchronization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.Synchronization
{
    public static class LockOrderDeadlockResolver
    {
        // Maps the lockObject to the thread that it holds it.
        static Dictionary<object, Thread> holdingThreads = new Dictionary<object, Thread>();
        static MicroValueLock holdingThreadsLock = new MicroValueLock();

        // ConditionalWeakTable is used to ensure that the thread is not kept alive after it finished.
        // The alternative would be to remove it from the table each time it does not hold any lock and add it again when it enters a lock,
        // but that would be quite costly.
        static ConditionalWeakTable<Thread, ThreadLockInfo> threadLockInfos = new ConditionalWeakTable<Thread, ThreadLockInfo>();

        [ThreadStatic]
        static ThreadLockInfo myThreadLockInfo;

        private class ThreadLockInfo
        {            
            public List<object> heldLocks = new List<object>();
            public object waitingForLock = null;

            static public ThreadLockInfo Create(Thread thread) => new ThreadLockInfo();
        }

        private static bool IsDeadlock(object lockObject, List<object> heldLocks)
        {
            if (heldLocks.Count == 0) return false;

            if (holdingThreads.TryGetValue(lockObject, out Thread holdingThread))
            {
                if (threadLockInfos.TryGetValue(holdingThread, out ThreadLockInfo holdingThreadInfo) &&
                    holdingThreadInfo.waitingForLock != null)
                {
                    if (heldLocks.Contains(holdingThreadInfo.waitingForLock))
                    {
                        // Current Thread A holds a lock that Thread B waits for. But Thread B holds the lock that Thread A wants to get right now. That is a deadlock.
                        return true;
                    }
                    else
                    {
                        // Thread B holds the lock that Thread A wants to get right now, but it waits for a lock that Thread C holds.
                        // We need to check if Thread C waits for the lock that Thread A holds, because that would be a deadlock, too.
                        // The recursion can detect a deadlock chain of any length.
                        return IsDeadlock(holdingThreadInfo.waitingForLock, heldLocks);
                    }
                }
            }
            return false;
        }        

        #region FeatureLock

        /// <summary>
        /// Enters a FeatureLock and returns a disposable lock handle to be used in a using-statement (like the usual FeatureLock.LockHandle)
        /// If a deadlock (caused by inverse lock order) is detected, the blocked lock is "borrowed" to finally resolve the deadlock.
        /// Usage is the same like normal locking: using(fetureLockObject.LockWithLockOrderDeadlockResolution()){ ... }
        /// Note: Deadlocks can only be detected if all involved threads used LockWithLockOrderDeadlockResolution instead of a simple Lock() or LockAsync() etc.
        /// But beside this, LockWithLockOrderDeadlockResolution works together with Lock() or LockAsync() etc.
        /// </summary>
        /// <param name="featureLock">The lock object that is used</param>
        /// <param name="prioritized">Used to priotirize this lock attempt over not prioritized ones</param>
        /// <returns></returns>
        public static FeatureLockHandle LockWithLockOrderDeadlockResolution(this FeatureLock featureLock, bool prioritized = false)
        {
            Thread thread = Thread.CurrentThread;

            ThreadLockInfo threadLockInfo = myThreadLockInfo;
            if (threadLockInfo == null)
            {
                threadLockInfo = threadLockInfos.GetValue(thread, ThreadLockInfo.Create);
                myThreadLockInfo = threadLockInfo;
            }

            if (featureLock.TryLock(out var lockHandle, prioritized))
            {
                // heldLocks does not have any concurrent access, because only the own thread will access it.
                threadLockInfo.heldLocks.Add(featureLock);
                holdingThreadsLock.Enter();
                holdingThreads[featureLock] = thread;
                holdingThreadsLock.Exit();
            }
            else
            {
                holdingThreadsLock.Enter();
                if (IsDeadlock(featureLock, threadLockInfo.heldLocks))
                {
                    // That would normally be a deadlock. But, we know that the owner of the lock is blocked until the current thread finishes,
                    // so we let it "borrow" the lock from the actual owner, in order to resolve the deadlock.
                    return new FeatureLockHandle(null, new FeatureLock.LockHandle());
                }

                threadLockInfo.waitingForLock = featureLock;
                holdingThreadsLock.Exit();

                lockHandle = featureLock.Lock(prioritized);

                holdingThreadsLock.Enter();
                threadLockInfo.waitingForLock = null;
                threadLockInfo.heldLocks.Add(featureLock);
                holdingThreads[featureLock] = thread;
                holdingThreadsLock.Exit();
            }

            return new FeatureLockHandle(featureLock, lockHandle);
        }

        private static void ReleaseFeatureLock(FeatureLock featureLock, FeatureLock.LockHandle lockHandle)
        {
            myThreadLockInfo?.heldLocks.Remove(featureLock);

            holdingThreadsLock.Enter();
            holdingThreads.Remove(featureLock);
            holdingThreadsLock.Exit();

            lockHandle.Exit();
        }

        public struct FeatureLockHandle : IDisposable
        {
            FeatureLock featureLock;
            FeatureLock.LockHandle lockHandle;

            public FeatureLockHandle(FeatureLock featureLock, FeatureLock.LockHandle lockHandle)
            {
                this.featureLock = featureLock;
                this.lockHandle = lockHandle;
            }

            public void Dispose()
            {
                // If no lockObject was set, the lock was "borrowed", due to a deadlock.
                if (featureLock != null) ReleaseFeatureLock(featureLock, lockHandle);
            }
        }
        #endregion FeatureLock


        #region Monitor
        /// <summary>
        /// Enters a Monitor lock for the passed lockObject and returns a disposable handle to be used in a using-statement.
        /// If a deadlock (caused by inverse lock order) is detected, the blocked lock is "borrowed" to finally resolve the deadlock.
        /// Usage is similar to the lock() statement: using(LockOrderDeadlockResolver.Lock(lockObject)){ ... }
        /// Note: Deadlocks can only be detected if all involved threads used LockOrderDeadlockResolver.Lock() instead of lock() or Monitor.Enter().
        /// But beside this, LockOrderDeadlockResolver.Lock() works together with lock() and Monitor.Enter().
        /// </summary>
        /// <param name="lockObject">The object that is used for the Monitor.Enter(lockObject)</param>
        /// <returns></returns>
        public static MonitorLockHandle Lock(object lockObject)
        {
            Thread thread = Thread.CurrentThread;
            ThreadLockInfo threadLockInfo = myThreadLockInfo;
            if (threadLockInfo == null)
            {
                threadLockInfo = threadLockInfos.GetValue(thread, ThreadLockInfo.Create);
                myThreadLockInfo = threadLockInfo;
            }

            if (Monitor.TryEnter(lockObject))
            {                       
                // heldLocks does not have any concurrent access, because only the own thread will access it.
                threadLockInfo.heldLocks.Add(lockObject);
                holdingThreadsLock.Enter();
                holdingThreads[lockObject] = thread;
                holdingThreadsLock.Exit();
            }
            else
            {
                holdingThreadsLock.Enter();
                if (IsDeadlock(lockObject, threadLockInfo.heldLocks))
                {                    
                    // That would normally be a deadlock. But, we know that the owner of the lock is blocked until the current thread finishes,
                    // so we let it "borrow" the lock from the actual owner, in order to resolve the deadlock.
                    return new MonitorLockHandle(null);
                }

                threadLockInfo.waitingForLock = lockObject;
                holdingThreadsLock.Exit();

                Monitor.Enter(lockObject);

                holdingThreadsLock.Enter();
                threadLockInfo.waitingForLock = null;
                threadLockInfo.heldLocks.Add(lockObject);
                holdingThreads[lockObject] = thread;
                holdingThreadsLock.Exit();
            }

            return new MonitorLockHandle(lockObject);
        }

        private static void ReleaseMonitorLock(object lockObject)
        {
            myThreadLockInfo?.heldLocks.Remove(lockObject);

            holdingThreadsLock.Enter();
            holdingThreads.Remove(lockObject);
            holdingThreadsLock.Exit();

            Monitor.Exit(lockObject);
        }

        // To be used in a using statement to ensure the release of the lock.
        public struct MonitorLockHandle :IDisposable
        {
            object lockObject;

            public MonitorLockHandle(object lockObject)
            {
                this.lockObject = lockObject;
            }

            public void Dispose()
            {
                // If no lockObject was set, the lock was "borrowed", due to a deadlock.
                if (lockObject != null) ReleaseMonitorLock(lockObject);
            }
        }
        #endregion Montor

    }
}
