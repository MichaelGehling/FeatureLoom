using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using System;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.DependencyInversion;

public class FactoryTests
{

    public interface ITest
    {
        string TestString { get; }
    }

    public class A : ITest
    {
        public string TestString => "A";
    }

    public class B : ITest
    {
        string s = "B";

        public B()
        {
        }

        public B(string s)
        {
            this.s = s;
        }

        public string TestString => s;
    }


    [Fact]
    public void FactoryCanBeOverriden()
    {
        TestHelper.PrepareTestContext();

        ITest testObj = Factory.Create<ITest>(() => new A());
        Assert.NotNull(testObj);
        Assert.Equal("A", testObj.TestString);

        Factory.Override<ITest>(() => new B());
        testObj = Factory.Create<ITest>(() => new A());
        Assert.NotNull(testObj);
        Assert.Equal("B", testObj.TestString);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void FactoryCanUseDefaultConstructor()
    {
        TestHelper.PrepareTestContext();

        B testObj = Factory.Create<B>();
        Assert.NotNull(testObj);
        Assert.Equal("B", testObj.TestString);

        Factory.Override<B>(() => new B("X"));
        testObj = Factory.Create<B>(() => new B());
        Assert.NotNull(testObj);
        Assert.Equal("X", testObj.TestString);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void OverrideCanBeRemoved()
    {
        TestHelper.PrepareTestContext();

        Factory.Override<ITest>(() => new B());
        ITest testObj = Factory.Create<ITest>(() => new A());
        Assert.NotNull(testObj);
        Assert.Equal("B", testObj.TestString);

        Factory.RemoveOverride<ITest>();
        testObj = Factory.Create<ITest>(() => new A());
        Assert.NotNull(testObj);
        Assert.Equal("A", testObj.TestString);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void TryCreateFromType_CreatesInstance()
    {
        TestHelper.PrepareTestContext();

        bool result = Factory.TryCreateFromType(typeof(B), out object instance);
        Assert.True(result);
        Assert.NotNull(instance);
        Assert.IsType<B>(instance);
        Assert.Equal("B", ((B)instance).TestString);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void TryCreateFromType_WithSimplifiedName_CreatesInstance()
    {
        TestHelper.PrepareTestContext();

        string typeName = typeof(B).FullName;
        bool result = Factory.TryCreateFromType(typeName, out object instance);
        Assert.True(result);
        Assert.NotNull(instance);
        Assert.IsType<B>(instance);

        Assert.False(TestHelper.HasAnyLogError());
    }

    [Fact]
    public void Override_WithNull_RemovesOverride()
    {
        TestHelper.PrepareTestContext();

        Factory.Override<ITest>(() => new B());
        ITest testObj = Factory.Create<ITest>(() => new A());
        Assert.Equal("B", testObj.TestString);

        Factory.Override<ITest>(null); // Should remove override
        testObj = Factory.Create<ITest>(() => new A());
        Assert.Equal("A", testObj.TestString);

        Assert.False(TestHelper.HasAnyLogError());
    }
}