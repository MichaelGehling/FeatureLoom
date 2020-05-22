using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Diagnostics;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class ForwarderTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new Forwarder();
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }
    }
}