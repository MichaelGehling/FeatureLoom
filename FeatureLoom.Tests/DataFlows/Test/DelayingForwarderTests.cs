using FeatureLoom.Diagnostics;
using FeatureLoom.Time;
using Xunit;

namespace FeatureLoom.DataFlows
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

        [Theory(Skip = "Fails on GitHub test server.")]
        [InlineData(100, 120)]
        [InlineData(0, 5)]
        public void CanDelayOnForward(int delay, int maxDuration)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var forwarder = new DelayingForwarder(delay.Milliseconds());
            var sink = new SingleMessageTestSink<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            var timer = AppTime.TimeKeeper;
            sender.Send(42);
            Assert.True(sink.received);
            Assert.InRange(timer.Elapsed, delay.Milliseconds(), (delay + maxDuration).Milliseconds());
        }
    }
}