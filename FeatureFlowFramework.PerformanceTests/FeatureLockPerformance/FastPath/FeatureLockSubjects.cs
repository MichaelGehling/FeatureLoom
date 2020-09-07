using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPath
{

    public class FeatureLockSubjects
    {
        FeatureLock myLock = new FeatureLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            using (myLock.Lock())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await myLock.LockAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly()
        {
            using (myLock.LockReadOnly())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockReadOnlyAsync()
        {
            using (await myLock.LockReadOnlyAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {            
            if (myLock.TryLock(out var acquiredLock)) using (acquiredLock)
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLockReadOnly()
        {
            if (myLock.TryLockReadOnly(out var acquiredLock)) using (acquiredLock)
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLock()
        {
            using (myLock.LockReentrant())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockAsync()
        {
            using (await myLock.LockReentrantAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly()
        {
            using (myLock.LockReadOnlyReentrant())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockReadOnlyAsync()
        {
            using (await myLock.LockReadOnlyReentrantAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLock()
        {
            if (myLock.TryLockReentrant(out var acquiredLock)) using (acquiredLock)
                {

                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLockReadOnly()
        {
            if (myLock.TryLockReadOnlyReentrant(out var acquiredLock)) using (acquiredLock)
            {

            }
        }

    }
}
