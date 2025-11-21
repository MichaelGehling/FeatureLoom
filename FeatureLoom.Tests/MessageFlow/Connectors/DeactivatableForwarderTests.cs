using System.Threading.Tasks;
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
            var forwarder = new DeactivatableForwarder(); // no delegate -> manual Active usage
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
        public void DelegateCanMirrorExternalState()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool externallyActive = false;
            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(prev => externallyActive); // ignores prev
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            sender.Send(1);
            Assert.False(sink.HasMessage);

            externallyActive = true;
            sender.Send(2);
            Assert.True(sink.HasMessage);
            Assert.Equal(2, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void DelegateReceivesPreviousState()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool observedPrevOnFirst = false;
            bool first = true;
            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(prev =>
            {
                if (first)
                {
                    observedPrevOnFirst = prev;
                    first = false;
                }
                return prev; // keep state
            });
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            sender.Send(10);

            Assert.True(observedPrevOnFirst); // default Active = true
            Assert.True(sink.HasMessage);
            Assert.Equal(10, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void DelegateCanToggleStateUsingPrevious()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(prev => !prev);
            forwarder.Active = false; // start false
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            sender.Send(1); // prev=false -> true -> forwarded
            Assert.True(sink.HasMessage);
            Assert.Equal(1, sink.LatestMessageOrDefault);

            sender.Send(2); // prev=true -> false -> suppressed
            Assert.Equal(1, sink.LatestMessageOrDefault);

            sender.Send(3); // prev=false -> true -> forwarded
            Assert.Equal(3, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void DelegateAllowsOnlyFirstMessage()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool first = true;
            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(prev =>
            {
                if (first)
                {
                    first = false;
                    return true; // activate for first
                }
                return false; // then stay inactive
            });
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            sender.Send(100);
            Assert.True(sink.HasMessage);
            Assert.Equal(100, sink.LatestMessageOrDefault);

            sender.Send(200);
            Assert.Equal(100, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void VolatileAccessDisabledBehavesSame()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(null, volatileAccess: false);
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
        public async Task AsyncSendRespectsDelegate()
        {
            using var testContext = TestHelper.PrepareTestContext();

            bool allow = true;
            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(prev => allow);
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            await sender.SendAsync(5);
            Assert.True(sink.HasMessage);
            Assert.Equal(5, sink.LatestMessageOrDefault);

            allow = false;
            await sender.SendAsync(6);
            Assert.Equal(5, sink.LatestMessageOrDefault);

            allow = true;
            await sender.SendAsync(7);
            Assert.Equal(7, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void DelegateCanForceActivationRegardlessOfPrevious()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(prev => true); // always active
            forwarder.Active = false; // will be overridden by delegate
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            sender.Send(11);
            Assert.True(sink.HasMessage);
            Assert.Equal(11, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void DelegateCanForceDeactivationRegardlessOfPrevious()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(prev => false); // always inactive
            var sink = new LatestMessageReceiver<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            sender.Send(99);
            Assert.False(sink.HasMessage);
        }
    }
}