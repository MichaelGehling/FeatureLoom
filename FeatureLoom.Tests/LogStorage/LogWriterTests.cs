using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FeatureLoom.LogStorage;
using Xunit;

namespace FeatureLoom.Tests.LogStorage;

public class LogWriterTests : IDisposable
{
    public class Msg
    {
        public int Level { get; set; }
        public string User { get; set; }
    }

    private readonly string directory = Path.Combine(Path.GetTempPath(), "FeatureLoomLogWriterTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static LogIndexSchema CreateSchema() => new LogIndexSchema(
        flagNames: new[] { "error" },
        fields: new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1), new LogFieldDefinition("user", LogFieldKind.Hash, 4) },
        typeIdSize: 1);

    private LogWriter CreateWriter(LogIndexSchema schema = null, Action<LogStorageOptions> configure = null)
    {
        schema ??= CreateSchema();
        var registry = new LogPayloadTypeRegistry(schema, LogPayloadTypeMap.Load(directory));
        registry.Register(new LogPayloadType<Msg>(name: "msg")
            .Field("level", m => (ulong)m.Level)
            .Field("user", m => m.User)
            .Flag("error", m => m.Level > 3));
        var options = new LogStorageOptions { Directory = directory, Schema = schema };
        configure?.Invoke(options);
        return new LogWriter(options, registry);
    }

    private string[] IndexFiles() => Directory.GetFiles(directory, LogSegmentFiles.IndexSearchPattern);

    [Fact]
    public void SegmentFiles_NamesRoundTrip()
    {
        string name = LogSegmentFiles.GetBaseName(42, new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)) + LogSegmentFiles.IndexExtension;
        Assert.True(LogSegmentFiles.TryParseStartTime(name, out var start));
        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), start);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
        Assert.Equal("segment.0000000042.20240102030405.idx", name);
        Assert.True(LogSegmentFiles.TryParseSequence(name, out uint sequence));
        Assert.Equal(42u, sequence);
        Assert.False(LogSegmentFiles.TryParseSequence("other.0000000042.20240102030405.idx", out _));
        Assert.False(LogSegmentFiles.TryParseSequence("segment.x.20240102030405.idx", out _));
        Assert.False(LogSegmentFiles.TryParseSequence(null, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("äöü€")]
    [InlineData("😀x")]
    [InlineData("a\uD800b")]
    public void FieldHash_MatchesFnv1aOverUtf8(string value)
    {
        ulong expected = 14695981039346656037UL;
        foreach (byte b in Encoding.UTF8.GetBytes(value)) expected = (expected ^ b) * 1099511628211UL;
        Assert.Equal(expected, LogFieldHash.Compute(value, ulong.MaxValue));
        Assert.Equal(expected & 0xFFFF, LogFieldHash.Compute(value, 0xFFFF));
        Assert.Equal(0UL, LogFieldHash.Compute(null, ulong.MaxValue));
    }

    [Fact]
    public void Append_WritesSegmentIndexAndTypeMap()
    {
        using (var writer = CreateWriter())
        {
            var first = writer.Append(new Msg { Level = 1, User = "a" });
            var second = writer.Append(new Msg { Level = 5, User = "b" });
            Assert.Equal(first.SegmentSequence, second.SegmentSequence);
            Assert.Equal(0u, first.EntryIndex);
            Assert.Equal(1u, second.EntryIndex);
        }

        var indexFile = Assert.Single(IndexFiles());
        Assert.True(File.Exists(Path.ChangeExtension(indexFile, LogSegmentFiles.SegmentExtension)));
        Assert.True(File.Exists(Path.Combine(directory, LogPayloadTypeMap.FileName)));
        Assert.True(LogSchemaFile.TryLoad(directory, out var storedSchema, out _));
        Assert.True(LogIndexSchemaCodec.AreEqual(CreateSchema(), storedSchema));

        var schema = CreateSchema();
        var bytes = File.ReadAllBytes(indexFile);
        LogIndexFileHeader header;
        using (var stream = new MemoryStream(bytes)) header = LogIndexFileHeader.Read(stream);
        int headerSize = header.ToBytes().Length;
        Assert.Equal(headerSize + 2 * schema.RecordSize, bytes.Length);
        Assert.Equal(5UL, LogIndexRecordCodec.ReadField(schema, bytes, headerSize + schema.RecordSize, 0));
        Assert.Equal(LogFieldHash.Compute("b", schema.Fields[1].MaxValue), LogIndexRecordCodec.ReadField(schema, bytes, headerSize + schema.RecordSize, 1));
        Assert.True(schema.TryGetFlagMask("error", out ulong errorMask));
        Assert.NotEqual(0UL, LogIndexRecordCodec.ReadFlags(schema, bytes, headerSize + schema.RecordSize) & errorMask);
        Assert.Equal(0UL, LogIndexRecordCodec.ReadFlags(schema, bytes, headerSize) & errorMask);
    }

    [Fact]
    public void Reopen_ContinuesActiveSegment()
    {
        Guid logId;
        using (var writer = CreateWriter())
        {
            writer.Append(new Msg());
            logId = writer.LogId;
        }
        using (var writer = CreateWriter())
        {
            Assert.Equal(logId, writer.LogId);
            var id = writer.Append(new Msg());
            Assert.Equal(1u, id.EntryIndex);
        }
        Assert.Single(IndexFiles());
    }

    [Fact]
    public void Reopen_WithChangedSchema_StartsNewSegment()
    {
        uint firstSequence;
        using (var writer = CreateWriter())
        {
            firstSequence = writer.Append(new Msg()).SegmentSequence;
        }
        var changed = new LogIndexSchema(
            flagNames: new[] { "error", "audit" },
            fields: new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1), new LogFieldDefinition("user", LogFieldKind.Hash, 4) },
            typeIdSize: 1);
        using (var writer = CreateWriter(changed))
        {
            var id = writer.Append(new Msg());
            Assert.Equal(firstSequence + 1, id.SegmentSequence);
            Assert.Equal(0u, id.EntryIndex);
        }
        Assert.Equal(2, IndexFiles().Length);
        Assert.True(LogSchemaFile.TryLoad(directory, out var stored, out _));
        Assert.True(LogIndexSchemaCodec.AreEqual(changed, stored));
    }

    [Fact]
    public async Task Rollover_StartsNewSegment()
    {
        using (var writer = CreateWriter())
        {
            var first = writer.Append(new Msg());
            await writer.RolloverAsync();
            var second = writer.Append(new Msg());
            Assert.Equal(first.SegmentSequence + 1, second.SegmentSequence);
            Assert.Equal(0u, second.EntryIndex);
        }
        Assert.Equal(2, IndexFiles().Length);
    }

    [Fact]
    public void Append_RollsOverAtMaxSegmentSize()
    {
        using var writer = CreateWriter(configure: o => o.Rotation.MaxSegmentSize = 1);
        var first = writer.Append(new Msg());
        var second = writer.Append(new Msg());
        Assert.Equal(first.SegmentSequence + 1, second.SegmentSequence);
    }

    [Fact]
    public async Task TryDelete_SetsDeletedFlagOnce()
    {
        using var writer = CreateWriter();
        var old = writer.Append(new Msg());
        await writer.RolloverAsync();
        var current = writer.Append(new Msg());

        Assert.True(await writer.TryDeleteAsync(current));
        Assert.False(await writer.TryDeleteAsync(current));
        Assert.True(await writer.TryDeleteAsync(old));
        Assert.False(await writer.TryDeleteAsync(old));
        Assert.False(await writer.TryDeleteAsync(new LogEntryId(current.SegmentSequence, 5)));
        Assert.False(await writer.TryDeleteAsync(new LogEntryId(99, 0)));
    }

    [Fact]
    public void Reopen_TruncatesTornTail()
    {
        string indexFile;
        using (var writer = CreateWriter())
        {
            writer.Append(new Msg { Level = 1 });
            writer.Append(new Msg { Level = 2 });
        }
        indexFile = Assert.Single(IndexFiles());
        string segmentFile = Path.ChangeExtension(indexFile, LogSegmentFiles.SegmentExtension);
        long indexLength = new FileInfo(indexFile).Length;
        long segmentLength = new FileInfo(segmentFile).Length;

        // Torn index record and unindexed segment bytes.
        using (var stream = new FileStream(indexFile, FileMode.Append)) stream.Write(new byte[3], 0, 3);
        using (var stream = new FileStream(segmentFile, FileMode.Append)) stream.Write(new byte[7], 0, 7);

        using (var writer = CreateWriter())
        {
            Assert.Equal(new FileInfo(indexFile).Length, indexLength);
            Assert.Equal(new FileInfo(segmentFile).Length, segmentLength);
            Assert.Equal(2u, writer.Append(new Msg()).EntryIndex);
        }
    }

    [Fact]
    public void Constructor_RejectsHandleFields()
    {
        var schema = new LogIndexSchema(fields: new[] { new LogFieldDefinition("h", LogFieldKind.Handle, 2) });
        var options = new LogStorageOptions { Directory = directory, Schema = schema };
        Assert.Throws<NotSupportedException>(() => new LogWriter(options, new LogPayloadTypeRegistry(schema)));
    }

    [Fact]
    public void Append_AfterDispose_Throws()
    {
        var writer = CreateWriter();
        writer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => writer.Append(new Msg()));
    }
}
