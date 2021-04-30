using FeatureFlowFramework.Helpers.Synchronization;
using System;
using System.Runtime.CompilerServices;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{
    public class MicroValueLockSubjects
    {
        private MicroValueLock myLock = new MicroValueLock();

        public void Init() => myLock = new MicroValueLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            myLock.Enter(out var lockHandle);
            try
            {
                action();
            }
            finally
            {
                myLock.Exit(lockHandle);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            myLock.Enter(out var lockHandle);
            try
            {
            }
            finally
            {
                myLock.Exit(lockHandle);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            if (myLock.TryEnter(out var lockHandle))
            {
                try
                {
                }
                finally
                {
                    myLock.Exit(lockHandle);
                }
            }
        }
    }
}