using System;
using System.Linq;
using System.Threading.Tasks;
using FeatureLoom.MessageFlow;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class ReceiverBufferTests
    {
        [Fact]
        public void TryReceive_ReturnsFalse_WhenEmpty()
        {
            var receiver = new QueueReceiver<int>();
            var buffer = new ReceiverBuffer<int>(receiver);
            Assert.False(buffer.TryReceive(out var _));
        }

        [Fact]
        public void TryReceive_ReturnsItemsInOrder()
        {
            var receiver = new QueueReceiver<int>();
            receiver.Post(1);
            receiver.Post(2);
            receiver.Post(3);
            var buffer = new ReceiverBuffer<int>(receiver, maxBufferSize: 2);

            Assert.True(buffer.TryReceive(out var a));
            Assert.Equal(1, a);
            Assert.True(buffer.TryReceive(out var b));
            Assert.Equal(2, b);
            Assert.True(buffer.TryReceive(out var c));
            Assert.Equal(3, c);
            Assert.False(buffer.TryReceive(out var _));
        }

        [Fact]
        public void ReceiveMany_ReturnsUpToMaxItems()
        {
            var receiver = new QueueReceiver<int>();
            for (int i = 1; i <= 5; i++) receiver.Post(i);
            var buffer = new ReceiverBuffer<int>(receiver, maxBufferSize: 3);

            var seg = buffer.ReceiveMany(4);
            Assert.Equal(new[] { 1, 2, 3, 4 }, seg.ToArray());
            Assert.Equal(1, buffer.Count);
        }

        [Fact]
        public void PeekMany_DoesNotRemoveItems()
        {
            var receiver = new QueueReceiver<int>();
            for (int i = 1; i <= 3; i++) receiver.Post(i);
            var buffer = new ReceiverBuffer<int>(receiver, maxBufferSize: 2);

            var peeked = buffer.PeekMany(2);
            Assert.Equal(new[] { 1, 2 }, peeked.ToArray());
            Assert.Equal(3, buffer.Count);

            Assert.True(buffer.TryReceive(out var first));
            Assert.Equal(1, first);
        }

        [Fact]
        public void TryPeek_ReturnsNextItemWithoutRemoving()
        {
            var receiver = new QueueReceiver<int>();
            receiver.Post(42);
            var buffer = new ReceiverBuffer<int>(receiver);

            Assert.True(buffer.TryPeek(out var value));
            Assert.Equal(42, value);
            Assert.True(buffer.TryReceive(out var value2));
            Assert.Equal(42, value2);
        }

        [Fact]
        public void Buffer_FetchesBatch_WhenDepleted()
        {
            var receiver = new QueueReceiver<int>();
            for (int i = 1; i <= 10; i++) receiver.Post(i);
            var buffer = new ReceiverBuffer<int>(receiver, maxBufferSize: 4);

            for (int i = 1; i <= 10; i++)
            {
                Assert.True(buffer.TryReceive(out var value));
                Assert.Equal(i, value);
            }
            Assert.False(buffer.TryReceive(out var _));
        }
    }
}