using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public static class AsyncLockExtensions
    {
        private static ConditionalWeakTable<object, AsyncLock> lockObjects = new ConditionalWeakTable<object, AsyncLock>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncLock.ReadLock LockForReading<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForReading();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<AsyncLock.ReadLock> LockForReadingAsync<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForReadingAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncLock.WriteLock LockForWriting<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForWriting();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<AsyncLock.WriteLock> LockForWritingAsync<T>(this T obj) where T : class
        {
            var objLock = lockObjects.GetOrCreateValue(obj);
            return objLock.ForWritingAsync();
        }
    }

    public class Box<T> where T : struct
    {
        public T value;
    }

    public class AsyncLock
    {
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
        TaskCompletionSource<bool> tcs;
        SpinWait spinWait = new SpinWait();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadLock ForReading(bool onlySpinWaiting = false)
        {
            var newLockId = 0;
            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if (currentLockId > NO_LOCKID)
                {
                    if (onlySpinWaiting || !spinWait.NextSpinWillYield)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    if(tcs == null) Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), null);
                    tcs?.Task.Wait();
                    currentLockId = lockId;
                }
                newLockId = currentLockId - 1;
            }
            while(currentLockId > NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId));

            ResetTcs();

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<ReadLock> ForReadingAsync(bool onlySpinWaiting = false)
        {
            var newLockId = 0;
            var currentLockId = 0;
            do
            {
                currentLockId = lockId;
                if (currentLockId > NO_LOCKID)
                {
                    if (onlySpinWaiting || !spinWait.NextSpinWillYield)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    if (tcs == null) Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), null);
                    await tcs?.Task;
                    currentLockId = lockId;
                }
                newLockId = currentLockId - 1;
            }
            while(currentLockId > NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, currentLockId));

            ResetTcs();

            return new ReadLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public WriteLock ForWriting(bool onlySpinWaiting = false)
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
                    if (onlySpinWaiting || !spinWait.NextSpinWillYield)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    if (tcs == null) Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), null);
                    tcs?.Task.Wait();
                    currentLockId = lockId;
                }
            }
            while(currentLockId != NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID));

            ResetTcs();

            return new WriteLock(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<WriteLock> ForWritingAsync(bool onlySpinWaiting = false)
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
                    if (onlySpinWaiting || !spinWait.NextSpinWillYield)
                    {
                        spinWait.SpinOnce();
                        continue;
                    }
                    if (tcs == null) Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), null);
                    await tcs?.Task;
                    currentLockId = lockId;
                }
            }
            while(currentLockId != NO_LOCKID || currentLockId != Interlocked.CompareExchange(ref lockId, newLockId, NO_LOCKID));

            ResetTcs();

            return new WriteLock(this);
        }

        public struct ReadLock : IDisposable
        {
            AsyncLock safeLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadLock(AsyncLock safeLock)
            {
                this.safeLock = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                var newLockId = Interlocked.Increment(ref safeLock.lockId);

                if (NO_LOCKID == newLockId)
                {
                    safeLock.spinWait.Reset();
                    safeLock.tcs?.TrySetResult(true);
                }
            }
        }

        public struct WriteLock : IDisposable
        {
            AsyncLock safeLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public WriteLock(AsyncLock safeLock)
            {
                this.safeLock = safeLock;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                safeLock.spinWait.Reset();
                safeLock.lockId = NO_LOCKID;
                safeLock.tcs?.TrySetResult(true);
            }
        }
    }
}
