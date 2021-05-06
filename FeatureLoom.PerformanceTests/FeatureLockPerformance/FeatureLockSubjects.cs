using FeatureLoom.Synchronization;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance
{
    public class FeatureLockSubjects
    {
        private FeatureLock perfLock = new FeatureLock();
        private FeatureLock fairLock = new FeatureLock(FeatureLock.FairnessSettings);

        #region EmbracingLock

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using (perfLock.Lock())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockPrio(Action action)
        {
            using (perfLock.Lock(true))
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Func<Task> action)
        {
            using (await perfLock.LockAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockPrioAsync(Func<Task> action)
        {
            using (await perfLock.LockAsync(true))
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly(Action action)
        {
            using (perfLock.LockReadOnly())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockReadOnlyAsync(Func<Task> action)
        {
            using (await perfLock.LockReadOnlyAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock(Action action)
        {
            if (perfLock.TryLock(out var acquiredLock)) using (acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLockReadOnly(Action action)
        {
            if (perfLock.TryLockReadOnly(out var acquiredLock)) using (acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockAsync(Func<Task> action)
        {
            if ((await perfLock.TryLockAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using (acquiredLock)
                {
                    await action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockReadOnlyAsync(Func<Task> action)
        {
            if ((await perfLock.TryLockReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using (acquiredLock)
                {
                    await action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLock(Action action)
        {
            using (perfLock.LockReentrant())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantPrioLock(Action action)
        {
            using (perfLock.LockReentrant(true))
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockAsync(Func<Task> action)
        {
            using (await perfLock.LockReentrantAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantPrioLockAsync(Func<Task> action)
        {
            using (await perfLock.LockReentrantAsync(true))
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly(Action action)
        {
            using (perfLock.LockReentrantReadOnly())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockReadOnlyAsync(Func<Task> action)
        {
            using (await perfLock.LockReentrantReadOnlyAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLock(Action action)
        {
            if (perfLock.TryLockReentrant(out var acquiredLock)) using (acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLockReadOnly(Action action)
        {
            if (perfLock.TryLockReentrantReadOnly(out var acquiredLock)) using (acquiredLock)
                {
                    action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockAsync(Func<Task> action)
        {
            if ((await perfLock.TryLockReentrantAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using (acquiredLock)
                {
                    await action();
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockReadOnlyAsync(Func<Task> action)
        {
            if ((await perfLock.TryLockReentrantReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using (acquiredLock)
                {
                    await action();
                }
        }

        #endregion EmbracingLock

        #region EmptyLock

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            using (perfLock.Lock())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await perfLock.LockAsync())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly()
        {
            using (perfLock.LockReadOnly())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockReadOnlyAsync()
        {
            using (await perfLock.LockReadOnlyAsync())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            if (perfLock.TryLock(out var acquiredLock)) using (acquiredLock)
                {
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLockReadOnly()
        {
            if (perfLock.TryLockReadOnly(out var acquiredLock)) using (acquiredLock)
                {
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockAsync()
        {
            if ((await perfLock.TryLockAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using (acquiredLock)
                {
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task TryLockReadOnlyAsync()
        {
            if ((await perfLock.TryLockReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using (acquiredLock)
                {
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLock()
        {
            using (perfLock.LockReentrant())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockAsync()
        {
            using (await perfLock.LockReentrantAsync())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantLockReadOnly()
        {
            using (perfLock.LockReentrantReadOnly())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantLockReadOnlyAsync()
        {
            using (await perfLock.LockReentrantReadOnlyAsync())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLock()
        {
            if (perfLock.TryLockReentrant(out var acquiredLock)) using (acquiredLock)
                {
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReentrantTryLockReadOnly()
        {
            if (perfLock.TryLockReentrantReadOnly(out var acquiredLock)) using (acquiredLock)
                {
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockAsync()
        {
            if ((await perfLock.TryLockReentrantAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using (acquiredLock)
                {
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task ReentrantTryLockReadOnlyAsync()
        {
            if ((await perfLock.TryLockReentrantReadOnlyAsync(TimeSpan.Zero)).Succeeded(out var acquiredLock)) using (acquiredLock)
                {
                }
        }

        #endregion EmptyLock
    }
}