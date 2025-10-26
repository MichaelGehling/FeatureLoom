using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class MessageFlowExtensions_Receive_Tests
{
    [Fact]
    public void TryReceiveRequest_extracts_payload_and_id()
    {
        var recv = new QueueReceiver<IRequestMessage<int>>();
        recv.Post(new RequestMessage<int>(1234, requestId: 555));

        var ok = recv.TryReceiveRequest(out int payload, out long id);

        Assert.True(ok);
        Assert.Equal(1234, payload);
        Assert.Equal(555, id);
    }

    [Fact]
    public void TryReceive_with_cancellation_returns_false_when_cancelled()
    {
        var recv = new QueueReceiver<string>();
        using var cts = new CancellationTokenSource(100);

        var ok = recv.TryReceive(out string _, cts.Token);

        Assert.False(ok);
    }

    [Fact]
    public void TryPeek_with_cancellation_returns_false_when_cancelled()
    {
        var recv = new QueueReceiver<int>();
        using var cts = new CancellationTokenSource(100);

        var ok = recv.TryPeek(out int _, cts.Token);

        Assert.False(ok);
    }

    [Fact]
    public void TryReceive_with_timeout_returns_false_when_no_item()
    {
        var recv = new QueueReceiver<int>();
        var sw = Stopwatch.StartNew();

        var ok = recv.TryReceive(out int _, TimeSpan.FromMilliseconds(150));

        sw.Stop();
        Assert.False(ok);
        Assert.InRange(sw.ElapsedMilliseconds, 100, 1500);
    }

    [Fact]
    public void TryPeek_with_timeout_returns_false_when_no_item()
    {
        var recv = new QueueReceiver<int>();
        var sw = Stopwatch.StartNew();

        var ok = recv.TryPeek(out int _, TimeSpan.FromMilliseconds(150));

        sw.Stop();
        Assert.False(ok);
        Assert.InRange(sw.ElapsedMilliseconds, 100, 1500);
    }

    [Fact]
    public async Task TryReceiveAsync_with_cancellation_returns_false_when_cancelled()
    {
        var recv = new QueueReceiver<string>();
        using var cts = new CancellationTokenSource(100);

        var (ok, value) = await recv.TryReceiveAsync(cts.Token);

        Assert.False(ok);
        Assert.Null(value);
    }

    [Fact]
    public async Task TryReceiveAsync_returns_true_when_item_available()
    {
        var recv = new QueueReceiver<string>();
        recv.Post("x");

        var (ok, value) = await recv.TryReceiveAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.Equal("x", value);
    }

    [Fact]
    public async Task TryPeekAsync_with_timeout_returns_false_when_no_item()
    {
        var recv = new QueueReceiver<int>();

        var (ok, value) = await recv.TryPeekAsync(TimeSpan.FromMilliseconds(150));

        Assert.False(ok);
        Assert.Equal(default, value);
    }

    [Fact]
    public void ReceiveAll_returns_all_items_and_clears()
    {
        var recv = new QueueReceiver<int>();
        recv.Post(1);
        recv.Post(2);
        recv.Post(3);

        var slice = recv.ReceiveAll();

        Assert.Equal(3, slice.Count);
        Assert.Equal(new[] { 1, 2, 3 }, slice.ToArray());
        Assert.True(recv.IsEmpty);
    }

    [Fact]
    public void PeekAll_returns_all_items_without_removing()
    {
        var recv = new QueueReceiver<int>();
        recv.Post(10);
        recv.Post(20);

        var slice = recv.PeekAll();

        Assert.Equal(2, slice.Count);
        Assert.Equal(new[] { 10, 20 }, slice.ToArray());
        Assert.False(recv.IsEmpty);
        Assert.Equal(2, recv.Count);
    }
}