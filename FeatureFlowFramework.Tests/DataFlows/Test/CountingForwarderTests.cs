using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.Helper;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class CountingForwarderTests
    {

        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            var sender = new Sender<T>();
            var forwarder = new CountingForwarder();
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

        [Fact]
        public void CountsTheForwardedMessages()
        {
            var sender = new Sender();
            var forwarder = new CountingForwarder();
            var sink = new SingleMessageTestSink<object>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(41);
            Assert.Equal(1, forwarder.Counter);
            sender.Send(42);
            Assert.Equal(2, forwarder.Counter);
            sender.Send(43);
            Assert.Equal(3, forwarder.Counter);
        }

        [Fact]
        public void CanWaitForANumberOfMessages()
        {
            var sender = new Sender();
            var forwarder = new CountingForwarder();
            var sink = new SingleMessageTestSink<object>();
            sender.ConnectTo(forwarder).ConnectTo(sink);

            Task waitFor1 = forwarder.WaitForAsync(1);
            Task waitFor3 = forwarder.WaitForAsync(3);
            Assert.False(waitFor1.IsCompleted);
            Assert.False(waitFor3.IsCompleted);
            sender.Send(41);
            Assert.True(waitFor1.IsCompleted);
            Assert.False(waitFor3.IsCompleted);
            sender.Send(42);
            Assert.True(waitFor1.IsCompleted);
            Assert.False(waitFor3.IsCompleted);
            sender.Send(43);
            Assert.True(waitFor1.IsCompleted);
            Assert.True(waitFor3.IsCompleted);
        }
    }
}
