using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{

    public class FairFeatureLockSubjects
    {
        FeatureLock fairLock = new FeatureLock(FeatureLock.FairnessSettings);        

        #region EmbracingLock        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using(fairLock.Lock())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockPrio(Action action)
        {
            using(fairLock.Lock(true))
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Func<Task> action)
        {
            using(await fairLock.LockAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockPrioAsync(Func<Task> action)
        {
            using(await fairLock.LockAsync(true))
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly(Action action)
        {
            using(fairLock.LockReadOnly())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockReadOnlyAsync(Func<Task> action)
        {
            using(await fairLock.LockReadOnlyAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock(Action action)
        {
            if(fairLock.TryLock(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLockReadOnly(Action action)
        {
            if(fairLock.TryLockReadOnly(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockAsync(Func<Task> action)
        {
            if((await fairLock.TryLockAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    await action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockReadOnlyAsync(Func<Task> action)
        {
            if((await fairLock.TryLockReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    await action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLock(Action action)
        {
            using(fairLock.LockReentrant())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantPrioLock(Action action)
        {
            using (fairLock.LockReentrant(true))
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockAsync(Func<Task> action)
        {
            using(await fairLock.LockReentrantAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantPrioLockAsync(Func<Task> action)
        {
            using (await fairLock.LockReentrantAsync(true))
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly(Action action)
        {
            using(fairLock.LockReentrantReadOnly())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockReadOnlyAsync(Func<Task> action)
        {
            using(await fairLock.LockReentrantReadOnlyAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLock(Action action)
        {
            if(fairLock.TryLockReentrant(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLockReadOnly(Action action)
        {
            if(fairLock.TryLockReentrantReadOnly(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockAsync(Func<Task> action)
        {
            if((await fairLock.TryLockReentrantAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    await action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockReadOnlyAsync(Func<Task> action)
        {
            if((await fairLock.TryLockReentrantReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    await action();
                }
        }
        #endregion EmbracingLock

        #region EmptyLock
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            using (fairLock.Lock())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await fairLock.LockAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly()
        {
            using (fairLock.LockReadOnly())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockReadOnlyAsync()
        {
            using (await fairLock.LockReadOnlyAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {            
            if (fairLock.TryLock(out var acquiredLock)) using (acquiredLock)
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLockReadOnly()
        {
            if (fairLock.TryLockReadOnly(out var acquiredLock)) using (acquiredLock)
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockAsync()
        {
            if((await fairLock.TryLockAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {

                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockReadOnlyAsync()
        {
            if((await fairLock.TryLockReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {

                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLock()
        {
            using (fairLock.LockReentrant())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockAsync()
        {
            using (await fairLock.LockReentrantAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly()
        {
            using (fairLock.LockReentrantReadOnly())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockReadOnlyAsync()
        {
            using (await fairLock.LockReentrantReadOnlyAsync())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLock()
        {
            if (fairLock.TryLockReentrant(out var acquiredLock)) using (acquiredLock)
                {

                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLockReadOnly()
        {
            if (fairLock.TryLockReentrantReadOnly(out var acquiredLock)) using (acquiredLock)
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockAsync()
        {
            if((await fairLock.TryLockReentrantAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {

                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockReadOnlyAsync()
        {
            if((await fairLock.TryLockReentrantReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {

                }
        }
        #endregion EmptyLock

    }
}
