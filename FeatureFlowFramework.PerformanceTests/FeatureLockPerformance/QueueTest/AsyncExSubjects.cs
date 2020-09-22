using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.QueueTest
{

    public class AsyncExSubjects
    {
        Nito.AsyncEx.AsyncLock myLock;

        public void Init() => myLock = new Nito.AsyncEx.AsyncLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using (myLock.Lock())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Action action)
        {
            using (await myLock.LockAsync())
            {
                action();
            }
        }
    }
}
