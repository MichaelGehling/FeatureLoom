using System;
using System.Text;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class EnumHelperTests
{
    enum SequentialIntEnum { A = 0, B = 1, C = 2 }
    enum OffsetIntEnum { X = 10, Y = 11, Z = 12 }
    enum DuplicateEnum { A = 1, B = 2, C = 2, D = 3 }
    [Flags]
    enum FlagEnum { None = 0, A = 1, B = 2, C = 4, AB = A | B }
    enum ByteEnum : byte { A = 1, B = 2, C = 3 }
    enum ShortEnum : short { A = -1, B = 0, C = 1 }
    enum EmptyEnum { }

    [Fact]
    public void ToName_ReturnsCorrectName()
    {
        Assert.Equal("A", EnumHelper.ToName(SequentialIntEnum.A));
        Assert.Equal("Y", EnumHelper.ToName(OffsetIntEnum.Y));
        Assert.Equal("B", EnumHelper.ToName(DuplicateEnum.B));
        Assert.Equal("C", EnumHelper.ToName(ByteEnum.C));
        Assert.Equal("A", EnumHelper.ToName(ShortEnum.A));
    }

    [Fact]
    public void ToUtf8Name_ReturnsUtf8EncodedName()
    {
        Assert.Equal("A"u8.ToArray(), EnumHelper.ToUtf8Name(SequentialIntEnum.A));
        Assert.Equal("Y"u8.ToArray(), EnumHelper.ToUtf8Name(OffsetIntEnum.Y));
        Assert.Equal("B"u8.ToArray(), EnumHelper.ToUtf8Name(DuplicateEnum.B));
        Assert.Equal("C"u8.ToArray(), EnumHelper.ToUtf8Name(ByteEnum.C));
        Assert.Equal("A"u8.ToArray(), EnumHelper.ToUtf8Name(ShortEnum.A));
    }

    [Fact]
    public void ToUtf8Name_MatchesToName()
    {
        Assert.Equal(EnumHelper.ToName(FlagEnum.AB), Encoding.UTF8.GetString(EnumHelper.ToUtf8Name(FlagEnum.AB)));
        Assert.Equal(EnumHelper.ToName(ShortEnum.B), Encoding.UTF8.GetString(EnumHelper.ToUtf8Name(ShortEnum.B)));
    }

    [Fact]
    public void ToUtf8Name_ReturnsCachedInstance()
    {
        Assert.Same(EnumHelper.ToUtf8Name(OffsetIntEnum.Z), EnumHelper.ToUtf8Name(OffsetIntEnum.Z));
    }

    [Fact]
    public void ToInt_ReturnsUnderlyingValue()
    {
        Assert.Equal(0, EnumHelper.ToInt(SequentialIntEnum.A));
        Assert.Equal(11, EnumHelper.ToInt(OffsetIntEnum.Y));
        Assert.Equal(2, EnumHelper.ToInt(DuplicateEnum.C));
        Assert.Equal(3, EnumHelper.ToInt(ByteEnum.C));
        Assert.Equal(-1, EnumHelper.ToInt(ShortEnum.A));
    }

    [Fact]
    public void TryFromString_ParsesCorrectly()
    {
        Assert.True(EnumHelper.TryFromString("B", out SequentialIntEnum result));
        Assert.Equal(SequentialIntEnum.B, result);

        Assert.True(EnumHelper.TryFromString("c", out ByteEnum byteResult));
        Assert.Equal(ByteEnum.C, byteResult);

        Assert.False(EnumHelper.TryFromString("NotAValue", out OffsetIntEnum _));
    }

    [Fact]
    public void TryFromInt_ParsesCorrectly()
    {
        Assert.True(EnumHelper.TryFromInt(10, out OffsetIntEnum result));
        Assert.Equal(OffsetIntEnum.X, result);

        Assert.True(EnumHelper.TryFromInt(2, out DuplicateEnum dupResult));
        // Could be B or C, as both have value 2
        Assert.True(dupResult == DuplicateEnum.B || dupResult == DuplicateEnum.C);

        Assert.False(EnumHelper.TryFromInt(99, out ByteEnum _));
    }

    [Fact]
    public void IsFlagSet_WorksForFlags()
    {
        Assert.True(EnumHelper.IsFlagSet(FlagEnum.AB, FlagEnum.A));
        Assert.True(EnumHelper.IsFlagSet(FlagEnum.AB, FlagEnum.B));
        Assert.False(EnumHelper.IsFlagSet(FlagEnum.A, FlagEnum.B));
        Assert.False(EnumHelper.IsFlagSet(FlagEnum.None, FlagEnum.A));
    }

    [Fact]
    public void EmptyEnum_HandlesGracefully()
    {
        // Should not throw
        Assert.False(EnumHelper.TryFromString("A", out EmptyEnum _));
        Assert.False(EnumHelper.TryFromInt(0, out EmptyEnum _));
    }

    [Fact]
    public void NotSupportedEnumType_Throws()
    {
        // ulong is not supported
        Assert.Throws<NotSupportedException>(() => EnumHelper<NotSupportedEnum>.ToInt(NotSupportedEnum.A));
    }

    enum NotSupportedEnum : ulong { A = 1 }
}