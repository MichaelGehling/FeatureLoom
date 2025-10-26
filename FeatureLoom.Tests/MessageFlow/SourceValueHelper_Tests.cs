using FeatureLoom.Time;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class SourceValueHelper_Tests
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

    [Fact]
    public void Forward_by_value_calls_sinks_in_order()
    {
        var helper = new SourceValueHelper();
        var log = new List<string>();
        var a = new RecordingSink("A", log);
        var b = new RecordingSink("B", log);

        helper.ConnectTo(a);
        helper.ConnectTo(b);

        helper.Forward("x");
        helper.Forward(123);

        Assert.Equal(new[]
        {
            "A:x","B:x",
            "A:123","B:123"
        }, log);
    }

    [Fact]
    public void Forward_by_ref_calls_sinks_in_order()
    {
        var helper = new SourceValueHelper();
        var log = new List<string>();
        var a = new RecordingSink("A", log);
        var b = new RecordingSink("B", log);

        helper.ConnectTo(a);
        helper.ConnectTo(b);

        var value = 42;
        helper.Forward(in value);
        value = 7;
        helper.Forward(in value);

        Assert.Equal(new[]
        {
            "A:42","B:42",
            "A:7","B:7"
        }, log);
    }

    [Fact]
    public async Task ForwardAsync_is_sequential_and_defers_second_until_first_completes()
    {
        var helper = new SourceValueHelper();
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
        // At this point, A returned a pending task, so B should not have been invoked yet.
        Assert.Equal(0, Volatile.Read(ref sinkBAsyncCalls));

        tcs.SetResult(true);
        await sendTask;

        Assert.Equal(1, Volatile.Read(ref sinkBAsyncCalls));
        Assert.Equal(new[] { "A:msg", "B:msg" }, log);
    }

    [Fact]
    public async Task ForwardAsync_returns_last_pending_task_directly()
    {
        var helper = new SourceValueHelper();
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
        Assert.Equal(new[] { "A:X" }, log); // B logged only after its task completes, but we don't log in async behavior
    }

    [Fact]
    public void GetConnectedSinks_filters_invalid_and_prunes()
    {
        var helper = new SourceValueHelper();
        var strong = new RecordingSink("strong", new List<string>());

        helper.ConnectTo(strong);
        CreateAndConnectWeak(ref helper);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var connected = helper.GetConnectedSinks();
        Assert.Single(connected);
        Assert.Same(strong, connected[0]);

        Assert.Equal(1, helper.CountConnectedSinks);
    }

    [Fact]
    public void DisconnectFrom_and_DisconnectAll_work()
    {
        var helper = new SourceValueHelper();
        var a = new RecordingSink("A", new List<string>());
        var b = new RecordingSink("B", new List<string>());

        helper.ConnectTo(a);
        helper.ConnectTo(b);
        Assert.True(helper.IsConnected(a));
        Assert.True(helper.IsConnected(b));

        helper.DisconnectFrom(a);
        Assert.False(helper.IsConnected(a));
        Assert.True(helper.IsConnected(b));
        Assert.Equal(1, helper.CountConnectedSinks);

        helper.DisconnectAll();
        Assert.Equal(0, helper.CountConnectedSinks);
        Assert.False(helper.IsConnected(b));
    }

    private static void CreateAndConnectWeak(ref SourceValueHelper helper)
    {
        var log = new List<string>();
        var weakSink = new RecordingSink("weak", log);
        helper.ConnectTo(weakSink, weakReference: true);
        // After method returns, no strong references to weakSink remain (except weak ref in helper).
    }
}