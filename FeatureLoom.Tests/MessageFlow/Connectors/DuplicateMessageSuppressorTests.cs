using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Threading;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class DuplicateMessageSuppressorTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var suppressor = new DuplicateMessageSuppressor<T>(100.Milliseconds());
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(suppressor).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void SuppressesIdenticalMessagesWithinTimeFrame()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var suppressionTime = 100.Milliseconds();
            var sender = new Sender();
            var suppressor = new DuplicateMessageSuppressor<int>(suppressionTime, null, true);
            var counter = new MessageCounter();
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
            using var testContext = TestHelper.PrepareTestContext();

            var suppressionTime = 100.Milliseconds();
            var sender = new Sender();
            var suppressor = new DuplicateMessageSuppressor<int>(suppressionTime, (a, b) =>
                {
                    return Math.Abs(a - b) <= 1;
                });
            var counter = new MessageCounter();
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