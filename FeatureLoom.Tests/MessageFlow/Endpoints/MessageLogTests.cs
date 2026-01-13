using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow
{
    public class MessageLogTests
    {
        [Fact]
        public void Constructor_Throws_OnNonPositiveSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MessageLog<int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MessageLog<int>(-1));
        }

        [Fact]
        public void Add_AssignsIncreasingIds_AndExposesSizes()
        {
            var log = new MessageLog<string>(10);

            var id0 = log.Add("a");
            var id1 = log.Add("b");
            var id2 = log.Add("c");

            Assert.Equal(0, id0);
            Assert.Equal(1, id1);
            Assert.Equal(2, id2);

            Assert.Equal(3, log.CurrentSize);
            Assert.Equal(2, log.LatestId);
            Assert.Equal(10, log.MaxSize);
            Assert.Equal(0, log.OldestAvailableId);
            Assert.Equal("c", log.GetLatest());
        }

        [Fact]
        public void GetLatest_Throws_WhenEmpty()
        {
            var log = new MessageLog<int>(3);
            Assert.Throws<ArgumentOutOfRangeException>(() => log.GetLatest());
        }

        [Fact]
        public void TryGetFromId_Works_ForValidIds_AndFailsForOutOfRange()
        {
            var log = new MessageLog<int>(3);
            log.Add(10);
            log.Add(20);
            log.Add(30);

            Assert.True(log.TryGetFromId(0, out int v0));
            Assert.True(log.TryGetFromId(1, out int v1));
            Assert.True(log.TryGetFromId(2, out int v2));
            Assert.Equal(10, v0);
            Assert.Equal(20, v1);
            Assert.Equal(30, v2);

            Assert.False(log.TryGetFromId(-1, out _));
            Assert.False(log.TryGetFromId(3, out _));
        }

        [Fact]
        public void BufferOverflow_DropsOldest_AndUpdatesOldestAvailableId()
        {
            var log = new MessageLog<int>(3);

            var id0 = log.Add(1); // 0
            var id1 = log.Add(2); // 1
            var id2 = log.Add(3); // 2
            var id3 = log.Add(4); // 3 -> overwrites oldest (1)

            // LatestId advanced
            Assert.Equal(id3, log.LatestId);
            Assert.Equal(3, log.LatestId);

            // OldestAvailableId advanced by overflow
            Assert.Equal(1, log.OldestAvailableId);

            // Oldest (id0) no longer available
            Assert.False(log.TryGetFromId(id0, out _));

            // Still can get ids 1..3
            Assert.True(log.TryGetFromId(1, out int v1));
            Assert.True(log.TryGetFromId(2, out int v2));
            Assert.True(log.TryGetFromId(3, out int v3));
            Assert.Equal(2, v1);
            Assert.Equal(3, v2);
            Assert.Equal(4, v3);

            // CurrentSize remains bounded by capacity
            Assert.Equal(3, log.CurrentSize);
        }

        [Fact]
        public void GetAllAvailable_ReturnsContiguousSlice_FromRequestedId()
        {
            var log = new MessageLog<string>(5);
            log.Add("a"); // 0
            log.Add("b"); // 1
            log.Add("c"); // 2

            var items = log.GetAllAvailable(1, out long first, out long last);
            Assert.Equal(2, items.Length); // ids 1,2
            Assert.Equal("b", items[0]);
            Assert.Equal("c", items[1]);
            Assert.Equal(1, first);
            Assert.Equal(3, last); // exclusive upper bound

            // with maxItems limiting
            var items2 = log.GetAllAvailable(0, 2, out long first2, out long last2);
            Assert.Equal(new[] { "a", "b" }, items2);
            Assert.Equal(0, first2);
            Assert.Equal(2, last2);
        }

        [Fact]
        public void GetAllAvailable_AfterOverflow_ClampsToOldestAvailable()
        {
            var log = new MessageLog<int>(3);
            log.Add(1); // id0
            log.Add(2); // id1
            log.Add(3); // id2
            log.Add(4); // id3 -> overflow, oldest available becomes 1

            var items = log.GetAllAvailable(0, out long first, out long last);
            Assert.Equal(3, items.Length);
            Assert.Equal(new[] { 2, 3, 4 }, items);
            Assert.Equal(log.OldestAvailableId, first);
            Assert.Equal(log.LatestId + 1, last);
        }

        [Fact]
        public void AddRange_AddsItemsAndSignalsSize()
        {
            var log = new MessageLog<int>(10);
            var range = Enumerable.Range(1, 5);
            log.AddRange(range);

            Assert.Equal(5, log.CurrentSize);
            Assert.Equal(4, log.LatestId);

            var items = log.GetAllAvailable(0, out long first, out long last);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, items);
            Assert.Equal(0, first);
            Assert.Equal(5, last);
        }

        [Fact]
        public void Post_ByValueAndByRef_OnlyAcceptsMatchingType()
        {
            var log = new MessageLog<int>(5);

            // matching type
            log.Post(10);
            int val = 20;
            log.Post<int>(in val);
            Assert.Equal(2, log.CurrentSize);
            Assert.Equal(1, log.LatestId);
            Assert.Equal(10, log.GetAllAvailable(0, out _, out _)[0]);
            Assert.Equal(20, log.GetLatest());

            // mismatched type should be ignored
            log.Post("not an int");
            log.PostAsync("also not an int").Wait();
            Assert.Equal(2, log.CurrentSize);
            Assert.Equal(1, log.LatestId);
        }

        [Fact]
        public async Task PostAsync_AddsItemAndCompletesImmediately()
        {
            var log = new MessageLog<string>(3);

            var task = log.PostAsync("x");
            Assert.True(task.IsCompleted);

            // Let the event pulse propagate
            await Task.Yield();

            Assert.Equal(0, log.LatestId);
            Assert.True(log.TryGetFromId(0, out string v));
            Assert.Equal("x", v);
        }

        [Fact]
        public void Reset_ClearsState()
        {
            var log = new MessageLog<int>(3);
            log.Add(1);
            log.Add(2);
            Assert.Equal(2, log.CurrentSize);
            Assert.Equal(1, log.LatestId);
            Assert.Equal(0, log.OldestAvailableId);

            log.Reset();

            Assert.Equal(0, log.CurrentSize);
            Assert.Equal(-1, log.LatestId);
            Assert.Equal(-1, log.OldestAvailableId);
            Assert.False(log.TryGetFromId(0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => log.GetLatest());
        }

        [Fact]
        public async Task WaitForIdAsync_Completes_WhenIdBecomesAvailable()
        {
            var log = new MessageLog<int>(3);

            // Start waiting for id 2
            var waitTask = log.WaitForIdAsync(2);
            Assert.False(waitTask.IsCompleted);

            // Add 3 items to reach id 2
            log.Add(10); // id0
            log.Add(20); // id1
            log.Add(30); // id2

            await waitTask;
            Assert.True(waitTask.IsCompleted);
            Assert.Equal(2, log.LatestId);
            Assert.True(log.TryGetFromId(2, out int v));
            Assert.Equal(30, v);
        }

        [Fact]
        public async Task WaitForIdAsync_RespectsCancellation()
        {
            var log = new MessageLog<int>(2);
            using var cts = new CancellationTokenSource();

            var waitTask = log.WaitForIdAsync(10, cts.Token);
            Assert.False(waitTask.IsCompleted);

            cts.Cancel();

            // Ensure the task completes after cancellation
            await waitTask;
            Assert.True(waitTask.IsCompleted);
        }
    }
}