using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class DelayingForwarderTests2
{
    private static TimeSpan Tolerance = 5.Milliseconds();

    [Fact]
    public void BlockingMode_ForwardsAfterDelay_WhenSinkConnected()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var delay = 40.Milliseconds();
        var sender = new Sender<int>();
        var forwarder = new DelayingForwarder(delay, blocking: true);
        var sink = new LatestMessageReceiver<int>();
        sender.ConnectTo(forwarder).ConnectTo(sink);

        var sw = Stopwatch.StartNew();
        sender.Send(123);
        sw.Stop();

        Assert.True(sw.Elapsed >= delay - Tolerance, $"Elapsed {sw.Elapsed} < expected {delay}");
        Assert.True(sink.HasMessage);
        Assert.Equal(123, sink.LatestMessageOrDefault);
    }

    [Fact]
    public void BlockingMode_WaitsEvenWithoutSink()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var delay = 30.Milliseconds();
        var sender = new Sender<int>();
        var forwarder = new DelayingForwarder(delay, blocking: true);
        sender.ConnectTo(forwarder); // no sink attached after forwarder

        var sw = Stopwatch.StartNew();
        sender.Send(42); // Should still wait even without sinks (intended doc behavior)
        sw.Stop();

        Assert.True(sw.Elapsed >= delay - Tolerance, $"Elapsed {sw.Elapsed} < expected {delay}");
    }

    [Fact]
    public void NonBlockingMode_DelaysForwarding_WhenSinkConnected()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var delay = 50.Milliseconds();
        var sender = new Sender<string>();
        var forwarder = new DelayingForwarder(delay);
        var sink = new LatestMessageReceiver<string>();
        sender.ConnectTo(forwarder).ConnectTo(sink);

        sender.Send("hello");
        // Should not be available immediately
        Assert.False(sink.HasMessage);

        // Half of delay
        Assert.False(sink.WaitHandle.Wait(delay.Divide(2)));
        Assert.False(sink.HasMessage);

        // Full delay + tolerance
        Assert.True(sink.WaitHandle.Wait((delay + 20.Milliseconds())));
        Assert.True(sink.HasMessage);
        Assert.Equal("hello", sink.LatestMessageOrDefault);
    }

    [Fact]
    public void NonBlockingMode_NoSink_DoesNotWaitCurrently()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var delay = 40.Milliseconds();
        var sender = new Sender<int>();
        var forwarder = new DelayingForwarder(delay);
        sender.ConnectTo(forwarder); // no sink after forwarder

        var sw = Stopwatch.StartNew();
        sender.Send(777);
        sw.Stop();

        // Current implementation returns immediately (contradicts XML remark claiming delay always performed).
        Assert.True(sw.Elapsed < delay / 2, $"Elapsed {sw.Elapsed} unexpectedly >= delay {delay}");
    }

    [Fact]
    public void ZeroDelay_ImmediateForward_Blocking()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var sender = new Sender<int>();
        var forwarder = new DelayingForwarder(TimeSpan.Zero, blocking: true);
        var sink = new LatestMessageReceiver<int>();
        sender.ConnectTo(forwarder).ConnectTo(sink);

        var sw = Stopwatch.StartNew();
        sender.Send(1);
        sw.Stop();

        Assert.True(sw.Elapsed < 5.Milliseconds());
        Assert.True(sink.HasMessage);
        Assert.Equal(1, sink.LatestMessageOrDefault);
    }

    [Fact]
    public void ZeroDelay_ImmediateForward_NonBlocking()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var sender = new Sender<int>();
        var forwarder = new DelayingForwarder(TimeSpan.Zero);
        var sink = new LatestMessageReceiver<int>();
        sender.ConnectTo(forwarder).ConnectTo(sink);

        sender.Send(2);
        Assert.True(sink.WaitHandle.Wait(5.Milliseconds()));
        Assert.True(sink.HasMessage);
        Assert.Equal(2, sink.LatestMessageOrDefault);
    }

    [Fact]
    public void Cancellation_ShortensBlockingDelay_ButStillForwards()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var delay = 80.Milliseconds();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // already canceled
        var sender = new Sender<int>();
        var forwarder = new DelayingForwarder(delay, blocking: true, ct: cts.Token);
        var sink = new LatestMessageReceiver<int>();
        sender.ConnectTo(forwarder).ConnectTo(sink);

        var sw = Stopwatch.StartNew();
        sender.Send(999);
        sw.Stop();

        // Should be significantly less than intended delay because cancellation ends wait early.
        Assert.True(sw.Elapsed < delay / 4, $"Elapsed {sw.Elapsed} unexpectedly >= quarter of delay {delay}");
        Assert.True(sink.HasMessage);
        Assert.Equal(999, sink.LatestMessageOrDefault);
    }

    [Fact]
    public async Task Cancellation_ShortensAsyncDelay_ButStillForwards()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var delay = 70.Milliseconds();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var sender = new Sender<int>();
        var forwarder = new DelayingForwarder(delay, blocking: true, ct: cts.Token);
        var sink = new LatestMessageReceiver<int>();
        sender.ConnectTo(forwarder).ConnectTo(sink);

        var sw = Stopwatch.StartNew();
        await sender.SendAsync(5);
        sw.Stop();

        Assert.True(sw.Elapsed < delay / 4);
        Assert.True(sink.HasMessage);
        Assert.Equal(5, sink.LatestMessageOrDefault);
    }

    [Fact]
    public void PreciseDelay_Blocking_WaitsApproximatelyDelay()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var delay = 30.Milliseconds();
        var sender = new Sender<int>();
        var forwarder = new DelayingForwarder(delay, delayPrecisely: true, blocking: true);
        var sink = new LatestMessageReceiver<int>();
        sender.ConnectTo(forwarder).ConnectTo(sink);

        var sw = Stopwatch.StartNew();
        sender.Send(11);
        sw.Stop();

        Assert.True(sw.Elapsed >= delay - Tolerance);
        Assert.True(sink.HasMessage);
    }

    [Fact]
    public void NonBlocking_DoesNotBlockCaller()
    {
        using var ctx = TestHelper.PrepareTestContext();
        var delay = 60.Milliseconds();
        var sender = new Sender<int>();
        var forwarder = new DelayingForwarder(delay, blocking: false);
        var sink = new LatestMessageReceiver<int>();
        sender.ConnectTo(forwarder).ConnectTo(sink);

        var sw = Stopwatch.StartNew();
        sender.Send(321);
        sw.Stop();

        Assert.True(sw.Elapsed < 5.Milliseconds(), "Non-blocking send took too long.");
        Assert.False(sink.HasMessage);
        Assert.True(sink.WaitHandle.Wait((delay + 25.Milliseconds())));
        Assert.True(sink.HasMessage);
        Assert.Equal(321, sink.LatestMessageOrDefault);
    }
}