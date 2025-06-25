using FeatureLoom.Extensions;
using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
#if !NETSTANDARD2_0
using System.Buffers;
#endif
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FeatureLoom.Helpers;

/// <summary>
/// Provides static methods for generating random values of various types, supporting both standard and cryptographically secure randomness.
/// Includes efficient buffer pooling for reduced allocations.
/// </summary>
public static class RandomGenerator
{
    [ThreadStatic]
    private static Random _rng;

    [ThreadStatic]
    static RandomNumberGenerator _cryptoRng;

    // Pools for temporary byte buffers used in random value generation.
    static Pool<byte[]> sixteenBytesPool = new Pool<byte[]>(() => new byte[16], null, 32, true);
    static Pool<byte[]> eightBytesPool = new Pool<byte[]>(() => new byte[8], null, 32, true);
    static Pool<byte[]> fourBytesPool = new Pool<byte[]>(() => new byte[4], null, 32, true);

    /// <summary>
    /// Gets a thread-local instance of <see cref="Random"/>.
    /// </summary>
    private static Random Rng
    {
        get
        {
            if (_rng == null) _rng = new Random(Guid.NewGuid().GetHashCode());
            return _rng;
        }
    }

    /// <summary>
    /// Gets a thread-local instance of <see cref="RandomNumberGenerator"/>.
    /// </summary>
    private static RandomNumberGenerator CryptoRng
    {
        get
        {
            var rng = _cryptoRng;
            if (rng == null)
            {
                rng = RandomNumberGenerator.Create();
                _cryptoRng = rng;
            }
            return rng;
        }
    }

    /// <summary>
    /// Resets the thread-local random number generator with a specific seed.
    /// </summary>
    /// <param name="seed">The seed value to use for the random number generator.</param>
    public static void Reset(int seed)
    {
        _rng = new Random(seed);
    }

    /// <summary>
    /// Returns a random boolean value.
    /// </summary>
    /// <param name="probability">The probability of returning <c>true</c>. Must be between 0.0 (never) and 1.0 (always).</param>
    /// <returns><c>true</c> with the specified probability; otherwise, <c>false</c>.</returns>
    public static bool Bool(double probability = 0.5)
    {
        return Rng.NextDouble() < probability;
    }

    /// <summary>
    /// Returns a random signed 32-bit integer in the range of <see cref="int.MinValue"/> (inclusive) to <see cref="int.MaxValue"/> (exclusive).
    /// </summary>
    /// <param name="crypto">If <c>true</c>, uses a cryptographically secure random number generator.</param>
    /// <returns>A random 32-bit integer.</returns>
    public static int Int32(bool crypto = false)
    {
        if (crypto)
        {
            byte[] bytes = fourBytesPool.Take();
            CryptoRng.GetBytes(bytes);
            int result = BitConverter.ToInt32(bytes, 0);
            fourBytesPool.Return(bytes);
            return result;
        }
        else
        {
            return Rng.Next(int.MinValue, int.MaxValue);
        }
    }

    /// <summary>
    /// Returns a random signed 16-bit integer in the range of <see cref="short.MinValue"/> (inclusive) to <see cref="short.MaxValue"/> (exclusive).
    /// </summary>
    /// <returns>A random 16-bit integer.</returns>
    public static short Int16()
    {
        return (short)Rng.Next(short.MinValue, short.MaxValue);
    }

    /// <summary>
    /// Returns a random signed 32-bit integer in the specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound (must not exceed <see cref="int.MaxValue"/> - 1).</param>
    /// <returns>A random 32-bit integer between <paramref name="min"/> and <paramref name="max"/> (inclusive).</returns>
    public static int Int32(int min, int max)
    {
        max = max.ClampHigh(int.MaxValue - 1);
        return Rng.Next(min, max + 1);
    }

    /// <summary>
    /// Returns a random signed 16-bit integer in the specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns>A random 16-bit integer between <paramref name="min"/> and <paramref name="max"/> (inclusive).</returns>
    public static short Int16(short min, short max)
    {
        max = max.ClampHigh((short)(short.MaxValue - 1));
        return (short)Rng.Next(min, ((int)max) + 1);
    }

