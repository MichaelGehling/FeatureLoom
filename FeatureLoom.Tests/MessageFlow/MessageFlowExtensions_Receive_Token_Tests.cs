using FeatureLoom.MessageFlow;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class MessageFlowExtensions_Receive_Token_Tests
{
    [Fact]
    public async Task TryPeekAsync_token_cancels_and_returns_false()
    {
        var recv = new QueueReceiver<int>();
        using var cts = new CancellationTokenSource(120);

        var (ok, value) = await recv.TryPeekAsync(cts.Token);

        Assert.False(ok);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryReceive_timeout_and_token_returns_false_when_cancelled()
    {
        var recv = new QueueReceiver<int>();
        using var cts = new CancellationTokenSource(100);

        var ok = recv.TryReceive(out int _, TimeSpan.FromSeconds(5), cts.Token);

        Assert.False(ok);
    }

    [Fact]
    public void TryPeek_timeout_and_token_returns_false_when_cancelled()
    {
        var recv = new QueueReceiver<int>();
        using var cts = new CancellationTokenSource(100);

        var ok = recv.TryPeek(out int _, TimeSpan.FromSeconds(5), cts.Token);

        Assert.False(ok);
    }

    [Fact]
    public async Task TryReceiveAsync_timeout_and_token_returns_false_when_cancelled()
    {
        var recv = new QueueReceiver<string>();
        using var cts = new CancellationTokenSource(100);

        var (ok, value) = await recv.TryReceiveAsync(TimeSpan.FromSeconds(5), cts.Token);

        Assert.False(ok);
        Assert.Null(value);
    }

    [Fact]
    public async Task TryPeekAsync_timeout_and_token_returns_false_when_cancelled()
    {
        var recv = new QueueReceiver<int>();
        using var cts = new CancellationTokenSource(100);

        var (ok, value) = await recv.TryPeekAsync(TimeSpan.FromSeconds(5), cts.Token);

        Assert.False(ok);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryReceive_timeout_and_token_returns_true_when_item_available()
    {
        var recv = new QueueReceiver<int>();
        recv.Post(42);

        var ok = recv.TryReceive(out int msg, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(42, msg);
    }

    [Fact]
    public void TryPeek_timeout_and_token_returns_true_when_item_available()
    {
        var recv = new QueueReceiver<int>();
        recv.Post(77);

        var ok = recv.TryPeek(out int msg, TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(77, msg);
    }

    [Fact]
    public async Task TryReceiveAsync_timeout_and_token_returns_true_when_item_available()
    {
        var recv = new QueueReceiver<string>();
        recv.Post("x");

        var (ok, value) = await recv.TryReceiveAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.True(ok);
        Assert.Equal("x", value);
    }

    [Fact]
    public async Task TryPeekAsync_timeout_and_token_returns_true_when_item_available()
    {
        var recv = new QueueReceiver<int>();
        recv.Post(9);

        var (ok, value) = await recv.TryPeekAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(9, value);
    }
}