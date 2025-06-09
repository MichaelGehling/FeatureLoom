using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class CommonTypeFinderTests
{
    interface IBase { }
    interface IDerived : IBase { }
    class A : IBase { }
    class B : IBase { }
    class C : IDerived { }
    class D : IDerived { }
    class E : IDerived, IDisposable { public void Dispose() { } }
    class F : IDisposable { public void Dispose() { } }
    class G : IBase, IDisposable { public void Dispose() { } }
    class H : G { }

    [Fact]
    public void ReturnsObjectForEmptyCollection()
    {
        var list = new object[0];
        Assert.Equal(typeof(object), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void ReturnsTypeForSingleElement()
    {
        var list = new[] { new A() };
        Assert.Equal(typeof(A), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void ReturnsObjectForAllNulls()
    {
        var list = new object[] { null, null };
        Assert.Equal(typeof(object), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void ReturnsTypeForAllSameType()
    {
        var list = new[] { new A(), new A(), new A() };
        Assert.Equal(typeof(A), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void ReturnsCommonBaseType()
    {
        var list = new object[] { new A(), new B() };
        Assert.Equal(typeof(IBase), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void ReturnsMostDerivedCommonInterface()
    {
        var list = new object[] { new C(), new D() };
        Assert.Equal(typeof(IDerived), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void ReturnsMostDerivedCommonInterfaceAmongMultiple()
    {
        var list = new object[] { new E(), new C() }; // Both implement IDerived, only E implements IDisposable
        Assert.Equal(typeof(IDerived), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void ReturnsIDisposableForDifferentImplementations()
    {
        var list = new object[] { new E(), new F() };
        Assert.Equal(typeof(IDisposable), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void ReturnsObjectForUnrelatedTypes()
    {
        var list = new object[] { new A(), new F() };
        Assert.Equal(typeof(object), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void ReturnsCommonBaseClass()
    {
        var list = new object[] { new G(), new H() };
        Assert.Equal(typeof(G), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void HandlesNullAndNullable()
    {
        var list = new object[] { null, new A() };
        Assert.Equal(typeof(A), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void HandlesValueTypes()
    {
        var list = new object[] { 1, 2, 3 };
        Assert.Equal(typeof(int), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void HandlesNullableAndNonNullableValueTypes()
    {
        var list = new object[] { 1, (int?)2 };
        Assert.Equal(typeof(int), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void HandlesNullAndValueTypes()
    {
        var list = new object[] { null,  1, null };
        Assert.Equal(typeof(int?), CommonTypeFinder.GetCommonType(list));
    }

    [Fact]
    public void HandlesStringsAndObjects()
    {
        var list = new object[] { "a", "b", new object() };
        Assert.Equal(typeof(object), CommonTypeFinder.GetCommonType(list));
    }
}