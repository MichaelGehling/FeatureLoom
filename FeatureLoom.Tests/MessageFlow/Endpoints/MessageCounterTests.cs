using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.MessageFlow;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class MessageCounterTests
    {
        [Fact]
        public async Task WaitForCountAsync_CompletesImmediately_WhenThresholdAlreadyMet()
        {
            var counter = new MessageCounter();

            // Arrange: reach 5
            for (int i = 0; i < 5; i++) counter.Post(i);

            // Act: wait for <= current count
            var task = counter.WaitForCountAsync(3);
            var result = await task;

            // Assert
            Assert.Equal(5, result);
            Assert.Equal(5, counter.Counter);
        }

        [Fact]
        public async Task WaitForCountAsync_Completes_WhenCounterReachesThreshold()
        {
            var counter = new MessageCounter();

            // Act: start waiter first
            var task = counter.WaitForCountAsync(4);

            // Post messages
            for (int i = 0; i < 4; i++) counter.Post(i);

            var result = await task;

            // Assert
            Assert.Equal(4, result);
            Assert.Equal(4, counter.Counter);
        }

        [Fact]
        public async Task MultipleWaiters_CompleteAtOrAfterTheirExpectedCounts()
        {
            var counter = new MessageCounter();

            var wait2 = counter.WaitForCountAsync(2);
            var wait5 = counter.WaitForCountAsync(5);
            var wait3 = counter.WaitForCountAsync(3);

            // Post 5 messages
            for (int i = 0; i < 5; i++) counter.Post(i);

            var r2 = await wait2;
            var r3 = await wait3;
            var r5 = await wait5;

            Assert.Equal(2, r2);
            Assert.Equal(3, r3);
            Assert.Equal(5, r5);
            Assert.Equal(5, counter.Counter);
        }

        [Fact]
        public async Task SingleWaiterFastPath_AvoidsAllocationAndCompletesCorrectly()
        {
            var counter = new MessageCounter();

            // Arrange: one waiter whose threshold will be met by a single increment
            var wait1 = counter.WaitForCountAsync(1);

            // Act
            counter.Post("msg");

            // Assert
            var r1 = await wait1;
            Assert.Equal(1, r1);
            Assert.Equal(1, counter.Counter);
        }

        [Fact]
        public async Task PostAsync_IncrementsCounterAndCompletesWaiters()
        {
            var counter = new MessageCounter();

            var wait3 = counter.WaitForCountAsync(3);

            await counter.PostAsync("a");
            await counter.PostAsync("b");
            await counter.PostAsync("c");

            var r3 = await wait3;

            Assert.Equal(3, r3);
            Assert.Equal(3, counter.Counter);
        }

        [Fact]
        public async Task ConcurrentPosts_AreCountedCorrectly_AndWaitersComplete()
        {
            var counter = new MessageCounter();
            var target = 1000;
            var waiter = counter.WaitForCountAsync(target);

            // Post concurrently
            var tasks = new Task[10];
            for (int t = 0; t < tasks.Length; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < target / tasks.Length; i++)
                    {
                        counter.Post(i);
                    }
                });
            }

            await Task.WhenAll(tasks);

            var result = await waiter;
            Assert.Equal(target, result);
            Assert.Equal(target, counter.Counter);
        }

        [Fact]
        public async Task WaitForCountAsync_ZeroOrNegative_CompletesImmediatelyWithCurrentCount()
        {
            var counter = new MessageCounter();

            // No posts yet
            var w0 = await counter.WaitForCountAsync(0);
            var wNeg = await counter.WaitForCountAsync(-5);

            Assert.Equal(0, w0);
            Assert.Equal(0, wNeg);

            // After some posts
            counter.Post(1);
            counter.Post(2);

            var w0b = await counter.WaitForCountAsync(0);
            var wNegb = await counter.WaitForCountAsync(-1);

            Assert.Equal(2, w0b);
            Assert.Equal(2, wNegb);
        }

        [Fact]
        public async Task WaitersAddedAreSortedDescending_ByExpectedCount()
        {
            var counter = new MessageCounter();

            // Add multiple waiters out of order; they should complete together when threshold reached
            var w10 = counter.WaitForCountAsync(10);
            var w1 = counter.WaitForCountAsync(1);
            var w5 = counter.WaitForCountAsync(5);

            for (int i = 0; i < 10; i++) counter.Post(i);

            var r1 = await w1;
            var r5 = await w5;
            var r10 = await w10;

            Assert.Equal(1, r1);
            Assert.Equal(5, r5);
            Assert.Equal(10, r10);
            Assert.Equal(10, counter.Counter);
        }
    }
}