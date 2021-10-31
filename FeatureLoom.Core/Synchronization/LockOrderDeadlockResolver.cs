using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureLoom.Core.Synchronization
{
    public static class LockOrderDeadlockResolver
    {
        // Maps the lockObject to the thread that it holds it.
        static ConcurrentDictionary<object, Thread> holdingThreads = new ConcurrentDictionary<object, Thread>();

        // ConditionalWeakTable is used to ensure that the thread is not kept alive after it finished.
        // The alternative would be to remove it from the table each time it does not hold any lock and add it again when it enters a lock,
        // but that would be quite costly.
        static ConditionalWeakTable<Thread, ThreadLockInfo> threadLockInfos = new ConditionalWeakTable<Thread, ThreadLockInfo>();

        private class ThreadLockInfo
        {            
            public List<object> heldLocks = new List<object>();
            public object waitingForLock = null;
        }

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
            ThreadLockInfo threadLockInfo = threadLockInfos.GetValue(thread, _ => new ThreadLockInfo() );            

            if (Monitor.TryEnter(lockObject))
            {                       
                // heldLocks does not have any concurrent access, because only the own thread will access it.
                threadLockInfo.heldLocks.Add(lockObject);
                holdingThreads[lockObject] = thread;
            }
            else
            {                
                if (isDeadlock(lockObject, threadLockInfo.heldLocks))
                {                    
                    // That would normally be a deadlock. But, we know that the owner of the lock is blocked until the current thread finishes,
                    // so we let it "borrow" the lock from the actual owner, in order to resolve the deadlock.
                    return new MonitorLockHandle(null);
                }

                threadLockInfo.waitingForLock = lockObject;

                Monitor.Enter(lockObject);

                threadLockInfo.waitingForLock = null;
                threadLockInfo.heldLocks.Add(lockObject);
                holdingThreads[lockObject] = thread;
            }

            return new MonitorLockHandle(lockObject);
        }

        private static bool isDeadlock(object lockObject, List<object> heldLocks)
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
                        return isDeadlock(holdingThreadInfo.waitingForLock, heldLocks);
                    }
                }
            }
            return false;
        }

        private static void ReleaseLock(object lockObject)
        {
            Thread thread = Thread.CurrentThread;
            if (threadLockInfos.TryGetValue(thread, out var threadLockInfo))
            {
                threadLockInfo.heldLocks.Remove(lockObject);
            }
            holdingThreads.TryRemove(lockObject, out _);
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
                if (lockObject != null) ReleaseLock(lockObject);
            }
        }
    }
}
