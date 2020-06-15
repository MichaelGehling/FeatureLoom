using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Helpers.Diagnostics;
using FeatureFlowFramework.Services;
using System;
using System.Threading;
using Xunit;
using FeatureFlowFramework.Helpers.Extensions;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class LatestMessageReceiverTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanReceiveObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

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
            TestHelper.PrepareTestContext();

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
            TestHelper.PrepareTestContext();

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
        [InlineData(200, 0, false)]
        [InlineData(200, 30, false)]
        [InlineData(20, 100, true)]
        public void AllowsAsyncReceiving(int sendDelayInMs, int toleranceInMs, bool shouldBeReceived)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new LatestMessageReceiver<int>();
            sender.ConnectTo(receiver);

            var timeKeeper = AppTime.TimeKeeper;
            Task.Run(() =>
            {
                Thread.Sleep(sendDelayInMs);
                sender.Send(42);
            });

            var receivingTask = receiver.TryReceiveAsync(toleranceInMs.Milliseconds());
            Assert.Equal(shouldBeReceived, receivingTask.Result.Out(out int message));
        }
    }
}