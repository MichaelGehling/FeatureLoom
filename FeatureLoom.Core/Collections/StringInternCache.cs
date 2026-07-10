using FeatureLoom.Extensions;
using FeatureLoom.Synchronization;
using System;
using System.Runtime.CompilerServices;
#if !NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif

namespace FeatureLoom.Collections;

/// <summary>
/// Very fast, fixed-size string interning cache. Provides a faster and more flexible
/// alternative to <see cref="string.Intern(string)"/>.
/// Uses direct-index probing with 2 adjacent candidate slots and
/// a lightweight age-based replacement policy.
/// </summary>
/// <remarks>
/// Unlike <see cref="string.Intern(string)"/>, which stores strings permanently in a global
/// intern pool that can never be reclaimed, this cache has a fixed capacity and evicts old
/// entries, so it never grows unbounded and can be cleared. It is optimized for throughput and
/// low allocation behavior, not for perfect hit ratio.
/// Capacity is fixed at construction time as <c>2^hashSizeInBits</c>.
/// It also supports deduplicating from a <see cref="ReadOnlySpan{Char}"/> without allocating a
/// new string on a cache hit.
/// <para>
/// Concurrency: the cache is lock-free and safe to use from multiple threads. It never returns a
/// string that is not actually equal to the requested input. This is achieved by reading the
/// slot's <c>value</c> reference into a local exactly once and validating the full comparison
/// against that local, instead of trusting the cached <c>length</c> field (which may be updated
/// independently by a concurrent writer). Reference assignments are atomic in the CLR, so a reader
/// always observes either the old or the new value reference, never a torn one. The only effects of
/// concurrent writers are benign: an occasional missed fast path (treated as a miss), a redundant
/// store into the same slot, or a lost <c>stamp</c> update that slightly perturbs victim selection.
/// None of these affect correctness of the returned strings.
/// </para>
/// <para>
/// Why prefer this over <see cref="string.Intern(string)"/>? Despite the name, this is best
/// understood as a bounded, lock-free string DEDUPLICATION cache rather than a true intern pool,
/// and that gives it a much lower risk profile:
/// </para>
/// <list type="bullet">
/// <item><description>
/// Bounded and reclaimable: <see cref="string.Intern(string)"/> stores strings permanently in a
/// process-wide pool that is never collected, so interning high-cardinality or attacker-controlled
/// data is effectively an unbounded memory leak. This cache has a fixed capacity, evicts old
/// entries and can be <see cref="Clear"/>ed, so its worst case degrades to "no benefit" (extra
/// churn / lower hit ratio) instead of "unbounded leak".
/// </description></item>
/// <item><description>
/// Scoped, not global: it is an ordinary instance (or an opt-in <see cref="Shared"/> instance)
/// with no process-wide side effects, and it is lock-free with no global-table contention.
/// </description></item>
/// <item><description>
/// Saves memory on repeated content: in the many scenarios where the same string is rebuilt over
/// and over (JSON/XML parsing with recurring property names and categorical values, logging,
/// protocol tokens, repeated DB column values, etc.) it collapses duplicate instances into one,
/// reducing heap usage and GC pressure.
/// </description></item>
/// <item><description>
/// Can avoid the allocation entirely: the <see cref="Intern(ReadOnlySpan{char})"/> overload
/// deduplicates directly from a character buffer, so on a cache hit no new string is allocated at
/// all - something <see cref="string.Intern(string)"/> cannot do because it requires an existing
/// <see cref="string"/> instance first.
/// </description></item>
/// </list>
/// <para>
/// IMPORTANT limitation: only VALUE equality of the returned string is guaranteed, not stable
/// reference identity. Because entries can be evicted between calls, two value-equal strings may
/// still coexist as separate instances, and the specific instance returned for a given content may
/// change over time. Therefore this type is NOT a drop-in replacement for
/// <see cref="string.Intern(string)"/> when correctness relies on reference-equality (e.g. using
/// <see cref="object.ReferenceEquals(object, object)"/> as a fast identity check). Use it purely to
/// deduplicate string storage.
/// </para>
/// </remarks>
public sealed class StringInternCache
{

    /// <summary>
    /// Backing field for the shared <see cref="StringInternCache"/> instance.
    /// </summary>
    private static StringInternCache shared;
    private static readonly MicroLock sharedInstanceLock = new MicroLock(); 

