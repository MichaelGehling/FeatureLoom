using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Collections;

/// <summary>
/// Very fast, fixed-size string cache for <see cref="ByteSegment"/> keys.
/// Uses direct-index probing with 2 adjacent candidate slots and
/// a lightweight age-based replacement policy.
/// </summary>
/// <remarks>
/// This cache is optimized for throughput and low allocation behavior, not for perfect hit ratio.
/// Capacity is fixed at construction time as <c>2^hashSizeInBits</c>.
/// </remarks>
public sealed class QuickStringCache
{
    /// <summary>
    /// Cache entry containing key/value and metadata for fast lookup/replacement.
    /// </summary>
    struct CacheEntry
    {
        /// <summary>The cached key.</summary>
        public ByteSegment key;

        /// <summary>The cached string value for <see cref="key"/>.</summary>
        public string value;

        /// <summary>Monotonic access/update marker used for victim selection.</summary>
        public uint stamp;
    }

    readonly CacheEntry[] cache;
    readonly int mask;
    readonly int maxStringLength;
    uint stampCounter;

    /// <summary>
    /// Initializes a new <see cref="QuickStringCache"/>.
    /// </summary>
    /// <param name="hashSizeInBits">
    /// Cache size exponent. Effective size is <c>2^hashSizeInBits</c>, e.g. 8 for 256 entries, 10 for 1024 entries, 16 for 65536 entries.
    /// Clamped to range [8..16].
    /// To calculate the maximum size of the cache, consider the average string length and the memory overhead of each entry. 
    /// For example, with an average string length of 20 characters (40 bytes) and a cache size of 1024 entries, 
    /// the memory usage would be approximately 40 KB for the strings plus additional overhead for the cache structure itself.
    /// E.g. the worst case for a string length of max 128 characters (256 bytes) and 65536 entries would be around 16 MB for the strings plus overhead.
    /// Recommended is 12bit for 4096 entries, which provides a good balance between hit ratio and memory usage for typical scenarios and a maximum string length of around 128 characters
    /// which results in around 512 KB memory usage for the strings plus overhead.
    /// That said, the optimal size depends on the specific use case, including the expected number of unique strings, their average length, and the acceptable memory usage.
    /// </param>
    /// <param name="maxStringLength">
    /// Maximum length of strings to cache. Strings longer than this will not be cached.
    /// </param>
    public QuickStringCache(int hashSizeInBits, int maxStringLength)
    {
        hashSizeInBits = hashSizeInBits.Clamp(8, 16);
        cache = new CacheEntry[1 << hashSizeInBits];
        mask = cache.Length - 1;
        this.maxStringLength = maxStringLength;
    }

    /// <summary>
    /// Returns the cached string for the provided segment, or creates/stores it on miss.
    /// </summary>
    /// <param name="segment">The byte segment key.</param>
    /// <param name="stringBuilder">Optional <see cref="StringBuilder"/> for decoding.</param>
    /// <returns>The cached or newly created string value.</returns>
    /// <remarks>
    /// On miss, the key is copied via <c>CropArray(true)</c> to detach from larger backing arrays.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetOrCreate(ByteSegment segment, StringBuilder stringBuilder = null)
    {
        if (segment.Count > maxStringLength)
        {
            // Too long to cache, decode directly without caching
            return Utf8Converter.DecodeUtf8ToString(segment, stringBuilder);
        }
        int hash = ComputeFastHash(segment);
        segment.SetCustomHashCode(hash);

        int index1 = hash & mask;
        ref CacheEntry entry1 = ref cache[index1];
        if (entry1.key.Equals(segment))
        {
            entry1.stamp = ++stampCounter;
            return entry1.value;
        }

        int index2 = (index1 + 1) & mask;
        ref CacheEntry entry2 = ref cache[index2];
        if (entry2.key.Equals(segment))
        {
            entry2.stamp = ++stampCounter;
            return entry2.value;
        }

        ByteSegment cropped = segment.CropArray(true);
        string value = Utf8Converter.DecodeUtf8ToString(cropped, stringBuilder);

        ref CacheEntry victim = ref entry1;
        if (!entry1.key.IsValid)
        {
            // keep victim = entry1
        }
        else if (!entry2.key.IsValid)
        {
            victim = ref entry2;
        }
        else if (entry2.stamp < victim.stamp)
        {
            victim = ref entry2;
        }

        victim.key = cropped;
        victim.value = value;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int ComputeFastHash(ByteSegment segment)
    {
        var s = segment.AsArraySegment;
        var arr = s.Array;
        int count = s.Count;

        if (arr == null) return 0;
        if (count == 0) return 17;

        int start = s.Offset;

        unchecked
        {
            ref byte arrayRef = ref arr[0];

            // For very short strings, full hash keeps quality and is still cheap.
            if (count <= 8)
            {
                int h = 17;
                int end = start + count;
                for (int i = start; i < end; i++)
                {
                    h = h * 23 + Unsafe.Add(ref arrayRef, i);
                }
                return h;
            }

            // Sampled hash: much less work for larger segments.
            int h2 = 17;
            h2 = h2 * 23 + count;
            h2 = h2 * 23 + Unsafe.Add(ref arrayRef, start);                         // first
            h2 = h2 * 23 + Unsafe.Add(ref arrayRef, start + (count >> 2));          // 25%
            h2 = h2 * 23 + Unsafe.Add(ref arrayRef, start + (count >> 1));          // 50%
            h2 = h2 * 23 + Unsafe.Add(ref arrayRef, start + ((count * 3) >> 2));    // 75%
            h2 = h2 * 23 + Unsafe.Add(ref arrayRef, start + count - 1);              // last
            return h2;
        }
    }
}
