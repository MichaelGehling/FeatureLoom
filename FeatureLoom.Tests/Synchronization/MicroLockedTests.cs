using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Synchronization;

public class MicroLockedTests
{
    class TestClass
    {
        public int Value { get; set; }
    }

    [Fact]
    public void EagerInitialization_ReturnsObject()
    {
        var obj = new TestClass { Value = 42 };
        var locked = new MicroLocked<TestClass>(obj);

        using (locked.UseReadLocked(out var readObj))
        {
            Assert.Equal(42, readObj.Value);
        }
    }

    [Fact]
    public void LazyInitialization_CreatesObjectOnFirstAccess()
    {
        bool factoryCalled = false;
        var locked = new MicroLocked<TestClass>(() =>
        {
            factoryCalled = true;
            return new TestClass { Value = 99 };
        });

        Assert.False(factoryCalled);
        using (locked.UseReadLocked(out var obj))
        {
            Assert.True(factoryCalled);
            Assert.Equal(99, obj.Value);
        }
    }

    [Fact]
    public void Set_ReplacesObject()
    {
        var locked = new MicroLocked<TestClass>(new TestClass { Value = 1 });
        locked.Set(new TestClass { Value = 2 });

        using (locked.UseReadLocked(out var obj))
        {
            Assert.Equal(2, obj.Value);
        }
    }

    [Fact]
    public void UseLocked_ExecutesAction()
    {
        var locked = new MicroLocked<TestClass>(new TestClass { Value = 0 });
        locked.UseLocked(obj => obj.Value = 123);

        using (locked.UseReadLocked(out var obj))
        {
            Assert.Equal(123, obj.Value);
        }
    }

    [Fact]
    public void UseLocked_ReturnsResult()
    {
        var locked = new MicroLocked<TestClass>(new TestClass { Value = 55 });
        int result = locked.UseLocked(obj => obj.Value * 2);
        Assert.Equal(110, result);
    }

    [Fact]
    public void TryUseWriteLocked_Succeeds()
    {
        var locked = new MicroLocked<TestClass>(new TestClass { Value = 7 });
        Assert.True(locked.TryUseLocked(out var handle, out var obj));
        using (handle)
        {
            obj.Value = 8;
        }
        using (locked.UseReadLocked(out var readObj))
        {
            Assert.Equal(8, readObj.Value);
        }
    }

    [Fact]
    public void TryUseReadLocked_Succeeds()
    {
        var locked = new MicroLocked<TestClass>(new TestClass { Value = 7 });
        Assert.True(locked.TryUseReadLocked(out var handle, out var obj));
        using (handle)
        {
            Assert.Equal(7, obj.Value);
        }
    }

    [Fact]
    public void GetUnlocked_ReturnsObjectWithoutLock()
    {
        var obj = new TestClass { Value = 42 };
        var locked = new MicroLocked<TestClass>(obj);
        Assert.Equal(42, locked.GetUnlocked().Value);
    }

    [Fact]
    public void UseReadLocked_AllowsLinqMaterialization()
    {
        var locked = new MicroLocked<List<int>>(new List<int> { 1, 2, 3, 4 });
        List<int> evens;
        using (locked.UseReadLocked(out var list))
        {
            evens = list.Where(x => x % 2 == 0).ToList();
        }
        Assert.Equal(new[] { 2, 4 }, evens);
    }

    [Fact]
    public void ThreadSafety_AllowsConcurrentReadsAndWrites()
    {
        var locked = new MicroLocked<List<int>>(new List<int>());
        int writeCount = 0;
        int readCount = 0;
        var cts = new CancellationTokenSource();
        var writeTask = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                locked.UseLocked(list => list.Add(1));
                Interlocked.Increment(ref writeCount);
            }
        });
        var readTask = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                locked.UseReadLocked(list => { var _ = list.Count; });
                Interlocked.Increment(ref readCount);
            }
        });

        Thread.Sleep(200);
        cts.Cancel();
        Task.WaitAll(writeTask, readTask);

        Assert.True(writeCount > 0);
        Assert.True(readCount > 0);
    }
}