using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{
    public class NeoSmartSubjects
    {
        NeoSmart.AsyncLock.AsyncLock myLock = new NeoSmart.AsyncLock.AsyncLock();

        public void Init() => myLock = new NeoSmart.AsyncLock.AsyncLock();

        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using(myLock.Lock())
            {
                action();
            }
        }
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Func<Task> action)
        {
            using(await myLock.LockAsync())
            {
                await action();
            }
        }




        /*
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            using (myLock.Lock())
            {

            }
        }
        */

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await myLock.LockAsync())
            {

            }
        }
    }
}
