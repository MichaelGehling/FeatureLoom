using FeatureLoom.Collections;
using System;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Collections;

public class LazyListTests
{
    [Fact]
    public void ReadOperationsOnUninitializedListReturnDefaults()
    {
        var list = new LazyList<int>();
        Assert.Equal(0, list.Count);
        Assert.False(list.Contains(42));
        Assert.Empty(list);
        Assert.Equal(-1, list.IndexOf(42));
    }

    [Fact]
    public void Add_AllocatesAndAddsElement()
    {
        var list = new LazyList<string>();
        Assert.Equal(0, list.Count);

        list.Add("foo");
        Assert.Equal(1, list.Count);
        Assert.True(list.Contains("foo"));
        Assert.Equal(0, list.IndexOf("foo"));
    }

    [Fact]
    public void Remove_ReturnsFalseIfNotPresentOrUninitialized()
    {
        var list = new LazyList<int>();
        Assert.False(list.Remove(1));

        list.Add(1);
        Assert.True(list.Remove(1));
        Assert.False(list.Contains(1));
    }

    [Fact]
    public void Clear_DoesNothingIfUninitialized()
    {
        var list = new LazyList<int>();
        list.Clear(); // Should not throw
        Assert.Equal(0, list.Count);

        list.Add(1);
        list.Clear();
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void Indexer_ThrowsIfUninitializedOrOutOfRange()
    {
        var list = new LazyList<string>();
        Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = list[0]; });

        list.Add("a");
        Assert.Equal("a", list[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = list[1]; });

        Assert.Throws<ArgumentOutOfRangeException>(() => list[1] = "b");
        list[0] = "b";
        Assert.Equal("b", list[0]);
    }

    [Fact]
    public void Insert_AllocatesAndInserts()
    {
        var list = new LazyList<int>();
        list.Insert(0, 42);
        Assert.Equal(1, list.Count);
        Assert.Equal(42, list[0]);
    }

    [Fact]
    public void RemoveAt_ThrowsIfUninitializedOrOutOfRange()
    {
        var list = new LazyList<int>();
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(0));

        list.Add(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(1));
        list.RemoveAt(0);
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void CopyTo_DoesNothingIfUninitialized()
    {
        var list = new LazyList<int>();
        var array = new int[3];
        list.CopyTo(array, 0); // Should not throw
        Assert.Equal(new int[3], array);

        list.Add(1);
        list.Add(2);
        list.CopyTo(array, 1);
        Assert.Equal(new int[] { 0, 1, 2 }, array);
    }

    [Fact]
    public void ImplicitConversionToAndFromListWorks()
    {
        var baseList = new List<string> { "a", "b" };
        LazyList<string> lazy = baseList;
        Assert.True(lazy.Contains("a"));
        Assert.Equal(2, lazy.Count);

        List<string> extracted = lazy;
        Assert.Same(baseList, extracted);
    }
}