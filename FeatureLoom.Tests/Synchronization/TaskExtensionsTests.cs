using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Synchronization;

public class TaskExtensionsTests
{
    [Fact]
    public async Task TryWaitAsyncReturnsTrueWhenTaskCompletes()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = tcs.Task.TryWaitAsync(10.Seconds());
        tcs.SetResult(true);
        Assert.True(await waiting);

        Assert.True(await Task.CompletedTask.TryWaitAsync());
        Assert.True(await Task.CompletedTask.TryWaitAsync(10.Seconds()));
        Assert.True(await Task.CompletedTask.TryWaitAsync(CancellationToken.None));
        Assert.True(await Task.CompletedTask.TryWaitAsync(10.Seconds(), CancellationToken.None));
    }

    [Fact]
    public async Task TryWaitAsyncReturnsFalseOnTimeout()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.False(await tcs.Task.TryWaitAsync(50.Milliseconds()));
        Assert.False(await tcs.Task.TryWaitAsync(50.Milliseconds(), CancellationToken.None));
        Assert.False(await tcs.Task.TryWaitAsync(TimeSpan.Zero));
        Assert.False(await tcs.Task.TryWaitAsync(TimeSpan.Zero, CancellationToken.None));
    }

    [Fact]
    public async Task TryWaitAsyncTreatsInfiniteTimeSpanAsInfinite()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var withoutToken = tcs.Task.TryWaitAsync(Timeout.InfiniteTimeSpan);
        var withToken = tcs.Task.TryWaitAsync(Timeout.InfiniteTimeSpan, CancellationToken.None);

        Assert.False(withoutToken.IsCompleted);
        Assert.False(withToken.IsCompleted);

        tcs.SetResult(true);

        Assert.True(await withoutToken);
        Assert.True(await withToken);
    }

    [Fact]
    public void TryWaitAsyncRejectsInvalidTimeoutsSynchronously()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tooLarge = TimeSpan.FromMilliseconds((double)uint.MaxValue);

        // Thrown synchronously, not returned as a faulted task, consistent with Task.Delay/Task.WaitAsync.
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = tcs.Task.TryWaitAsync(TimeSpan.FromMilliseconds(-2)); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = tcs.Task.TryWaitAsync(TimeSpan.FromSeconds(-1)); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = tcs.Task.TryWaitAsync(tooLarge); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = tcs.Task.TryWaitAsync(TimeSpan.FromMilliseconds(-2), CancellationToken.None); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = tcs.Task.TryWaitAsync(tooLarge, CancellationToken.None); });

        // Validation happens even for already completed tasks, so the contract does not depend on timing.
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = Task.CompletedTask.TryWaitAsync(TimeSpan.FromSeconds(-1)); });
    }

    [Fact]
    public async Task TryWaitAsyncReturnsFalseOnCancellation()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        var withoutTimeout = tcs.Task.TryWaitAsync(cts.Token);
        var withTimeout = tcs.Task.TryWaitAsync(10.Seconds(), cts.Token);

        cts.Cancel();

        Assert.False(await withoutTimeout);
        Assert.False(await withTimeout);

        using var cancelledCts = new CancellationTokenSource();
        cancelledCts.Cancel();
        Assert.False(await tcs.Task.TryWaitAsync(cancelledCts.Token));
        Assert.False(await tcs.Task.TryWaitAsync(10.Seconds(), cancelledCts.Token));
    }

    [Fact]
    public async Task TryWaitAsyncNeverThrowsOnFaultedOrCancelledTask()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var faulted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitingForFault = faulted.Task.TryWaitAsync();
        var waitingForFaultWithToken = faulted.Task.TryWaitAsync(CancellationToken.None);
        var waitingForFaultWithTimeout = faulted.Task.TryWaitAsync(10.Seconds());
        var waitingForFaultWithBoth = faulted.Task.TryWaitAsync(10.Seconds(), CancellationToken.None);
        faulted.SetException(new InvalidOperationException("test"));

        Assert.False(await waitingForFault);
        Assert.False(await waitingForFaultWithToken);
        Assert.False(await waitingForFaultWithTimeout);
        Assert.False(await waitingForFaultWithBoth);

        // Already faulted / cancelled tasks
        Assert.False(await faulted.Task.TryWaitAsync());
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancelled.SetCanceled();
        Assert.False(await cancelled.Task.TryWaitAsync());
        Assert.False(await cancelled.Task.TryWaitAsync(10.Seconds()));
        Assert.False(await cancelled.Task.TryWaitAsync(CancellationToken.None));
        Assert.False(await cancelled.Task.TryWaitAsync(10.Seconds(), CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryWaitAsyncDoesNotRunContinuationsInsideCancel(bool withTimeout)
    {
        using var testContext = TestHelper.PrepareTestContext();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();

        int continuationThreadId = -1;
        var done = new ManualResetEventSlim(false);

        _ = Task.Run(async () =>
        {
            if (withTimeout) await tcs.Task.TryWaitAsync(10.Seconds(), cts.Token);
            else await tcs.Task.TryWaitAsync(cts.Token);
            continuationThreadId = Thread.CurrentThread.ManagedThreadId;
            done.Set();
        });

        Thread.Sleep(50);
        int cancellingThreadId = Thread.CurrentThread.ManagedThreadId;
        cts.Cancel();

        Assert.True(done.Wait(10.Seconds()));
        Assert.NotEqual(cancellingThreadId, continuationThreadId);
    }

    [Fact]
    public void CancellationTokenAsTaskDoesNotRunContinuationsInsideCancel()
    {
        using var testContext = TestHelper.PrepareTestContext();

        using var cts = new CancellationTokenSource();
        var task = cts.Token.AsTask();

        int continuationThreadId = -1;
        var done = new ManualResetEventSlim(false);

        _ = task.ContinueWith(_ =>
        {
            continuationThreadId = Thread.CurrentThread.ManagedThreadId;
            done.Set();
        }, TaskContinuationOptions.ExecuteSynchronously);

        Thread.Sleep(50);
        int cancellingThreadId = Thread.CurrentThread.ManagedThreadId;
        cts.Cancel();

        Assert.True(done.Wait(10.Seconds()));
        Assert.NotEqual(cancellingThreadId, continuationThreadId);
    }

    [Fact]
    public async Task CancellationTokenAsTaskCompletesOnCancellation()
    {
        using var testContext = TestHelper.PrepareTestContext();

        using var cts = new CancellationTokenSource();
        var task = cts.Token.AsTask();
        Assert.False(task.IsCompleted);

        cts.Cancel();
        Assert.True(await task.TryWaitAsync(10.Seconds()));

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();
        Assert.True(alreadyCancelled.Token.AsTask().IsCompleted);

        Assert.False(CancellationToken.None.AsTask().IsCompleted);
    }

    [Fact]
    public void SuspendRemovesAndRestoresTheCurrentSynchronizationContext()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var original = new SynchronizationContext();
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(original);

            using (SynchronizationContext.Current.Suspend())
            {
                Assert.Null(SynchronizationContext.Current);
            }
            Assert.Same(original, SynchronizationContext.Current);

            // Even when called on a foreign context, the context that was current gets restored.
            using (new SynchronizationContext().Suspend())
            {
                Assert.Null(SynchronizationContext.Current);
            }
            Assert.Same(original, SynchronizationContext.Current);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void SuspendWithoutCurrentContextIsANoOp()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            using (SynchronizationContext.Current.Suspend())
            {
                Assert.Null(SynchronizationContext.Current);
            }
            Assert.Null(SynchronizationContext.Current);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void WaitForUnwrapsExceptionsByDefault()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var failing = Task.Run(() => throw new InvalidOperationException("test"));
        Assert.Throws<InvalidOperationException>(() => failing.WaitFor());

        var failingAgain = Task.Run(() => throw new InvalidOperationException("test"));
        Assert.Throws<AggregateException>(() => failingAgain.WaitFor(unwrapException: false));
    }

    // Resumes on the captured SynchronizationContext, which is what makes blocking on it deadlock-prone.
    private static async Task ResumeOnCapturedContextAsync()
    {
        await Task.Delay(20).ConfigureAwait(true);
    }

    [Fact]
    public void SuspendAroundTheInvocationPreventsSyncOverAsyncDeadlock()
    {
        using var testContext = TestHelper.PrepareTestContext();
        using var context = new SingleThreadSynchronizationContext();

        Exception error = null;
        var finished = new ManualResetEventSlim(false);

        context.Post(_ =>
        {
            try
            {
                // The async method must be STARTED inside the suspended region, because its continuation
                // captures SynchronizationContext.Current when the await is reached. Suspending only around
                // the blocking wait would be too late and would still deadlock.
                using (SynchronizationContext.Current.Suspend())
                {
                    ResumeOnCapturedContextAsync().WaitFor();
                }
                Assert.NotNull(SynchronizationContext.Current); // context must be restored afterwards
            }
            catch (Exception e)
            {
                error = e;
            }
            finished.Set();
        }, null);

        Assert.True(finished.Wait(10.Seconds()));
        Assert.Null(error);
    }

    [Fact]
    public void WaitForUsesGlobalUnwrapExceptionsDefault()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var original = AwaitConfig.UnwrapExceptionsByDefault;
        try
        {
            AwaitConfig.UnwrapExceptionsByDefault = true;
            Assert.Throws<InvalidOperationException>(() => FailingTask().WaitFor());
            Assert.Throws<InvalidOperationException>(() => FailingTaskOf<int>().WaitFor());

            AwaitConfig.UnwrapExceptionsByDefault = false;
            Assert.Throws<AggregateException>(() => FailingTask().WaitFor());
            Assert.Throws<AggregateException>(() => FailingTaskOf<int>().WaitFor());

            // An explicit argument still wins over the global default.
            Assert.Throws<InvalidOperationException>(() => FailingTask().WaitFor(true));
        }
        finally
        {
            AwaitConfig.UnwrapExceptionsByDefault = original;
        }

        static Task FailingTask() => Task.FromException(new InvalidOperationException("fail"));
        static Task<T> FailingTaskOf<T>() => Task.FromException<T>(new InvalidOperationException("fail"));
    }
}
