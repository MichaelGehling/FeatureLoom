using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Collections;
using Xunit;

namespace FeatureLoom.Collections
{
    public class CircularLogBufferTests
    {
        [Fact]
        public void AddAndRetrieve_Items_AreCorrect()
        {
            var buffer = new CircularLogBuffer<int>(3);

            buffer.Add(1);
            buffer.Add(2);
            buffer.Add(3);

            Assert.Equal(3, buffer.CurrentSize);
            Assert.Equal(3, buffer.MaxSize);

            long firstId = buffer.LatestId - 2;
            for (int i = 0; i < 3; i++)
            {
                Assert.True(buffer.TryGetFromId(firstId + i, out int value));
                Assert.Equal(i + 1, value);
            }
        }

        [Fact]
        public void Overwrites_Oldest_WhenFull()
        {
            var buffer = new CircularLogBuffer<int>(3);

            buffer.Add(1);
            buffer.Add(2);
            buffer.Add(3);
            buffer.Add(4); // Overwrites 1

            Assert.Equal(3, buffer.CurrentSize);
            Assert.False(buffer.Contains(1));
            Assert.True(buffer.Contains(2));
            Assert.True(buffer.Contains(3));
            Assert.True(buffer.Contains(4));
        }

        [Fact]
        public void GetLatest_ReturnsMostRecent()
        {
            var buffer = new CircularLogBuffer<string>(2);

            buffer.Add("A");
            buffer.Add("B");
            Assert.Equal("B", buffer.GetLatest());

            buffer.Add("C");
            Assert.Equal("C", buffer.GetLatest());
        }

        [Fact]
        public void Reset_ClearsBuffer()
        {
            var buffer = new CircularLogBuffer<int>(2);
            buffer.Add(1);
            buffer.Add(2);
            buffer.Reset();

            Assert.Equal(0, buffer.CurrentSize);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetLatest());
        }

        [Fact]
        public void AddRange_AddsAllItems()
        {
            var buffer = new CircularLogBuffer<int>(5);
            buffer.AddRange(new List<int> { 1, 2, 3 });

            Assert.Equal(3, buffer.CurrentSize);
            Assert.True(buffer.Contains(1));
            Assert.True(buffer.Contains(2));
            Assert.True(buffer.Contains(3));
        }

        [Fact]
        public void GetAllAvailable_ReturnsCorrectItems()
        {
            var buffer = new CircularLogBuffer<int>(3);
            buffer.Add(10);
            buffer.Add(20);
            buffer.Add(30);

            var items = buffer.GetAllAvailable(buffer.LatestId - 2, out long firstId, out long lastId);
            Assert.Equal(new[] { 10, 20, 30 }, items);
            Assert.Equal(buffer.LatestId - 2, firstId);
            Assert.Equal(buffer.LatestId + 1, lastId);
        }

        [Fact]
        public async Task WaitForIdAsync_WaitsForItem()
        {
            var buffer = new CircularLogBuffer<int>(2);

            var task = Task.Run(async () =>
            {
                await Task.Delay(100);
                buffer.Add(42);
            });

            await buffer.WaitForIdAsync(buffer.LatestId + 1);
            Assert.True(buffer.Contains(42));
        }

        [Fact]
        public void GetAllAvailable_ReturnsFromRequestedId_WithMaxItems_NoWrap()
        {
            var buf = new CircularLogBuffer<int>(10);
            // ids: 0..5
            foreach (var x in Enumerable.Range(0, 6)) buf.Add(x);

            var items = buf.GetAllAvailable(2, 2, out long first, out long last);
            Assert.Equal(new[] { 2, 3 }, items);
            Assert.Equal(2, first);
            Assert.Equal(4, last); // exclusive upper bound
        }

        [Fact]
        public void GetAllAvailable_ReturnsFromRequestedId_WithMaxItems_WithWrap()
        {
            var buf = new CircularLogBuffer<int>(5);
            // Fill up and wrap: ids 0..6, buffer holds last 5: ids 2..6 => values 2..6
            foreach (var x in Enumerable.Range(0, 7)) buf.Add(x);

            // Request from id 3, expect [3,4], not newest tail
            var items = buf.GetAllAvailable(3, 2, out long first, out long last);
            Assert.Equal(new[] { 3, 4 }, items);
            Assert.Equal(3, first);
            Assert.Equal(5, last);
        }

