using FeatureLoom.DataFlows.Test;
using FeatureLoom.Helpers.Time;
using FeatureLoom.Helpers.Diagnostics;
using FeatureLoom.Services;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.DataFlows
{
    public class AsyncForwarderTest
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new AsyncForwarder();
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }

        [Fact(Skip = "Fails on GitHub test server.")]
        public void ForwardsMessagesToMultipleSinksAsynchronously()
        {
            TestHelper.PrepareTestContext();

            var delay = 50.Milliseconds();
            var sender = new Sender();
            var forwarder = new AsyncForwarder();
            var delayerA = new DelayingForwarder(delay);
            var delayerB = new DelayingForwarder(delay);
            var counterA = new CountingForwarder();
            var counterB = new CountingForwarder();
            sender.ConnectTo(forwarder);
            forwarder.ConnectTo(delayerA).ConnectTo(counterA);
            forwarder.ConnectTo(delayerB).ConnectTo(counterB);

            var timer = AppTime.TimeKeeper;
            sender.Send(42);
            Task.WhenAll(counterA.WaitForAsync(1), counterB.WaitForAsync(1));
            Assert.True(timer.Elapsed < delay * 2);
        }
    }
}