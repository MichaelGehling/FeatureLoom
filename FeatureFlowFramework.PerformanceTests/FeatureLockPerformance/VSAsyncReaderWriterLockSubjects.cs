using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{
    public class VSAsyncReaderWriterLockSubjects
    {
        AsyncReaderWriterLock myLock = new AsyncReaderWriterLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await myLock.WriteLockAsync())
            {

            }                
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockReadOnlyAsync()
        {
            using (await myLock.ReadLockAsync())
            {

            }
        }
    }
}
