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