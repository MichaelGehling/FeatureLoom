using FeatureLoom.DataFlows.Test;
using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace FeatureLoom.DataFlows
{
    public class DeactivatableForwarderTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new DeactivatableForwarder();
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

        [Fact]
        public void CanDeactivateForwarding()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder();
            var sink = new SingleMessageTestSink<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            
            forwarder.Active = false;
            sender.Send(1);
            Assert.False(sink.received);

            forwarder.Active = true;
            sender.Send(2);
            Assert.True(sink.received);
            Assert.Equal(2, sink.receivedMessage);
        }

        [Fact]
        public void CanDeactivateForwardingViaDelegate()
        {
            TestHelper.PrepareTestContext();
            bool active = false;
            var sender = new Sender<int>();
            var forwarder = new DeactivatableForwarder(() => active);
            var sink = new SingleMessageTestSink<int>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            
            sender.Send(1);
            Assert.False(sink.received);

            active = true;
            sender.Send(2);
            Assert.True(sink.received);
            Assert.Equal(2, sink.receivedMessage);
        }
    }
}
