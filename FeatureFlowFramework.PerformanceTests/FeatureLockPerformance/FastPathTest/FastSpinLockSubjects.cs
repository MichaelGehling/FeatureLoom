using System.Runtime.CompilerServices;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPathTest
{
    public class FastSpinLockSubjects
    {
        FastSpinLock myLock = new FastSpinLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            using(myLock.Lock())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            if(myLock.TryLock(out var acquiredLock)) using(acquiredLock)
            {

            }
        }

    }
}
