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

        volatile int lockId = NO_LOCKID;
        volatile int lockIdCounter = NO_LOCKID;
        AsyncManualResetEvent waitingEvent = new AsyncManualResetEvent();

        const int NO_LOCKID = 0;

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public IDisposable ForReading()
        {
            var newLockId = 0;
            var currentLockId = 0;
            do
            {
                if(lockId > NO_LOCKID)
                {
                    waitingEvent.Wait();
                }
                currentLockId = lockId;
                newLockId = currentLockId - 1;
            }
            while(currentLockId > NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId));
            waitingEvent.Reset();

            if(currentLockId > NO_LOCKID)
            {
                Console.WriteLine("RL" + currentLockId + "/" + newLockId + " ");
            }

            return new ReadLock(this);
        }

        public async Task<IDisposable> ForReadingAsync()
        {
            var newLockId = 0;
            do
            {
                if(lockId > NO_LOCKID)
                {
                    await waitingEvent.WaitingTask;
                }
                var currentLockId = lockId;
                newLockId = currentLockId - 1;
                Interlocked.CompareExchange(ref lockId, newLockId, currentLockId);
            }
            while(lockId != newLockId);
            waitingEvent.Reset();

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public IDisposable ForWriting()
        {
            var newLockId = Interlocked.Increment(ref lockIdCounter);
            while(newLockId <= NO_LOCKID) newLockId = Interlocked.Increment(ref lockIdCounter);

            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if(currentLockId != NO_LOCKID)
                {
                    waitingEvent.Wait();
                }
            }
            while(currentLockId != NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID));
            waitingEvent.Reset();

            if(currentLockId != NO_LOCKID)
            {
                Console.WriteLine("WL" + currentLockId + "/" + newLockId + " ");
            }

            return new WriteLock(this, newLockId);
        }

        public async Task<IDisposable> ForWritingAsync()
        {
            var newLockId = Interlocked.Increment(ref lockIdCounter);
            while(newLockId == NO_LOCKID) newLockId = Interlocked.Increment(ref lockIdCounter);
            do
            {
                if(lockId != NO_LOCKID)
                {
                    await waitingEvent.WaitingTask;
                }
                Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID);
            }
            while(lockId != newLockId);
            waitingEvent.Reset();

            return new WriteLock(this, newLockId);
        }

        private struct ReadLock : IDisposable
        {
            AsyncSafeLock safeLock;

            [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
            public ReadLock(AsyncSafeLock safeLock)
            {
                this.safeLock = safeLock;
            }

            [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
            public void Dispose()
            {
                /*var newLockId = 0;
                var currentLockId = 0;
                do
                { 
                    currentLockId = safeLock.lockId;
                    newLockId = currentLockId + 1;
                    if (newLockId > NO_LOCKID)
                    {
                        Console.WriteLine("R*" + currentLockId + "/" + newLockId + "/" + safeLock.lockId + " ");
                        continue;
                    }
                }
                while(currentLockId != Interlocked.CompareExchange(ref safeLock.lockId, newLockId, currentLockId));
                */
                var newLockId = Interlocked.Increment(ref safeLock.lockId);

                if (NO_LOCKID == newLockId)
                {
                    safeLock.waitingEvent.Set();
                }
                else if (newLockId > NO_LOCKID)
                {
                    Console.WriteLine("RU"+ newLockId + "/" + safeLock.lockId + " ");
                }
            }
        }

        private struct WriteLock : IDisposable
        {
            AsyncSafeLock safeLock;
            int myLockId;

            [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
            public WriteLock(AsyncSafeLock safeLock, int myLockId)
            {
                this.safeLock = safeLock;
                this.myLockId = myLockId;
            }

            [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
            public void Dispose()
            {
                var currentLockId = safeLock.lockId;
                if(currentLockId == myLockId)
                {
                    //safeLock.lockId = NO_LOCKID;
                    Interlocked.Exchange(ref safeLock.lockId, NO_LOCKID);
                    safeLock.waitingEvent.Set();
                }
                else
                {
                    Console.WriteLine("WU" + currentLockId + "/" + myLockId + " ");
                }
            }
        }
    }
}
