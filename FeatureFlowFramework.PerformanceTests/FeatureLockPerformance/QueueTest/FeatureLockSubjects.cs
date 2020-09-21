using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.QueueTest
{
    public class FeatureLockSubjects
    {
        FeatureLock myLock = new FeatureLock();


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using(myLock.Lock())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock_HighPrio(Action action)
        {
            using(myLock.Lock(FeatureLock.HIGH_PRIORITY))
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Action action)
        {
            using(await myLock.LockAsync())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly(Action action)
        {
            using(myLock.LockReadOnly())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockReadOnlyAsync(Action action)
        {
            using(await myLock.LockReadOnlyAsync())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock(Action action)
        {
            if(myLock.TryLock(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLockReadOnly(Action action)
        {
            if(myLock.TryLockReadOnly(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockAsync(Action action)
        {
            if((await myLock.TryLockAsync()).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockReadOnlyAsync(Action action)
        {
            if((await myLock.TryLockReadOnlyAsync()).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLock(Action action)
        {
            using(myLock.LockReentrant())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockAsync(Action action)
        {
            using(await myLock.LockReentrantAsync())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly(Action action)
        {
            using(myLock.LockReadOnlyReentrant())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockReadOnlyAsync(Action action)
        {
            using(await myLock.LockReadOnlyReentrantAsync())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLock(Action action)
        {
            if(myLock.TryLockReentrant(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLockReadOnly(Action action)
        {
            if(myLock.TryLockReadOnlyReentrant(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockAsync(Action action)
        {
            if((await myLock.TryLockReentrantAsync()).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockReadOnlyAsync(Action action)
        {
            if((await myLock.TryLockReadOnlyReentrantAsync()).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

    }
}
