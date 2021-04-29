using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{
    public class SpinLockSubjects
    {
        private SpinLock myLock = new SpinLock(false);

        public void Init() => myLock = new SpinLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            bool lockTaken = false;
            myLock.Enter(ref lockTaken);
            if (lockTaken)
            {
                try
                {
                }
                finally
                {
                    myLock.Exit(false);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            bool lockTaken = false;
            myLock.Enter(ref lockTaken);
            if (lockTaken)
            {
                try
                {
                    action();
                }
                finally
                {
                    myLock.Exit(false);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            bool lockTaken = false;
            myLock.TryEnter(ref lockTaken);
            if (lockTaken)
            {
                try
                {
                }
                finally
                {
                    myLock.Exit(false);
                }
            }
        }
    }
}