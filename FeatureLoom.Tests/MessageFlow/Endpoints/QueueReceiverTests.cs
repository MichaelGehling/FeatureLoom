using FeatureLoom.Diagnostics;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class QueueReceiverTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanReceiveObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var receiver = new QueueReceiver<T>();
            sender.ConnectTo(receiver);
            sender.Send(message);
            Assert.True(receiver.TryReceive(out T receivedMessage));
            Assert.Equal(message, receivedMessage);
        }

        [Theory]
        [InlineData(0, 10, true)]
        [InlineData(5, 10, true)]
        [InlineData(10, 10, true)]
        [InlineData(20, 10, true)]
        [InlineData(0, 10, false)]
        [InlineData(5, 10, false)]
        [InlineData(10, 10, false)]
        [InlineData(20, 10, false)]
        public void CanLimitQueueSize(int numMessages, int limit, bool dropLatestMessageOnFullQueue)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new QueueReceiver<int>(limit, default, dropLatestMessageOnFullQueue);
            sender.ConnectTo(receiver);
            var sendMessages = new List<int>();
            for (int i = 1; i <= numMessages; i++)
            {
                sender.Send(i);
                sendMessages.Add(i);
            }
            Assert.Equal(limit.ClampHigh(numMessages), receiver.Count);
            var receivedMessages = receiver.ReceiveAll();
            Assert.Equal(limit.ClampHigh(numMessages), receivedMessages.Count);

            int offset = dropLatestMessageOnFullQueue ? 0 : (numMessages - limit).ClampLow(0);

            for (int i = 0; i < limit.ClampHigh(numMessages); i++)
            {
                Assert.Equal(sendMessages[i + offset], receivedMessages[i]);
            }
        }

        [Theory(Skip = "Fails on GitHub test server.")]
        [InlineData(true)]
        [InlineData(false)]
        public void CanBlockOnFullQueue(bool sendAsync)
        {
            using var testContext = TestHelper.PrepareTestContext();

            TimeSpan blockTime = 100.Milliseconds();
            int limit = 5;
            var sender = new Sender();
            var receiver = new QueueReceiver<int>(limit, blockTime);
            sender.ConnectTo(receiver);
            Assert.True(receiver.IsEmpty);
            for (int i = 1; i <= limit; i++)
            {
                sender.Send(i);
            }
            Assert.True(receiver.IsFull);
            if (sendAsync)
            {
                TimeSpan tolerance = 30.Milliseconds();

                var timeKeeper = AppTime.TimeKeeper;
                var task = sender.SendAsync(42);
                Assert.False(task.IsCompleted);
                task.WaitFor();
                Assert.InRange(timeKeeper.Elapsed, blockTime - tolerance, blockTime + tolerance);
            }
            else
            {
                TimeSpan tolerance = 10.Milliseconds();

                var timeKeeper = AppTime.TimeKeeper;
                sender.Send(42);
                Assert.InRange(timeKeeper.Elapsed, blockTime - tolerance, blockTime + tolerance);
            }
        }

        [Fact]
        public void SignalsFilledQueue()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new QueueReceiver<int>();
            sender.ConnectTo(receiver);
            Assert.True(receiver.IsEmpty);
            var waitHandle = receiver.WaitHandle;
            Assert.False(waitHandle.WaitingTask.IsCompleted);
            sender.Send(42);
            Assert.True(waitHandle.WaitingTask.IsCompleted);
        }

        [Fact]
        public void DropLatestForwardsNewestMessageToAlternativeSource()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new QueueReceiver<int>(1, TimeSpan.Zero, dropLatestMessageOnFullQueue: true);
            var altSink = new RecordingSink<int>();
            receiver.Else.ConnectTo(altSink);

            receiver.Post(1); // fills queue
            receiver.Post(2); // forwarded instead of enqueued

            Assert.True(receiver.TryPeek(out var queued));
            Assert.Equal(1, queued);

            var forwarded = altSink.Received.ToArray();
            Assert.Single(forwarded);
            Assert.Equal(2, forwarded[0]);
        }

        [Fact]
        public void DropOldestForwardsRemovedMessageToAlternativeSource()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new QueueReceiver<int>(2, TimeSpan.Zero, dropLatestMessageOnFullQueue: false);
            var altSink = new RecordingSink<int>();
            receiver.Else.ConnectTo(altSink);

            receiver.Post(1);
            receiver.Post(2);
            receiver.Post(3); // drops 1

            Assert.Equal(2, receiver.Count);

            var forwarded = altSink.Received.ToArray();
            Assert.Single(forwarded);
            Assert.Equal(1, forwarded[0]);

            Assert.True(receiver.TryReceive(out var first));
            Assert.Equal(2, first);
            Assert.True(receiver.TryReceive(out var second));
            Assert.Equal(3, second);
        }

        [Fact]
        public async Task PostAsyncWaitsUntilSpaceIsAvailableWhenBlockingIsEnabled()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new QueueReceiver<int>(1, 2.Seconds());
            receiver.Post(1);

            var postTask = receiver.PostAsync(2);
            Assert.False(postTask.IsCompleted);

            Assert.True(receiver.TryReceive(out var first));
            Assert.Equal(1, first);

            var completed = await Task.WhenAny(postTask, Task.Delay(1.Seconds()));
            Assert.Same(postTask, completed);
            await postTask;

            Assert.True(receiver.TryPeek(out var queued));
            Assert.Equal(2, queued);
        }

        [Fact(Skip = "Unstable when run with other tests.")]
        public void MultipleThreadsCanReceiveConcurrently()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int numMessages = 1000;
            int numThreads = 2;
            int minReceived = numMessages / numThreads - numMessages / numThreads / 10;
            int maxReceived = numMessages / numThreads + numMessages / numThreads / 10;
            int[] threadCounters = new int[numThreads];
            Task[] tasks = new Task[numThreads];
            var sender = new Sender();
            var receiver = new QueueReceiver<int>();
            sender.ConnectTo(receiver);
            for (int i = 0; i < numMessages; i++)
            {
                sender.Send(i);
            }
            Assert.Equal(numMessages, receiver.Count);
            for (int i = 0; i < numThreads; i++)
            {
                int threadIndex = i;
                tasks[threadIndex] = Task.Run((Action)(() =>
                {
                    while (receiver.TryReceive(out int msg))
                    {
                        threadCounters[threadIndex] = threadCounters[threadIndex] + 1;
                        //Thread.Sleep(20);
                    }
                    Assert.Equal(0, (int)receiver.Count);
                }));
            }

            Task.WhenAll(tasks).WaitFor(false);

            for (int i = 0; i < numThreads; i++)
            {
                Assert.InRange(threadCounters[i], minReceived, maxReceived);
            }
        }

        private sealed class RecordingSink<T> : IMessageSink
        {
            private readonly List<T> received = new List<T>();

            public IReadOnlyList<T> Received
            {
                get
                {
                    lock (received) return received.ToArray();
                }
            }

            public void Post<M>(in M message)
            {
                if (message is T typed) lock (received) received.Add(typed);
            }

            public void Post<M>(M message)
            {
                if (message is T typed) lock (received) received.Add(typed);
            }

            public Task PostAsync<M>(M message)
            {
                Post(message);
                return Task.CompletedTask;
            }
        }
    }
}