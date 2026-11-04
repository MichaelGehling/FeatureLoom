using System;
using System.IO;
using System.Text;
using FeatureLoom.LogStorage;
using Xunit;

namespace FeatureLoom.Tests.LogStorage;

public class LogStorageFormatTests
{
    private static LogIndexSchema CreateSchema(LogHeaderMode mode = LogHeaderMode.Full) => new LogIndexSchema(
        flagNames: new[] { "error", "audit" },
        fields: mode == LogHeaderMode.Full
            ? new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1), new LogFieldDefinition("path", LogFieldKind.Handle, 2), new LogFieldDefinition("user", LogFieldKind.Hash, 4) }
            : new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1) },
        headerMode: mode);

    [Fact]
    public void Metadata_ClearResetsValues()
    {
        var metadata = new LogEntryMetadata(CreateSchema()) { Flags = 8, Timestamp = DateTime.UtcNow, TypeId = 3 };
        metadata.SetValue(0, 3);
        metadata.SetSource(1, "a");
        metadata.Clear();
        Assert.Equal(default, metadata.Timestamp);
        Assert.Equal(0UL, metadata.Flags);
        Assert.Equal(0, metadata.TypeId);
        Assert.Equal(0UL, metadata.GetValue(0));
        Assert.Null(metadata.GetSource(1));
    }

    [Fact]
    public void IndexRecord_RoundTrips()
    {
        var schema = CreateSchema();
        var buffer = new byte[schema.RecordSize * 2];
        LogIndexRecordCodec.Write(schema, buffer, schema.RecordSize, 1234, 999, 0b101, 0, new ulong[] { 7, 65535, 0xDEADBEEF });

        int o = schema.RecordSize;
        Assert.Equal(1234u, LogIndexRecordCodec.ReadTimeOffset(buffer, o));
        Assert.Equal(999UL, LogIndexRecordCodec.ReadPosition(schema, buffer, o));
        Assert.Equal(0b101UL, LogIndexRecordCodec.ReadFlags(schema, buffer, o));
        Assert.Equal(7UL, LogIndexRecordCodec.ReadField(schema, buffer, o, 0));
        Assert.Equal(65535UL, LogIndexRecordCodec.ReadField(schema, buffer, o, 1));
        Assert.Equal(0xDEADBEEFUL, LogIndexRecordCodec.ReadField(schema, buffer, o, 2));

        LogIndexRecordCodec.WriteFlags(schema, buffer, o, LogReservedFlags.Deleted);
        Assert.Equal(LogReservedFlags.Deleted, LogIndexRecordCodec.ReadFlags(schema, buffer, o));
        Assert.Equal(7UL, LogIndexRecordCodec.ReadField(schema, buffer, o, 0));
    }

    [Fact]
    public void IndexRecord_RejectsValueTooLargeForField()
    {
        var schema = CreateSchema();
        Assert.Throws<ArgumentException>(() => LogIndexRecordCodec.Write(schema, new byte[schema.RecordSize], 0, 0, 0, 0, 0, new ulong[] { 256, 0, 0 }));
    }

    [Fact]
    public void IndexRecord_RoundTripsTypeId()
    {
        var schema = new LogIndexSchema(fields: new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1) }, typeIdSize: 2);
        var buffer = new byte[schema.RecordSize];
        LogIndexRecordCodec.Write(schema, buffer, 0, 1, 2, 0, 513, new ulong[] { 9 });
        Assert.Equal(513, LogIndexRecordCodec.ReadTypeId(schema, buffer, 0));
        Assert.Equal(9UL, LogIndexRecordCodec.ReadField(schema, buffer, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LogIndexRecordCodec.Write(CreateSchema(), new byte[CreateSchema().RecordSize], 0, 0, 0, 0, 1, new ulong[3]));
    }

    [Fact]
    public void IndexRecord_RejectsPositionAbove4GiB()
    {
        var schema = CreateSchema();
        Assert.Throws<ArgumentOutOfRangeException>(() => LogIndexRecordCodec.Write(schema, new byte[schema.RecordSize], 0, 0, (ulong)uint.MaxValue + 1, 0, 0, new ulong[3]));
    }

    [Fact]
    public void IndexHeader_RoundTripsViaStream()
    {
        var schema = CreateSchema();
        var baseTime = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        var bytes = new LogIndexFileHeader(schema, 42, baseTime, secureDelete: true).ToBytes();

        var stream = new MemoryStream();
        stream.Write(bytes, 0, bytes.Length);
        stream.WriteByte(0xFF); // index records follow the header
        stream.Position = 0;
        var read = LogIndexFileHeader.Read(stream);

        Assert.Equal(bytes.Length, stream.Position);
        Assert.Equal(42u, read.SegmentSequence);
        Assert.Equal(baseTime, read.BaseTime);
        Assert.True(read.SecureDelete);
        Assert.Equal(schema.RecordSize, read.Schema.RecordSize);
        Assert.Equal(schema.FlagNames, read.Schema.FlagNames);
        Assert.Equal(schema.TimeResolution, read.Schema.TimeResolution);
        Assert.Equal(LogFieldKind.Hash, read.Schema.Fields[2].Kind);
        Assert.Equal("path", read.Schema.Fields[1].Name);
    }

    [Fact]
    public void IndexHeader_DetectsCorruption()
    {
        var bytes = new LogIndexFileHeader(CreateSchema(), 1, DateTime.UtcNow, false).ToBytes();
        bytes[20] ^= 0x01;
        Assert.Throws<InvalidDataException>(() => LogIndexFileHeader.Parse(bytes));
    }

    [Fact]
    public void IndexHeader_DetectsTruncation()
    {
        var bytes = new LogIndexFileHeader(CreateSchema(), 1, DateTime.UtcNow, false).ToBytes();
        Assert.Throws<InvalidDataException>(() => LogIndexFileHeader.Read(new MemoryStream(bytes, 0, bytes.Length - 1)));
    }

	[Fact]
	public void IndexHeader_RoundTripsLogId()
	{
		var logId = Guid.NewGuid();
		var read = LogIndexFileHeader.Parse(new LogIndexFileHeader(CreateSchema(), 1, DateTime.UtcNow, false, logId).ToBytes());
		Assert.Equal(logId, read.LogId);
	}

	[Fact]
	public void SchemaFile_RoundTrips()
	{
		var schema = new LogIndexSchema(
			flagNames: new[] { "error" },
			fields: new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1), new LogFieldDefinition("user", LogFieldKind.Hash, 4) },
			typeIdSize: 2);
		var logId = Guid.NewGuid();
		var read = LogSchemaFile.Parse(LogSchemaFile.ToBytes(schema, logId), out var readId);

		Assert.Equal(logId, readId);
		Assert.Equal(schema.RecordSize, read.RecordSize);
		Assert.Equal(schema.FlagNames, read.FlagNames);
		Assert.Equal(2, read.TypeIdSize);
		Assert.Equal("user", read.Fields[1].Name);
		Assert.Equal(LogFieldKind.Hash, read.Fields[1].Kind);
	}

	[Fact]
	public void SchemaFile_DetectsCorruptionAndTruncation()
	{
		var bytes = LogSchemaFile.ToBytes(CreateSchema(), Guid.NewGuid());
		var corrupt = (byte[])bytes.Clone();
		corrupt[corrupt.Length / 2] ^= 0x01;
		Assert.Throws<InvalidDataException>(() => LogSchemaFile.Parse(corrupt, out _));
		var truncated = new byte[bytes.Length - 1];
		Array.Copy(bytes, truncated, truncated.Length);
		Assert.Throws<InvalidDataException>(() => LogSchemaFile.Parse(truncated, out _));
	}

	[Fact]
	public void SchemaFile_WritesLoadsAndReplaces()
	{
		string dir = Path.Combine(Path.GetTempPath(), "FeatureLoomSchemaFile_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			var logId = Guid.NewGuid();
			Assert.False(LogSchemaFile.TryLoad(dir, out _, out _));
			LogSchemaFile.Write(dir, CreateSchema(), logId);
			var grown = new LogIndexSchema(flagNames: new[] { "error", "audit", "new" }, fields: new[] { new LogFieldDefinition("level", LogFieldKind.Raw, 1) });
			LogSchemaFile.Write(dir, grown, logId);

			Assert.True(LogSchemaFile.TryLoad(dir, out var loaded, out var loadedId));
			Assert.Equal(logId, loadedId);
			Assert.Equal(grown.FlagNames, loaded.FlagNames);
			Assert.False(File.Exists(Path.Combine(dir, LogSchemaFile.FileName + ".tmp")));
		}
		finally
		{
			Directory.Delete(dir, true);
		}
	}

    [Fact]
    public void IndexHeader_ConvertsTimeOffsets()
    {
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var header = new LogIndexFileHeader(CreateSchema(), 1, baseTime, false);
        var t = baseTime.AddTicks(12500); // 1.25 ms; AddMilliseconds rounds to whole ms on .NET Framework
        uint offset = header.ToTimeOffset(t);
        Assert.Equal(12u, offset);
        Assert.Equal(baseTime.AddTicks(12000), header.FromTimeOffset(offset));
        Assert.Throws<ArgumentOutOfRangeException>(() => header.ToTimeOffset(baseTime.AddTicks(-1)));
    }

    [Theory]
    [InlineData(LogHeaderMode.Full)]
    [InlineData(LogHeaderMode.Minimal)]
    [InlineData(LogHeaderMode.None)]
    public void Framing_RoundTrips(LogHeaderMode mode)
    {
        var schema = CreateSchema(mode);
        var time = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var info = new LogEntryMetadata(schema) { Flags = 1UL << LogReservedFlags.Count, Timestamp = time, TypeId = 7 };
        info.SetValue(0, 5);
        if (mode == LogHeaderMode.Full) { info.SetSource(1, "/a/b"); info.SetSource(2, "Jörg"); }
        var payload = Encoding.UTF8.GetBytes("{\"msg\":\"hi\"}");

        var buffer = new byte[256];
        int written = LogBinaryRecordFraming.Write(schema, buffer, 3, info, payload);
        Assert.Equal(LogBinaryRecordFraming.GetHeaderSize(schema, info) + payload.Length, written);

        var record = new LogRecord { FieldValues = new ulong[3], FieldSources = new string[3] };
        int available = mode == LogHeaderMode.None ? payload.Length : buffer.Length - 3;
        Assert.Equal(LogRecordReadStatus.Ok, LogBinaryRecordFraming.TryRead(schema, buffer, 3, available, ref record));
        Assert.Equal(written, record.RecordLength);
        Assert.Equal(payload, ToArray(record.Payload));
        if (mode == LogHeaderMode.Full)
        {
            Assert.Equal(time, record.Timestamp);
            Assert.Equal(info.Flags, record.Flags);
            Assert.Equal(7, record.TypeId);
            Assert.Equal(5UL, record.FieldValues[0]);
            Assert.Equal("/a/b", record.FieldSources[1]);
            Assert.Equal("Jörg", record.FieldSources[2]);
        }
    }

    [Theory]
    [InlineData(LogHeaderMode.Full)]
    [InlineData(LogHeaderMode.Minimal)]
    public void Framing_DetectsTruncationAndCorruption(LogHeaderMode mode)
    {
        var schema = CreateSchema(mode);
        var info = new LogEntryMetadata(schema) { Timestamp = DateTime.UtcNow };
        var payload = new byte[] { 1, 2, 3, 4 };
        var buffer = new byte[128];
        int written = LogBinaryRecordFraming.Write(schema, buffer, 0, info, payload);

        var record = new LogRecord();
        Assert.Equal(LogRecordReadStatus.Truncated, LogBinaryRecordFraming.TryRead(schema, buffer, 0, written - 1, ref record));
        Assert.Equal(LogRecordReadStatus.Truncated, LogBinaryRecordFraming.TryRead(schema, buffer, 0, 3, ref record));

        buffer[written - 1] ^= 0xFF;
        Assert.Equal(LogRecordReadStatus.Corrupt, LogBinaryRecordFraming.TryRead(schema, buffer, 0, written, ref record));
    }

    private static byte[] ToArray(ArraySegment<byte> segment)
    {
        var result = new byte[segment.Count];
        Array.Copy(segment.Array, segment.Offset, result, 0, segment.Count);
        return result;
    }
}
