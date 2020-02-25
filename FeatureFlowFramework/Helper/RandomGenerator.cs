using System;

namespace FeatureFlowFramework.Helper
{
    public static class RandomGenerator
    {
        private static Random rnd = new Random();
        private static RWLock myLock = new RWLock();

        public static int Int32()
        {
            using (myLock.ForWriting())
            {
                return rnd.Next();
            }
        }

        public static int Int32(int min, int max)
        {
            using (myLock.ForWriting())
            {
                return rnd.Next(min, max);
            }
        }

        public static long Int64()
        {
            using (myLock.ForWriting())
            {
                return (long)(rnd.NextDouble() * long.MaxValue);
            }            
        }

        public static double Double()
        {
            using (myLock.ForWriting())
            {
                return rnd.NextDouble();
            }
        }
    }
}