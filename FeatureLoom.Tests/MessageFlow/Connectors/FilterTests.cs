using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class FilterTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var filter = new Filter<T>(msg => true);
            var sink = new LatestMessageReceiver<T>();
            sender.ConnectTo(filter).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void CanFilterMessages()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var filter = new Filter<int>(msg => msg <= 10);
            var counter = new MessageCounter();
            sender.ConnectTo(filter).ConnectTo(counter);
            sender.Send(42);
            Assert.Equal(0, counter.Counter);
            sender.Send(5);
            Assert.Equal(1, counter.Counter);
            sender.Send(10);
            Assert.Equal(2, counter.Counter);
            sender.Send(11);
            Assert.Equal(2, counter.Counter);
        }

        [Fact]
        public void DeclinedMessagesAreRoutedToElseWhenConnected()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var filter = new Filter<int>(msg => msg <= 10);
            var acceptedCounter = new MessageCounter();
            var declinedCounter = new MessageCounter();

            sender.ConnectTo(filter).ConnectTo(acceptedCounter);
            filter.Else.ConnectTo(declinedCounter);

            sender.Send(5);
            Assert.Equal(1, acceptedCounter.Counter);
            Assert.Equal(0, declinedCounter.Counter);

            sender.Send(42);
            Assert.Equal(1, acceptedCounter.Counter);
            Assert.Equal(1, declinedCounter.Counter);
        }

        [Fact]
        public void DeclinedMessagesAreDroppedWhenElseNotAccessed()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var filter = new Filter<int>(msg => msg < 0); // none accepted
            var counter = new MessageCounter();
            sender.ConnectTo(filter).ConnectTo(counter);

            sender.Send(1);
            sender.Send(2);
            sender.Send(3);

            Assert.Equal(0, counter.Counter); // all declined, Else never accessed -> dropped
        }

        [Fact]
        public void ForwardsOtherMessageTypesWhenEnabled()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var filter = new Filter<int>(msg => msg >= 0, forwardOtherMessages: true);
            var receiver = new QueueReceiver<object>();

            sender.ConnectTo(filter).ConnectTo(receiver);

            sender.Send("hello");
            sender.Send(5);

            Assert.Equal(2, receiver.Count);
            Assert.True(receiver.TryReceive(out var first) && (string)first == "hello");
            Assert.True(receiver.TryReceive(out var second) && (int)second == 5);
        }

        [Fact]
        public void RoutesOtherMessageTypesToElseWhenDisabled()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var filter = new Filter<int>(msg => msg >= 0, forwardOtherMessages: false);
            var acceptedReceiver = new QueueReceiver<int>();
            var otherReceiver = new QueueReceiver<object>();

            sender.ConnectTo(filter).ConnectTo(acceptedReceiver);
            filter.Else.ConnectTo(otherReceiver);

            sender.Send("world"); // different type -> Else
            sender.Send(7);       // accepted

            Assert.Equal(1, acceptedReceiver.Count);
            Assert.True(acceptedReceiver.TryReceive(out var v) && v == 7);

            Assert.Equal(1, otherReceiver.Count);
            Assert.True(otherReceiver.TryReceive(out var other) && (string)other == "world");
        }

        [Fact]
        public async Task AsyncForwardingPreservesOrder()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var filter = new Filter<int>(msg => true);
            var receiver = new QueueReceiver<int>();

            sender.ConnectTo(filter).ConnectTo(receiver);

            await sender.SendAsync(1);
            await sender.SendAsync(2);
            await sender.SendAsync(3);

            Assert.Equal(3, receiver.Count);
            Assert.True(receiver.TryReceive(out var a) && a == 1);
            Assert.True(receiver.TryReceive(out var b) && b == 2);
            Assert.True(receiver.TryReceive(out var c) && c == 3);
        }

        [Fact]
        public void FansOutAcceptedMessagesToMultipleSinks()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var filter = new Filter<int>(msg => msg % 2 == 0);
            var r1 = new QueueReceiver<int>();
            var r2 = new QueueReceiver<int>();

            sender.ConnectTo(filter).ConnectTo(r1);
            filter.ConnectTo(r2);

            sender.Send(2);
            sender.Send(3);
            sender.Send(4);

            Assert.Equal(2, r1.Count);
            Assert.Equal(2, r2.Count);

            Assert.True(r1.TryReceive(out var r1a) && r1a == 2);
            Assert.True(r1.TryReceive(out var r1b) && r1b == 4);
            Assert.True(r2.TryReceive(out var r2a) && r2a == 2);
            Assert.True(r2.TryReceive(out var r2b) && r2b == 4);
        }

        [Fact]
        public void DisconnectPreventsFurtherForwarding()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<int>();
            var filter = new Filter<int>(msg => true);
            var receiver = new MessageCounter();

            sender.ConnectTo(filter).ConnectTo(receiver);
            sender.Send(1);
            Assert.Equal(1, receiver.Counter);

            filter.DisconnectFrom(receiver);
            sender.Send(2);

            Assert.Equal(1, receiver.Counter); // unchanged after disconnect
        }
    }
}