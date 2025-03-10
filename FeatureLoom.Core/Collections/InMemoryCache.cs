﻿using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using FeatureLoom.Extensions;
using System.Data;

namespace FeatureLoom.Collections
{
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
        FeatureLock storageLock = new FeatureLock();
        long totalSize = 0;
        volatile bool cleanUpActive = false;
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

        public bool Contains(K key)
        {
            using (storageLock.Lock())
            {
                return storage.ContainsKey(key);
            }
        }

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

        public bool TryGet(K key, out V value)
        {
            using(storageLock.Lock())
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

        public bool TryGetInfo(K key, out CacheItemInfo info)
        {
            using (storageLock.Lock())
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

        public void StartCleanUp()
        {
            if (cleanUpActive) return;
            else cleanUpActive = true;
            Task.Run(() =>
            {
                DateTime now = AppTime.CoarseNow;
                lastCleanUp = now;

                IEnumerable<CacheItem> items;
                using (storageLock.Lock())
                {
                    items = storage.Values;
                }
                
                var orderedItems = items.OrderBy(item =>
                {
                    if ((now - item.lastAccessTime).TotalSeconds > settings.maxUnusedTimeInSeconds) return 0;
                    else
                    {
                        double rating = 1000;
                        rating *= item.accessCount;
                        rating /= (double)(now - item.creationTime).TotalSeconds;
                        rating /= (double)(now - item.lastAccessTime).TotalSeconds;
                        rating *= (double)settings.targetCacheSizeInByte / item.size;
                        rating *= item.priorityFactor;
                        return rating;
                    }
                });

                foreach(var item in orderedItems)
                {
                    if (totalSize > settings.targetCacheSizeInByte ||
                        (now - item.lastAccessTime).TotalSeconds > settings.maxUnusedTimeInSeconds)
                    {
                        Remove(item.key);
                    }
                    else break;
                }

                cleanUpActive = false;
            });
        }

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
}
