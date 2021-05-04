using Microsoft.VisualStudio.Threading;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance
{
    public class VSAsyncReaderWriterLockSubjects
    {
        private AsyncReaderWriterLock myLock = new AsyncReaderWriterLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await myLock.WriteLockAsync())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Func<Task> action)
        {
            using (await myLock.WriteLockAsync())
            {
                await action();
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