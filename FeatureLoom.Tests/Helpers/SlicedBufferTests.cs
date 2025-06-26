using FeatureLoom.Helpers;
using System;
using System.Linq;
using Xunit;

namespace FeatureLoom.Helpers;

public class SlicedBufferTests
{
    [Fact]
    public void AllocatesSlicesWithinLimit()
    {
        var buffer = new SlicedBuffer<byte>(128, 256, 4, false, false);
        var slice = buffer.GetSlice(32);
        Assert.Equal(32, slice.Count);
        Assert.True(slice.Array.Length >= 128);
    }

    [Fact]
    public void AllocatesNewArrayForLargeSlice()
    {
        var buffer = new SlicedBuffer<byte>(128, 256, 4, false, false);
        var slice = buffer.GetSlice(128); // sliceLimit = 32
        Assert.Equal(128, slice.Count);
        Assert.NotSame(GetBufferArray(buffer), slice.Array);
    }

    [Fact]
    public void BufferGrowsAndSliceLimitRemainsFixed_WhenGrowSliceLimitFalse()
    {
        var buffer = new SlicedBuffer<byte>(64, 256, 4, false, false);
        for (int i = 0; i < 5; i++)
        {
            buffer.GetSlice(16); // allocate several slices to trigger growth
        }
        var slice = buffer.GetSlice(16); // should still use old sliceLimit
        int newLimit = GetSliceLimit(buffer);
        Assert.True(newLimit == 16);
    }

    [Fact]
    public void BufferGrowsAndSliceLimitIncreases_WhenGrowSliceLimitTrue()
    {
        var buffer = new SlicedBuffer<byte>(64, 256, 4, true, false);
        for (int i = 0; i < 5; i++)
        {
            buffer.GetSlice(16); // allocate several slices to trigger growth
        }                
        int newLimit = GetSliceLimit(buffer);
        Assert.True(newLimit > 16);
    }

    [Fact]
    public void FreeSlice_ReclaimsBufferSpaceForLatestSlice()
    {
        var buffer = new SlicedBuffer<byte>(64, 256, 4, false, false);
        var slice1 = buffer.GetSlice(16);
        var slice2 = buffer.GetSlice(16);
        buffer.FreeSlice(ref slice2);
        var slice3 = buffer.GetSlice(16);
        Assert.Equal(slice2.Offset, slice3.Offset);
    }

    [Fact]
    public void FreeSlice_DoesNotReclaimIfNotLatest()
    {
        var buffer = new SlicedBuffer<byte>(64, 256, 4, false, false);
        var slice1 = buffer.GetSlice(16);
        var slice2 = buffer.GetSlice(16);
        buffer.FreeSlice(ref slice1);
        var slice3 = buffer.GetSlice(16);
        Assert.NotEqual(slice1.Offset, slice3.Offset);
    }

    [Fact]
    public void ExtendSlice_ExpandsLatestSliceInPlace()
    {
        var buffer = new SlicedBuffer<byte>(64, 256, 4, false, false);
        var slice = buffer.GetSlice(16);
        buffer.ExtendSlice(ref slice, 8);
        Assert.Equal(24, slice.Count);
    }

    [Fact]
    public void ResizeSlice_ShrinksLatestSliceAndReclaimsSpace()
    {
        var buffer = new SlicedBuffer<byte>(64, 256, 4, false, false);
        var slice = buffer.GetSlice(16);
        buffer.ResizeSlice(ref slice, 8);
        Assert.Equal(8, slice.Count);
        var next = buffer.GetSlice(8);
        Assert.Equal(slice.Offset + slice.Count, next.Offset);
    }

    [Fact]
    public void Reset_ReusesBuffer()
    {
        var buffer = new SlicedBuffer<byte>(64, 256, 4, false, false);
        var slice1 = buffer.GetSlice(32);
        buffer.Reset(true);
        var slice2 = buffer.GetSlice(32);
        Assert.Equal(0, slice2.Offset);
    }

    [Fact]
    public void ThreadSafe_AllowsConcurrentAccess()
    {
        int numSlices = 10;
        var buffer = new SlicedBuffer<byte>(128, 256, 4, false, true);
        var results = new int[numSlices];
        System.Threading.Tasks.Parallel.For(0, numSlices, i =>
        {
            var slice = buffer.GetSlice(8);
            slice[0] = (byte)i;
            results[i] = slice[0];
        });
        Assert.Equal(Enumerable.Range(0, numSlices), results);
    }

    // Helper to access private fields for testing
    private static int GetSliceLimit<T>(SlicedBuffer<T> buffer)
    {
        var field = typeof(SlicedBuffer<T>).GetField("sliceLimit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (int)field.GetValue(buffer);
    }

    private static T[] GetBufferArray<T>(SlicedBuffer<T> buffer)
    {
        var field = typeof(SlicedBuffer<T>).GetField("buffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (T[])field.GetValue(buffer);
    }
}