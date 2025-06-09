using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using FeatureLoom.Extensions;
using System.Data;

namespace FeatureLoom.Collections;

/// <summary>
/// Thread-safe, in-memory cache with size-based, time-based, and usage-based eviction.
/// 
/// This cache stores key-value pairs and automatically evicts items based on a hybrid policy
/// that considers least-recently-used (LRU), least-frequently-used (LFU), item size, and a user-defined priority factor.
/// Items that have not been accessed for a configurable maximum time are always evicted first.
/// The cache supports concurrent access with efficient read/write locking and periodic or on-demand cleanup.
/// 
/// Eviction is triggered when the total cache size exceeds the configured target plus margin, or on schedule.
/// The eviction rating formula is tunable and combines access frequency, recency, item size, and priority.
/// </summary>
public sealed class InMemoryCache<K,V> : ISchedule
{
    public readonly struct CacheItemInfo
    {
        public readonly K key;
        public readonly V value;

        public readonly int size;
        public readonly DateTime creationTime;
        public readonly DateTime lastAccessTime;
        public readonly int accessCount;
        public readonly float priorityFactor;

        public CacheItemInfo(K key, V value, int size, DateTime creationTime, DateTime lastAccessTime, int accessCount, float priorityFactor)
        {
            this.key = key;
            this.value = value;
            this.size = size;
            this.creationTime = creationTime;
            this.lastAccessTime = lastAccessTime;
            this.accessCount = accessCount;
            this.priorityFactor = priorityFactor;
        }
    }

    class CacheItem
    {
        public K key;
        public V value;

        public int size;
        public DateTime creationTime;
        public DateTime lastAccessTime;
        public int accessCount;
        public float priorityFactor;

        public CacheItemInfo Info => new CacheItemInfo(            
            key,
            value,
            size,
            creationTime,
            lastAccessTime,
            accessCount,
            priorityFactor
        );
    }

    public class CacheSettings
    {
        public int targetCacheSizeInByte = 1024 * 1024;
        public int cacheSizeMarginInByte = (1024 * 1024) / 2;
        public int maxUnusedTimeInSeconds = 60 * 60 * 24;
        public int cleanUpPeriodeInSeconds = 60;
    }

    CacheSettings settings;
    Func<V, int> calculateSize;
    Dictionary<K, CacheItem> storage = new Dictionary<K, CacheItem>();
    MicroLock storageLock = new MicroLock();
    long totalSize = 0;
    MicroLock cleanUpLock = new MicroLock();
    DateTime lastCleanUp = AppTime.CoarseNow;
    TimeSpan cleanUpDelay = TimeSpan.Zero;

    public CacheSettings Settings => settings;

    public string Name => "InMemoryCache";

    public InMemoryCache(Func<V, int> calculateSize, CacheSettings settings = null)
    {
        this.calculateSize = calculateSize;
        this.settings = settings ?? new CacheSettings();
        cleanUpDelay = this.settings.cleanUpPeriodeInSeconds.Seconds();
    }

    /// <summary>
    /// Checks if the cache contains an item with the specified key.
    /// </summary>
    /// <param name="key">The key to check for existence.</param>
    /// <returns>True if the key exists in the cache; otherwise, false.</returns>
    public bool Contains(K key)
    {
        using (storageLock.LockReadOnly())
        {
            return storage.ContainsKey(key);
        }
    }

    /// <summary>
    /// Adds a new item to the cache or updates an existing one.
    /// Triggers cleanup if the cache size exceeds the configured target plus margin.
    /// </summary>
    /// <param name="key">The key of the item to add or update.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="priorityFactor">A user-defined factor to influence eviction priority (default: 1).</param>
    public void Add(K key, V value, float priorityFactor = 1)
    {
        int newSize = calculateSize(value);

        using (storageLock.Lock())
        {
            DateTime now = AppTime.CoarseNow;

            CacheItem item = storage.GetOrCreate(key, init => new CacheItem()
            {
                key = init.key,
                value = init.value,
                size = init.newSize,
                creationTime = init.now,
                lastAccessTime = init.now,
                accessCount = 0,
                priorityFactor = init.priorityFactor
            }, (key, value, newSize, now, priorityFactor), out bool existed);

            if (!existed)
            {                                        
                totalSize += item.size;
            }
            else
            {
                int oldSize = item.size;                    

                item.value = value;
                item.size = newSize;
                item.lastAccessTime = now;
                item.priorityFactor = priorityFactor;

                totalSize += newSize - oldSize;
            }
        }

        if (totalSize > settings.targetCacheSizeInByte + settings.cacheSizeMarginInByte) StartCleanUp();
    }

