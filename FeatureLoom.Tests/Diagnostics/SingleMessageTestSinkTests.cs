using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using Xunit;

namespace FeatureLoom.Diagnostics
{
    public class SingleMessageTestSinkTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanReceiveObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
            sink.Clear();
            Assert.False(sink.HasMessage);
        }
    }
}