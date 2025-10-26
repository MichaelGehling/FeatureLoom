using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class TypedSourceValueHelper_Tests
{
    private sealed class RecordingSink : IMessageSink
    {
        private readonly string name;
        private readonly List<string> log;
        private readonly Func<object, Task> asyncBehavior;
        private readonly Action onAsyncCalled;

        public RecordingSink(string name, List<string> log, Func<object, Task> asyncBehavior = null, Action onAsyncCalled = null)
        {
            this.name = name;
            this.log = log;
            this.asyncBehavior = asyncBehavior;
            this.onAsyncCalled = onAsyncCalled;
        }

        public void Post<M>(in M message)
        {
            lock (log) log.Add($"{name}:{message}");
        }

        public void Post<M>(M message)
        {
            lock (log) log.Add($"{name}:{message}");
        }

        public Task PostAsync<M>(M message)
        {
            onAsyncCalled?.Invoke();
            if (asyncBehavior != null) return asyncBehavior(message);
            lock (log) log.Add($"{name}:{message}");
            return Task.CompletedTask;
        }
    }

    private sealed class TypedRecordingSink<TConsumed> : IMessageSink<TConsumed>
    {
        private readonly string name;
        private readonly List<string> log;
        private readonly Func<object, Task> asyncBehavior;

        public TypedRecordingSink(string name, List<string> log, Func<object, Task> asyncBehavior = null)
        {
            this.name = name;
            this.log = log;
            this.asyncBehavior = asyncBehavior;
        }

        public Type ConsumedMessageType => typeof(TConsumed);

        public void Post<M>(in M message)
        {
            lock (log) log.Add($"{name}:{message}");
        }

        public void Post<M>(M message)
        {
            lock (log) log.Add($"{name}:{message}");
        }

        public Task PostAsync<M>(M message)
        {
            if (asyncBehavior != null) return asyncBehavior(message);
            lock (log) log.Add($"{name}:{message}");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Forward_by_value_and_by_ref_calls_sinks_in_order()
    {
        var helper = new TypedSourceValueHelper<int>();
        var log = new List<string>();
        var a = new RecordingSink("A", log);
        var b = new RecordingSink("B", log);

        helper.ConnectTo(a);
        helper.ConnectTo(b);

        helper.Forward(10);
        var v = 20;
        helper.Forward(in v);

        Assert.Equal(new[]
        {
            "A:10","B:10",
            "A:20","B:20"
        }, log);
    }

    [Fact]
    public async Task ForwardAsync_is_sequential_and_defers_second_until_first_completes()
    {
        var helper = new TypedSourceValueHelper<string>();
        var log = new List<string>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int sinkBAsyncCalls = 0;

        var sinkA = new RecordingSink("A", log, msg =>
        {
            lock (log) log.Add($"A:{msg}");
            return tcs.Task;
        });
        var sinkB = new RecordingSink("B", log, msg =>
        {
            lock (log) log.Add($"B:{msg}");
            Interlocked.Increment(ref sinkBAsyncCalls);
            return Task.CompletedTask;
        });

        helper.ConnectTo(sinkA);
        helper.ConnectTo(sinkB);

        var sendTask = helper.ForwardAsync("msg");
        Assert.Equal(0, Volatile.Read(ref sinkBAsyncCalls));

        tcs.SetResult(true);
        await sendTask;

        Assert.Equal(1, Volatile.Read(ref sinkBAsyncCalls));
        Assert.Equal(new[] { "A:msg", "B:msg" }, log);
    }

    [Fact]
    public async Task ForwardAsync_returns_last_pending_task_directly()
    {
        var helper = new TypedSourceValueHelper<string>();
        var log = new List<string>();
        var last = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var sinkA = new RecordingSink("A", log); // completes synchronously
        var sinkB = new RecordingSink("B", log, _ => last.Task);

        helper.ConnectTo(sinkA);
        helper.ConnectTo(sinkB);

        var returned = helper.ForwardAsync("X");

        Assert.Same(last.Task, returned);
        last.SetResult(true);
        await returned;
        Assert.Equal(new[] { "A:X" }, log);
    }

    [Fact]
    public void ConnectTo_throws_for_incompatible_typed_sink()
    {
        var helper = new TypedSourceValueHelper<int>();
        var log = new List<string>();
        var incompatibleTypedSink = new TypedRecordingSink<string>("typed", log);

        Assert.Throws<Exception>(() => helper.ConnectTo(incompatibleTypedSink));
    }

    [Fact]
    public void ConnectTo_allows_compatible_typed_sink()
    {
        var helper = new TypedSourceValueHelper<int>();
        var log = new List<string>();
        var compatible1 = new TypedRecordingSink<object>("o", log);
        var compatible2 = new TypedRecordingSink<int>("i", log);

        var ex1 = Record.Exception(() => helper.ConnectTo(compatible1));
        var ex2 = Record.Exception(() => helper.ConnectTo(compatible2));

        Assert.Null(ex1);
        Assert.Null(ex2);
        Assert.Equal(2, helper.CountConnectedSinks);
    }

    [Fact]
    public void Weak_reference_sink_is_pruned_lazily()
    {
        var helper = new TypedSourceValueHelper<int>();
        var strong = new RecordingSink("strong", new List<string>());
        helper.ConnectTo(strong);

        CreateAndConnectWeak(ref helper);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Trigger pruning via GetConnectedSinks/CountConnectedSinks
        var sinks = helper.GetConnectedSinks();
        Assert.Single(sinks);
        Assert.Same(strong, sinks[0]);
        Assert.Equal(1, helper.CountConnectedSinks);
    }

    private static void CreateAndConnectWeak(ref TypedSourceValueHelper<int> helper)
    {
        var log = new List<string>();
        var weakSink = new RecordingSink("weak", log);
        helper.ConnectTo(weakSink, weakReference: true);
        // After method returns, no strong references to weakSink remain (except weak ref in helper).
    }
}