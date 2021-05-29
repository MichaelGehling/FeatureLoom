using FeatureLoom.Diagnostics;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class SingleMessageTestSinkTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanReceiveObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
            sink.Reset();
            Assert.False(sink.received);
        }
    }
}