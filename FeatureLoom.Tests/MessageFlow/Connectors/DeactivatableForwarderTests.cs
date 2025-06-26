using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class DeactivatableForwarderTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new DeactivatableForwarder();
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void CanDeactivateForwarding()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder();
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            forwarder.Active = false;
            sender.Send(1);
            Assert.False(sink.HasMessage);

            forwarder.Active = true;
            sender.Send(2);
            Assert.True(sink.HasMessage);
            Assert.Equal(2, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void CanDeactivateForwardingViaDelegate()
        {
            using var testContext = TestHelper.PrepareTestContext();
            bool active = false;
            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(() => active);
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            sender.Send(1);
            Assert.False(sink.HasMessage);

            active = true;
            sender.Send(2);
            Assert.True(sink.HasMessage);
            Assert.Equal(2, sink.LatestMessageOrDefault);
        }
    }
}