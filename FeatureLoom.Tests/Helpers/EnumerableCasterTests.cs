using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Helpers;

public class EnumerableCasterTests
{
    [Fact]
    public void TryCastAllElements_Generic_Success()
    {
        IEnumerable input = new object[] { 1, 2, 3 };
        Assert.True(input.TryCastAllElements<int>(out var result));
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void TryCastAllElements_Generic_FailsOnMixedTypes()
    {
        IEnumerable input = new object[] { 1, "2", 3 };
        Assert.False(input.TryCastAllElements<int>(out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryCastAllElements_NonGeneric_Success()
    {
        IEnumerable input = new object[] { "a", "b", "c" };
        Assert.True(input.TryCastAllElements(typeof(string), out var result));
        Assert.True(result is string[]);
        Assert.Equal(new[] { "a", "b", "c" }, result.Cast<string>());
    }

    [Fact]
    public void TryCastAllElements_NonGeneric_FailsOnMixedTypes()
    {
        IEnumerable input = new object[] { "a", 2, "c" };
        Assert.False(input.TryCastAllElements(typeof(string), out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryCastAllElements_Generic_SuccessWithNullsAndNullable()
    {
        IEnumerable input = new object[] { 1, null, 3 };
        Assert.True(input.TryCastAllElements<int?>(out var result));
        Assert.True(result is int?[]);
        Assert.Equal(new int?[] { 1, null, 3 }, result);
    }

    [Fact]
    public void TryCastAllElements_Generic_FailsWithNullsAndNonNullable()
    {
        IEnumerable input = new object[] { 1, null, 3 };
        Assert.False(input.TryCastAllElements<int>(out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryCastAllElements_EmptyCollection()
    {
        IEnumerable input = new object[0];
        Assert.True(input.TryCastAllElements<int>(out var result));
        Assert.True(result is int[]);
    }

    [Fact]
    public void CastToCommonTypeArray_ReturnsTypedArray()
    {
        IEnumerable input = new object[] { 1, 2, 3 };
        var array = input.CastToCommonTypeArray(out var commonType);
        Assert.Equal(typeof(int), commonType);
        Assert.Equal(new[] { 1, 2, 3 }, array.Cast<int>());
    }

    [Fact]
    public void CastToCommonTypeArray_ReturnsNullableArrayForNullsAndValueTypes()
    {
        IEnumerable input = new object[] { 1, null, 3 };
        var array = input.CastToCommonTypeArray(out var commonType);
        Assert.Equal(typeof(int?), commonType);
        Assert.Equal(new int?[] { 1, null, 3 }, array.Cast<int?>());
    }

    [Fact]
    public void CastToCommonTypeArray_ReturnsIComparableArrayForMixedBuiltInTypes()
    {
        IEnumerable input = new object[] { 1, "a", 3.0 };
        var array = input.CastToCommonTypeArray(out var commonType);
        Assert.Equal(typeof(IComparable), commonType);
        Assert.Equal(new object[] { 1, "a", 3.0 }, array.Cast<IComparable>());
    }

    [Fact]
    public void ThreadSafety_Basic()
    {
        IEnumerable input = Enumerable.Range(0, 1000).Cast<object>().ToArray();
        var results = new List<int[]>();
        var actions = Enumerable.Range(0, 10).Select(_ => new Action(() =>
        {
            input.TryCastAllElements<int>(out var arr);
            lock (results) results.Add(arr);
        })).ToArray();

        Parallel.Invoke(actions);
        Assert.All(results, arr => Assert.Equal(Enumerable.Range(0, 1000), arr));
    }

    class A { }
    class B { }
    class C { }

    [Fact]
    public void CastToCommonTypeArray_ReturnsObjectArrayForUnrelatedTypes()
    {
        var a = new A();
        var b = new B();
        var c = new C();
        IEnumerable input = new object[] { a, b, c };
        var array = input.CastToCommonTypeArray(out var commonType);
        Assert.Equal(typeof(object), commonType);
        var types = array.Cast<object>().Select(o => o.GetType()).ToArray();
        Assert.Contains(typeof(A), types);
        Assert.Contains(typeof(B), types);
        Assert.Contains(typeof(C), types);
        Assert.Equal(3, types.Length);
    }
}