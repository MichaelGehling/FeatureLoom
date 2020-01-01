using FeatureFlowFramework.DataFlows.Test;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class SenderTests
    {

        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanSendObjectsAndValues<T>(T message)
        {
            var sender = new Sender<T>();
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

        [Fact]
        public void CanConnectToMultipleSinks()
        {
            var sender = new Sender();
            var sinkInt1 = new SingleMessageTestSink<int>();
            var sinkInt2 = new SingleMessageTestSink<int>();
            var sinkString = new SingleMessageTestSink<string>();
            sender.ConnectTo(sinkInt1);
            sender.ConnectTo(sinkInt2);
            sender.ConnectTo(sinkString);

            Assert.Equal(3, sender.CountConnectedSinks);

            sender.Send(42);
            sender.Send("test string");

            Assert.True(sinkInt1.received);
            Assert.Equal(42, sinkInt1.receivedMessage);
            Assert.True(sinkInt2.received);
            Assert.Equal(42, sinkInt2.receivedMessage);
            Assert.True(sinkString.received);
            Assert.Equal("test string", sinkString.receivedMessage);
        }

        [Fact]
        public void CanDisconnectSinks()
        {
            var sender = new Sender();
            var sinkInt1 = new SingleMessageTestSink<int>();
            var sinkInt2 = new SingleMessageTestSink<int>();
            sender.ConnectTo(sinkInt1);
            sender.ConnectTo(sinkInt2);
            Assert.Equal(2, sender.CountConnectedSinks);
            sender.DisconnectFrom(sinkInt2);
            Assert.Equal(1, sender.CountConnectedSinks);

            sender.Send(42);

            Assert.True(sinkInt1.received);
            Assert.Equal(42, sinkInt1.receivedMessage);
            Assert.False(sinkInt2.received);
            Assert.NotEqual(42, sinkInt2.receivedMessage);
        }

        [Fact]
        public void ProvidesConnectedSinks()
        {
            var sender = new Sender();
            var sinkInt1 = new SingleMessageTestSink<int>();
            var sinkInt2 = new SingleMessageTestSink<int>();
            sender.ConnectTo(sinkInt1);
            sender.ConnectTo(sinkInt2);
            Assert.Contains(sinkInt1, sender.GetConnectedSinks());
            Assert.Contains(sinkInt2, sender.GetConnectedSinks());
        }

    }
}
