using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{

    public class FeatureLockSubjects
    {
        FeatureLock myLock = new FeatureLock();
        public void Init() => myLock = new FeatureLock();

        #region EmbracingLock        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using(myLock.Lock())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockPrio(Action action)
        {
            using(myLock.Lock(true))
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Func<Task> action)
        {
            using(await myLock.LockAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockPrioAsync(Func<Task> action)
        {
            using(await myLock.LockAsync(true))
            {
                await action();
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
        public async Task LockReadOnlyAsync(Func<Task> action)
        {
            using(await myLock.LockReadOnlyAsync())
            {
                await action();
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
        public async Task TryLockAsync(Func<Task> action)
        {
            if((await myLock.TryLockAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    await action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockReadOnlyAsync(Func<Task> action)
        {
            if((await myLock.TryLockReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    await action();
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
        public void ReentrantPrioLock(Action action)
        {
            using (myLock.LockReentrant(true))
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockAsync(Func<Task> action)
        {
            using(await myLock.LockReentrantAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantPrioLockAsync(Func<Task> action)
        {
            using (await myLock.LockReentrantAsync(true))
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly(Action action)
        {
            using(myLock.LockReentrantReadOnly())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockReadOnlyAsync(Func<Task> action)
        {
            using(await myLock.LockReentrantReadOnlyAsync())
            {
                await action();
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
            if(myLock.TryLockReentrantReadOnly(out var acquiredLock)) using(acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockAsync(Func<Task> action)
        {
            if((await myLock.TryLockReentrantAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    await action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockReadOnlyAsync(Func<Task> action)
        {
            if((await myLock.TryLockReentrantReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {
                    await action();
                }
        }
        #endregion EmbracingLock

        #region EmptyLock
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
        public async Task TryLockAsync()
        {
            if((await myLock.TryLockAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {

                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockReadOnlyAsync()
        {
            if((await myLock.TryLockReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
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
            using (myLock.LockReentrantReadOnly())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockReadOnlyAsync()
        {
            using (await myLock.LockReentrantReadOnlyAsync())
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
            if (myLock.TryLockReentrantReadOnly(out var acquiredLock)) using (acquiredLock)
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockAsync()
        {
            if((await myLock.TryLockReentrantAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {

                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockReadOnlyAsync()
        {
            if((await myLock.TryLockReentrantReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using(acquiredLock)
                {

                }
        }
        #endregion EmptyLock

    }
}
