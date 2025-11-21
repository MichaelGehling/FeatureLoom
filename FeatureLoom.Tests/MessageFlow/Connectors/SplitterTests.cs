using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class SplitterTests
    {
        [Fact]
        public void CanSplitMessageIntoMultiple()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var splitter = new Splitter<string, char>(str => str.ToCharArray());
            var receiver = new QueueReceiver<char>();
            sender.ConnectTo(splitter).ConnectTo(receiver);

            sender.Send("HELLO");
            Assert.Equal(5, receiver.Count);
            Assert.Equal('H', receiver.TryReceive(out char msg1) ? msg1 : ' ');
            Assert.Equal('E', receiver.TryReceive(out char msg2) ? msg2 : ' ');
            Assert.Equal('L', receiver.TryReceive(out char msg3) ? msg3 : ' ');
            Assert.Equal('L', receiver.TryReceive(out char msg4) ? msg4 : ' ');
            Assert.Equal('O', receiver.TryReceive(out char msg5) ? msg5 : ' ');
        }

        [Fact]
        public void ForwardsOtherMessagesWhenEnabled()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var splitter = new Splitter<string, char>(str => str.ToCharArray(), forwardOtherMessages: true);
            var charReceiver = new QueueReceiver<char>();
            var intReceiver = new QueueReceiver<int>();

            sender.ConnectTo(splitter).ConnectTo(charReceiver);
            // Also connect an int receiver to ensure non-matching messages get forwarded unchanged.
            splitter.ConnectTo(intReceiver);

            sender.Send(42);

            Assert.True(intReceiver.TryReceive(out int v) && v == 42);
            Assert.True(charReceiver.IsEmpty);
        }

        [Fact]
        public void DoesNotForwardOtherMessagesWhenDisabled()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var splitter = new Splitter<string, char>(str => str.ToCharArray(), forwardOtherMessages: false);
            var charReceiver = new QueueReceiver<char>();
            var intReceiver = new QueueReceiver<int>();

            sender.ConnectTo(splitter).ConnectTo(charReceiver);
            splitter.ConnectTo(intReceiver);

            sender.Send(7);

            Assert.True(intReceiver.IsEmpty);
            Assert.True(charReceiver.IsEmpty);
        }

        [Fact]
        public void SplitReturnsNullDoesNotForward()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var splitter = new Splitter<string, char>(str => str == "NULL" ? null : str.ToCharArray());
            var receiver = new QueueReceiver<char>();
            sender.ConnectTo(splitter).ConnectTo(receiver);

            sender.Send("NULL");
            Assert.True(receiver.IsEmpty);
        }

        [Fact]
        public async Task AsyncPostPreservesOrder()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var splitter = new Splitter<string, char>(str => str.ToCharArray());
            var receiver = new QueueReceiver<char>();
            sender.ConnectTo(splitter).ConnectTo(receiver);

            await sender.SendAsync("ABC");

            Assert.Equal(3, receiver.Count);
            Assert.True(receiver.TryReceive(out var a) && a == 'A');
            Assert.True(receiver.TryReceive(out var b) && b == 'B');
            Assert.True(receiver.TryReceive(out var c) && c == 'C');
        }

        [Fact]
        public void FansOutToMultipleSinks()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender();
            var splitter = new Splitter<string, char>(str => str.ToCharArray());
            var r1 = new QueueReceiver<char>();
            var r2 = new QueueReceiver<char>();

            sender.ConnectTo(splitter).ConnectTo(r1);
            splitter.ConnectTo(r2);

            sender.Send("HI");

            Assert.Equal(2, r1.Count);
            Assert.Equal(2, r2.Count);

            Assert.True(r1.TryReceive(out var h1) && h1 == 'H');
            Assert.True(r1.TryReceive(out var i1) && i1 == 'I');

            Assert.True(r2.TryReceive(out var h2) && h2 == 'H');
            Assert.True(r2.TryReceive(out var i2) && i2 == 'I');
        }
    }
}