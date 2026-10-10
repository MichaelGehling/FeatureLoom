using FeatureLoom.Time;
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

            private int CopyChunk(byte[] buffer, int offset, int count)
            {
                int n = Math.Min(Math.Min(count, chunkSize), data.Length - pos);
                Array.Copy(data, pos, buffer, offset, n);
                pos += n;
                return n;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (delayMs > 0) AppTime.WaitPrecisely(TimeSpan.FromMilliseconds(delayMs));
                return CopyChunk(buffer, offset, count);
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref AsyncReads);
                //if (delayMs > 0) await AppTime.WaitPreciselyAsync(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
                if (delayMs > 0) AppTime.WaitPrecisely(TimeSpan.FromMilliseconds(delayMs));
                return CopyChunk(buffer, offset, count);
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
        public async Task TryDeserializeAsync_FullyBufferedValues_CompleteSynchronously()
        {
            var deserializer = new JsonDeserializer();
            deserializer.SetDataSource("{\"id\":1} {\"id\":2}");

            var task1 = deserializer.TryDeserializeAsync<Record>();
            Assert.True(task1.IsCompleted);
            var (s1, r1) = await task1;
            Assert.True(s1); Assert.Equal(1, r1.id);

            var (s2, r2) = await deserializer.TryDeserializeAsync<Record>();
            Assert.True(s2); Assert.Equal(2, r2.id);

            var (s3, _) = await deserializer.TryDeserializeAsync<Record>();
            Assert.False(s3);
        }

#if !CORE_NETSTANDARD2_0
        [Theory]
        [InlineData(false, 37)]
        [InlineData(true, 37)]
        [InlineData(false, 50000)]
        public async Task TryDeserializeValueAsync_ReadsAllValues(bool jsonLines, int chunkSize)
        {
            var stream = new SlowChunkStream(CreateJsonLines(200, 20), chunkSize, 0);
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
            {
                initialBufferSize = 1024,
                inputFormat = jsonLines ? JsonDeserializer.Settings.InputFormat.JsonLines : JsonDeserializer.Settings.InputFormat.Json
            });
            deserializer.SetDataSource(stream);

            int count = 0;
            while (true)
            {
                var (success, record) = await deserializer.TryDeserializeValueAsync<Record>();
                if (!success) break;
                Assert.Equal(count, record.id);
                Assert.Equal(20, record.values.Count);
                count++;
            }
            Assert.Equal(200, count);
        }

        [Fact]
        public async Task TryDeserializeValueAsync_FullyBufferedValue_CompletesSynchronously()
        {
            var deserializer = new JsonDeserializer();
            deserializer.SetDataSource("{\"id\":7}");

            var task = deserializer.TryDeserializeValueAsync<Record>();
            Assert.True(task.IsCompletedSuccessfully);
            var (success, record) = await task;
            Assert.True(success); Assert.Equal(7, record.id);
        }
