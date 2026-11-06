using System;

namespace FeatureLoom.Helpers;

/// <summary>
/// CRC-32 (IEEE 802.3, polynomial 0xEDB88320, as used by zip/gzip/PNG). Table-based (slicing-by-8), allocation-free.
/// Supports incremental computation: pass the result of a previous call as <c>crc</c> to continue.
/// </summary>
public static class Crc32
{
    // Slicing-by-8: table[k * 256 + b] is the CRC of byte b followed by k zero bytes; processes 8 bytes per step.
    private static readonly uint[] table = CreateTable();

    private static uint[] CreateTable()
    {
        var t = new uint[8 * 256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        for (int i = 0; i < 256; i++)
        {
            for (int k = 1; k < 8; k++)
            {
                uint prev = t[(k - 1) * 256 + i];
                t[k * 256 + i] = t[prev & 0xFF] ^ (prev >> 8);
            }
        }
        return t;
    }

    /// <summary>Computes the CRC of <paramref name="count"/> bytes, optionally continuing a previous CRC.</summary>
    public static uint Compute(byte[] data, int offset, int count, uint crc = 0)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || count < 0 || offset > data.Length - count) throw new ArgumentOutOfRangeException(nameof(count), "Range exceeds the buffer.");

        var t = table;
        uint c = ~crc;
        int i = offset;
        int end = offset + count;
        int end8 = end - 8;
        while (i <= end8)
        {
            uint low = c ^ (uint)(data[i] | data[i + 1] << 8 | data[i + 2] << 16 | data[i + 3] << 24);
            uint high = (uint)(data[i + 4] | data[i + 5] << 8 | data[i + 6] << 16 | data[i + 7] << 24);
            c = t[7 * 256 + (low & 0xFF)] ^ t[6 * 256 + ((low >> 8) & 0xFF)] ^ t[5 * 256 + ((low >> 16) & 0xFF)] ^ t[4 * 256 + (low >> 24)]
              ^ t[3 * 256 + (high & 0xFF)] ^ t[2 * 256 + ((high >> 8) & 0xFF)] ^ t[1 * 256 + ((high >> 16) & 0xFF)] ^ t[high >> 24];
            i += 8;
        }
        for (; i < end; i++) c = t[(c ^ data[i]) & 0xFF] ^ (c >> 8);
        return ~c;
    }

    /// <summary>Computes the CRC of the whole array.</summary>
    public static uint Compute(byte[] data) => Compute(data, 0, data?.Length ?? 0);
}
