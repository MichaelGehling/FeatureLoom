using System;
using System.Collections.Generic;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class DelegateEqualityComparerTests
{
    [Fact]
    public void Equals_ReturnsTrue_ForEqualObjects()
    {
        var comparer = new DelegateEqualityComparer<string>(
            (a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
        );

        Assert.True(comparer.Equals("test", "TEST"));
    }

    [Fact]
    public void Equals_ReturnsFalse_ForNonEqualObjects()
    {
        var comparer = new DelegateEqualityComparer<int>(
            (a, b) => a == b
        );

        Assert.False(comparer.Equals(1, 2));
    }

    [Fact]
    public void GetHashCode_UsesCustomDelegate()
    {
        var comparer = new DelegateEqualityComparer<string>(
            (a, b) => a.Length == b.Length,
            s => s.Length
        );

        Assert.Equal(comparer.GetHashCode("abc"), comparer.GetHashCode("xyz"));
        Assert.NotEqual(comparer.GetHashCode("abc"), comparer.GetHashCode("abcd"));
    }

    [Fact]
    public void GetHashCode_UsesDefaultIfNotProvided()
    {
        var comparer = new DelegateEqualityComparer<string>(
            (a, b) => a == b
        );

        string value = "hello";
        Assert.Equal(value.GetHashCode(), comparer.GetHashCode(value));
    }

    [Fact]
    public void ThrowsArgumentNullException_IfEqualsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegateEqualityComparer<object>(null));
    }

    [Fact]
    public void GetHashCode_ReturnsZero_ForNullObject()
    {
        var comparer = new DelegateEqualityComparer<string>(
            (a, b) => a == b
        );

        Assert.Equal(0, comparer.GetHashCode(null));
    }
}