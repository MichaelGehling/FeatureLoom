using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace FeatureLoom.Collections
{
    public class InMemoryCache<K,V> : ISchedule
    {
        class CacheItem
        {
            public K key;
            public V value;

            public int size;
            public DateTime creationTime;
            public DateTime lastAccessTime;
            public int accessCount;
            public float priorityFactor;
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

        public InMemoryCache(Func<V, int> calculateSize, CacheSettings settings = null)
        {
            this.calculateSize = calculateSize;
            this.settings = settings ?? new CacheSettings();
            cleanUpDelay = this.settings.cleanUpPeriodeInSeconds.Seconds();
        }

        public bool Contains(K key)
        {
            using (storageLock.Lock()) return storage.ContainsKey(key);
        }

        public void Add(K key, V value, float priorityFactor = 1)
        {
            int newSize = calculateSize(value);

            using (storageLock.Lock())
            {
                DateTime now = AppTime.CoarseNow;
                if (!storage.TryGetValue(key, out CacheItem item))
                {                    
                    item = new CacheItem()
                    {
                        key = key,
                        value = value,
                        size = newSize,
                        creationTime = now,
                        lastAccessTime = now,
                        accessCount = 0,
                        priorityFactor = priorityFactor
                    };
                    storage.Add(key, item);
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

        public void StartCleanUp()
        {
            if (cleanUpActive) return;
            else cleanUpActive = true;
            Task.Run(() =>
            {
                ICollection<CacheItem> items;
                using (storageLock.Lock())
                {
                    items = storage.Values;
                }

                DateTime now = AppTime.CoarseNow;
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

                lastCleanUp = now;
                cleanUpActive = false;
            });
        }

        bool ISchedule.IsActive => true;
        TimeSpan ISchedule.MaxDelay => cleanUpDelay;        

        void ISchedule.Handle(DateTime now)
        {
            if (now > lastCleanUp + settings.cleanUpPeriodeInSeconds.Seconds())
            {
                StartCleanUp();
            }
        }
    }
}
