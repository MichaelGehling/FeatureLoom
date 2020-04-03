using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class PriorityQueueReceiverTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanReceiveObjectsAndValues<T>(T message)
        {
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
            Assert.Equal(limit.ClampHigh(numMessages), receiver.CountQueuedMessages);
            var receivedMessages = receiver.ReceiveAll();
            Assert.Equal(limit.ClampHigh(numMessages), receivedMessages.Length);

            int offset = (numMessages - limit).ClampLow(0);

            sendMessages.Sort();            
            for (int i = 0; i < receivedMessages.Length; i++)
            {
                Assert.Equal(sendMessages[sendMessages.Count-1-i], receivedMessages[i]);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanBlockOnFullQueue(bool sendAsync)
        {
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
                TimeSpan tolerance = 30.Milliseconds();

                var timeKeeper = AppTime.TimeKeeper;
                var task = sender.SendAsync(42);
                Assert.False(task.IsCompleted);
                task.Wait();
                Assert.InRange(timeKeeper.Elapsed, blockTime - tolerance, blockTime + tolerance);
            }
            else
            {
                TimeSpan tolerance = 2.Milliseconds();

                var timeKeeper = AppTime.TimeKeeper;
                sender.Send(42);
                Assert.InRange(timeKeeper.Elapsed, blockTime - tolerance, blockTime + tolerance);
            }
        }

        [Fact]
        public void SignalsFilledQueue()
        {
            var sender = new Sender();
            var receiver = new PriorityQueueReceiver<int>(Comparer<int>.Default);
            sender.ConnectTo(receiver);
            Assert.True(receiver.IsEmpty);
            var waitHandle = receiver.WaitHandle;
            Assert.False(waitHandle.WaitingTask.IsCompleted);
            sender.Send(42);
            Assert.True(waitHandle.WaitingTask.IsCompleted);
        }


        [Theory]
        [InlineData(80, 0, false)]
        [InlineData(80, 40, false)]
        [InlineData(20, 50, true)]
        public void AllowsAsyncReceiving(int sendDelayInMs, int receivingWaitLimitInMs, bool shouldBeReceived)
        {
            TimeSpan tolerance = 20.Milliseconds();
            var sender = new Sender();
            var receiver = new PriorityQueueReceiver<int>(Comparer<int>.Default);
            sender.ConnectTo(receiver);

            var timeKeeper = AppTime.TimeKeeper;
            new Timer(_ => sender.Send(42), null, sendDelayInMs, -1);

            var receivingTask = receiver.TryReceiveAsync(receivingWaitLimitInMs.Milliseconds());
            Assert.Equal(shouldBeReceived, receivingTask.Result.Out(out int message));
            var expectedTime = sendDelayInMs.Milliseconds().ClampHigh(receivingWaitLimitInMs.Milliseconds());
            Assert.InRange(timeKeeper.Elapsed, expectedTime - tolerance, expectedTime + tolerance);
        }
    }
}