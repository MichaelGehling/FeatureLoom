using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Synchronization;

public class AsyncManualResetEventTests
{
    [Fact]
    public void CanBeAwaitedSyncAndAsync()
    {
        using var testContext = TestHelper.PrepareTestContext();

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
        using var testContext = TestHelper.PrepareTestContext();

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
        using var testContext = TestHelper.PrepareTestContext();

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
        using var testContext = TestHelper.PrepareTestContext();

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
        using var testContext = TestHelper.PrepareTestContext();

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


    volatile bool stop = false;
    [Fact]
    public void PulseAllRepeatetlyWillAwakeAll()
    {
        using var testContext = TestHelper.PrepareTestContext();

        AsyncManualResetEvent mre = new AsyncManualResetEvent();
        List<Thread> threads = new List<Thread>();
        int syncStarted = 0;
        int syncEnded = 0;
        int asyncStarted = 0;
        int asyncEnded = 0;        
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
        Thread.Sleep(100);

        stop = true;            
        mre.PulseAll();            
        Thread.Sleep(100);            
        Assert.Equal(numThreads, syncEnded);
        Assert.Equal(numThreads, asyncEnded);
    }

    [Fact]
    public void ResetSendsFalseNotification()
    {
        using var testContext = TestHelper.PrepareTestContext();

        // Arrange
        AsyncManualResetEvent mre = new AsyncManualResetEvent(true);
        TestMessageSink<bool> testSink = new TestMessageSink<bool>();
        mre.ConnectTo(testSink);

        // Act
        mre.Reset();

        // Assert
        Assert.Single(testSink.ReceivedMessages);
        Assert.False(testSink.ReceivedMessages[0]);
    }

    [Fact]
    public void SetSendsTrueNotification()
    {
        using var testContext = TestHelper.PrepareTestContext();

        // Arrange
        AsyncManualResetEvent mre = new AsyncManualResetEvent(false);
        TestMessageSink<bool> testSink = new TestMessageSink<bool>();
        mre.ConnectTo(testSink);

        // Act
        mre.Set();

        // Assert
        Assert.Single(testSink.ReceivedMessages);
        Assert.True(testSink.ReceivedMessages[0]);
    }

    [Fact]
    public void PulseAllSendsNotification()
    {
        using var testContext = TestHelper.PrepareTestContext();

        // Arrange
        AsyncManualResetEvent mre = new AsyncManualResetEvent(false);
        TestMessageSink<bool> testSink = new TestMessageSink<bool>();
        mre.ConnectTo(testSink);

        // Act
        mre.PulseAll();

        // Assert
        Assert.True(testSink.ReceivedMessages.Count == 2);
        Assert.True(testSink.ReceivedMessages[0]);
        Assert.False(testSink.ReceivedMessages[1]);
    }

    // Helper class to act as a test sink
    private class TestMessageSink<T> : IMessageSink<T>
    {
        public List<T> ReceivedMessages { get; } = new List<T>();

        public void Post<M>(in M message)
        {
            if (message is T typedMessage) ReceivedMessages.Add(typedMessage);
        }

        public void Post<M>(M message)
        {
            if (message is T typedMessage) ReceivedMessages.Add(typedMessage);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T typedMessage) ReceivedMessages.Add(typedMessage);
            return Task.CompletedTask;
        }

        public Type ConsumedMessageType => typeof(T);
    }

    #region Construction and state

    [Fact]
    public void Constructor_DefaultState_IsNotSet()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();

