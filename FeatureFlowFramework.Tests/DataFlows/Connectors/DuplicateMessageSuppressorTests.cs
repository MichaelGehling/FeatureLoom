using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.Helper;
using System;
using System.Threading;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class DuplicateMessageSuppressorTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            var sender = new Sender<T>();
            var suppressor = new DuplicateMessageSuppressor(100.Milliseconds());
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(suppressor).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

        [Fact]
        public void SuppressesIdenticalMessagesWithinTimeFrame()
        {
            var suppressionTime = 50.Milliseconds();
            var sender = new Sender();
            var suppressor = new DuplicateMessageSuppressor(suppressionTime);
            var counter = new CountingForwarder();
            sender.ConnectTo(suppressor).ConnectTo(counter);

            sender.Send(42);
            Assert.Equal(1, counter.Counter);
            sender.Send(42);
            Assert.Equal(1, counter.Counter);
            sender.Send(99);
            Assert.Equal(2, counter.Counter);
            sender.Send(42);
            Assert.Equal(2, counter.Counter);
            sender.Send(99);
            Assert.Equal(2, counter.Counter);

            Thread.Sleep(suppressionTime);

            sender.Send(42);
            Assert.Equal(3, counter.Counter);
            sender.Send(99);
            Assert.Equal(4, counter.Counter);
        }

        [Fact]
        public void SupportsCustomIdentityCheck()
        {
            var suppressionTime = 50.Milliseconds();
            var sender = new Sender();
            var suppressor = new DuplicateMessageSuppressor(suppressionTime, (a, b) =>
                {
                    if (a is int intA && b is int intB)
                    {
                        return Math.Abs(intA - intB) <= 1;
                    }
                    return a == b;
                });
            var counter = new CountingForwarder();
            sender.ConnectTo(suppressor).ConnectTo(counter);

            sender.Send(42);
            Assert.Equal(1, counter.Counter);
            sender.Send(42);
            Assert.Equal(1, counter.Counter);
            sender.Send(43);
            Assert.Equal(1, counter.Counter);
            sender.Send(41);
            Assert.Equal(1, counter.Counter);
            sender.Send(99);
            Assert.Equal(2, counter.Counter);
        }
    }
}