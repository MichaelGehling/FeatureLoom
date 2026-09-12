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
        var reported = new ManualResetEventSlim();
        var recovered = new ManualResetEventSlim();
        SingleThreadSynchronizationContext.WatchdogEvent stall = default;
        context.EnableWatchdog(value =>
        {
            if (value.State == SingleThreadSynchronizationContext.StallState.Stalled)
            {
                stall = value;
                reported.Set();
            }
            else
            {
                recovered.Set();
            }
        }, 100.Milliseconds(), 20.Milliseconds());

        var blocking = new ManualResetEventSlim();
        context.Post(_ => blocking.Wait(5000), null);
        context.Post(_ => { }, null);

        Assert.True(reported.Wait(3.Seconds()));
        Assert.Same(context, stall.Context);
        Assert.Equal(SingleThreadSynchronizationContext.StallState.Stalled, stall.State);
        Assert.True(stall.StallDuration >= 100.Milliseconds());
        Assert.True(stall.QueuedWorkItemsCount >= 1);

        blocking.Set();
        Assert.True(recovered.Wait(1.Seconds()));
    }

    [Fact]
    public void Watchdog_DoesNotReportWhileLifeSignsAreReported()
    {
        using var context = new SingleThreadSynchronizationContext();
        int reports = 0;
        context.EnableWatchdog(_ => reports++, 100.Milliseconds(), 20.Milliseconds());

        var finished = new ManualResetEventSlim();
        context.Post(_ =>
        {
            var end = AppTime.Now + 500.Milliseconds();
            while (AppTime.Now < end)
            {
                SingleThreadSynchronizationContext.ReportLifeSign();
                Thread.Sleep(10);
            }
            finished.Set();
        }, null);

        Assert.True(finished.Wait(2.Seconds()));
        Assert.Equal(0, Volatile.Read(ref reports));
    }

    [Fact]
    public void Watchdog_ReportsRecoveryWhenLifeSignsResume()
    {
        using var context = new SingleThreadSynchronizationContext();
        var stalled = new ManualResetEventSlim();
        var recovered = new ManualResetEventSlim();
        context.EnableWatchdog(report =>
        {
            if (report.State == SingleThreadSynchronizationContext.StallState.Stalled) stalled.Set();
            else recovered.Set();
        }, 100.Milliseconds(), 20.Milliseconds(), 10.Seconds());

        var resumeLifeSigns = new ManualResetEventSlim();
        var stop = new ManualResetEventSlim();
        context.Post(_ =>
        {
            while (!stop.IsSet)
            {
                if (resumeLifeSigns.IsSet) SingleThreadSynchronizationContext.ReportLifeSign();
                Thread.Sleep(10);
            }
        }, null);

        Assert.True(stalled.Wait(2.Seconds()));
        resumeLifeSigns.Set();
        Assert.True(recovered.Wait(1.Seconds()));
        stop.Set();
    }

    [Fact]
    public void Watchdog_CanBeEnabledFromContextThreadAndReplaced()
    {
        using var context = new SingleThreadSynchronizationContext();
        int oldReports = 0;
        int newReports = 0;
        var reported = new ManualResetEventSlim();
        var configured = new ManualResetEventSlim();

        context.EnableWatchdog(_ => oldReports++, 10.Seconds());
        context.Post(_ =>
        {
            SingleThreadSynchronizationContext.EnableWatchdogForCurrentThread(_ =>
            {
                Interlocked.Increment(ref newReports);
                reported.Set();
            }, 100.Milliseconds(), 20.Milliseconds());
            configured.Set();
        }, null);
        Assert.True(configured.Wait(1.Seconds()));

        context.Post(_ => Thread.Sleep(500), null);

        Assert.True(reported.Wait(2.Seconds()));
        Assert.Equal(0, Volatile.Read(ref oldReports));
        Assert.True(Volatile.Read(ref newReports) > 0);
    }
}
