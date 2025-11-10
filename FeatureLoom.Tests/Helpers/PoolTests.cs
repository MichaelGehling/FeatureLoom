using FeatureLoom.Collections;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Helpers;

public class PoolTests
{
    private class TestObject
    {
        public int Value = 0;
    }

    [Fact]
    public void Take_CreatesNewObject_WhenPoolIsEmpty()
    {
        int created = 0;
        var pool = new Pool<TestObject>(() => { created++; return new TestObject(); });

        var obj1 = pool.Take();
        Assert.NotNull(obj1);
        Assert.Equal(1, created);

        var obj2 = pool.Take();
        Assert.NotNull(obj2);
        Assert.Equal(2, created);
    }

    [Fact]
    public void Return_AddsObjectBackToPool_AndTakeReusesIt()
    {
        var pool = new Pool<TestObject>(() => new TestObject());

        var obj1 = pool.Take();
        pool.Return(obj1);

        Assert.Equal(1, pool.Count);

        var obj2 = pool.Take();
        Assert.Same(obj1, obj2);
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Return_DoesNotExceedMaxSize()
    {
        var pool = new Pool<TestObject>(() => new TestObject(), maxSize: 1);

        var obj1 = pool.Take();
        var obj2 = pool.Take();

        pool.Return(obj1);
        pool.Return(obj2); // Should not increase count above 1

        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void Return_CallsResetIfProvided()
    {
        bool resetCalled = false;
        var pool = new Pool<TestObject>(
            () => new TestObject(),
            obj => resetCalled = true
        );

        var obj = pool.Take();
        pool.Return(obj);

        Assert.True(resetCalled);
    }

    [Fact]
    public void Clear_EmptiesThePool()
    {
        var pool = new Pool<TestObject>(() => new TestObject());

        var obj1 = pool.Take();
        pool.Return(obj1);
        Assert.Equal(1, pool.Count);

        pool.Clear();
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Pool_CanBeUsedWithoutThreadSafety()
    {
        var pool = new Pool<TestObject>(() => new TestObject(), threadSafe: false);

        var obj1 = pool.Take();
        pool.Return(obj1);
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void Pool_IsThreadSafe_WhenThreadSafeIsTrue()
    {
        var pool = new Pool<TestObject>(() => new TestObject(), maxSize: 100, threadSafe: true);

        int created = 0;
        pool = new Pool<TestObject>(() => { Interlocked.Increment(ref created); return new TestObject(); }, maxSize: 100, threadSafe: true);

        Parallel.For(0, 1000, i =>
        {
            var obj = pool.Take();
            pool.Return(obj);
        });

        Assert.True(created <= 1000);
        Assert.True(pool.Count <= 100);
    }
}