    /// <summary>
    /// Returns a random signed 64-bit integer in the range of <see cref="long.MinValue"/> (inclusive) to <see cref="long.MaxValue"/> (exclusive).
    /// </summary>
    /// <param name="crypto">If <c>true</c>, uses a cryptographically secure random number generator.</param>
    /// <returns>A random 64-bit integer.</returns>
    public static long Int64(bool crypto = false)
    {
        if (crypto)
        {
            byte[] bytes = eightBytesPool.Take();
            CryptoRng.GetBytes(bytes);
            var result = BitConverter.ToInt64(bytes, 0);
            eightBytesPool.Return(bytes);
            return result;
        }
        else
        {
#if NET6_0_OR_GREATER
            return Rng.NextInt64(long.MinValue, long.MaxValue);
#else
            byte[] bytes = eightBytesPool.Take();
            Rng.NextBytes(bytes);
            var result = BitConverter.ToInt64(bytes, 0);
            eightBytesPool.Return(bytes);
            return result;
#endif
        }
    }

    /// <summary>
    /// Returns a random signed 64-bit integer in the specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound (must not exceed <see cref="long.MaxValue"/> - 1).</param>
    /// <returns>A random 64-bit integer between <paramref name="min"/> and <paramref name="max"/> (inclusive).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="min"/> is greater than <paramref name="max"/>.</exception>
    /// <remarks>
    /// Uses rejection sampling to ensure a uniform distribution. The method is efficient and the expected number of iterations is close to 1.
    /// </remarks>
    public static long Int64(long min, long max)
    {
#if NET6_0_OR_GREATER
        max = max.ClampHigh(long.MaxValue - 1);
        return Rng.NextInt64(min, max + 1);
#else
        if (min > max) throw new ArgumentOutOfRangeException(nameof(min), "min must be less than or equal to max");
        ulong range = (ulong)(max - min) + 1UL;
        ulong ulongRand;
        byte[] buf = eightBytesPool.Take();
        ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
        do
        {
            Rng.NextBytes(buf);
            ulongRand = BitConverter.ToUInt64(buf, 0);
        }
        while (ulongRand >= limit);
        eightBytesPool.Return(buf);
        return (long)(ulongRand % range) + min;
#endif
    }

    /// <summary>
    /// Returns a random 64-bit floating point value in the range of 0.0 (inclusive) to 1.0 (exclusive).
    /// </summary>
    /// <param name="crypto">If <c>true</c>, uses a cryptographically secure random number generator.</param>
    /// <returns>A random double in [0.0, 1.0).</returns>
    public static double Double(bool crypto = false)
    {
        if (crypto)
        {
            byte[] bytes = eightBytesPool.Take();
            CryptoRng.GetBytes(bytes);
            var result = BitConverter.ToDouble(bytes, 0);
            eightBytesPool.Return(bytes);
            return result;
        }
        else
        {
            return Rng.NextDouble();
        }
    }

    /// <summary>
    /// Returns a random 64-bit floating point value in the specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns>A random double between <paramref name="min"/> and <paramref name="max"/> (inclusive).</returns>
    public static double Double(double min, double max)
    {
        double sample = Rng.NextDouble();
        return (max * sample) + (min * (1.0 - sample));
    }

    /// <summary>
    /// Returns a random 32-bit floating point value in the range of 0.0 (inclusive) to 1.0 (exclusive).
    /// </summary>
    /// <returns>A random float in [0.0, 1.0).</returns>
    public static float Float()
    {
        return (float)Double();
    }

    /// <summary>
    /// Returns a random 32-bit floating point value in the specified range.
    /// </summary>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <returns>A random float between <paramref name="min"/> and <paramref name="max"/> (inclusive).</returns>
    public static float Float(float min, float max)
    {
        return (float)Double(min, max);
    }

    /// <summary>
    /// Returns a random <see cref="Guid"/>.
    /// </summary>
    /// <param name="crypto">If <c>true</c>, uses a cryptographically secure random number generator.</param>
    /// <returns>A random GUID.</returns>
    public static Guid GUID(bool crypto = false)
    {
        if (crypto)
        {
            byte[] bytes = sixteenBytesPool.Take();
            CryptoRng.GetBytes(bytes);
            var result = new Guid(bytes);
            sixteenBytesPool.Return(bytes);
            return result;
        }
        else
        {
            return Guid.NewGuid();
        }
    }

