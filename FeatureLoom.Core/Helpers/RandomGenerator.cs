using FeatureLoom.Extensions;
using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.Helpers
{
    public static class RandomGenerator
    {
        [ThreadStatic]
        private static Random rnd;

        private static Random Rnd
        {
            get 
            {
                if (rnd == null) rnd = new Random(Guid.NewGuid().GetHashCode());
                return rnd;
            }
        }

        public static int Int32()
        {
            return Rnd.Next();
        }

        public static int Int32(int min, int max)
        {
            return Rnd.Next(min, max);
        }

        public static long Int64()
        {
            return (long)(Double() * long.MaxValue);            
        }

        public static long Int64(long min, long max)
        {
            return (long)Double(min, max);
        }

        public static double Double()
        {            
            return Rnd.NextDouble();            
        }

        public static double Double(double min, double max)
        {
            double sample = Rnd.NextDouble();
            return (max * sample) + (min * (1d - sample));
        }

        public static Guid GUID()
        {
            return Guid.NewGuid();
        }

    }
}