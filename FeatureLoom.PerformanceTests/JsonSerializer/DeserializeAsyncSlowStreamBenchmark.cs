using BenchmarkDotNet.Attributes;
using FeatureLoom.Serialization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Measures continuous deserialization from a slow stream (sync vs. async read-ahead,
/// plain JSON vs. JSON Lines with line feed pre-scan).
/// Async is not expected to beat sync in elapsed time; its benefit is not blocking threads while waiting.
/// Read-ahead was verified by disabling it: with real async latency (50us) it saves ~2-5%,
/// with fast streams it shows no measurable cost and no additional allocations.
/// </summary>
[MemoryDiagnoser]
public class DeserializeAsyncSlowStreamBenchmark
{
    public class Record
    {
        public int id;
        public string name;
        public List<int> values;
    }

    /// <summary>
    /// Simulates network I/O: every read (sync and async) waits a fixed latency and returns at most
    /// chunkSize bytes. Task.Delay is unusable (~15ms timer resolution on Windows), so AppTime's precise
    /// waits are used.
    /// </summary>
    private sealed class SimulatedSlowStream : Stream
    {
        private readonly byte[] data;
        private readonly int chunkSize;
        private readonly TimeSpan latency;
        private readonly bool realAsync;
        private int position;

        public SimulatedSlowStream(byte[] data, int chunkSize, int latencyMicroseconds, bool realAsync)
        {
            this.data = data;
            this.chunkSize = chunkSize;
            this.realAsync = realAsync;
            latency = TimeSpan.FromTicks(latencyMicroseconds * 10L);
        }

        private int CopyChunk(byte[] buffer, int offset, int count)
        {
            int n = Math.Min(Math.Min(count, chunkSize), data.Length - position);
            if (n <= 0) return 0;
            Buffer.BlockCopy(data, position, buffer, offset, n);
            position += n;
            return n;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (latency > TimeSpan.Zero) AppTime.WaitPrecisely(latency);            
            return CopyChunk(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!realAsync)
            {
                // Completes synchronously: measures pure async overhead without any overlap.
                if (latency > TimeSpan.Zero) AppTime.WaitPrecisely(latency);
            }
            else if (latency > TimeSpan.Zero)
            {
                // AppTime.WaitPreciselyAsync completes synchronously for waits below ~0.1ms (Thread.Sleep(0) loop),
                // so the latency is simulated on another thread, like I/O that progresses in parallel.
                await Task.Run(() => AppTime.WaitPrecisely(latency), cancellationToken);
            }
            else await Task.Yield();
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

    // 0: pure CPU/async overhead, 50: I/O dominated. Intermediate values behaved in between.
    [Params(0, 50)]
    public int LatencyMicroseconds;

    [Params(4096)]
    public int ChunkSize;

    // Read-ahead only has room if the buffer is larger than one chunk.
    // (4096 was measured as well and showed no relevant difference.)
    [Params(65536)]
    public int BufferSize;

    // false: ReadAsync completes synchronously (no overlap possible), true: really asynchronous.
    [Params(false, true)]
    public bool RealAsyncRead;

    // JSON Lines (true) was measured as well and behaved like plain JSON.
    [Params(false)]
    public bool JsonLinesMode;

    // Mixed: 2000 mostly small records. Large: 20 records of ~220KB each. Total volume is similar (~4MB).
    [Params("Mixed", "Large")]
    public string Messages;

    byte[] data;
    JsonDeserializer deserializer;

    [GlobalSetup]
    public void Setup()
    {
        var serializer = new FeatureLoom.Serialization.JsonSerializer(new FeatureLoom.Serialization.JsonSerializer.Settings { formatting = FeatureLoom.Serialization.JsonSerializer.JsonFormatting.JsonLines });
        var sb = new StringBuilder();
        var rnd = new Random(42);
        bool large = Messages == "Large";
        int recordCount = large ? 20 : 2000;
        for (int i = 0; i < recordCount; i++)
        {
            // Mixed record sizes, some larger than the initial buffer to provoke buffer growth.
            int size = large ? 20000 : rnd.Next(10) == 0 ? rnd.Next(500, 3000) : rnd.Next(1, 50);
            var values = new List<int>(size);
            for (int v = 0; v < size; v++) values.Add(rnd.Next());
            sb.Append(serializer.Serialize(new Record { id = i, name = "record" + i, values = values }));
        }
        data = Encoding.UTF8.GetBytes(sb.ToString());
        deserializer = new JsonDeserializer(new JsonDeserializer.Settings
        {
            initialBufferSize = BufferSize,
            inputFormat = JsonLinesMode ? JsonDeserializer.Settings.InputFormat.JsonLines : default
        });
    }

    private Stream CreateStream() => new SimulatedSlowStream(data, ChunkSize, LatencyMicroseconds, RealAsyncRead);

    [Benchmark(Baseline = true)]
    public int Sync()
    {
        deserializer.SetDataSource(CreateStream());
        int count = 0;
        while (deserializer.TryDeserialize(out Record _)) count++;
        return count;
    }

    [Benchmark]
    public async Task<int> Async()
    {
        deserializer.SetDataSource(CreateStream());
        int count = 0;
        while (true)
        {
            var (success, _) = await deserializer.TryDeserializeAsync<Record>();
            if (!success) break;
            count++;
        }
        return count;
    }

    [Benchmark]
    public async Task<int> AsyncPrepareThenSync()
    {
        deserializer.SetDataSource(CreateStream());
        int count = 0;
        while (await deserializer.IsAnyDataLeftAsync(true))
        {
            if (!deserializer.TryDeserialize(out Record _)) break;
            count++;
        }
        return count;
    }

    [Benchmark]
    public async Task<int> AsyncValueTask()
    {
        deserializer.SetDataSource(CreateStream());
        int count = 0;
        while (true)
        {
            var (success, _) = await deserializer.TryDeserializeValueAsync<Record>();
            if (!success) break;
            count++;
        }
        return count;
    }
}
