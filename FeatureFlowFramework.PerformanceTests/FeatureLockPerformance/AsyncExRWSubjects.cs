using Nito.AsyncEx;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance
{
    public class AsyncExRWSubjects
    {
        AsyncReaderWriterLock myLock = new AsyncReaderWriterLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            using (myLock.WriterLock())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using (myLock.WriterLock())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LockReadOnly()
        {
            using (myLock.ReaderLock())
            {

            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await myLock.WriterLockAsync())
            {
                
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Func<Task> action)
        {
            using (await myLock.WriterLockAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockReadOnlyAsync()
        {
            using (await myLock.ReaderLockAsync())
            {

            }
        }
    }
}
