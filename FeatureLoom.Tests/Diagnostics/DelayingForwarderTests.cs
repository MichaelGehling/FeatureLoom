using FeatureLoom.Diagnostics;
using FeatureLoom.MessageFlow;
using FeatureLoom.Time;
using Xunit;

namespace FeatureLoom.Diagnostics
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
            var forwarder = new DelayingForwarder(1.Milliseconds());
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.False(sink.received);
            Assert.True(sink.WaitHandle.Wait(2.Milliseconds()));
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Theory]
        [InlineData(100, 120)]
        [InlineData(0, 5)]
        public void CanDelayOnForward(int delay, int maxDuration)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var forwarder = new DelayingForwarder(delay.Milliseconds());
            var sink = new SingleMessageTestSink<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);            
            sender.Send(42);
            if (delay > 0) Assert.False(sink.received);
            Assert.True(sink.WaitHandle.Wait(maxDuration.Milliseconds()));
            Assert.True(sink.received);

            Assert.False(TestHelper.HasAnyLogError());
        }
    }
}