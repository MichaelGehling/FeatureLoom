using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.Helper;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class HubTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var hub = new Hub();
            var senderSocket = hub.CreateSocket(sender);
            var sink = new SingleMessageTestSink<T>();
            var sinkSocket = hub.CreateSocket(sink);
            sender.ConnectTo(senderSocket);
            sinkSocket.ConnectTo(sink);

            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

        [Fact]
        public void MessagesAreForwardedToAllButSendingSocket()
        {
            TestHelper.PrepareTestContext();

            var senderA = new Sender();
            var senderB = new Sender();
            var senderC = new Sender();
            var counterA = new CountingForwarder();
            var counterB = new CountingForwarder();
            var counterC = new CountingForwarder();
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