using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.MessageFlow;
using Xunit;

namespace FeatureLoom.MessageFlow;

// Collecting sink to assert forwarded messages
sealed class CollectingSink<T> : IMessageSink<T>
{
    private readonly System.Collections.Generic.List<T> items = new System.Collections.Generic.List<T>();
    private readonly object gate = new object();
    public Type ConsumedMessageType => typeof(T);

    public void Post<M>(in M message)
    {
        if (message is T t) { lock (gate) items.Add(t); }
    }

    public void Post<M>(M message)
    {
        if (message is T t) { lock (gate) items.Add(t); }
    }

    public Task PostAsync<M>(M message)
    {
        if (message is T t) { lock (gate) items.Add(t); }
        return Task.CompletedTask;
    }

    public T[] Snapshot()
    {
        lock (gate) return items.ToArray();
    }
}

public class MessageLogReaderTests
{
    [Fact]
    public async Task Synchronous_Forwards_All_From_MessageLog()
    {
        var log = new MessageLog<int>(10);
        foreach (var x in Enumerable.Range(1, 5)) log.Add(x); // ids 0..4

        var reader = new MessageLogReader<int>(log, ForwardingMethod.Synchronous);
        var sink = new CollectingSink<int>();
        reader.ConnectTo(sink);

        using var cts = new CancellationTokenSource();
        var runTask = reader.Run(cts.Token);

        await Task.Delay(100);
        cts.Cancel();
        await runTask;

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, sink.Snapshot());
    }

    [Fact]
    public async Task SynchronousByRef_Forwards_All_From_MessageLog()
    {
        var log = new MessageLog<string>(10);
        foreach (var s in new[] { "A", "B", "C" }) log.Add(s);

        var reader = new MessageLogReader<string>(log, ForwardingMethod.SynchronousByRef);
        var sink = new CollectingSink<string>();
        reader.ConnectTo(sink);

        using var cts = new CancellationTokenSource();
        var runTask = reader.Run(cts.Token);

        await Task.Delay(100);
        cts.Cancel();
        await runTask;

        Assert.Equal(new[] { "A", "B", "C" }, sink.Snapshot());
    }

    [Fact]
    public async Task Asynchronous_Forwards_All_From_MessageLog()
    {
        var log = new MessageLog<int>(10);
        foreach (var x in Enumerable.Range(10, 4)) log.Add(x); // 10..13

        var reader = new MessageLogReader<int>(log, ForwardingMethod.Asynchronous);
        var sink = new CollectingSink<int>();
        reader.ConnectTo(sink);

        using var cts = new CancellationTokenSource();
        var runTask = reader.Run(cts.Token);

        await Task.Delay(100);
        cts.Cancel();
        await runTask;

        Assert.Equal(new[] { 10, 11, 12, 13 }, sink.Snapshot());
    }

    [Fact]
    public async Task LateStart_Aligns_To_MessageLog_OldestAvailable()
    {
        var log = new MessageLog<int>(5);
        foreach (var x in Enumerable.Range(0, 7)) log.Add(x); // overflow -> buffer holds 2..6

        var reader = new MessageLogReader<int>(log, ForwardingMethod.Synchronous);
        var sink = new CollectingSink<int>();
        reader.ConnectTo(sink);

        using var cts = new CancellationTokenSource();
        var runTask = reader.Run(cts.Token);

        await Task.Delay(100);
        cts.Cancel();
        await runTask;

        Assert.Equal(new[] { 2, 3, 4, 5, 6 }, sink.Snapshot());
    }

    [Fact]
    public async Task ContinuesReading_NewlyAdded_From_MessageLog()
    {
        var log = new MessageLog<int>(10);
        var reader = new MessageLogReader<int>(log, ForwardingMethod.Synchronous);
        var sink = new CollectingSink<int>();
        reader.ConnectTo(sink);

        using var cts = new CancellationTokenSource();
        var runTask = reader.Run(cts.Token);

        foreach (var x in Enumerable.Range(1, 3)) log.Add(x);
        await Task.Delay(50);
        foreach (var x in Enumerable.Range(4, 3)) log.Add(x); // 4,5,6

        await Task.Delay(100);
        cts.Cancel();
        await runTask;

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, sink.Snapshot());
    }

    [Fact]
    public async Task OverflowWhileRunning_SkipsTo_MessageLog_OldestAvailable()
    {
        var log = new MessageLog<int>(3);
        var reader = new MessageLogReader<int>(log, ForwardingMethod.Synchronous);
        var sink = new CollectingSink<int>();
        reader.ConnectTo(sink);

        using var cts = new CancellationTokenSource();
        var runTask = reader.Run(cts.Token);

        log.Add(1); // id0
        log.Add(2); // id1
        await Task.Delay(25);
        log.Add(3); // id2
        log.Add(4); // id3 -> overflow, oldest available becomes 1

        await Task.Delay(100);
        cts.Cancel();
        await runTask;

        var received = sink.Snapshot();
        Assert.True(
            received.SequenceEqual(new[] { 1, 2, 3, 4 }) || received.SequenceEqual(new[] { 2, 3, 4 }),
            "Expected contiguous available messages; oldest may be dropped upon overflow.");
    }

    [Fact]
    public async Task Run_Throws_WhenAlreadyRunning()
    {
        var log = new MessageLog<int>(2);
        var reader = new MessageLogReader<int>(log, ForwardingMethod.Synchronous);

        using var cts1 = new CancellationTokenSource();
        var t1 = reader.Run(cts1.Token);

        await Task.Delay(10);

        using var cts2 = new CancellationTokenSource();
        await Assert.ThrowsAsync<Exception>(() => reader.Run(cts2.Token));

        cts1.Cancel();
        await t1;
    }
}