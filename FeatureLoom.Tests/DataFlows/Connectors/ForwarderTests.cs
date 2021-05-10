using FeatureLoom.Diagnostics;
using System;
using Xunit;

namespace FeatureLoom.DataFlows
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

        [Fact]
        public void TypedForwarderFailsWhenConnectedToWrongType()
        {
            Forwarder<int> intForwarder = new Forwarder<int>();
            Forwarder<int> intForwarder2 = new Forwarder<int>();
            Forwarder<string> stringForwarder = new Forwarder<string>();
            Forwarder<object> objectForwarder = new Forwarder<object>();

            Assert.ThrowsAny<Exception>(() => intForwarder.ConnectTo(stringForwarder));

            Assert.ThrowsAny<Exception>(() => objectForwarder.ConnectTo(stringForwarder));

            intForwarder.ConnectTo(intForwarder2);
            Assert.True(intForwarder.CountConnectedSinks == 1);

            stringForwarder.ConnectTo(objectForwarder);
            Assert.True(stringForwarder.CountConnectedSinks == 1);

            intForwarder2.ConnectTo(objectForwarder);
            Assert.True(intForwarder2.CountConnectedSinks == 1);
        }
    }
}