    /// <summary>
    /// Returns an array of random byte values.
    /// </summary>
    /// <param name="length">The number of bytes in the array.</param>
    /// <param name="crypto">If <c>true</c>, uses a cryptographically secure random number generator.</param>
    /// <returns>A byte array filled with random values.</returns>
    public static byte[] Bytes(int length, bool crypto = false)
    {
        if (crypto)
        {
            byte[] bytes = new byte[length];
            CryptoRng.GetBytes(bytes);
            return bytes;
        }
        else
        {
            byte[] bytes = new byte[length];
            Rng.NextBytes(bytes);
            return bytes;
        }
    }

    /// <summary>
    /// Fills a segment of a byte array with random values.
    /// </summary>
    /// <param name="bytes">The byte array to fill.</param>
    /// <param name="offset">The start index in the array.</param>
    /// <param name="length">The number of bytes to fill.</param>
    /// <param name="crypto">If <c>true</c>, uses a cryptographically secure random number generator.</param>
    /// <returns>The original byte array, filled with random values in the specified segment.</returns>
    public static byte[] Bytes(byte[] bytes, int offset, int length, bool crypto = false)
    {
        if (crypto)
        {
            CryptoRng.GetBytes(bytes, offset, length);
            return bytes;
        }
        else if (offset == 0 && length == bytes.Length)
        {
#if NETSTANDARD2_0
            byte[] randomBytes = new byte[length];
            Rng.NextBytes(randomBytes);
            randomBytes.CopyTo(bytes, offset);
            return bytes;
#else
            Rng.NextBytes(bytes.AsSpan<byte>(offset, length));
            return bytes;
#endif
        }
        else
        {
            Rng.NextBytes(bytes);
            return bytes;
        }
    }

    private const string DefaultCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// Generates a random string of a specified length using an optional set of allowed characters.
    /// </summary>
    /// <param name="length">The desired length of the resulting string.</param>
    /// <param name="crypto">If <c>true</c>, uses a cryptographically secure random number generator.</param>
    /// <param name="allowedChars">An optional set of characters to use. If not provided, a default set of alphanumeric characters is used.</param>
    /// <returns>A random string of the specified length composed of characters from the provided or default character set.</returns>
    public static string String(int length, bool crypto = false, string allowedChars = null)
    {
        if (length <= 0) return string.Empty;

        string characterSet = allowedChars ?? DefaultCharacters;
        int maxValidByteValue = 256 - (256 % characterSet.Length);  // Highest byte value that doesn't introduce bias
#if NETSTANDARD2_0
        char[] chars = new char[length];
        // Allocate a byte buffer with twice the required length as initial size to allow skipping invalid values
        byte[] randomBytes = new byte[2 * length];
#else
        char[] chars = ArrayPool<char>.Shared.Rent(length);
        // Allocate a byte buffer with twice the required length as initial size to allow skipping invalid values
        byte[] randomBytes = ArrayPool<byte>.Shared.Rent(2 * length);
#endif
        int charsFilled = 0;

        
        int numGeneratedBytes;

        while (charsFilled < length)
        {
            numGeneratedBytes = 2 * (length - charsFilled);
            Bytes(randomBytes, 0, numGeneratedBytes, crypto);  // Fill the buffer with random bytes

            for (int i = 0; i < numGeneratedBytes && charsFilled < length; i++)
            {
                // Ensure the value is in the calculated limit to avoid a bias, otherwise skip this byte
                if (randomBytes[i] < maxValidByteValue)
                {
                    chars[charsFilled] = characterSet[randomBytes[i] % characterSet.Length];
                    charsFilled++;
                }
            }
        }

        var result = new string(chars, 0, length);
#if !NETSTANDARD2_0
        ArrayPool<char>.Shared.Return(chars, true);
        ArrayPool<byte>.Shared.Return(randomBytes, true);
#endif
        return result;
    }
}