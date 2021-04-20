using System;
using System.Runtime.CompilerServices;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{
    public class FastSpinLockSubjects
    {
        MicroLock myLock = new MicroLock();

        public void Init() => myLock = new MicroLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using(myLock.Lock())
            {
                action();
            }
        }

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
