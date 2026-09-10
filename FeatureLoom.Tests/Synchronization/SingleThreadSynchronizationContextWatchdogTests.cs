using FeatureLoom.MessageFlow;
using FeatureLoom.Time;
using System;
using System.Threading;
using Xunit;

namespace FeatureLoom.Synchronization;

public class SingleThreadSynchronizationContextWatchdogTests
{
    [Fact]
    public void Watchdog_ReportsStuckWorkItem()
    {
        using var context = new SingleThreadSynchronizationContext();
        using var watchdog = new SingleThreadSynchronizationContextWatchdog(context, 100.Milliseconds(), 20.Milliseconds());

        var receiver = new LatestMessageReceiver<SingleThreadSynchronizationContextStall>();
        watchdog.ConnectTo(receiver);

        var blocking = new ManualResetEventSlim();
        context.Post(_ => blocking.Wait(5000), null);
        context.Post(_ => { }, null);

        Assert.True(receiver.WaitHandle.Wait(3.Seconds()));
        Assert.True(receiver.TryReceive(out var stall));
        Assert.Same(context, stall.Context);
        Assert.True(stall.StallDuration >= 100.Milliseconds());
        Assert.True(stall.QueuedWorkItemsCount >= 1);

        blocking.Set();
    }

    [Fact]
    public void Watchdog_DoesNotReportWhileWorkProgresses()
    {
        using var context = new SingleThreadSynchronizationContext();
        using var watchdog = new SingleThreadSynchronizationContextWatchdog(context, 100.Milliseconds(), 20.Milliseconds());

        var receiver = new LatestMessageReceiver<SingleThreadSynchronizationContextStall>();
        watchdog.ConnectTo(receiver);

        var end = AppTime.Now + 500.Milliseconds();
        while (AppTime.Now < end)
        {
            context.Post(_ => { }, null);
            Thread.Sleep(10);
        }

        Assert.True(receiver.IsEmpty);
    }
}