#endif

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

        /// <summary>Async-only chunk stream: a synchronous Read fails the test, proving the parser never blocks on I/O.</summary>
        private sealed class AsyncOnlyChunkStream : Stream
        {
            private readonly byte[] data;
            private readonly int chunkSize;
            private int pos;

            public AsyncOnlyChunkStream(byte[] data, int chunkSize)
            {
                this.data = data;
                this.chunkSize = chunkSize;
            }

            public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous read");

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                await Task.Yield();
                int n = Math.Min(Math.Min(count, chunkSize), data.Length - pos);
                Array.Copy(data, pos, buffer, offset, n);
                pos += n;
                return n;
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

        private static readonly int[] scanRecordSizes = { 3, 800, 1, 50, 3000, 2, 2, 1500, 10, 4000, 5 };

        private static byte[] CreateScanRecords(bool jsonLines)
        {
            var serializer = new JsonSerializer(new JsonSerializer.Settings
            {
                formatting = jsonLines ? JsonSerializer.JsonFormatting.JsonLines : JsonSerializer.JsonFormatting.Compact
            });
            var sb = new StringBuilder();
            for (int i = 0; i < scanRecordSizes.Length; i++)
            {
                var values = new List<int>();
                for (int v = 0; v < scanRecordSizes[i]; v++) values.Add(i * 100000 + v);
                sb.Append(serializer.Serialize(new Record { id = i, name = new string('x', i * 37) + "}]\"\\", values = values }));
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static void AssertScanRecord(int i, Record record)
        {
            Assert.Equal(i, record.id);
            Assert.Equal(new string('x', i * 37) + "}]\"\\", record.name);
            Assert.Equal(scanRecordSizes[i], record.values.Count);
            for (int v = 0; v < scanRecordSizes[i]; v++) Assert.Equal(i * 100000 + v, record.values[v]);
        }

        [Theory]
        [InlineData(false, 256, 7)]
        [InlineData(false, 256, 2000)]
        [InlineData(false, 4096, 50000)]
        [InlineData(true, 256, 7)]
        [InlineData(true, 1024, 333)]
        [InlineData(true, 4096, 50000)]
        public async Task TryDeserializeAsync_LargeValues_NeverReadsSynchronously(bool jsonLines, int bufferSize, int chunkSize)
        {
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
            {
                initialBufferSize = bufferSize,
                inputFormat = jsonLines ? JsonDeserializer.Settings.InputFormat.JsonLines : JsonDeserializer.Settings.InputFormat.Json
            });
            deserializer.SetDataSource(new AsyncOnlyChunkStream(CreateScanRecords(jsonLines), chunkSize));
            for (int i = 0; i < scanRecordSizes.Length; i++)
            {
                var (success, record) = await deserializer.TryDeserializeAsync<Record>();
                Assert.True(success);
                AssertScanRecord(i, record);
            }
            Assert.False(await deserializer.IsAnyDataLeftAsync());
        }

        [Theory]
        [InlineData(false, 256, 7)]
        [InlineData(false, 4096, 50000)]
        [InlineData(true, 256, 7)]
        [InlineData(true, 4096, 50000)]
        public async Task IsAnyDataLeftAsync_EnsureFullValue_SyncDeserializeNeverReadsSynchronously(bool jsonLines, int bufferSize, int chunkSize)
        {
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
            {
                initialBufferSize = bufferSize,
                inputFormat = jsonLines ? JsonDeserializer.Settings.InputFormat.JsonLines : JsonDeserializer.Settings.InputFormat.Json
            });
            deserializer.SetDataSource(new AsyncOnlyChunkStream(CreateScanRecords(jsonLines), chunkSize));
            int i = 0;
            while (await deserializer.IsAnyDataLeftAsync(true))
            {
                Assert.True(deserializer.TryDeserialize(out Record record));
                AssertScanRecord(i++, record);
            }
            Assert.Equal(scanRecordSizes.Length, i);
        }

        [Theory]
        [InlineData(false, 256, 7)]
        [InlineData(false, 4096, 50000)]
        [InlineData(true, 256, 7)]
        [InlineData(true, 4096, 50000)]
        public void IsAnyDataLeft_EnsureFullValue_ReadsAllValues(bool jsonLines, int bufferSize, int chunkSize)
        {
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings
            {
                initialBufferSize = bufferSize,
                inputFormat = jsonLines ? JsonDeserializer.Settings.InputFormat.JsonLines : JsonDeserializer.Settings.InputFormat.Json
            });
            deserializer.SetDataSource(new SlowChunkStream(CreateScanRecords(jsonLines), chunkSize, 0));
            int i = 0;
            while (deserializer.IsAnyDataLeft(true))
            {
                Assert.True(deserializer.TryDeserialize(out Record record));
                AssertScanRecord(i++, record);
            }
            Assert.Equal(scanRecordSizes.Length, i);
        }

        // The value end scan must not be fooled by brackets/quotes inside strings, escapes or root primitives.
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(64)]
        public async Task TryDeserializeAsync_ValueEndScan_HandlesTrickyContent(int chunkSize)
        {
            var json = "{\"name\":\"a}]\\\"[{\\\\\",\"id\":1}{\"id\":2,\"values\":[1,[],3]}[ ] 42 \"str}\" true\t{\"id\":3} 7";
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings { initialBufferSize = 16 });
            deserializer.SetDataSource(new AsyncOnlyChunkStream(Encoding.UTF8.GetBytes(json), chunkSize));

            var (s1, r1) = await deserializer.TryDeserializeAsync<Record>();
            Assert.True(s1); Assert.Equal(1, r1.id); Assert.Equal("a}]\"[{\\", r1.name);
            var (s2, r2) = await deserializer.TryDeserializeAsync<Dictionary<string, object>>();
            Assert.True(s2); Assert.Equal(2, r2.Count);
            var (s3, r3) = await deserializer.TryDeserializeAsync<List<int>>();
            Assert.True(s3); Assert.Empty(r3);
            var (s4, r4) = await deserializer.TryDeserializeAsync<int>();
            Assert.True(s4); Assert.Equal(42, r4);
            var (s5, r5) = await deserializer.TryDeserializeAsync<string>();
            Assert.True(s5); Assert.Equal("str}", r5);
            var (s6, r6) = await deserializer.TryDeserializeAsync<bool>();
            Assert.True(s6); Assert.True(r6);
            var (s7, r7) = await deserializer.TryDeserializeAsync<Record>();
            Assert.True(s7); Assert.Equal(3, r7.id);
            var (s8, r8) = await deserializer.TryDeserializeAsync<int>();
            Assert.True(s8); Assert.Equal(7, r8);
            Assert.False(await deserializer.IsAnyDataLeftAsync(true));
        }

        [Theory]
        [InlineData("[1\"x\"]")]
        [InlineData("[{}{}]")]
        [InlineData("[[][]]")]
        [InlineData("{\"a\":1{}}")]
        // Note: object readers are lenient about a missing comma between fields (also with whitespace,
        // e.g. {"a":1 "b":2}), so {"a":[]"b":1} is accepted and intentionally not listed here.
        public void BackToBackValues_InsideContainers_AreRejected(string json)
        {
            var deserializer = new JsonDeserializer(new JsonDeserializer.Settings { logCatchedExceptions = false });
            Assert.False(deserializer.TryDeserialize(json, out object _));
        }

        [Fact]
        public void BackToBackRootValues_AreSupported()
        {
            var deserializer = new JsonDeserializer();
            deserializer.SetDataSource("{\"id\":1}[2]\"s\"{\"id\":3}");
            Assert.True(deserializer.TryDeserialize(out Record r1)); Assert.Equal(1, r1.id);
            Assert.True(deserializer.TryDeserialize(out List<int> l)); Assert.Equal(2, l[0]);
            Assert.True(deserializer.TryDeserialize(out string s)); Assert.Equal("s", s);
            Assert.True(deserializer.TryDeserialize(out Record r3)); Assert.Equal(3, r3.id);
            Assert.False(deserializer.IsAnyDataLeft());
        }

        [Fact]
        public async Task TryDeserializeAsync_FileStream_LargeObjects()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, CreateScanRecords(false));
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                {
                    var deserializer = new JsonDeserializer(new JsonDeserializer.Settings { initialBufferSize = 1024 });
                    deserializer.SetDataSource(fs);
                    int i = 0;
                    while (await deserializer.IsAnyDataLeftAsync(true))
                    {
                        Assert.True(deserializer.TryDeserialize(out Record record));
                        AssertScanRecord(i++, record);
                    }
                    Assert.Equal(scanRecordSizes.Length, i);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
