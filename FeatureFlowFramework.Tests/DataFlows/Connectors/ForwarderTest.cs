using FeatureFlowFramework.DataFlows.Test;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class ForwarderTest
    {      

        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
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
