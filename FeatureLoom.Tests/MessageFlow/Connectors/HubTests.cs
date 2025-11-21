using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class HubTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var hub = new Hub();
            var senderSocket = hub.CreateSocket(sender);
            var sink = new LatestMessageReceiver<T>();
            var sinkSocket = hub.CreateSocket(sink);
            sender.ConnectTo(senderSocket);
            sinkSocket.ConnectTo(sink);

            sender.Send(message);
            Assert.True(sink.HasMessage);
            Assert.Equal(message, sink.LatestMessageOrDefault);
        }

        [Fact]
        public void MessagesAreForwardedToAllButSendingSocket()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var senderA = new Sender();
            var senderB = new Sender();
            var senderC = new Sender();
            var counterA = new MessageCounter();
            var counterB = new MessageCounter();
            var counterC = new MessageCounter();
            var hub = new Hub();
            var socketA = hub.CreateSocket();
            var socketB = hub.CreateSocket();
            var socketC = hub.CreateSocket();
            senderA.ConnectTo(socketA);
            socketA.ConnectTo(counterA);
            senderB.ConnectTo(socketB);
            socketB.ConnectTo(counterB);
            senderC.ConnectTo(socketC);
            socketC.ConnectTo(counterC);

            senderA.Send(42);
            Assert.Equal(0, counterA.Counter);
            Assert.Equal(1, counterB.Counter);
            Assert.Equal(1, counterC.Counter);

            senderB.Send(42);
            Assert.Equal(1, counterA.Counter);
            Assert.Equal(1, counterB.Counter);
            Assert.Equal(2, counterC.Counter);
        }

        [Fact]
        public void SocketWithOwnerIsAutoRemovedAfterGC()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var hub = new Hub();
            var sender = new Sender();
            var senderSocket = hub.CreateSocket();
            sender.ConnectTo(senderSocket);

            var counterOwned = new MessageCounter();

            // Create owned socket and return only a weak reference to the owner (no strong refs in this frame).
            var ownerWeak = CreateOwnedSocket(hub, counterOwned);

            // First send is delivered as owner is alive.
            sender.Send(1);
            Assert.Equal(1, counterOwned.Counter);

            // Force GC until the weak reference reports dead (avoid local-lifetime/JIT surprises).
            CollectUntilDead(ownerWeak);

            // Trigger forwarding: the owned socket should remove itself on receive and NOT forward the message.
            sender.Send(2);
            Assert.Equal(1, counterOwned.Counter);

            // Subsequent sends should also not be delivered (socket stays removed).
            sender.Send(3);
            Assert.Equal(1, counterOwned.Counter);
        }

        [Fact]
        public void RemoveSocketByOwnerRemovesAllMatching()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var hub = new Hub();
            var sender = new Sender();
            var senderSocket = hub.CreateSocket();
            sender.ConnectTo(senderSocket);

            var owner1 = new object();
            var owner2 = new object();

            var c1 = new MessageCounter();
            var c2 = new MessageCounter();
            var c3 = new MessageCounter();

            var s1 = hub.CreateSocket(owner1);
            s1.ConnectTo(c1);
            var s2 = hub.CreateSocket(owner1);
            s2.ConnectTo(c2);
            var s3 = hub.CreateSocket(owner2);
            s3.ConnectTo(c3);

            sender.Send(42);
            Assert.Equal(1, c1.Counter);
            Assert.Equal(1, c2.Counter);
            Assert.Equal(1, c3.Counter);

            // Remove all sockets owned by owner1
            hub.RemoveSocketByOwner(owner1);

            sender.Send(43);
            Assert.Equal(1, c1.Counter); // unchanged
            Assert.Equal(1, c2.Counter); // unchanged
            Assert.Equal(2, c3.Counter); // still receives
        }

        [Fact]
        public async Task PostAsyncAwaitsAllReceivers()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var hub = new Hub();
            var senderSocket = hub.CreateSocket();
            var awaitableSink = new AwaitableSink();

            var receiverSocket = hub.CreateSocket();
            receiverSocket.ConnectTo(awaitableSink);

            var sender = new Sender();
            sender.ConnectTo(senderSocket);

            var postTask = senderSocket.PostAsync(123);

            var timedOut = await Task.WhenAny(postTask, Task.Delay(100)) != postTask;
            Assert.True(timedOut);
            Assert.Equal(1, awaitableSink.PostAsyncCalls);

            awaitableSink.Complete();
            await postTask;
            Assert.True(postTask.IsCompletedSuccessfully);
            Assert.Equal(1, awaitableSink.PostAsyncCalls);
        }

        // Helper: create owned socket and return only a weak reference to the owner.
        private static WeakReference CreateOwnedSocket(Hub hub, IMessageSink sink)
        {
            var owner = new object();
            var ownedSocket = hub.CreateSocket(owner);
            ownedSocket.ConnectTo(sink);
            return new WeakReference(owner);
        }

        // Helper: force GC a few times until the weak reference is dead (or timeout).
        private static void CollectUntilDead(WeakReference wr, int maxAttempts = 20)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                if (!wr.IsAlive) return;
                AppTime.Wait(TimeSpan.FromMilliseconds(10));
            }
            // Final check to make assertion failures clearer at call site
            Assert.False(wr.IsAlive);
        }

        // Helper sink for async-await verification
        private sealed class AwaitableSink : IMessageSink
        {
            private readonly TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int postAsyncCalls = 0;

            public int PostAsyncCalls => postAsyncCalls;

            public void Post<M>(in M message) { }
            public void Post<M>(M message) { }

            public Task PostAsync<M>(M message)
            {
                Interlocked.Increment(ref postAsyncCalls);
                return tcs.Task;
            }

            public void Complete() => tcs.TrySetResult(true);
        }
    }
}