using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class HubTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var hub = new Hub();
            var senderSocket = hub.CreateSocket(sender);
            var sink = new LatestMessageReceiver<T>();
            var sinkSocket = hub.CreateSocket(sink);
            sender.ConnectTo(senderSocket);
            sinkSocket.ConnectTo(sink);

            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void MessagesAreForwardedToAllButSendingSocket()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var senderA = new Sender();
            var senderB = new Sender();
            var senderC = new Sender();
            var counterA = new MessageCounter();
            var counterB = new MessageCounter();
            var counterC = new MessageCounter();
            var hub = new Hub();
            var socketA = hub.CreateSocket();
            var socketB = hub.CreateSocket();
            var socketC = hub.CreateSocket();
            senderA.ConnectTo(socketA);
            socketA.ConnectTo(counterA);
            senderB.ConnectTo(socketB);
            socketB.ConnectTo(counterB);
            senderC.ConnectTo(socketC);
            socketC.ConnectTo(counterC);

            senderA.Send(42);
            Assert.Equal(0, counterA.Counter);
            Assert.Equal(1, counterB.Counter);
            Assert.Equal(1, counterC.Counter);

            senderB.Send(42);
            Assert.Equal(1, counterA.Counter);
            Assert.Equal(1, counterB.Counter);
            Assert.Equal(2, counterC.Counter);
        }
    }
}