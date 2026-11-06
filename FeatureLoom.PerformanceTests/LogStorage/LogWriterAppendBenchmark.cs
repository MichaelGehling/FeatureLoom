using BenchmarkDotNet.Attributes;
using FeatureLoom.Collections;
using FeatureLoom.LogStorage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.LogStorage;

/// <summary>Append throughput of LogWriter: single sync appends, AppendMany batches and async group commit.</summary>
[MemoryDiagnoser]
[InvocationCount(128)]
public class LogWriterAppendBenchmark
{
    public class Msg
    {
        public DateTime Time { get; set; }
        public int Level { get; set; }
        public string User { get; set; }
    }

    private const int Count = 1000;
    private string directory;
    private LogWriter writer;
    private LogWriterSink<Msg> sink;
    private Msg[] messages;

    // CPU-only replica of LogWriter.AppendLocked (no file IO) to separate CPU cost from IO cost.
    private LogPayloadTypeRegistry cpuRegistry;
    private LogEntryMetadata cpuMetadata;
    private ulong[] cpuFieldValues;
    private byte[] cpuIndexBuffer;
    private readonly byte[] cpuRecordBuffer = new byte[4096];
    private readonly MemoryStream cpuPayloadStream = new MemoryStream();
    private LogIndexFileHeader cpuHeader;

    [Params(LogHeaderMode.Minimal, LogHeaderMode.Full)]
    public LogHeaderMode HeaderMode { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var t0 = DateTime.UtcNow;
        messages = Enumerable.Range(0, Count).Select(i => new Msg { Time = t0, Level = i % 5, User = "user" + (i % 10) }).ToArray();
    }