    /// <summary>
    /// Gets or sets a shared <see cref="StringInternCache"/> instance that can be used across the application.
    /// If not set, a default instance with 8192 entries and a maximum string length of 128 characters is created on first access.
    /// This may use at most about 2 MB of memory for the cached strings plus overhead.
    /// The shared instance can be replaced with a custom instance if desired, but cached strings from the previous instance will not be carried over.
    /// </summary>
    public static StringInternCache Shared
    {
        get
        {
            if (shared == null)
            {
                using (sharedInstanceLock.Lock())
                {
                    if (shared == null) shared = new StringInternCache(13, 128);
                }
            }
            return shared;
        }
        set
        {
            shared = value;
        }
    }


    /// <summary>
    /// Cache entry containing key/value and metadata for fast lookup/replacement.
    /// </summary>
    struct CacheEntry
    {
        /// <summary>The cached (interned) string value.</summary>
        public string value;

        /// <summary>Monotonic access/update marker used for victim selection.</summary>
        public uint stamp;

        /// <summary>
        /// Cached length of the value for fast comparison.
        /// Avoids accessing <see cref="value"/> in most cases.
        /// </summary>
        public int length;
    }

    readonly CacheEntry[] cache;
    readonly int mask;
    readonly int maxStringLength;
    uint stampCounter;

    /// <summary>
    /// Initializes a new <see cref="StringInternCache"/>.
    /// </summary>
    /// <param name="hashSizeInBits">
    /// Cache size exponent. Effective size is <c>2^hashSizeInBits</c>, e.g. 8 for 256 entries, 10 for 1024 entries, 16 for 65536 entries.
    /// Clamped to range [8..16].
    /// To calculate the maximum size of the cache, consider the average string length and the memory overhead of each entry.
    /// Recommended is 12bit for 4096 entries, which provides a good balance between hit ratio and memory usage for typical scenarios.
    /// That said, the optimal size depends on the specific use case, including the expected number of unique strings, their average length, and the acceptable memory usage.
    /// </param>
    /// <param name="maxStringLength">
    /// Maximum length of strings to cache. Strings longer than this will not be cached and are returned as-is.
    /// </param>
    public StringInternCache(int hashSizeInBits, int maxStringLength)
    {
        hashSizeInBits = hashSizeInBits.Clamp(8, 16);
        cache = new CacheEntry[1 << hashSizeInBits];
        mask = cache.Length - 1;
        this.maxStringLength = maxStringLength;
    }

    /// <summary>
    /// Returns a shared (interned) instance equal to <paramref name="value"/>.
    /// If an equal string is already cached, the cached instance is returned and
    /// <paramref name="value"/> can be garbage collected. Otherwise <paramref name="value"/>
    /// is stored and returned.
    /// </summary>
    /// <param name="value">The string to intern.</param>
    /// <returns>The shared cached instance, or <paramref name="value"/> itself.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Intern(string value)
    {
        if (value == null) return null;

        int len = value.Length;

        if (len == 0) return string.Empty;
        if (len > maxStringLength)
        {
            // Too long to cache, return as-is without caching
            return value;
        }

        int hash = ComputeFastHash(value);

        int index1 = hash & mask;
        ref CacheEntry entry1 = ref cache[index1];

        // Fast path:
        // 1. Compare length (cheap pre-filter, may be momentarily stale under concurrency)
        // 2. Only if length matches -> full comparison against a single, atomically-read value
        //    reference. Comparing the input against this local (not against the length field)
        //    guarantees we never return a string that is not truly equal, even if a concurrent
        //    writer is replacing the slot at the same time.
        if (entry1.length == len)
        {
            string candidate = entry1.value;
            if (candidate != null && (ReferenceEquals(value, candidate) || string.Equals(value, candidate)))
            {
                entry1.stamp = ++stampCounter;
                return candidate;
            }
        }

        int index2 = (index1 + 1) & mask;
        ref CacheEntry entry2 = ref cache[index2];

        if (entry2.length == len)
        {
            string candidate = entry2.value;
            if (candidate != null && (ReferenceEquals(value, candidate) || string.Equals(value, candidate)))
            {
                entry2.stamp = ++stampCounter;
                return candidate;
            }
        }

        return StoreNew(value, len, ref entry1, ref entry2);
    }

#if !NETSTANDARD2_0
    /// <summary>
    /// Returns a shared (interned) instance equal to <paramref name="value"/>.
    /// On a cache hit no new string is allocated; on a miss a new string is created,
    /// stored and returned.
    /// </summary>
    /// <param name="value">The character span to intern.</param>
    /// <returns>The shared cached instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Intern(ReadOnlySpan<char> value)
    {
        int len = value.Length;

        if (len == 0) return string.Empty;
        if (len > maxStringLength)
        {
            // Too long to cache, materialize directly without caching
            return value.ToString();
        }

        int hash = ComputeFastHash(value);

        int index1 = hash & mask;
        ref CacheEntry entry1 = ref cache[index1];

        // Read the value reference once (atomic) and use its own length for the comparison, so the
        // check is self-consistent even if a concurrent writer replaces the slot in between.
        if (entry1.length == len)
        {
            string candidate = entry1.value;
            if (candidate != null && candidate.Length == len && value.SequenceEqual(candidate.AsSpan()))
            {
                entry1.stamp = ++stampCounter;
                return candidate;
            }
        }

        int index2 = (index1 + 1) & mask;
        ref CacheEntry entry2 = ref cache[index2];

        if (entry2.length == len)
        {
            string candidate = entry2.value;
            if (candidate != null && candidate.Length == len && value.SequenceEqual(candidate.AsSpan()))
            {
                entry2.stamp = ++stampCounter;
                return candidate;
            }
        }

        return StoreNew(value.ToString(), len, ref entry1, ref entry2);
    }
#endif

