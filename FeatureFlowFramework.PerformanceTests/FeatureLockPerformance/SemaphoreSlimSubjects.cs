using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{
    public class SemaphoreSlimSubjects
    {
        SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public void Init() => semaphore = new SemaphoreSlim(1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            semaphore.Wait();
            try
            {
                action();
            }
            finally
            {
                semaphore.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Action action)
        {
            await semaphore.WaitAsync();
            try
            {
                action();
            }
            finally
            {
                semaphore.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock(Action action)
        {
            if(semaphore.Wait(0))
            {
                try
                {
                    action();
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }





        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            semaphore.Wait();
            try
            {

            }
            finally
            {
                semaphore.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            await semaphore.WaitAsync();
            try
            {

            }
            finally
            {
                semaphore.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            if (semaphore.Wait(0))
            {
                try
                {

                }
                finally
                {
                    semaphore.Release();
                }
            }
        }
    }
}
