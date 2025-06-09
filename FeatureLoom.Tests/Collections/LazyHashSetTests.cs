using FeatureLoom.Collections;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Collections;

public class LazyHashSetTests
{
    [Fact]
    public void ReadOperationsOnUninitializedSetReturnDefaults()
    {
        var set = new LazyHashSet<int>();
        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(42));
        Assert.Empty(set);
    }

    [Fact]
    public void Add_AllocatesAndAddsElement()
    {
        var set = new LazyHashSet<string>();
        Assert.Equal(0, set.Count);

        bool added = set.Add("foo");
        Assert.True(added);
        Assert.Equal(1, set.Count);
        Assert.True(set.Contains("foo"));
    }

    [Fact]
    public void Remove_ReturnsFalseIfNotPresentOrUninitialized()
    {
        var set = new LazyHashSet<int>();
        Assert.False(set.Remove(1));

        set.Add(1);
        Assert.True(set.Remove(1));
        Assert.False(set.Contains(1));
    }

    [Fact]
    public void Clear_DoesNothingIfUninitialized()
    {
        var set = new LazyHashSet<int>();
        set.Clear(); // Should not throw
        Assert.Equal(0, set.Count);

        set.Add(1);
        set.Clear();
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void UnionWith_AllocatesAndUnions()
    {
        var set = new LazyHashSet<int>();
        set.UnionWith(new[] { 1, 2, 3 });
        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(2));
    }

    [Fact]
    public void IntersectWith_DoesNothingIfUninitialized()
    {
        var set = new LazyHashSet<int>();
        set.IntersectWith(new[] { 1, 2, 3 }); // Should not throw
        Assert.Equal(0, set.Count);

        set.UnionWith(new[] { 1, 2, 3 });
        set.IntersectWith(new[] { 2, 3, 4 });
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(3));
        Assert.False(set.Contains(1));
    }

    [Fact]
    public void ExceptWith_DoesNothingIfUninitialized()
    {
        var set = new LazyHashSet<int>();
        set.ExceptWith(new[] { 1, 2 }); // Should not throw

        set.UnionWith(new[] { 1, 2, 3 });
        set.ExceptWith(new[] { 2 });
        Assert.Equal(2, set.Count);
        Assert.False(set.Contains(2));
    }

    [Fact]
    public void SetEqualsAndOverlapsBehaveAsExpected()
    {
        var set = new LazyHashSet<int>();
        Assert.True(set.SetEquals(new int[0]));
        Assert.False(set.Overlaps(new[] { 1 }));

        set.Add(1);
        set.Add(2);
        Assert.True(set.SetEquals(new[] { 2, 1 }));
        Assert.True(set.Overlaps(new[] { 2, 3 }));
    }

    [Fact]
    public void ImplicitConversionToAndFromHashSetWorks()
    {
        var hashSet = new HashSet<string> { "a", "b" };
        LazyHashSet<string> lazy = hashSet;
        Assert.True(lazy.Contains("a"));
        Assert.Equal(2, lazy.Count);

        HashSet<string> extracted = lazy;
        Assert.Same(hashSet, extracted);
    }
}