        Assert.False(mre.IsSet);
        Assert.True(mre.WouldWait());
    }

    [Fact]
    public void Constructor_InitialStateTrue_IsSet()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(true);

        Assert.True(mre.IsSet);
        Assert.False(mre.WouldWait());
    }

    [Fact]
    public void Constructor_InitialStateFalse_IsNotSet()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(false);

        Assert.False(mre.IsSet);
        Assert.True(mre.WouldWait());
    }

    #endregion

    #region Set / Reset return values and idempotency

    [Fact]
    public void Set_WhenNotSet_ReturnsTrueAndSetsFlag()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();

        bool result = mre.Set();

        Assert.True(result);
        Assert.True(mre.IsSet);
    }

    [Fact]
    public void Set_WhenAlreadySet_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(true);

        bool result = mre.Set();

        Assert.False(result);
    }

    [Fact]
    public void Reset_WhenSet_ReturnsTrueAndClearsFlag()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(true);

        bool result = mre.Reset();

        Assert.True(result);
        Assert.False(mre.IsSet);
    }

    [Fact]
    public void Reset_WhenAlreadyReset_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();

        bool result = mre.Reset();

        Assert.False(result);
    }

    [Fact]
    public void SetResetSet_CycleBehavesCorrectly()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();

        Assert.False(mre.IsSet);
        mre.Set();
        Assert.True(mre.IsSet);
        mre.Reset();
        Assert.False(mre.IsSet);
        mre.Set();
        Assert.True(mre.IsSet);
    }

    #endregion

    #region Wait return values

    [Fact]
    public void Wait_WhenAlreadySet_ReturnsTrueImmediately()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(true);

        Assert.True(mre.Wait());
        Assert.True(mre.Wait(1.Seconds()));
        Assert.True(mre.Wait(CancellationToken.None));
        Assert.True(mre.Wait(1.Seconds(), CancellationToken.None));
    }

    [Fact]
    public async Task WaitAsync_WhenAlreadySet_ReturnsTrueImmediately()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(true);

        Assert.True(await mre.WaitAsync());
        Assert.True(await mre.WaitAsync(1.Seconds()));
        Assert.True(await mre.WaitAsync(CancellationToken.None));
        Assert.True(await mre.WaitAsync(1.Seconds(), CancellationToken.None));
    }

    [Fact]
    public void Wait_Timeout_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();

        Assert.False(mre.Wait(TimeSpan.FromMilliseconds(30)));
        Assert.False(mre.Wait(TimeSpan.FromMilliseconds(30), CancellationToken.None));
    }

    [Fact]
    public async Task WaitAsync_Timeout_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();

        Assert.False(await mre.WaitAsync(TimeSpan.FromMilliseconds(30)));
        Assert.False(await mre.WaitAsync(TimeSpan.FromMilliseconds(30), CancellationToken.None));
    }

    [Fact]
    public void Wait_Cancelled_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();
        using var cts = new CancellationTokenSource(30);

        Assert.False(mre.Wait(cts.Token));
    }

    [Fact]
    public void Wait_TimeoutAndCancellation_ReturnsFalseWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();
        using var cts = new CancellationTokenSource(30);

        Assert.False(mre.Wait(2.Seconds(), cts.Token));
    }

    [Fact]
    public async Task WaitAsync_Cancelled_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();
        using var cts = new CancellationTokenSource(30);

        Assert.False(await mre.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task WaitAsync_TimeoutAndCancellation_ReturnsFalseWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();
        using var cts = new CancellationTokenSource(30);

        Assert.False(await mre.WaitAsync(2.Seconds(), cts.Token));
    }

    [Fact]
    public void Wait_SignalledBeforeTimeout_ReturnsTrue()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();
        Task.Run(() => { Thread.Sleep(20); mre.Set(); });

        Assert.True(mre.Wait(2.Seconds()));
    }

    [Fact]
    public async Task WaitAsync_SignalledBeforeTimeout_ReturnsTrue()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent();
        _ = Task.Run(async () => { await Task.Delay(20); mre.Set(); });

        Assert.True(await mre.WaitAsync(2.Seconds()));
    }

    #endregion

    #region WaitingTask

    [Fact]
    public void WaitingTask_WhenSet_ReturnsCompletedTask()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(true);

        Assert.True(mre.WaitingTask.IsCompleted);
    }

    [Fact]
    public void WaitingTask_WhenNotSet_ReturnsIncompleteTask()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(false);

        Assert.False(mre.WaitingTask.IsCompleted);
    }

    [Fact]
    public async Task WaitingTask_CompletesWhenSet()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(false);
        var task = mre.WaitingTask;

        Assert.False(task.IsCompleted);

        _ = Task.Run(async () => { await Task.Delay(20); mre.Set(); });

        await task;
        Assert.True(task.IsCompleted);
    }

    #endregion

    #region WaitHandle lifecycle

    [Fact]
    public void TryConvertToWaitHandle_WhenSet_ReturnsSignalledHandle()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(true);
        Assert.True(mre.TryConvertToWaitHandle(out WaitHandle wh));

        Assert.True(wh.WaitOne(0));
        mre.DetachAndDisposeWaitHandle();
    }

    [Fact]
    public void TryConvertToWaitHandle_WhenNotSet_ReturnsUnsignalledHandle()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(false);
        Assert.True(mre.TryConvertToWaitHandle(out WaitHandle wh));

        Assert.False(wh.WaitOne(0));
        mre.DetachAndDisposeWaitHandle();
    }

    [Fact]
    public void WaitHandle_TracksSetAndReset()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(false);
        Assert.True(mre.TryConvertToWaitHandle(out WaitHandle wh));

        mre.Set();
        Assert.True(wh.WaitOne(0));

        mre.Reset();
        Assert.False(wh.WaitOne(0));

        mre.DetachAndDisposeWaitHandle();
    }

    [Fact]
    public void DetachAndDisposeWaitHandle_SubsequentConvertCreatesNew()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(true);
        mre.TryConvertToWaitHandle(out WaitHandle wh1);
        mre.DetachAndDisposeWaitHandle();

        mre.TryConvertToWaitHandle(out WaitHandle wh2);
        Assert.NotSame(wh1, wh2);
        mre.DetachAndDisposeWaitHandle();
    }

    #endregion

    #region Notification — already-set / already-reset idempotency

    [Fact]
    public void Set_WhenAlreadySet_SendsNoNotification()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(true);
        var sink = new TestMessageSink<bool>();
        mre.ConnectTo(sink);

        mre.Set();

        Assert.Empty(sink.ReceivedMessages);
    }

    [Fact]
    public void Reset_WhenAlreadyReset_SendsNoNotification()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var mre = new AsyncManualResetEvent(false);
        var sink = new TestMessageSink<bool>();
        mre.ConnectTo(sink);

        mre.Reset();

        Assert.Empty(sink.ReceivedMessages);
    }

    #endregion
}
