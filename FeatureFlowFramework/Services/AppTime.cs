using FeatureFlowFramework.Helpers.Time;
using System;
using System.Diagnostics;

namespace FeatureFlowFramework.Services
{
    public static class AppTime
    {
        private static Stopwatch stopWatch = new Stopwatch();
        public static Func<DateTime> timeFactory = () => DateTime.UtcNow;

        static AppTime()
        {
            stopWatch.Start();
        }
        
        public static DateTime Now => timeFactory();
        public static TimeSpan Elapsed => stopWatch.Elapsed;
        public static TimeKeeper TimeKeeper => new TimeKeeper(Elapsed);
    }
}