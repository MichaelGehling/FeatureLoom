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
        FeatureLock reentrantLock = new FeatureLock(true);

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
            using (reentrantLock.Lock())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockAsync()
        {
            using (await reentrantLock.LockAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly()
        {
            using (reentrantLock.LockReadOnly())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockReadOnlyAsync()
        {
            using (await reentrantLock.LockReadOnlyAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLock()
        {
            if (reentrantLock.TryLock(out var acquiredLock)) using (acquiredLock)
                {

                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLockReadOnly()
        {
            if (reentrantLock.TryLockReadOnly(out var acquiredLock)) using (acquiredLock)
            {

            }
        }

    }
}
