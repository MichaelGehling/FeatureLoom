using System;
using System.Threading.Tasks;
using Xunit;
using FeatureFlowFramework.Helper;
using System.Collections.Generic;
using System.Threading;

namespace FeatureFlowFramework.DataFlows
{
    public class PriorityQueueReceiverTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void PriorityQueueReceiverCanReceiveObjectsAndValues<T>(T message)
        {
            var sender = new Sender<T>();
            var receiver = new PriorityQueueReceiver<T>(Comparer<T>.Default);
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
        public void PriorityQueueReceiverCanLimitQueueSize(int numMessages, int limit, bool dropLatestMessageOnFullQueue)
        {
            var sender = new Sender();
            var receiver = new PriorityQueueReceiver<int>(Comparer<int>.Create((a, b) => a == b ? 0 : a > b ? -1 : 1), limit, default, dropLatestMessageOnFullQueue);
            sender.ConnectTo(receiver);
            var sendMessages = new List<int>();
            for (int i=1; i <= numMessages; i++)
            {
                sender.Send(i);
                sendMessages.Add(i);
            }            
            Assert.Equal(limit.ClampHigh(numMessages), receiver.CountQueuedMessages);
            var receivedMessages = receiver.ReceiveAll();
            Assert.Equal(limit.ClampHigh(numMessages), receivedMessages.Length);

            int offset = dropLatestMessageOnFullQueue? 0: (numMessages-limit).ClampLow(0);

            for(int i=0; i < limit.ClampHigh(numMessages); i++)
            {
                Assert.Equal(sendMessages[i+offset], receivedMessages[i]);
            }
        }

        [Theory(Skip = "Unstable when run with other tests.")]
        [InlineData(true)]
        [InlineData(false)]
        public void PriorityQueueReceiverCanBlockOnFullQueue(bool sendAsync)
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
                TimeSpan tolerance = 20.Milliseconds();

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
        public void PriorityQueueReceiverSignalsFilledQueue()
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
        [InlineData(20, 100, true)]        
        public void PriorityQueueReceiverAllowsBlockingReceiving(int sendDelayInMs, int receivingWaitLimitInMs, bool shouldBeReceived)
        {
            TimeSpan tolerance = 20.Milliseconds();
            var sender = new Sender();
            var receiver = new PriorityQueueReceiver<int>(Comparer<int>.Default);
            sender.ConnectTo(receiver);

            var timeKeeper = AppTime.TimeKeeper;
            new Timer(_ => sender.Send(42), null, sendDelayInMs, -1);

            var received = receiver.TryReceive(out int message, receivingWaitLimitInMs.Milliseconds());
            Assert.Equal(shouldBeReceived, received);
            var expectedTime = sendDelayInMs.Milliseconds().ClampHigh(receivingWaitLimitInMs.Milliseconds());
            Assert.InRange(timeKeeper.Elapsed, expectedTime - tolerance, expectedTime + tolerance);
        }

        [Theory(Skip = "Unstable when run with other tests.")]
        [InlineData(80, 0, false)]
        [InlineData(80, 40, false)]
        [InlineData(20, 50, true)]
        public void PriorityQueueReceiverAllowsAsyncReceiving(int sendDelayInMs, int receivingWaitLimitInMs, bool shouldBeReceived)
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
