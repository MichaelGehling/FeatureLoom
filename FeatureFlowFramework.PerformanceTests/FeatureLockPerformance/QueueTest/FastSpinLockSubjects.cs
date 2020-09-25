using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Runtime.CompilerServices;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.QueueTest
{
    public class FastSpinLockSubjects
    {
        FastSpinLock myLock;

        public void Init() => myLock = new FastSpinLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using(myLock.Lock())
            {
                action();
            }
        }
    }
}
