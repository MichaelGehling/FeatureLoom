using FeatureLoom.Diagnostics;
using FeatureLoom.Time;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class QueueForwarderTest
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new QueueForwarder();
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            sink.WaitHandle.Wait(2.Seconds());
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

    }
}