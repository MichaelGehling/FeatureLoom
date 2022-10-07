using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using Xunit;

namespace FeatureLoom.MessageFlow
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
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.False(sink.HasMessage);
            Assert.True(sink.WaitHandle.Wait(10.Milliseconds()));
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);

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
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);            
            sender.Send(42);
            if (delay > 0) Assert.False(sink.HasMessage);
            Assert.True(sink.WaitHandle.Wait(maxDuration.Milliseconds()));
            Assert.True(sink.HasMessage);

            Assert.False(TestHelper.HasAnyLogError());
        }
    }
}