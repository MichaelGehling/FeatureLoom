using System;

namespace FeatureFlowFramework.Helper
{
    public static class RandomGenerator
    {
        private static Random rnd = new Random();

        public static int Int32
        {
            get
            {
                lock (rnd)
                {
                    return rnd.Next();
                }
            }
        }

        public static long Int64
        {
            get
            {
                lock (rnd)
                {
                    return (long)(rnd.NextDouble() * Int64.MaxValue);
                }
            }
        }

        public static double Double
        {
            get
            {
                lock (rnd)
                {
                    return rnd.NextDouble();
                }
            }
        }
    }
}