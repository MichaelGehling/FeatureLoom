using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonDeserializerAsyncTests
    {
        public class Record
        {
            public int id;
            public string name;
            public List<int> values;
        }

        /// <summary>Delivers data in small chunks with an async delay, like a network stream.</summary>
        private sealed class SlowChunkStream : Stream
        {
            private readonly byte[] data;
            private readonly int chunkSize;
            private readonly int delayMs;
            private int pos;
            public int AsyncReads;

            public SlowChunkStream(byte[] data, int chunkSize, int delayMs)
            {
                this.data = data;
                this.chunkSize = chunkSize;
                this.delayMs = delayMs;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int n = Math.Min(Math.Min(count, chunkSize), data.Length - pos);
                Array.Copy(data, pos, buffer, offset, n);
                pos += n;
                return n;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref AsyncReads);
                if (delayMs > 0) await Task.Delay(delayMs, cancellationToken);
                return Read(buffer, offset, count);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static byte[] CreateJsonLines(int count, int valuesPerRecord)
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings { formatting = JsonSerializer.JsonFormatting.JsonLines });
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                var values = new List<int>();
                for (int v = 0; v < valuesPerRecord; v++) values.Add(v);
                sb.Append(serializer.Serialize(new Record { id = i, name = "n" + i, values = values }));
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static async Task<List<Record>> ReadAllAsync(JsonDeserializer deserializer)
        {
            var result = new List<Record>();
            while (await deserializer.IsAnyDataLeftAsync())
            {
                var (success, record) = await deserializer.TryDeserializeAsync<Record>();
                Assert.True(success);
                result.Add(record);
            }
            return result;
        }

        [Fact]
        public async Task TryDeserializeAsync_ReadsAllValuesFromChunkedStream()
        {
            var stream = new SlowChunkStream(CreateJsonLines(200, 20), 37, 0);
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings { initialBufferSize = 1024 });
            deserializer.SetDataSource(stream);

            var records = await ReadAllAsync(deserializer);

            Assert.Equal(200, records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                Assert.Equal(i, records[i].id);
                Assert.Equal(20, records[i].values.Count);
            }
            Assert.True(stream.AsyncReads > 0);
        }

        [Fact]
        public async Task TryDeserializeAsync_SlowStream_LargeValueGrowsBuffer()
        {
            var stream = new SlowChunkStream(CreateJsonLines(3, 5000), 4096, 1);
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings { initialBufferSize = 1024 });
            deserializer.SetDataSource(stream);

            var records = await ReadAllAsync(deserializer);

            Assert.Equal(3, records.Count);
            Assert.All(records, r => Assert.Equal(5000, r.values.Count));
        }

        // Covers BufferExceededException with a pending read-ahead in all variants: buffer growth
        // (value starts early in the buffer) and compaction without growth (value starts late),
        // small/large chunks, with/without delay and mixed record sizes. Content is verified exactly.
        [Theory]
        [InlineData(256, 7, 0)]
        [InlineData(256, 2000, 1)]
        [InlineData(1024, 4096, 0)]
        [InlineData(1024, 333, 0)]
        [InlineData(1024, 3000, 1)]
        [InlineData(4096, 50000, 0)]
        public async Task TryDeserializeAsync_BufferExceeded_KeepsAllData(int bufferSize, int chunkSize, int delayMs)
        {
            int[] sizes = { 3, 800, 1, 50, 3000, 2, 2, 1500, 10, 4000, 5 };
            var serializer = new JsonSerializer(new JsonSerializer.Settings { formatting = JsonSerializer.JsonFormatting.JsonLines });
            var sb = new StringBuilder();
            for (int i = 0; i < sizes.Length; i++)
            {
                var values = new List<int>();
                for (int v = 0; v < sizes[i]; v++) values.Add(i * 100000 + v);
                sb.Append(serializer.Serialize(new Record { id = i, name = new string('x', i * 37), values = values }));
            }
            var stream = new SlowChunkStream(Encoding.UTF8.GetBytes(sb.ToString()), chunkSize, delayMs);
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings { initialBufferSize = bufferSize });
            deserializer.SetDataSource(stream);

            var records = await ReadAllAsync(deserializer);

            Assert.Equal(sizes.Length, records.Count);
            for (int i = 0; i < sizes.Length; i++)
            {
                Assert.Equal(i, records[i].id);
                Assert.Equal(new string('x', i * 37), records[i].name);
                Assert.Equal(sizes[i], records[i].values.Count);
                for (int v = 0; v < sizes[i]; v++) Assert.Equal(i * 100000 + v, records[i].values[v]);
            }
        }

        [Fact]
        public async Task TryDeserializeAsync_EmptyStream_ReturnsFalse()
        {
            var deserializer = new JsonDeserializer();
            var (success, _) = await deserializer.TryDeserializeAsync<Record>(new SlowChunkStream(new byte[0], 10, 1));
            Assert.False(success);
            Assert.False(await deserializer.IsAnyDataLeftAsync());
        }

        [Fact]
        public async Task MixedSyncAndAsyncCalls_KeepOrder()
        {
            var stream = new SlowChunkStream(CreateJsonLines(50, 10), 64, 0);
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings { initialBufferSize = 512 });
            deserializer.SetDataSource(stream);

            var ids = new List<int>();
            for (int i = 0; i < 50; i++)
            {
                Record record;
                bool success;
                if (i % 2 == 0) (success, record) = await deserializer.TryDeserializeAsync<Record>();
                else success = deserializer.TryDeserialize(out record);
                Assert.True(success);
                ids.Add(record.id);
            }

            for (int i = 0; i < 50; i++) Assert.Equal(i, ids[i]);
            Assert.False(await deserializer.IsAnyDataLeftAsync());
        }

        [Fact]
        public async Task TryDeserializeAsync_JsonLines_SkipsBrokenLines()
        {
            var data = Encoding.UTF8.GetBytes("{\"id\":1}\n{\"id\":2,\n{\"id\":3}\n{broken}\n{\"id\":4}\n");
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
            {
                inputFormat = JsonDeserializer.Settings.InputFormat.JsonLines,
                logCatchedExceptions = false
            });
            deserializer.SetDataSource(new SlowChunkStream(data, 5, 1));

            var ids = new List<int>();
            int failures = 0;
            while (await deserializer.IsAnyDataLeftAsync())
            {
                var (success, record) = await deserializer.TryDeserializeAsync<Record>();
                if (success) ids.Add(record.id);
                else Assert.True(++failures < 100);
            }

            Assert.Equal(new[] { 1, 3, 4 }, ids);
            Assert.Equal(2, failures);
        }
    }
}
