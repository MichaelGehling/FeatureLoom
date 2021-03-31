using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{
    public class BmbsqdSubjects
    {
        Bmbsqd.Async.AsyncLock myLock = new Bmbsqd.Async.AsyncLock();

        public void Init() => myLock = new Bmbsqd.Async.AsyncLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await myLock)
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Func<Task> action)
        {
            using (await myLock)
            {
                await action();
            }
        }
    }
}
