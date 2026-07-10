using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Collections
{
    public class StringInternCacheTests
    {
        [Fact]
        public void Constructor_ClampsHashSizeInBits()
        {
            var small = new StringInternCache(1, 1024);
            var large = new StringInternCache(99, 1024);

            var cacheField = typeof(StringInternCache).GetField("cache", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(cacheField);

            var smallArray = (Array)cacheField.GetValue(small);
            var largeArray = (Array)cacheField.GetValue(large);

            Assert.Equal(1 << 8, smallArray.Length);   // min clamp
            Assert.Equal(1 << 16, largeArray.Length);  // max clamp
        }

        [Fact]
        public void Intern_Null_ReturnsNull()
        {
            var cache = new StringInternCache(8, 1024);
            Assert.Null(cache.Intern((string)null));
        }

        [Fact]
        public void Intern_Empty_ReturnsStringEmpty()
        {
            var cache = new StringInternCache(8, 1024);
            Assert.Same(string.Empty, cache.Intern(""));
        }

        [Fact]
        public void Intern_ReturnsSameReference_ForRepeatedLookup()
        {
            var cache = new StringInternCache(hashSizeInBits: 8, 1024);

            string first = cache.Intern("hello");
            string second = cache.Intern("hello");

            Assert.Same(first, second);
        }

        [Fact]
        public void Intern_ReturnsSameReference_ForEqualContentDifferentBacking()
        {
            var cache = new StringInternCache(hashSizeInBits: 8, 1024);

            // Two distinct instances with equal content.
            string s1 = new string("abc123".ToCharArray());
            string s2 = new string("abc123".ToCharArray());
            Assert.NotSame(s1, s2);

            string first = cache.Intern(s1);
            string second = cache.Intern(s2);

            Assert.Same(first, second);
            Assert.Same(s1, second); // first stored instance is returned
        }

        [Fact]
        public void Intern_FirstStoredInstanceIsReturned()
        {
            var cache = new StringInternCache(hashSizeInBits: 8, 1024);

            string original = new string("payload".ToCharArray());
            string stored = cache.Intern(original);
            Assert.Same(original, stored);

            string duplicate = new string("payload".ToCharArray());
            string result = cache.Intern(duplicate);

            Assert.Same(original, result);
            Assert.NotSame(duplicate, result);
        }

        [Fact]
        public void Intern_DoesNotCache_WhenStringExceedsMaxStringLength()
        {
            var cache = new StringInternCache(hashSizeInBits: 8, maxStringLength: 4);
            string longValue = "this_is_longer_than_4";

            string a = cache.Intern(longValue);
            string b = cache.Intern(longValue);

            Assert.Same(longValue, a); // returned as-is
            Assert.Same(longValue, b);
            Assert.Equal(0u, GetStampCounter(cache));
            Assert.Equal(0, CountValidEntries(cache));
        }

#if !NETSTANDARD2_0
        [Fact]
        public void Intern_Span_ReturnsCachedInstance_WithoutAllocatingOnHit()
        {
            var cache = new StringInternCache(hashSizeInBits: 8, 1024);

            string stored = cache.Intern("spanned");

            ReadOnlySpan<char> span = "spanned".AsSpan();
            string result = cache.Intern(span);

            Assert.Same(stored, result);
        }

        [Fact]
        public void Intern_Span_Empty_ReturnsStringEmpty()
        {
            var cache = new StringInternCache(hashSizeInBits: 8, 1024);
            Assert.Same(string.Empty, cache.Intern(ReadOnlySpan<char>.Empty));
        }

        [Fact]
        public void Intern_Span_DoesNotCache_WhenExceedsMaxStringLength()
        {
            var cache = new StringInternCache(hashSizeInBits: 8, maxStringLength: 4);

            ReadOnlySpan<char> span = "this_is_longer_than_4".AsSpan();
            string result = cache.Intern(span);

            Assert.Equal("this_is_longer_than_4", result);
            Assert.Equal(0u, GetStampCounter(cache));
            Assert.Equal(0, CountValidEntries(cache));
        }
#endif

        [Fact]
        public void Clear_ResetsCacheAndStampCounter()
        {
            var cache = new StringInternCache(hashSizeInBits: 8, maxStringLength: 1024);

            _ = cache.Intern("a");
            _ = cache.Intern("b");

            Assert.True(GetStampCounter(cache) > 0);
            Assert.True(CountValidEntries(cache) > 0);

            cache.Clear();

            Assert.Equal(0u, GetStampCounter(cache));
            Assert.Equal(0, CountValidEntries(cache));
        }

        [Fact]
        public void Intern_UsesSecondProbeSlot_ForCollision()
        {
            var cache = new StringInternCache(hashSizeInBits: 8, maxStringLength: 1024);
            var (a, b) = FindCollidingKeyPair(cache);

            string a1 = cache.Intern(a);
            string b1 = cache.Intern(b);

            string a2 = cache.Intern(a);
            string b2 = cache.Intern(b);

            Assert.Same(a1, a2);
            Assert.Same(b1, b2);
        }

        [Fact]
        public void Shared_ReturnsSameInstance_AndIsLazilyCreated()
        {
            var previous = StringInternCache.Shared;
            try
            {
                StringInternCache.Shared = null;

                var first = StringInternCache.Shared;
                var second = StringInternCache.Shared;

                Assert.NotNull(first);
                Assert.Same(first, second);
            }
            finally
            {
                StringInternCache.Shared = previous;
            }
        }

        [Fact]
        public void Shared_CanBeReplacedWithCustomInstance()
        {
            var previous = StringInternCache.Shared;
            try
            {
                var custom = new StringInternCache(8, 16);
                StringInternCache.Shared = custom;

                Assert.Same(custom, StringInternCache.Shared);
            }
            finally
            {
                StringInternCache.Shared = previous;
            }
        }

        [Fact]
        public void Intern_IsThreadSafe_AndNeverReturnsWrongResult()
        {
            var cache = new StringInternCache(hashSizeInBits: 10, maxStringLength: 128);
            var values = Enumerable.Range(0, 500).Select(i => "value_" + i).ToArray();

            Parallel.For(0, 16, _ =>
            {
                for (int round = 0; round < 200; round++)
                {
                    foreach (var v in values)
                    {
                        string result = cache.Intern(new string(v.ToCharArray()));
                        Assert.Equal(v, result);
                    }
                }
            });
        }

        static List<string> FindCollidingKeys(StringInternCache cache, int needed)
        {
            var list = new List<string>();
            int? targetIndex = null;

            for (int i = 0; i < 200_000; i++)
            {
                var candidate = "k_" + i;
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

        static (string, string) FindCollidingKeyPair(StringInternCache cache)
        {
            var list = FindCollidingKeys(cache, 2);
            return (list[0], list[1]);
        }

        static int GetPrimaryIndex(StringInternCache cache, string value)
        {
            int hash = InvokeComputeFastHash(value);
            var cacheArray = (Array)GetField(cache, "cache");
            int mask = cacheArray.Length - 1;
            return hash & mask;
        }

        static int InvokeComputeFastHash(string value)
        {
            var method = typeof(StringInternCache).GetMethod(
                "ComputeFastHash",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);
            Assert.NotNull(method);
            return (int)method.Invoke(null, new object[] { value });
        }

        static uint GetStampCounter(StringInternCache cache) => (uint)GetField(cache, "stampCounter");

        static int CountValidEntries(StringInternCache cache)
        {
            var cacheArray = (Array)GetField(cache, "cache");
            var entryType = cacheArray.GetType().GetElementType();
            var valueField = entryType.GetField("value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            int count = 0;
            foreach (var entry in cacheArray)
            {
                var value = (string)valueField.GetValue(entry);
                if (value != null) count++;
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