    /// <summary>
    /// Selects a victim entry and stores the new value into it.
    /// </summary>
    /// <remarks>
    /// This method is best-effort under concurrency. Two threads may pick the same victim and
    /// overwrite each other; the outcome is always a valid interned string in the slot, only the
    /// hit ratio may suffer briefly. Fields are published value-first so a reader observing the new
    /// <c>length</c> also observes the matching new <c>value</c>; correctness does not depend on       
    /// this ordering because readers always re-validate against the value reference they read.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    string StoreNew(string value, int len, ref CacheEntry entry1, ref CacheEntry entry2)
    {
        // Select victim entry (simple 2-way LRU-like policy). Snapshots may be stale under
        // concurrency, which only affects which slot is chosen, never correctness.
        ref CacheEntry victim = ref entry1;

        if (entry1.value == null)
        {
            // keep entry1
        }
        else if (entry2.value == null)
        {
            victim = ref entry2;
        }
        else if (entry2.stamp < entry1.stamp)
        {
            victim = ref entry2;
        }

        // Store new entry (value first, then the metadata that guards the fast path).
        victim.value = value;
        victim.length = len;
        victim.stamp = ++stampCounter;

        return value;
    }

    /// <summary>
    /// Clears all cache entries and resets replacement state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        Array.Clear(cache, 0, cache.Length);
        stampCounter = 0;
    }

    #if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int ComputeFastHash(string value) => ComputeFastHash(value.AsSpan());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int ComputeFastHash(ReadOnlySpan<char> value)
    {
        int count = value.Length;

        if (count == 0) return 17;

        unchecked
        {
            ref char valueRef = ref MemoryMarshal.GetReference(value);

            // For very short strings, full hash keeps quality and is still cheap.
            if (count <= 8)
            {
                int h = 17;
                for (int i = 0; i < count; i++)
                {
                    h = h * 23 + Unsafe.Add(ref valueRef, i);
                }
                return h;
            }

            // Sampled hash: much less work for larger strings.
            int h2 = 17;
            h2 = h2 * 23 + count;
            h2 = h2 * 23 + Unsafe.Add(ref valueRef, 0);                   // first
            h2 = h2 * 23 + Unsafe.Add(ref valueRef, count >> 2);          // 25%
            h2 = h2 * 23 + Unsafe.Add(ref valueRef, count >> 1);          // 50%
            h2 = h2 * 23 + Unsafe.Add(ref valueRef, (count * 3) >> 2);    // 75%
            h2 = h2 * 23 + Unsafe.Add(ref valueRef, count - 1);           // last
            return h2;
        }
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int ComputeFastHash(string value)
    {
        int count = value.Length;

        if (count == 0) return 17;

        unchecked
        {
            // For very short strings, full hash keeps quality and is still cheap.
            if (count <= 8)
            {
                int h = 17;
                for (int i = 0; i < count; i++)
                {
                    h = h * 23 + value[i];
                }
                return h;
            }

            // Sampled hash: much less work for larger strings.
            int h2 = 17;
            h2 = h2 * 23 + count;
            h2 = h2 * 23 + value[0];                   // first
            h2 = h2 * 23 + value[count >> 2];          // 25%
            h2 = h2 * 23 + value[count >> 1];          // 50%
            h2 = h2 * 23 + value[(count * 3) >> 2];    // 75%
            h2 = h2 * 23 + value[count - 1];           // last
            return h2;
        }
    }
#endif
}
