using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
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
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new QueueForwarder<T>();
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.WaitHandle.Wait(2.Seconds()));
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

    }
}