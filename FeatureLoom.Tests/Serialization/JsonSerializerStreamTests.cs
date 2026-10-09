using FeatureLoom.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonSerializerStreamTests
    {
        private static string ReadStreamString(MemoryStream stream)
        {
            stream.Position = 0;
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        [Fact]
        public void Serialize_Stream_Sync_FlushesAllData()
        {
            string payload = new string('a', 200);
            var settings = new JsonSerializer.Settings { writeBufferChunkSize = 32 };
            var serializer = new JsonSerializer(settings);

            using var stream = new MemoryStream();
            serializer.Serialize(stream, payload);

            string json = ReadStreamString(stream);
            string expected = $"\"{payload}\"";

            Assert.Equal(expected, json);
            Assert.True(stream.Length > settings.writeBufferChunkSize);
        }

        [Fact]
        public async Task Serialize_Stream_Async_FlushesAllData()
        {
            string payload = new string('b', 200);
            var settings = new JsonSerializer.Settings { writeBufferChunkSize = 32 };
            var serializer = new JsonSerializer(settings);

            using var stream = new MemoryStream();
            await serializer.SerializeAsync(stream, payload);

            string json = ReadStreamString(stream);
            string expected = $"\"{payload}\"";

            Assert.Equal(expected, json);
            Assert.True(stream.Length > settings.writeBufferChunkSize);
        }

        private sealed class SlowAsyncStream : MemoryStream
        {
            private int activeWrites;
            public int MaxConcurrentWrites;
            public int AsyncWriteCount;
            public int SyncWriteCount;
            public int FailAfterAsyncWrites = -1;

            public override void Write(byte[] buffer, int offset, int count)
            {
                SyncWriteCount++;
                base.Write(buffer, offset, count);
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                int active = Interlocked.Increment(ref activeWrites);
                if (active > MaxConcurrentWrites) MaxConcurrentWrites = active;
                try
                {
                    // Copy before yielding to detect if the serializer modifies a buffer that is still being written.
                    byte[] copy = new byte[count];
                    System.Buffer.BlockCopy(buffer, offset, copy, 0, count);
                    await Task.Delay(1, cancellationToken);
                    for (int i = 0; i < count; i++)
                    {
                        if (copy[i] != buffer[offset + i]) throw new InvalidOperationException("Buffer modified during pending write");
                    }
                    if (FailAfterAsyncWrites >= 0 && AsyncWriteCount >= FailAfterAsyncWrites) throw new IOException("Simulated failure");
                    AsyncWriteCount++;
                    base.Write(copy, 0, count);
                }
                finally
                {
                    Interlocked.Decrement(ref activeWrites);
                }
            }
        }

        private class AsyncTestItem
        {
            public string name;
            public int[] numbers;
            public List<AsyncTestItem> children;
        }

        private static AsyncTestItem CreateAsyncTestItem(int depth)
        {
            var item = new AsyncTestItem
            {
                name = "item_" + depth,
                numbers = Enumerable.Range(0, 50).ToArray(),
                children = new List<AsyncTestItem>()
            };
            if (depth > 0)
            {
                for (int i = 0; i < 3; i++) item.children.Add(CreateAsyncTestItem(depth - 1));
            }
            return item;
        }

        [Fact]
        public void Serialize_Stream_Sync_Null_WritesNull()
        {
            var serializer = new JsonSerializer();
            using var stream = new MemoryStream();
            serializer.Serialize<string>(stream, null);
            Assert.Equal("null", ReadStreamString(stream));
        }

        [Fact]
        public async Task SerializeAsync_Stream_Null_WritesNull()
        {
            var serializer = new JsonSerializer();
            using var stream = new MemoryStream();
            await serializer.SerializeAsync<string>(stream, null);
            Assert.Equal("null", ReadStreamString(stream));
        }

        [Fact]
        public async Task SerializeAsync_MultipleChunks_WritesAsyncWithoutOverlapAndMatchesSync()
        {
            var settings = new JsonSerializer.Settings { writeBufferChunkSize = 256 };
            var serializer = new JsonSerializer(settings);
            var item = CreateAsyncTestItem(3);

            string expected = serializer.Serialize(item);

            using var stream = new SlowAsyncStream();
            await serializer.SerializeAsync(stream, item);

            Assert.Equal(expected, ReadStreamString(stream));
            Assert.Equal(0, stream.SyncWriteCount);
            Assert.True(stream.AsyncWriteCount > 2);
            Assert.Equal(1, stream.MaxConcurrentWrites);
        }

        [Fact]
        public async Task SerializeAsync_LargeStringWithSmallBuffer_MatchesSync()
        {
            var settings = new JsonSerializer.Settings { writeBufferChunkSize = 32 };
            var serializer = new JsonSerializer(settings);
            var payload = new[] { new string('x', 1000), "short", new string('y', 500) };

            string expected = serializer.Serialize(payload);

            using var stream = new SlowAsyncStream();
            await serializer.SerializeAsync(stream, payload);

            Assert.Equal(expected, ReadStreamString(stream));
            Assert.Equal(0, stream.SyncWriteCount);
        }

        [Fact]
        public async Task SerializeAsync_FailingStream_ThrowsAndSerializerIsReusable()
        {
            var settings = new JsonSerializer.Settings { writeBufferChunkSize = 256 };
            var serializer = new JsonSerializer(settings);
            var item = CreateAsyncTestItem(3);

            using (var failing = new SlowAsyncStream { FailAfterAsyncWrites = 1 })
            {
                await Assert.ThrowsAnyAsync<Exception>(() => serializer.SerializeAsync(failing, item));
            }

            string expected = serializer.Serialize(item);
            using var stream = new SlowAsyncStream();
            await serializer.SerializeAsync(stream, item);
            Assert.Equal(expected, ReadStreamString(stream));

            using var syncStream = new MemoryStream();
            serializer.Serialize(syncStream, item);
            Assert.Equal(expected, ReadStreamString(syncStream));
        }
    }
}