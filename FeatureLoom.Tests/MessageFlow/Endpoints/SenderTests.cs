using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class SenderTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanSendObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void CanConnectToMultipleSinks()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var sinkInt1 = new LatestMessageReceiver<int>();
            var sinkInt2 = new LatestMessageReceiver<int>();
            var sinkString = new LatestMessageReceiver<string>();
            sender.ConnectTo(sinkInt1);
            sender.ConnectTo(sinkInt2);
            sender.ConnectTo(sinkString);

            Assert.Equal(3, sender.CountConnectedSinks);

            sender.Send(42);
            sender.Send("test string");

            Assert.True(sinkInt1.HasMessage);
            Assert.Equal(42, sinkInt1.LatestMessageOrDefault);
            Assert.True(sinkInt2.HasMessage);
            Assert.Equal(42, sinkInt2.LatestMessageOrDefault);
            Assert.True(sinkString.HasMessage);
            Assert.Equal("test string", sinkString.LatestMessageOrDefault);
        }

        [Fact]
        public void CanDisconnectSinks()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var sinkInt1 = new LatestMessageReceiver<int>();
            var sinkInt2 = new LatestMessageReceiver<int>();
            sender.ConnectTo(sinkInt1);
            sender.ConnectTo(sinkInt2);
            Assert.Equal(2, sender.CountConnectedSinks);
            sender.DisconnectFrom(sinkInt2);
            Assert.Equal(1, sender.CountConnectedSinks);

            sender.Send(42);

            Assert.True(sinkInt1.HasMessage);
            Assert.Equal(42, sinkInt1.LatestMessageOrDefault);
            Assert.False(sinkInt2.HasMessage);
            Assert.NotEqual(42, sinkInt2.LatestMessageOrDefault);
        }

        [Fact]
        public void ProvidesConnectedSinks()
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender();
            var sinkInt1 = new LatestMessageReceiver<int>();
            var sinkInt2 = new LatestMessageReceiver<int>();
            sender.ConnectTo(sinkInt1);
            sender.ConnectTo(sinkInt2);
            Assert.Contains(sinkInt1, sender.GetConnectedSinks());
            Assert.Contains(sinkInt2, sender.GetConnectedSinks());
        }
    }
}