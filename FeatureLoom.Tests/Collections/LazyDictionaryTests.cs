using FeatureLoom.Collections;
using System.Collections.Generic;
using Xunit;

namespace FeatureLoom.Collections;

public class LazyDictionaryTests
{
    [Fact]
    public void ReadOperationsOnUninitializedDictionaryReturnDefaults()
    {
        var dict = new LazyDictionary<int, string>();
        Assert.Equal(0, dict.Count);
        Assert.False(dict.ContainsKey(42));
        Assert.Empty(dict);
        Assert.Empty(dict.Keys);
        Assert.Empty(dict.Values);

        Assert.False(dict.TryGetValue(1, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Add_AllocatesAndAddsElement()
    {
        var dict = new LazyDictionary<string, int>();
        dict.Add("foo", 42);
        Assert.Equal(1, dict.Count);
        Assert.True(dict.ContainsKey("foo"));
        Assert.Equal(42, dict["foo"]);
    }

    [Fact]
    public void Indexer_ThrowsIfUninitializedOrKeyNotFound()
    {
        var dict = new LazyDictionary<int, string>();
        Assert.Throws<KeyNotFoundException>(() => { var _ = dict[1]; });

        dict.Add(1, "a");
        Assert.Equal("a", dict[1]);
        dict[1] = "b";
        Assert.Equal("b", dict[1]);
    }

    [Fact]
    public void Remove_ReturnsFalseIfNotPresentOrUninitialized()
    {
        var dict = new LazyDictionary<int, string>();
        Assert.False(dict.Remove(1));

        dict.Add(1, "a");
        Assert.True(dict.Remove(1));
        Assert.False(dict.ContainsKey(1));
    }

    [Fact]
    public void Clear_DoesNothingIfUninitialized()
    {
        var dict = new LazyDictionary<int, string>();
        dict.Clear(); // Should not throw
        Assert.Equal(0, dict.Count);

        dict.Add(1, "a");
        dict.Clear();
        Assert.Equal(0, dict.Count);
    }

    [Fact]
    public void TryGetValue_ReturnsFalseIfUninitializedOrKeyNotFound()
    {
        var dict = new LazyDictionary<int, string>();
        Assert.False(dict.TryGetValue(1, out var value));
        Assert.Null(value);

        dict.Add(1, "a");
        Assert.True(dict.TryGetValue(1, out value));
        Assert.Equal("a", value);
    }

    [Fact]
    public void ContainsKeyAndContainsBehaveAsExpected()
    {
        var dict = new LazyDictionary<string, int>();
        Assert.False(dict.ContainsKey("foo"));
        Assert.False(dict.Contains(new KeyValuePair<string, int>("foo", 1)));

        dict.Add("foo", 1);
        Assert.True(dict.ContainsKey("foo"));
        Assert.True(dict.Contains(new KeyValuePair<string, int>("foo", 1)));
    }

    [Fact]
    public void CopyTo_DoesNothingIfUninitialized()
    {
        var dict = new LazyDictionary<int, string>();
        var array = new KeyValuePair<int, string>[3];
        dict.CopyTo(array, 0); // Should not throw
        Assert.Equal(new KeyValuePair<int, string>[3], array);

        dict.Add(1, "a");
        dict.Add(2, "b");
        dict.CopyTo(array, 1);
        Assert.Equal(1, array[1].Key);
        Assert.Equal("a", array[1].Value);
        Assert.Equal(2, array[2].Key);
        Assert.Equal("b", array[2].Value);
    }

    [Fact]
    public void ImplicitConversionToAndFromDictionaryWorks()
    {
        var baseDict = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
        LazyDictionary<string, int> lazy = baseDict;
        Assert.True(lazy.ContainsKey("a"));
        Assert.Equal(2, lazy.Count);

        Dictionary<string, int> extracted = lazy;
        Assert.Same(baseDict, extracted);
    }
}