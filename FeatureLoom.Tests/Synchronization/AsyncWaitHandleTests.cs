using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Synchronization;

public class AsyncWaitHandleTests
{
    private const int TestTimeoutMs = 5000;

    #region FromTask / NoWaitingHandle / WouldWait

    [Fact]
    public void FromTask_CompletedTask_ReturnsNoWaitingHandle()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var handle = AsyncWaitHandle.FromTask(Task.CompletedTask);

        Assert.Same(AsyncWaitHandle.NoWaitingHandle, handle);
        Assert.False(handle.WouldWait());
    }

    [Fact]
    public void FromTask_IncompleteTask_ReturnsNewHandle()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        var handle = AsyncWaitHandle.FromTask(tcs.Task);

        Assert.NotSame(AsyncWaitHandle.NoWaitingHandle, handle);
        Assert.True(handle.WouldWait());

        tcs.SetResult(true);
        Assert.False(handle.WouldWait());
    }

    [Fact]
    public void ImplicitConversion_FromTask_WorksCorrectly()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;

        Assert.True(handle.WouldWait());
        tcs.SetResult(true);
        Assert.False(handle.WouldWait());
    }

    #endregion

    #region Instance Wait methods

    [Fact]
    public void Wait_CompletedTask_ReturnsTrueImmediately()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var handle = AsyncWaitHandle.FromTask(Task.CompletedTask);

        Assert.True(handle.Wait());
    }

    [Fact]
    public void Wait_SignalledBeforeTimeout_ReturnsTrue()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;

        Task.Run(() => { Thread.Sleep(20); tcs.SetResult(true); });

        Assert.True(handle.Wait(2.Seconds()));
    }

    [Fact]
    public void Wait_TimeoutElapsed_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;

        Assert.False(handle.Wait(TimeSpan.FromMilliseconds(30)));
    }

    [Fact]
    public void Wait_CancellationToken_ReturnsFalseWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;
        var cts = new CancellationTokenSource(30);

        bool result = handle.Wait(cts.Token);

        Assert.False(result);
    }

    [Fact]
    public void Wait_TimeoutAndCancellationToken_ReturnsTrueWhenSignalled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;

        Task.Run(() => { Thread.Sleep(20); tcs.SetResult(true); });

        Assert.True(handle.Wait(2.Seconds(), CancellationToken.None));
    }

    [Fact]
    public void Wait_TimeoutAndCancellation_ReturnsFalseOnTimeout()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;

        Assert.False(handle.Wait(TimeSpan.FromMilliseconds(30), CancellationToken.None));
    }

    #endregion

    #region Instance WaitAsync methods

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAsync_CompletedTask_ReturnsTrueImmediately()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var handle = AsyncWaitHandle.FromTask(Task.CompletedTask);

        Assert.True(await handle.WaitAsync());
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAsync_SignalledBeforeTimeout_ReturnsTrue()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;

        _ = Task.Run(async () => { await Task.Delay(20); tcs.SetResult(true); });

        Assert.True(await handle.WaitAsync(2.Seconds()));
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAsync_TimeoutElapsed_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;

        Assert.False(await handle.WaitAsync(TimeSpan.FromMilliseconds(30)));
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAsync_CancellationToken_ReturnsFalseWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;
        var cts = new CancellationTokenSource(30);

        Assert.False(await handle.WaitAsync(cts.Token));
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAsync_TimeoutAndCancellation_ReturnsTrueWhenSignalled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;

        _ = Task.Run(async () => { await Task.Delay(20); tcs.SetResult(true); });

        Assert.True(await handle.WaitAsync(2.Seconds(), CancellationToken.None));
    }

    #endregion

    #region TryConvertToWaitHandle

    [Fact]
    public void TryConvertToWaitHandle_CompletedTask_ReturnsTrue()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var handle = AsyncWaitHandle.FromTask(Task.CompletedTask);

        Assert.True(handle.TryConvertToWaitHandle(out WaitHandle wh));
        Assert.NotNull(wh);
    }

    [Fact]
    public void TryConvertToWaitHandle_IncompleteTask_ReturnsHandleThatSetsOnCompletion()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        AsyncWaitHandle handle = tcs.Task;

        Assert.True(handle.TryConvertToWaitHandle(out WaitHandle wh));
        Assert.NotNull(wh);
        Assert.False(wh.WaitOne(0));

        tcs.SetResult(true);
        Assert.True(wh.WaitOne(1.Seconds()));
    }

    #endregion

    #region Static WaitAll

    [Fact]
    public void WaitAll_AllAlreadySignalled_ReturnsTrueImmediately()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var h1 = AsyncWaitHandle.FromTask(Task.CompletedTask);
        var h2 = AsyncWaitHandle.FromTask(Task.CompletedTask);

        Assert.True(AsyncWaitHandle.WaitAll(h1, h2));
    }

    [Fact]
    public void WaitAll_SignalledBeforeTimeout_ReturnsTrue()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h1 = AsyncWaitHandle.FromTask(tcs1.Task);
        IAsyncWaitHandle h2 = AsyncWaitHandle.FromTask(tcs2.Task);

        Task.Run(() => { Thread.Sleep(20); tcs1.SetResult(true); tcs2.SetResult(true); });

        Assert.True(AsyncWaitHandle.WaitAll(2.Seconds(), h1, h2));
    }

    [Fact]
    public void WaitAll_TimeoutElapsed_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);

        Assert.False(AsyncWaitHandle.WaitAll(TimeSpan.FromMilliseconds(30), h));
    }

    [Fact]
    public void WaitAll_CancellationToken_ReturnsFalseWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);
        var cts = new CancellationTokenSource(30);

        Assert.False(AsyncWaitHandle.WaitAll(cts.Token, h));
    }

    [Fact]
    public void WaitAll_TimeoutAndCancellation_ReturnsFalseOnTimeout()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);

        Assert.False(AsyncWaitHandle.WaitAll(TimeSpan.FromMilliseconds(30), CancellationToken.None, h));
    }

    [Fact]
    public void WaitAll_TimeoutAndCancellation_ReturnsFalseWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);
        var cts = new CancellationTokenSource(30);

        Assert.False(AsyncWaitHandle.WaitAll(2.Seconds(), cts.Token, h));
    }

    #endregion

    #region Static WaitAllAsync

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAllAsync_AllAlreadySignalled_ReturnsTrueImmediately()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var h1 = AsyncWaitHandle.FromTask(Task.CompletedTask);
        var h2 = AsyncWaitHandle.FromTask(Task.CompletedTask);

        Assert.True(await AsyncWaitHandle.WaitAllAsync(h1, h2));
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAllAsync_SignalledBeforeTimeout_ReturnsTrue()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h1 = AsyncWaitHandle.FromTask(tcs1.Task);
        IAsyncWaitHandle h2 = AsyncWaitHandle.FromTask(tcs2.Task);

        _ = Task.Run(async () => { await Task.Delay(20); tcs1.SetResult(true); tcs2.SetResult(true); });

        Assert.True(await AsyncWaitHandle.WaitAllAsync(2.Seconds(), h1, h2));
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAllAsync_TimeoutElapsed_ReturnsFalse()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);

        Assert.False(await AsyncWaitHandle.WaitAllAsync(TimeSpan.FromMilliseconds(30), h));
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAllAsync_CancellationToken_ReturnsFalseWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);
        var cts = new CancellationTokenSource(30);

        Assert.False(await AsyncWaitHandle.WaitAllAsync(cts.Token, h));
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAllAsync_TimeoutAndCancellation_ReturnsFalseOnTimeout()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);

        Assert.False(await AsyncWaitHandle.WaitAllAsync(TimeSpan.FromMilliseconds(30), CancellationToken.None, h));
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAllAsync_TimeoutAndCancellation_ReturnsFalseWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);
        var cts = new CancellationTokenSource(30);

        Assert.False(await AsyncWaitHandle.WaitAllAsync(2.Seconds(), cts.Token, h));
    }

    #endregion

    #region Static WaitAny

    [Fact]
    public void WaitAny_OneAlreadySignalled_ReturnsIndexImmediately()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h0 = AsyncWaitHandle.FromTask(tcs.Task);
        IAsyncWaitHandle h1 = AsyncWaitHandle.FromTask(Task.CompletedTask);

        int index = AsyncWaitHandle.WaitAny(h0, h1);

        Assert.Equal(1, index);
    }

    [Fact]
    public void WaitAny_SignalledBeforeTimeout_ReturnsCorrectIndex()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs0 = new TaskCompletionSource<bool>();
        var tcs1 = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h0 = AsyncWaitHandle.FromTask(tcs0.Task);
        IAsyncWaitHandle h1 = AsyncWaitHandle.FromTask(tcs1.Task);

        Task.Run(() => { Thread.Sleep(20); tcs1.SetResult(true); });

        int index = AsyncWaitHandle.WaitAny(2.Seconds(), h0, h1);

        Assert.Equal(1, index);
    }

    [Fact]
    public void WaitAny_TimeoutElapsed_ReturnsWaitTimeout()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);

        int index = AsyncWaitHandle.WaitAny(TimeSpan.FromMilliseconds(30), h);

        Assert.Equal(WaitHandle.WaitTimeout, index);
    }

    [Fact]
    public void WaitAny_CancellationToken_ReturnsWaitTimeoutWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);
        var cts = new CancellationTokenSource(30);

        int index = AsyncWaitHandle.WaitAny(cts.Token, h);

        Assert.Equal(WaitHandle.WaitTimeout, index);
    }

    [Fact]
    public void WaitAny_TimeoutAndCancellation_ReturnsWaitTimeoutOnTimeout()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);

        int index = AsyncWaitHandle.WaitAny(TimeSpan.FromMilliseconds(30), CancellationToken.None, h);

        Assert.Equal(WaitHandle.WaitTimeout, index);
    }

    [Fact]
    public void WaitAny_TimeoutAndCancellation_ReturnsWaitTimeoutWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);
        var cts = new CancellationTokenSource(30);

        int index = AsyncWaitHandle.WaitAny(2.Seconds(), cts.Token, h);

        Assert.Equal(WaitHandle.WaitTimeout, index);
    }

    #endregion

    #region Static WaitAnyAsync

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAnyAsync_OneAlreadySignalled_ReturnsIndexImmediately()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h0 = AsyncWaitHandle.FromTask(tcs.Task);
        IAsyncWaitHandle h1 = AsyncWaitHandle.FromTask(Task.CompletedTask);

        int index = await AsyncWaitHandle.WaitAnyAsync(h0, h1);

        Assert.Equal(1, index);
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAnyAsync_SignalledBeforeTimeout_ReturnsCorrectIndex()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs0 = new TaskCompletionSource<bool>();
        var tcs1 = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h0 = AsyncWaitHandle.FromTask(tcs0.Task);
        IAsyncWaitHandle h1 = AsyncWaitHandle.FromTask(tcs1.Task);

        _ = Task.Run(async () => { await Task.Delay(20); tcs1.SetResult(true); });

        int index = await AsyncWaitHandle.WaitAnyAsync(2.Seconds(), h0, h1);

        Assert.Equal(1, index);
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAnyAsync_TimeoutElapsed_ReturnsWaitTimeout()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);

        int index = await AsyncWaitHandle.WaitAnyAsync(TimeSpan.FromMilliseconds(30), h);

        Assert.Equal(WaitHandle.WaitTimeout, index);
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAnyAsync_CancellationToken_ReturnsWaitTimeoutWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);
        var cts = new CancellationTokenSource(30);

        int index = await AsyncWaitHandle.WaitAnyAsync(cts.Token, h);

        Assert.Equal(WaitHandle.WaitTimeout, index);
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAnyAsync_TimeoutAndCancellation_ReturnsWaitTimeoutOnTimeout()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);

        int index = await AsyncWaitHandle.WaitAnyAsync(TimeSpan.FromMilliseconds(30), CancellationToken.None, h);

        Assert.Equal(WaitHandle.WaitTimeout, index);
    }

    [Fact(Timeout = TestTimeoutMs)]
    public async Task WaitAnyAsync_TimeoutAndCancellation_ReturnsWaitTimeoutWhenCancelled()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>();
        IAsyncWaitHandle h = AsyncWaitHandle.FromTask(tcs.Task);
        var cts = new CancellationTokenSource(30);

        int index = await AsyncWaitHandle.WaitAnyAsync(2.Seconds(), cts.Token, h);

        Assert.Equal(WaitHandle.WaitTimeout, index);
    }

    #endregion
}
