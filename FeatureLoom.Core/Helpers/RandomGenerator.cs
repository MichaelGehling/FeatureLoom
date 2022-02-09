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
                return Rng.Next();
            }
        }

        public static int Int32(int min, int max)
        {
            return Rng.Next(min, max);
        }

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
                return (long)(Double() * long.MaxValue);
            }
        }

        public static long Int64(long min, long max)
        {
            return (long)Double(min, max);
        }

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

        public static double Double(double min, double max)
        {
            double sample = Rng.NextDouble();
            return (max * sample) + (min * (1d - sample));
        }

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