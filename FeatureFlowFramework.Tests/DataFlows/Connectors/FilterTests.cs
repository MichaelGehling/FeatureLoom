using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Diagnostics;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class FilterTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var filter = new Filter<T>(msg => true);
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(filter).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

        [Fact]
        public void CanFilterMessages()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var filter = new Filter<int>(msg => msg <= 10);
            var counter = new CountingForwarder();
            sender.ConnectTo(filter).ConnectTo(counter);
            sender.Send(42);
            Assert.Equal(0, counter.Counter);
            sender.Send(5);
            Assert.Equal(1, counter.Counter);
            sender.Send(10);
            Assert.Equal(2, counter.Counter);
            sender.Send(11);
            Assert.Equal(2, counter.Counter);
        }
    }
}