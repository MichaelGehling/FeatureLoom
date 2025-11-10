using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class CurrentContextForwarderTests
    {
        private sealed class RecordingSink : IMessageSink
        {
            private readonly string name;
            private readonly List<string> log;
            private readonly SynchronizationContext expectedCtx;
            private readonly Func<object, Task> asyncBehavior;
            private readonly Action onAsyncCalled;

            public RecordingSink(string name, List<string> log, SynchronizationContext expectedCtx = null,
                                 Func<object, Task> asyncBehavior = null, Action onAsyncCalled = null)
            {
                this.name = name;
                this.log = log;
                this.expectedCtx = expectedCtx;
                this.asyncBehavior = asyncBehavior;
                this.onAsyncCalled = onAsyncCalled;
            }

            private void AssertContext()
            {
                if (expectedCtx != null)
                {
                    Assert.Same(expectedCtx, SynchronizationContext.Current);
                }
            }

            public void Post<M>(in M message)
            {
                AssertContext();
                lock (log) log.Add($"{name}:{message}");
            }

            public void Post<M>(M message)
            {
                AssertContext();
                lock (log) log.Add($"{name}:{message}");
            }

            public Task PostAsync<M>(M message)
            {
                AssertContext();
                onAsyncCalled?.Invoke();
                if (asyncBehavior != null) return asyncBehavior(message);
                lock (log) log.Add($"{name}:{message}");
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task Forwards_on_captured_SynchronizationContext_from_background_thread()
        {
            var capturedCtx = SynchronizationContext.Current; // xUnit provides a context; if null, we just skip the context assertion
            var fwd = new CurrentContextForwarder<string>();

            var delivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sink = new RecordingSink("S", new List<string>(), capturedCtx, _ =>
            {
                delivered.TrySetResult(true);
                return Task.CompletedTask;
            });

            fwd.ConnectTo(sink);

            await Task.Run(() => fwd.Post("X"));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => delivered.Task.IsCompleted, cts.Token);
        }

        [Fact]
        public async Task Preserves_order_and_runs_on_context_for_async_sinks()
        {
            var capturedCtx = SynchronizationContext.Current;
            var fwd = new CurrentContextForwarder<string>();
            var log = new List<string>();

            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var sinkA = new RecordingSink("A", log, capturedCtx, async _ =>
            {
                lock (log) log.Add("A:msg");
                await gate.Task;
            });

            int bCalls = 0;
            var sinkB = new RecordingSink("B", log, capturedCtx, _ =>
            {
                Interlocked.Increment(ref bCalls);
                lock (log) log.Add("B:msg");
                return Task.CompletedTask;
            });

            fwd.ConnectTo(sinkA);
            fwd.ConnectTo(sinkB);

            // Enqueue an async forwarding path
            await fwd.PostAsync("msg");

            // Give the loop a chance to start, B must not run before A completes
            await Task.Delay(50);
            Assert.Equal(0, Volatile.Read(ref bCalls));

            gate.SetResult(true);

            await SpinUntilAsync(() => Volatile.Read(ref bCalls) == 1, TimeSpan.FromSeconds(1));
            Assert.Equal(new[] { "A:msg", "B:msg" }, log);
        }

        [Fact]
        public async Task Cancel_stops_processing_and_Restart_resumes_on_same_context()
        {
            var capturedCtx = SynchronizationContext.Current;
            var fwd = new CurrentContextForwarder<string>();
            var log = new List<string>();
            int received = 0;

            var delivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var sink = new RecordingSink("S", log, capturedCtx, _ =>
            {
                if (Interlocked.Increment(ref received) == 1)
                {
                    delivered.TrySetResult(true);
                }
                return Task.CompletedTask;
            });

            fwd.ConnectTo(sink);

            // First message should be delivered
            await fwd.PostAsync("one");
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                await WaitUntilAsync(() => delivered.Task.IsCompleted, cts.Token);
            }

            // Cancel and await loop completion
            await fwd.Cancel();

            // Post after cancel should not be delivered
            await fwd.PostAsync("two");
            await Task.Delay(50);
            Assert.Equal(1, Volatile.Read(ref received));

            // Restart and ensure messages flow again on same context
            await fwd.Restart();

            var deliveredAfterRestart = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sink2 = new RecordingSink("S2", log, capturedCtx, _ =>
            {
                if (Interlocked.Increment(ref received) == 2)
                {
                    deliveredAfterRestart.TrySetResult(true);
                }
                return Task.CompletedTask;
            });

            // Replace sink to ensure re-check of context and delivery
            fwd.DisconnectAll();
            fwd.ConnectTo(sink2);

            await fwd.PostAsync("three");

            using (var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                await WaitUntilAsync(() => deliveredAfterRestart.Task.IsCompleted, cts2.Token);
            }

            Assert.Equal(2, Volatile.Read(ref received));
        }

        [Fact]
        public void NotConnected_short_circuits_posts()
        {
            var fwd = new CurrentContextForwarder<string>();

            // No sinks connected; Post and PostAsync should return immediately and not enqueue.
            fwd.Post("ignored");
            Assert.Equal(0, fwd.Count);

            var t = fwd.PostAsync("ignoredAsync");
            Assert.True(t.IsCompleted);
            Assert.Equal(0, fwd.Count);
        }

        [Fact]
        public async Task Post_by_ref_and_by_value_are_processed_in_order()
        {
            var capturedCtx = SynchronizationContext.Current;
            var fwd = new CurrentContextForwarder<int>();
            var log = new List<string>();
            var delivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var sink = new RecordingSink("S", log, capturedCtx, _ =>
            {
                lock (log)
                {
                    if (log.Count >= 2) delivered.TrySetResult(true);
                }
                return Task.CompletedTask;
            });

            fwd.ConnectTo(sink);

            int value = 42;
            fwd.Post(in value);  // by-ref (sync)
            fwd.Post(value);     // by-value (sync)

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await WaitUntilAsync(() => delivered.Task.IsCompleted, cts.Token);

            // Order is preserved (both are synchronous paths through the loop).
            Assert.Equal(new[] { "S:42", "S:42" }, log);
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