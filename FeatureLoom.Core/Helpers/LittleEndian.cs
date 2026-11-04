using System;

namespace FeatureLoom.Helpers;

/// <summary>
/// Reads and writes unsigned integers of 1-8 bytes in little-endian byte order, independent of the platform's endianness.
/// Works on byte arrays on all targets (no System.Memory dependency on netstandard2.0).
/// </summary>
public static class LittleEndian
{
    /// <summary>Writes the lowest <paramref name="size"/> bytes of <paramref name="value"/> at <paramref name="offset"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Size is not 1-8 or the range exceeds the buffer.</exception>
    public static void Write(byte[] buffer, int offset, ulong value, int size)
    {
        CheckRange(buffer, offset, size);
        for (int i = 0; i < size; i++)
        {
            buffer[offset + i] = (byte)value;
            value >>= 8;
        }
    }

    /// <summary>Reads an unsigned integer of <paramref name="size"/> bytes at <paramref name="offset"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Size is not 1-8 or the range exceeds the buffer.</exception>
    public static ulong Read(byte[] buffer, int offset, int size)
    {
        CheckRange(buffer, offset, size);
        ulong value = 0;
        for (int i = size - 1; i >= 0; i--)
        {
            value = (value << 8) | buffer[offset + i];
        }
        return value;
    }

    /// <summary>Writes a 16-bit value.</summary>
    public static void WriteUInt16(byte[] buffer, int offset, ushort value) => Write(buffer, offset, value, 2);
    /// <summary>Writes a 32-bit value.</summary>
    public static void WriteUInt32(byte[] buffer, int offset, uint value) => Write(buffer, offset, value, 4);
    /// <summary>Writes a 64-bit value.</summary>
    public static void WriteUInt64(byte[] buffer, int offset, ulong value) => Write(buffer, offset, value, 8);
    /// <summary>Reads a 16-bit value.</summary>
    public static ushort ReadUInt16(byte[] buffer, int offset) => (ushort)Read(buffer, offset, 2);
    /// <summary>Reads a 32-bit value.</summary>
    public static uint ReadUInt32(byte[] buffer, int offset) => (uint)Read(buffer, offset, 4);
    /// <summary>Reads a 64-bit value.</summary>
    public static ulong ReadUInt64(byte[] buffer, int offset) => Read(buffer, offset, 8);

    private static void CheckRange(byte[] buffer, int offset, int size)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (size < 1 || size > 8) throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be between 1 and 8.");
        if (offset < 0 || offset > buffer.Length - size) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Range exceeds the buffer.");
    }
}
