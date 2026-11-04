namespace FeatureLoom.LogStorage;

/// <summary>
/// Stable hash used for <see cref="LogFieldKind.Hash"/> fields: 64-bit FNV-1a over the UTF-8 bytes of the source value,
/// truncated to the field size. Part of the file format; must never change.
/// </summary>
public static class LogFieldHash
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    /// <summary>Computes the stored value for <paramref name="source"/>; null maps to 0.</summary>
    /// <param name="source">Source value.</param>
    /// <param name="maxValue">Largest value of the target field (see <see cref="LogFieldDefinition.MaxValue"/>).</param>
    public static ulong Compute(string source, ulong maxValue)
    {
        if (source == null) return 0;
        ulong hash = OffsetBasis;
        // UTF-8 is encoded inline to avoid allocating a byte array per value.
        for (int i = 0; i < source.Length; i++)
        {
            int c = source[i];
            if (c < 0x80)
            {
                hash = (hash ^ (uint)c) * Prime;
            }
            else if (c < 0x800)
            {
                hash = (hash ^ (uint)(0xC0 | (c >> 6))) * Prime;
                hash = (hash ^ (uint)(0x80 | (c & 0x3F))) * Prime;
            }
            else if (char.IsHighSurrogate((char)c) && i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]))
            {
                int cp = char.ConvertToUtf32((char)c, source[++i]);
                hash = (hash ^ (uint)(0xF0 | (cp >> 18))) * Prime;
                hash = (hash ^ (uint)(0x80 | ((cp >> 12) & 0x3F))) * Prime;
                hash = (hash ^ (uint)(0x80 | ((cp >> 6) & 0x3F))) * Prime;
                hash = (hash ^ (uint)(0x80 | (cp & 0x3F))) * Prime;
            }
            else
            {
                // Lone surrogates are encoded as U+FFFD, like Encoding.UTF8.
                if (c >= 0xD800 && c <= 0xDFFF) c = 0xFFFD;
                hash = (hash ^ (uint)(0xE0 | (c >> 12))) * Prime;
                hash = (hash ^ (uint)(0x80 | ((c >> 6) & 0x3F))) * Prime;
                hash = (hash ^ (uint)(0x80 | (c & 0x3F))) * Prime;
            }
        }
        return hash & maxValue;
    }
}
