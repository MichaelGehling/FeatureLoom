using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FeatureLoom.LogStorage;
using Xunit;

namespace FeatureLoom.Tests.LogStorage;

public class LogReaderTests : IDisposable
{
    public class Msg
    {
        public DateTime Time { get; set; }
        public int Level { get; set; }
        public string User { get; set; }
    }

    public class Other
    {
        public string Text { get; set; }
    }

    private readonly string directory = Path.Combine(Path.GetTempPath(), "FeatureLoomLogReaderTests", Guid.NewGuid().ToString("N"));
    private static readonly DateTime T0 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    // Hash fields require the full header; other modes are tested without the "user" field.
    private static LogIndexSchema CreateSchema(LogHeaderMode headerMode = LogHeaderMode.Full, bool withAudit = false) => new LogIndexSchema(
        flagNames: withAudit ? new[] { "error", "audit" } : new[] { "error" },
        fields: headerMode == LogHeaderMode.Full
            ? new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1), new LogFieldDefinition("user", LogFieldKind.Hash, 4) }
            : new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1) },
        typeIdSize: 1,
        headerMode: headerMode);

    private LogPayloadTypeRegistry CreateRegistry(LogIndexSchema schema)
    {
        var registry = new LogPayloadTypeRegistry(schema, LogPayloadTypeMap.Load(directory));
        if (!registry.TryGet<Msg>(out _))
        {
            var type = new LogPayloadType<Msg>(name: "msg")
                .Timestamp(m => m.Time)
                .Field("level", m => (ulong)m.Level)
                .Flag("error", m => m.Level > 3);
            if (schema.TryGetFieldIndex("user", out _)) type.Field("user", m => m.User);
            registry.Register(type);
        }
        return registry;
    }

    private LogWriter CreateWriter(LogIndexSchema schema) =>
        new LogWriter(new LogStorageOptions { Directory = directory, Schema = schema }, CreateRegistry(schema));

    private LogReader CreateReader(LogIndexSchema schema) => new LogReader(directory, CreateRegistry(schema));

    private void WriteSample(LogIndexSchema schema)
    {
        using var writer = CreateWriter(schema);
        writer.Append(new Msg { Time = T0, Level = 1, User = "alice" });
        writer.Append(new Msg { Time = T0.AddSeconds(1), Level = 5, User = "bob" });
        writer.Append(new Other { Text = "x" });
        writer.RolloverAsync().Wait();
        writer.Append(new Msg { Time = T0.AddSeconds(2), Level = 2, User = "alice" });
    }

    [Theory]
    [InlineData(LogHeaderMode.None)]
    [InlineData(LogHeaderMode.Minimal)]
    [InlineData(LogHeaderMode.Full)]
    public async Task Query_ReturnsPayloadsAcrossSegments(LogHeaderMode headerMode)
    {
        var schema = CreateSchema(headerMode);
        WriteSample(schema);
        using var reader = CreateReader(schema);

        var entries = await reader.QueryAsync<Msg>(new LogQuery());
        Assert.Equal(new[] { "alice", "bob", "alice" }, entries.Select(e => e.Payload.User));
        Assert.Equal(T0.AddSeconds(1), entries[1].Info.Timestamp);

        var others = await reader.QueryAsync<Other>(new LogQuery());
        Assert.Equal("x", Assert.Single(others).Payload.Text);
        Assert.Equal(4, await reader.CountAsync(new LogQuery()));
    }

    [Fact]
    public async Task Query_FiltersByFieldFlagTimeAndLimit()
    {
        var schema = CreateSchema();
        WriteSample(schema);
        using var reader = CreateReader(schema);

        var byUser = await reader.QueryAsync<Msg>(new LogQuery { FieldFilters = new[] { LogFieldFilter.SourceEquals(1, "alice") } });
        Assert.Equal(new[] { 1, 2 }, byUser.Select(e => e.Payload.Level));

        var byLevel = await reader.QueryAsync<Msg>(new LogQuery { FieldFilters = new[] { LogFieldFilter.ValueEquals(0, 5) } });
        Assert.Equal("bob", Assert.Single(byLevel).Payload.User);

        Assert.True(schema.TryGetFlagMask("error", out ulong error));
        Assert.Equal(1, await reader.CountAsync(new LogQuery { RequiredFlags = error }));

        var byTime = await reader.QueryAsync<Msg>(new LogQuery { From = T0.AddSeconds(1), To = T0.AddSeconds(2) });
        Assert.Equal("bob", Assert.Single(byTime).Payload.User);

        var newest = await reader.QueryAsync<Msg>(new LogQuery { Reverse = true, Limit = 2 });
        Assert.Equal(new[] { 2, 5 }, newest.Select(e => e.Payload.Level));
    }

    [Fact]
    public async Task Query_ExcludesDeletedByDefault()
    {
        var schema = CreateSchema();
        using (var writer = CreateWriter(schema))
        {
            var id = writer.Append(new Msg { Level = 1 });
            writer.Append(new Msg { Level = 2 });
            Assert.True(await writer.TryDeleteAsync(id));
        }
        using var reader = CreateReader(schema);
        Assert.Equal(2, Assert.Single(await reader.QueryAsync<Msg>(new LogQuery())).Payload.Level);
        Assert.Equal(2, await reader.CountAsync(new LogQuery { ExcludedFlags = 0 }));
    }

    [Fact]
    public async Task TryRead_FindsEntryById()
    {
        var schema = CreateSchema();
        LogEntryId id, otherId;
        using (var writer = CreateWriter(schema))
        {
            writer.Append(new Msg { Level = 1 });
            id = writer.Append(new Msg { Level = 7, User = "u" });
            otherId = writer.Append(new Other());
        }
        using var reader = CreateReader(schema);
        var (found, entry) = await reader.TryReadAsync<Msg>(id);
        Assert.True(found);
        Assert.Equal(7, entry.Payload.Level);
        Assert.Equal(id, entry.Info.Id);
        Assert.False((await reader.TryReadAsync<Msg>(otherId)).found);
        Assert.False((await reader.TryReadAsync<Msg>(new LogEntryId(id.SegmentSequence, 99))).found);
        Assert.False((await reader.TryReadAsync<Msg>(new LogEntryId(99, 0))).found);
    }

    [Fact]
    public async Task Query_WorksWhileWriterIsOpen()
    {
        var schema = CreateSchema();
        using var writer = CreateWriter(schema);
        writer.Append(new Msg { Level = 1 });
        using var reader = CreateReader(schema);
        Assert.Equal(1, await reader.CountAsync(new LogQuery()));
        writer.Append(new Msg { Level = 2 });
        Assert.Equal(new[] { 1, 2 }, (await reader.QueryAsync<Msg>(new LogQuery())).Select(e => e.Payload.Level));
    }

    [Fact]
    public async Task Query_MapsFlagsAcrossSchemaChanges()
    {
        WriteSample(CreateSchema());
        var newSchema = CreateSchema(withAudit: true);
        using (var writer = CreateWriter(newSchema)) writer.Append(new Msg { Level = 9 });

        using var reader = CreateReader(newSchema);
        Assert.True(newSchema.TryGetFlagMask("error", out ulong error));
        Assert.True(newSchema.TryGetFlagMask("audit", out ulong audit));
        Assert.Equal(new[] { 5, 9 }, (await reader.QueryAsync<Msg>(new LogQuery { RequiredFlags = error })).Select(e => e.Payload.Level));
        // "audit" is unknown to the old segments, so only the new segment can match.
        Assert.Equal(0, await reader.CountAsync(new LogQuery { RequiredFlags = audit }));
        Assert.Equal(5, await reader.CountAsync(new LogQuery { ExcludedFlags = audit }));
    }

    [Fact]
    public async Task Query_EmptyOrMissingLog_ReturnsNothing()
    {
        using var reader = CreateReader(CreateSchema());
        Assert.Empty(await reader.QueryAsync<Msg>(new LogQuery()));
        Assert.Equal(0, await reader.CountAsync(null));
    }

    [Fact]
    public async Task Query_DoesNotRegisterUnknownImplicitTypes()
    {
        var schema = CreateSchema();
        WriteSample(schema);
        var registry = CreateRegistry(schema);
        using var reader = new LogReader(directory, registry);
        Assert.Empty(await reader.QueryAsync<Version>(new LogQuery()));
        Assert.False(registry.TryGet<Version>(out _));
    }

    [Fact]
    public async Task MultiLogReader_MergesByTimestamp()
    {
        var schema = CreateSchema();
        string dirA = Path.Combine(directory, "a"), dirB = Path.Combine(directory, "b");
        LogPayloadTypeRegistry Registry(string dir) => new LogPayloadTypeRegistry(schema, LogPayloadTypeMap.Load(dir)).Register(new LogPayloadType<Msg>(name: "msg").Timestamp(m => m.Time).Field("level", m => (ulong)m.Level));
        using (var a = new LogWriter(new LogStorageOptions { Directory = dirA, Schema = schema }, Registry(dirA)))
        using (var b = new LogWriter(new LogStorageOptions { Directory = dirB, Schema = schema }, Registry(dirB)))
        {
            a.Append(new Msg { Time = T0, Level = 1 });
            b.Append(new Msg { Time = T0.AddSeconds(1), Level = 2 });
            a.Append(new Msg { Time = T0.AddSeconds(2), Level = 3 });
            b.Append(new Msg { Time = T0.AddSeconds(3), Level = 4 });
        }
        using var readerA = new LogReader(dirA, Registry(dirA));
        using var readerB = new LogReader(dirB, Registry(dirB));
        var multi = new MultiLogReader(new ILogReader[] { readerA, readerB });

        Assert.Equal(new[] { 1, 2, 3, 4 }, (await multi.QueryAsync<Msg>(new LogQuery())).Select(e => e.Payload.Level));
        Assert.Equal(new[] { 4, 3, 2 }, (await multi.QueryAsync<Msg>(new LogQuery { Reverse = true, Limit = 3 })).Select(e => e.Payload.Level));
    }

    [Fact]
    public async Task TryRead_SeesEntriesAppendedAfterHandleWasOpened()
    {
        var schema = CreateSchema();
        using var writer = CreateWriter(schema);
        var first = writer.Append(new Msg { Level = 1 });
        using var reader = CreateReader(schema);
        Assert.True((await reader.TryReadAsync<Msg>(first)).found);
        var second = writer.Append(new Msg { Level = 2 });
        var (found, entry) = await reader.TryReadAsync<Msg>(second);
        Assert.True(found);
        Assert.Equal(2, entry.Payload.Level);
        Assert.True(await writer.TryDeleteAsync(first));
        Assert.False((await reader.TryReadAsync<Msg>(first)).found);
    }

    [Fact]
    public async Task TryRead_AfterDispose_Throws()
    {
        var reader = CreateReader(CreateSchema());
        reader.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => reader.TryReadAsync<Msg>(new LogEntryId(1, 0)));
    }

    [Fact]
    public async Task TryRead_ConcurrentReadsOnSameSegment_ReturnCorrectEntries()
    {
        var schema = CreateSchema();
        using var writer = CreateWriter(schema);
        var ids = new LogEntryId[50];
        for (int i = 0; i < ids.Length; i++) ids[i] = writer.Append(new Msg { Level = i });
        using var reader = CreateReader(schema);
        var tasks = new Task[8];
        for (int t = 0; t < tasks.Length; t++)
        {
            int seed = t;
            tasks[t] = Task.Run(async () =>
            {
                for (int n = 0; n < 500; n++)
                {
                    int i = (n * 7 + seed) % ids.Length;
                    var (found, entry) = await reader.TryReadAsync<Msg>(ids[i]);
                    Assert.True(found);
                    Assert.Equal(i, entry.Payload.Level);
                }
            });
        }
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task TryRead_EvictsIdleHandlesAndReopensOnDemand()
    {
        var schema = CreateSchema();
        LogEntryId first, second;
        using (var writer = CreateWriter(schema))
        {
            first = writer.Append(new Msg { Level = 1 });
            await writer.RolloverAsync();
            second = writer.Append(new Msg { Level = 2 });
        }
        using var reader = CreateReader(schema);
        reader.HandleIdleTimeout = TimeSpan.FromMilliseconds(20);
        Assert.True((await reader.TryReadAsync<Msg>(first)).found);
        await Task.Delay(100);
        // Accessing another segment triggers eviction of the idle first handle.
        Assert.True((await reader.TryReadAsync<Msg>(second)).found);
        // The evicted segment is reopened transparently.
        var (found, entry) = await reader.TryReadAsync<Msg>(first);
        Assert.True(found);
        Assert.Equal(1, entry.Payload.Level);
    }

    [Fact]
    public async Task Query_LargeSegment_ChunkedScanAndTimeRangeSearch()
    {
        const int total = 5000; // spans several 64 KB index chunks
        var schema = CreateSchema();
        // No fsync per append: this test is about reading, not durability.
        using var writer = new LogWriter(new LogStorageOptions { Directory = directory, Schema = schema, Flush = new LogFlushPolicy { FlushToDisk = false } }, CreateRegistry(schema));
        for (int i = 0; i < total; i++) writer.Append(new Msg { Level = i % 10, Time = T0.AddSeconds(i) });
        using var reader = CreateReader(schema);

        Assert.Single(System.IO.Directory.GetFiles(directory, "*.idx"));
        Assert.Equal(total, await reader.CountAsync(new LogQuery()));
        var all = await reader.QueryAsync<Msg>(new LogQuery { Reverse = true });
        Assert.Equal(total, all.Count);
        for (int i = 0; i < total; i++) Assert.Equal(T0.AddSeconds(total - 1 - i), all[i].Info.Timestamp);

        var range = await reader.QueryAsync<Msg>(new LogQuery { From = T0.AddSeconds(1000), To = T0.AddSeconds(3500) });
        Assert.Equal(2500, range.Count);
        Assert.Equal(T0.AddSeconds(1000), range[0].Info.Timestamp);
        Assert.Equal(T0.AddSeconds(3499), range[range.Count - 1].Info.Timestamp);

        var reverseRange = await reader.QueryAsync<Msg>(new LogQuery { From = T0.AddSeconds(1000), To = T0.AddSeconds(3500), Reverse = true, Limit = 3 });
        Assert.Equal(new[] { T0.AddSeconds(3499), T0.AddSeconds(3498), T0.AddSeconds(3497) }, reverseRange.Select(e => e.Info.Timestamp));

        Assert.Equal(250, await reader.CountAsync(new LogQuery { From = T0.AddSeconds(1000), To = T0.AddSeconds(3500), FieldFilters = new[] { LogFieldFilter.ValueEquals(0, 3) } }));
        Assert.Equal(0, await reader.CountAsync(new LogQuery { From = T0.AddSeconds(total) }));
    }

    [Fact]
    public async Task Query_SeesSegmentsCreatedAfterReaderOpened()
    {
        var schema = CreateSchema();
        using var writer = CreateWriter(schema);
        writer.Append(new Msg { Level = 1 });
        using var reader = CreateReader(schema);
        Assert.Equal(1, await reader.CountAsync(new LogQuery()));
        await writer.RolloverAsync();
        writer.Append(new Msg { Level = 2 });
        Assert.Equal(new[] { 1, 2 }, (await reader.QueryAsync<Msg>(new LogQuery())).Select(e => e.Payload.Level));
    }

    [Fact]
    public async Task Append_HistoricTimestamps_DoNotTriggerAgeRollover()
    {
        var schema = CreateSchema();
        using (var writer = CreateWriter(schema)) // default MaxSegmentAge = 1 day
        {
            for (int i = 0; i < 5; i++) writer.Append(new Msg { Level = i, Time = T0.AddSeconds(i) });
        }
        // Reopening continues the segment: its age comes from the file name (creation time), not the historic BaseTime.
        using (var writer = CreateWriter(schema)) writer.Append(new Msg { Level = 5, Time = T0.AddSeconds(5) });
        Assert.Single(System.IO.Directory.GetFiles(directory, "*.idx"));
        using var reader = CreateReader(schema);
        Assert.Equal(T0, (await reader.QueryAsync<Msg>(new LogQuery())).First().Info.Timestamp);
    }
}
