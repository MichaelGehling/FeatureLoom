using System.Runtime.CompilerServices;
using System.Threading;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPath
{
    public class MonitorSubjects
    {
        object lockObj = new object();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            lock(lockObj)
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryLock()
        {
            if (Monitor.TryEnter(lockObj))
            {
                try
                {
                }
                finally
                {
                    Monitor.Exit(lockObj);
                }
            }
        }
    }
}