    /// <summary>
    /// Removes the item with the specified key from the cache, if it exists.
    /// </summary>
    /// <param name="key">The key of the item to remove.</param>
    public void Remove(K key)
    {
        using(storageLock.Lock())
        {
            if (storage.TryGetValue(key, out CacheItem item))
            {
                storage.Remove(key);
                totalSize -= item.size;
            }
        }
    }

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key.
    /// Updates the item's last access time and access count if found.
    /// </summary>
    /// <param name="key">The key of the item to retrieve.</param>
    /// <param name="value">The value associated with the key, if found.</param>
    /// <returns>True if the item was found; otherwise, false.</returns>
    public bool TryGet(K key, out V value)
    {
        using(storageLock.LockReadOnly())
        {
            if (storage.TryGetValue(key, out var item))
            {
                item.lastAccessTime = AppTime.CoarseNow;
                item.accessCount++;
                value = item.value;
                return true;
            }
            else
            {
                value = default;
                return false;
            }                
        }
    }

    /// <summary>
    /// Attempts to retrieve detailed information about the item with the specified key.
    /// Updates the item's last access time and access count if found.
    /// </summary>
    /// <param name="key">The key of the item to retrieve info for.</param>
    /// <param name="info">The detailed info about the item, if found.</param>
    /// <returns>True if the item was found; otherwise, false.</returns>
    public bool TryGetInfo(K key, out CacheItemInfo info)
    {
        using (storageLock.LockReadOnly())
        {
            if (storage.TryGetValue(key, out var item))
            {
                item.lastAccessTime = AppTime.CoarseNow;
                item.accessCount++;
                info = item.Info;
                return true;
            }
            else
            {
                info = default;
                return false;
            }
        }
    }

    /// <summary>
    /// Starts the cleanup process to evict items from the cache based on the eviction policy.
    /// Only one cleanup can run at a time.
    /// </summary>
    public void StartCleanUp()
    {
        if (!cleanUpLock.TryLock(out var lockHandle)) return;
         
        Task.Run(() =>
        {
            using (lockHandle)
            {
                DateTime now = AppTime.CoarseNow;
                lastCleanUp = now;

                IEnumerable<CacheItem> items;
                using (storageLock.LockReadOnly())
                {
                    items = storage.Values;
                }

                var orderedItems = items.OrderBy(item =>
                {
                    DateTime now = AppTime.CoarseNow;
                    if ((now - item.lastAccessTime).TotalSeconds > settings.maxUnusedTimeInSeconds)
                        return double.MinValue; // Expired: evict first

                    const double ACCESS_WEIGHT = 0.4;
                    const double RECENCY_WEIGHT = 0.4;
                    const double SIZE_WEIGHT = 0.1;
                    const double PRIORITY_WEIGHT = 0.1;
                    const double EPSILON = 1.0;

                    double normalizedAccess = Math.Log(item.accessCount + 1);
                    double normalizedRecency = 1.0 / ((now - item.lastAccessTime).TotalSeconds + EPSILON);
                    double normalizedSize = 1.0 / (item.size + EPSILON);
                    double normalizedPriority = item.priorityFactor;

                    double rating =
                        ACCESS_WEIGHT * normalizedAccess +
                        RECENCY_WEIGHT * normalizedRecency +
                        SIZE_WEIGHT * normalizedSize +
                        PRIORITY_WEIGHT * normalizedPriority;

                    return rating;
                });

                foreach (var item in orderedItems)
                {
                    if (totalSize > settings.targetCacheSizeInByte ||
                        (now - item.lastAccessTime).TotalSeconds > settings.maxUnusedTimeInSeconds)
                    {
                        Remove(item.key);
                    }
                    else break;
                }
            }
        });
    }

    /// <summary>
    /// Called by the scheduler to trigger periodic cleanup based on the configured schedule.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>The next scheduled time frame for execution.</returns>
    ScheduleStatus ISchedule.Trigger(DateTime now)
    {
        TimeSpan remaining = now - (lastCleanUp + settings.cleanUpPeriodeInSeconds.Seconds());
        if (remaining < 1.Seconds())
        {
            StartCleanUp();
            return new TimeFrame(now, cleanUpDelay);                
        }
        return new TimeFrame(now, remaining);
    }
}
