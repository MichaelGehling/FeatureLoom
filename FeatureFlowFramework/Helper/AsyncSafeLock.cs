using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public class AsyncSafeLock
    {
        /*private static ConditionalWeakTable<object, AsyncSafeLock> lockObjects = new ConditionalWeakTable<object, AsyncSafeLock>();
        public static IDisposable LockForReading<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForReading;
        }

        public static Task<IDisposable> LockForReadingAsync<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForReadingAsync();
        }
        public static IDisposable LockForWriting<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForWriting;
        }
        public static Task<IDisposable> LockForWritingAsync<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForWritingAsync();
        }*/

        const int NO_LOCKID = 0;

        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockId larger than NO_LOCKID (0) implies a write-lock, while a lockId smaller than NO_LOCKID implies a read-lock.
        /// When entering a read-lock, the lockId is decreased and increased when leaving a read-lock.
        /// When entering a write-lock, a positive lockId (greater than NO_LOCK) is set and set back to NO_LOCK when the write-lock is left.
        /// </summary>
        volatile int lockId = NO_LOCKID;

        /// <summary>
        /// Used to generate write lock Ids. To generate a new id, it is incremented. 
        /// When exceeding the maximum value it is reset to NO_LOCKID.
        /// </summary>
        volatile int lockIdCounter = NO_LOCKID;
        //AsyncManualResetEvent waitingEvent = new AsyncManualResetEvent();
        TaskCompletionSource<bool> tcs;

        public IDisposable ForReading()
        {
            var newLockId = 0;
            var currentLockId = 0;
            do
            {
                if(lockId > NO_LOCKID)
                {
                    //waitingEvent.Wait();
                    if(tcs == null) Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), null);
                    tcs?.Task.Wait();
                }
                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            while(currentLockId > NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId));

            //waitingEvent.Reset();
            ResetTcs();

            return new ReadLock(this);
        }

        private void ResetTcs()
        {
            bool ok = true;
            do
            {
                ok = true;
                var currentTCS = tcs;
                if(currentTCS != null && currentTCS.Task.IsCompleted) ok = currentTCS == Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), currentTCS);
            }
            while(!ok);
        }

        public async Task<IDisposable> ForReadingAsync()
        {
            var newLockId = 0;
            var currentLockId = 0;
            do
            {
                if(lockId > NO_LOCKID)
                {
                    //await waitingEvent.WaitingTask;
                    if(tcs == null) Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), null);
                    await tcs?.Task;
                }
                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            while(currentLockId > NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId));

            //waitingEvent.Reset();
            ResetTcs();

            return new ReadLock(this);
        }

        public IDisposable ForWriting()
        {
            var newLockId = Interlocked.Increment(ref lockIdCounter);
            while(newLockId <= NO_LOCKID)
            {
                Interlocked.CompareExchange(ref lockIdCounter, NO_LOCKID, newLockId);
                newLockId = Interlocked.Increment(ref lockIdCounter);
            }

            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if(currentLockId != NO_LOCKID)
                {
                    //waitingEvent.Wait();
                    if(tcs == null) Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), null);
                    tcs?.Task.Wait();
                }
            }
            while(currentLockId != NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID));

            //waitingEvent.Reset();
            ResetTcs();

            return new WriteLock(this);
        }

        public async Task<IDisposable> ForWritingAsync()
        {
            var newLockId = Interlocked.Increment(ref lockIdCounter);
            while(newLockId <= NO_LOCKID)
            {
                Interlocked.CompareExchange(ref lockIdCounter, NO_LOCKID, newLockId);
                newLockId = Interlocked.Increment(ref lockIdCounter);
            }

            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if(currentLockId != NO_LOCKID)
                {
                    //await waitingEvent.WaitingTask;
                    if(tcs == null) Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), null);
                    await tcs?.Task;
                }
            }
            while(currentLockId != NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID));

            //waitingEvent.Reset();
            ResetTcs();

            return new WriteLock(this);
        }

        private struct ReadLock : IDisposable
        {
            AsyncSafeLock safeLock;

            public ReadLock(AsyncSafeLock safeLock)
            {
                this.safeLock = safeLock;
            }

            public void Dispose()
            {
                var newLockId = Interlocked.Increment(ref safeLock.lockId);

                if (NO_LOCKID == newLockId)
                {
                    //safeLock.waitingEvent.Set();
                    safeLock.tcs?.TrySetResult(true);
                }
            }
        }

        private struct WriteLock : IDisposable
        {
            AsyncSafeLock safeLock;

            public WriteLock(AsyncSafeLock safeLock)
            {
                this.safeLock = safeLock;
            }

            public void Dispose()
            {
                safeLock.lockId = NO_LOCKID;
                //safeLock.waitingEvent.Set();
                safeLock.tcs?.TrySetResult(true);
            }
        }
    }
}
