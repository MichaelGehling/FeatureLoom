using System;
using System.Collections;
using System.Linq;
using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Collections;

public class ArraySegmentBuilderTests
{
    [Fact]
    public void InitialState_IsEmpty()
    {
        var builder = new ArraySegmentBuilder<int>(new SlicedBuffer<int>(capacity: 64));

        Assert.Equal(0, builder.Count);
        Assert.Equal(0, builder.CombinedSegment.Count);
        Assert.Empty(builder.CombinedSegment.Array);
    }

    [Fact]
    public void Append_ByteSegments_CombinesInOrder()
    {
        var builder = new ArraySegmentBuilder<byte>(new SlicedBuffer<byte>(capacity: 64));

        var result1 = builder.Append(new ArraySegment<byte>(new byte[] { 1, 2 }));
        var result2 = builder.Append(new ArraySegment<byte>(new byte[] { 3, 4, 5 }));

        Assert.Equal(2, result1.Count);
        Assert.Equal(5, result2.Count);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, result2.ToArray());
        Assert.Equal(5, builder.Count);
        Assert.Equal(4, builder[3]);
    }

    [Fact]
    public void Append_ByteSegmentWithOffset_CopiesCorrectSlice()
    {
        var builder = new ArraySegmentBuilder<byte>(new SlicedBuffer<byte>(capacity: 64));
        var source = new byte[] { 99, 1, 2, 3, 88 };

        var result = builder.Append(new ArraySegment<byte>(source, 1, 3));

        Assert.Equal(new byte[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void Append_NonByteSegments_CombinesInOrder()
    {
        var builder = new ArraySegmentBuilder<int>(new SlicedBuffer<int>(capacity: 64));

        var result = builder.Append(new ArraySegment<int>(new[] { 10, 20 }));
        result = builder.Append(new ArraySegment<int>(new[] { 30, 40, 50 }));

        Assert.Equal(5, result.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50 }, result.ToArray());
        Assert.Equal(5, builder.Count);
        Assert.Equal(50, builder[4]);
    }

    [Fact]
    public void Append_NonByteSegmentWithOffset_CopiesCorrectSlice()
    {
        var builder = new ArraySegmentBuilder<int>(new SlicedBuffer<int>(capacity: 64));
        var source = new[] { 100, 11, 22, 33, 200 };

        var result = builder.Append(new ArraySegment<int>(source, 1, 3));

        Assert.Equal(new[] { 11, 22, 33 }, result.ToArray());
    }

    [Fact]
    public void Append_ReferenceTypeSegments_CombinesInOrder()
    {
        var builder = new ArraySegmentBuilder<string>(new SlicedBuffer<string>(capacity: 64));

        var result = builder.Append(new ArraySegment<string>(new[] { "a", "b" }));
        result = builder.Append(new ArraySegment<string>(new[] { "c" }));

        Assert.Equal(new[] { "a", "b", "c" }, result.ToArray());
        Assert.Equal(3, builder.Count);
        Assert.Equal("b", builder[1]);
    }

    [Fact]
    public void Append_EmptyDefaultSegment_DoesNotChangeState()
    {
        var builder = new ArraySegmentBuilder<int>(new SlicedBuffer<int>(capacity: 64));
        builder.Append(new ArraySegment<int>(new[] { 1, 2, 3 }));

        var result = builder.Append(default);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
        Assert.Equal(3, builder.Count);
    }

    [Fact]
    public void Clear_Default_ResetsBuilderState()
    {
        var builder = new ArraySegmentBuilder<int>(new SlicedBuffer<int>(capacity: 64));
        builder.Append(new ArraySegment<int>(new[] { 1, 2, 3 }));

        builder.Clear();

        Assert.Equal(0, builder.CombinedSegment.Count);
        Assert.Equal(0, builder.Count);

        var result = builder.Append(new ArraySegment<int>(new[] { 7, 8 }));
        Assert.Equal(new[] { 7, 8 }, result.ToArray());
    }

    [Fact]
    public void Clear_UnsafeReuse_ResetsBuilderState()
    {
        var builder = new ArraySegmentBuilder<byte>(new SlicedBuffer<byte>(capacity: 64));
        builder.Append(new ArraySegment<byte>(new byte[] { 9, 8, 7 }));

        builder.Clear(unsafeReuse: true);

        Assert.Equal(0, builder.CombinedSegment.Count);
        Assert.Equal(0, builder.Count);

        var result = builder.Append(new ArraySegment<byte>(new byte[] { 1, 2 }));
        Assert.Equal(new byte[] { 1, 2 }, result.ToArray());
    }

    [Fact]
    public void Dispose_ResetsBuilderState()
    {
        var builder = new ArraySegmentBuilder<int>(new SlicedBuffer<int>(capacity: 64));
        builder.Append(new ArraySegment<int>(new[] { 1, 2, 3 }));

        builder.Dispose();

        Assert.Equal(0, builder.Count);
        Assert.Equal(0, builder.CombinedSegment.Count);
    }

    [Fact]
    public void GetEnumerator_Generic_EnumeratesCombinedSegment()
    {
        var builder = new ArraySegmentBuilder<int>(new SlicedBuffer<int>(capacity: 64));
        builder.Append(new ArraySegment<int>(new[] { 1, 2 }));
        builder.Append(new ArraySegment<int>(new[] { 3, 4 }));

        var values = builder.ToArray();

        Assert.Equal(new[] { 1, 2, 3, 4 }, values);
    }

    [Fact]
    public void GetEnumerator_NonGeneric_EnumeratesCombinedSegment()
    {
        var builder = new ArraySegmentBuilder<int>(new SlicedBuffer<int>(capacity: 64));
        builder.Append(new ArraySegment<int>(new[] { 5, 6, 7 }));

        var values = ((IEnumerable)builder).Cast<int>().ToArray();

        Assert.Equal(new[] { 5, 6, 7 }, values);
    }

    [Fact]
    public void GetEnumerator_Empty_YieldsNoElements()
    {
        var builder = new ArraySegmentBuilder<int>(new SlicedBuffer<int>(capacity: 64));

        Assert.Empty(builder);
        Assert.Empty(((IEnumerable)builder).Cast<int>());
    }
}