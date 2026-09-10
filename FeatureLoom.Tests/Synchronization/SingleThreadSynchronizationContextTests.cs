using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Synchronization;

public class SingleThreadSynchronizationContextTests
{
    [Fact]
    public void Post_ExecutesOnDedicatedThread()
    {
        using var context = new SingleThreadSynchronizationContext();
        int threadId = -1;
        var done = new ManualResetEventSlim();

        context.Post(_ =>
        {
            threadId = Thread.CurrentThread.ManagedThreadId;
            done.Set();
        }, null);

        Assert.True(done.Wait(1000));
        Assert.Equal(contextThreadId(context), threadId);
    }

    [Fact]
    public void Send_ExecutesSynchronouslyOnDedicatedThread()
    {
        using var context = new SingleThreadSynchronizationContext();
        int threadId = -1;

        var task = Task.Run(() =>
        {
            context.Send(_ =>
            {
                threadId = Thread.CurrentThread.ManagedThreadId;
            }, null);
        });

        Assert.True(task.Wait(1000));
        Assert.Equal(contextThreadId(context), threadId);
    }

    [Fact]
    public void Post_MultipleItems_ExecutesInOrder()
    {
        using var context = new SingleThreadSynchronizationContext();
        var result = "";
        var done = new ManualResetEventSlim();

        context.Post(_ => result += "A", null);
        context.Post(_ => result += "B", null);
        context.Post(_ =>
        {
            result += "C";
            done.Set();
        }, null);

        Assert.True(done.Wait(1000));
        Assert.Equal("ABC", result);
    }

    [Fact]
    public void Send_FromContextThread_ExecutesInline()
    {
        using var context = new SingleThreadSynchronizationContext();
        int threadId = -1;
        var done = new ManualResetEventSlim();

        context.Post(_ =>
        {
            context.Send(_ =>
            {
                threadId = Thread.CurrentThread.ManagedThreadId;
                done.Set();
            }, null);
        }, null);

        Assert.True(done.Wait(1000));
        Assert.Equal(contextThreadId(context), threadId);
    }

    [Fact]
    public void ExceptionInCallback_DoesNotStopContext()
    {
        using var context = new SingleThreadSynchronizationContext();
        var done = new ManualResetEventSlim();
        bool secondExecuted = false;

        context.Post(_ => throw new InvalidOperationException(), null);
        context.Post(_ =>
        {
            secondExecuted = true;
            done.Set();
        }, null);

        Assert.True(done.Wait(1000));
        Assert.True(secondExecuted);
    }

    [Fact]
    public void Dispose_StopsProcessing()
    {
        var context = new SingleThreadSynchronizationContext();
        context.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            context.Post(_ => { }, null);
        });
    }

    [Fact]
    public void CallbacksMayPostFurtherWorkWithoutStallingTheContext()
    {
        using var context = new SingleThreadSynchronizationContext();

        int executed = 0;
        const int maxDepth = 3;

        void Work(object state)
        {
            int depth = (int)state;
            Interlocked.Increment(ref executed);
            if (depth < maxDepth)
            {
                // Posting from within a callback must not re-enter the queue lock.
                context.Post(Work, depth + 1);
                context.Post(Work, depth + 1);
            }
        }

        context.Post(Work, 1);

        // 1 + 2 + 4 = 7 expected executions.
        Assert.True(SpinUntil(() => Volatile.Read(ref executed) >= 7, TimeSpan.FromSeconds(5)),
            $"context stalled, executed={Volatile.Read(ref executed)} of 7");
    }

    [Fact]
    public void WorkPostedFromOtherThreadsIsAlwaysPickedUp()
    {
        using var context = new SingleThreadSynchronizationContext();

        const int count = 500;
        int executed = 0;

        // Posting from a foreign thread while the context repeatedly goes idle
        // must never lose a wake-up.
        for (int i = 0; i < count; i++)
        {
            context.Post(_ => Interlocked.Increment(ref executed), null);
            if (i % 25 == 0) Thread.Sleep(1); // let the context reach its idle wait
        }

        Assert.True(SpinUntil(() => Volatile.Read(ref executed) == count, TimeSpan.FromSeconds(10)),
            $"lost wake-up, executed={Volatile.Read(ref executed)} of {count}");
    }

    [Fact]
    public void Send_PreservesTheOriginalExceptionStackTrace()
    {
        using var context = new SingleThreadSynchronizationContext();

        var ex = Assert.Throws<InvalidOperationException>(() => Task.Run(
            () => context.Send(_ => ThrowingHelper(), null)).GetAwaiter().GetResult());

        Assert.Equal("boom", ex.Message);
        Assert.Contains(nameof(ThrowingHelper), ex.StackTrace);
    }

    [Fact]
    public void Send_DoesNotBlockForeverWhenContextIsDisposed()
    {
        var context = new SingleThreadSynchronizationContext();

        // Occupy the context thread so the Send work item stays queued.
        var blocking = new ManualResetEventSlim();
        context.Post(_ => blocking.Wait(TimeSpan.FromSeconds(5)), null);
        Thread.Sleep(50);

        Exception caught = null;
        bool executed = false;
        var sender = new Thread(() =>
        {
            try { context.Send(_ => executed = true, null); }
            catch (Exception e) { caught = e; }
        });
        sender.Start();

        Thread.Sleep(50);
        // The work item is queued but has not started; Dispose must abort it deterministically.
        context.Dispose();

        Assert.True(sender.Join(TimeSpan.FromSeconds(5)), "Send() caller was stranded after Dispose()");
        Assert.IsType<ObjectDisposedException>(caught);
        Assert.False(executed);

        blocking.Set();
    }

    [Fact]
    public void Send_AfterDisposeThrowsImmediately()
    {
        var context = new SingleThreadSynchronizationContext();
        context.Dispose();

        Assert.Throws<ObjectDisposedException>(() => Task.Run(
            () => context.Send(_ => { }, null)).GetAwaiter().GetResult());
    }

    [Fact]
    public void ExceptionInCallback_DoesNotDelayAlreadyQueuedWork()
    {
        using var context = new SingleThreadSynchronizationContext();

        int executed = 0;
        // Several failing callbacks interleaved with successful ones: the loop must never park
        // while work is still queued.
        for (int i = 0; i < 20; i++)
        {
            context.Post(_ => throw new InvalidOperationException(), null);
            context.Post(_ => Interlocked.Increment(ref executed), null);
        }

        Assert.True(SpinUntil(() => Volatile.Read(ref executed) == 20, TimeSpan.FromSeconds(5)),
            $"context parked after an exception, executed={Volatile.Read(ref executed)} of 20");
    }

    private static void ThrowingHelper() => throw new InvalidOperationException("boom");

    private static bool SpinUntil(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout) return false;
            Thread.Sleep(5);
        }
        return true;
    }

    // Helper to get the context thread id by posting a callback and capturing the thread id
    private int contextThreadId(SingleThreadSynchronizationContext context)
    {
        int id = -1;
        var done = new ManualResetEventSlim();
        context.Post(_ =>
        {
            id = Thread.CurrentThread.ManagedThreadId;
            done.Set();
        }, null);
        done.Wait(1000);
        return id;
    }
}