using FeatureLoom.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Synchronization
{
    public class AsyncManualResetEventTests
    {
        [Fact]
        public void CanBeAwaitedSyncAndAsync()
        {
            TestHelper.PrepareTestContext();

            AsyncManualResetEvent mre = new AsyncManualResetEvent();

            Task syncTask = Task.Run(() =>
            {
                mre.Wait();
            });

            Task asyncTask = Task.Run(async () =>
            {
                await mre.WaitAsync();
            });

            Thread.Sleep(10);
            Assert.False(syncTask.IsCompleted);
            Assert.False(asyncTask.IsCompleted);

            mre.Set();
            Thread.Sleep(10);
            Assert.True(syncTask.IsCompleted);
            Assert.True(asyncTask.IsCompleted);
        }

        [Fact]
        public void PulseAllWillAwakeAll()
        {
            TestHelper.PrepareTestContext();

            AsyncManualResetEvent mre = new AsyncManualResetEvent();
            List<Thread> threads = new List<Thread>();
            int countEnd = 0;

            for (int i = 0; i < 20; i = i + 2)
            {
                Thread thread = new Thread(() =>
                {
                    mre.Wait();
                    Interlocked.Increment(ref countEnd);
                });
                thread.Start();
                threads.Add(thread);

                thread = new Thread(async () =>
                {
                    await mre.WaitAsync();
                    Interlocked.Increment(ref countEnd);
                });
                thread.Start();
                threads.Add(thread);
            }

            Thread.Sleep(100);
            Assert.Equal(0, countEnd);

            mre.PulseAll();
            Thread.Sleep(100);
            Assert.Equal(threads.Count, countEnd);
        }
    }
}