        [Fact]
        public void GetAllAvailable_ClampsToOldestAvailable_WhenRequestedTooOld()
        {
            var buf = new CircularLogBuffer<int>(3);
            // ids 0..4, buffer holds ids 2..4 => values 2,3,4
            foreach (var x in Enumerable.Range(0, 5)) buf.Add(x);

            // Request id 0 (too old), expect start at OldestAvailableId (=2)
            var items = buf.GetAllAvailable(0, 2, out long first, out long last);
            Assert.Equal(buf.OldestAvailableId, first);
            Assert.Equal(new[] { 2, 3 }, items);
            Assert.Equal(first + items.Length, last);
        }

        [Fact]
        public void GetAllAvailable_RespectsMaxItems_NotAlwaysNewest()
        {
            var buf = new CircularLogBuffer<string>(4);
            // ids 0..3 : A,B,C,D
            foreach (var s in new[] { "A", "B", "C", "D" }) buf.Add(s);

            // Request id 1, max 2 => must return ["B","C"], not ["C","D"]
            var items = buf.GetAllAvailable(1, 2, out long first, out long last);
            Assert.Equal(new[] { "B", "C" }, items);
            Assert.Equal(1, first);
            Assert.Equal(3, last);
        }

        [Fact]
        public void GetAllAvailable_FullWindow_FromRequestedToLatest_NoWrap()
        {
            var buf = new CircularLogBuffer<int>(8);
            foreach (var x in Enumerable.Range(0, 6)) buf.Add(x); // ids 0..5

            var items = buf.GetAllAvailable(1, out long first, out long last);
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, items);
            Assert.Equal(1, first);
            Assert.Equal(6, last);
        }

        [Fact]
        public void GetAllAvailable_FullWindow_AfterWrap_StartsAtRequested()
        {
            var buf = new CircularLogBuffer<int>(4);
            // After adding ids 0..6, buffer holds ids 3..6 => values 3..6
            foreach (var x in Enumerable.Range(0, 7)) buf.Add(x);

            // Request id 4 -> expect 4,5,6
            var items = buf.GetAllAvailable(4, out long first, out long last);
            Assert.Equal(new[] { 4, 5, 6 }, items);
            Assert.Equal(4, first);
            Assert.Equal(7, last);
        }

        [Fact]
        public void GetAllAvailable_EmptyOrFutureRequest_ReturnsEmptyAndMinusOnes()
        {
            var buf = new CircularLogBuffer<int>(3);

            // Empty
            var empty = buf.GetAllAvailable(0, 2, out long f1, out long l1);
            Assert.Empty(empty);
            Assert.Equal(-1, f1);
            Assert.Equal(-1, l1);

            // Future request
            buf.Add(1); // id 0
            var future = buf.GetAllAvailable(5, 2, out long f2, out long l2);
            Assert.Empty(future);
            Assert.Equal(-1, f2);
            Assert.Equal(-1, l2);
        }

        [Fact]
        public void CopyTo_DefaultOverloads_ReturnNewestItems_TailSemantics()
        {
            var buf = new CircularLogBuffer<int>(5);
            foreach (var x in Enumerable.Range(0, 5)) buf.Add(x); // 0..4

            var dest = new int[3];
            buf.CopyTo(dest, 0);
            // Should copy newest three: 2,3,4
            Assert.Equal(new[] { 2, 3, 4 }, dest);

            var dest2 = new int[5];
            buf.CopyTo(dest2, 0, 4);
            // Should copy newest four: 1,2,3,4
            Assert.Equal(new[] { 1, 2, 3, 4 }, dest2.Take(4).ToArray());
        }

        [Fact]
        public void TryGetFromId_IndexingMatches_GetAllAvailableElements()
        {
            var buf = new CircularLogBuffer<int>(5);
            foreach (var x in Enumerable.Range(10, 6)) buf.Add(x); // ids 0..5, buffer holds ids 1..5

            var items = buf.GetAllAvailable(2, 3, out long first, out long last); // expect ids 2,3,4
            Assert.Equal(3, items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                long id = first + i;
                Assert.True(buf.TryGetFromId(id, out int value));
                Assert.Equal(items[i], value);
            }
            Assert.Equal(last, first + items.Length);
        }
    }
}