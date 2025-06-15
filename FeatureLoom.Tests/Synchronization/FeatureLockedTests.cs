using FeatureLoom.Synchronization;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;

namespace FeatureLoom.Synchronization;

public class FeatureLockedTests
{
    private class TestClass
    {
        public int Value;
    }

    [Fact]
    public void UseWriteLocked_AllowsExclusiveAccess()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        using (locked.UseWriteLocked(out var obj))
        {
            obj.Value = 42;
        }
        Assert.Equal(42, locked.GetUnlocked().Value);
    }

    [Fact]
    public void UseReadLocked_AllowsReadAccess()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 99 });
        using (locked.UseReadLocked(out var obj))
        {
            Assert.Equal(99, obj.Value);
        }
    }

    [Fact]
    public void TryUseWriteLocked_ReturnsFalseIfAlreadyLocked()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        using (locked.UseWriteLocked(out var obj))
        {
            var result = locked.TryUseWriteLocked(out var handle, out var obj2);
            Assert.False(result);
            Assert.Null(obj2);
        }
    }

    [Fact]
    public void TryUseReadLocked_ReturnsTrueIfNotLocked()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        var result = locked.TryUseReadLocked(out var handle, out var obj);
        Assert.True(result);
        Assert.NotNull(obj);
        handle.Dispose();
    }

    [Fact]
    public async Task UseWriteLockedAsync_ProvidesExclusiveAccess()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        var (handle, obj) = await locked.UseWriteLockedAsync();
        obj.Value = 123;
        handle.Dispose();
        Assert.Equal(123, locked.GetUnlocked().Value);
    }

    [Fact]
    public async Task UseReadLockedAsync_ProvidesReadAccess()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 55 });
        var (handle, obj) = await locked.UseReadLockedAsync();
        Assert.Equal(55, obj.Value);
        handle.Dispose();
    }

    [Fact]
    public async Task UseLockedAsync_ExecutesDelegate()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        await locked.UseLockedAsync(obj =>
        {
            obj.Value = 77;
            return Task.CompletedTask;
        });
        Assert.Equal(77, locked.GetUnlocked().Value);
    }

    [Fact]
    public void UseLocked_ExecutesDelegate()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        locked.UseLocked(obj => obj.Value = 88);
        Assert.Equal(88, locked.GetUnlocked().Value);
    }

    [Fact]
    public void Set_ChangesValue()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        locked.Set(new TestClass { Value = 999 });
        Assert.Equal(999, locked.GetUnlocked().Value);
    }

    [Fact]
    public void IsLocked_ReflectsLockState()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        Assert.False(locked.IsLocked);
        var handle = locked.UseWriteLocked(out var obj);
        Assert.True(locked.IsLocked);
        handle.Dispose();
        Assert.False(locked.IsLocked);
    }

    [Fact]
    public void UseReentrantWriteLocked_AllowsReentrancy()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        locked.UseReentrantWriteLocked(obj =>
        {
            obj.Value = 111;
            locked.UseReentrantWriteLocked(obj2 =>
            {
                obj2.Value = 222;
            });
        });
        Assert.Equal(222, locked.GetUnlocked().Value);
    }

    [Fact]
    public async Task UseReentrantWriteLockedAsync_AllowsReentrancyAsync()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        await locked.UseReentrantWriteLockedAsync(async obj =>
        {
            obj.Value = 444;
            await locked.UseReentrantWriteLockedAsync(obj2 =>
            {
                obj2.Value = 333;
                return Task.CompletedTask;
            });
        });
        Assert.Equal(333, locked.GetUnlocked().Value);
    }

    [Fact]
    public void UseReadLocked_DelegateVariant()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 10 });
        int result = locked.UseReadLocked(obj => obj.Value + 5);
        Assert.Equal(15, result);
    }

    [Fact]
    public void UseLocked_DelegateVariant()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 20 });
        int result = locked.UseLocked(obj => obj.Value * 2);
        Assert.Equal(40, result);
    }

    [Fact]
    public void UseReentrantReadLocked_DelegateVariant()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 30 });
        int result = locked.UseReentrantReadLocked(obj =>
        {
            return obj.Value + 1;
        });
        Assert.Equal(31, result);
    }

    [Fact]
    public void UseReentrantWriteLocked_DelegateVariant()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 40 });
        int result = locked.UseReentrantWriteLocked(obj =>
        {
            obj.Value += 2;
            return obj.Value;
        });
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task UseLockedAsync_DelegateVariant()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 50 });
        int result = await locked.UseLockedAsync(obj => Task.FromResult(obj.Value + 3));
        Assert.Equal(53, result);
    }

    [Fact]
    public async Task UseReadLockedAsync_DelegateVariant()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 60 });
        int result = await locked.UseReadLockedAsync(obj => Task.FromResult(obj.Value + 4));
        Assert.Equal(64, result);
    }

    [Fact]
    public async Task UseReentrantReadLockedAsync_DelegateVariant()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 70 });
        int result = await locked.UseReentrantReadLockedAsync(obj => Task.FromResult(obj.Value + 5));
        Assert.Equal(75, result);
    }

    [Fact]
    public async Task UseReentrantWriteLockedAsync_DelegateVariant()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 80 });
        int result = await locked.UseReentrantWriteLockedAsync(obj =>
        {
            obj.Value += 6;
            return Task.FromResult(obj.Value);
        });
        Assert.Equal(86, result);
    }

    [Fact]
    public void TryUseWriteLocked_Timeout_Success()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        bool result = locked.TryUseWriteLocked(TimeSpan.FromMilliseconds(100), out var handle, out var obj);
        Assert.True(result);
        obj.Value = 101;
        handle.Dispose();
        Assert.Equal(101, locked.GetUnlocked().Value);
    }

    [Fact]
    public void TryUseReadLocked_Timeout_Success()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 102 });
        bool result = locked.TryUseReadLocked(TimeSpan.FromMilliseconds(100), out var handle, out var obj);
        Assert.True(result);
        Assert.Equal(102, obj.Value);
        handle.Dispose();
    }

    [Fact]
    public async Task TryUseWriteLockedAsync_Timeout_Success()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass());
        var (success, handle, obj) = await locked.TryUseWriteLockedAsync(TimeSpan.FromMilliseconds(100));
        Assert.True(success);
        obj.Value = 103;
        handle.Dispose();
        Assert.Equal(103, locked.GetUnlocked().Value);
    }

    [Fact]
    public async Task TryUseReadLockedAsync_Timeout_Success()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 104 });
        var (success, handle, obj) = await locked.TryUseReadLockedAsync(TimeSpan.FromMilliseconds(100));
        Assert.True(success);
        Assert.Equal(104, obj.Value);
        handle.Dispose();
    }

    [Fact]
    public void TryUseLocked_Delegate_Immediate()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 105 });
        bool result = locked.TryUseLocked(obj => obj.Value = 106);
        Assert.True(result);
        Assert.Equal(106, locked.GetUnlocked().Value);
    }

    [Fact]
    public void TryUseLocked_Delegate_Timeout()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 107 });
        bool result = locked.TryUseLocked(obj => obj.Value = 108, TimeSpan.FromMilliseconds(100));
        Assert.True(result);
        Assert.Equal(108, locked.GetUnlocked().Value);
    }

    [Fact]
    public async Task TryUseLockedAsync_Delegate_Timeout()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 109 });
        bool result = await locked.TryUseLockedAsync(obj =>
        {
            obj.Value = 110;
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(100));
        Assert.True(result);
        Assert.Equal(110, locked.GetUnlocked().Value);
    }

    [Fact]
    public void TryUseReadLocked_Delegate_Immediate()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 111 });
        bool result = locked.TryUseReadLocked(obj => { var v = obj.Value; });
        Assert.True(result);
    }

    [Fact]
    public void TryUseReadLocked_Delegate_Timeout()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 112 });
        bool result = locked.TryUseReadLocked(obj => { var v = obj.Value; }, TimeSpan.FromMilliseconds(100));
        Assert.True(result);
    }

    [Fact]
    public async Task TryUseReadLockedAsync_Delegate_Timeout()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 113 });
        bool result = await locked.TryUseReadLockedAsync(obj =>
        {
            var v = obj.Value;
            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(100));
        Assert.True(result);
    }

    [Fact]
    public void TryUseLocked_Delegate_WithResult()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 114 });
        bool result = locked.TryUseLocked(obj => obj.Value + 1, out int res);
        Assert.True(result);
        Assert.Equal(115, res);
    }

    [Fact]
    public void TryUseLocked_Delegate_WithResult_Timeout()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 116 });
        bool result = locked.TryUseLocked(obj => obj.Value + 2, TimeSpan.FromMilliseconds(100), out int res);
        Assert.True(result);
        Assert.Equal(118, res);
    }

    [Fact]
    public async Task TryUseLockedAsync_Delegate_WithResult_Timeout()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 119 });
        var (success, res) = await locked.TryUseLockedAsync(obj => Task.FromResult(obj.Value + 3), TimeSpan.FromMilliseconds(100));
        Assert.True(success);
        Assert.Equal(122, res);
    }

    [Fact]
    public void TryUseReadLocked_Delegate_WithResult()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 123 });
        bool result = locked.TryUseReadLocked(obj => obj.Value + 4, out int res);
        Assert.True(result);
        Assert.Equal(127, res);
    }

    [Fact]
    public void TryUseReadLocked_Delegate_WithResult_Timeout()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 128 });
        bool result = locked.TryUseReadLocked(obj => obj.Value + 5, TimeSpan.FromMilliseconds(100), out int res);
        Assert.True(result);
        Assert.Equal(133, res);
    }

    [Fact]
    public async Task TryUseReadLockedAsync_Delegate_WithResult_Timeout()
    {
        var locked = new FeatureLocked<TestClass>(new TestClass { Value = 134 });
        var (success, res) = await locked.TryUseReadLockedAsync(obj => Task.FromResult(obj.Value + 6), TimeSpan.FromMilliseconds(100));
        Assert.True(success);
        Assert.Equal(140, res);
    }
}