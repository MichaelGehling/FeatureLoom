using System;
using System.Runtime.CompilerServices;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{
    public class MicroSpinLockSubjects
    {
        MicroSpinLock myLock = new MicroSpinLock();

        public void Init() => myLock = new MicroSpinLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {            
            myLock.Enter();
            try
            {
                action();
            }
            finally
            {
                myLock.Exit();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            myLock.Enter();
            try
            {
                
            }
            finally
            {
                myLock.Exit();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            if (myLock.TryEnter())
            {
                try
                {

                }
                finally
                {
                    myLock.Exit();
                }
            }
        }

    }
}
