using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class AsyncForwarderTest
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new AsyncForwarder();
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact(Skip = "Fails on GitHub test server.")]
        public void ForwardsMessagesToMultipleSinksAsynchronously()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var delay = 50.Milliseconds();
            var sender = new Sender();
            var forwarder = new AsyncForwarder();
            var delayerA = new DelayingForwarder(delay);
            var delayerB = new DelayingForwarder(delay);
            var counterA = new MessageCounter();
            var counterB = new MessageCounter();
            sender.ConnectTo(forwarder);
            forwarder.ConnectTo(delayerA).ConnectTo(counterA);
            forwarder.ConnectTo(delayerB).ConnectTo(counterB);

            var timer = AppTime.TimeKeeper;
            sender.Send(42);
            Task.WhenAll(counterA.WaitForCountAsync(1), counterB.WaitForCountAsync(1));
            Assert.True(timer.Elapsed < delay * 2);
        }
    }
}