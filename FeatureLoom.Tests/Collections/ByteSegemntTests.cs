using System;
using System.Collections.Generic;
using System.Linq;
using FeatureLoom.Collections;
using Xunit;

namespace FeatureLoom.Collections;

public class ByteSegmentTests
{
    [Fact]
    public void Constructor_FromArraySegment_Works()
    {
        byte[] data = { 1, 2, 3, 4, 5 };
        var seg = new ArraySegment<byte>(data, 1, 3);
        var bs = new ByteSegment(seg);

        Assert.Equal(3, bs.Count);
        Assert.Equal(2, bs[0]);
        Assert.Equal(4, bs[2]);
    }

    [Fact]
    public void Constructor_FromArray_Works()
    {
        byte[] data = { 10, 20, 30 };
        var bs = new ByteSegment(data);

        Assert.Equal(3, bs.Count);
        Assert.Equal(10, bs[0]);
        Assert.Equal(30, bs[2]);
    }

    [Fact]
    public void Constructor_FromString_Works()
    {
        string str = "abc";
        var bs = new ByteSegment(str);

        Assert.Equal(3, bs.Count);
        Assert.Equal((byte)'a', bs[0]);
        Assert.Equal((byte)'c', bs[2]);
    }

    [Fact]
    public void ImplicitConversions_Work()
    {
        byte[] data = { 1, 2, 3 };
        ByteSegment bs = data;
        ArraySegment<byte> seg = bs;
        byte[] arr = bs;

        Assert.Equal(data, arr);
        Assert.Equal(data, seg.ToArray());
    }

    [Fact]
    public void SubSegment_Works()
    {
        byte[] data = { 1, 2, 3, 4, 5 };
        var bs = new ByteSegment(data);

        var sub = bs.SubSegment(1, 3);
        Assert.Equal(3, sub.Count);
        Assert.Equal(2, sub[0]);
        Assert.Equal(4, sub[2]);
    }

    [Fact]
    public void TryFindIndex_Byte_Works()
    {
        byte[] data = { 1, 2, 3, 2, 5 };
        var bs = new ByteSegment(data);

        Assert.True(bs.TryFindIndex((byte)2, out int idx));
        Assert.Equal(1, idx);

        Assert.False(bs.TryFindIndex((byte)9, out _));
    }

    [Fact]
    public void TryFindIndex_ByteSegment_Works()
    {
        byte[] data = { 1, 2, 3, 2, 3, 4 };
        var bs = new ByteSegment(data);
        var search = new ByteSegment(new byte[] { 2, 3 });

        Assert.True(bs.TryFindIndex(search, out int idx));
        Assert.Equal(1, idx);

        var notFound = new ByteSegment(new byte[] { 9, 9 });
        Assert.False(bs.TryFindIndex(notFound, out _));
    }

    [Fact]
    public void Split_Works()
    {
        byte[] data = { 1, 2, 0, 3, 0, 4 };
        var bs = new ByteSegment(data);

        var segments = bs.Split(0).ToList();
        Assert.Equal(3, segments.Count);
        Assert.Equal(new byte[] { 1, 2 }, segments[0].ToArray());
        Assert.Equal(new byte[] { 3 }, segments[1].ToArray());
        Assert.Equal(new byte[] { 4 }, segments[2].ToArray());
    }

    [Fact]
    public void Equality_Works()
    {
        byte[] data1 = { 1, 2, 3 };
        byte[] data2 = { 1, 2, 3 };
        var bs1 = new ByteSegment(data1);
        var bs2 = new ByteSegment(data2);

        Assert.True(bs1.Equals(bs2));
        Assert.True(bs1 == bs2);
        Assert.False(bs1 != bs2);

        var bs3 = new ByteSegment(new byte[] { 1, 2 });
        Assert.False(bs1.Equals(bs3));
    }

    [Fact]
    public void ToString_UTF8()
    {
        var utf8 = new ByteSegment("abc");
        Assert.Equal("abc", utf8.ToString());
    }

    [Fact]
    public void IsValid_And_IsEmptyOrInvalid_Work()
    {
        var empty = ByteSegment.Empty;
        Assert.True(empty.IsValid);
        Assert.True(empty.IsEmptyOrInvalid);

        var bs = new ByteSegment(new byte[] { 1 });
        Assert.True(bs.IsValid);
        Assert.False(bs.IsEmptyOrInvalid);
    }
}