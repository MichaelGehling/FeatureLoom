using FeatureLoom.Diagnostics;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class PriorityQueueReceiverTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanReceiveObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var receiver = new PriorityQueueReceiver<T>(Comparer<T>.Default);
            sender.ConnectTo(receiver);
            sender.Send(message);
            Assert.True(receiver.TryReceive(out T receivedMessage));
            Assert.Equal(message, receivedMessage);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(5, 10)]
        [InlineData(10, 10)]
        [InlineData(20, 10)]
        public void CanLimitQueueSize(int numMessages, int limit)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new PriorityQueueReceiver<int>(Comparer<int>.Create((a, b) => a == b ? 0 : a < b ? -1 : 1), limit, default);
            sender.ConnectTo(receiver);
            var sendMessages = new List<int>();
            for (int i = 1; i <= numMessages; i++)
            {
                int number = RandomGenerator.Int32();
                sender.Send(number);
                sendMessages.Add(number);
            }
            Assert.Equal(limit.ClampHigh(numMessages), receiver.Count);
            var receivedMessages = receiver.ReceiveAll();
            Assert.Equal(limit.ClampHigh(numMessages), receivedMessages.Count);

            int offset = (numMessages - limit).ClampLow(0);

            sendMessages.Sort();
            for (int i = 0; i < receivedMessages.Count; i++)
            {
                Assert.Equal(sendMessages[sendMessages.Count - 1 - i], receivedMessages[i]);
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
            var receiver = new PriorityQueueReceiver<int>(Comparer<int>.Default, limit, blockTime);
            sender.ConnectTo(receiver);
            Assert.True(receiver.IsEmpty);
            for (int i = 1; i <= limit; i++)
            {
                sender.Send(i);
            }
            Assert.True(receiver.IsFull);
            if (sendAsync)
            {
                TimeSpan tolerance = 100.Milliseconds();

                var timeKeeper = AppTime.TimeKeeper;
                var task = sender.SendAsync(42);
                Assert.False(task.IsCompleted);
                task.WaitFor();
                Assert.InRange(timeKeeper.Elapsed, blockTime - tolerance, blockTime + tolerance);
            }
            else
            {
                TimeSpan tolerance = 15.Milliseconds();

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
            var receiver = new PriorityQueueReceiver<int>(Comparer<int>.Default);
            sender.ConnectTo(receiver);
            Assert.True(receiver.IsEmpty);
            var waitHandle = receiver.WaitHandle;
            Assert.False(waitHandle.WaitingTask.IsCompleted);
            sender.Send(42);
            Assert.True(waitHandle.WaitingTask.IsCompleted);
        }

        [Fact]
        public void PeekManyReturnsPriorityOrderWithoutDequeuing()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new PriorityQueueReceiver<int>(Comparer<int>.Default);
            sender.ConnectTo(receiver);

            sender.Send(1);
            sender.Send(5);
            sender.Send(3);

            var peeked = receiver.PeekMany(2);
            Assert.Equal(3, receiver.Count);
            Assert.Equal(5, peeked.Array[peeked.Offset + 0]);
            Assert.Equal(3, peeked.Array[peeked.Offset + 1]);
        }

        [Fact]
        public void PostAsyncWaitsUntilSpaceIsAvailable()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int limit = 2;
            var sender = new Sender();
            var receiver = new PriorityQueueReceiver<int>(Comparer<int>.Default, limit, 1.Seconds());
            sender.ConnectTo(receiver);

            sender.Send(1);
            sender.Send(2);
            Assert.True(receiver.IsFull);

            var sendTask = sender.SendAsync(99);
            Assert.False(sendTask.IsCompleted);

            Assert.True(receiver.TryReceive(out _));
            sendTask.WaitFor();
            Assert.True(sendTask.IsCompletedSuccessfully);

            var remaining = receiver.ReceiveAll();
            Assert.Equal(limit, remaining.Count);
            Assert.Equal(99, remaining[0]);
        }
    }
}