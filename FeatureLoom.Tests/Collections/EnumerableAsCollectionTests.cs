using System;
using System.Collections.Generic;
using System.Linq;
using FeatureLoom.Collections;
using Xunit;

namespace FeatureLoom.Collections;

public class EnumerableAsCollectionTests
{
    [Fact]
    public void Constructor_Throws_OnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new EnumerableAsCollection<int>(null));
    }

    [Fact]
    public void Count_And_Enumeration_Work()
    {
        var source = Enumerable.Range(1, 5);
        var col = new EnumerableAsCollection<int>(source);

        Assert.Equal(5, col.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, col.ToArray());
    }

    [Fact]
    public void Contains_Works()
    {
        var col = new EnumerableAsCollection<string>(new[] { "a", "b", "c" });
        Assert.True(col.Contains("b"));
        Assert.False(col.Contains("x"));
    }

    [Fact]
    public void IsReadOnly_Is_True()
    {
        var col = new EnumerableAsCollection<int>(Enumerable.Empty<int>());
        Assert.True(col.IsReadOnly);
    }

    [Fact]
    public void Add_Throws()
    {
        var col = new EnumerableAsCollection<int>(Enumerable.Empty<int>());
        Assert.Throws<NotSupportedException>(() => col.Add(1));
    }

    [Fact]
    public void Remove_Throws()
    {
        var col = new EnumerableAsCollection<int>(Enumerable.Empty<int>());
        Assert.Throws<NotSupportedException>(() => col.Remove(1));
    }

    [Fact]
    public void Clear_Throws()
    {
        var col = new EnumerableAsCollection<int>(Enumerable.Empty<int>());
        Assert.Throws<NotSupportedException>(() => col.Clear());
    }

    [Fact]
    public void CopyTo_Works()
    {
        var col = new EnumerableAsCollection<int>(new[] { 1, 2, 3 });
        int[] arr = new int[5];
        col.CopyTo(arr, 1);
        Assert.Equal(new[] { 0, 1, 2, 3, 0 }, arr);
    }

    [Fact]
    public void CopyTo_Throws_OnNull()
    {
        var col = new EnumerableAsCollection<int>(new[] { 1 });
        Assert.Throws<ArgumentNullException>(() => col.CopyTo(null, 0));
    }

    [Fact]
    public void CopyTo_Throws_OnNegativeIndex()
    {
        var col = new EnumerableAsCollection<int>(new[] { 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => col.CopyTo(new int[1], -1));
    }

    [Fact]
    public void CopyTo_Throws_IfArrayTooSmall()
    {
        var col = new EnumerableAsCollection<int>(new[] { 1, 2, 3 });
        Assert.Throws<ArgumentException>(() => col.CopyTo(new int[2], 0));
    }

    [Fact]
    public void Materialization_Is_Lazy_And_Only_Once()
    {
        int callCount = 0;
        IEnumerable<int> source = Enumerable.Range(1, 3).Select(x => { callCount++; return x; });
        var col = new EnumerableAsCollection<int>(source);

        // First access triggers enumeration
        Assert.Equal(3, col.Count);
        Assert.Equal(3, callCount);

        // Second access does not re-enumerate
        Assert.Equal(3, col.Count);
        Assert.Equal(3, callCount);
    }
}