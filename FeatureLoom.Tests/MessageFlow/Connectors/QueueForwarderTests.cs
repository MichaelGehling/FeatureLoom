using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class QueueForwarderTest
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new QueueForwarder();
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.WaitHandle.Wait(2.Seconds()));
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public async Task PostAsync_returns_immediately_and_forwards_eventually()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var fwd = new QueueForwarder(threadLimit: 1, spawnThresholdFactor: 1);
            var delivered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var sink = new RecordingSink(msg =>
            {
                delivered.TrySetResult(true);
            });

            fwd.ConnectTo(sink);

            var returned = fwd.PostAsync("X");
            Assert.True(returned.IsCompleted); // fire-and-forget semantics

            using var cts = new CancellationTokenSource(1.Seconds());
            await WaitUntilAsync(() => delivered.Task.IsCompleted, cts.Token);
        }

        [Fact]
        public async Task Single_thread_preserves_order()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var fwd = new QueueForwarder(threadLimit: 1, spawnThresholdFactor: 1000);
            var log = new List<int>();
            var sink = new RecordingSink(msg =>
            {
                lock (log) if (msg is int i) log.Add(i);
            });
            fwd.ConnectTo(sink);

            for (int i = 0; i < 50; i++) fwd.Post(i);

            var ok = await SpinUntilAsync(() => { lock (log) return log.Count == 50; }, 2.Seconds());
            Assert.True(ok, "Did not receive all messages in time");

            for (int i = 0; i < 50; i++) Assert.Equal(i, log[i]);
        }

        [Fact]
        public async Task Multiple_threads_can_reorder_messages()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var fwd = new QueueForwarder(threadLimit: 4, spawnThresholdFactor: 1);
            var oddGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var log = new List<int>();

            // Even numbers block until an odd arrived; odds proceed immediately.
            var sink = new RecordingSink(msg =>
            {
                if (msg is int i)
                {
                    if ((i % 2) == 0)
                    {
                        oddGate.Task.Wait(); // block this worker until an odd has arrived
                    }
                    lock (log) log.Add(i);
                    if ((i % 2) == 1) oddGate.TrySetResult(true);
                }
            });
            fwd.ConnectTo(sink);

            // Post two messages so scaling can spawn a second worker and cause a reorder deterministically.
            fwd.Post(0);
            fwd.Post(1);

            var ok = await SpinUntilAsync(() => { lock (log) return log.Count >= 2; }, 1.Seconds());
            Assert.True(ok, "Did not receive both messages in time");

            lock (log)
            {
                Assert.Equal(2, log.Count);
                Assert.Equal(1, log[0]); // odd should pass first
                Assert.Equal(0, log[1]); // even unblocks and arrives second
            }
        }

        [Fact]
        public async Task DropLatest_when_full_keeps_oldest_items_in_queue()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int queueSize = 5;

            // One worker that blocks on the first message; queue capacity 3; drop newest on overflow.
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fwd = new QueueForwarder(threadLimit: 1, spawnThresholdFactor: 1000, maxQueueSize: queueSize, maxWaitOnFullQueue: TimeSpan.Zero, dropLatestMessageOnFullQueue: true);
            var log = new List<int>();
            var sink = new BlockingFirstMessageSink(gate, msg =>
            {
                if (msg is int i) lock (log) log.Add(i);
            });
            fwd.ConnectTo(sink);

            // First item will be taken by the worker and block; subsequent items fill the queue up to 3.
            for (int i = 0; i < queueSize * 2; i++) fwd.Post(i);

            // Unblock worker so it can drain what made it into the queue.
            gate.SetResult(true);

            var ok = await SpinUntilAsync(() => { lock (log) return log.Count >= queueSize; }, 2.Seconds());
            Assert.True(ok, "Did not receive expected number of messages");

            lock (log)
            {
                Assert.Equal(Enumerable.Range(0, queueSize*2).Take(queueSize), log.ToArray());
            }
        }

        [Fact]
        public async Task DropOldest_when_full_keeps_newest_items_in_queue()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int queueSize = 5;

            // One worker that blocks on the first message; queue capacity 3; drop oldest on overflow.
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fwd = new QueueForwarder(threadLimit: 1, spawnThresholdFactor: 1000, maxQueueSize: queueSize, maxWaitOnFullQueue: TimeSpan.Zero, dropLatestMessageOnFullQueue: false);
            var log = new List<int>();
            var sink = new BlockingFirstMessageSink(gate, msg =>
            {
                if (msg is int i) lock (log) log.Add(i);
            });
            fwd.ConnectTo(sink);

            for (int i = 0; i < queueSize*2; i++) fwd.Post(i);

            gate.SetResult(true);

            var ok = await SpinUntilAsync(() => { lock (log) return log.Count >= queueSize; }, 2.Seconds());
            Assert.True(ok, "Did not receive expected number of messages");

            lock (log)
            {
                Assert.Equal(Enumerable.Range(0, queueSize * 2).Last(), log.Last());
            }
        }

        [Fact]
        public async Task Scales_up_workers_when_backlog_increases()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fwd = new QueueForwarder(threadLimit: 4, spawnThresholdFactor: 1, maxQueueSize: 1024);
            var sink = new BlockingFirstMessageSink(gate, _ => { /* no-op */ });
            fwd.ConnectTo(sink);

            // Post enough messages to justify multiple workers.
            for (int i = 0; i < 64; i++) fwd.Post(i);

            var ok = await SpinUntilAsync(() => fwd.CountThreads >= 2, 2.Seconds());
            Assert.True(ok, $"Expected at least 2 worker threads, got {fwd.CountThreads}");

            gate.SetResult(true);
        }

        // Helpers

        private sealed class RecordingSink : IMessageSink
        {
            private readonly Action<object> onMessage;

            public RecordingSink(Action<object> onMessage) => this.onMessage = onMessage;

            public void Post<M>(in M message) => onMessage?.Invoke(message);
            public void Post<M>(M message) => onMessage?.Invoke(message);
            public Task PostAsync<M>(M message)
            {
                onMessage?.Invoke(message);
                return Task.CompletedTask;
            }
        }

        private sealed class BlockingFirstMessageSink : IMessageSink
        {
            private readonly TaskCompletionSource<bool> gate;
            private readonly Action<object> onMessage;
            private int firstSeen = 0;

            public BlockingFirstMessageSink(TaskCompletionSource<bool> gate, Action<object> onMessage)
            {
                this.gate = gate;
                this.onMessage = onMessage;
            }

            public void Post<M>(in M message)
            {
                BlockIfFirst();
                onMessage?.Invoke(message);
            }

            public void Post<M>(M message)
            {
                BlockIfFirst();
                onMessage?.Invoke(message);
            }

            public Task PostAsync<M>(M message)
            {
                BlockIfFirst();
                onMessage?.Invoke(message);
                return Task.CompletedTask;
            }

            private void BlockIfFirst()
            {
                if (Interlocked.CompareExchange(ref firstSeen, 1, 0) == 0)
                {
                    // Block this worker until gate is released.
                    gate.Task.Wait();
                }
            }
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