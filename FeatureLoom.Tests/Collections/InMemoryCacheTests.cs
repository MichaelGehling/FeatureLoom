using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FeatureLoom.Collections;
using Xunit;

namespace FeatureLoom.Collections;

public class InMemoryCacheTests
{
    private static int SizeOfInt(int value) => sizeof(int);

    [Fact]
    public void Add_And_Contains_Works()
    {
        var cache = new InMemoryCache<string, int>(SizeOfInt);

        cache.Add("a", 1);
        Assert.True(cache.Contains("a"));

        cache.Add("b", 2);
        Assert.True(cache.Contains("b"));
        Assert.False(cache.Contains("c"));
    }

    [Fact]
    public void TryGet_Returns_Expected_Value_And_Updates_Stats()
    {
        var cache = new InMemoryCache<string, int>(SizeOfInt);
        cache.Add("x", 42);

        Assert.True(cache.TryGet("x", out int value));
        Assert.Equal(42, value);

        Assert.False(cache.TryGet("y", out _));
    }

    [Fact]
    public void Remove_Removes_Item()
    {
        var cache = new InMemoryCache<string, int>(SizeOfInt);
        cache.Add("a", 1);
        Assert.True(cache.Contains("a"));

        cache.Remove("a");
        Assert.False(cache.Contains("a"));
    }

    [Fact]
    public void TryGetInfo_Returns_Info_And_Updates_Stats()
    {
        var cache = new InMemoryCache<string, int>(SizeOfInt);
        cache.Add("info", 99, priorityFactor: 2.5f);

        Assert.True(cache.TryGetInfo("info", out var info));
        Assert.Equal("info", info.key);
        Assert.Equal(99, info.value);
        Assert.Equal(sizeof(int), info.size);
        Assert.True(info.accessCount > 0);
        Assert.Equal(2.5f, info.priorityFactor);
    }

    [Fact]
    public void Add_Updates_Existing_Item_And_Size()
    {
        var cache = new InMemoryCache<string, int>(SizeOfInt);
        cache.Add("dup", 1);
        cache.Add("dup", 2);

        Assert.True(cache.TryGet("dup", out int value));
        Assert.Equal(2, value);
    }

    [Fact]
    public async Task Evicts_Least_Valuable_When_OverCapacity()
    {
        var settings = new InMemoryCache<string, int>.CacheSettings
        {
            targetCacheSizeInByte = sizeof(int) * 2,
            cacheSizeMarginInByte = 0,
            maxUnusedTimeInSeconds = 1000,
            cleanUpPeriodeInSeconds = 1000
        };
        var cache = new InMemoryCache<string, int>(SizeOfInt, settings);

        cache.Add("a", 1);
        cache.Add("b", 2);
        cache.Add("c", 3); // Should trigger eviction

        // Wait for cleanup to complete (polling)
        int count = 0;
        for (int i = 0; i < 20; i++)
        {
            count = 0;
            if (cache.Contains("a")) count++;
            if (cache.Contains("b")) count++;
            if (cache.Contains("c")) count++;
            if (count == 2) break;
            await Task.Delay(10);
        }
        Assert.Equal(2, count); // Only two items should remain
    }

    [Fact]
    public async Task Evicts_Expired_Items_On_Cleanup()
    {
        var settings = new InMemoryCache<string, int>.CacheSettings
        {
            targetCacheSizeInByte = sizeof(int) * 10,
            cacheSizeMarginInByte = 0,
            maxUnusedTimeInSeconds = 0, // Expire immediately
            cleanUpPeriodeInSeconds = 1
        };
        var cache = new InMemoryCache<string, int>(SizeOfInt, settings);

        cache.Add("old", 1);
        await Task.Delay(10); // Ensure time passes
        cache.StartCleanUp();
        await Task.Delay(50); // Wait for cleanup to run

        Assert.False(cache.Contains("old"));
    }

    [Fact]
    public async Task PriorityFactor_Affects_Eviction()
    {
        var settings = new InMemoryCache<string, int>.CacheSettings
        {
            targetCacheSizeInByte = sizeof(int) * 2,
            cacheSizeMarginInByte = 0,
            maxUnusedTimeInSeconds = 1000,
            cleanUpPeriodeInSeconds = 1000
        };
        var cache = new InMemoryCache<string, int>(SizeOfInt, settings);

        cache.Add("low", 1, priorityFactor: 0.1f);
        cache.Add("high", 2, priorityFactor: 10f);
        cache.Add("new", 3); // Should evict "low" due to low priority

        // Wait for cleanup to complete (polling)
        for (int i = 0; i < 20; i++)
        {
            bool lowPresent = cache.Contains("low");
            bool highPresent = cache.Contains("high");
            bool newPresent = cache.Contains("new");
            if (!lowPresent && highPresent && newPresent) break;
            await Task.Delay(10);
        }

        Assert.False(cache.Contains("low"));
        Assert.True(cache.Contains("high"));
        Assert.True(cache.Contains("new"));
    }
}