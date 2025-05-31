using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}