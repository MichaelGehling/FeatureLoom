using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPath
{
    public class SemaphoreSlimSubjects
    {
        SemaphoreSlim semaphore = new SemaphoreSlim(1);

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
