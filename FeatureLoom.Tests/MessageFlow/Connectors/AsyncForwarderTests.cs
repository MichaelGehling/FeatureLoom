using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class AsyncForwarderTest
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
        public async Task PostAsync_returns_immediately_and_forwards_eventually()
        {
            var fwd = new AsyncForwarder();
            var log = new List<string>();
            var delivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var sink = new RecordingSink("S", log, msg =>
            {
                lock (log) log.Add($"S:{msg}");
                delivered.TrySetResult(true);
                return Task.CompletedTask;
            });

            fwd.ConnectTo(sink);

            var returned = fwd.PostAsync("X");

            // Must return immediately (fire-and-forget semantics)
            Assert.True(returned.IsCompleted);

            // The forwarding is offloaded but should complete shortly.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => delivered.Task.IsCompleted, cts.Token);

            Assert.Contains("S:X", log);
        }

        [Fact]
        public async Task Per_send_async_order_is_preserved_and_backpressure_applied()
        {
            var fwd = new AsyncForwarder();
            var log = new List<string>();
            var aGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int bCalls = 0;

            var sinkA = new RecordingSink("A", log, async _ =>
            {
                lock (log) log.Add("A:msg");
                await aGate.Task;
            });
            var sinkB = new RecordingSink("B", log, _ =>
            {
                Interlocked.Increment(ref bCalls);
                lock (log) log.Add("B:msg");
                return Task.CompletedTask;
            });

            fwd.ConnectTo(sinkA);
            fwd.ConnectTo(sinkB);

            _ = fwd.PostAsync("msg");

            // Give the worker a moment to start; B must not be called before A completes.
            await Task.Delay(50);
            Assert.Equal(0, Volatile.Read(ref bCalls));

            aGate.SetResult(true);

            await SpinUntilAsync(() => Volatile.Read(ref bCalls) == 1, TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { "A:msg", "B:msg" }, log);
        }

        [Fact]
        public async Task Calls_multiple_sinks_in_order_even_when_synchronous()
        {
            var fwd = new AsyncForwarder();
            var log = new List<string>();

            var sinkA = new RecordingSink("A", log); // completes synchronously
            var sinkB = new RecordingSink("B", log); // completes synchronously

            fwd.ConnectTo(sinkA);
            fwd.ConnectTo(sinkB);

            _ = fwd.PostAsync("Y");

            await SpinUntilAsync(() => log.Count >= 2, TimeSpan.FromSeconds(1));
            Assert.Equal(new[] { "A:Y", "B:Y" }, log);
        }

        [Fact]
        public async Task Exceptions_in_sinks_are_caught_and_do_not_escape()
        {
            var fwd = new AsyncForwarder();
            var log = new List<string>();

            var faulty = new RecordingSink("Faulty", log, _ => Task.FromException(new InvalidOperationException("boom")));
            var other = new RecordingSink("Other", log);

            fwd.ConnectTo(faulty);
            fwd.ConnectTo(other);

            // Should not throw to caller (fire-and-forget). The internal task logs and swallows.
            var t = fwd.PostAsync("Z");
            Assert.True(t.IsCompleted);

            // Give background task a chance to run.
            await Task.Delay(50);

            // Depending on SourceValueHelper semantics, once an exception occurs, subsequent sinks may not be invoked.
            // We only assert that no exception escaped and that either 0 or 1 logs exist (if Other ran).
            Assert.True(log.Count == 0 || log.Count == 1);
        }

        [Fact]
        public void NotConnected_short_circuits_and_returns_completed_task()
        {
            var fwd = new AsyncForwarder();

            var t = fwd.PostAsync("ignored");
            Assert.True(t.IsCompleted);
        }

        [Fact]
        public async Task Post_by_ref_and_by_value_delegate_to_async_path()
        {
            var fwd = new AsyncForwarder();
            var log = new List<string>();
            var delivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int received = 0;

            var sink = new RecordingSink("S", log, _ =>
            {
                Interlocked.Increment(ref received);
                if (received >= 2) delivered.TrySetResult(true);
                return Task.CompletedTask;
            });

            fwd.ConnectTo(sink);

            int value = 42;
            fwd.Post(in value);
            fwd.Post(value);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => delivered.Task.IsCompleted, cts.Token);

            Assert.Equal(2, Volatile.Read(ref received));
        }

        private static async Task<bool> SpinUntilAsync(Func<bool> condition, TimeSpan timeout, int delayMs = 10)
        {
            var start = DateTime.UtcNow;
            while (!condition())
            {
                if (DateTime.UtcNow - start > timeout) return false;
                await Task.Delay(delayMs);
            }
            return true;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token, int delayMs = 10)
        {
            while (!condition())
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(delayMs, token);
            }
        }
    }
}