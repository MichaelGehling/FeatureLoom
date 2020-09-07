using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPath
{
    public class BmbsqdSubjects
    {
        Bmbsqd.Async.AsyncLock myLock = new Bmbsqd.Async.AsyncLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await myLock)
            {

            }
        }
    }
}
