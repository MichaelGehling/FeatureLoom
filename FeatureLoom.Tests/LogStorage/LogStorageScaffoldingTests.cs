using System;
using FeatureLoom.LogStorage;
using Xunit;

namespace FeatureLoom.Tests.LogStorage;

public class LogStorageScaffoldingTests
{
    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(1u, 2u)]
    [InlineData(uint.MaxValue, uint.MaxValue)]
    public void LogEntryId_RoundTripsSegmentAndIndex(uint segment, uint index)
    {
        var id = new LogEntryId(segment, index);
        Assert.Equal(segment, id.SegmentSequence);
        Assert.Equal(index, id.EntryIndex);
        Assert.Equal(id, new LogEntryId(id.Value));
    }

    [Fact]
    public void LogEntryId_OrdersBySegmentFirst()
    {
        Assert.True(new LogEntryId(1, uint.MaxValue) < new LogEntryId(2, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void LogFieldDefinition_RejectsInvalidSize(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogFieldDefinition("f", LogFieldKind.Raw, size));
    }

    [Fact]
    public void Schema_DefaultRecordSize()
    {
        var schema = new LogIndexSchema();
        Assert.Equal(1, schema.FlagSize);
        Assert.Equal(4 + 4 + 1, schema.RecordSize);
    }

    [Fact]
    public void Schema_ComputesLayout()
    {
        var schema = new LogIndexSchema(
            flagNames: new[] { "a", "b", "c", "d", "e", "f", "g" },
            fields: new[] { new LogFieldDefinition("x", LogFieldKind.Raw, 2), new LogFieldDefinition("y", LogFieldKind.Handle, 3) });
        Assert.Equal(2, schema.FlagSize);
        Assert.Equal(10, schema.GetFieldOffset(0));
        Assert.Equal(12, schema.GetFieldOffset(1));
        Assert.Equal(15, schema.RecordSize);
        Assert.True(schema.TryGetFlagMask("a", out var mask));
        Assert.Equal(1UL << LogReservedFlags.Count, mask);
        Assert.True(schema.TryGetFieldIndex("y", out var idx));
        Assert.Equal(1, idx);
    }

    [Fact]
    public void Schema_RejectsDuplicateFields()
    {
        Assert.Throws<ArgumentException>(() => new LogIndexSchema(fields: new[] { new LogFieldDefinition("x", LogFieldKind.Raw, 1), new LogFieldDefinition("x", LogFieldKind.Raw, 1) }));
    }

    [Fact]
    public void Schema_RejectsHashFieldWithoutFullHeader()
    {
        Assert.Throws<ArgumentException>(() => new LogIndexSchema(fields: new[] { new LogFieldDefinition("h", LogFieldKind.Hash, 4) }, headerMode: LogHeaderMode.Minimal));
    }

    [Fact]
    public void Schema_DefaultTimeRangeCoversSeveralDays()
    {
        Assert.True(new LogIndexSchema().MaxTimeRange > TimeSpan.FromDays(4));
    }
}
