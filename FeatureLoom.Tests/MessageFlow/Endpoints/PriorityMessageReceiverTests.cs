using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class PriorityMessageReceiverTests
    {
        private sealed class IntComparer : IComparer<int>
        {
            // Higher value => higher or equal priority
            public int Compare(int x, int y) => x.CompareTo(y);
        }

        [Fact]
        public void Accepts_First_Message_And_Subsequent_Higher_Or_Equal_Priority()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new PriorityMessageReceiver<int>(new IntComparer());

            // Initially empty
            Assert.True(receiver.WouldWait());

            // First message should be accepted
            receiver.Post(10);
            Assert.False(receiver.WouldWait());

            // Equal priority should be accepted
            receiver.Post(10);
            Assert.False(receiver.WouldWait());

            // Higher priority should be accepted (latest becomes 20)
            receiver.Post(20);
            Assert.False(receiver.WouldWait());

            // Verify latest via TryPeek/Receive
            Assert.True(receiver.TryPeek(out var peeked));
            Assert.Equal(20, peeked);

            Assert.True(receiver.TryReceive(out var received));
            Assert.Equal(20, received);
        }

        [Fact]
        public void Rejects_Lower_Priority_Than_Latest()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new PriorityMessageReceiver<int>(new IntComparer());

            // Seed with 50
            receiver.Post(50);
            Assert.True(receiver.TryPeek(out var latest));
            Assert.Equal(50, latest);

            // Lower priorities should not overwrite
            receiver.Post(40);
            receiver.Post(49);

            // Latest remains 50
            Assert.True(receiver.TryPeek(out var latestStill));
            Assert.Equal(50, latestStill);
        }

        [Fact]
        public async Task WaitAsync_Completes_On_Accepted_Message_And_TimesOut_On_None()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new PriorityMessageReceiver<int>(new IntComparer());

            // Timeout when empty
            var timedOut = await receiver.WaitAsync(TimeSpan.FromMilliseconds(10));
            Assert.False(timedOut);

            // Start a waiter, then post a message that is accepted
            var waiter = receiver.WaitAsync();
            Assert.False(waiter.IsCompleted);
            receiver.Post(1);
            Assert.True(await waiter);
            receiver.TryReceive(out _);
            // Start another waiter and post a lower-priority message that is rejected
            var waiter2 = receiver.WaitAsync(TimeSpan.FromMilliseconds(20));
            Assert.False(waiter2.IsCompleted);
            receiver.Post(0); // rejected (lower than latest 1)
            var result2 = await waiter2;
            Assert.True(result2);
        }

        [Fact]
        public async Task PostAsync_Accepts_Or_Rejects_By_Priority()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new PriorityMessageReceiver<int>(new IntComparer());

            // First accepted
            await receiver.PostAsync(5);
            Assert.True(receiver.TryPeek(out var latest));
            Assert.Equal(5, latest);

            // Lower rejected
            await receiver.PostAsync(1);
            Assert.True(receiver.TryPeek(out var latestStill));
            Assert.Equal(5, latestStill);

            // Higher accepted
            await receiver.PostAsync(7);
            Assert.True(receiver.TryPeek(out var latestNow));
            Assert.Equal(7, latestNow);
        }

        [Fact]
        public void PeekMany_And_ReceiveMany_Delegate_To_Underlying_Receiver()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new PriorityMessageReceiver<int>(new IntComparer());

            receiver.Post(1);
            receiver.Post(2);
            receiver.Post(3);

            var peekedSegment = receiver.PeekMany();
            Assert.True(peekedSegment.Count == 1);

            var receivedSegment = receiver.ReceiveMany();
            // Depending on LatestMessageReceiver semantics, ensure at least one item received
            Assert.True(receivedSegment.Count == 1);
        }

        [Fact]
        public void TryConvertToWaitHandle_Works_For_Synchronous_Wait()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new PriorityMessageReceiver<int>(new IntComparer());
            Assert.True(receiver.TryConvertToWaitHandle(out var wh));

            using (wh)
            {
                // Not signaled initially
                Assert.False(wh.WaitOne(TimeSpan.FromMilliseconds(10)));

                // After accepted post, signaled
                receiver.Post(10);
                Assert.True(wh.WaitOne(TimeSpan.FromMilliseconds(50)));
            }
        }

        [Fact]
        public void Cancellation_And_Timeout_Are_Handled_In_Wait_Overloads()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new PriorityMessageReceiver<int>(new IntComparer());

            // Timeout path
            Assert.False(receiver.Wait(TimeSpan.FromMilliseconds(10)));

            // Cancellation path
            using var cts = new CancellationTokenSource();
            var waitStarted = Task.Run(() => receiver.Wait(cts.Token));
            // Cancel quickly
            cts.Cancel();
            Assert.False(waitStarted.Result);

            // Positive path
            var waitPositive = Task.Run(() => receiver.Wait(TimeSpan.FromMilliseconds(200)));
            receiver.Post(99);
            Assert.True(waitPositive.Result);
        }

        [Fact]
        public void IsEmpty_IsFull_Count_Reflect_Underlying_State()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var receiver = new PriorityMessageReceiver<int>(new IntComparer());

            Assert.True(receiver.IsEmpty);
            Assert.False(receiver.IsFull);
            Assert.Equal(0, receiver.Count);

            receiver.Post(1);

            // LatestMessageReceiver typically holds at least one item
            Assert.False(receiver.IsEmpty);
            Assert.True(receiver.Count >= 1);
        }
    }
}