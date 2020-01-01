using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.Helper;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class LatestMessageReceiverTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanReceiveObjectsAndValues<T>(T message)
        {
            var sender = new Sender<T>();
            var receiver = new LatestMessageReceiver<T>();
            sender.ConnectTo(receiver);
            sender.Send(message);
            Assert.True(receiver.TryReceive(out T receivedMessage));
            Assert.Equal(message, receivedMessage);
        }

        [Fact]
        public void OnlyProvidesLatestMessage()
        {
            var sender = new Sender();
            var receiver = new LatestMessageReceiver<int>();
            sender.ConnectTo(receiver);
            sender.Send(42);
            sender.Send(99);
            Assert.True(receiver.TryReceive(out int receivedMessage));
            Assert.Equal(99, receivedMessage);
        }

        [Fact]
        public void SignalsFilledQueue()
        {
            var sender = new Sender();
            var receiver = new LatestMessageReceiver<int>();
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
        public void AllowsBlockingReceiving(int sendDelayInMs, int receivingWaitLimitInMs, bool shouldBeReceived)
        {
            TimeSpan tolerance = 20.Milliseconds();
            var sender = new Sender();
            var receiver = new LatestMessageReceiver<int>();
            sender.ConnectTo(receiver);

            var timeKeeper = AppTime.TimeKeeper;
            new Timer(_ => sender.Send(42), null, sendDelayInMs, -1);

            var received = receiver.TryReceive(out int message, receivingWaitLimitInMs.Milliseconds());
            Assert.Equal(shouldBeReceived, received);
            var expectedTime = sendDelayInMs.Milliseconds().ClampHigh(receivingWaitLimitInMs.Milliseconds());
            Assert.InRange(timeKeeper.Elapsed, expectedTime - tolerance, expectedTime + tolerance);
        }

        [Theory]
        [InlineData(80, 0, false)]
        [InlineData(80, 40, false)]
        [InlineData(20, 50, true)]
        public void AllowsAsyncReceiving(int sendDelayInMs, int receivingWaitLimitInMs, bool shouldBeReceived)
        {
            TimeSpan tolerance = 20.Milliseconds();
            var sender = new Sender();
            var receiver = new LatestMessageReceiver<int>();
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
