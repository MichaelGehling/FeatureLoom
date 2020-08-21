using FeatureFlowFramework.Helpers.Synchronization;
using System;

namespace FeatureFlowFramework.Services
{
    public static class RandomGenerator
    {
        private static Random rnd = new Random();
        private static FeatureLock myLock = new FeatureLock();

        public static int Int32()
        {
            using(myLock.Lock())
            {
                return rnd.Next();
            }
        }

        public static int Int32(int min, int max)
        {
            using(myLock.Lock())
            {
                return rnd.Next(min, max);
            }
        }

        public static long Int64()
        {
            using(myLock.Lock())
            {
                return (long)(rnd.NextDouble() * long.MaxValue);
            }
        }

        public static double Double()
        {
            using(myLock.Lock())
            {
                return rnd.NextDouble();
            }
        }
    }
}