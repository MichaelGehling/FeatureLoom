using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FeatureLoom.Time;

namespace FeatureLoom.Synchronization
{
    public class AsyncManualResetEventTests
    {
        [Fact]
        public void CanBeAwaitedSyncAndAsync()
        {
            TestHelper.PrepareTestContext();

            AsyncManualResetEvent mre = new AsyncManualResetEvent(true);

            mre.Reset();

            Task syncTask = Task.Run(() =>
            {
                mre.Wait();
            });

            Task asyncTask = Task.Run(async () =>
            {
                await mre.WaitAsync();
            });

            Task syncTask2 = Task.Run(() =>
            {
                mre.Wait(1.Seconds());
            });

            Task asyncTask2 = Task.Run(async () =>
            {
                await mre.WaitAsync(1.Seconds());
            });

            Task syncTask3 = Task.Run(() =>
            {
                mre.Wait(1.Seconds(), CancellationToken.None);
            });

            Task asyncTask3 = Task.Run(async () =>
            {
                await mre.WaitAsync(1.Seconds(), CancellationToken.None);
            });

            Task syncTask4 = Task.Run(() =>
            {
                mre.Wait(CancellationToken.None);
            });

            Task asyncTask4 = Task.Run(async () =>
            {
                await mre.WaitAsync(CancellationToken.None);
            });

            Thread.Sleep(10);
            Assert.False(syncTask.IsCompleted);
            Assert.False(asyncTask.IsCompleted);
            Assert.False(syncTask2.IsCompleted);
            Assert.False(asyncTask2.IsCompleted);
            Assert.False(syncTask3.IsCompleted);
            Assert.False(asyncTask3.IsCompleted);
            Assert.False(syncTask4.IsCompleted);
            Assert.False(asyncTask4.IsCompleted);

            mre.Set();
            Thread.Sleep(10);
            Assert.True(syncTask.IsCompleted);
            Assert.True(asyncTask.IsCompleted);
            Assert.True(syncTask2.IsCompleted);
            Assert.True(asyncTask2.IsCompleted);
            Assert.True(syncTask3.IsCompleted);
            Assert.True(asyncTask3.IsCompleted);
            Assert.True(syncTask4.IsCompleted);
            Assert.True(asyncTask4.IsCompleted);
        }

        [Fact]
        public void WaitingCanTimeOut()
        {
            TestHelper.PrepareTestContext();

            AsyncManualResetEvent mre = new AsyncManualResetEvent();

            Task syncTask = Task.Run(() =>
            {
                mre.Wait(10.Milliseconds());
            });

            Task asyncTask = Task.Run(async () =>
            {
                await mre.WaitAsync(10.Milliseconds());
            });

            Task syncTask2 = Task.Run(() =>
            {
                mre.Wait(10.Milliseconds(), CancellationToken.None);
            });

            Task asyncTask2 = Task.Run(async () =>
            {
                await mre.WaitAsync(10.Milliseconds(), CancellationToken.None);
            });

            Thread.Sleep(50);
            Assert.True(syncTask.IsCompleted);
            Assert.True(asyncTask.IsCompleted);
            Assert.True(syncTask2.IsCompleted);
            Assert.True(asyncTask2.IsCompleted);
        }

        [Fact]
        public void WaitingCanBeCancelled()
        {
            TestHelper.PrepareTestContext();

            AsyncManualResetEvent mre = new AsyncManualResetEvent();
            CancellationTokenSource cts = new CancellationTokenSource();

            Task syncTask = Task.Run(() =>
            {
                mre.Wait(cts.Token);
            });

            Task asyncTask = Task.Run(async () =>
            {
                await mre.WaitAsync(cts.Token);
            });

            Task syncTask2 = Task.Run(() =>
            {
                mre.Wait(10.Seconds(), cts.Token);
            });

            Task asyncTask2 = Task.Run(async () =>
            {
                await mre.WaitAsync(10.Seconds(), cts.Token);
            });
            
            Thread.Sleep(10);
            cts.Cancel();

            Thread.Sleep(50);
            Assert.True(syncTask.IsCompleted);
            Assert.True(asyncTask.IsCompleted);
            Assert.True(syncTask2.IsCompleted);
            Assert.True(asyncTask2.IsCompleted);
        }

        [Fact]
        public void CanConvertToWaitHandle()
        {
            TestHelper.PrepareTestContext();

            AsyncManualResetEvent mre = new AsyncManualResetEvent();
            Assert.True(mre.TryConvertToWaitHandle(out WaitHandle waitHandle));

            Task waitHandleTask = Task.Run(() =>
            {
                waitHandle.WaitOne();
            });

            Thread.Sleep(10);
            Assert.False(waitHandleTask.IsCompleted);

            mre.Set();
            Thread.Sleep(10);
            Assert.True(waitHandleTask.IsCompleted);

            mre.DetachAndDisposeWaitHandle();
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

        [Fact]
        public void PulseAllRepeatetlyWillAwakeAll()
        {
            TestHelper.PrepareTestContext();

            AsyncManualResetEvent mre = new AsyncManualResetEvent();
            List<Thread> threads = new List<Thread>();
            int syncStarted = 0;
            int syncEnded = 0;
            int asyncStarted = 0;
            int asyncEnded = 0;
            bool stop = false;
            int numThreads = 10;

            for (int i = 0; i < numThreads; i++)
            {
                Thread thread = new Thread(() =>
                {
                    Interlocked.Increment(ref syncStarted);
                    while (!stop)
                    {
                        mre.Wait();
                    }
                    Interlocked.Increment(ref syncEnded);
                });
                thread.Start();
                threads.Add(thread);
                
                thread = new Thread(async () =>
                {
                    Interlocked.Increment(ref asyncStarted);
                    while (!stop)
                    {
                        await mre.WaitAsync();
                    }
                    Interlocked.Increment(ref asyncEnded);
                });
                thread.Start();
                threads.Add(thread);
                
            }

            Thread.Sleep(100);
            Assert.Equal(numThreads, syncStarted);
            Assert.Equal(numThreads, asyncStarted);
            Assert.Equal(0, syncEnded);
            Assert.Equal(0, asyncEnded);


            for (int i = 0; i < 1000; i++)
            {
                mre.PulseAll();
            }

            stop = true;            
            mre.PulseAll();            
            Thread.Sleep(100);            
            Assert.Equal(numThreads, syncEnded);
            Assert.Equal(numThreads, asyncEnded);
        }

    }
}