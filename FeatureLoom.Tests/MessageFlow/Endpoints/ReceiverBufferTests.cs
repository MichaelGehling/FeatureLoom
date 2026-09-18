using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Helpers;
using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class ReceiverBufferTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void LocalBatchCopy_PreservesNonzeroDestinationOffset(bool peek)
        {
            var receiver = new QueueReceiver<int>();
            var buffer = new ReceiverBuffer<int>(receiver);
            receiver.Post(10);
            receiver.Post(20);
            receiver.Post(30);
            Assert.True(buffer.TryReceive(out _));
            var destination = new SlicedBuffer<int>();
            var prefix = destination.GetSlice(3);
            for (int i = 0; i < prefix.Count; i++) prefix.Array[prefix.Offset + i] = -1;

            var result = peek ? buffer.PeekMany(1, destination) : buffer.ReceiveMany(1, destination);

            Assert.Equal(new[] { 20 }, result.ToArray());
            Assert.Equal(new[] { -1, -1, -1 }, prefix.ToArray());
            Assert.Equal(peek ? 2 : 1, buffer.Count);
            while (buffer.TryReceive(out _)) { }
        }

        [Fact]
        public void Notifier_ReportsLocalBatchAvailabilityAndDrain()
        {
            var receiver = new QueueReceiver<int>();
            var buffer = new ReceiverBuffer<int>(receiver);
            bool notified = false;
            buffer.Notifier.ProcessMessage<bool>(set => notified = set);
            receiver.Post(1);
            receiver.Post(2);
            Assert.True(notified);
            Assert.True(buffer.TryReceive(out _));
            Assert.True(receiver.IsEmpty);
            Assert.True(notified);
            Assert.True(buffer.TryReceive(out _));
            Assert.False(notified);
        }

        [Fact]
        public async Task WaitHandle_IgnoresOutOfOrderQueueNotifications()
        {
            var receiver = new QueueReceiver<int>();
            using var resetEntered = new ManualResetEventSlim();
            using var releaseReset = new ManualResetEventSlim();
            // Hold a reset notification before it reaches the buffer, while a later Set is delivered.
            receiver.Notifier.ProcessMessage<bool>(set =>
            {
                if (!set)
                {
                    resetEntered.Set();
                    Assert.True(releaseReset.Wait(TimeSpan.FromSeconds(5)));
                }
            });
            var buffer = new ReceiverBuffer<int>(receiver);
            receiver.Post(1);
            var reset = Task.Run(() => receiver.Clear());
            try
            {
                Assert.True(resetEntered.Wait(TimeSpan.FromSeconds(5)));
                receiver.Post(2);
            }
            finally
            {
                releaseReset.Set();
                await reset;
            }
            Assert.False(receiver.WaitHandle.WouldWait());
            Assert.False(buffer.WaitHandle.WouldWait());
            Assert.True(buffer.WaitingTask.IsCompleted);
            Assert.True(await buffer.WaitHandle.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(buffer.TryReceive(out var item));
            Assert.Equal(2, item);
        }

        [Fact]
        public async Task ConcurrentProducerAndSingleConsumer_MakeProgressWithoutPolling()
        {
            const int count = 10000;
            var receiver = new QueueReceiver<int>();
            var buffer = new ReceiverBuffer<int>(receiver, maxBufferSize: 7);
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var consumer = Task.Run(async () =>
            {
                var handle = buffer.WaitHandle;
                for (int expected = 0; expected < count;)
                {
                    stop.Token.ThrowIfCancellationRequested();
                    if (buffer.TryReceive(out var item)) Assert.Equal(expected++, item);
                    else if (buffer.IsEmpty) Assert.True(await handle.WaitAsync(stop.Token));
                }
            });
            var producer = Task.Run(() =>
            {
                for (int i = 0; i < count && !stop.IsCancellationRequested; i++)
                {
                    receiver.Post(i);
                    Thread.Yield();
                }
            });
            try
            {
                await Task.WhenAll(producer, consumer);
                Assert.True(buffer.IsEmpty);
            }
            finally
            {
                stop.Cancel();
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task WaitHandle_DoesNotLoseWakeup_AfterDelayedQueueSet(int postMode)
        {
            var receiver = new QueueReceiver<int>();
            var buffer = new ReceiverBuffer<int>(receiver);
            var handle = buffer.WaitHandle;
            receiver.Post(1);
            Assert.True(buffer.TryReceive(out var first));
            Assert.Equal(1, first);

            // Replay a producer's trailing Set after its enqueued message was already drained.
            ((AsyncManualResetEvent)receiver.WaitHandle).Set();
            Assert.False(buffer.TryReceive(out _));
            Assert.False(receiver.WaitHandle.WouldWait());

            using var cancellation = new CancellationTokenSource();
            var wait = handle.WaitAsync(cancellation.Token);
            var waitingTask = buffer.WaitingTask;
            try
            {
                for (int i = 2; i <= 101; i++)
                {
                    if (postMode == 0) receiver.Post(i);
                    else if (postMode == 1) receiver.Post(in i);
                    else await receiver.PostAsync(i);
                }

                Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(2))));
                Assert.True(await wait);
                Assert.Same(waitingTask, await Task.WhenAny(waitingTask, Task.Delay(TimeSpan.FromSeconds(2))));
                Assert.False(handle.WouldWait());
                for (int i = 2; i <= 101; i++)
                {
                    Assert.True(buffer.TryReceive(out var item));
                    Assert.Equal(i, item);
                }
                Assert.True(buffer.IsEmpty);
                Assert.True(handle.WouldWait());
            }
            finally
            {
                cancellation.Cancel();
                await wait;
            }
        }

        [Fact]
        public async Task CachedWaitHandle_TracksLocalBatchAndNextPost()
        {
            var receiver = new QueueReceiver<int>();
            var buffer = new ReceiverBuffer<int>(receiver);
            var handle = buffer.WaitHandle;
            Assert.True(handle.WouldWait());
            receiver.Post(1);
            receiver.Post(2);
            receiver.Post(3);
            Assert.True(buffer.TryReceive(out var first));
            Assert.Equal(1, first);
            Assert.True(receiver.IsEmpty);
            Assert.True(receiver.WaitHandle.WouldWait());
            Assert.Same(handle, buffer.WaitHandle);
            Assert.False(handle.WouldWait());
            Assert.True(handle.WaitingTask.IsCompleted);
            Assert.True(buffer.WaitingTask.IsCompleted);
            Assert.True(handle.Wait());
            Assert.True(handle.Wait(TimeSpan.Zero));
            Assert.True(handle.Wait(CancellationToken.None));
            Assert.True(handle.Wait(TimeSpan.Zero, CancellationToken.None));
            Assert.True(await handle.WaitAsync());
            Assert.True(await handle.WaitAsync(TimeSpan.Zero));
            Assert.True(await handle.WaitAsync(CancellationToken.None));
            Assert.True(await handle.WaitAsync(TimeSpan.Zero, CancellationToken.None));

            Assert.Equal(new[] { 2, 3 }, buffer.ReceiveMany(2).ToArray());
            Assert.True(handle.WouldWait());
            var pending = handle.WaitingTask;
            Assert.False(pending.IsCompleted);
            receiver.Post(4);
            Assert.Same(pending, await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(2))));
            Assert.True(buffer.TryReceive(out var last));
            Assert.Equal(4, last);
        }

        [Fact]
        public async Task EmptyBuffer_WaitsHonorTimeoutAndCancellation()
        {
            var buffer = new ReceiverBuffer<int>(new QueueReceiver<int>());
            var handle = buffer.WaitHandle;
            using var cancellation = new CancellationTokenSource();
            var pending = handle.WaitAsync(cancellation.Token);
            var timedPending = handle.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token);
            Assert.False(pending.IsCompleted);
            cancellation.Cancel();
            Assert.False(await pending);
            Assert.False(await timedPending);
            Assert.False(handle.Wait(cancellation.Token));
            Assert.False(handle.Wait(TimeSpan.FromSeconds(5), cancellation.Token));
            Assert.False(handle.Wait(TimeSpan.Zero));
            Assert.False(handle.Wait(TimeSpan.Zero, CancellationToken.None));
            Assert.False(await handle.WaitAsync(TimeSpan.Zero));
            Assert.False(await handle.WaitAsync(TimeSpan.Zero, CancellationToken.None));
        }

        [Fact]
        public void WaitHandle_DoesNotExposeStaleNativeEvent()
        {
            var receiver = new QueueReceiver<int>();
            var buffer = new ReceiverBuffer<int>(receiver);
            var handle = buffer.WaitHandle;
            Assert.False(handle.TryConvertToWaitHandle(out var native));
            Assert.Null(native);
            receiver.Post(1);
            receiver.Post(2);
            Assert.True(buffer.TryReceive(out _));
            Assert.False(handle.TryConvertToWaitHandle(out native));
            Assert.Null(native);
            Assert.True(buffer.TryReceive(out _));
        }

        [Fact]
        public void TryReceive_ReturnsFalse_WhenEmpty()
        {
            var receiver = new QueueReceiver<int>();
            var buffer = new ReceiverBuffer<int>(receiver);
            Assert.False(buffer.TryReceive(out var _));
        }

        [Fact]
        public void WaitHandle_IsSet_WhenWrappingPopulatedReceiver()
        {
            var receiver = new QueueReceiver<int>();
            receiver.Post(42);

            var buffer = new ReceiverBuffer<int>(receiver);

            Assert.True(buffer.WaitingTask.IsCompleted);
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