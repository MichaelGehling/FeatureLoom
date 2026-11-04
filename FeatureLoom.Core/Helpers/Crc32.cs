using System;

namespace FeatureLoom.Helpers;

/// <summary>
/// CRC-32 (IEEE 802.3, polynomial 0xEDB88320, as used by zip/gzip/PNG). Table-based, allocation-free.
/// Supports incremental computation: pass the result of a previous call as <c>crc</c> to continue.
/// </summary>
public static class Crc32
{
    private static readonly uint[] table = CreateTable();

    private static uint[] CreateTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    /// <summary>Computes the CRC of <paramref name="count"/> bytes, optionally continuing a previous CRC.</summary>
    public static uint Compute(byte[] data, int offset, int count, uint crc = 0)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || count < 0 || offset > data.Length - count) throw new ArgumentOutOfRangeException(nameof(count), "Range exceeds the buffer.");

        uint c = ~crc;
        int end = offset + count;
        for (int i = offset; i < end; i++) c = table[(c ^ data[i]) & 0xFF] ^ (c >> 8);
        return ~c;
    }

    /// <summary>Computes the CRC of the whole array.</summary>
    public static uint Compute(byte[] data) => Compute(data, 0, data?.Length ?? 0);
}
