using FeatureLoom.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace FeatureLoom.Collections
{
    public class QuickStringCacheTests
    {
        [Fact]
        public void Constructor_ClampsHashSizeInBits()
        {
            var small = new QuickStringCache(1, 1024);
            var large = new QuickStringCache(99, 1024);

            var cacheField = typeof(QuickStringCache).GetField("cache", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(cacheField);

            var smallArray = (Array)cacheField.GetValue(small);
            var largeArray = (Array)cacheField.GetValue(large);

            Assert.Equal(1 << 8, smallArray.Length);   // min clamp
            Assert.Equal(1 << 16, largeArray.Length);  // max clamp
        }

        [Fact]
        public void GetOrCreate_ReturnsSameReference_ForRepeatedLookup()
        {
            var cache = new QuickStringCache(hashSizeInBits: 8, 1024);
            ByteSegment key = new ByteSegment("hello");

            string first = cache.GetOrCreate(key);
            string second = cache.GetOrCreate(key);

            Assert.Same(first, second);
        }

        [Fact]
        public void GetOrCreate_ReturnsSameReference_ForEqualContentDifferentBacking()
        {
            var cache = new QuickStringCache(hashSizeInBits: 8, 1024);

            ByteSegment key1 = new ByteSegment("abc123");
            ByteSegment key2 = new ByteSegment("abc123"); // different backing array, same content

            string first = cache.GetOrCreate(key1);
            string second = cache.GetOrCreate(key2);

            Assert.Same(first, second);
        }

        [Fact]
        public void GetOrCreate_DoesNotCache_WhenSegmentExceedsMaxStringLength()
        {
            var cache = new QuickStringCache(hashSizeInBits: 8, maxStringLength: 4);
            ByteSegment longKey = new ByteSegment("this_is_longer_than_4");

            _ = cache.GetOrCreate(longKey);
            _ = cache.GetOrCreate(longKey);

            Assert.Equal(0u, GetStampCounter(cache));
            Assert.Equal(0, CountValidEntries(cache));
        }

        [Fact]
        public void Clear_ResetsCacheAndStampCounter()
        {
            var cache = new QuickStringCache(hashSizeInBits: 8, maxStringLength: 1024);

            _ = cache.GetOrCreate(new ByteSegment("a"));
            _ = cache.GetOrCreate(new ByteSegment("b"));

            Assert.True(GetStampCounter(cache) > 0);
            Assert.True(CountValidEntries(cache) > 0);

            cache.Clear();

            Assert.Equal(0u, GetStampCounter(cache));
            Assert.Equal(0, CountValidEntries(cache));
        }

        [Fact]
        public void GetOrCreate_UsesSecondProbeSlot_ForCollision()
        {
            var cache = new QuickStringCache(hashSizeInBits: 8, maxStringLength: 1024);
            var (a, b) = FindCollidingKeyPair(cache);

            string a1 = cache.GetOrCreate(a);
            string b1 = cache.GetOrCreate(b);

            string a2 = cache.GetOrCreate(a);
            string b2 = cache.GetOrCreate(b);

            Assert.Same(a1, a2);
            Assert.Same(b1, b2);
        }

        static List<ByteSegment> FindCollidingKeys(QuickStringCache cache, int needed)
        {
            var list = new List<ByteSegment>();
            int? targetIndex = null;

            for (int i = 0; i < 200_000; i++)
            {
                var candidate = new ByteSegment("k_" + i);
                int idx = GetPrimaryIndex(cache, candidate);

                if (targetIndex == null)
                {
                    targetIndex = idx;
                    list.Add(candidate);
                }
                else if (idx == targetIndex.Value && !list.Any(x => x.Equals(candidate)))
                {
                    list.Add(candidate);
                    if (list.Count == needed) return list;
                }
            }

            throw new InvalidOperationException("Could not find enough colliding keys.");
        }

        static (ByteSegment, ByteSegment) FindCollidingKeyPair(QuickStringCache cache)
        {
            var list = FindCollidingKeys(cache, 2);
            return (list[0], list[1]);
        }

        static int GetPrimaryIndex(QuickStringCache cache, ByteSegment segment)
        {
            int hash = InvokeComputeFastHash(segment);
            var cacheArray = (Array)GetField(cache, "cache");
            int mask = cacheArray.Length - 1;
            return hash & mask;
        }

        static int InvokeComputeFastHash(ByteSegment segment)
        {
            var method = typeof(QuickStringCache).GetMethod("ComputeFastHash", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            return (int)method.Invoke(null, new object[] { segment });
        }

        static uint GetStampCounter(QuickStringCache cache) => (uint)GetField(cache, "stampCounter");

        static int CountValidEntries(QuickStringCache cache)
        {
            var cacheArray = (Array)GetField(cache, "cache");
            var entryType = cacheArray.GetType().GetElementType();
            var keyField = entryType.GetField("key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            int count = 0;
            foreach (var entry in cacheArray)
            {
                var key = (ByteSegment)keyField.GetValue(entry);
                if (key.IsValid) count++;
            }

            return count;
        }

        static object GetField(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field.GetValue(instance);
        }
    }
}