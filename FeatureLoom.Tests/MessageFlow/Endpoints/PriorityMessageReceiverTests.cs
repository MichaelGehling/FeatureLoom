using FeatureLoom.MessageFlow;
using FeatureLoom.Diagnostics;
using System.Collections.Generic;
using Xunit;
using FeatureLoom.Helpers;

namespace FeatureLoom.MesssageFlow.Endpoints
{
    public class PriorityMessageReceiverTests
    {
        [Fact]
        public void ProvidesSingleMessageWithHighestPriority()
        {
            using var testContext = TestHelper.PrepareTestContext();

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
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var receiver = new PriorityMessageReceiver<int>(Comparer<int>.Create((oldMsg, newMsg) => oldMsg - newMsg));
            sender.ConnectTo(receiver);
            Assert.True(receiver.IsEmpty);
            var waitHandle = receiver.WaitHandle;
            Assert.False(waitHandle.WaitingTask.IsCompleted);
            sender.Send(42);
            Assert.True(waitHandle.WaitingTask.IsCompleted);
        }
    }
}