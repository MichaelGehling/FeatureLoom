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
        private static ConditionalWeakTable<object, AsyncSafeLock> lockObjects = new ConditionalWeakTable<object, AsyncSafeLock>();
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
        }

        volatile int lockId = NO_LOCKID;
        int lockIdCounter = NO_LOCKID;
        LazySlim<AsyncManualResetEvent> storedWaitingEvent = new LazySlim<AsyncManualResetEvent>();
        AsyncManualResetEvent activeWaitingEvent = null;

        const int NO_LOCKID = 0;

        public IDisposable ForReading
        {
            get
            {
                var newLockId = 0;
                do
                {
                    if(lockId > NO_LOCKID)
                    {
                        activeWaitingEvent = storedWaitingEvent.Obj;
                        activeWaitingEvent.Wait();
                    }
                    var currentLockId = lockId;
                    newLockId = currentLockId - 1;
                    Interlocked.CompareExchange(ref lockId, newLockId, currentLockId);
                }
                while(lockId != newLockId);

                return new ReadLock(this);
            }
        }

        public async Task<IDisposable> ForReadingAsync()
        {
            var newLockId = 0;
            do
            {
                if(lockId > NO_LOCKID)
                {
                    activeWaitingEvent = storedWaitingEvent.Obj;
                    await activeWaitingEvent.WaitingTask;
                }
                var currentLockId = lockId;
                newLockId = currentLockId - 1;
                Interlocked.CompareExchange(ref lockId, newLockId, currentLockId);
            }
            while(lockId != newLockId);

            return new ReadLock(this);
        }

        public IDisposable ForWriting
        {
            get
            {
                var newLockId = Interlocked.Increment(ref lockIdCounter);
                while(newLockId == NO_LOCKID) newLockId = Interlocked.Increment(ref lockIdCounter);
                do
                {
                    if(lockId != NO_LOCKID)
                    {
                        activeWaitingEvent = storedWaitingEvent.Obj;
                        activeWaitingEvent.Wait();
                    }
                    Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID);
                }
                while(lockId != newLockId);

                return new WriteLock(this);
            }
        }

        public async Task<IDisposable> ForWritingAsync()
        {
            var newLockId = Interlocked.Increment(ref lockIdCounter);
            while(newLockId == NO_LOCKID) newLockId = Interlocked.Increment(ref lockIdCounter);
            do
            {
                if(lockId != NO_LOCKID)
                {
                    activeWaitingEvent = storedWaitingEvent.Obj;
                    await activeWaitingEvent.WaitingTask;
                }
                Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID);
            }
            while(lockId != newLockId);

            return new WriteLock(this);
        }

        private struct ReadLock : IDisposable
        {
            AsyncSafeLock safeLock;

            public ReadLock(AsyncSafeLock safeLock)
            {
                this.safeLock = safeLock;
                safeLock.activeWaitingEvent?.Reset();
            }

            public void Dispose()
            {
                if(NO_LOCKID == Interlocked.Increment(ref safeLock.lockId))
                {
                    safeLock.activeWaitingEvent = null;
                    safeLock.storedWaitingEvent.ObjIfExists?.Set();
                }
            }
        }

        private struct WriteLock : IDisposable
        {
            AsyncSafeLock safeLock;

            public WriteLock(AsyncSafeLock safeLock)
            {
                this.safeLock = safeLock;
                safeLock.activeWaitingEvent?.Reset();
            }

            public void Dispose()
            {
                safeLock.lockId = NO_LOCKID;
                safeLock.activeWaitingEvent = null;
                safeLock.storedWaitingEvent.ObjIfExists?.Set();
            }
        }
    }
}
