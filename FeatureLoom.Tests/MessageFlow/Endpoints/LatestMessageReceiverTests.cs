using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
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
    }
}