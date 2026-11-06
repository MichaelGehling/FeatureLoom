using System;
using System.Text;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Tests.Helpers;

public class LittleEndianTests
{
    [Theory]
    [InlineData(0UL, 1)]
    [InlineData(0xABUL, 1)]
    [InlineData(0x123456UL, 3)]
    [InlineData(ulong.MaxValue, 8)]
    public void WriteRead_RoundTrips(ulong value, int size)
    {
        var buffer = new byte[10];
        LittleEndian.Write(buffer, 1, value, size);
        Assert.Equal(value, LittleEndian.Read(buffer, 1, size));
    }

    [Fact]
    public void Write_UsesLittleEndianOrder()
    {
        var buffer = new byte[4];
        LittleEndian.WriteUInt32(buffer, 0, 0x04030201);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer);
    }

    [Fact]
    public void Write_TruncatesToSize()
    {
        var buffer = new byte[3];
        LittleEndian.Write(buffer, 0, 0x11223344, 2);
        Assert.Equal(new byte[] { 0x44, 0x33, 0 }, buffer);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 9)]
    [InlineData(2, 2)]
    [InlineData(-1, 1)]
    public void InvalidRange_Throws(int offset, int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LittleEndian.Read(new byte[3], offset, size));
    }
}

public class Crc32Tests
{
    [Fact]
    public void Compute_MatchesStandardCheckValue()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void Compute_EmptyIsZero()
    {
        Assert.Equal(0u, Crc32.Compute(new byte[0]));
    }

    [Fact]
    public void Compute_IncrementalEqualsWhole()
    {
        var data = Encoding.ASCII.GetBytes("123456789");
        uint part = Crc32.Compute(data, 0, 4);
        Assert.Equal(Crc32.Compute(data), Crc32.Compute(data, 4, 5, part));
    }

    [Fact]
    public void Compute_MatchesBytewiseReference_ForAllLengthsAndOffsets()
    {
        var data = new byte[300];
        new Random(42).NextBytes(data);
        for (int offset = 0; offset < 9; offset++)
        {
            for (int count = 0; count <= data.Length - offset; count += count < 40 ? 1 : 37)
            {
                Assert.Equal(Reference(data, offset, count, 0), Crc32.Compute(data, offset, count));
                Assert.Equal(Reference(data, offset, count, 0x12345678u), Crc32.Compute(data, offset, count, 0x12345678u));
            }
        }
    }

    private static uint Reference(byte[] data, int offset, int count, uint crc)
    {
        uint c = ~crc;
        for (int i = offset; i < offset + count; i++)
        {
            c ^= data[i];
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }
        return ~c;
    }
}
