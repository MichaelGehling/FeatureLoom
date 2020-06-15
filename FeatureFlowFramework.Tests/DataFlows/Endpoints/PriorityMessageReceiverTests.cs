using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helpers.Time;
using FeatureFlowFramework.Helpers.Diagnostics;
using FeatureFlowFramework.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Xunit;
using FeatureFlowFramework.Helpers.Extensions;

namespace FeatureFlowFramework.Tests.DataFlows.Endpoints
{
    public class PriorityMessageReceiverTests
    {
        [Fact]
        public void ProvidesSingleMessageWithHighestPriority()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new PriorityMessageReceiver<int>(Comparer<int>.Create((oldMsg, newMsg) => oldMsg - newMsg));
            sender.ConnectTo(receiver);
            sender.Send(42);
            sender.Send(99);
            sender.Send(10);
            Assert.True(receiver.TryReceive(out int receivedMessage));
            Assert.Equal(99, receivedMessage);
            sender.Send(10);
            Assert.True(receiver.TryReceive(out int receivedMessage2));
            Assert.Equal(10, receivedMessage2);
        }

        [Fact]
        public void SignalsFilledQueue()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new PriorityMessageReceiver<int>(Comparer<int>.Create((oldMsg, newMsg) => oldMsg - newMsg));
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
            TestHelper.PrepareTestContext();

            TimeSpan tolerance = 30.Milliseconds();
            var sender = new Sender();
            var receiver = new PriorityMessageReceiver<int>(Comparer<int>.Create((oldMsg, newMsg) => oldMsg - newMsg));
            sender.ConnectTo(receiver);

            var timeKeeper = AppTime.TimeKeeper;
            new Timer(_ => sender.Send(42), null, sendDelayInMs, -1);

            var receivingTask = receiver.TryReceiveAsync(receivingWaitLimitInMs.Milliseconds());
            Assert.Equal(shouldBeReceived, receivingTask.Result.Out(out int message));
        }

    }
}
