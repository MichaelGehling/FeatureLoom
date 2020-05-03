using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.Helper;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class DelayingForwarderTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new DelayingForwarder(20.Milliseconds());
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

        [Theory]
        [InlineData(20)]
        [InlineData(0)]
        public void CanDelayOnForward(int delay)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var forwarder = new DelayingForwarder(delay.Milliseconds());
            var sink = new SingleMessageTestSink<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            var timer = AppTime.TimeKeeper;
            sender.Send(42);
            Assert.True(sink.received);
            Assert.InRange(timer.Elapsed, delay.Milliseconds(), (delay + 5).Milliseconds());
        }
    }
}
