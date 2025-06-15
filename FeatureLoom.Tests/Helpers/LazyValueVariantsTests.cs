using System;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class LazyValueVariantsTests
{
    private class Counter
    {
        public int Value;
        public Counter() => Value++;
    }

    [Fact]
    public void LazyValue_CreatesOnceAndResets()
    {
        var lazy = new LazyValue<Counter>();
        var first = lazy.Obj;
        var second = lazy.Obj;
        Assert.Same(first, second);
        Assert.True(lazy.Exists);

        lazy.RemoveObj();
        Assert.False(lazy.Exists);

        var third = lazy.Obj;
        Assert.NotSame(first, third);
        Assert.True(lazy.Exists);
    }

    [Fact]
    public void LazyValue_ImplicitConversionWorks()
    {
        var counter = new Counter();
        LazyValue<Counter> lazy = counter;
        Assert.Same(counter, lazy.Obj);

        Counter direct = lazy;
        Assert.Same(counter, direct);
    }

    [Fact]
    public void LazyFactoryValue_CreatesOnceAndResets_WithFactoryClearing()
    {
        int factoryCalls = 0;
        var lazy = new LazyFactoryValue<Counter>(() => { factoryCalls++; return new Counter(); }, clearFactoryAfterConstruction: true);

        var first = lazy.Obj;
        Assert.Equal(1, factoryCalls);
        Assert.True(lazy.Exists);

        lazy.RemoveObj();
        Assert.False(lazy.Exists);

        Assert.Throws<InvalidOperationException>(() => { var _ = lazy.Obj; });
    }

    [Fact]
    public void LazyFactoryValue_CreatesOnceAndResets_WithoutFactoryClearing()
    {
        int factoryCalls = 0;
        var lazy = new LazyFactoryValue<Counter>(() => { factoryCalls++; return new Counter(); }, clearFactoryAfterConstruction: false);

        var first = lazy.Obj;
        Assert.Equal(1, factoryCalls);
        Assert.True(lazy.Exists);

        lazy.RemoveObj();
        Assert.False(lazy.Exists);

        var second = lazy.Obj;
        Assert.Equal(2, factoryCalls);
        Assert.True(lazy.Exists);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void LazyFactoryValue_ThreadSafeInitialization()
    {
        int factoryCalls = 0;
        var lazy = new LazyFactoryValue<Counter>(() => { Interlocked.Increment(ref factoryCalls); return new Counter(); }, threadSafe: true);

        Counter[] results = new Counter[10];
        Parallel.For(0, 10, i => results[i] = lazy.Obj);

        // The factory may be called multiple times, but all results must be the same instance
        Assert.True(factoryCalls >= 1);
        Assert.All(results, c => Assert.Same(results[0], c));
    }

    [Fact]
    public void LazyFactoryValue_NonThreadSafeInitialization()
    {
        int factoryCalls = 0;
        var lazy = new LazyFactoryValue<Counter>(() => { factoryCalls++; return new Counter(); }, threadSafe: false, clearFactoryAfterConstruction: false);

        // Simulate sequential access (not thread-safe)
        var first = lazy.Obj;
        lazy.RemoveObj();
        var second = lazy.Obj;

        Assert.Equal(2, factoryCalls);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void LazyUnsafeValue_CreatesOnceAndResets()
    {
        var lazy = new LazyUnsafeValue<Counter>();
        var first = lazy.Obj;
        var second = lazy.Obj;
        Assert.Same(first, second);
        Assert.True(lazy.Exists);

        lazy.RemoveObj();
        Assert.False(lazy.Exists);

        var third = lazy.Obj;
        Assert.NotSame(first, third);
        Assert.True(lazy.Exists);
    }

    [Fact]
    public void LazyUnsafeValue_ImplicitConversionWorks()
    {
        var counter = new Counter();
        LazyUnsafeValue<Counter> lazy = counter;
        Assert.Same(counter, lazy.Obj);

        Counter direct = lazy;
        Assert.Same(counter, direct);
    }
}