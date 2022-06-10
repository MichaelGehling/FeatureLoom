using FeatureLoom.Extensions;
using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FeatureLoom.Helpers
{
    public static class RandomGenerator
    {
        [ThreadStatic]
        private static Random _rng;

        [ThreadStatic]
        static RandomNumberGenerator _cryptoRng;        
        

        private static Random Rng
        {
            get 
            {
                if (_rng == null) _rng = new Random(Guid.NewGuid().GetHashCode());
                return _rng;
            }
        }

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
        /// Returns a ramdom bool value.
        /// </summary>
        /// <param name="probability">Defines the chance of returning the value 'true'. Must be between 0.0 (never) and 1.0 (always).</param>
        /// <returns></returns>
        public static bool Bool(double probability = 0.5)
        {
            return Rng.NextDouble() + probability >= 1.0;
        }

        /// <summary>
        /// Returns a random signed 32bit integer value in the range of int.MinValue (inclusive) to int.MaxValue (exclusive).
        /// </summary>
        /// <param name="crypto">If true, the value is created by a cryptographic number generator and may be used for security task.</param>
        public static int Int32(bool crypto = false)
        {
            if (crypto)
            {
                byte[] bytes = new byte[4];
                CryptoRng.GetBytes(bytes);
                return BitConverter.ToInt32(bytes, 0);
            }
            else
            {
                return Rng.Next(int.MinValue, int.MaxValue);
            }
        }

        /// <summary>
        /// Returns a random signed 16bit integer value in the range of short.MinValue (inclusive) to short.MaxValue (exclusive).
        /// </summary>
        public static short Int16()
        {
            return (short)Rng.Next(short.MinValue, short.MaxValue);
        }

        /// <summary>
        /// Returns a random signed 32bit integer value in the range of min (inclusive) to max (inclusive).
        /// </summary>
        /// <param name="min">the smallest possbile value</param>
        /// <param name="max">the biggest possible value (must not be bigger than int.MaxValue-1)</param>
        public static int Int32(int min, int max)
        {
            return Rng.Next(min, max+1);
        }

        /// <summary>
        /// Returns a random signed 16bit integer value in the range of min (inclusive) to max (inclusive).
        /// </summary>
        /// <param name="min">the smallest possbile value</param>
        /// <param name="max">the biggest possible value</param>
        public static short Int16(short min, short max)
        {
            return (short) Rng.Next(min, ((int)max)+1);
        }

        /// <summary>
        /// Returns a random signed 64bit integer value in the range of long.MinValue (inclusive) to long.MaxValue (exclusive).
        /// </summary>
        /// <param name="crypto">If true, the value is created by a cryptographic number generator and may be used for security task.</param>
        public static long Int64(bool crypto = false)
        {
            if (crypto)
            {
                byte[] bytes = new byte[8];
                CryptoRng.GetBytes(bytes);
                return BitConverter.ToInt64(bytes, 0);
            }
            else
            {
                return (long)((Double() * 2.0 - 1.0) * long.MaxValue);
            }
        }

        /// <summary>
        /// Returns a random signed 64bit integer value in the range of min (inclusive) to max (inclusive).
        /// </summary>
        /// <param name="min">the smallest possbile value</param>
        /// <param name="max">the biggest possible value (must not be bigger than long.MaxValue-1)</param>
        public static long Int64(long min, long max)
        {
            return (long)Double(min, max+1);
        }

        /// <summary>
        /// Returns a random 64bit floating point value in the range of 0.0 (inclusive) to 1.0 (exclusive).
        /// Cryptographic values may have any possible value in the range of Double.
        /// </summary>
        /// <param name="crypto">If true, the value is created by a cryptographic number generator and may be used for security task.</param>
        public static double Double(bool crypto = false)
        {
            if (crypto)
            {                
                byte[] bytes = new byte[8];
                CryptoRng.GetBytes(bytes);
                return BitConverter.ToDouble(bytes, 0);
            }
            else
            {
                return Rng.NextDouble();
            }
        }

        /// <summary>
        /// Returns a random 64bit floating point value in the range of min (inclusive) to max (inclusive).
        /// </summary>
        /// <param name="min">the smallest possbile value</param>
        /// <param name="max">the biggest possible value</param>
        public static double Double(double min, double max)
        {
            double sample = Rng.NextDouble();
            return (max * sample) + (min * (1.0 - sample));
        }

        /// <summary>
        /// Returns a random GUID.
        /// </summary>        
        /// <param name="crypto">If true, the GUID is created by a cryptographic number generator and may be used for security task.</param>
        public static Guid GUID(bool crypto = false)
        {
            if (crypto)
            {
                byte[] bytes = new byte[16];
                CryptoRng.GetBytes(bytes);
                return new Guid(bytes);
            }
            else
            {
                return Guid.NewGuid();
            }
        }

        /// <summary>
        /// Returns an array of random byte values.
        /// </summary>
        /// <param name="length">The number of bytes in the array</param>
        /// <param name="crypto">If true, the bytes are created by a cryptographic number generator and may be used for security task.</param>
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
        /// Generates random values inside a given byte array.
        /// </summary>
        /// <param name="bytes">The byte array to be filled with random values</param>
        /// <param name="offset">The start index</param>
        /// <param name="length">The number of generated values</param>
        /// <param name="crypto">If true, the bytes are created by a cryptographic number generator and may be used for security task.</param>
        /// <returns>The orginal passed in array parameter</returns>
        public static byte[] Bytes(byte[] bytes, int offset, int length, bool crypto = false)
        {
            if (crypto)
            {
                CryptoRng.GetBytes(bytes, offset, length);
                return bytes;
            }
            else if (offset == 0 && length == bytes.Length)
            {
#if NETSTANDARD2_1_OR_GREATER                
                Rng.NextBytes(bytes.AsSpan<byte>(offset, length));                
                return bytes;
#elif NETSTANDARD2_0
                byte[] randomBytes = new byte[length];
                Rng.NextBytes(randomBytes);
                randomBytes.CopyTo(bytes, offset);
                return bytes;
#else
#error Target Framework not supported
#endif
            }
            else
            {
                Rng.NextBytes(bytes);
                return bytes;
            }
        }

    }
}