    // A fresh log per iteration keeps the written data bounded (InvocationCount * Count entries, ~10 MB).
    // Appending to a single log for the whole run lets it grow to many GB and saturates the disk.
    [IterationSetup]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "FeatureLoomLogWriterBenchmark", Guid.NewGuid().ToString("N"));
        var schema = new LogIndexSchema(
            flagNames: new[] { "error" },
            fields: HeaderMode == LogHeaderMode.Full
                ? new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1), new LogFieldDefinition("user", LogFieldKind.Hash, 4) }
                : new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1) },
            typeIdSize: 1,
            headerMode: HeaderMode);
        var registry = new LogPayloadTypeRegistry(schema, LogPayloadTypeMap.Load(directory));
        var type = new LogPayloadType<Msg>(name: "msg").Timestamp(m => m.Time).Field("level", m => (ulong)m.Level).Flag("error", m => m.Level > 3);
        if (HeaderMode == LogHeaderMode.Full) type.Field("user", m => m.User);
        registry.Register(type);
        writer = new LogWriter(new LogStorageOptions { Directory = directory, Schema = schema }, registry);
        sink = new LogWriterSink<Msg>(writer);

        cpuRegistry = registry;
        cpuMetadata = new LogEntryMetadata(schema);
        cpuFieldValues = new ulong[schema.Fields.Count];
        cpuIndexBuffer = new byte[schema.RecordSize];
        cpuHeader = new LogIndexFileHeader(schema, 1, messages[0].Time, false);
    }

    [IterationCleanup]
    public void Cleanup()
    {
        writer.Dispose();
        try { Directory.Delete(directory, true); } catch { }
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void Append()
    {
        for (int i = 0; i < messages.Length; i++) writer.Append(messages[i]);
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public void AppendMany() => writer.AppendMany(messages);

    [Benchmark(OperationsPerInvoke = Count)]
    public Task AppendManyAsync() => writer.AppendManyAsync(messages);

    [Benchmark(OperationsPerInvoke = Count)]
    public async Task AppendAsync()
    {
        var tasks = new Task<LogEntryId>[messages.Length];
        for (int i = 0; i < messages.Length; i++) tasks[i] = writer.AppendAsync(messages[i]);
        await Task.WhenAll(tasks);
    }

    /// <summary>Synchronous MessageFlow posts (fire-and-forget group commit), then waits until all are written (same durability as the other async benchmarks).</summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public Task SinkPost()
    {
        int last = messages.Length - 1;
        for (int i = 0; i < last; i++) sink.Post(messages[i]);
        // The queue writes in order, so awaiting the last post waits for all (without the forced disk flush of FlushAsync).
        return sink.PostAsync(messages[last]);
    }

    /// <summary>Same per-entry CPU work as LogWriter.AppendLocked (extract, encode, hash, index, framing) without any file IO.</summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public int CpuOnly()
    {
        var schema = cpuRegistry.Schema;
        int total = 0;
        for (int n = 0; n < messages.Length; n++)
        {
            var payload = messages[n];
            var payloadType = cpuRegistry.GetOrRegister<Msg>();
            DateTime now = DateTime.UtcNow;
            cpuMetadata.Clear();
            payloadType.Extract(payload, now, cpuMetadata);
            cpuPayloadStream.SetLength(0);
            payloadType.Codec.Encode(payload, cpuPayloadStream);
            var payloadSegment = new ByteSegment(cpuPayloadStream.GetBuffer(), 0, (int)cpuPayloadStream.Length);
            for (int i = 0; i < cpuFieldValues.Length; i++)
            {
                var field = schema.Fields[i];
                cpuFieldValues[i] = field.Kind == LogFieldKind.Hash ? LogFieldHash.Compute(cpuMetadata.GetSource(i), field.MaxValue) : cpuMetadata.GetValue(i);
            }
            DateTime timestamp = cpuMetadata.Timestamp.ToUniversalTime();
            LogIndexRecordCodec.Write(schema, cpuIndexBuffer, 0, cpuHeader.ToTimeOffset(timestamp), (ulong)total, cpuMetadata.Flags, cpuMetadata.TypeId, cpuFieldValues);
            total += LogBinaryRecordFraming.Write(schema, cpuRecordBuffer, 0, cpuMetadata, payloadSegment);
        }
        return total;
    }

    /// <summary>CpuOnly part: payload JSON encoding only.</summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public long CpuEncode()
    {
        var codec = cpuRegistry.GetOrRegister<Msg>().Codec;
        long total = 0;
        for (int n = 0; n < messages.Length; n++)
        {
            cpuPayloadStream.SetLength(0);
            codec.Encode(messages[n], cpuPayloadStream);
            total += cpuPayloadStream.Length;
        }
        return total;
    }

    /// <summary>CpuOnly part: metadata extraction (timestamp, fields, flags) only.</summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public ulong CpuExtract()
    {
        var payloadType = cpuRegistry.GetOrRegister<Msg>();
        ulong total = 0;
        for (int n = 0; n < messages.Length; n++)
        {
            cpuMetadata.Clear();
            payloadType.Extract(messages[n], DateTime.UtcNow, cpuMetadata);
            total += cpuMetadata.Flags;
        }
        return total;
    }

    /// <summary>CpuOnly part: hashes, index record and record framing (incl. CRC) for a pre-encoded payload.</summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public int CpuFraming()
    {
        var schema = cpuRegistry.Schema;
        var payloadType = cpuRegistry.GetOrRegister<Msg>();
        cpuMetadata.Clear();
        payloadType.Extract(messages[1], DateTime.UtcNow, cpuMetadata);
        cpuPayloadStream.SetLength(0);
        payloadType.Codec.Encode(messages[1], cpuPayloadStream);
        var payloadSegment = new ByteSegment(cpuPayloadStream.GetBuffer(), 0, (int)cpuPayloadStream.Length);
        int total = 0;
        for (int n = 0; n < messages.Length; n++)
        {
            for (int i = 0; i < cpuFieldValues.Length; i++)
            {
                var field = schema.Fields[i];
                cpuFieldValues[i] = field.Kind == LogFieldKind.Hash ? LogFieldHash.Compute(cpuMetadata.GetSource(i), field.MaxValue) : cpuMetadata.GetValue(i);
            }
            DateTime timestamp = cpuMetadata.Timestamp.ToUniversalTime();
            LogIndexRecordCodec.Write(schema, cpuIndexBuffer, 0, cpuHeader.ToTimeOffset(timestamp), (ulong)total, cpuMetadata.Flags, cpuMetadata.TypeId, cpuFieldValues);
            total += LogBinaryRecordFraming.Write(schema, cpuRecordBuffer, 0, cpuMetadata, payloadSegment);
        }
        return total;
    }

    /// <summary>CpuOnly part: payload type lookup and DateTime.UtcNow only.</summary>
    [Benchmark(OperationsPerInvoke = Count)]
    public long CpuLookupAndClock()
    {
        long total = 0;
        for (int n = 0; n < messages.Length; n++)
        {
            total += cpuRegistry.GetOrRegister<Msg>().GetHashCode();
            total += DateTime.UtcNow.Ticks;
        }
        return total;
    }
}
